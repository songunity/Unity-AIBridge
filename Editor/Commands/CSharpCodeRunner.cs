using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Provides functionality to execute C# code at runtime within Unity.
/// </summary>
public sealed class CSharpCodeRunner
{
    private readonly List<MetadataReference> references = new List<MetadataReference>();
    public const int CacheCapacity = 64;
    private readonly Dictionary<string, MethodInfo> compiledMethods = new Dictionary<string, MethodInfo>();
    private readonly Queue<string> cacheOrder = new Queue<string>();
    private string referenceFingerprint;
    private static readonly CSharpCompilationOptions EditorCompileOptions = new CSharpCompilationOptions(
        OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug, allowUnsafe: true);

    public int CachedEntryCount => compiledMethods.Count;
    public int CacheHits { get; private set; }
    public int CompilationCount { get; private set; }
    public int ReferenceBuildCount { get; private set; }
    private const string AsyncMethodName = "ExecuteAsync";

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpCodeRunner"/> class.
    /// </summary>
    public CSharpCodeRunner()
    {
        RefreshReferences();
    }

    /// <summary>
    /// 按程序集身份、路径、MVID 和文件版本刷新引用；只在引用变化时重建元数据并清空编译缓存。
    /// 在 Editor 主线程调用，动态探针不参与引用集合，避免每次执行都使缓存失效。
    /// </summary>
    private void RefreshReferences()
    {
        var allAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => !x.IsDynamic && !string.IsNullOrEmpty(x.Location)
                && !x.GetName().Name.StartsWith("DynamicAssembly_", StringComparison.Ordinal))
            .ToList();

        var assemblyNames = new HashSet<string>(allAssemblies.Select(a => a.GetName().Name));

        var filtered = allAssemblies.Where(x =>
        {
            var name = x.GetName().Name;
            if (name.EndsWith("_AIBridge"))
            {
                var baseName = name.Substring(0, name.Length - "_AIBridge".Length);
                return !assemblyNames.Contains(baseName);
            }
            return true;
        }).OrderBy(x => x.FullName, StringComparer.Ordinal)
            .ThenBy(x => x.Location, StringComparer.Ordinal).ToArray();

        var identity = new StringBuilder();
        foreach (var assembly in filtered)
        {
            var file = new FileInfo(assembly.Location);
            identity.Append(assembly.FullName).Append('\0').Append(assembly.Location).Append('\0')
                .Append(assembly.ManifestModule.ModuleVersionId).Append('\0')
                .Append(file.Exists ? file.Length : -1).Append('\0')
                .Append(file.Exists ? file.LastWriteTimeUtc.Ticks : 0).Append('\n');
        }
        var fingerprint = identity.ToString();
        if (referenceFingerprint == fingerprint)
            return;

        references.Clear();
        compiledMethods.Clear();
        cacheOrder.Clear();
        ReferenceBuildCount++;
        foreach (var assembly in filtered)
        {
            try
            {
                this.references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            catch (Exception)
            {
            }
        }
        referenceFingerprint = fingerprint;
    }

    /// <summary>
    /// Wraps the specified code in a class with a static method.
    /// </summary>
    /// <param name="code">The code to wrap.</param>
    /// <returns>The wrapped code.</returns>
    private string WrapCodeInClass(string code)
    {
        var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();
        var usings = new StringBuilder();
        foreach (var directive in root.Usings)
            usings.AppendLine(directive.ToString());

        // 只移除语法树中的 using 指令；保留 using 声明、字符串、注释及原有换行。
        var body = new StringBuilder(code);
        foreach (var directive in root.Usings.Reverse())
        {
            for (var i = directive.Span.Start; i < directive.Span.End; i++)
                if (body[i] != '\r' && body[i] != '\n') body[i] = ' ';
        }

        // 在异步方法上下文解析，支持 await 换行、await using 和 await foreach。
        // 嵌套函数中的 await 不要求外层入口也为 async。
        var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            "public async Task<object> ExecuteAsync() {\n" + body + "\n}");
        var hasAwait = method.Body.DescendantTokens(node =>
                !(node is AnonymousFunctionExpressionSyntax) && !(node is LocalFunctionStatementSyntax))
            .Any(token => token.IsKind(SyntaxKind.AwaitKeyword));
        var methodName = hasAwait ? AsyncMethodName : "Execute";
        var returnType = hasAwait ? "async Task<object>" : "object";
        return $@"
{usings}
using System;
using System.Threading.Tasks;
public static class CodeExecutor
{{
    public static {returnType} {methodName}()
    {{
        {body}
        return null;
    }}
}}
";
    }

    /// <summary>
    /// Compiles the specified C# code and returns the raw assembly bytes without executing.
    /// Used for runtime_execute where the DLL is sent to a remote Player.
    /// </summary>
    public EvaluationResult CompileToBytes(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = "Code cannot be null or empty"
            };
        }

        var wrappedCode = this.WrapCodeInClass(code);
        return this.CompileCodeToBytes(wrappedCode);
    }

    private EvaluationResult CompileCodeToBytes(string code)
    {
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Debug,
            allowUnsafe: true);

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "DynamicAssembly_" + Guid.NewGuid().ToString("N"),
            new[] { syntaxTree },
            this.references,
            options);

        using (var ms = new MemoryStream())
        {
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToArray();

                return new EvaluationResult
                {
                    Success = false,
                    ErrorMessage = string.Join(Environment.NewLine, errors)
                };
            }

            return new EvaluationResult
            {
                Success = true,
                AssemblyBytes = ms.ToArray()
            };
        }
    }

    /// <summary>最近一次入口准备耗时，不包含业务方法执行。</summary>
    public long LastPreparationMs { get; private set; }
    /// <summary>最近一次入口是否复用了已编译方法。</summary>
    public bool LastCacheHit { get; private set; }

    /// <summary>准备编译入口并执行；每次调用产生独立结果。</summary>
    public EvaluationResult CompileAndExecute(string code)
    {
        var preparation = System.Diagnostics.Stopwatch.StartNew();
        LastPreparationMs = 0;
        LastCacheHit = false;
        if (string.IsNullOrEmpty(code))
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = "Code cannot be null or empty"
            };
        }

        RefreshReferences();
        var wrappedCode = this.WrapCodeInClass(code);
        // 引用变化时整体失效，编译选项固定，包装后的代码即可作为缓存键。
        var key = wrappedCode;

        if (!compiledMethods.TryGetValue(key, out var method))
        {
            CompilationCount++;
            var result = this.CompileCode(wrappedCode);
            if (!result.Success)
            {
                LastPreparationMs = preparation.ElapsedMilliseconds;
                return result;
            }

            var type = result.CompiledAssembly.GetType("CodeExecutor");
            method = type?.GetMethod("Execute") ?? type?.GetMethod(AsyncMethodName);
            if (method == null)
            {
                return new EvaluationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to find the Execute method"
                };
            }
            // 只限制缓存持有量；Mono 中已经加载的程序集不能通过清空字典卸载。
            if (compiledMethods.Count >= CacheCapacity)
                compiledMethods.Remove(cacheOrder.Dequeue());
            compiledMethods.Add(key, method);
            cacheOrder.Enqueue(key);
        }
        else
        {
            CacheHits++;
            LastCacheHit = true;
        }

        // 每次重新执行入口并生成结果和 Task，不复用上一次读取到的状态。
        LastPreparationMs = preparation.ElapsedMilliseconds;
        return ExecuteCompiledMethod(method);
    }

    /// <summary>
    /// 执行已编译入口；异步任务及返回值仅属于当前请求。
    /// </summary>
    private EvaluationResult ExecuteCompiledMethod(MethodInfo method)
    {
        try
        {
            var returnValue = method.Invoke(null, null);
            if (returnValue is Task task)
            {
                if (!task.IsCompleted)
                {
                    return new EvaluationResult
                    {
                        Success = false,
                        IsPending = true,
                        PendingTask = task
                    };
                }

                returnValue = GetTaskResult(task);
            }

            return new EvaluationResult
            {
                Success = true,
                ReturnValue = returnValue
            };
        }
        catch (TargetInvocationException ex)
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = $"Runtime error: {ex.InnerException?.Message ?? ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = $"Runtime error: {ex.Message}"
            };
        }
    }

    public EvaluationResult ContinuePendingTask(EvaluationResult pendingResult)
    {
        if (pendingResult?.PendingTask == null)
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = "Pending task is missing"
            };
        }

        if (!pendingResult.PendingTask.IsCompleted)
        {
            return pendingResult;
        }

        try
        {
            return new EvaluationResult
            {
                Success = true,
                ReturnValue = GetTaskResult(pendingResult.PendingTask)
            };
        }
        catch (Exception ex)
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = $"Runtime error: {ex.Message}"
            };
        }
    }

    private object GetTaskResult(Task task)
    {
        task.GetAwaiter().GetResult();
        if (task.GetType().IsGenericType)
        {
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return null;
    }

    /// <summary>
    /// Compiles the specified C# code.
    /// </summary>
    /// <param name="code">The C# code to compile.</param>
    /// <returns>The result of the compilation.</returns>
    private EvaluationResult CompileCode(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "DynamicAssembly_" + Guid.NewGuid().ToString("N"),
            new[] { syntaxTree },
            this.references,
            EditorCompileOptions);

        using (var ms = new MemoryStream())
        {
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToArray();

                return new EvaluationResult
                {
                    Success = false,
                    ErrorMessage = string.Join(Environment.NewLine, errors)
                };
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            return new EvaluationResult
            {
                Success = true,
                CompiledAssembly = assembly
            };
        }
    }
}

/// <summary>
/// Represents the result of a code evaluation.
/// </summary>
public sealed class EvaluationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the evaluation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the return value from the executed code.
    /// </summary>
    public object ReturnValue { get; set; }

    /// <summary>
    /// Gets or sets the error message if evaluation failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    public bool IsPending { get; set; }

    /// <summary>
    /// Gets or sets the compiled assembly.
    /// </summary>
    internal Assembly CompiledAssembly { get; set; }

    /// <summary>
    /// Gets or sets the raw compiled assembly bytes (for remote execution).
    /// </summary>
    public byte[] AssemblyBytes { get; set; }

    internal Task PendingTask { get; set; }
}

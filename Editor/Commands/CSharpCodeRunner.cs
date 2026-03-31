using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Provides functionality to execute C# code at runtime within Unity.
/// </summary>
public sealed class CSharpCodeRunner
{
    private readonly List<MetadataReference> references;
    private const string AsyncMethodName = "ExecuteAsync";

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpCodeRunner"/> class.
    /// </summary>
    public CSharpCodeRunner()
    {
        this.references = new List<MetadataReference>();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => !x.IsDynamic && !string.IsNullOrEmpty(x.Location));

        foreach (var assembly in assemblies)
        {
            try
            {
                this.references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            catch (Exception)
            {
                // Skip assemblies that cannot be referenced
            }
        }
    }

    /// <summary>
    /// Wraps the specified code in a class with a static method.
    /// </summary>
    /// <param name="code">The code to wrap.</param>
    /// <returns>The wrapped code.</returns>
    private string WrapCodeInClass(string code)
    {
        var matches = Regex.Matches(code, $"(using.*?;)");
        StringBuilder sb = new StringBuilder();
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                sb.AppendLine(match.Groups[1].Value);
            }
        }

        code = Regex.Replace(code, "using.*?;", "");
        var methodName = code.Contains("await ") ? AsyncMethodName : "Execute";
        var returnType = methodName == AsyncMethodName ? "async Task<object>" : "object";
        return $@"
{sb}
using System;
using System.Threading.Tasks;
public static class CodeExecutor
{{
    public static {returnType} {methodName}()
    {{
        try{{
        {code}
        }}
        catch (Exception e)
        {{
            UnityEngine.Debug.LogError(e.ToString());
            return e.ToString();
        }}
        return null;
    }}
}}
";
    }

    /// <summary>
    /// Compiles and executes the specified C# code.
    /// </summary>
    /// <param name="code">The C# code to compile and execute.</param>
    /// <returns>The result of the compilation and execution.</returns>
    public EvaluationResult CompileAndExecute(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = "Code cannot be null or empty"
            };
        }

        // Wrap the code in a class with a static method that returns the result
        var wrappedCode = this.WrapCodeInClass(code);

        // Compile the code
        var result = this.CompileCode(wrappedCode);

        if (!result.Success)
        {
            return result;
        }

        // Execute the compiled code
        try
        {
            return ExecuteCompiledAssembly(result.CompiledAssembly);
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

    private EvaluationResult ExecuteCompiledAssembly(Assembly assembly)
    {
        if (assembly == null)
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = "Failed to compile the code"
            };
        }

        var type = assembly.GetType("CodeExecutor");
        if (type == null)
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = "Failed to find the CodeExecutor type"
            };
        }

        var method = type.GetMethod("Execute") ?? type.GetMethod(AsyncMethodName);
        if (method == null)
        {
            return new EvaluationResult
            {
                Success = false,
                ErrorMessage = "Failed to find the Execute method"
            };
        }

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
            return new EvaluationResult
            {
                Success = false,
                IsPending = true,
                PendingTask = pendingResult.PendingTask
            };
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

    internal Task PendingTask { get; set; }
}
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using AIBridge.Editor;
using UnityEngine;

public static class CodeExecuteCommand
{
    [AIBridge("this command is use to run code, both editor or runtime. pass code or file. 如果脚本内容过多更建议写入文件来运行，脚本文件放到AIBridgeCache/code中", 
        example:@"
AIBridge.exe CodeExecuteCommand_Execute --code 'using UnityEngine;Debug.Log(""Log"");Debug.LogWarning(""Warning"");'

// 上边代码是你需要提供的逻辑，不需要写方法，只需要写using和逻辑
// 以上的代码会被编译成下边的
using UnityEngine;

public static class CodeExecutor
{{
    public static object Execute()
    {{
        Debug.Log(""Log"");
        Debug.LogWarning(""Warning"");
        return null;
    }}
}}
")]
    public static IEnumerator Execute([Description("code to execute")]string code = null, [Description("file to execute, need full path")]string file = null)
    {
        CSharpCodeRunner codeRunner = new CSharpCodeRunner();
        if (!string.IsNullOrEmpty(file))
        {
            if (File.Exists(file))
            {
                code = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(code))
                {
                    yield return CommandResult.Failure("File is empty.");
                }
            }
            else
            {
                yield return CommandResult.Failure("File is not exist.");
            }
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            yield return CommandResult.Failure("Code is null or empty.");
        }

        // Capture logs during execution
        var logMessages = new List<string>();
        var logHandler = new Application.LogCallback((logString, stackTrace, type) =>
        {
            var prefix = type switch
            {
                LogType.Error or LogType.Exception => "[ERROR] ",
                LogType.Warning => "[WARNING] ",
                _ => "[INFO] "
            };
            logMessages.Add(prefix + logString);
        });

        Application.logMessageReceived += logHandler;

        // Execute on main thread
        var result = codeRunner.CompileAndExecute(code);
        
        // Remove log handler
        Application.logMessageReceived -= logHandler;
        var output = string.Join("\n", logMessages);
        
        if (!result.Success)
        {
            yield return CommandResult.Failure($"Execution Failed:\n{result.ErrorMessage}\nOutput:\n{output}");
        }
        else
        {
            yield return CommandResult.Success($"Output:\n{output}");
        }
    }
}
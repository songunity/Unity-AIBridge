using System.Text.Json;

namespace AIBridgeCLI.Commands;

public static class CompileUnityCommand
{
    public static CommandResult Compile(int timeout = 120000)
    {
        var sender = new CommandSender(timeout: timeout);
        var startResult = sender.SendCommand(new CommandRequest()
        {
            id = PathHelper.GenerateCommandId(),
            type = "CompileCommand_Start",
        });
        if (!startResult.success)
        {
            return startResult;
        }

        var startTime = DateTime.Now;
        while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
        {
            try
            {
                var stateResult = sender.SendCommand(new CommandRequest()
                {
                    id = PathHelper.GenerateCommandId(),
                    type = "CompileCommand_Status",
                });
                if (!stateResult.success)
                {
                    return stateResult;
                }

                var state = ((JsonElement)stateResult.data).GetProperty("status").GetString();
                if (state != "compiling")
                {
                    return stateResult;
                }
                Thread.Sleep(500);
            }
            catch (Exception e)
            {
                return new CommandResult()
                {
                    id = startResult.id,
                    success = false,
                    error = e.Message,
                };
            }
        }

        return new CommandResult()
        {
            id = startResult.id,
            success = false,
            error = $"Compile timed out after {timeout}ms",
        };
    }
}
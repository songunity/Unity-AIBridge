using System.Text.Json;

namespace AIBridgeCLI.Commands;

public static class CompileUnityCommand
{
    public static CommandResult Compile()
    {
        var sender = new CommandSender(timeout: 120000);
        var startResult = sender.SendCommand(new CommandRequest()
        {
            id = PathHelper.GenerateCommandId(),
            type = "CompileCommand_Start",
        });
        if (!startResult.success)
        {
            return startResult;
        }


        while (true)
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
    }
}
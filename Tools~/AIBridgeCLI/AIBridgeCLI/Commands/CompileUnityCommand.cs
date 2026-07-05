using System.Text.Json;

namespace AIBridgeCLI.Commands;

public static class CompileUnityCommand
{
    private const int DefaultPollInterval = 500;
    private const int DefaultTransportTimeout = 30000;

    public static CommandResult Compile(int timeout = 120000)
    {
        var transportTimeout = Math.Min(DefaultTransportTimeout, Math.Max(DefaultPollInterval, timeout));
        var sender = new CommandSender(timeout: transportTimeout);
        var startResult = sender.SendCommand(new CommandRequest()
        {
            id = PathHelper.GenerateCommandId(),
            type = "CompileCommand_Start",
        });

        var startTime = DateTime.Now;
        var startedByInvocation = false;
        var attachedToExistingCompilation = false;
        var observedCompilationActivity = false;
        var startCommunicationTimedOut = false;
        string lastCommunicationError = null;

        if (startResult.success)
        {
            startedByInvocation = TryReadBool(startResult.data, "compilationStarted");
            attachedToExistingCompilation = TryReadBool(startResult.data, "alreadyCompiling");
            observedCompilationActivity = startedByInvocation || attachedToExistingCompilation;
        }
        else if (IsTransportTimeout(startResult.error))
        {
            startCommunicationTimedOut = true;
            lastCommunicationError = startResult.error;
        }
        else
        {
            return startResult;
        }

        while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
        {
            CommandResult stateResult;
            try
            {
                stateResult = sender.SendCommand(new CommandRequest()
                {
                    id = PathHelper.GenerateCommandId(),
                    type = "CompileCommand_Status",
                });
            }
            catch (Exception e)
            {
                lastCommunicationError = e.Message;
                Thread.Sleep(DefaultPollInterval);
                continue;
            }

            if (!stateResult.success)
            {
                lastCommunicationError = stateResult.error;
                Thread.Sleep(DefaultPollInterval);
                continue;
            }

            var state = TryReadString(stateResult.data, "status");
            var isCompiling = TryReadBool(stateResult.data, "isCompiling");

            if (state == "compiling" || isCompiling)
            {
                observedCompilationActivity = true;
                Thread.Sleep(DefaultPollInterval);
                continue;
            }

            if (!observedCompilationActivity && (string.IsNullOrEmpty(state) || state == "idle" || state == "unknown"))
            {
                if (startCommunicationTimedOut)
                {
                    return new CommandResult()
                    {
                        id = stateResult.id,
                        success = false,
                        error = "Compile start was not confirmed, and Unity is idle now.",
                        data = AddPollingMetadata(
                            stateResult.data,
                            startedByInvocation,
                            attachedToExistingCompilation,
                            statusConfirmed: true,
                            startCommunicationTimedOut,
                            lastCommunicationError)
                    };
                }

                Thread.Sleep(DefaultPollInterval);
                continue;
            }

            stateResult.data = AddPollingMetadata(
                stateResult.data,
                startedByInvocation,
                attachedToExistingCompilation,
                statusConfirmed: true,
                startCommunicationTimedOut,
                lastCommunicationError);
            return stateResult;
        }

        return new CommandResult()
        {
            id = startResult.id,
            success = false,
            error = lastCommunicationError == null
                ? $"Compile timed out after {timeout}ms"
                : $"Compile timed out after {timeout}ms. Last communication error: {lastCommunicationError}",
            data = AddPollingMetadata(
                null,
                startedByInvocation,
                attachedToExistingCompilation,
                statusConfirmed: false,
                startCommunicationTimedOut,
                lastCommunicationError)
        };
    }

    private static bool IsTransportTimeout(string error)
    {
        return !string.IsNullOrEmpty(error)
               && error.Contains("Timeout waiting for result", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadBool(object data, string propertyName)
    {
        if (data is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return element.TryGetProperty(propertyName, out var property)
               && property.ValueKind is JsonValueKind.True or JsonValueKind.False
               && property.GetBoolean();
    }

    private static string TryReadString(object data, string propertyName)
    {
        if (data is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static object AddPollingMetadata(
        object data,
        bool startedByInvocation,
        bool attachedToExistingCompilation,
        bool statusConfirmed,
        bool startCommunicationTimedOut,
        string lastCommunicationError)
    {
        var metadata = new Dictionary<string, object>
        {
            ["startedByInvocation"] = startedByInvocation,
            ["attachedToExistingCompilation"] = attachedToExistingCompilation,
            ["statusConfirmed"] = statusConfirmed,
            ["startCommunicationTimedOut"] = startCommunicationTimedOut
        };

        if (!string.IsNullOrEmpty(lastCommunicationError))
        {
            metadata["lastCommunicationError"] = lastCommunicationError;
        }

        if (data is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                metadata[property.Name] = property.Value.Clone();
            }
        }

        return metadata;
    }
}

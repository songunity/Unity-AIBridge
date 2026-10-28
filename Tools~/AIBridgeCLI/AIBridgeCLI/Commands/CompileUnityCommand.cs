using System.Text.Json;
using System.Diagnostics;

namespace AIBridgeCLI.Commands;

public static class CompileUnityCommand
{
    private const int DefaultPollInterval = 500;
    private const int DefaultTransportTimeout = 30000;

    public static CommandResult Compile(int timeout = 120000)
    {
        if (timeout <= 0) throw new ArgumentOutOfRangeException(nameof(timeout));
        var total = Stopwatch.StartNew();
        var transportTimeout = Math.Min(DefaultTransportTimeout, timeout);
        var sender = new CommandSender(timeout: transportTimeout);
        var startResult = sender.SendCommand(new CommandRequest()
        {
            id = PathHelper.GenerateCommandId(),
            type = "CompileCommand_Start",
        });
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
            return WithTotal(startResult, total);
        }

        while (total.ElapsedMilliseconds < timeout)
        {
            var remaining = timeout - (int)total.ElapsedMilliseconds;
            sender = new CommandSender(timeout: Math.Min(DefaultTransportTimeout, Math.Max(1, remaining)));
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
                Thread.Sleep(Math.Min(DefaultPollInterval, Math.Max(0, timeout - (int)total.ElapsedMilliseconds)));
                continue;
            }

            if (!stateResult.success)
            {
                lastCommunicationError = stateResult.error;
                Thread.Sleep(Math.Min(DefaultPollInterval, Math.Max(0, timeout - (int)total.ElapsedMilliseconds)));
                continue;
            }

            var state = TryReadString(stateResult.data, "status");
            var isCompiling = TryReadBool(stateResult.data, "isCompiling");

            if (state == "compiling" || isCompiling)
            {
                observedCompilationActivity = true;
                Thread.Sleep(Math.Min(DefaultPollInterval, Math.Max(0, timeout - (int)total.ElapsedMilliseconds)));
                continue;
            }

            if (!observedCompilationActivity && (string.IsNullOrEmpty(state) || state == "idle" || state == "unknown"))
            {
                if (startCommunicationTimedOut)
                {
                    return WithTotal(new CommandResult()
                    {
                        protocolVersion = 2,
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
                    }, total);
                }

                Thread.Sleep(Math.Min(DefaultPollInterval, Math.Max(0, timeout - (int)total.ElapsedMilliseconds)));
                continue;
            }

            stateResult.data = AddPollingMetadata(
                stateResult.data,
                startedByInvocation,
                attachedToExistingCompilation,
                statusConfirmed: true,
                startCommunicationTimedOut,
                lastCommunicationError);
            return WithTotal(stateResult, total);
        }

        return WithTotal(new CommandResult()
        {
            protocolVersion = 2,
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
        }, total);
    }

    private static CommandResult WithTotal(CommandResult result, Stopwatch timer)
    {
        var timings = result.timings is JsonElement element && element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone())
            : result.timings as Dictionary<string, object> ?? new Dictionary<string, object>();
        timings["compileTotalMs"] = timer.ElapsedMilliseconds;
        result.timings = timings;
        return result;
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

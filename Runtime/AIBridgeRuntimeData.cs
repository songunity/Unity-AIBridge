using System;
using System.Collections.Generic;

namespace AIBridge.Runtime
{
    /// <summary>
    /// Runtime command data structure for communication between Editor and Runtime.
    /// </summary>
    [Serializable]
    public class AIBridgeRuntimeCommand
    {
        /// <summary>
        /// Unique command ID for tracking
        /// </summary>
        public string Id;

        /// <summary>
        /// Command action type (e.g., trigger_event, get_panels, custom actions)
        /// </summary>
        public string Action;

        /// <summary>
        /// Panel name for targeting UI elements (optional, for UI frameworks)
        /// </summary>
        public string PanelName;

        /// <summary>
        /// Event name to trigger (optional, for UI frameworks)
        /// </summary>
        public string EventName;

        /// <summary>
        /// View name (optional, for targeting View events)
        /// </summary>
        public string ViewName;

        /// <summary>
        /// Generic parameters dictionary for extensibility
        /// </summary>
        public Dictionary<string, object> Params;

        /// <summary>
        /// Timestamp when command was created
        /// </summary>
        public long Timestamp;

        /// <summary>
        /// Get parameter value with type conversion
        /// </summary>
        public T GetParam<T>(string key, T defaultValue = default)
        {
            if (Params == null || !Params.TryGetValue(key, out var value))
            {
                return defaultValue;
            }

            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    /// <summary>
    /// Runtime command execution result.
    /// </summary>
    [Serializable]
    public class AIBridgeRuntimeCommandResult
    {
        /// <summary>
        /// Corresponding command ID
        /// </summary>
        public string CommandId;

        /// <summary>
        /// Whether command execution was successful
        /// </summary>
        public bool Success;

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string Error;

        /// <summary>
        /// Result data (JSON serializable)
        /// </summary>
        public object Data;

        /// <summary>
        /// Whether command has completed execution
        /// </summary>
        public bool Completed;

        /// <summary>
        /// Execution timestamp
        /// </summary>
        public long Timestamp;

        public AIBridgeRuntimeCommandResult()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public AIBridgeRuntimeCommandResult(string commandId) : this()
        {
            CommandId = commandId;
        }

        public static AIBridgeRuntimeCommandResult FromSuccess(string commandId, object data = null)
        {
            return new AIBridgeRuntimeCommandResult(commandId)
            {
                Success = true,
                Completed = true,
                Data = data
            };
        }

        public static AIBridgeRuntimeCommandResult FromFailure(string commandId, string error)
        {
            return new AIBridgeRuntimeCommandResult(commandId)
            {
                Success = false,
                Completed = true,
                Error = error
            };
        }
    }

    /// <summary>
    /// Panel information for query results (compatible with UI framework integrations).
    /// </summary>
    [Serializable]
    public class AIBridgePanelInfo
    {
        public string PanelName;
        public string PkgName;
        public string ResName;
        public string Layer;
        public bool IsActive;
        public string[] Events;
    }
}

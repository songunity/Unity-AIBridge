using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Logger utility for AI Bridge with consistent prefix
    /// </summary>
    public static class AIBridgeLogger
    {
        private const string PREFIX = "[AIBridge]";

        /// <summary>
        /// Enable or disable debug logging
        /// </summary>
        public static bool DebugEnabled { get; set; } = false;

        public static void LogDebug(string message)
        {
            if (DebugEnabled)
            {
                Debug.Log($"{PREFIX} {message}");
            }
        }

        public static void LogInfo(string message)
        {
            Debug.Log($"{PREFIX} {message}");
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning($"{PREFIX} {message}");
        }

        public static void LogError(string message)
        {
            Debug.LogError($"{PREFIX} {message}");
        }
    }
}

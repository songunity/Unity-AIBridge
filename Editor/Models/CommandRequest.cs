using System;
using System.Collections.Generic;

namespace AIBridge.Editor
{
    /// <summary>
    /// Command request model received from AI Code assistant
    /// </summary>
    [Serializable]
    public class CommandRequest
    {
        /// <summary>
        /// Unique command identifier
        /// </summary>
        public string id;

        /// <summary>
        /// Command type (e.g., "execute_code", "menu_item")
        /// </summary>
        public string type;

        /// <summary>
        /// Command parameters as key-value pairs
        /// </summary>
        public Dictionary<string, object> @params;

        /// <summary>
        /// Command timeout in milliseconds (default: 60000 = 60 seconds)
        /// </summary>
        public int timeout = 60000;

        /// <summary>
        /// Get parameter value by key
        /// </summary>
        public T GetParam<T>(string key, T defaultValue = default)
        {
            if (@params == null || !@params.TryGetValue(key, out var value))
            {
                return defaultValue;
            }

            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                // Handle numeric type conversions
                if (typeof(T) == typeof(int) && value is long longVal)
                {
                    return (T)(object)(int)longVal;
                }
                if (typeof(T) == typeof(float) && value is double doubleVal)
                {
                    return (T)(object)(float)doubleVal;
                }
                if (typeof(T) == typeof(int) && value is double dVal)
                {
                    return (T)(object)(int)dVal;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Check if parameter exists
        /// </summary>
        public bool HasParam(string key)
        {
            return @params != null && @params.ContainsKey(key);
        }
    }
}

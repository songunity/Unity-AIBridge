using System;

namespace AIBridge.Editor
{
    /// <summary>
    /// Command execution result returned to AI Code assistant
    /// </summary>
    [Serializable]
    public class CommandResult
    {
        /// <summary>Editor 与 CLI 的结构化通信版本。</summary>
        public int protocolVersion = 2;
        /// <summary>可供调用脚本判断的错误类别。</summary>
        public string errorCode;
        /// <summary>命令执行状态；unknown 不表示未执行。</summary>
        public string status;
        /// <summary>以毫秒为单位的分段耗时。</summary>
        public object timings;
        /// <summary>对应提交请求的唯一标识。</summary>
        public string id;

        /// <summary>
        /// Whether command executed successfully
        /// </summary>
        public bool success;

        /// <summary>
        /// Result data (command-specific)
        /// </summary>
        public object data;

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string error;

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public long executionTime;

        /// <summary>
        /// Create a successful result
        /// </summary>
        public static CommandResult Success(object data = null)
        {
            return SuccessWithId(null, data);
        }
        
        public static CommandResult SuccessWithId(string id, object data = null)
        {
            return new CommandResult
            {
                id = id,
                success = true,
                data = data,
                error = null
            };
        }

        /// <summary>
        /// Create a failed result
        /// </summary>
        public static CommandResult Failure(string error)
        {
            return FailureWithId(null, error);
        }

        public static CommandResult FailureWithId(string id, string error)
        {
            return new CommandResult
            {
                id = id,
                success = false,
                errorCode = "COMMAND_FAILED",
                data = null,
                error = error
            };
        }

        /// <summary>
        /// Create a failed result from exception
        /// </summary>
        public static CommandResult FromException(string id, Exception ex)
        {
            return new CommandResult
            {
                id = id,
                success = false,
                data = null,
                errorCode = "EXECUTION_FAILED",
                error = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
            };
        }
    }
}

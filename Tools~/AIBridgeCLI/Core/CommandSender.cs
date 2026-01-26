using System;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace AIBridgeCLI.Core
{
    /// <summary>
    /// Handles sending commands and receiving results
    /// </summary>
    public class CommandSender
    {
        private readonly string _commandsDir;
        private readonly string _resultsDir;
        private readonly int _timeout;
        private readonly int _pollInterval;

        /// <summary>
        /// Create a new CommandSender
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
        /// <param name="pollInterval">Poll interval in milliseconds (default: 50)</param>
        public CommandSender(int timeout = 5000, int pollInterval = 50)
        {
            _commandsDir = PathHelper.GetCommandsDirectory();
            _resultsDir = PathHelper.GetResultsDirectory();
            _timeout = timeout;
            _pollInterval = pollInterval;

            PathHelper.EnsureDirectoriesExist();
        }

        /// <summary>
        /// Send a command and wait for result
        /// </summary>
        public CommandResult SendCommand(CommandRequest request)
        {
            if (string.IsNullOrEmpty(request.id))
            {
                request.id = PathHelper.GenerateCommandId();
            }

            var commandFile = Path.Combine(_commandsDir, $"{request.id}.json");
            var resultFile = Path.Combine(_resultsDir, $"{request.id}.json");

            // Write command file with UTF-8 encoding (no BOM)
            var json = JsonConvert.SerializeObject(request, Formatting.None);
            File.WriteAllText(commandFile, json, new UTF8Encoding(false));

            // Wait for result
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < _timeout)
            {
                if (File.Exists(resultFile))
                {
                    // Small delay to ensure file is fully written
                    Thread.Sleep(10);

                    try
                    {
                        var resultJson = File.ReadAllText(resultFile, Encoding.UTF8);
                        var result = JsonConvert.DeserializeObject<CommandResult>(resultJson);

                        // Clean up result file
                        try { File.Delete(resultFile); } catch { }

                        return result;
                    }
                    catch (IOException)
                    {
                        // File might still be locked, retry
                        Thread.Sleep(_pollInterval);
                        continue;
                    }
                }

                Thread.Sleep(_pollInterval);
            }

            // Timeout - clean up command file if still exists
            try { File.Delete(commandFile); } catch { }

            return new CommandResult
            {
                id = request.id,
                success = false,
                error = $"Timeout waiting for result after {_timeout}ms. Make sure Unity Editor is running and AIBridge is active."
            };
        }

        /// <summary>
        /// Send a command without waiting for result
        /// </summary>
        public string SendCommandNoWait(CommandRequest request)
        {
            if (string.IsNullOrEmpty(request.id))
            {
                request.id = PathHelper.GenerateCommandId();
            }

            var commandFile = Path.Combine(_commandsDir, $"{request.id}.json");

            // Write command file with UTF-8 encoding (no BOM)
            var json = JsonConvert.SerializeObject(request, Formatting.None);
            File.WriteAllText(commandFile, json, new UTF8Encoding(false));

            return request.id;
        }

        /// <summary>
        /// Check if a result is available for a given command ID
        /// </summary>
        public CommandResult TryGetResult(string commandId)
        {
            var resultFile = Path.Combine(_resultsDir, $"{commandId}.json");

            if (!File.Exists(resultFile))
            {
                return null;
            }

            try
            {
                var resultJson = File.ReadAllText(resultFile, Encoding.UTF8);
                var result = JsonConvert.DeserializeObject<CommandResult>(resultJson);

                // Clean up result file
                try { File.Delete(resultFile); } catch { }

                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}

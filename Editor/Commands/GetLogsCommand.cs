using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class GetLogsCommand
    {
        private static bool _capturing;
        private static readonly List<LogEntry> _logs = new List<LogEntry>();

        [AIBridge("Get console logs from Unity Editor",
            "AIBridgeCLI Log --count 50",
            "Log")]
        public static IEnumerator Log(
            [Description("Log type filter: All, Error, Warning, Log")] string logType = "All",
            [Description("Text filter (substring match)")] string filter = null,
            [Description("Maximum number of logs to return")] int count = 50)
        {
            if (_capturing)
            {
                IEnumerable<LogEntry> results = _logs;
                if (!logType.Equals("All", StringComparison.OrdinalIgnoreCase))
                    results = results.Where(l => MatchesType(l.type, logType));
                if (!string.IsNullOrEmpty(filter))
                    results = results.Where(l => l.message.Contains(filter));

                var captured = results.TakeLast(count).Select(l => new
                {
                    type = l.type.ToString(),
                    message = l.message,
                    time = l.time.ToString("HH:mm:ss.fff")
                }).ToArray();

                yield return CommandResult.Success(new { count = captured.Length, logs = captured });
            }
            else
            {
                int targetMask = 0;
                if (logType == "All" || logType.Contains("Error"))   targetMask |= DebugSkills.ErrorModeMask;
                if (logType == "All" || logType.Contains("Warning")) targetMask |= DebugSkills.WarningModeMask;
                if (logType == "All" || logType.Contains("Log"))     targetMask |= DebugSkills.LogModeMask;

                var log = DebugSkills.ReadLogEntries(targetMask, filter, count);
                if (log.Count <= 0)
                {
                    yield return CommandResult.Success("No logs found");
                }
                else
                {
                    yield return CommandResult.Success(log);
                }
            }
        }

        [AIBridge("Start capturing Unity console logs into buffer",
            "AIBridgeCLI GetLogsCommand_StartCapture")]
        public static IEnumerator StartCapture()
        {
            if (!_capturing)
            {
                Application.logMessageReceived += OnLogMessage;
                _capturing = true;
            }
            _logs.Clear();
            yield return CommandResult.Success(new { success = true, message = "Console capture started" });
        }

        [AIBridge("Stop capturing Unity console logs",
            "AIBridgeCLI GetLogsCommand_StopCapture")]
        public static IEnumerator StopCapture()
        {
            if (_capturing)
            {
                Application.logMessageReceived -= OnLogMessage;
                _capturing = false;
            }
            yield return CommandResult.Success(new { success = true, message = "Console capture stopped", capturedCount = _logs.Count });
        }

        private static bool MatchesType(LogType logType, string typeFilter)
        {
            switch (typeFilter)
            {
                case "Error": return logType == LogType.Error || logType == LogType.Exception || logType == LogType.Assert;
                case "Warning": return logType == LogType.Warning;
                case "Log": return logType == LogType.Log;
                default: return true;
            }
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            _logs.Add(new LogEntry { message = message, stackTrace = stackTrace, type = type, time = DateTime.Now });
            if (_logs.Count > 1000) _logs.RemoveAt(0);
        }

        private class LogEntry
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public DateTime time;
        }
    }
}

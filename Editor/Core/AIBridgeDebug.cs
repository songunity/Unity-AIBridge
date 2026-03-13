using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace AIBridge.Editor
{
    /// <summary>
    /// Debug skills - self-healing, active error checking, compilation control.
    /// </summary>
    public static class DebugSkills
    {
        // Unity LogEntry mode bits (from UnityCsReference)
        // Error=1, Assert=2, Log=4, Fatal=16,
        // DontPreprocessCondition=32, AssetImportError=64, AssetImportWarning=128,
        // ScriptingError=256, ScriptingWarning=512, ScriptingLog=1024,
        // ScriptCompileError=2048, ScriptCompileWarning=4096,
        // ScriptingException=131072
        private const int ModeError               = 1;
        private const int ModeAssert              = 2;
        private const int ModeLog                 = 4;
        private const int ModeFatal               = 16;
        private const int ModeAssetImportError    = 64;
        private const int ModeAssetImportWarning  = 128;
        private const int ModeScriptingError      = 256;
        private const int ModeScriptingWarning    = 512;
        private const int ModeScriptingLog        = 1024;
        private const int ModeScriptCompileError  = 2048;
        private const int ModeScriptCompileWarning = 4096;
        private const int ModeScriptingException  = 131072;

        internal const int ErrorModeMask   = ModeError | ModeAssert | ModeFatal | ModeAssetImportError | ModeScriptingError | ModeScriptCompileError | ModeScriptingException;
        internal const int WarningModeMask = ModeAssetImportWarning | ModeScriptingWarning | ModeScriptCompileWarning;
        internal const int LogModeMask     = ModeLog | ModeScriptingLog;

        // Cached reflection members (initialized on first use, cleared on failure to allow retry)
        private static System.Type _logEntriesType;
        private static System.Type _logEntryType;
        private static MethodInfo  _getCountMethod;
        private static MethodInfo  _getEntryMethod;
        private static MethodInfo  _startMethod;
        private static MethodInfo  _endMethod;
        private static FieldInfo   _modeField;
        private static FieldInfo   _messageField;
        private static FieldInfo   _fileField;
        private static FieldInfo   _lineField;

        internal static bool EnsureReflection()
        {
            if (_getEntryMethod != null) return true;

            var asm = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
            _logEntriesType = asm?.GetType("UnityEditor.LogEntries");
            _logEntryType   = asm?.GetType("UnityEditor.LogEntry");

            if (_logEntriesType == null || _logEntryType == null)
            {
                AIBridgeLogger.LogError("DebugSkills: UnityEditor.LogEntries or LogEntry type not found.");
                return false;
            }

            var staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            _getCountMethod = _logEntriesType.GetMethod("GetCount",             staticFlags);
            _getEntryMethod = _logEntriesType.GetMethod("GetEntryInternal",     staticFlags);
            _startMethod    = _logEntriesType.GetMethod("StartGettingEntries",  staticFlags);
            _endMethod      = _logEntriesType.GetMethod("EndGettingEntries",    staticFlags);

            var instFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            _modeField    = _logEntryType.GetField("mode",    instFlags);
            _messageField = _logEntryType.GetField("message", instFlags);
            _fileField    = _logEntryType.GetField("file",    instFlags);
            _lineField    = _logEntryType.GetField("line",    instFlags);

            bool ok = _getCountMethod != null && _getEntryMethod != null &&
                      _startMethod    != null && _endMethod      != null &&
                      _modeField      != null && _messageField   != null &&
                      _fileField      != null && _lineField      != null;

            if (!ok)
            {
                AIBridgeLogger.LogError("DebugSkills: Failed to reflect required members of LogEntries/LogEntry.");
                // Clear everything so the next call will retry
                _logEntriesType = null;
                _logEntryType   = null;
                _getCountMethod = null;
                _getEntryMethod = null;
                _startMethod    = null;
                _endMethod      = null;
                _modeField      = null;
                _messageField   = null;
                _fileField      = null;
                _lineField      = null;
            }

            return ok;
        }

        internal static List<object> ReadLogEntries(int targetMask, string filter, int limit)
        {
            var results = new List<object>();
            if (!EnsureReflection()) return results;

            var entry = System.Activator.CreateInstance(_logEntryType);
            _startMethod.Invoke(null, null);
            try
            {
                int count = (int)_getCountMethod.Invoke(null, null);
                int found = 0;
                for (int i = count - 1; i >= 0 && found < limit; i--)
                {
                    _getEntryMethod.Invoke(null, new object[] { i, entry });
                    int mode = (int)_modeField.GetValue(entry);
                    if ((mode & targetMask) == 0) continue;

                    string msg  = (string)_messageField.GetValue(entry) ?? "";
                    if (!string.IsNullOrEmpty(filter) && !msg.Contains(filter)) continue;

                    string file = (string)_fileField.GetValue(entry) ?? "";
                    int    line = (int)_lineField.GetValue(entry);

                    string logType = (mode & ErrorModeMask)   != 0 ? "Error"
                                   : (mode & WarningModeMask) != 0 ? "Warning"
                                   : "Log";

                    results.Add(new
                    {
                        type    = logType,
                        message = msg.Length > 500 ? msg.Substring(0, 500) + "..." : msg,
                        file,
                        line
                    });
                    found++;
                }
            }
            finally
            {
                _endMethod.Invoke(null, null);
            }

            return results;
        }
    }
}
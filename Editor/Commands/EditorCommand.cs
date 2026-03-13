using System.Collections;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class EditorCommand
    {
        [AIBridge("Perform undo operations",
            "AIBridgeCLI EditorCommand_Undo --count 3")]
        public static IEnumerator Undo(
            [Description("Number of undo steps")] int count = 1)
        {
            for (var i = 0; i < count; i++)
                UnityEditor.Undo.PerformUndo();
            yield return CommandResult.Success(new { action = "undo", count });
        }

        [AIBridge("Perform redo operations",
            "AIBridgeCLI EditorCommand_Redo --count 1")]
        public static IEnumerator Redo(
            [Description("Number of redo steps")] int count = 1)
        {
            for (var i = 0; i < count; i++)
                UnityEditor.Undo.PerformRedo();
            yield return CommandResult.Success(new { action = "redo", count });
        }

        [AIBridge("Refresh the AssetDatabase",
            "AIBridgeCLI EditorCommand_Refresh")]
        public static IEnumerator Refresh(
            [Description("Force update all assets")] bool forceUpdate = false)
        {
            AssetDatabase.Refresh(forceUpdate ? ImportAssetOptions.ForceUpdate : ImportAssetOptions.Default);
            yield return CommandResult.Success(new { action = "refresh", forceUpdate });
        }

        [AIBridge("Enter Play mode",
            "AIBridgeCLI EditorCommand_Play")]
        public static IEnumerator Play()
        {
            if (EditorApplication.isPlaying)
                yield return CommandResult.Success(new { action = "play", alreadyPlaying = true });
            else
            {
                EditorApplication.isPlaying = true;
                yield return CommandResult.Success(new { action = "play", started = true });
            }
        }

        [AIBridge("Exit Play mode",
            "AIBridgeCLI EditorCommand_Stop")]
        public static IEnumerator Stop()
        {
            if (!EditorApplication.isPlaying)
                yield return CommandResult.Success(new { action = "stop", alreadyStopped = true });
            else
            {
                EditorApplication.isPlaying = false;
                yield return CommandResult.Success(new { action = "stop", stopped = true });
            }
        }

        [AIBridge("Toggle or set pause state",
            "AIBridgeCLI EditorCommand_Pause")]
        public static IEnumerator Pause(
            [Description("Toggle pause (true) or set specific value (false)")] bool toggle = true,
            [Description("Pause state to set when toggle is false")] bool pause = true)
        {
            EditorApplication.isPaused = toggle ? !EditorApplication.isPaused : pause;
            yield return CommandResult.Success(new { action = "pause", isPaused = EditorApplication.isPaused });
        }

        [AIBridge("Get current Editor state (play/pause/compile status)",
            "AIBridgeCLI EditorCommand_GetState")]
        public static IEnumerator GetState()
        {
            yield return CommandResult.Success(new
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                applicationPath = EditorApplication.applicationPath,
                applicationContentsPath = EditorApplication.applicationContentsPath
            });
        }

        [AIBridge("Log a message to the Unity console",
            "AIBridgeCLI EditorCommand_Log --message \"Hello World\"")]
        public static IEnumerator Log(
            [Description("Message to log")] string message,
            [Description("Log type: Log, Warning, Error")] string logType = "Log")
        {
            if (string.IsNullOrEmpty(message))
            {
                yield return CommandResult.Failure("Parameter 'message' is required");
                yield break;
            }
            switch (logType.ToLower())
            {
                case "warning": Debug.LogWarning($"[AIBridge] {message}"); break;
                case "error": Debug.LogError($"[AIBridge] {message}"); break;
                default: Debug.Log($"[AIBridge] {message}"); break;
            }
            yield return CommandResult.Success(new { action = "log", message, logType });
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIBridge.Editor
{
    public class AIBridgeRuntimeSettingsWindow : EditorWindow
    {
        private const float RuntimeBridgeSettingsLabelWidthRatio = 0.28f;
        private const float RuntimeBridgeSettingsMinLabelWidth = 220f;
        private const float RuntimeBridgeSettingsMaxLabelWidth = 280f;

        private Vector2 _scrollPosition;

        [MenuItem("Window/AIBridge Runtime")]
        private static void OpenWindow()
        {
            var window = GetWindow<AIBridgeRuntimeSettingsWindow>();
            window.titleContent = new GUIContent(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeRuntimeTitle));
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnGUI()
        {
            titleContent = new GUIContent(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeRuntimeTitle));
            DrawHeader();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawRuntimeBridgeSettings();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeRuntimeTitle), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeRuntimeSubtitle), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            DrawLanguagePopup(150f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
        }

        private static void DrawLanguagePopup(float width)
        {
            var currentIndex = AIBridgeEditorText.GetLanguageIndex(AIBridgeEditorText.Language);
            var nextIndex = EditorGUILayout.Popup(currentIndex, AIBridgeEditorText.LanguageLabels, GUILayout.Width(width));
            if (nextIndex == currentIndex || nextIndex < 0 || nextIndex >= AIBridgeEditorText.LanguageValues.Length)
            {
                return;
            }

            AIBridgeProjectSettings.Instance.EditorLanguage = AIBridgeEditorText.LanguageValues[nextIndex];
            AIBridgeProjectSettings.Instance.SaveSettings();
        }

        private void DrawRuntimeBridgeSettings()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;

            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeBridge), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeBridgeHelp), MessageType.Info);

            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = GetRuntimeBridgeSettingsLabelWidth();

            EditorGUI.BeginChangeCheck();

            settings.EnableRuntimeBridge = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.CompileRuntimeBridge),
                settings.EnableRuntimeBridge);

            settings.AutoInjectRuntimeBridgeInDevelopmentBuild = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AutoInjectDevelopmentBuild),
                settings.AutoInjectRuntimeBridgeInDevelopmentBuild);

            settings.KeepRunningInBackground = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.KeepRunningInBackground),
                settings.KeepRunningInBackground);

            var hybridClrInstalled = AIBridgeHybridClrUtility.IsHybridClrInstalled();
            using (new EditorGUI.DisabledScope(!hybridClrInstalled))
            {
                settings.EnableRuntimeCodeExecution = EditorGUILayout.Toggle(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.EnableRuntimeCodeExecution),
                    settings.EnableRuntimeCodeExecution);
            }

            if (!hybridClrInstalled)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HybridClrHelp), MessageType.Info);
            }
            else if (settings.EnableRuntimeCodeExecution)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeCodeWarning), MessageType.Warning);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpTransportSettings), EditorStyles.boldLabel);

            settings.EnableHttpTransport = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.EnableHttpTransport),
                settings.EnableHttpTransport);

            using (new EditorGUI.DisabledScope(!settings.EnableHttpTransport))
            {
                settings.HttpBindAddress = EditorGUILayout.DelayedTextField(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpBindAddress),
                    settings.HttpBindAddress ?? string.Empty);

                settings.HttpPort = EditorGUILayout.IntField(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpPort),
                    settings.HttpPort);

                settings.EnableLanDiscovery = EditorGUILayout.Toggle(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.EnableLanDiscovery),
                    settings.EnableLanDiscovery);

                using (new EditorGUI.DisabledScope(!settings.EnableLanDiscovery))
                {
                    settings.DiscoveryUdpPort = EditorGUILayout.IntField(
                        AIBridgeEditorText.Get(AIBridgeEditorTextKey.DiscoveryUdpPort),
                        settings.DiscoveryUdpPort);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Settings), EditorStyles.boldLabel);

            settings.AllowRuntimeBridgeInReleaseBuild = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowReleaseBuild),
                settings.AllowRuntimeBridgeInReleaseBuild);

            if (settings.AllowRuntimeBridgeInReleaseBuild)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ReleaseWarning), MessageType.Warning);
            }

            var displayExchangeDirectory = string.IsNullOrWhiteSpace(settings.ExchangeDirectory)
                ? AIBridgeRuntimeBridgeEditorUtility.GetDefaultRuntimeDirectory()
                : settings.ExchangeDirectory;
            var nextExchangeDirectory = EditorGUILayout.DelayedTextField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeDirectory),
                displayExchangeDirectory);
            settings.ExchangeDirectory = string.Equals(
                nextExchangeDirectory.Trim(),
                AIBridgeRuntimeBridgeEditorUtility.GetDefaultRuntimeDirectory(),
                StringComparison.Ordinal)
                ? string.Empty
                : nextExchangeDirectory;

            settings.TargetId = EditorGUILayout.DelayedTextField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.DefaultTargetId),
                settings.TargetId ?? string.Empty);

            settings.AuthToken = EditorGUILayout.DelayedTextField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AuthToken),
                settings.AuthToken ?? string.Empty);

            settings.AllowedActions = EditorGUILayout.DelayedTextField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowedActions),
                settings.AllowedActions ?? string.Empty);

            settings.HeartbeatIntervalSeconds = EditorGUILayout.Slider(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.HeartbeatInterval),
                settings.HeartbeatIntervalSeconds,
                0.1f,
                10f);

            settings.LogBufferSize = EditorGUILayout.IntSlider(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.LogBufferSize),
                settings.LogBufferSize,
                50,
                5000);

            settings.MaxResultBytes = EditorGUILayout.IntField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.MaxResultBytes),
                settings.MaxResultBytes);

            var settingsChanged = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = oldLabelWidth;

            if (settingsChanged)
            {
                SaveRuntimeSettings();
            }

            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowedActionsHelp), MessageType.None);

            DrawRuntimeInfo();
            DrawRuntimeActions();
        }

        private static void DrawRuntimeInfo()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeInfo), EditorStyles.boldLabel);
            DrawInfoBlock(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeDirectory), AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory());
            DrawInfoBlock(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeHttpEntry), AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl());
            DrawInfoBlock(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeConfigPath), AIBridgeRuntimeBridgeEditorUtility.GetRuntimeConfigPath());
        }

        private static void DrawInfoBlock(string label, string value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(20));
        }

        private static void DrawRuntimeActions()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Actions), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
#if AIBRIDGE_RUNTIME_ENABLED
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CreateRuntimeObject), GUILayout.Height(28)))
            {
                CreateOrSelectRuntimeObject();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ApplySceneRuntime), GUILayout.Height(28)))
            {
                ApplySettingsToSceneRuntimes(showDialog: true);
            }
#else
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CreateRuntimeObject), GUILayout.Height(28));
                GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ApplySceneRuntime), GUILayout.Height(28));
            }
#endif
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Open), GUILayout.Height(24)))
            {
                OpenRuntimeDirectory();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.OpenPlayersPanel), GUILayout.Height(24)))
            {
                AIBridgePlayersWindow.OpenWindow();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyLaunchArgs), GUILayout.Height(24)))
            {
                CopyLaunchArguments();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.WriteRuntimeConfig), GUILayout.Height(24)))
            {
                WriteRuntimeConfig();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyHttpStatusCli), GUILayout.Height(24)))
            {
                CopyHttpStatusCommand();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyDiscoverCli), GUILayout.Height(24)))
            {
                CopyDiscoverCommand();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void SaveRuntimeSettings()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;

            settings.MaxResultBytes = Math.Max(1024, settings.MaxResultBytes);
            settings.HttpBindAddress = string.IsNullOrWhiteSpace(settings.HttpBindAddress)
                ? AIBridgeProjectSettings.DefaultRuntimeBridgeHttpBindAddress
                : settings.HttpBindAddress.Trim();
            settings.HttpPort = Math.Max(1, settings.HttpPort);
            settings.DiscoveryUdpPort = Math.Max(1, settings.DiscoveryUdpPort);

            AIBridgeProjectSettings.Instance.SaveSettings();
            AIBridgeRuntimeBridgeEditorUtility.WriteRuntimeConfig();
            AIBridgeRuntimeBuildProcessor.SyncRuntimeBootstrapDefinesForActiveTarget();
        }

#if AIBRIDGE_RUNTIME_ENABLED
        private static void CreateOrSelectRuntimeObject()
        {
            var runtime = AIBridgeRuntimeBridgeEditorUtility.FindSceneRuntime();
            if (runtime == null)
            {
                runtime = AIBridgeRuntimeBridgeEditorUtility.CreateConfiguredRuntimeObject(
                    "AIBridgeRuntime",
                    HideFlags.None,
                    useUndo: true);
            }

            AIBridgeRuntimeBridgeEditorUtility.ApplyProjectSettingsToRuntime(runtime);
            EditorUtility.SetDirty(runtime);
            Selection.activeGameObject = runtime.gameObject;
            EditorGUIUtility.PingObject(runtime.gameObject);
            EditorSceneManager.MarkSceneDirty(runtime.gameObject.scene);
        }

        private static void ApplySettingsToSceneRuntimes(bool showDialog)
        {
            var runtimes = AIBridgeRuntimeBridgeEditorUtility.FindSceneRuntimes();

            for (var i = 0; i < runtimes.Length; i++)
            {
                AIBridgeRuntimeBridgeEditorUtility.ApplyProjectSettingsToRuntime(runtimes[i]);
                EditorUtility.SetDirty(runtimes[i]);
                EditorSceneManager.MarkSceneDirty(runtimes[i].gameObject.scene);
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "AIBridge",
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.AppliedRuntimeSettingsMessage, runtimes.Length),
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.Ok));
            }
        }
#endif

        private static void OpenRuntimeDirectory()
        {
            var path = AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory();
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static void CopyLaunchArguments()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            var runtimeDirectory = AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory();
            var targetId = string.IsNullOrWhiteSpace(settings.TargetId) ? "player1" : settings.TargetId.Trim();
            EditorGUIUtility.systemCopyBuffer =
                "--aibridge-runtime-dir " + AIBridgeRuntimeBridgeEditorUtility.Quote(runtimeDirectory)
                + " --aibridge-target-id " + AIBridgeRuntimeBridgeEditorUtility.Quote(targetId);
            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeLaunchArgsCopied));
        }

        private static void WriteRuntimeConfig()
        {
            var path = AIBridgeRuntimeBridgeEditorUtility.WriteRuntimeConfig();
            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeConfigWritten, path));
        }

        private static void CopyHttpStatusCommand()
        {
            EditorGUIUtility.systemCopyBuffer = AIBridgeRuntimeBridgeEditorUtility.BuildCliCommand(
                "runtime status --transport http --url " + AIBridgeRuntimeBridgeEditorUtility.Quote(AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl()) + " --target latest",
                includeRuntimeDirectory: false);
            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeHttpCliCopied));
        }

        private static void CopyDiscoverCommand()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            EditorGUIUtility.systemCopyBuffer = AIBridgeRuntimeBridgeEditorUtility.BuildCliCommand(
                "runtime discover --udpPort " + Math.Max(1, settings.DiscoveryUdpPort),
                includeRuntimeDirectory: false);
            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeDiscoveryCliCopied));
        }

        private float GetRuntimeBridgeSettingsLabelWidth()
        {
            return Mathf.Clamp(
                position.width * RuntimeBridgeSettingsLabelWidthRatio,
                RuntimeBridgeSettingsMinLabelWidth,
                RuntimeBridgeSettingsMaxLabelWidth);
        }
    }
}

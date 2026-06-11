using System;
using System.Collections.Generic;
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
        private const int AllBuiltInRuntimeActionsMask = (1 << 8) - 1;
        private static readonly string[] BuiltInRuntimeActions =
        {
            "runtime.ping",
            "runtime.status",
            "runtime.logs",
            "runtime.logs.clear",
            "runtime.perf",
            "runtime.screenshot",
            "runtime.code.execute",
            "runtime.handlers"
        };

        private static readonly AIBridgeEditorTextKey[] BuiltInRuntimeActionHelpKeys =
        {
            AIBridgeEditorTextKey.AllowedActionPingHelp,
            AIBridgeEditorTextKey.AllowedActionStatusHelp,
            AIBridgeEditorTextKey.AllowedActionLogsHelp,
            AIBridgeEditorTextKey.AllowedActionLogsClearHelp,
            AIBridgeEditorTextKey.AllowedActionPerfHelp,
            AIBridgeEditorTextKey.AllowedActionScreenshotHelp,
            AIBridgeEditorTextKey.AllowedActionCodeExecuteHelp,
            AIBridgeEditorTextKey.AllowedActionHandlersHelp
        };

        private Vector2 _scrollPosition;
        private bool _cliCommandsExpanded;

        [MenuItem("Window/AIBridge/Runtime")]
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

            DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.BuildInjectionSettings));

            settings.EnableRuntimeBridge = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.CompileRuntimeBridge),
                settings.EnableRuntimeBridge);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CompileRuntimeBridgeHelp), MessageType.None);

            settings.AutoInjectRuntimeBridgeInDevelopmentBuild = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AutoInjectDevelopmentBuild),
                settings.AutoInjectRuntimeBridgeInDevelopmentBuild);

            settings.AllowRuntimeBridgeInReleaseBuild = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowReleaseBuild),
                settings.AllowRuntimeBridgeInReleaseBuild);

            if (settings.AllowRuntimeBridgeInReleaseBuild)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ReleaseWarning), MessageType.Warning);
            }

            DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeCapabilities));

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

                settings.MaxRuntimeCodeExecutionSeconds = EditorGUILayout.FloatField(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.MaxCodeExecutionSeconds),
                    settings.MaxRuntimeCodeExecutionSeconds);
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.MaxCodeExecutionSecondsHelp), MessageType.None);
            }

            DrawAllowedActionsField(settings);

            DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Transport));

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
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AuthTokenHelp), MessageType.None);

            settings.EnableHttpTransport = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.EnableHttpTransport),
                settings.EnableHttpTransport);

            using (new EditorGUI.DisabledScope(!settings.EnableHttpTransport))
            {
                settings.HttpBindAddress = EditorGUILayout.DelayedTextField(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpBindAddress),
                    settings.HttpBindAddress ?? string.Empty);

                if (!IsLoopbackBindAddress(settings.HttpBindAddress) && string.IsNullOrEmpty(settings.AuthToken))
                {
                    EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.InsecureBindWarning), MessageType.Warning);
                }

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

            DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeBehaviorLimits));

            settings.KeepRunningInBackground = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.KeepRunningInBackground),
                settings.KeepRunningInBackground);

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

            settings.OrphanResultRetentionSeconds = EditorGUILayout.FloatField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.OrphanResultRetentionSeconds),
                settings.OrphanResultRetentionSeconds);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.OrphanResultRetentionSecondsHelp), MessageType.None);

            var settingsChanged = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = oldLabelWidth;

            if (settingsChanged)
            {
                SaveRuntimeSettings();
            }

            DrawRuntimeInfo();
            DrawRuntimeActions();
        }

        private void DrawRuntimeInfo()
        {
            DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ResolvedInfo));
            DrawRuntimeDirectoryInfoBlock();
            DrawInfoBlock(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeHttpEntry), AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl());
            DrawInfoBlock(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeConfigPath), AIBridgeRuntimeBridgeEditorUtility.GetRuntimeConfigPath());
        }

        private static void DrawRuntimeDirectoryInfoBlock()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeDirectory), EditorStyles.miniBoldLabel, GUILayout.Width(120));
            EditorGUILayout.SelectableLabel(AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory(), EditorStyles.wordWrappedMiniLabel, GUILayout.Height(20));
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Open), GUILayout.Width(58), GUILayout.Height(20)))
            {
                OpenRuntimeDirectory();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawInfoBlock(string label, string value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(20));
        }

        private void DrawRuntimeActions()
        {
            DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Actions));
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
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.OpenPlayersPanel), GUILayout.Height(24)))
            {
                AIBridgePlayersWindow.OpenWindow();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.WriteRuntimeConfig), GUILayout.Height(24)))
            {
                WriteRuntimeConfig();
            }
            EditorGUILayout.EndHorizontal();

            _cliCommandsExpanded = EditorGUILayout.Foldout(
                _cliCommandsExpanded,
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.CliCommands),
                true);
            if (!_cliCommandsExpanded)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyLaunchArgs), GUILayout.Height(24)))
            {
                CopyLaunchArguments();
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

        private static void DrawSectionHeader(string label)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private static void DrawAllowedActionsField(AIBridgeProjectSettings.RuntimeBridgeSettingsData settings)
        {
            var hasAllowedActions = !string.IsNullOrWhiteSpace(settings.AllowedActions);
            var useWhitelist = EditorGUILayout.Toggle(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.UseAllowedActionsWhitelist),
                hasAllowedActions);
            var allowedActions = ParseAllowedActionNames(settings.AllowedActions);

            if (!useWhitelist)
            {
                settings.AllowedActions = string.Empty;
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowedActionsHelp), MessageType.None);
                DrawBuiltInActionDescriptions();
                return;
            }

            var mask = hasAllowedActions ? 0 : AllBuiltInRuntimeActionsMask;
            var customActions = new List<string>();
            for (var i = 0; i < allowedActions.Count; i++)
            {
                if (TryGetBuiltInActionIndex(allowedActions[i], out var builtInIndex))
                {
                    mask |= 1 << builtInIndex;
                }
                else
                {
                    customActions.Add(allowedActions[i]);
                }
            }

            var nextMask = EditorGUILayout.MaskField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowedActions),
                mask,
                BuiltInRuntimeActions);
            var nextCustomActions = EditorGUILayout.DelayedTextField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowedActionsCustom),
                string.Join("\n", customActions.ToArray()));

            settings.AllowedActions = BuildAllowedActionsValue(nextMask, nextCustomActions);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AllowedActionsWhitelistHelp), MessageType.None);
            DrawBuiltInActionDescriptions();
        }

        private static void DrawBuiltInActionDescriptions()
        {
            EditorGUILayout.Space(2);
            for (var i = 0; i < BuiltInRuntimeActions.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(BuiltInRuntimeActions[i], EditorStyles.miniBoldLabel, GUILayout.Width(170));
                GUILayout.Label(
                    AIBridgeEditorText.Get(BuiltInRuntimeActionHelpKeys[i]),
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static List<string> ParseAllowedActionNames(string value)
        {
            var actions = new List<string>();
            var seenActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
            {
                return actions;
            }

            var parts = value.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var action = parts[i].Trim();
                if (action.Length == 0 || !seenActions.Add(action))
                {
                    continue;
                }

                actions.Add(action);
            }

            return actions;
        }

        private static string BuildAllowedActionsValue(int builtInMask, string customActionsValue)
        {
            var actions = new List<string>();
            for (var i = 0; i < BuiltInRuntimeActions.Length; i++)
            {
                if ((builtInMask & (1 << i)) != 0)
                {
                    actions.Add(BuiltInRuntimeActions[i]);
                }
            }

            var customActions = ParseAllowedActionNames(customActionsValue);
            for (var i = 0; i < customActions.Count; i++)
            {
                if (!TryGetBuiltInActionIndex(customActions[i], out _))
                {
                    actions.Add(customActions[i]);
                }
            }

            return string.Join("\n", actions.ToArray());
        }

        private static bool TryGetBuiltInActionIndex(string action, out int index)
        {
            for (var i = 0; i < BuiltInRuntimeActions.Length; i++)
            {
                if (string.Equals(BuiltInRuntimeActions[i], action, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        // 反向白名单:仅本机回环地址视为安全;其余(0.0.0.0、::、* 、具体网卡 IP 等)在无 Token 时均需警告。
        private static bool IsLoopbackBindAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            var trimmed = address.Trim();
            return string.Equals(trimmed, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "::1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "[::1]", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase);
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

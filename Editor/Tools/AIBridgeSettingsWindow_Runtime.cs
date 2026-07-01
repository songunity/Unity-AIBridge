using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly List<AIBridgeRuntimePlayerInfo> _players = new List<AIBridgeRuntimePlayerInfo>();
        private readonly List<AIBridgeRuntimeDiscoveredTargetInfo> _discoveredTargets = new List<AIBridgeRuntimeDiscoveredTargetInfo>();
        private Vector2 _scrollPosition;
        private string _runtimeDirectory;
        private string _localHttpUrl;
        private string _discoveryCachePath;
        private bool _scanLanOnRefresh = true;
        private bool _lanScanRunning;
        private string _lanScanStatus;
        private int _lanScanGeneration;
        private double _lastRefreshTime;

        [MenuItem("Window/AIBridge/Runtime")]
        private static void OpenWindow()
        {
            var window = GetWindow<AIBridgeRuntimeSettingsWindow>();
            window.titleContent = new GUIContent(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeRuntimeTitle));
            window.minSize = new Vector2(820, 560);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshRuntimeTargets();
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

            if (!settings.EnableRuntimeBridge)
            {
                var compileToggleChanged = EditorGUI.EndChangeCheck();
                EditorGUIUtility.labelWidth = oldLabelWidth;

                if (compileToggleChanged)
                {
                    SaveRuntimeSettings();
                }

                return;
            }

            DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Transport));

            settings.AuthToken = EditorGUILayout.DelayedTextField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.AuthToken),
                settings.AuthToken ?? string.Empty);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AuthTokenHelp), MessageType.None);

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

            var settingsChanged = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = oldLabelWidth;

            if (settingsChanged)
            {
                SaveRuntimeSettings();
            }

            DrawRuntimeInfo();
            DrawRuntimeActions();
            DrawRuntimeTargets();
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

        }

        private void DrawRuntimeTargets()
        {
            DrawRuntimeTargetsHeader();
            DrawRuntimeTargetsToolbar();
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeDirectory), _runtimeDirectory ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpEntry), _localHttpUrl ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DiscoverCache), _discoveryCachePath ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            if (_lanScanRunning || !string.IsNullOrEmpty(_lanScanStatus))
            {
                EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.LanScan), _lanScanStatus ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            }

            DrawDiscoveredTargets();

            if (_players.Count == 0)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.NoFileTransportTargets), MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.FileTransportTargets), EditorStyles.boldLabel);
            for (var i = 0; i < _players.Count; i++)
            {
                DrawPlayer(_players[i]);
                EditorGUILayout.Space(5);
            }
        }

        private static void DrawRuntimeTargetsHeader()
        {
            EditorGUILayout.Space(10);
            var rect = EditorGUILayout.GetControlRect(false, 30f);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.22f, 0.28f, 1f));
            var labelRect = new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 20f);
            EditorGUI.LabelField(labelRect, AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgePlayersTitle), EditorStyles.whiteBoldLabel);
        }

        private void DrawRuntimeTargetsToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Refresh), EditorStyles.toolbarButton, GUILayout.Width(72)))
            {
                RefreshRuntimeTargets();
            }

            var previousScanLanOnRefresh = _scanLanOnRefresh;
            _scanLanOnRefresh = EditorGUILayout.ToggleLeft(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.ScanLan),
                _scanLanOnRefresh,
                GUILayout.Width(96));
            if (_scanLanOnRefresh && !previousScanLanOnRefresh)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        RefreshRuntimeTargets();
                    }
                };
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.OpenDirectory), EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                OpenRuntimeDirectory();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.TargetCount, _players.Count + _discoveredTargets.Count),
                EditorStyles.miniLabel,
                GUILayout.Width(90));
            EditorGUILayout.LabelField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.RefreshedCount, FormatRefreshAge()),
                EditorStyles.miniLabel,
                GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayer(AIBridgeRuntimePlayerInfo player)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(player.TargetId, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var statusText = player.Stale
                ? AIBridgeEditorText.Get(AIBridgeEditorTextKey.Stale)
                : AIBridgeEditorText.Get(AIBridgeEditorTextKey.Online);
            var previousColor = GUI.color;
            GUI.color = player.Stale ? new Color(1f, 0.72f, 0.25f) : new Color(0.55f, 1f, 0.55f);
            GUILayout.Label(statusText, EditorStyles.boldLabel, GUILayout.Width(72));
            GUI.color = previousColor;
            if (player.Stale
                && GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteCache), GUILayout.Width(92)))
            {
                DeletePlayerCache(player);
            }
            EditorGUILayout.EndHorizontal();

            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Product), JoinNonEmpty(player.ProductName, player.ApplicationVersion));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Transport), string.IsNullOrEmpty(player.Transport) ? "file" : player.Transport);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpUrl), player.HttpUrl);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Scene), player.ActiveScene);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Platform), player.Platform);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Runtime), player.RuntimeVersion);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Process), player.ProcessId > 0 ? player.ProcessId.ToString() : "-");
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Heartbeat), FormatHeartbeat(player));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Path), player.TargetPath);

            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrWhiteSpace(player.HttpUrl)
                && GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyHttpStatus)))
            {
                CopyHttpCommand("runtime status --transport http --url " + Quote(player.HttpUrl) + " --target " + QuoteTarget(player.TargetId));
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyStatusCli)))
            {
                CopyFileCommand("runtime status --transport file --target " + QuoteTarget(player.TargetId));
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyLogsCli)))
            {
                CopyFileCommand("runtime logs --transport file --target " + QuoteTarget(player.TargetId) + " --logType Error --count 100");
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyScreenshotCli)))
            {
                CopyFileCommand("runtime screenshot --transport file --target " + QuoteTarget(player.TargetId));
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Open), GUILayout.Width(58)))
            {
                if (Directory.Exists(player.TargetPath))
                {
                    EditorUtility.RevealInFinder(player.TargetPath);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawDiscoveredTargets()
        {
            if (_discoveredTargets.Count == 0)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.NoLanDiscoveredTargets), MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpLanDiscoveredTargets), EditorStyles.boldLabel);
            for (var i = 0; i < _discoveredTargets.Count; i++)
            {
                DrawDiscoveredTarget(_discoveredTargets[i]);
                EditorGUILayout.Space(5);
            }
        }

        private void DrawDiscoveredTarget(AIBridgeRuntimeDiscoveredTargetInfo target)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(target.TargetId, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var statusText = target.Stale
                ? AIBridgeEditorText.Get(AIBridgeEditorTextKey.Cache)
                : target.Reachable
                    ? AIBridgeEditorText.Get(AIBridgeEditorTextKey.Reachable)
                    : AIBridgeEditorText.Get(AIBridgeEditorTextKey.Discovered);
            var previousColor = GUI.color;
            GUI.color = target.Stale ? new Color(1f, 0.72f, 0.25f) : target.Reachable ? new Color(0.55f, 1f, 0.55f) : new Color(0.65f, 0.8f, 1f);
            GUILayout.Label(statusText, EditorStyles.boldLabel, GUILayout.Width(96));
            GUI.color = previousColor;
            if (target.Stale
                && GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteCache), GUILayout.Width(92)))
            {
                DeleteDiscoveredTargetCache(target);
            }
            EditorGUILayout.EndHorizontal();

            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Url), target.Url);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.BindUrl), target.BindUrl);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Project), JoinNonEmpty(target.ProjectName, target.ApplicationVersion));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Device), target.DeviceName);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Platform), target.Platform);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Kind), target.TargetKind);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Auth), target.RequiresToken ? AIBridgeEditorText.Get(AIBridgeEditorTextKey.TokenRequired) : AIBridgeEditorText.Get(AIBridgeEditorTextKey.NoToken));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.LastSeen), FormatDiscoveryAge(target));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Health), target.Reachable ? target.LastHealthCheckUtc : AIBridgeEditorText.Get(AIBridgeEditorTextKey.Unreachable));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Remote), target.RemoteEndPoint);
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.SourceNic), JoinNonEmpty(target.SourceInterface, target.SourceInterfaceAddress));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyStatusCli)))
            {
                CopyDiscoveredCommand(target, "status");
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyLogsCli)))
            {
                CopyDiscoveredCommand(target, "logs --logType Error --count 100");
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyScreenshotCli)))
            {
                CopyDiscoveredCommand(target, "screenshot");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void DrawInfoLine(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(88));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.miniLabel, GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshRuntimeTargets()
        {
            LoadRuntimeTargetsFromCache();
            if (_scanLanOnRefresh)
            {
                BeginLanScan();
            }
        }

        private void LoadRuntimeTargetsFromCache()
        {
            _runtimeDirectory = AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory();
            _localHttpUrl = AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl();
            _discoveryCachePath = AIBridgeRuntimeBridgeEditorUtility.GetDiscoveryCachePath();
            _players.Clear();
            _players.AddRange(AIBridgeRuntimeBridgeEditorUtility.ListPlayers());
            _discoveredTargets.Clear();
            _discoveredTargets.AddRange(AIBridgeRuntimeBridgeEditorUtility.ListDiscoveredTargets());
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void BeginLanScan()
        {
            if (_lanScanRunning)
            {
                return;
            }

            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            var udpPort = Math.Max(1, settings.DiscoveryUdpPort);
            var authToken = settings.AuthToken;
            var generation = ++_lanScanGeneration;
            var synchronizationContext = SynchronizationContext.Current;
            _lanScanRunning = true;
            _lanScanStatus = AIBridgeEditorText.Get(AIBridgeEditorTextKey.ScanningUdp, udpPort);
            Repaint();

            Task.Run(() =>
            {
                try
                {
                    return AIBridgeRuntimeBridgeEditorUtility.DiscoverLanTargets(
                        AIBridgeRuntimeBridgeEditorUtility.DefaultLanDiscoveryTimeoutMs,
                        udpPort,
                        authToken);
                }
                catch (Exception exception)
                {
                    return new AIBridgeRuntimeLanDiscoveryResult
                    {
                        Success = false,
                        Error = exception.Message
                    };
                }
            }).ContinueWith(task =>
            {
                var result = task.Status == TaskStatus.RanToCompletion
                    ? task.Result
                    : new AIBridgeRuntimeLanDiscoveryResult
                    {
                        Success = false,
                        Error = task.Exception == null ? "task canceled" : task.Exception.GetBaseException().Message
                    };
                if (synchronizationContext != null)
                {
                    synchronizationContext.Post(_ => CompleteLanScan(generation, result), null);
                }
                else
                {
                    EditorApplication.delayCall += () => CompleteLanScan(generation, result);
                }
            }, TaskScheduler.Default);
        }

        private void CompleteLanScan(int generation, AIBridgeRuntimeLanDiscoveryResult result)
        {
            if (this == null || generation != _lanScanGeneration)
            {
                return;
            }

            _lanScanRunning = false;
            if (result == null || !result.Success)
            {
                var error = result == null ? "unknown error" : result.Error;
                _lanScanStatus = AIBridgeEditorText.Get(AIBridgeEditorTextKey.ScanFailed, error);
            }
            else
            {
                _lanScanStatus = AIBridgeEditorText.Get(AIBridgeEditorTextKey.FoundLanTargets, result.ReachableCount, result.Count);
            }

            LoadRuntimeTargetsFromCache();
        }

        private static void DrawSectionHeader(string label)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
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

            settings.AutoInjectRuntimeBridgeInDevelopmentBuild = AIBridgeProjectSettings.DefaultRuntimeBridgeAutoInjectInDevelopmentBuild;
            settings.AllowRuntimeBridgeInReleaseBuild = AIBridgeProjectSettings.DefaultRuntimeBridgeAllowInReleaseBuild;
            settings.ExchangeDirectory = AIBridgeProjectSettings.DefaultRuntimeBridgeExchangeDirectory;
            settings.TargetId = AIBridgeProjectSettings.DefaultRuntimeBridgeTargetId;
            settings.AllowedActions = string.Empty;
            settings.EnableRuntimeCodeExecution = AIBridgeProjectSettings.DefaultRuntimeBridgeCodeExecutionEnabled;
            settings.KeepRunningInBackground = AIBridgeProjectSettings.DefaultRuntimeBridgeKeepRunningInBackground;
            settings.HeartbeatIntervalSeconds = AIBridgeProjectSettings.DefaultRuntimeBridgeHeartbeatIntervalSeconds;
            settings.LogBufferSize = AIBridgeProjectSettings.DefaultRuntimeBridgeLogBufferSize;
            settings.MaxResultBytes = AIBridgeProjectSettings.DefaultRuntimeBridgeMaxResultBytes;
            settings.OrphanResultRetentionSeconds = AIBridgeProjectSettings.DefaultRuntimeBridgeOrphanResultRetentionSeconds;
            settings.EnableHttpTransport = AIBridgeProjectSettings.DefaultRuntimeBridgeEnableHttpTransport;
            settings.HttpBindAddress = AIBridgeProjectSettings.DefaultRuntimeBridgeHttpBindAddress;
            settings.MaxResultBytes = Math.Max(1024, settings.MaxResultBytes);
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

        private static void CopyFileCommand(string commandBody)
        {
            EditorGUIUtility.systemCopyBuffer = AIBridgeRuntimeBridgeEditorUtility.BuildCliCommand(commandBody);
            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeCliCopied));
        }

        private static void CopyHttpCommand(string commandBody)
        {
            EditorGUIUtility.systemCopyBuffer = AIBridgeRuntimeBridgeEditorUtility.BuildCliCommand(commandBody, includeRuntimeDirectory: false);
            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeHttpCliCopied));
        }

        private static void CopyDiscoveredCommand(AIBridgeRuntimeDiscoveredTargetInfo target, string action)
        {
            if (target == null)
            {
                return;
            }

            CopyHttpCommand("runtime " + action
                + " --transport http --url " + Quote(target.Url)
                + " --target " + QuoteTarget(target.TargetId));
        }

        private void DeletePlayerCache(AIBridgeRuntimePlayerInfo player)
        {
            if (player == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteRuntimeTargetCache),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteRuntimeTargetCacheMessage, player.TargetId),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Delete),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Cancel)))
            {
                return;
            }

            if (!AIBridgeRuntimeBridgeEditorUtility.TryDeletePlayerCache(player, out var error))
            {
                EditorUtility.DisplayDialog(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteFailed),
                    error,
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.Ok));
                return;
            }

            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.StaleRuntimeDeleted, player.TargetId));
            RefreshRuntimeTargets();
            GUIUtility.ExitGUI();
        }

        private void DeleteDiscoveredTargetCache(AIBridgeRuntimeDiscoveredTargetInfo target)
        {
            if (target == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteDiscoveryCache),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteDiscoveryCacheMessage, target.TargetId),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Delete),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Cancel)))
            {
                return;
            }

            if (!AIBridgeRuntimeBridgeEditorUtility.TryDeleteDiscoveredTargetCache(target, out var error))
            {
                EditorUtility.DisplayDialog(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteFailed),
                    error,
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.Ok));
                return;
            }

            Debug.Log(AIBridgeEditorText.Get(AIBridgeEditorTextKey.StaleDiscoveryDeleted, target.TargetId));
            RefreshRuntimeTargets();
            GUIUtility.ExitGUI();
        }

        private string FormatRefreshAge()
        {
            var age = Math.Max(0, EditorApplication.timeSinceStartup - _lastRefreshTime);
            return age < 1 ? AIBridgeEditorText.Get(AIBridgeEditorTextKey.Now) : age.ToString("F0") + "s";
        }

        private static string FormatHeartbeat(AIBridgeRuntimePlayerInfo player)
        {
            if (!player.AgeSeconds.HasValue)
            {
                return "-";
            }

            return player.AgeSeconds.Value.ToString("F1") + "s ago / " + player.LastHeartbeatUtc;
        }

        private static string FormatDiscoveryAge(AIBridgeRuntimeDiscoveredTargetInfo target)
        {
            if (target == null || !target.AgeSeconds.HasValue)
            {
                return "-";
            }

            return target.AgeSeconds.Value.ToString("F1") + "s ago / " + target.LastSeenUtc;
        }

        private static string JoinNonEmpty(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return right;
            }

            return string.IsNullOrEmpty(right) ? left : left + " " + right;
        }

        private static string QuoteTarget(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return "latest";
            }

            return targetId.IndexOf(' ') >= 0 ? "\"" + targetId.Replace("\"", "\\\"") + "\"" : targetId;
        }

        private static string Quote(string value)
        {
            return AIBridgeRuntimeBridgeEditorUtility.Quote(value ?? string.Empty);
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

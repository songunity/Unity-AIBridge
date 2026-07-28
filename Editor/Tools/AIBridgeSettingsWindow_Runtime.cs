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
        private const double TargetCacheRefreshIntervalSeconds = 5d;
        private readonly List<AIBridgeRuntimePlayerInfo> _players = new List<AIBridgeRuntimePlayerInfo>();
        private readonly List<AIBridgeRuntimeDiscoveredTargetInfo> _discoveredTargets = new List<AIBridgeRuntimeDiscoveredTargetInfo>();
        private readonly List<RuntimeTargetView> _targets = new List<RuntimeTargetView>();
        private readonly HashSet<string> _expandedTargetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _scrollPosition;
        private string _runtimeDirectory;
        private string _localHttpUrl;
        [SerializeField] private bool _sceneDebugExpanded;
        [SerializeField] private bool _scanLanOnRefresh = true;
        private bool _lanScanRunning;
        private string _lanScanStatus;
        private int _lanScanGeneration;
        private double _lastRefreshTime;
        private double _nextTargetCacheRefreshTime;
        private double _nextUiRepaintTime;

        private sealed class RuntimeTargetView
        {
            public string TargetId;
            public AIBridgeRuntimePlayerInfo Player;
            public AIBridgeRuntimeDiscoveredTargetInfo Discovery;
        }

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
            LoadRuntimeTargetsFromCache();
        }

        private void OnInspectorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now >= _nextTargetCacheRefreshTime)
            {
                LoadRuntimeTargetsFromCache();
                return;
            }

            if (now >= _nextUiRepaintTime)
            {
                _nextUiRepaintTime = now + 1d;
                Repaint();
            }
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

            if (settings.EnableRuntimeBridge)
            {
                DrawSectionHeader(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Transport));

                var currentNetworkMode = IsLoopbackBindAddress(settings.HttpBindAddress) ? 0 : 1;
                var networkMode = EditorGUILayout.Popup(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.NetworkMode),
                    currentNetworkMode,
                    new[]
                    {
                        AIBridgeEditorText.Get(AIBridgeEditorTextKey.LocalOnly),
                        AIBridgeEditorText.Get(AIBridgeEditorTextKey.LocalAreaNetwork)
                    });
                if (networkMode != currentNetworkMode)
                {
                    settings.HttpBindAddress = networkMode == 0 ? "127.0.0.1" : "0.0.0.0";
                    if (networkMode == 0)
                    {
                        settings.EnableLanDiscovery = false;
                    }
                }

                settings.AuthToken = EditorGUILayout.DelayedTextField(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.AuthToken),
                    settings.AuthToken ?? string.Empty);
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AuthTokenHelp), MessageType.None);

                if (networkMode == 1 && string.IsNullOrEmpty(settings.AuthToken))
                {
                    EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.InsecureBindWarning), MessageType.Warning);
                }

                settings.HttpPort = EditorGUILayout.DelayedIntField(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpPort),
                    settings.HttpPort);

                if (networkMode == 1)
                {
                    settings.EnableLanDiscovery = EditorGUILayout.Toggle(
                        AIBridgeEditorText.Get(AIBridgeEditorTextKey.EnableLanDiscovery),
                        settings.EnableLanDiscovery);

                    using (new EditorGUI.DisabledScope(!settings.EnableLanDiscovery))
                    {
                        settings.DiscoveryUdpPort = EditorGUILayout.DelayedIntField(
                            AIBridgeEditorText.Get(AIBridgeEditorTextKey.DiscoveryUdpPort),
                            settings.DiscoveryUdpPort);
                    }
                }
            }

            var settingsChanged = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = oldLabelWidth;

            if (settingsChanged)
            {
                SaveRuntimeSettings();
            }

            if (settings.EnableRuntimeBridge)
            {
                DrawRuntimeActions();
            }

            DrawRuntimeTargets();
        }

        private void DrawRuntimeActions()
        {
            EditorGUILayout.Space(8);
            _sceneDebugExpanded = EditorGUILayout.Foldout(
                _sceneDebugExpanded,
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.SceneDebug),
                true);
            if (!_sceneDebugExpanded)
            {
                return;
            }

            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.SceneDebugHelp), MessageType.None);
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
            DrawRuntimeSummary();
            if (_lanScanRunning || !string.IsNullOrEmpty(_lanScanStatus))
            {
                EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.LanScan), _lanScanStatus ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            }

            if (_targets.Count == 0)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.NoRuntimeTargets), MessageType.Info);
                return;
            }

            for (var i = 0; i < _targets.Count; i++)
            {
                DrawRuntimeTarget(_targets[i]);
                EditorGUILayout.Space(5);
            }
        }

        private void DrawRuntimeSummary()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeDirectory), EditorStyles.miniBoldLabel, GUILayout.Width(88));
            EditorGUILayout.SelectableLabel(_runtimeDirectory ?? string.Empty, EditorStyles.miniLabel, GUILayout.Height(18));
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Open), GUILayout.Width(52), GUILayout.Height(18)))
            {
                OpenRuntimeDirectory();
            }

            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpEntry), EditorStyles.miniBoldLabel, GUILayout.Width(68));
            EditorGUILayout.SelectableLabel(_localHttpUrl ?? string.Empty, EditorStyles.miniLabel, GUILayout.Width(190), GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
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

            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            var canScanLan = settings.EnableRuntimeBridge
                && !IsLoopbackBindAddress(settings.HttpBindAddress)
                && settings.EnableLanDiscovery;
            using (new EditorGUI.DisabledScope(!canScanLan))
            {
                _scanLanOnRefresh = EditorGUILayout.ToggleLeft(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.ScanLan),
                    _scanLanOnRefresh,
                    GUILayout.Width(96));
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.OpenDirectory), EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                OpenRuntimeDirectory();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.TargetCount, _targets.Count),
                EditorStyles.miniLabel,
                GUILayout.Width(90));
            EditorGUILayout.LabelField(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.RefreshedCount, FormatRefreshAge()),
                EditorStyles.miniLabel,
                GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRuntimeTarget(RuntimeTargetView target)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(target.TargetId, EditorStyles.boldLabel);
            if (target.Player != null)
            {
                DrawTransportBadge("FILE");
            }

            if (HasHttpTransport(target))
            {
                DrawTransportBadge("HTTP");
            }

            if (target.Discovery != null)
            {
                DrawTransportBadge("LAN");
            }

            GUILayout.FlexibleSpace();
            var statusText = GetTargetStatus(target);
            var previousColor = GUI.color;
            GUI.color = GetTargetStatusColor(target);
            GUILayout.Label(statusText, EditorStyles.boldLabel, GUILayout.Width(76));
            GUI.color = previousColor;
            GUILayout.Label(FormatTargetAge(target), EditorStyles.miniLabel, GUILayout.Width(68));

            var expanded = _expandedTargetIds.Contains(target.TargetId);
            if (GUILayout.Button(
                AIBridgeEditorText.Get(expanded ? AIBridgeEditorTextKey.HideDetails : AIBridgeEditorTextKey.Details),
                GUILayout.Width(72)))
            {
                if (expanded)
                {
                    _expandedTargetIds.Remove(target.TargetId);
                }
                else
                {
                    _expandedTargetIds.Add(target.TargetId);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(BuildTargetSummary(target), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyStatusCli), GUILayout.Width(96)))
            {
                CopyTargetCommand(target, "status");
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyLogsCli), GUILayout.Width(96)))
            {
                CopyTargetCommand(target, "logs --logType Error --count 100");
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyScreenshotCli), GUILayout.Width(96)))
            {
                CopyTargetCommand(target, "screenshot");
            }
            EditorGUILayout.EndHorizontal();

            if (_expandedTargetIds.Contains(target.TargetId))
            {
                DrawRuntimeTargetDetails(target);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawTransportBadge(string text)
        {
            GUILayout.Label(text, EditorStyles.miniButton, GUILayout.Width(42), GUILayout.Height(18));
        }

        private void DrawRuntimeTargetDetails(RuntimeTargetView target)
        {
            EditorGUILayout.Space(3);
            var player = target.Player;
            var discovery = target.Discovery;
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Product), GetTargetProject(target));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Platform), GetTargetPlatform(target));
            DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpUrl), GetTargetUrl(target));

            if (player != null)
            {
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Scene), player.ActiveScene);
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Runtime), player.RuntimeVersion);
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Process), player.ProcessId > 0 ? player.ProcessId.ToString() : "-");
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Heartbeat), FormatHeartbeat(player));
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Path), player.TargetPath);
            }

            if (discovery != null)
            {
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Device), discovery.DeviceName);
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.BindUrl), discovery.BindUrl);
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Kind), discovery.TargetKind);
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Auth), discovery.RequiresToken ? AIBridgeEditorText.Get(AIBridgeEditorTextKey.TokenRequired) : AIBridgeEditorText.Get(AIBridgeEditorTextKey.NoToken));
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.LastSeen), FormatDiscoveryAge(discovery));
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Health), discovery.Reachable ? discovery.LastHealthCheckUtc : AIBridgeEditorText.Get(AIBridgeEditorTextKey.Unreachable));
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Remote), discovery.RemoteEndPoint);
                DrawInfoLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.SourceNic), JoinNonEmpty(discovery.SourceInterface, discovery.SourceInterfaceAddress));
            }

            EditorGUILayout.BeginHorizontal();
            if (player != null && GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Open), GUILayout.Width(72)))
            {
                if (Directory.Exists(player.TargetPath))
                {
                    EditorUtility.RevealInFinder(player.TargetPath);
                }
            }

            if (player != null && player.Stale
                && GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteCache), GUILayout.Width(110)))
            {
                DeletePlayerCache(player);
            }

            if (discovery != null && discovery.Stale
                && GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DeleteCache), GUILayout.Width(110)))
            {
                DeleteDiscoveredTargetCache(discovery);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawInfoLine(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(88));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.miniLabel, GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();
        }

        private static bool HasHttpTransport(RuntimeTargetView target)
        {
            return target != null
                && ((target.Player != null && !string.IsNullOrWhiteSpace(target.Player.HttpUrl))
                    || (target.Discovery != null && !string.IsNullOrWhiteSpace(GetDiscoveryUrl(target.Discovery))));
        }

        private static string GetTargetStatus(RuntimeTargetView target)
        {
            if (target.Player != null && !target.Player.Stale)
            {
                return AIBridgeEditorText.Get(AIBridgeEditorTextKey.Online);
            }

            if (target.Discovery != null && !target.Discovery.Stale && target.Discovery.Reachable)
            {
                return AIBridgeEditorText.Get(AIBridgeEditorTextKey.Reachable);
            }

            if ((target.Player != null && target.Player.Stale)
                || (target.Discovery != null && target.Discovery.Stale))
            {
                return AIBridgeEditorText.Get(AIBridgeEditorTextKey.Stale);
            }

            return AIBridgeEditorText.Get(AIBridgeEditorTextKey.Discovered);
        }

        private static Color GetTargetStatusColor(RuntimeTargetView target)
        {
            if ((target.Player != null && !target.Player.Stale)
                || (target.Discovery != null && !target.Discovery.Stale && target.Discovery.Reachable))
            {
                return new Color(0.55f, 1f, 0.55f);
            }

            if ((target.Player != null && target.Player.Stale)
                || (target.Discovery != null && target.Discovery.Stale))
            {
                return new Color(1f, 0.72f, 0.25f);
            }

            return new Color(0.65f, 0.8f, 1f);
        }

        private string FormatTargetAge(RuntimeTargetView target)
        {
            double? ageSeconds = null;
            if (target.Player != null && target.Player.AgeSeconds.HasValue)
            {
                ageSeconds = target.Player.AgeSeconds.Value;
            }

            if (target.Discovery != null && target.Discovery.AgeSeconds.HasValue
                && (!ageSeconds.HasValue || target.Discovery.AgeSeconds.Value < ageSeconds.Value))
            {
                ageSeconds = target.Discovery.AgeSeconds.Value;
            }

            if (!ageSeconds.HasValue)
            {
                return "-";
            }

            var elapsed = Math.Max(0d, EditorApplication.timeSinceStartup - _lastRefreshTime);
            return (ageSeconds.Value + elapsed).ToString("F0") + "s";
        }

        private static string BuildTargetSummary(RuntimeTargetView target)
        {
            var parts = new List<string>();
            AddSummaryPart(parts, GetTargetProject(target));
            AddSummaryPart(parts, GetTargetPlatform(target));
            if (target.Player != null)
            {
                AddSummaryPart(parts, target.Player.ActiveScene);
            }

            AddSummaryPart(parts, GetTargetUrl(target));
            return parts.Count == 0 ? "-" : string.Join("  •  ", parts.ToArray());
        }

        private static void AddSummaryPart(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        private static string GetTargetProject(RuntimeTargetView target)
        {
            if (target.Player != null)
            {
                return JoinNonEmpty(target.Player.ProductName, target.Player.ApplicationVersion);
            }

            return target.Discovery == null
                ? null
                : JoinNonEmpty(target.Discovery.ProjectName, target.Discovery.ApplicationVersion);
        }

        private static string GetTargetPlatform(RuntimeTargetView target)
        {
            return target.Player != null && !string.IsNullOrWhiteSpace(target.Player.Platform)
                ? target.Player.Platform
                : target.Discovery == null ? null : target.Discovery.Platform;
        }

        private static string GetTargetUrl(RuntimeTargetView target)
        {
            var discoveryUrl = target.Discovery == null ? null : GetDiscoveryUrl(target.Discovery);
            if (!string.IsNullOrWhiteSpace(discoveryUrl))
            {
                return discoveryUrl;
            }

            return target.Player == null ? null : target.Player.HttpUrl;
        }

        private static string GetDiscoveryUrl(AIBridgeRuntimeDiscoveredTargetInfo target)
        {
            return target != null && !string.IsNullOrWhiteSpace(target.ReachableUrl)
                ? target.ReachableUrl
                : target == null ? null : target.Url;
        }

        private void RefreshRuntimeTargets()
        {
            LoadRuntimeTargetsFromCache();
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            if (_scanLanOnRefresh
                && settings.EnableRuntimeBridge
                && !IsLoopbackBindAddress(settings.HttpBindAddress)
                && settings.EnableLanDiscovery)
            {
                BeginLanScan();
            }
        }

        private void LoadRuntimeTargetsFromCache()
        {
            _runtimeDirectory = AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory();
            _localHttpUrl = AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl();
            _players.Clear();
            _players.AddRange(AIBridgeRuntimeBridgeEditorUtility.ListPlayers());
            _discoveredTargets.Clear();
            _discoveredTargets.AddRange(AIBridgeRuntimeBridgeEditorUtility.ListDiscoveredTargets());
            RebuildRuntimeTargets();
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            _nextTargetCacheRefreshTime = _lastRefreshTime + TargetCacheRefreshIntervalSeconds;
            _nextUiRepaintTime = _lastRefreshTime + 1d;
            Repaint();
        }

        private void RebuildRuntimeTargets()
        {
            var targetsById = new Dictionary<string, RuntimeTargetView>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                var targetId = string.IsNullOrWhiteSpace(player.TargetId) ? "file-" + i : player.TargetId;
                if (!targetsById.TryGetValue(targetId, out var target))
                {
                    target = new RuntimeTargetView { TargetId = targetId };
                    targetsById.Add(targetId, target);
                }

                target.Player = player;
            }

            for (var i = 0; i < _discoveredTargets.Count; i++)
            {
                var discovery = _discoveredTargets[i];
                var targetId = string.IsNullOrWhiteSpace(discovery.TargetId)
                    ? GetDiscoveryUrl(discovery) ?? "lan-" + i
                    : discovery.TargetId;
                if (!targetsById.TryGetValue(targetId, out var target))
                {
                    target = new RuntimeTargetView { TargetId = targetId };
                    targetsById.Add(targetId, target);
                }

                if (ShouldPreferDiscovery(discovery, target.Discovery))
                {
                    target.Discovery = discovery;
                }
            }

            _targets.Clear();
            _targets.AddRange(targetsById.Values);
            _targets.Sort(CompareRuntimeTargets);
        }

        private static bool ShouldPreferDiscovery(
            AIBridgeRuntimeDiscoveredTargetInfo candidate,
            AIBridgeRuntimeDiscoveredTargetInfo current)
        {
            if (current == null)
            {
                return true;
            }

            if (candidate.Reachable != current.Reachable)
            {
                return candidate.Reachable;
            }

            if (candidate.Stale != current.Stale)
            {
                return !candidate.Stale;
            }

            return candidate.AgeSeconds.HasValue
                && (!current.AgeSeconds.HasValue || candidate.AgeSeconds.Value < current.AgeSeconds.Value);
        }

        private static int CompareRuntimeTargets(RuntimeTargetView left, RuntimeTargetView right)
        {
            var statusCompare = GetTargetSortOrder(left).CompareTo(GetTargetSortOrder(right));
            return statusCompare != 0
                ? statusCompare
                : string.Compare(left.TargetId, right.TargetId, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetTargetSortOrder(RuntimeTargetView target)
        {
            if ((target.Player != null && !target.Player.Stale)
                || (target.Discovery != null && !target.Discovery.Stale && target.Discovery.Reachable))
            {
                return 0;
            }

            if ((target.Player != null && target.Player.Stale)
                || (target.Discovery != null && target.Discovery.Stale))
            {
                return 2;
            }

            return 1;
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

        private static void CopyTargetCommand(RuntimeTargetView target, string action)
        {
            if (target == null)
            {
                return;
            }

            var url = GetTargetUrl(target);
            if (!string.IsNullOrWhiteSpace(url))
            {
                CopyHttpCommand("runtime " + action
                    + " --transport http --url " + Quote(url)
                    + " --target " + QuoteTarget(target.TargetId));
                return;
            }

            CopyFileCommand("runtime " + action
                + " --transport file --target " + QuoteTarget(target.TargetId));
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

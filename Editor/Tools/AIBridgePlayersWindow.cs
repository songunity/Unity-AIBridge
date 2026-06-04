using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public sealed class AIBridgePlayersWindow : EditorWindow
    {
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

        [MenuItem("Window/AIBridge/Players")]
        [MenuItem("AIBridge/Players")]
        public static void OpenWindow()
        {
            var window = GetWindow<AIBridgePlayersWindow>();
            window.titleContent = new GUIContent(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgePlayersTitle));
            window.minSize = new Vector2(820, 420);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshPlayers();
        }

        private void OnGUI()
        {
            titleContent = new GUIContent(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgePlayersTitle));
            DrawToolbar();
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RuntimeDirectory), _runtimeDirectory ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.HttpEntry), _localHttpUrl ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DiscoverCache), _discoveryCachePath ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            if (_lanScanRunning || !string.IsNullOrEmpty(_lanScanStatus))
            {
                EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.LanScan), _lanScanStatus ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(6);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawDiscoveredTargets();

            if (_players.Count == 0)
            {
                EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.NoFileTransportTargets), MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.FileTransportTargets), EditorStyles.boldLabel);
                for (var i = 0; i < _players.Count; i++)
                {
                    DrawPlayer(_players[i]);
                    EditorGUILayout.Space(5);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Refresh), EditorStyles.toolbarButton, GUILayout.Width(72)))
            {
                RefreshPlayers();
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
                        RefreshPlayers();
                    }
                };
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.OpenDirectory), EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                OpenRuntimeDirectory();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyListCli), EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                CopyFileCommand("runtime list_targets");
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyHttpCli), EditorStyles.toolbarButton, GUILayout.Width(112)))
            {
                CopyHttpCommand("runtime status --transport http --url " + Quote(_localHttpUrl) + " --target latest");
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyDiscoverCli), EditorStyles.toolbarButton, GUILayout.Width(128)))
            {
                var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
                CopyHttpCommand("runtime discover --udpPort " + Math.Max(1, settings.DiscoveryUdpPort));
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

        private void RefreshPlayers()
        {
            LoadPlayersFromCache();
            if (_scanLanOnRefresh)
            {
                BeginLanScan();
            }
        }

        private void LoadPlayersFromCache()
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

            LoadPlayersFromCache();
        }

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
            RefreshPlayers();
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
            RefreshPlayers();
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
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIBridge.Editor
{
    public class AIBridgeRuntimeSettingsWindow : EditorWindow
    {
        private Toggle _compileRuntimeBridge;
        private Toggle _autoInjectRuntimeBridgeInEditorPlayMode;
        private Toggle _autoInjectRuntimeBridgeInDevelopmentBuild;
        private Toggle _keepRunningInBackground;
        private Toggle _enableRuntimeCodeExecution;
        private Toggle _enableHttpTransport;
        private TextField _httpBindAddress;
        private IntegerField _httpPort;
        private Toggle _enableLanDiscovery;
        private IntegerField _discoveryUdpPort;
        private Toggle _allowRuntimeBridgeInReleaseBuild;
        private TextField _exchangeDirectory;
        private TextField _targetId;
        private TextField _authToken;
        private TextField _allowedActions;
        private Slider _heartbeatIntervalSeconds;
        private SliderInt _logBufferSize;
        private IntegerField _maxResultBytes;

        private Label _hybridClrHelp;
        private Label _runtimeCodeWarning;
        private Label _releaseWarning;
        private Label _runtimeDirectoryValue;
        private Label _runtimeHttpEntryValue;
        private Label _runtimeConfigPathValue;

        private Button _createRuntimeObject;
        private Button _applySceneRuntime;

        [MenuItem("Window/AIBridge Runtime")]
        private static void OpenWindow()
        {
            var window = GetWindow<AIBridgeRuntimeSettingsWindow>();
            window.titleContent = new GUIContent("AIBridge Runtime");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        public void CreateGUI()
        {
            var paths = new[]
            {
                "Packages/com.sh.aibridge/Editor/UI/AIBridgeRuntimeSettingsWindow.uxml",
                "Packages/AIBridge/Editor/UI/AIBridgeRuntimeSettingsWindow.uxml"
            };

            VisualTreeAsset visualTree = null;
            foreach (var path in paths)
            {
                visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (visualTree != null)
                {
                    break;
                }
            }

            if (visualTree == null)
            {
                var label = new Label("Error: Could not load UXML file. Tried paths:\n" + string.Join("\n", paths));
                label.style.color = Color.red;
                label.style.whiteSpace = WhiteSpace.Normal;
                rootVisualElement.Add(label);
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            InitializeFields();
            LoadSettings();
            SetupActionButtons();
            SetupLanguageSelector();
            // 先本地化（设置 label）再注册回调：UIToolkit 中 TextField 内部 label 的文本变化
            // 会冒泡出 ChangeEvent<string>，若回调已注册会被误当成 value 变更，污染配置。
            ApplyLocalization();
            RegisterSettingsCallbacks();
            RefreshDynamicState();
        }

        private void InitializeFields()
        {
            _compileRuntimeBridge = rootVisualElement.Q<Toggle>("compile-runtime-bridge");
            _autoInjectRuntimeBridgeInEditorPlayMode = rootVisualElement.Q<Toggle>("auto-inject-editor-play-mode");
            _autoInjectRuntimeBridgeInDevelopmentBuild = rootVisualElement.Q<Toggle>("auto-inject-development-build");
            _keepRunningInBackground = rootVisualElement.Q<Toggle>("keep-running-in-background");
            _enableRuntimeCodeExecution = rootVisualElement.Q<Toggle>("enable-runtime-code-execution");
            _enableHttpTransport = rootVisualElement.Q<Toggle>("enable-http-transport");
            _httpBindAddress = rootVisualElement.Q<TextField>("http-bind-address");
            _httpPort = rootVisualElement.Q<IntegerField>("http-port");
            _enableLanDiscovery = rootVisualElement.Q<Toggle>("enable-lan-discovery");
            _discoveryUdpPort = rootVisualElement.Q<IntegerField>("discovery-udp-port");
            _allowRuntimeBridgeInReleaseBuild = rootVisualElement.Q<Toggle>("allow-release-build");
            _exchangeDirectory = rootVisualElement.Q<TextField>("exchange-directory");
            _targetId = rootVisualElement.Q<TextField>("target-id");
            _authToken = rootVisualElement.Q<TextField>("auth-token");
            _allowedActions = rootVisualElement.Q<TextField>("allowed-actions");
            _heartbeatIntervalSeconds = rootVisualElement.Q<Slider>("heartbeat-interval-seconds");
            _logBufferSize = rootVisualElement.Q<SliderInt>("log-buffer-size");
            _maxResultBytes = rootVisualElement.Q<IntegerField>("max-result-bytes");

            _hybridClrHelp = rootVisualElement.Q<Label>("hybridclr-help");
            _runtimeCodeWarning = rootVisualElement.Q<Label>("runtime-code-warning");
            _releaseWarning = rootVisualElement.Q<Label>("release-warning");
            _runtimeDirectoryValue = rootVisualElement.Q<Label>("runtime-directory-value");
            _runtimeHttpEntryValue = rootVisualElement.Q<Label>("runtime-http-entry-value");
            _runtimeConfigPathValue = rootVisualElement.Q<Label>("runtime-config-path-value");

            _createRuntimeObject = rootVisualElement.Q<Button>("create-runtime-object");
            _applySceneRuntime = rootVisualElement.Q<Button>("apply-scene-runtime");

            _httpBindAddress.isDelayed = true;
            _exchangeDirectory.isDelayed = true;
            _targetId.isDelayed = true;
            _authToken.isDelayed = true;
            _allowedActions.isDelayed = true;
        }

        private void LoadSettings()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;

            _compileRuntimeBridge.SetValueWithoutNotify(settings.EnableRuntimeBridge);
            _autoInjectRuntimeBridgeInEditorPlayMode.SetValueWithoutNotify(settings.AutoInjectRuntimeBridgeInEditorPlayMode);
            _autoInjectRuntimeBridgeInDevelopmentBuild.SetValueWithoutNotify(settings.AutoInjectRuntimeBridgeInDevelopmentBuild);
            _keepRunningInBackground.SetValueWithoutNotify(settings.KeepRunningInBackground);
            _enableRuntimeCodeExecution.SetValueWithoutNotify(settings.EnableRuntimeCodeExecution);
            _enableHttpTransport.SetValueWithoutNotify(settings.EnableHttpTransport);
            _httpBindAddress.SetValueWithoutNotify(settings.HttpBindAddress ?? string.Empty);
            _httpPort.SetValueWithoutNotify(settings.HttpPort);
            _enableLanDiscovery.SetValueWithoutNotify(settings.EnableLanDiscovery);
            _discoveryUdpPort.SetValueWithoutNotify(settings.DiscoveryUdpPort);
            _allowRuntimeBridgeInReleaseBuild.SetValueWithoutNotify(settings.AllowRuntimeBridgeInReleaseBuild);
            _exchangeDirectory.SetValueWithoutNotify(
                string.IsNullOrWhiteSpace(settings.ExchangeDirectory)
                    ? AIBridgeRuntimeBridgeEditorUtility.GetDefaultRuntimeDirectory()
                    : settings.ExchangeDirectory);
            _targetId.SetValueWithoutNotify(settings.TargetId ?? string.Empty);
            _authToken.SetValueWithoutNotify(settings.AuthToken ?? string.Empty);
            _allowedActions.SetValueWithoutNotify(settings.AllowedActions ?? string.Empty);
            _heartbeatIntervalSeconds.SetValueWithoutNotify(settings.HeartbeatIntervalSeconds);
            _logBufferSize.SetValueWithoutNotify(settings.LogBufferSize);
            _maxResultBytes.SetValueWithoutNotify(settings.MaxResultBytes);

            _autoInjectRuntimeBridgeInEditorPlayMode.SetEnabled(false);
        }

        private void RegisterSettingsCallbacks()
        {
            _compileRuntimeBridge.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.EnableRuntimeBridge = evt.newValue;
                SaveRuntimeSettings();
            });

            _autoInjectRuntimeBridgeInEditorPlayMode.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.AutoInjectRuntimeBridgeInEditorPlayMode = evt.newValue;
                SaveRuntimeSettings();
            });

            _autoInjectRuntimeBridgeInDevelopmentBuild.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.AutoInjectRuntimeBridgeInDevelopmentBuild = evt.newValue;
                SaveRuntimeSettings();
            });

            _keepRunningInBackground.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.KeepRunningInBackground = evt.newValue;
                SaveRuntimeSettings();
            });

            _enableRuntimeCodeExecution.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.EnableRuntimeCodeExecution = evt.newValue;
                SaveRuntimeSettings();
            });

            _enableHttpTransport.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.EnableHttpTransport = evt.newValue;
                SaveRuntimeSettings();
            });

            _httpBindAddress.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != _httpBindAddress)
                {
                    return;
                }

                AIBridgeProjectSettings.Instance.RuntimeBridge.HttpBindAddress = evt.newValue ?? string.Empty;
                SaveRuntimeSettings();
            });

            _httpPort.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.HttpPort = evt.newValue;
                SaveRuntimeSettings();
            });

            _enableLanDiscovery.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.EnableLanDiscovery = evt.newValue;
                SaveRuntimeSettings();
            });

            _discoveryUdpPort.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.DiscoveryUdpPort = evt.newValue;
                SaveRuntimeSettings();
            });

            _allowRuntimeBridgeInReleaseBuild.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.AllowRuntimeBridgeInReleaseBuild = evt.newValue;
                SaveRuntimeSettings();
            });

            _exchangeDirectory.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != _exchangeDirectory)
                {
                    return;
                }

                // 输入等于默认路径时存空字符串，保持「跟随默认」语义，避免固化绝对路径
                var input = evt.newValue ?? string.Empty;
                var isDefault = string.Equals(
                    input.Trim(),
                    AIBridgeRuntimeBridgeEditorUtility.GetDefaultRuntimeDirectory(),
                    StringComparison.Ordinal);
                AIBridgeProjectSettings.Instance.RuntimeBridge.ExchangeDirectory =
                    isDefault ? string.Empty : input;
                SaveRuntimeSettings();
            });

            _targetId.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != _targetId)
                {
                    return;
                }

                AIBridgeProjectSettings.Instance.RuntimeBridge.TargetId = evt.newValue ?? string.Empty;
                SaveRuntimeSettings();
            });

            _authToken.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != _authToken)
                {
                    return;
                }

                AIBridgeProjectSettings.Instance.RuntimeBridge.AuthToken = evt.newValue ?? string.Empty;
                SaveRuntimeSettings();
            });

            _allowedActions.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != _allowedActions)
                {
                    return;
                }

                AIBridgeProjectSettings.Instance.RuntimeBridge.AllowedActions = evt.newValue ?? string.Empty;
                SaveRuntimeSettings();
            });

            _heartbeatIntervalSeconds.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.HeartbeatIntervalSeconds = evt.newValue;
                SaveRuntimeSettings();
            });

            _logBufferSize.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.LogBufferSize = evt.newValue;
                SaveRuntimeSettings();
            });

            _maxResultBytes.RegisterValueChangedCallback(evt =>
            {
                AIBridgeProjectSettings.Instance.RuntimeBridge.MaxResultBytes = evt.newValue;
                SaveRuntimeSettings();
            });
        }

        private void SetupActionButtons()
        {
#if AIBRIDGE_RUNTIME_ENABLED
            _createRuntimeObject.clicked += CreateOrSelectRuntimeObject;
            _applySceneRuntime.clicked += () => ApplySettingsToSceneRuntimes(showDialog: true);
#else
            _createRuntimeObject.SetEnabled(false);
            _applySceneRuntime.SetEnabled(false);
#endif

            rootVisualElement.Q<Button>("open-runtime-directory").clicked += OpenRuntimeDirectory;
            rootVisualElement.Q<Button>("copy-launch-args").clicked += CopyLaunchArguments;
            rootVisualElement.Q<Button>("write-runtime-config").clicked += WriteRuntimeConfig;
            rootVisualElement.Q<Button>("copy-http-status-cli").clicked += CopyHttpStatusCommand;
            rootVisualElement.Q<Button>("copy-discover-cli").clicked += CopyDiscoverCommand;
        }

        private void SetupLanguageSelector()
        {
            var languageSelector = rootVisualElement.Q<DropdownField>("language-selector");
            var languageLabels = AIBridgeEditorText.LanguageLabels.ToList();

            languageSelector.choices = languageLabels;
            languageSelector.SetValueWithoutNotify(languageLabels[AIBridgeEditorText.GetLanguageIndex(AIBridgeEditorText.Language)]);
            languageSelector.RegisterValueChangedCallback(evt =>
            {
                var languageIndex = languageLabels.IndexOf(evt.newValue);
                if (languageIndex < 0)
                {
                    return;
                }

                AIBridgeProjectSettings.Instance.EditorLanguage = AIBridgeEditorText.LanguageValues[languageIndex];
                AIBridgeProjectSettings.Instance.SaveSettings();
                ApplyLocalization();
            });
        }

        private void ApplyLocalization()
        {
            titleContent = new GUIContent(AIBridgeEditorText.T("AIBridge Runtime", "AIBridge Runtime"));

            var languageSelector = rootVisualElement.Q<DropdownField>("language-selector");
            languageSelector.SetValueWithoutNotify(
                AIBridgeEditorText.LanguageLabels[AIBridgeEditorText.GetLanguageIndex(AIBridgeEditorText.Language)]);

            rootVisualElement.Q<Label>("header-title").text =
                AIBridgeEditorText.T("AIBridge Runtime", "AIBridge Runtime");
            rootVisualElement.Q<Label>("header-subtitle").text =
                AIBridgeEditorText.T(
                    "Configure Runtime Bridge for Play Mode and Player builds",
                    "配置 Play Mode 和 Player Build 的 Runtime Bridge");

            rootVisualElement.Q<Label>("runtime-bridge-title").text =
                AIBridgeEditorText.T("Runtime Bridge", "Runtime Bridge");
            rootVisualElement.Q<Label>("runtime-bridge-help").text =
                AIBridgeEditorText.T(
                    "Runtime Bridge lets AIBridgeCLI connect to AIBridgeRuntime inside Play Mode or a built Player. Release builds remain disabled unless explicitly allowed.",
                    "Runtime Bridge 允许 AIBridgeCLI 连接 Play Mode 或已编译 Player 内的 AIBridgeRuntime。Release Build 默认关闭，除非显式允许。");

            rootVisualElement.Q<Label>("settings-title").text = AIBridgeEditorText.T("Settings", "设置");
            _compileRuntimeBridge.label = AIBridgeEditorText.T("Compile Runtime Bridge", "编译 Runtime Bridge");
            _autoInjectRuntimeBridgeInEditorPlayMode.label =
                AIBridgeEditorText.T("Auto Inject In Editor Play Mode (Planned)", "Editor Play Mode 自动注入 (Planned)");
            rootVisualElement.Q<Label>("auto-inject-editor-play-mode-help").text =
                AIBridgeEditorText.T("This feature is not yet implemented.", "此功能尚未实现。");
            _autoInjectRuntimeBridgeInDevelopmentBuild.label =
                AIBridgeEditorText.T("Auto Inject In Development Build", "Development Build 自动注入");
            _keepRunningInBackground.label = AIBridgeEditorText.T("Keep Running In Background", "后台保持运行");
            _enableRuntimeCodeExecution.label =
                AIBridgeEditorText.T("Enable Runtime Code Execution", "启用 Runtime 代码执行");
            _hybridClrHelp.text = AIBridgeEditorText.T(
                "HybridCLR package is not installed. Runtime code execution will stay disabled to avoid IL2CPP Assembly.Load failures.",
                "当前未安装 HybridCLR 包。Runtime 代码执行会保持关闭，避免 IL2CPP 下 Assembly.Load 失败。");
            _runtimeCodeWarning.text = AIBridgeEditorText.T(
                "Runtime code execution loads Roslyn-compiled DLLs in Player by Assembly.Load. Keep it for trusted debugging builds only.",
                "Runtime 代码执行会在 Player 中通过 Assembly.Load 加载 Roslyn 编译的 DLL。仅用于可信调试构建。");
            _allowRuntimeBridgeInReleaseBuild.label =
                AIBridgeEditorText.T("Allow Runtime Bridge In Release Build", "允许 Release Build 启用 Runtime Bridge");
            _releaseWarning.text = AIBridgeEditorText.T(
                "Release Build Runtime Bridge is a debugging backdoor. Use an auth token and restrict Allowed Actions before shipping.",
                "Release Build Runtime Bridge 是调试入口。发布前请设置鉴权 Token 并限制 Allowed Actions。");

            rootVisualElement.Q<Label>("http-transport-title").text =
                AIBridgeEditorText.T("HTTP Transport Settings", "HTTP Transport 设置");
            _enableHttpTransport.label = AIBridgeEditorText.T("Enable HTTP Transport", "启用 HTTP Transport");
            _httpBindAddress.label = AIBridgeEditorText.T("HTTP Bind Address", "HTTP 监听地址");
            _httpPort.label = AIBridgeEditorText.T("HTTP Port", "HTTP 端口");
            _enableLanDiscovery.label = AIBridgeEditorText.T("Enable LAN Discovery", "启用局域网自动发现");
            _discoveryUdpPort.label = AIBridgeEditorText.T("Discovery UDP Port", "发现 UDP 端口");

            rootVisualElement.Q<Label>("runtime-config-title").text =
                AIBridgeEditorText.T("Runtime Config", "Runtime 配置");
            _exchangeDirectory.label = AIBridgeEditorText.T("Runtime Directory", "Runtime 目录");
            _targetId.label = AIBridgeEditorText.T("Default Target Id", "默认 Target Id");
            _authToken.label = AIBridgeEditorText.T("Auth Token", "鉴权 Token");
            _allowedActions.label = AIBridgeEditorText.T("Allowed Actions", "允许的 Actions");
            _heartbeatIntervalSeconds.label = AIBridgeEditorText.T("Heartbeat Interval", "Heartbeat 间隔");
            _logBufferSize.label = AIBridgeEditorText.T("Log Buffer Size", "日志缓存数量");
            _maxResultBytes.label = AIBridgeEditorText.T("Max Result Bytes", "最大结果字节数");
            rootVisualElement.Q<Label>("allowed-actions-help").text =
                AIBridgeEditorText.T(
                    "Allowed Actions accepts comma, semicolon, or newline separated runtime action names, including built-in actions (e.g. runtime.status, runtime.code.execute). Empty means all built-in actions are allowed; custom actions are allowed in Editor/Development Build and blocked in Release Build.",
                    "Allowed Actions 支持用逗号、分号或换行分隔 Runtime action 名称，包括内置 action（如 runtime.status、runtime.code.execute）。为空时所有内置 action 允许；自定义 action 在 Editor/Development Build 允许，Release Build 阻止。");

            rootVisualElement.Q<Label>("info-display-title").text = AIBridgeEditorText.T("Runtime Info", "Runtime 信息");
            rootVisualElement.Q<Label>("runtime-directory-label").text =
                AIBridgeEditorText.T("Resolved Runtime Directory:", "解析后的 Runtime 目录：");
            rootVisualElement.Q<Label>("runtime-http-entry-label").text =
                AIBridgeEditorText.T("Runtime HTTP Entry:", "Runtime HTTP 入口：");
            rootVisualElement.Q<Label>("runtime-config-path-label").text =
                AIBridgeEditorText.T("Runtime Config path:", "Runtime 配置路径：");

            rootVisualElement.Q<Label>("actions-title").text = AIBridgeEditorText.T("Actions", "操作");
            _createRuntimeObject.text = AIBridgeEditorText.T("Create Runtime Object", "创建 Runtime 对象");
            _applySceneRuntime.text = AIBridgeEditorText.T("Apply To Scene Runtime", "应用到场景 Runtime");
            rootVisualElement.Q<Button>("open-runtime-directory").text =
                AIBridgeEditorText.T("Open Runtime Directory", "打开 Runtime 目录");
            rootVisualElement.Q<Button>("copy-launch-args").text =
                AIBridgeEditorText.T("Copy Launch Args", "复制启动参数");
            rootVisualElement.Q<Button>("write-runtime-config").text =
                AIBridgeEditorText.T("Write Runtime Config", "写入 Runtime 配置");
            rootVisualElement.Q<Button>("copy-http-status-cli").text =
                AIBridgeEditorText.T("Copy HTTP Status CLI", "复制 HTTP 状态命令");
            rootVisualElement.Q<Button>("copy-discover-cli").text =
                AIBridgeEditorText.T("Copy Discover CLI", "复制发现命令");

#if !AIBRIDGE_RUNTIME_ENABLED
            var disabledTooltip = AIBridgeEditorText.T(
                "Requires Compile Runtime Bridge to be enabled",
                "需要启用 Compile Runtime Bridge");
            _createRuntimeObject.tooltip = disabledTooltip;
            _applySceneRuntime.tooltip = disabledTooltip;
#endif
        }

        private void SaveRuntimeSettings()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;

            settings.MaxResultBytes = Math.Max(1024, settings.MaxResultBytes);
            settings.HttpBindAddress = string.IsNullOrWhiteSpace(settings.HttpBindAddress)
                ? AIBridgeProjectSettings.DefaultRuntimeBridgeHttpBindAddress
                : settings.HttpBindAddress.Trim();
            settings.HttpPort = Math.Max(1, settings.HttpPort);
            settings.DiscoveryUdpPort = Math.Max(1, settings.DiscoveryUdpPort);

            _httpBindAddress.SetValueWithoutNotify(settings.HttpBindAddress);
            _httpPort.SetValueWithoutNotify(settings.HttpPort);
            _discoveryUdpPort.SetValueWithoutNotify(settings.DiscoveryUdpPort);
            _maxResultBytes.SetValueWithoutNotify(settings.MaxResultBytes);

            AIBridgeProjectSettings.Instance.SaveSettings();
            AIBridgeRuntimeBridgeEditorUtility.WriteRuntimeConfig();
            AIBridgeRuntimeBuildProcessor.SyncRuntimeBootstrapDefinesForActiveTarget();
            RefreshDynamicState();
        }

        private void RefreshDynamicState()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            var hybridClrInstalled = AIBridgeHybridClrUtility.IsHybridClrInstalled();

            _enableRuntimeCodeExecution.SetEnabled(hybridClrInstalled);
            _hybridClrHelp.style.display = hybridClrInstalled ? DisplayStyle.None : DisplayStyle.Flex;
            _runtimeCodeWarning.style.display =
                hybridClrInstalled && settings.EnableRuntimeCodeExecution ? DisplayStyle.Flex : DisplayStyle.None;
            _releaseWarning.style.display = settings.AllowRuntimeBridgeInReleaseBuild ? DisplayStyle.Flex : DisplayStyle.None;

            _httpBindAddress.SetEnabled(settings.EnableHttpTransport);
            _httpPort.SetEnabled(settings.EnableHttpTransport);
            _enableLanDiscovery.SetEnabled(settings.EnableHttpTransport);
            _discoveryUdpPort.SetEnabled(settings.EnableHttpTransport && settings.EnableLanDiscovery);

            _runtimeDirectoryValue.text = AIBridgeRuntimeBridgeEditorUtility.GetRuntimeDirectory();
            _runtimeHttpEntryValue.text = AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl();
            _runtimeConfigPathValue.text = AIBridgeRuntimeBridgeEditorUtility.GetRuntimeConfigPath();
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
                    AIBridgeEditorText.T(
                        $"Applied Runtime Bridge settings to {runtimes.Length} scene runtime object(s).",
                        $"已将 Runtime Bridge 设置应用到 {runtimes.Length} 个场景 Runtime 对象。"),
                    AIBridgeEditorText.T("OK", "确定"));
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
            Debug.Log(AIBridgeEditorText.T("[AIBridge] Runtime launch arguments copied.", "[AIBridge] Runtime 启动参数已复制。"));
        }

        private static void WriteRuntimeConfig()
        {
            var path = AIBridgeRuntimeBridgeEditorUtility.WriteRuntimeConfig();
            Debug.Log(AIBridgeEditorText.T(
                "[AIBridge] Runtime config written: " + path,
                "[AIBridge] Runtime 配置已写入：" + path));
        }

        private static void CopyHttpStatusCommand()
        {
            EditorGUIUtility.systemCopyBuffer = AIBridgeRuntimeBridgeEditorUtility.BuildCliCommand(
                "runtime status --transport http --url " + AIBridgeRuntimeBridgeEditorUtility.Quote(AIBridgeRuntimeBridgeEditorUtility.BuildLocalHttpUrl()) + " --target latest",
                includeRuntimeDirectory: false);
            Debug.Log(AIBridgeEditorText.T("[AIBridge] Runtime HTTP CLI command copied.", "[AIBridge] Runtime HTTP CLI 命令已复制。"));
        }

        private static void CopyDiscoverCommand()
        {
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            EditorGUIUtility.systemCopyBuffer = AIBridgeRuntimeBridgeEditorUtility.BuildCliCommand(
                "runtime discover --udpPort " + Math.Max(1, settings.DiscoveryUdpPort),
                includeRuntimeDirectory: false);
            Debug.Log(AIBridgeEditorText.T("[AIBridge] Runtime discovery CLI command copied.", "[AIBridge] Runtime 自动发现 CLI 命令已复制。"));
        }
    }
}

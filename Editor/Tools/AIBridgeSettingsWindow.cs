using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIBridge.Editor
{
    /// <summary>
    /// AI Bridge settings window using UI Toolkit.
    /// </summary>
    public class AIBridgeSettingsWindow : EditorWindow
    {
        private VisualElement _currentTab;
        
        // Settings fields
        private Toggle _bridgeEnabled;
        private Toggle _debugLogging;
        private Slider _gifDuration;
        private SliderInt _gifFps;
        private Slider _gifScale;
        private SliderInt _gifColorCount;
        private Toggle _autoScan;
        private TextField _scanAssemblies;

        // Agent selection
        private Toggle _agentCodex;
        private Toggle _agentClaude;
        private Toggle _agentKiro;

        private const string PrefKeyAgentCodex = "AIBridge.SkillAgent.Codex";
        private const string PrefKeyAgentClaude = "AIBridge.SkillAgent.Claude";
        private const string PrefKeyAgentKiro = "AIBridge.SkillAgent.Kiro";

        [MenuItem("Window/AIBridge")]
        private static void OpenWindow()
        {
            var window = GetWindow<AIBridgeSettingsWindow>();
            window.titleContent = new GUIContent("AI Bridge Settings");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        public void CreateGUI()
        {
            // Load UXML - try multiple possible paths
            var paths = new[]
            {
                "Packages/com.sh.aibridge/Editor/UI/AIBridgeSettingsWindow.uxml",
                "Packages/AIBridge/Editor/UI/AIBridgeSettingsWindow.uxml"
            };

            VisualTreeAsset visualTree = null;
            foreach (var path in paths)
            {
                visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (visualTree != null)
                    break;
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

            // Initialize UI
            InitializeFields();
            LoadSettings();
            SetupTabButtons();
            SetupActionButtons();
            SetupLanguageSelector();
            ApplyLocalization();
        }

        private void InitializeFields()
        {
            // General tab
            _bridgeEnabled = rootVisualElement.Q<Toggle>("bridge-enabled");
            _debugLogging = rootVisualElement.Q<Toggle>("debug-logging");
            
            // Directory info
            var queueDir = rootVisualElement.Q<TextField>("queue-dir");
            var screenshotDir = rootVisualElement.Q<TextField>("screenshot-dir");
            var cliPath = rootVisualElement.Q<TextField>("cli-path");
            
            queueDir.value = AIBridge.BridgeDirectory;
            screenshotDir.value = ScreenshotHelper.ScreenshotsDir;
            cliPath.value = AIBridge.BridgeCLI;

            // GIF tab
            _gifDuration = rootVisualElement.Q<Slider>("gif-duration");
            _gifFps = rootVisualElement.Q<SliderInt>("gif-fps");
            _gifScale = rootVisualElement.Q<Slider>("gif-scale");
            _gifColorCount = rootVisualElement.Q<SliderInt>("gif-color-count");

            // Commands tab
            _autoScan = rootVisualElement.Q<Toggle>("auto-scan");
            _scanAssemblies = rootVisualElement.Q<TextField>("scan-assemblies");

            // Agent selection toggles
            _agentCodex = rootVisualElement.Q<Toggle>("agent-codex");
            _agentClaude = rootVisualElement.Q<Toggle>("agent-claude");
            _agentKiro = rootVisualElement.Q<Toggle>("agent-kiro");
            
            // Setup auto-scan toggle listener
            _autoScan.RegisterValueChangedCallback(evt => UpdateRefreshButtonVisibility());
            
            UpdateCommandCount();
        }

        private void LoadSettings()
        {
            _bridgeEnabled.value = AIBridge.Enabled;
            _debugLogging.value = AIBridgeLogger.DebugEnabled;

            _gifDuration.value = GifRecorderSettings.DefaultDuration;
            _gifFps.value = GifRecorderSettings.DefaultFps;
            _gifScale.value = GifRecorderSettings.DefaultScale;
            _gifColorCount.value = GifRecorderSettings.DefaultColorCount;

            _scanAssemblies.value = EditorPrefs.GetString(
                CommandRegistry.PrefKeyScanAssemblies, 
                "Assembly-CSharp-Editor-firstpass;Assembly-CSharp");

            if (!CommandRegistry.IsEditablePackage)
            {
                _autoScan.value = true;
                _autoScan.SetEnabled(false);
            }
            else
            {
                _autoScan.value = EditorPrefs.GetBool(CommandRegistry.PrefKeyAutoScan, false);
            }

            _agentCodex.value = EditorPrefs.GetBool(PrefKeyAgentCodex, true);
            _agentClaude.value = EditorPrefs.GetBool(PrefKeyAgentClaude, true);
            _agentKiro.value = EditorPrefs.GetBool(PrefKeyAgentKiro, false);

            UpdateRefreshButtonVisibility();
        }

        private void SetupTabButtons()
        {
            var tabGeneral = rootVisualElement.Q<Button>("tab-general");
            var tabGif = rootVisualElement.Q<Button>("tab-gif");
            var tabCommands = rootVisualElement.Q<Button>("tab-commands");
            var tabTools = rootVisualElement.Q<Button>("tab-tools");

            tabGeneral.clicked += () => SwitchTab("content-general", tabGeneral);
            tabGif.clicked += () => SwitchTab("content-gif", tabGif);
            tabCommands.clicked += () => SwitchTab("content-commands", tabCommands);
            tabTools.clicked += () => SwitchTab("content-tools", tabTools);

            // Set initial tab
            _currentTab = rootVisualElement.Q<VisualElement>("content-general");
        }

        private void SwitchTab(string contentName, Button activeButton)
        {
            // Hide all tabs
            rootVisualElement.Q<VisualElement>("content-general").style.display = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("content-gif").style.display = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("content-commands").style.display = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("content-tools").style.display = DisplayStyle.None;

            // Remove active class from all buttons
            rootVisualElement.Q<Button>("tab-general").RemoveFromClassList("tab-active");
            rootVisualElement.Q<Button>("tab-gif").RemoveFromClassList("tab-active");
            rootVisualElement.Q<Button>("tab-commands").RemoveFromClassList("tab-active");
            rootVisualElement.Q<Button>("tab-tools").RemoveFromClassList("tab-active");

            // Show selected tab
            _currentTab = rootVisualElement.Q<VisualElement>(contentName);
            _currentTab.style.display = DisplayStyle.Flex;
            activeButton.AddToClassList("tab-active");
        }

        private void SetupActionButtons()
        {
            // Directory buttons
            rootVisualElement.Q<Button>("open-queue-dir").clicked += () => 
                EditorUtility.RevealInFinder(AIBridge.BridgeDirectory);
            rootVisualElement.Q<Button>("open-screenshot-dir").clicked += () => 
                EditorUtility.RevealInFinder(ScreenshotHelper.ScreenshotsDir);
            rootVisualElement.Q<Button>("open-cli-dir").clicked += () => 
                EditorUtility.RevealInFinder(Path.GetDirectoryName(AIBridge.BridgeCLI));

            // Command buttons
            rootVisualElement.Q<Button>("refresh-commands").clicked += () =>
            {
                CommandRegistry.Scan();
                UpdateCommandCount();
                SkillInstaller.GenerateSkillFile();
                SkillInstaller.OverrideSkill();
            };

            // Tools buttons
            rootVisualElement.Q<Button>("generate-skill").clicked += ()=>
            {
                SkillInstaller.GenerateSkillFile();
                SkillInstaller.OverrideSkill();
            };
            rootVisualElement.Q<Button>("install-skill-agent").clicked += SkillInstaller.CopyToAgent;

            rootVisualElement.Q<Button>("one-click-skill").clicked += () =>
            {
                var targets = new System.Collections.Generic.List<string>();
                if (_agentCodex.value) targets.Add(".agents");
                if (_agentClaude.value) targets.Add(".claude");
                if (_agentKiro.value) targets.Add(".kiro");

                if (targets.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        AIBridgeEditorText.T("Warning", "警告"),
                        AIBridgeEditorText.T("Please select at least one agent.", "请至少选择一个 Agent。"),
                        AIBridgeEditorText.T("OK", "确定"));
                    return;
                }

                SkillInstaller.GenerateSkillFile();
                SkillInstaller.CopyToAgent(targets.ToArray());

                EditorUtility.DisplayDialog(
                    AIBridgeEditorText.T("Success", "成功"),
                    AIBridgeEditorText.T(
                        $"Skill generated and installed to: {string.Join(", ", targets)}",
                        $"Skill 已生成并安装到：{string.Join(", ", targets)}"),
                    AIBridgeEditorText.T("OK", "确定"));
            };
            rootVisualElement.Q<Button>("clear-cache").clicked += ClearCache;
            rootVisualElement.Q<Button>("reset-settings").clicked += ResetSettings;
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
            titleContent = new GUIContent(AIBridgeEditorText.T("AI Bridge Settings", "AI Bridge 设置"));

            var languageSelector = rootVisualElement.Q<DropdownField>("language-selector");
            languageSelector.SetValueWithoutNotify(
                AIBridgeEditorText.LanguageLabels[AIBridgeEditorText.GetLanguageIndex(AIBridgeEditorText.Language)]);

            rootVisualElement.Q<Label>("header-title").text =
                AIBridgeEditorText.T("AI Bridge Settings", "AI Bridge 设置");
            rootVisualElement.Q<Label>("header-subtitle").text =
                AIBridgeEditorText.T("Configure AI Bridge behavior and tools", "配置 AI Bridge 行为和工具");

            rootVisualElement.Q<Button>("tab-general").text = AIBridgeEditorText.T("General", "通用");
            rootVisualElement.Q<Button>("tab-gif").text = AIBridgeEditorText.T("GIF Recorder", "GIF 录制");
            rootVisualElement.Q<Button>("tab-commands").text = AIBridgeEditorText.T("Commands", "命令");
            rootVisualElement.Q<Button>("tab-tools").text = AIBridgeEditorText.T("Tools", "工具");

            rootVisualElement.Q<Label>("quick-skill-title").text =
                AIBridgeEditorText.T("Quick Skill Install", "快速 Skill 安装");
            rootVisualElement.Q<Label>("quick-skill-help").text =
                AIBridgeEditorText.T(
                    "Generate SKILL.md and copy to selected agent directories",
                    "生成 SKILL.md 并复制到选定的 Agent 目录");
            rootVisualElement.Q<Button>("one-click-skill").text =
                AIBridgeEditorText.T("Generate and Install Skill", "生成并安装 Skill");
            rootVisualElement.Q<Toggle>("agent-codex").label =
                AIBridgeEditorText.T("Codex (.agents)", "Codex (.agents)");
            rootVisualElement.Q<Toggle>("agent-claude").label =
                AIBridgeEditorText.T("Claude Code (.claude)", "Claude Code (.claude)");
            rootVisualElement.Q<Toggle>("agent-kiro").label =
                AIBridgeEditorText.T("Kiro (.kiro)", "Kiro (.kiro)");

            rootVisualElement.Q<Label>("bridge-settings-title").text =
                AIBridgeEditorText.T("Bridge Settings", "Bridge 设置");
            rootVisualElement.Q<Toggle>("bridge-enabled").label =
                AIBridgeEditorText.T("Enable AI Bridge", "启用 AI Bridge");
            rootVisualElement.Q<Toggle>("debug-logging").label =
                AIBridgeEditorText.T("Debug Logging", "调试日志");

            rootVisualElement.Q<Label>("directory-info-title").text =
                AIBridgeEditorText.T("Directory Information", "目录信息");
            rootVisualElement.Q<Label>("command-queue-label").text =
                AIBridgeEditorText.T("Command Queue:", "命令队列：");
            rootVisualElement.Q<Label>("screenshots-label").text =
                AIBridgeEditorText.T("Screenshots:", "截图：");
            rootVisualElement.Q<Label>("cli-path-label").text =
                AIBridgeEditorText.T("CLI Path:", "CLI 路径：");
            rootVisualElement.Q<Button>("open-queue-dir").text = AIBridgeEditorText.T("Open", "打开");
            rootVisualElement.Q<Button>("open-screenshot-dir").text = AIBridgeEditorText.T("Open", "打开");
            rootVisualElement.Q<Button>("open-cli-dir").text = AIBridgeEditorText.T("Open", "打开");

            rootVisualElement.Q<Label>("shortcuts-title").text =
                AIBridgeEditorText.T("Shortcuts", "快捷键");
            rootVisualElement.Q<Label>("shortcuts-help").text =
                AIBridgeEditorText.T(
                    "F12 - Screenshot Game View (Play Mode)\nF11 - Start/Stop GIF Recording (Play Mode)",
                    "F12 - 截取 Game View（播放模式）\nF11 - 开始/停止 GIF 录制（播放模式）");

            rootVisualElement.Q<Label>("gif-settings-title").text =
                AIBridgeEditorText.T("GIF Recording Settings", "GIF 录制设置");
            rootVisualElement.Q<Label>("gif-settings-help").text =
                AIBridgeEditorText.T(
                    "Uses streaming encoding: frames are encoded and written to disk immediately, minimizing memory usage.",
                    "使用流式编码：帧会立即编码并写入磁盘，尽量减少内存占用。");
            rootVisualElement.Q<Slider>("gif-duration").label =
                AIBridgeEditorText.T("Duration (seconds)", "时长（秒）");
            rootVisualElement.Q<SliderInt>("gif-fps").label = AIBridgeEditorText.T("FPS", "帧率");
            rootVisualElement.Q<Slider>("gif-scale").label = AIBridgeEditorText.T("Scale", "缩放");
            rootVisualElement.Q<SliderInt>("gif-color-count").label =
                AIBridgeEditorText.T("Color Count", "颜色数");

            rootVisualElement.Q<Label>("command-registration-title").text =
                AIBridgeEditorText.T("Command Registration", "命令注册");
            rootVisualElement.Q<Toggle>("auto-scan").label =
                AIBridgeEditorText.T("Auto-scan Assemblies", "自动扫描程序集");
            rootVisualElement.Q<Label>("auto-scan-help").text =
                AIBridgeEditorText.T(
                    "When enabled, commands are scanned at runtime. When disabled, commands are pre-registered in code for better performance.",
                    "启用后会在运行时扫描命令；禁用后命令会在代码中预注册以提升性能。");
            rootVisualElement.Q<TextField>("scan-assemblies").label =
                AIBridgeEditorText.T("Scan Assemblies", "扫描程序集");
            rootVisualElement.Q<Label>("scan-assemblies-help").text =
                AIBridgeEditorText.T(
                    "Separate multiple assemblies with semicolons (e.g., Assembly-CSharp;Assembly-CSharp-Editor)",
                    "多个程序集请用分号分隔（例如 Assembly-CSharp;Assembly-CSharp-Editor）");

            rootVisualElement.Q<Label>("registered-commands-title").text =
                AIBridgeEditorText.T("Registered Commands", "已注册命令");
            rootVisualElement.Q<Button>("refresh-commands").text =
                AIBridgeEditorText.T("Refresh Command List", "刷新命令列表");

            rootVisualElement.Q<Label>("skill-doc-title").text =
                AIBridgeEditorText.T("Skill Documentation", "Skill 文档");
            rootVisualElement.Q<Label>("skill-doc-help").text =
                AIBridgeEditorText.T(
                    "Generate SKILL.md file for Droid integration",
                    "为 Droid 集成生成 SKILL.md 文件");
            rootVisualElement.Q<Button>("generate-skill").text =
                AIBridgeEditorText.T("Generate SKILL.md", "生成 SKILL.md");

            rootVisualElement.Q<Label>("skill-install-title").text =
                AIBridgeEditorText.T("Skill Installation", "Skill 安装");
            rootVisualElement.Q<Label>("skill-install-help").text =
                AIBridgeEditorText.T(
                    "Copy AIBridge skill to agent directory",
                    "将 AIBridge Skill 复制到 Agent 目录");
            rootVisualElement.Q<Button>("install-skill-agent").text =
                AIBridgeEditorText.T("Copy to Agent", "复制到 Agent");

            rootVisualElement.Q<Label>("maintenance-title").text =
                AIBridgeEditorText.T("Maintenance", "维护");
            rootVisualElement.Q<Button>("clear-cache").text =
                AIBridgeEditorText.T("Clear Screenshot Cache", "清除截图缓存");
            rootVisualElement.Q<Button>("reset-settings").text =
                AIBridgeEditorText.T("Reset All Settings", "重置所有设置");

            UpdateCommandCount();
        }

        private void OnDestroy()
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            AIBridge.Enabled = _bridgeEnabled.value;
            AIBridgeLogger.DebugEnabled = _debugLogging.value;

            GifRecorderSettings.DefaultDuration = _gifDuration.value;
            GifRecorderSettings.DefaultFps = _gifFps.value;
            GifRecorderSettings.DefaultScale = _gifScale.value;
            GifRecorderSettings.DefaultColorCount = _gifColorCount.value;

            EditorPrefs.SetBool(CommandRegistry.PrefKeyAutoScan, _autoScan.value);
            EditorPrefs.SetString(CommandRegistry.PrefKeyScanAssemblies, _scanAssemblies.value);

            EditorPrefs.SetBool(PrefKeyAgentCodex, _agentCodex.value);
            EditorPrefs.SetBool(PrefKeyAgentClaude, _agentClaude.value);
            EditorPrefs.SetBool(PrefKeyAgentKiro, _agentKiro.value);

            if (_autoScan.value)
            {
                CommandRegistry.Scan();
            }

            Debug.Log("[AIBridge] Settings saved.");
        }

        private void UpdateCommandCount()
        {
            var entries = CommandRegistry.GetAll().ToList();
            var label = rootVisualElement.Q<Label>("command-count");
            label.text = AIBridgeEditorText.T(
                $"Total registered commands: {entries.Count}",
                $"已注册命令总数：{entries.Count}");

            var listView = rootVisualElement.Q<ScrollView>("command-list");
            listView.Clear();

            var groups = entries.GroupBy(e => e.Method.DeclaringType.Assembly.GetName().Name).OrderBy(g => g.Key);
            foreach (var group in groups)
            {
                var categoryLabel = new Label(group.Key);
                categoryLabel.AddToClassList("command-category");
                listView.Add(categoryLabel);

                foreach (var entry in group.OrderBy(e => e.Name))
                {
                    var desc = entry.Description ?? "";
                    var itemLabel = new Label($"{entry.Name}  —  {desc}");
                    itemLabel.AddToClassList("command-item");
                    listView.Add(itemLabel);
                }
            }
        }

        private void UpdateRefreshButtonVisibility()
        {
            var refreshButton = rootVisualElement.Q<Button>("refresh-commands");
            if (refreshButton != null)
            {
                refreshButton.style.display = _autoScan.value ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void ClearCache()
        {
            if (EditorUtility.DisplayDialog(
                AIBridgeEditorText.T("Clear Cache", "清除缓存"),
                AIBridgeEditorText.T("Are you sure you want to clear the screenshot cache?", "确定要清除截图缓存吗？"),
                AIBridgeEditorText.T("Yes", "是"),
                AIBridgeEditorText.T("No", "否")))
            {
                ScreenshotCacheManager.CleanupOldScreenshots();
                Debug.Log("[AIBridge] Screenshot cache cleared.");
                EditorUtility.DisplayDialog(
                    AIBridgeEditorText.T("Success", "成功"),
                    AIBridgeEditorText.T("Screenshot cache cleared.", "截图缓存已清除。"),
                    AIBridgeEditorText.T("OK", "确定"));
            }
        }

        private void ResetSettings()
        {
            if (EditorUtility.DisplayDialog(
                AIBridgeEditorText.T("Reset Settings", "重置设置"),
                AIBridgeEditorText.T("Are you sure you want to reset all settings to default?", "确定要将所有设置重置为默认值吗？"),
                AIBridgeEditorText.T("Yes", "是"),
                AIBridgeEditorText.T("No", "否")))
            {
                EditorPrefs.DeleteKey(CommandRegistry.PrefKeyAutoScan);
                EditorPrefs.DeleteKey(CommandRegistry.PrefKeyScanAssemblies);
                EditorPrefs.DeleteKey(PrefKeyAgentCodex);
                EditorPrefs.DeleteKey(PrefKeyAgentClaude);
                EditorPrefs.DeleteKey(PrefKeyAgentKiro);
                
                LoadSettings();
                Debug.Log("[AIBridge] Settings reset to default.");
                EditorUtility.DisplayDialog(
                    AIBridgeEditorText.T("Success", "成功"),
                    AIBridgeEditorText.T("Settings reset to default.", "设置已重置为默认值。"),
                    AIBridgeEditorText.T("OK", "确定"));
            }
        }
    }
}

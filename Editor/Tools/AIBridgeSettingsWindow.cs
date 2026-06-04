using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public class AIBridgeSettingsWindow : EditorWindow
    {
        private enum Tab
        {
            General,
            Gif,
            Commands,
            Tools
        }

        private const string PrefKeyAgentCodex = "AIBridge.SkillAgent.Codex";
        private const string PrefKeyAgentClaude = "AIBridge.SkillAgent.Claude";
        private const string PrefKeyAgentKiro = "AIBridge.SkillAgent.Kiro";

        private Tab _currentTab;
        private Vector2 _scrollPosition;
        private bool _agentCodex;
        private bool _agentClaude;
        private bool _agentKiro;

        [MenuItem("Window/AIBridge")]
        private static void OpenWindow()
        {
            var window = GetWindow<AIBridgeSettingsWindow>();
            window.titleContent = new GUIContent(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeSettingsTitle));
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _agentCodex = EditorPrefs.GetBool(PrefKeyAgentCodex, true);
            _agentClaude = EditorPrefs.GetBool(PrefKeyAgentClaude, true);
            _agentKiro = EditorPrefs.GetBool(PrefKeyAgentKiro, false);
            UpdateTitle();
        }

        private void OnGUI()
        {
            UpdateTitle();
            DrawHeader();
            DrawTabs();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space(8);

            switch (_currentTab)
            {
                case Tab.General:
                    DrawGeneralTab();
                    break;
                case Tab.Gif:
                    DrawGifTab();
                    break;
                case Tab.Commands:
                    DrawCommandsTab();
                    break;
                case Tab.Tools:
                    DrawToolsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeSettingsTitle), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeSettingsSubtitle), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            DrawLanguagePopup(150f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
        }

        private void DrawTabs()
        {
            var labels = new[]
            {
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.General),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.GifRecorder),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Commands),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Tools)
            };

            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, labels);
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

        private void DrawGeneralTab()
        {
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.QuickSkillInstall), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.QuickSkillHelp), MessageType.None);
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.GenerateAndInstallSkill), GUILayout.Height(28)))
            {
                OneClickInstallSkill();
            }

            EditorGUI.BeginChangeCheck();
            _agentCodex = EditorGUILayout.ToggleLeft(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AgentCodex), _agentCodex);
            _agentClaude = EditorGUILayout.ToggleLeft(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AgentClaude), _agentClaude);
            _agentKiro = EditorGUILayout.ToggleLeft(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AgentKiro), _agentKiro);
            if (EditorGUI.EndChangeCheck())
            {
                SaveAgentPreferences();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.BridgeSettings), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            AIBridge.Enabled = EditorGUILayout.Toggle(AIBridgeEditorText.Get(AIBridgeEditorTextKey.EnableAIBridge), AIBridge.Enabled);
            AIBridgeLogger.DebugEnabled = EditorGUILayout.Toggle(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DebugLogging), AIBridgeLogger.DebugEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.DirectoryInformation), EditorStyles.boldLabel);
            DrawPathLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CommandQueue), AIBridge.BridgeDirectory, AIBridge.BridgeDirectory);
            DrawPathLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Screenshots), ScreenshotHelper.ScreenshotsDir, ScreenshotHelper.ScreenshotsDir);
            DrawPathLine(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CliPath), AIBridge.BridgeCLI, Path.GetDirectoryName(AIBridge.BridgeCLI));
        }

        private static void DrawPathLine(string label, string value, string revealPath)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(110));
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Open), GUILayout.Width(58)))
            {
                EditorUtility.RevealInFinder(revealPath);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawGifTab()
        {
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Shortcuts), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ShortcutsHelp), MessageType.None);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.GifRecordingSettings), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.GifRecordingHelp), MessageType.None);

            EditorGUI.BeginChangeCheck();
            GifRecorderSettings.DefaultDuration = EditorGUILayout.Slider(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.DurationSeconds),
                GifRecorderSettings.DefaultDuration,
                0.5f,
                10f);
            GifRecorderSettings.DefaultFps = EditorGUILayout.IntSlider(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Fps),
                GifRecorderSettings.DefaultFps,
                5,
                60);
            GifRecorderSettings.DefaultScale = EditorGUILayout.Slider(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Scale),
                GifRecorderSettings.DefaultScale,
                0.1f,
                1f);
            GifRecorderSettings.DefaultColorCount = EditorGUILayout.IntSlider(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.ColorCount),
                GifRecorderSettings.DefaultColorCount,
                16,
                256);
            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
            }
        }

        private static void DrawCommandsTab()
        {
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CommandRegistration), EditorStyles.boldLabel);

            var autoScan = CommandRegistry.IsEditablePackage
                ? EditorPrefs.GetBool(CommandRegistry.PrefKeyAutoScan, false)
                : true;
            using (new EditorGUI.DisabledScope(!CommandRegistry.IsEditablePackage))
            {
                var nextAutoScan = EditorGUILayout.Toggle(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AutoScanAssemblies), autoScan);
                if (nextAutoScan != autoScan)
                {
                    EditorPrefs.SetBool(CommandRegistry.PrefKeyAutoScan, nextAutoScan);
                    autoScan = nextAutoScan;
                }
            }

            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AutoScanAssembliesHelp), MessageType.None);

            var assemblies = EditorPrefs.GetString(
                CommandRegistry.PrefKeyScanAssemblies,
                "Assembly-CSharp-Editor-firstpass;Assembly-CSharp");
            var nextAssemblies = EditorGUILayout.DelayedTextField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ScanAssemblies), assemblies);
            if (nextAssemblies != assemblies)
            {
                EditorPrefs.SetString(CommandRegistry.PrefKeyScanAssemblies, nextAssemblies);
            }

            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ScanAssembliesHelp), MessageType.None);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RegisteredCommands), EditorStyles.boldLabel);
            var entries = CommandRegistry.GetAll().ToList();
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.TotalRegisteredCommands, entries.Count), EditorStyles.miniLabel);

            DrawCommandList(entries);

            using (new EditorGUI.DisabledScope(autoScan))
            {
                if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.RefreshCommandList), GUILayout.Height(28)))
                {
                    CommandRegistry.Scan();
                    SkillInstaller.GenerateSkillFile();
                    SkillInstaller.OverrideSkill();
                }
            }
        }

        private static void DrawCommandList(List<CommandEntry> entries)
        {
            var groups = entries.GroupBy(e => e.Method.DeclaringType.Assembly.GetName().Name).OrderBy(g => g.Key);
            foreach (var group in groups)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);
                foreach (var entry in group.OrderBy(e => e.Name))
                {
                    var desc = entry.Description ?? string.Empty;
                    EditorGUILayout.LabelField(entry.Name + " - " + desc, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private static void DrawToolsTab()
        {
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.SkillDocumentation), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(AIBridgeEditorText.Get(AIBridgeEditorTextKey.SkillDocHelp), MessageType.None);
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.GenerateSkill), GUILayout.Height(28)))
            {
                SkillInstaller.GenerateSkillFile();
                SkillInstaller.OverrideSkill();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.SkillInstallation), EditorStyles.boldLabel);
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.CopyAgent), GUILayout.Height(28)))
            {
                SkillInstaller.CopyToAgent();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(AIBridgeEditorText.Get(AIBridgeEditorTextKey.Maintenance), EditorStyles.boldLabel);
            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ClearScreenshotCache), GUILayout.Height(28)))
            {
                ClearCache();
            }

            if (GUILayout.Button(AIBridgeEditorText.Get(AIBridgeEditorTextKey.ResetAllSettings), GUILayout.Height(28)))
            {
                ResetSettings();
            }
        }

        private void OneClickInstallSkill()
        {
            var targets = new List<string>();
            if (_agentCodex) targets.Add(".agents");
            if (_agentClaude) targets.Add(".claude");
            if (_agentKiro) targets.Add(".kiro");

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.Warning),
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.AgentRequiredMessage),
                    AIBridgeEditorText.Get(AIBridgeEditorTextKey.Ok));
                return;
            }

            SkillInstaller.GenerateSkillFile();
            SkillInstaller.CopyToAgent(targets.ToArray());

            EditorUtility.DisplayDialog(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Success),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.SkillGeneratedInstalled, string.Join(", ", targets.ToArray())),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Ok));
        }

        private void SaveAgentPreferences()
        {
            EditorPrefs.SetBool(PrefKeyAgentCodex, _agentCodex);
            EditorPrefs.SetBool(PrefKeyAgentClaude, _agentClaude);
            EditorPrefs.SetBool(PrefKeyAgentKiro, _agentKiro);
        }

        private static void SaveSettings()
        {
            if (EditorPrefs.GetBool(CommandRegistry.PrefKeyAutoScan, false))
            {
                CommandRegistry.Scan();
            }
        }

        private static void ClearCache()
        {
            if (!EditorUtility.DisplayDialog(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.ClearCache),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.ClearCacheConfirm),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Yes),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.No)))
            {
                return;
            }

            ScreenshotCacheManager.CleanupOldScreenshots();
            Debug.Log("[AIBridge] Screenshot cache cleared.");
            EditorUtility.DisplayDialog(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Success),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.ScreenshotCacheCleared),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Ok));
        }

        private static void ResetSettings()
        {
            if (!EditorUtility.DisplayDialog(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.ResetSettings),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.ResetSettingsConfirm),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Yes),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.No)))
            {
                return;
            }

            EditorPrefs.DeleteKey(CommandRegistry.PrefKeyAutoScan);
            EditorPrefs.DeleteKey(CommandRegistry.PrefKeyScanAssemblies);
            EditorPrefs.DeleteKey(PrefKeyAgentCodex);
            EditorPrefs.DeleteKey(PrefKeyAgentClaude);
            EditorPrefs.DeleteKey(PrefKeyAgentKiro);

            Debug.Log("[AIBridge] Settings reset to default.");
            EditorUtility.DisplayDialog(
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Success),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.SettingsReset),
                AIBridgeEditorText.Get(AIBridgeEditorTextKey.Ok));
        }

        private void UpdateTitle()
        {
            titleContent = new GUIContent(AIBridgeEditorText.Get(AIBridgeEditorTextKey.AIBridgeSettingsTitle));
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    [InitializeOnLoad]
    public static class CommandRegistry
    {
        public const string PrefKeyAutoScan = "AIBridge.AutoScan";
        public const string PrefKeyScanAssemblies = "AIBridge.ScanAssemblies";

        private static readonly Dictionary<string, CommandEntry> _registry = new Dictionary<string, CommandEntry>();
        private static readonly string _thisFilePath = CallerFilePath();

        public static bool IsEditablePackage
        {
            get
            {
                if (string.IsNullOrEmpty(_thisFilePath)) return false;
                var projectRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, ".."));
                var packagesDir = System.IO.Path.Combine(projectRoot, "Packages");
                return System.IO.Path.GetFullPath(_thisFilePath)
                    .StartsWith(packagesDir, StringComparison.OrdinalIgnoreCase);
            }
        }

        static CommandRegistry()
        {
            var autoScan = EditorPrefs.GetBool(PrefKeyAutoScan, false);
            if (autoScan)
                Scan();
            else
                AutoRegister();
        }


        public static void Scan()
        {
            _registry.Clear();
            var found = new List<(string assemblyName, string typeName, string methodName)>();

            var ownAssembly = typeof(CommandRegistry).Assembly.GetName().Name;
            var extra = GetExtraScanAssemblies();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (name != ownAssembly && !extra.Contains(name))
                    continue;

                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var attr = method.GetCustomAttribute<AIBridgeAttribute>();
                        if (attr == null) continue;
                        if (method.ReturnType != typeof(IEnumerator))
                        {
                            AIBridgeLogger.LogWarning($"[CommandRegistry] {type.Name}.{method.Name} has [AIBridge] but does not return IEnumerator, skipped.");
                            continue;
                        }

                        var commandName = attr.Name ?? $"{type.Name}_{method.Name}";
                        _registry[commandName] = new CommandEntry
                        {
                            Name = commandName,
                            Description = attr.Description,
                            Example = attr.Example,
                            Method = method,
                            Parameters = method.GetParameters(),
                            Attribute = attr,
                        };
                        found.Add((name, type.FullName, method.Name));
                    }
                }
            }

            AIBridgeLogger.LogInfo($"[CommandRegistry] Scanned {found.Count} commands.");

            var autoScan = EditorPrefs.GetBool(PrefKeyAutoScan, false);
            if (!autoScan && IsEditablePackage)
                RegenerateAutoRegister(found);
        }

        private static HashSet<string> GetExtraScanAssemblies()
        {
            var raw = EditorPrefs.GetString(PrefKeyScanAssemblies, "");
            return new HashSet<string>(
                raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void RegenerateAutoRegister(List<(string assemblyName, string typeName, string methodName)> entries)
        {
            if (!File.Exists(_thisFilePath)) return;

            var source = File.ReadAllText(_thisFilePath);

            var newBody = BuildAutoRegisterBody(entries);
            source = Regex.Replace(source, @"(private static void AutoRegister\(\))\s*\{[\s\S]*?\n\s*\}\s*$",
                match => match.Groups[1].Value + "\n        {" + newBody + "        }",
                RegexOptions.Multiline);
            File.WriteAllText(_thisFilePath, source);

            AIBridgeLogger.LogInfo($"[CommandRegistry] Registered {_registry.Count} commands.");
        }

        private static string BuildAutoRegisterBody(List<(string assemblyName, string typeName, string methodName)> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;");
            sb.AppendLine("            var loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);");

            string lastType = null;
            string lastAssembly = null;
            foreach (var (assemblyName, typeName, methodName) in entries.OrderBy(e => e.assemblyName).ThenBy(e => e.typeName).ThenBy(e => e.methodName))
            {
                if (assemblyName != lastAssembly)
                {
                    sb.AppendLine();
                    sb.AppendLine($"            // {assemblyName}");
                    sb.AppendLine($"            if (!loadedAssemblies.ContainsKey(\"{assemblyName}\"))");
                    sb.AppendLine($"                loadedAssemblies[\"{assemblyName}\"] = Assembly.Load(\"{assemblyName}\");");
                    lastAssembly = assemblyName;
                    lastType = null;
                }

                var typeFieldName = typeName.Split('.').Last();
                if (typeName != lastType)
                {
                    sb.AppendLine($"            var {ToCamelCase(typeFieldName)}Type = loadedAssemblies[\"{assemblyName}\"].GetType(\"{typeName}\");");
                    lastType = typeName;
                }
                sb.AppendLine($"            RegisterCommand({ToCamelCase(typeFieldName)}Type.GetMethod(\"{methodName}\", flags));");
            }

            sb.AppendLine();
            sb.AppendLine("            AIBridgeLogger.LogInfo($\"[CommandRegistry] Registered {_registry.Count} commands.\");");
            sb.Append("        ");
            return sb.ToString();
        }

        private static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        private static void RegisterCommand(MethodInfo method)
        {
            if (method == null) return;
            var type = method.DeclaringType;
            var attr = method.GetCustomAttribute<AIBridgeAttribute>();
            if (attr == null || type == null) return;

            var commandName = attr.Name ?? $"{type.Name}_{method.Name}";
            _registry[commandName] = new CommandEntry
            {
                Name = commandName,
                Description = attr.Description,
                Example = attr.Example,
                Method = method,
                Parameters = method.GetParameters(),
                Attribute = attr,
            };
        }

        public static bool TryGetCommand(string name, out CommandEntry entry)
            => _registry.TryGetValue(name, out entry);

        public static IEnumerable<CommandEntry> GetAll() => _registry.Values;

        private static string CallerFilePath([CallerFilePath] string path = "") => path;
        
        
        // -----AUTO generate
        private static void AutoRegister()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

            // cn.su.aibridge.Editor
            if (!loadedAssemblies.ContainsKey("cn.su.aibridge.Editor"))
                loadedAssemblies["cn.su.aibridge.Editor"] = Assembly.Load("cn.su.aibridge.Editor");
            var assetDatabaseCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.AssetDatabaseCommand");
            RegisterCommand(assetDatabaseCommandType.GetMethod("Find", flags));
            RegisterCommand(assetDatabaseCommandType.GetMethod("Refresh", flags));
            var batchCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.BatchCommand");
            RegisterCommand(batchCommandType.GetMethod("Execute", flags));
            var compileCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.CompileCommand");
            RegisterCommand(compileCommandType.GetMethod("Start", flags));
            RegisterCommand(compileCommandType.GetMethod("Status", flags));
            var editorCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.EditorCommand");
            RegisterCommand(editorCommandType.GetMethod("GetState", flags));
            RegisterCommand(editorCommandType.GetMethod("Log", flags));
            RegisterCommand(editorCommandType.GetMethod("Pause", flags));
            RegisterCommand(editorCommandType.GetMethod("Play", flags));
            RegisterCommand(editorCommandType.GetMethod("Stop", flags));
            var gameObjectCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.GameObjectCommand");
            RegisterCommand(gameObjectCommandType.GetMethod("Create", flags));
            RegisterCommand(gameObjectCommandType.GetMethod("Destroy", flags));
            RegisterCommand(gameObjectCommandType.GetMethod("Find", flags));
            RegisterCommand(gameObjectCommandType.GetMethod("GetInfo", flags));
            RegisterCommand(gameObjectCommandType.GetMethod("SetActive", flags));
            var getLogsCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.GetLogsCommand");
            RegisterCommand(getLogsCommandType.GetMethod("Log", flags));
            RegisterCommand(getLogsCommandType.GetMethod("StartCapture", flags));
            RegisterCommand(getLogsCommandType.GetMethod("StopCapture", flags));
            var helpCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.HelpCommand");
            RegisterCommand(helpCommandType.GetMethod("Help", flags));
            var inputSimulationCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.InputSimulationCommand");
            RegisterCommand(inputSimulationCommandType.GetMethod("Click", flags));
            RegisterCommand(inputSimulationCommandType.GetMethod("ClickAt", flags));
            RegisterCommand(inputSimulationCommandType.GetMethod("ClickByInstanceId", flags));
            RegisterCommand(inputSimulationCommandType.GetMethod("Drag", flags));
            RegisterCommand(inputSimulationCommandType.GetMethod("DragByInstanceId", flags));
            RegisterCommand(inputSimulationCommandType.GetMethod("LongPress", flags));
            RegisterCommand(inputSimulationCommandType.GetMethod("LongPressByInstanceId", flags));
            var inspectorCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.InspectorCommand");
            RegisterCommand(inspectorCommandType.GetMethod("AddComponent", flags));
            RegisterCommand(inspectorCommandType.GetMethod("GetComponents", flags));
            RegisterCommand(inspectorCommandType.GetMethod("GetProperties", flags));
            RegisterCommand(inspectorCommandType.GetMethod("RemoveComponent", flags));
            RegisterCommand(inspectorCommandType.GetMethod("SetProperty", flags));
            var menuItemCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.MenuItemCommand");
            RegisterCommand(menuItemCommandType.GetMethod("Execute", flags));
            var prefabCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.PrefabCommand");
            RegisterCommand(prefabCommandType.GetMethod("Apply", flags));
            RegisterCommand(prefabCommandType.GetMethod("GetInfo", flags));
            RegisterCommand(prefabCommandType.GetMethod("Instantiate", flags));
            RegisterCommand(prefabCommandType.GetMethod("Save", flags));
            RegisterCommand(prefabCommandType.GetMethod("Unpack", flags));
            var sceneCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.SceneCommand");
            RegisterCommand(sceneCommandType.GetMethod("GetActive", flags));
            RegisterCommand(sceneCommandType.GetMethod("GetHierarchy", flags));
            RegisterCommand(sceneCommandType.GetMethod("Load", flags));
            var screenshotCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.ScreenshotCommand");
            RegisterCommand(screenshotCommandType.GetMethod("Gif", flags));
            RegisterCommand(screenshotCommandType.GetMethod("Image", flags));
            var selectionCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.SelectionCommand");
            RegisterCommand(selectionCommandType.GetMethod("Clear", flags));
            RegisterCommand(selectionCommandType.GetMethod("Get", flags));
            RegisterCommand(selectionCommandType.GetMethod("Set", flags));
            var transformCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("AIBridge.Editor.TransformCommand");
            RegisterCommand(transformCommandType.GetMethod("Get", flags));
            RegisterCommand(transformCommandType.GetMethod("LookAt", flags));
            RegisterCommand(transformCommandType.GetMethod("Reset", flags));
            RegisterCommand(transformCommandType.GetMethod("SetParent", flags));
            RegisterCommand(transformCommandType.GetMethod("SetPosition", flags));
            RegisterCommand(transformCommandType.GetMethod("SetRotation", flags));
            RegisterCommand(transformCommandType.GetMethod("SetScale", flags));
            var codeExecuteCommandType = loadedAssemblies["cn.su.aibridge.Editor"].GetType("CodeExecuteCommand");
            RegisterCommand(codeExecuteCommandType.GetMethod("Execute", flags));

            AIBridgeLogger.LogInfo($"[CommandRegistry] Registered {_registry.Count} commands.");
                }
        // -----AUTO generate
    }
}

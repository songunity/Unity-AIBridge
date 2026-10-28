using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;

var sb = new StringBuilder();
var asms = AppDomain.CurrentDomain.GetAssemblies();

Type FindType(string name)
{
    foreach (var asm in asms)
    {
        var t = asm.GetType(name);
        if (t != null) return t;
    }
    return null;
}

MethodInfo GetMethod(Type type, string name, Type[] paramTypes = null)
{
    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    if (paramTypes != null) return type.GetMethod(name, flags, null, paramTypes, null);
    return type.GetMethod(name, flags);
}

var utilType = FindType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility")
            ?? FindType("UnityEditorInternal.FrameDebuggerUtility");
var windowType = FindType("UnityEditor.FrameDebuggerWindow");

if (utilType == null || windowType == null)
{
    return DiagnosticResult("blocked", "找不到 FrameDebugger 内部类型");
}

sb.AppendLine("=== FrameDebugger 类型检查 ===");
sb.AppendLine($"UtilType: {utilType.FullName}");
sb.AppendLine($"WindowType: {windowType.FullName}");

// 状态检查只读取接口，不打开窗口、不启用调试或改变事件上限。
var stateType = FindType("UnityEngine.FrameDebugger");
var enabledProperty = stateType?.GetProperty("enabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
if (enabledProperty == null) return DiagnosticResult("blocked", "无法读取 Frame Debugger 启用状态。");
bool enabled = (bool)enabledProperty.GetValue(null);
sb.AppendLine($"FrameDebugger enabled: {enabled}");
sb.AppendLine($"PlayMode: {EditorApplication.isPlaying}, paused: {EditorApplication.isPaused}");

// 获取事件数量
var countProp = utilType.GetProperty("count", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
if (enabled && countProp != null)
{
    int count = (int)countProp.GetValue(null);
    sb.AppendLine($"Event count: {count}");
}

// 获取事件列表
var getEventsMethod = GetMethod(utilType, "GetFrameEvents");
if (enabled && getEventsMethod != null)
{
    var events = getEventsMethod.Invoke(null, null) as Array;
    sb.AppendLine($"FrameEvents array length: {events?.Length ?? 0}");
}

// 列出可用方法
sb.AppendLine("\n=== FrameDebuggerUtility Methods ===");
foreach (var m in utilType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
{
    var ps = m.GetParameters();
    var paramStr = string.Join(", ", Array.ConvertAll(ps, p => $"{p.ParameterType.Name} {p.Name}"));
    sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({paramStr})");
}

sb.AppendLine("\n=== FrameDebuggerWindow Key Methods ===");
foreach (var m in windowType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
{
    if (m.Name.Contains("Change") || m.Name.Contains("Enable") || m.Name.Contains("Repaint") || m.Name.Contains("Limit"))
    {
        var ps = m.GetParameters();
        var paramStr = string.Join(", ", Array.ConvertAll(ps, p => $"{p.ParameterType.Name} {p.Name}"));
        sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({paramStr})");
    }
}

return sb.ToString();

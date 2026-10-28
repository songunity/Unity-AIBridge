using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

var asms = AppDomain.CurrentDomain.GetAssemblies();
var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

Type FindType(string name)
{
    foreach (var asm in asms) { var t = asm.GetType(name); if (t != null) return t; }
    return null;
}
MethodInfo GetMethod(Type type, string name, Type[] paramTypes = null)
{
    if (paramTypes != null) return type.GetMethod(name, flags, null, paramTypes, null);
    return type.GetMethod(name, flags);
}
object GetField(object obj, string name)
{
    if (obj == null) return null;
    var f = obj.GetType().GetField(name, flags);
    return f?.GetValue(obj);
}
object GetFieldAny(object obj, params string[] names)
{
    if (obj == null) throw new ArgumentNullException(nameof(obj));
    foreach (var n in names)
    {
        var field = obj.GetType().GetField(n, flags);
        if (field != null) return field.GetValue(obj);
    }
    throw new MissingFieldException(obj.GetType().FullName, string.Join(" / ", names));
}

var utilType = FindType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility");
var eventDataType = FindType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData");
var windowType = FindType("UnityEditor.FrameDebuggerWindow");

var stateType = FindType("UnityEngine.FrameDebugger");
if (utilType == null || eventDataType == null || windowType == null || stateType == null)
    return DiagnosticResult("blocked", "当前 Unity 缺少所需的 Frame Debugger 类型。");
if (EditorApplication.isPlaying && !EditorApplication.isPaused)
    return DiagnosticResult("blocked", "请先暂停 Play Mode，再抓取稳定的渲染帧。");

var getEvents = GetMethod(utilType, "GetFrameEvents");
var getData = GetMethod(utilType, "GetFrameEventData");
var getBreakStrings = GetMethod(utilType, "GetBatchBreakCauseStrings");
var changeLimitMethod = GetMethod(windowType, "ChangeFrameEventLimit", new[] { typeof(int) });
var repaintMethod = GetMethod(windowType, "RepaintOnLimitChange");
var setEnabled = GetMethod(utilType, "SetEnabled", new[] { typeof(bool), typeof(int) });
var getRemotePlayer = GetMethod(utilType, "GetRemotePlayerGUID");
var enabledProperty = stateType.GetProperty("enabled", flags);
var limitProperty = utilType.GetProperty("limit", flags);
var openMethod = GetMethod(windowType, "OpenWindow");
var enableMethod = GetMethod(windowType, "EnableFrameDebugger");
var eventIndexField = eventDataType.GetField("m_FrameEventIndex", flags);
var receivingProperty = utilType.GetProperty("receivingRemoteFrameEventData", flags);
if (getEvents == null || getData == null || getBreakStrings == null || changeLimitMethod == null
    || repaintMethod == null || setEnabled == null || getRemotePlayer == null
    || enabledProperty == null || limitProperty == null || openMethod == null || enableMethod == null || eventIndexField == null)
    return DiagnosticResult("blocked", "当前 Unity 的 Frame Debugger API 不兼容，先运行 check_frame_debugger.cs 查看接口。");

bool wasEnabled = (bool)enabledProperty.GetValue(null);
int originalLimit = (int)limitProperty.GetValue(null);
int originalPlayer = (int)getRemotePlayer.Invoke(null, null);
EditorWindow window = null;
try
{
    window = openMethod.Invoke(null, null) as EditorWindow;
    if (window == null) throw new InvalidOperationException("Frame Debugger 窗口未创建。");
    if (!wasEnabled) enableMethod.Invoke(window, null);
    await Task.Delay(100);
    context["frameDebuggerTarget"] = getRemotePlayer.Invoke(null, null);
    context["frameDebuggerEnabledBefore"] = wasEnabled;

    var frameEvents = (Array)getEvents.Invoke(null, null);
    var breakReasons = (string[])getBreakStrings.Invoke(null, null);
    if (frameEvents == null || frameEvents.Length == 0) return DiagnosticResult("blocked", "没有可抓取的渲染事件。");
    int count = frameEvents.Length;

    Debug.Log($"[FrameDebuggerCapture] 开始抓取 {count} 个事件...");

    var capturedEvents = new List<object>();
    var deadline = System.Diagnostics.Stopwatch.StartNew();
    int budgetMs = Limit("budgetMs", 120000, 600000);
    int maxEvents = Limit("maxEvents", 3000, 20000);
    int captured = 0;
    var skipped = new List<int>();
    for (int i = 0; i < count; i++)
    {
        // 步进到当前事件
        changeLimitMethod.Invoke(window, new object[] { i + 1 });
        repaintMethod.Invoke(window, null);
        if (i >= maxEvents || deadline.ElapsedMilliseconds >= budgetMs) { skipped.Add(i); continue; }
        var evtDataObj = Activator.CreateInstance(eventDataType);
        var args = new object[] { i, evtDataObj };
        bool ok = false;
        for (int attempt = 0; attempt < 20 && deadline.ElapsedMilliseconds < budgetMs; attempt++) {
            await Task.Delay(10);
            if (receivingProperty != null && (bool)receivingProperty.GetValue(null)) continue;
            eventIndexField.SetValue(evtDataObj, -1);
            ok = (bool)getData.Invoke(null, args);
            if (ok) break;
            repaintMethod.Invoke(window, null);
        }
        if (!ok) { skipped.Add(i); continue; }

        evtDataObj = args[1];

        // 提取事件类型
        var evt = frameEvents.GetValue(i);
        string eventType = GetFieldAny(evt, "m_Type", "type")?.ToString() ?? "Unknown";

        // 提取事件数据
        int vertexCount = Convert.ToInt32(GetFieldAny(evtDataObj, "m_VertexCount", "vertexCount") ?? 0);
        int indexCount = Convert.ToInt32(GetFieldAny(evtDataObj, "m_IndexCount", "indexCount") ?? 0);
        int instanceCount = Convert.ToInt32(GetFieldAny(evtDataObj, "m_InstanceCount", "instanceCount") ?? 0);
        int drawCallCount = Convert.ToInt32(GetFieldAny(evtDataObj, "m_DrawCallCount", "drawCallCount") ?? 0);
        string shaderName = GetFieldAny(evtDataObj, "m_RealShaderName", "shaderName")?.ToString() ?? "";
        string passName = GetFieldAny(evtDataObj, "m_PassName", "passName")?.ToString() ?? "";
        string passLightMode = GetFieldAny(evtDataObj, "m_PassLightMode", "passLightMode")?.ToString() ?? "";
        int subShaderIndex = Convert.ToInt32(GetFieldAny(evtDataObj, "m_SubShaderIndex", "subShaderIndex") ?? 0);
        int shaderPassIndex = Convert.ToInt32(GetFieldAny(evtDataObj, "m_ShaderPassIndex", "shaderPassIndex") ?? 0);
        string rtName = GetFieldAny(evtDataObj, "m_RenderTargetName", "rtName")?.ToString() ?? "";
        int rtWidth = Convert.ToInt32(GetFieldAny(evtDataObj, "m_RenderTargetWidth", "rtWidth") ?? 0);
        int rtHeight = Convert.ToInt32(GetFieldAny(evtDataObj, "m_RenderTargetHeight", "rtHeight") ?? 0);
        int batchBreakCause = Convert.ToInt32(GetFieldAny(evtDataObj, "m_BatchBreakCause", "batchBreakCause") ?? 0);
        string batchBreakStr = (breakReasons != null && batchBreakCause >= 0 && batchBreakCause < breakReasons.Length)
            ? breakReasons[batchBreakCause] : "";
        int meshInstanceID = Convert.ToInt32(GetFieldAny(evtDataObj, "m_MeshInstanceID", "meshInstanceID") ?? 0);
        int meshSubset = Convert.ToInt32(GetFieldAny(evtDataObj, "m_MeshSubset", "meshSubset") ?? 0);

        // Shader Keywords
        string keywords = "";
        var shaderInfo = GetFieldAny(evtDataObj, "m_ShaderInfo", "shaderProperties");
        if (shaderInfo != null)
        {
            var kwArray = GetField(shaderInfo, "m_Keywords") as Array;
            if (kwArray != null && kwArray.Length > 0)
            {
                var kwSb = new StringBuilder();
                foreach (var kw in kwArray)
                {
                    var kwName = GetField(kw, "m_Name");
                    if (kwName != null) { if (kwSb.Length > 0) kwSb.Append(' '); kwSb.Append(kwName); }
                }
                keywords = kwSb.ToString();
            }
        }

        // GameObject 名称
        string goName = "";
        var objRef = GetFieldAny(evt, "m_Obj", "gameObject");
        if (objRef is UnityEngine.Object uObj && uObj != null)
        {
            if (uObj is GameObject go) goName = go.name;
            else goName = uObj.name;
        }

        var row = new Dictionary<string, object> { ["idx"] = i, ["type"] = eventType, ["go"] = goName,
            ["shader"] = shaderName, ["pass"] = passName, ["lightMode"] = passLightMode, ["keywords"] = keywords,
            ["subShader"] = subShaderIndex, ["passIdx"] = shaderPassIndex, ["verts"] = vertexCount, ["indices"] = indexCount,
            ["instances"] = instanceCount, ["drawCalls"] = drawCallCount, ["rt"] = rtName, ["rtW"] = rtWidth, ["rtH"] = rtHeight,
            ["batchBreak"] = batchBreakStr, ["meshID"] = meshInstanceID, ["meshSub"] = meshSubset };
        foreach (var field in evtDataObj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            if (field.Name.Contains("State") || field.Name.StartsWith("m_RenderTarget") || field.Name.StartsWith("m_Compute")
                || field.Name == "m_ShaderInfo" || field.Name == "m_ComponentInstanceID" || field.Name == "m_ShaderInstanceID")
                row[field.Name] = ProjectField(field.GetValue(evtDataObj), 0);
        }
        capturedEvents.Add(row);
        captured++;
    }

    SaveArtifact("frame_debugger", new { context, eventCount = count, capturedCount = captured,
        skippedEventIndices = skipped, events = capturedEvents });
    return DiagnosticResult(skipped.Count == 0 && warnings.Count == 0 ? "completed" : "partial", new { eventCount = count, capturedCount = captured, skipped = skipped.Count });

}
finally
{
    setEnabled.Invoke(null, new object[] { wasEnabled, originalPlayer });
    if (wasEnabled && window != null)
        changeLimitMethod.Invoke(window, new object[] { originalLimit });
}

object ProjectField(object value, int depth) {
    if (value == null) return null;
    if (value is UnityEngine.Object obj) return new { instanceId = obj.GetInstanceID(), name = obj.name, type = obj.GetType().FullName };
    var type = value.GetType();
    if (type.IsEnum) return value.ToString();
    if (type.IsPrimitive || value is string) return value;
    if (depth >= 5) return "<depth limit>";
    if (value is System.Collections.IEnumerable sequence) {
        var list = new List<object>();
        foreach (var item in sequence) { if (list.Count >= 128) { warnings.Add("Shader 参数数组超过 128 项，已截断"); break; } list.Add(ProjectField(item, depth + 1)); }
        return list;
    }
    var fields = new Dictionary<string, object>();
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) fields[field.Name] = ProjectField(field.GetValue(value), depth + 1);
    return fields;
}

using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling.Memory;

Assembly analysisAssembly;
try { analysisAssembly = Assembly.Load("Unity.MemoryProfiler.Editor"); }
catch (System.IO.FileNotFoundException)
{
    return DiagnosticResult("blocked", "未找到 Unity.MemoryProfiler.Editor，请先确认 Memory Profiler 包已安装并完成编译。");
}

if (analysisAssembly.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot") == null
    || analysisAssembly.GetType("Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader") == null)
    return DiagnosticResult("blocked", "Memory Profiler 包的内部 API 与脚本不兼容，请先核对包版本。");

var sb = new StringBuilder();

sb.AppendLine("=== 当前内存概况 ===");
sb.AppendLine($"Total Allocated: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):F1} MB");
sb.AppendLine($"Total Reserved:  {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024f * 1024f):F1} MB");
sb.AppendLine($"Mono Heap:       {UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / (1024f * 1024f):F1} MB");
sb.AppendLine($"Mono Used:       {UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024f * 1024f):F1} MB");
sb.AppendLine($"GFX Driver:      {UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f):F1} MB");

string folder = Path.Combine(Application.dataPath, "../MemorySnapshots");
if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

string basePath = Path.Combine(folder, $"snap_{DateTime.Now:yyyyMMdd_HHmmss_fff}.snap");
if (EditorApplication.isCompiling || EditorApplication.isUpdating) return DiagnosticResult("blocked", "编译或导入期间不采集快照");
if (options["targetId"] != null && (int)options["targetId"] != ProfilerDriver.connectedProfiler) return DiagnosticResult("blocked", "Profiler 连接目标与 targetId 不一致");
context["requestedSnapshot"] = basePath;
var completion = new TaskCompletionSource<(string path, bool success)>();
sb.AppendLine("正在拍摄快照...");
MemoryProfiler.TakeSnapshot(basePath, (path, success) => completion.TrySetResult((path, success)),
    CaptureFlags.ManagedObjects | CaptureFlags.NativeObjects | CaptureFlags.NativeAllocations | ((bool?)options["nativeStacks"] == true ? CaptureFlags.NativeAllocationSites | CaptureFlags.NativeStackTraces : (CaptureFlags)0));

if (await Task.WhenAny(completion.Task, Task.Delay(120000)) != completion.Task)
    return DiagnosticResult("unknown", "快照等待超时；引擎采集可能仍在继续，不要重放拍照操作");
var captured = await completion.Task;
string snapshotPath = captured.path;
artifacts.Add(Path.GetFullPath(snapshotPath));
context["captureFlags"] = (bool?)options["nativeStacks"] == true ? "ManagedObjects,NativeObjects,NativeAllocations,NativeAllocationSites,NativeStackTraces" : "ManagedObjects,NativeObjects,NativeAllocations";
SaveArtifact("snapshot_context", new { snapshotPath, context });
bool snapshotSuccess = captured.success;
if (!snapshotSuccess) return DiagnosticResult("failed", "快照采集失败");

sb.AppendLine($"快照已保存: {snapshotPath}");
sb.AppendLine($"文件大小: {new FileInfo(snapshotPath).Length / (1024.0 * 1024):F1} MB");

try
{
    var result = LoadSnapshot(snapshotPath);
    sb.AppendLine($"\n=== 快照分析 ===");
    sb.AppendLine($"Native 对象总数: {result.count}");
    sb.AppendLine($"Native 对象总大小: {FormatSize(result.totalSize)}");

    sb.AppendLine("\n--- Top 20 类型 (按总大小) ---");
    foreach (var kv in result.typeStats.OrderByDescending(x => x.Value.totalSize).Take(20))
        sb.AppendLine($"  {kv.Key,-35} x{kv.Value.count,-6} {FormatSize(kv.Value.totalSize)}");

    sb.AppendLine("\n--- Top 30 对象 (按大小) ---");
    foreach (var obj in result.objects.OrderByDescending(x => x.size).Take(30))
    {
        string name = string.IsNullOrEmpty(obj.name) ? "<No Name>" : (obj.name.Length > 45 ? obj.name.Substring(0, 45) + ".." : obj.name);
        sb.AppendLine($"  {name,-47} {obj.typeName,-22} {FormatSize(obj.size)}");
    }
}
catch (Exception) { throw; }

return sb.ToString();

// === Helpers ===

(long count, ulong totalSize, Dictionary<string, (long count, ulong totalSize)> typeStats, List<(string name, string typeName, ulong size)> objects) LoadSnapshot(string path)
{
    var editorAsm = Assembly.Load("Unity.MemoryProfiler.Editor");
    var readerType = editorAsm.GetType("Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader");
    var cachedType = editorAsm.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot");

    object reader = null;
    object snapshot = null;
    try
    {
        reader = Activator.CreateInstance(readerType);
        if (readerType.GetMethod("Open", new Type[] { typeof(string) }).Invoke(reader, new object[] { path }).ToString() != "Success") throw new IOException("无法打开快照: " + path);

        snapshot = Activator.CreateInstance(cachedType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { reader }, null);

        var nativeObjects = cachedType.GetField("NativeObjects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(snapshot);
        var nativeObjType = nativeObjects.GetType();
        long nativeCount = (long)nativeObjType.GetField("Count", BindingFlags.Instance | BindingFlags.Public).GetValue(nativeObjects);
        var objNames = (string[])nativeObjType.GetField("ObjectName", BindingFlags.Instance | BindingFlags.Public).GetValue(nativeObjects);

        var nativeTypes = cachedType.GetField("NativeTypes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(snapshot);
        var typeNames = (string[])nativeTypes.GetType().GetField("TypeName", BindingFlags.Instance | BindingFlags.Public).GetValue(nativeTypes);

        var sizes = MemoryValues(nativeObjType.GetField("Size", BindingFlags.Instance | BindingFlags.Public).GetValue(nativeObjects)).Select(Convert.ToUInt64).ToList();
        var typeIndices = MemoryValues(nativeObjType.GetField("NativeTypeArrayIndex", BindingFlags.Instance | BindingFlags.Public).GetValue(nativeObjects)).Select(Convert.ToInt32).ToList();

        var typeStats = new Dictionary<string, (long count, ulong totalSize)>();
        var objects = new List<(string name, string typeName, ulong size)>();
        ulong totalSize = 0;

        for (int i = 0; i < nativeCount && i < sizes.Count && i < typeIndices.Count; i++)
        {
            ulong size = sizes[i];
            int typeIdx = typeIndices[i];
            string typeName = typeIdx >= 0 && typeIdx < typeNames.Length ? typeNames[typeIdx] : "Unknown";
            string objName = i < objNames.Length ? objNames[i] : "<unknown>";

            if (!typeStats.ContainsKey(typeName)) typeStats[typeName] = (0, 0);
            var s = typeStats[typeName];
            typeStats[typeName] = (s.count + 1, s.totalSize + size);
            totalSize += size;
            objects.Add((objName, typeName, size));
        }


        return (nativeCount, totalSize, typeStats, objects);
    }
    finally
    {
        try { (snapshot as IDisposable)?.Dispose(); }
        finally { (reader as IDisposable)?.Dispose(); }
    }
}



string FormatSize(ulong bytes)
{
    if (bytes >= 1024UL * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    if (bytes >= 1024UL * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
    if (bytes >= 1024UL) return $"{bytes / 1024.0:F1} KB";
    return $"{bytes} B";
}

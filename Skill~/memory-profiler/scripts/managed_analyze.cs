using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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

string snapPath = (string)options["snapshot"];
if (string.IsNullOrWhiteSpace(snapPath) || !File.Exists(snapPath)) return DiagnosticResult("blocked", "请通过 snapshot 指定存在的快照文件");
snapPath = Path.GetFullPath(snapPath);
context["snapshot"] = snapPath;
sb.AppendLine($"快照: {Path.GetFileName(snapPath)}");
sb.AppendLine($"文件大小: {new FileInfo(snapPath).Length / (1024.0 * 1024):F1} MB");

var editorAsm = Assembly.Load("Unity.MemoryProfiler.Editor");
var cachedType = editorAsm.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot");
var readerType = editorAsm.GetType("Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader");
var crawlerType = editorAsm.GetType("Unity.MemoryProfiler.Editor.ManagedDataCrawler");
var esType = editorAsm.GetType("Unity.MemoryProfiler.Editor.EnumerationUtilities.EnumerationStatus");

object reader = null;
object snapshot = null;

try
{
    reader = Activator.CreateInstance(readerType);
    if (readerType.GetMethod("Open", new Type[] { typeof(string) }).Invoke(reader, new object[] { snapPath }).ToString() != "Success") throw new IOException("无法打开快照: " + snapPath);

    snapshot = Activator.CreateInstance(cachedType,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        null, new object[] { reader }, null);

    // Managed heap section summary
    var heapSections = cachedType.GetField("ManagedHeapSections", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(snapshot);
    var hsType = heapSections.GetType();
    var heapReserved = (ulong)hsType.GetField("ManagedHeapMemoryReserved", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(heapSections);
    var vmReserved = (ulong)hsType.GetField("VirtualMachineMemoryReserved", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(heapSections);

    sb.AppendLine("快照已加载，开始爬取托管堆...");



    var crawlMethod = crawlerType.GetMethod("Crawl", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    var enumerator = (IEnumerator)crawlMethod.Invoke(null, new object[] { snapshot });

    int steps = 0;
    while (enumerator.MoveNext()) { steps++; await Task.Yield(); }
    sb.AppendLine($"爬取完成，共 {steps} 步");

    var crawledProp = cachedType.GetProperty("CrawledData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    var crawledData = crawledProp.GetValue(snapshot);
    var mdType = crawledData.GetType();

    var memUsage = (ulong)mdType.GetProperty("ManagedObjectMemoryUsage").GetValue(crawledData);
    var activeUsage = (ulong)mdType.GetProperty("ActiveHeapMemoryUsage").GetValue(crawledData);
    var emptySpace = (ulong)mdType.GetProperty("ActiveHeapMemoryEmptySpace").GetValue(crawledData);

    sb.AppendLine($"\n=== 托管堆概况 ===");
    sb.AppendLine($"Managed Heap Reserved: {FormatSize(heapReserved)}");
    sb.AppendLine($"VM Memory Reserved: {FormatSize(vmReserved)}");
    sb.AppendLine($"托管对象内存占用: {FormatSize(memUsage)}");
    sb.AppendLine($"活跃堆内存使用: {FormatSize(activeUsage)}");
    sb.AppendLine($"活跃堆空闲空间: {FormatSize(emptySpace)}");

    var moField = mdType.GetField("m_ManagedObjects", BindingFlags.Instance | BindingFlags.NonPublic);
    var moArray = moField.GetValue(crawledData);
    var moArrayType = moArray.GetType();
    long moCount = (long)moArrayType.GetProperty("Count").GetValue(moArray);
    sb.AppendLine($"托管对象总数: {moCount}");

    var tdField = cachedType.GetField("TypeDescriptions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    var td = tdField.GetValue(snapshot);
    var typeNames = (string[])td.GetType().GetField("TypeDescriptionName", BindingFlags.Instance | BindingFlags.Public).GetValue(td);

    var moiType = editorAsm.GetType("Unity.MemoryProfiler.Editor.ManagedObjectInfo");
    var fTypeDesc = moiType.GetField("ITypeDescription", BindingFlags.Instance | BindingFlags.Public);
    var fSize = moiType.GetField("Size", BindingFlags.Instance | BindingFlags.Public);
    var fNativeIdx = moiType.GetField("NativeObjectIndex", BindingFlags.Instance | BindingFlags.Public);


    var typeStats = new Dictionary<string, (long count, long totalSize)>();
    long totalManagedSize = 0;
    long objCount = 0;
    var topObjects = new List<(string typeName, long size, long nativeIdx)>();

    foreach (var obj in MemoryValues(moArray))
    {
        int typeIdx = (int)fTypeDesc.GetValue(obj);
        long size = Convert.ToInt64(fSize.GetValue(obj));
        long nativeIdx = Convert.ToInt64(fNativeIdx.GetValue(obj));

        string typeName = (typeIdx >= 0 && typeIdx < typeNames.Length) ? typeNames[typeIdx] : $"Unknown({typeIdx})";

        if (!typeStats.ContainsKey(typeName)) typeStats[typeName] = (0, 0);
        var s = typeStats[typeName];
        typeStats[typeName] = (s.count + 1, s.totalSize + size);
        totalManagedSize += size;
        objCount++;

        if (size > 1024) { topObjects.Add((typeName, size, nativeIdx)); if (topObjects.Count > 60) topObjects = topObjects.OrderByDescending(x => x.size).Take(30).ToList(); }
    }

    sb.AppendLine($"遍历对象数: {objCount}");
    sb.AppendLine($"托管对象总大小: {FormatSize((ulong)totalManagedSize)}");

    sb.AppendLine($"\n--- Top 30 托管类型 (按总大小) ---");
    foreach (var kv in typeStats.OrderByDescending(x => x.Value.totalSize).Take(30))
        sb.AppendLine($"  {kv.Key,-60} x{kv.Value.count,-8} {FormatSize((ulong)kv.Value.totalSize)}");

    sb.AppendLine($"\n--- Top 30 单个托管对象 (按大小) ---");
    foreach (var obj in topObjects.OrderByDescending(x => x.size).Take(30))
    {
        string name = obj.typeName.Length > 60 ? obj.typeName.Substring(0, 57) + "..." : obj.typeName;
        string native = obj.nativeIdx >= 0 ? $" (native#{obj.nativeIdx})" : "";
        sb.AppendLine($"  {name,-60} {FormatSize((ulong)obj.size)}{native}");
    }
}
catch (Exception) { throw; }
finally
{
    try { if (snapshot is IDisposable d) d.Dispose(); } catch {}
    try { if (reader is IDisposable d) d.Dispose(); } catch {}
    if ((bool?)options["collectAfterAnalysis"] == true) { GC.Collect(); warnings.Add("分析后主动执行了 GC.Collect"); }
}

return sb.ToString();

string FormatSize(ulong bytes)
{
    if (bytes >= 1024UL * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    if (bytes >= 1024UL * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
    if (bytes >= 1024UL) return $"{bytes / 1024.0:F1} KB";
    return $"{bytes} B";
}

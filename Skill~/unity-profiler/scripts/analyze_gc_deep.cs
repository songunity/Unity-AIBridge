using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Text;
using System.Collections.Generic;
using System.Linq;

var sb = new StringBuilder();
sb.AppendLine($"采样范围：指定线程（默认 0）；Deep Profiling={ProfilerDriver.deepProfiling}");
int firstFrame = (int?)options["firstFrame"] ?? ProfilerDriver.firstFrameIndex;
int lastFrame = (int?)options["lastFrame"] ?? ProfilerDriver.lastFrameIndex;
if (firstFrame < 0 || lastFrame < firstFrame) return DiagnosticResult("blocked", "没有已录制的 Profiler 帧。");

int step = Mathf.Max(1, (lastFrame - firstFrame) / 30);
var gcByMethod = new Dictionary<string, (long bytes, int count, long maxSingleAlloc)>();
var gcPerFrame = new List<(int frame, long bytes, int count)>();
long grandTotal = 0;

for (int f = firstFrame; f <= lastFrame; f += step)
{
    using var fd = ProfilerDriver.GetRawFrameDataView(f, (int?)options["threadIndex"] ?? 0);
    if (fd == null || !fd.valid) continue;

    // 原始采样按子树展开；递归子节点数量确定祖先的结束位置。
    var parents = new int[fd.sampleCount];
    var ancestors = new Stack<(int index, int end)>();
    for (int s = 0; s < fd.sampleCount; s++)
    {
        while (ancestors.Count > 0 && s >= ancestors.Peek().end) ancestors.Pop();
        parents[s] = ancestors.Count > 0 ? ancestors.Peek().index : -1;
        int descendants = fd.GetSampleChildrenCountRecursive(s);
        if (descendants > 0) ancestors.Push((s, s + descendants + 1));
    }

    long frameBytes = 0;
    int frameCount = 0;

    for (int s = 0; s < fd.sampleCount; s++)
    {
        string name = fd.GetSampleName(s);
        if (name == "GC.Alloc" && fd.GetSampleMetadataCount(s) > 0)
        {
            long bytes = fd.GetSampleMetadataAsLong(s, 0);
            frameBytes += bytes;
            frameCount++;

            int parent = parents[s];
            int grandparent = parent >= 0 ? parents[parent] : -1;
            string caller = parent >= 0 ? fd.GetSampleName(parent) : "<root>";
            string fullChain = grandparent >= 0
                ? $"{fd.GetSampleName(grandparent)} -> {caller}" : caller;

            if (gcByMethod.TryGetValue(fullChain, out var ex))
                gcByMethod[fullChain] = (ex.bytes + bytes, ex.count + 1, System.Math.Max(ex.maxSingleAlloc, bytes));
            else
                gcByMethod[fullChain] = (bytes, 1, bytes);
        }
    }

    gcPerFrame.Add((f, frameBytes, frameCount));
    grandTotal += frameBytes;
}

if (gcPerFrame.Count == 0) return DiagnosticResult("blocked", "选定范围内没有有效的线程 0 帧数据。");

sb.AppendLine($"=== GC Alloc Overview ({gcPerFrame.Count} frames sampled) ===");
sb.AppendLine($"Total: {grandTotal / 1024f:F1} KB, avg per frame: {grandTotal / 1024f / gcPerFrame.Count:F1} KB");
sb.AppendLine($"Min frame: {gcPerFrame.Min(x => x.bytes) / 1024f:F1} KB, Max frame: {gcPerFrame.Max(x => x.bytes) / 1024f:F1} KB");

sb.AppendLine("\n=== GC Per Frame Trend ===");
foreach (var g in gcPerFrame)
    sb.AppendLine($"  Frame {g.frame}: {g.bytes / 1024f:F1} KB ({g.count} allocs)");

sb.AppendLine("\n=== Top 30 GC Sources (caller -> method) ===");
foreach (var kv in gcByMethod.OrderByDescending(x => x.Value.bytes).Take(30))
{
    var v = kv.Value;
    sb.AppendLine($"  {kv.Key}");
    sb.AppendLine($"    total={v.bytes / 1024f:F1}KB  count={v.count}  avg={v.bytes / v.count}B  maxSingle={v.maxSingleAlloc}B");
}

// Auto-categorize by namespace/class prefix
var categories = new Dictionary<string, long>();
foreach (var kv in gcByMethod)
{
    string key = kv.Key;
    string cat = "Other";

    // Extract category from method name pattern: "Namespace.dll!Namespace::Class.Method"
    int dllIdx = key.IndexOf(".dll!");
    if (dllIdx >= 0)
    {
        string afterDll = key.Substring(dllIdx + 4);
        int colonIdx = afterDll.IndexOf("::");
        if (colonIdx >= 0)
        {
            string ns = afterDll.Substring(0, colonIdx).TrimStart();
            // Use top-level namespace as category
            int dotIdx = ns.IndexOf('.');
            cat = dotIdx >= 0 ? ns.Substring(0, dotIdx) : ns;
        }
    }
    else if (key.Contains("Unity") || key.Contains("MonoBehaviour"))
    {
        cat = "Unity Internal";
    }

    if (categories.TryGetValue(cat, out long ex2))
        categories[cat] = ex2 + kv.Value.bytes;
    else
        categories[cat] = kv.Value.bytes;
}

sb.AppendLine("\n=== GC by Category ===");
foreach (var kv in categories.OrderByDescending(x => x.Value))
    sb.AppendLine($"  {kv.Key}: {kv.Value / 1024f:F1} KB ({(grandTotal > 0 ? kv.Value * 100.0 / grandTotal : 0):F1}%)");

return sb.ToString();

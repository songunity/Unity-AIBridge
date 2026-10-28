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

// Frame time stats
var frameTimes = new List<float>();
for (int f = firstFrame; f <= lastFrame; f++)
{
    using var fd = ProfilerDriver.GetRawFrameDataView(f, (int?)options["threadIndex"] ?? 0);
    if (fd != null && fd.valid) { frameTimes.Add(fd.frameTimeMs); }
}
frameTimes.Sort();
int cnt = frameTimes.Count;
if (cnt == 0) return DiagnosticResult("blocked", "选定范围内没有有效的线程 0 帧数据。");
sb.AppendLine($"=== Deep Profile Frame Stats ({cnt} frames) ===");
sb.AppendLine($"Avg: {frameTimes.Average():F1}ms  Median: {(cnt % 2 == 0 ? (frameTimes[cnt/2-1] + frameTimes[cnt/2]) / 2f : frameTimes[cnt/2]):F1}ms  Min: {frameTimes[0]:F1}ms  Max: {frameTimes[cnt-1]:F1}ms");
sb.AppendLine($"30fps hit: {frameTimes.Count(t => t <= 33.33f)}/{cnt}  Jank>50ms: {frameTimes.Count(t => t > 50)}");

// Deep analysis: sample 20 frames
sb.AppendLine("\n=== Deep Profile Hot Path (线程 0，抽样；total 包含子调用) ===");
int step = Mathf.Max(1, (lastFrame - firstFrame + 1) / 20);
int sampledFrames = 0;
var methodTime = new Dictionary<string, (double total, double self, float max, int count)>();
var gcByMethod = new Dictionary<string, (long bytes, int count)>();

for (int f = firstFrame; f <= lastFrame; f += step)
{
    using var fd = ProfilerDriver.GetRawFrameDataView(f, (int?)options["threadIndex"] ?? 0);
    if (fd == null || !fd.valid) continue;
    sampledFrames++;

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
    var selfTimes = new float[fd.sampleCount];
    for (int s = 0; s < fd.sampleCount; s++) selfTimes[s] = fd.GetSampleTimeMs(s);
    for (int s = 0; s < fd.sampleCount; s++)
        if (parents[s] >= 0) selfTimes[parents[s]] -= fd.GetSampleTimeMs(s);

    for (int s = 0; s < fd.sampleCount; s++)
    {
        string name = fd.GetSampleName(s);
        float ms = fd.GetSampleTimeMs(s);

        if (string.IsNullOrEmpty((string)options["markerFilter"]) || name.IndexOf((string)options["markerFilter"], System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (methodTime.TryGetValue(name, out var ex))
                methodTime[name] = (ex.total + ms, ex.self + Mathf.Max(0, selfTimes[s]), Mathf.Max(ex.max, ms), ex.count + 1);
            else
                methodTime[name] = (ms, Mathf.Max(0, selfTimes[s]), ms, 1);
        }

        if (name == "GC.Alloc" && fd.GetSampleMetadataCount(s) > 0)
        {
            long bytes = fd.GetSampleMetadataAsLong(s, 0);
            int parent = parents[s];
            string pName = parent >= 0 ? fd.GetSampleName(parent) : "<root>";
            if (gcByMethod.TryGetValue(pName, out var gex))
                gcByMethod[pName] = (gex.bytes + bytes, gex.count + 1);
            else
                gcByMethod[pName] = (bytes, 1);
        }
    }
}

sb.AppendLine($"\nTop 25 Methods by Self Time ({sampledFrames} frames sampled):");
foreach (var kv in methodTime.OrderByDescending(x => x.Value.self).Take(25))
{
    var v = kv.Value;
    sb.AppendLine($"  {kv.Key}");
    sb.AppendLine($"    total={v.total:F2}ms  self={v.self:F2}ms  avg={v.total/v.count:F2}ms  max={v.max:F2}ms  calls={v.count}");
}

sb.AppendLine("\nTop 10 Methods by Max Single Call:");
foreach (var kv in methodTime.OrderByDescending(x => x.Value.max).Take(10))
{
    var v = kv.Value;
    sb.AppendLine($"  {kv.Key}: max={v.max:F2}ms avg={v.total/v.count:F2}ms");
}

sb.AppendLine("\nTop 15 GC Alloc Sources (采样树直接父节点):");
foreach (var kv in gcByMethod.OrderByDescending(x => x.Value.bytes).Take(15))
{
    sb.AppendLine($"  {kv.Key}: {kv.Value.bytes/1024f:F1}KB ({kv.Value.count} allocs)");
}

// Jank frames (>50ms)
sb.AppendLine("\n=== Jank Frames (>50ms) ===");
for (int f = firstFrame; f <= lastFrame; f++)
{
    using var fd = ProfilerDriver.GetRawFrameDataView(f, (int?)options["threadIndex"] ?? 0);
    if (fd == null || !fd.valid) continue;
    if (fd.frameTimeMs > 50)
    {
        sb.Append($"Frame {f}: {fd.frameTimeMs:F1}ms  ");
        var markers = new Dictionary<string, float>();
        for (int s = 0; s < fd.sampleCount; s++)
        {
            string n = fd.GetSampleName(s); float m = fd.GetSampleTimeMs(s);
            if (markers.TryGetValue(n, out float e)) markers[n] = e + m; else markers[n] = m;
        }
        sb.AppendLine(string.Join(", ", markers.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key}={kv.Value:F1}ms")));
    }
}

return sb.ToString();

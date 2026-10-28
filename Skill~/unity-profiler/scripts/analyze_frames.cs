using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.Profiling;
using System.Text;
using System.Collections.Generic;
using System.Linq;

var sb = new StringBuilder();
sb.AppendLine($"采样范围：指定线程（默认 0）；Deep Profiling={ProfilerDriver.deepProfiling}");
int firstFrame = (int?)options["firstFrame"] ?? ProfilerDriver.firstFrameIndex;
int lastFrame = (int?)options["lastFrame"] ?? ProfilerDriver.lastFrameIndex;
if (firstFrame < 0 || lastFrame < firstFrame) return DiagnosticResult("blocked", "没有已录制的 Profiler 帧。");

var frameTimes = new List<float>();
float maxFrameTime = 0;
int maxFrameIdx = 0;
float totalTime = 0;

for (int f = firstFrame; f <= lastFrame; f++)
{
    using var fd = ProfilerDriver.GetRawFrameDataView(f, (int?)options["threadIndex"] ?? 0);
    if (fd != null && fd.valid)
    {
        float ms = fd.frameTimeMs;
        frameTimes.Add(ms);
        totalTime += ms;
        if (ms > maxFrameTime) { maxFrameTime = ms; maxFrameIdx = f; }
    }
}

frameTimes.Sort();
int count = frameTimes.Count;
if (count == 0) return DiagnosticResult("blocked", "选定范围内没有有效的线程 0 帧数据。");
float median = count % 2 == 0 ? (frameTimes[count / 2 - 1] + frameTimes[count / 2]) / 2f : frameTimes[count / 2];
float avg = totalTime / count;
float p95 = frameTimes[Mathf.CeilToInt(count * 0.95f) - 1];
float p99 = frameTimes[Mathf.CeilToInt(count * 0.99f) - 1];

sb.AppendLine("=== Frame Time Stats ===");
sb.AppendLine($"Frames: {count}");
sb.AppendLine($"Avg: {avg:F2}ms  Median: {median:F2}ms");
sb.AppendLine($"Min: {frameTimes[0]:F2}ms  Max: {maxFrameTime:F2}ms (frame {maxFrameIdx})");
sb.AppendLine($"P95: {p95:F2}ms  P99: {p99:F2}ms");
sb.AppendLine($"Target 60fps (16.67ms): {frameTimes.Count(t => t <= 16.67f)}/{count} frames hit");
sb.AppendLine($"Target 30fps (33.33ms): {frameTimes.Count(t => t <= 33.33f)}/{count} frames hit");

int jank50 = frameTimes.Count(t => t > 50);
int jank100 = frameTimes.Count(t => t > 100);
sb.AppendLine($"Jank >50ms: {jank50}  >100ms: {jank100}");

var markerTotalTime = new Dictionary<string, (double total, int count, float max)>();
int sampleStep = Mathf.Max(1, (lastFrame - firstFrame + 1) / 50);
int sampledFrames = 0;

for (int f = firstFrame; f <= lastFrame; f += sampleStep)
{
    using var fd = ProfilerDriver.GetRawFrameDataView(f, (int?)options["threadIndex"] ?? 0);
    if (fd == null || !fd.valid) continue;
    sampledFrames++;

    for (int s = 0; s < fd.sampleCount; s++)
    {
        string name = fd.GetSampleName(s);
        float sampleMs = fd.GetSampleTimeMs(s);

        if (markerTotalTime.TryGetValue(name, out var existing))
        {
            markerTotalTime[name] = (existing.total + sampleMs, existing.count + 1, Mathf.Max(existing.max, sampleMs));
        }
        else
        {
            markerTotalTime[name] = (sampleMs, 1, sampleMs);
        }
    }
}

sb.AppendLine($"\n=== Top 25 Markers by Total Time (线程 0，抽样 {sampledFrames} 帧，包含子调用；不能跨层求和) ===");
var topMarkers = markerTotalTime.OrderByDescending(kv => kv.Value.total).Take(25);
foreach (var kv in topMarkers)
{
    var v = kv.Value;
    sb.AppendLine($"  {kv.Key}: total={v.total:F2}ms count={v.count} max={v.max:F2}ms avg={v.total/v.count:F2}ms");
}

return sb.ToString();

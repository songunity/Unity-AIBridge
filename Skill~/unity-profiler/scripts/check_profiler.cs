using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Text;

var sb = new StringBuilder();

int firstFrame = (int?)options["firstFrame"] ?? ProfilerDriver.firstFrameIndex;
int lastFrame = (int?)options["lastFrame"] ?? ProfilerDriver.lastFrameIndex;
sb.AppendLine($"Profiler frames: {firstFrame} ~ {lastFrame} (total: {lastFrame - firstFrame + 1})");

sb.AppendLine($"Profiler enabled: {UnityEngine.Profiling.Profiler.enabled}");

if (firstFrame >= 0 && lastFrame >= firstFrame)
{
    sb.AppendLine("\n=== Last 5 frames top markers ===");
    for (int f = Mathf.Max(firstFrame, lastFrame - 4); f <= lastFrame; f++)
    {
        using var frameData = ProfilerDriver.GetRawFrameDataView(f, (int?)options["threadIndex"] ?? 0);
        if (frameData != null && frameData.valid)
        {
            float frameMs = frameData.frameTimeMs;
            sb.AppendLine($"Frame {f}: {frameMs:F2}ms, markers: {frameData.sampleCount}");
        }
    }
}
else
{
    sb.AppendLine("No profiler data available.");
}

return sb.ToString();

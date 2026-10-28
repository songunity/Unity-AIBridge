using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Text;

var sb = new StringBuilder();

int firstFrame = (int?)options["firstFrame"] ?? ProfilerDriver.firstFrameIndex;
int lastFrame = (int?)options["lastFrame"] ?? ProfilerDriver.lastFrameIndex;
sb.AppendLine($"Profiler frames: {firstFrame} ~ {lastFrame} (total: {lastFrame - firstFrame + 1})");
sb.AppendLine($"Profiler enabled: {UnityEngine.Profiling.Profiler.enabled}");
sb.AppendLine($"Deep profiling: {ProfilerDriver.deepProfiling}");

if (firstFrame >= 0 && lastFrame >= firstFrame)
{
    using var fd = ProfilerDriver.GetRawFrameDataView(lastFrame, 0);
    if (fd != null && fd.valid)
    {
        sb.AppendLine($"\nLast frame ({lastFrame}): {fd.frameTimeMs:F2}ms, samples: {fd.sampleCount}");

        int shown = 0;
        for (int s = 0; s < fd.sampleCount && shown < 30; s++)
        {
            int depth = fd.GetSampleChildrenCount(s);
            string name = fd.GetSampleName(s);
            float ms = fd.GetSampleTimeMs(s);
            if (ms > 0.1f)
            {
                sb.AppendLine($"  [{s}] {name}: {ms:F2}ms children:{depth}");
                shown++;
            }
        }
    }
}

return sb.ToString();

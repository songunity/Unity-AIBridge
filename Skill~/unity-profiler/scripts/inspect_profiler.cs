using UnityEditor.Profiling;
using System.Reflection;

int first = (int?)options["firstFrame"] ?? Math.Max(ProfilerDriver.firstFrameIndex, ProfilerDriver.lastFrameIndex - 59);
int last = (int?)options["lastFrame"] ?? ProfilerDriver.lastFrameIndex;
if (first < 0 || last < first) return DiagnosticResult("blocked", "没有可读取的 Profiler 帧");
if (last - first >= 1000) throw new ArgumentException("单次最多分析 1000 帧，请缩小范围");
int top = Limit("top", 30, 200);
int sampleLimit = Limit("maxSamples", 200000, 2000000);
int detailLimit = Limit("maxDetails", 2000, 20000);
string filter = (string)options["markerFilter"] ?? "";
string threadFilter = (string)options["thread"] ?? "";
bool detailed = (bool?)options["details"] ?? false;
var threads = new Dictionary<string, object>();
var markers = new Dictionary<string, (string name, ulong thread, double total, double self, int calls)>();
var frames = new List<object>();
var details = new List<object>();
var counters = new List<object>();
int samples = 0, missingFrames = 0, stackCount = 0, flowCount = 0;
int unnamedMarkers = 0;
bool countersTruncated = false;
bool truncated = false;
var analysisTimer = System.Diagnostics.Stopwatch.StartNew();
int budgetMs = Limit("budgetMs", 10000, 300000);
var availability = typeof(ProfilerDriver).GetMethod("GetStatisticsAvailabilityState", BindingFlags.Static | BindingFlags.NonPublic,
    null, new[] { typeof(UnityEngine.Profiling.ProfilerArea), typeof(int) }, null);
context["firstFrame"] = first; context["lastFrame"] = last; context["threadFilter"] = threadFilter;
for (int f = first; f <= last && !truncated; f++)
{
    bool any = false;
    for (int ti = 0; ti < 256 && !truncated; ti++)
    {
        if (analysisTimer.ElapsedMilliseconds >= budgetMs) { truncated = true; break; }
        using var fd = ProfilerDriver.GetRawFrameDataView(f, ti);
        if (fd == null || !fd.valid) break;
        if (threadFilter.Length > 0 && fd.threadName.IndexOf(threadFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
        any = true;
        string threadKey = fd.threadId.ToString();
        threads[threadKey] = new { id = threadKey, name = fd.threadName, group = fd.threadGroupName };
        int? gpuState = availability == null ? null : (int?)availability.Invoke(null, new object[] { UnityEngine.Profiling.ProfilerArea.GPU, f });
        frames.Add(new { frame = f, thread = threadKey, frameMs = fd.frameTimeMs,
            gpuMs = gpuState.HasValue && gpuState.Value == 3 ? (float?)fd.frameGpuTimeMs : null, gpuAvailability = gpuState });
        if (samples + fd.sampleCount > sampleLimit) { truncated = true; break; }
        samples += fd.sampleCount;
        var parents = new int[fd.sampleCount];
        var self = new float[fd.sampleCount];
        var ancestors = new Stack<(int index, int end)>();
        for (int i = 0; i < fd.sampleCount; i++) {
            while (ancestors.Count > 0 && i >= ancestors.Peek().end) ancestors.Pop();
            parents[i] = ancestors.Count == 0 ? -1 : ancestors.Peek().index;
            self[i] += fd.GetSampleTimeMs(i);
            if (parents[i] >= 0) self[parents[i]] -= fd.GetSampleTimeMs(i);
            int children = fd.GetSampleChildrenCountRecursive(i);
            if (children > 0) ancestors.Push((i, i + children + 1));
        }
        for (int i = 0; i < fd.sampleCount; i++) {
            if (analysisTimer.ElapsedMilliseconds >= budgetMs) { truncated = true; break; }
            string name = fd.GetSampleName(i);
            if (string.IsNullOrEmpty(name)) { unnamedMarkers++; name = "<unnamed marker>"; }
            if (parents[i] < 0 && (bool?)options["includeRoots"] != true) continue;
            if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            int markerId = fd.GetSampleMarkerId(i);
            string key = threadKey + ":" + markerId + ":" + name;
            markers.TryGetValue(key, out var old);
            markers[key] = (name, fd.threadId, old.total + fd.GetSampleTimeMs(i), old.self + Math.Max(0, self[i]), old.calls + 1);
            if (!detailed || details.Count >= detailLimit) continue;
            var addresses = new List<ulong>();
            if (name == "GC.Alloc" || (bool?)options["allCallstacks"] == true) fd.GetSampleCallstack(i, addresses);
            var stack = addresses.Select(address => {
                var method = fd.ResolveMethodInfo(address);
                return new { address = "0x" + address.ToString("X"), method = method.methodName, file = method.sourceFileName, line = method.sourceFileLine };
            }).ToArray();
            if (stack.Length > 0) stackCount++;
            var flows = new List<RawFrameDataView.FlowEvent>(); fd.GetSampleFlowEvents(i, flows); flowCount += flows.Count;
            long? gcBytes = name == "GC.Alloc" && fd.GetSampleMetadataCount(i) > 0 ? (long?)fd.GetSampleMetadataAsLong(i, 0) : null;
            details.Add(new { frame = f, thread = threadKey, sample = i, parent = parents[i], markerId, name,
                category = fd.GetSampleCategoryIndex(i), startMs = fd.GetSampleStartTimeMs(i), totalMs = fd.GetSampleTimeMs(i), selfMs = Math.Max(0, self[i]), gcBytes,
                callstack = stack, flows = flows.Select(x => new { id = x.FlowId, type = x.FlowEventType.ToString(), sample = x.ParentSampleIndex }).ToArray() });
        }
        var infos = new List<FrameDataView.MarkerInfo>(); fd.GetMarkers(infos);
        foreach (var info in infos) {
            if (!fd.HasCounterValue(info.id)) continue;
            if (counters.Count >= detailLimit) { countersTruncated = true; break; }
            var metadata = info.metadataInfo;
            string type = metadata != null && metadata.Length > 0 ? metadata[0].type.ToString() : "unknown";
            object value = null;
            switch (type) {
                case "Int32": value = fd.GetCounterValueAsInt(info.id); break;
                case "Int64": value = fd.GetCounterValueAsLong(info.id); break;
                case "Float": value = fd.GetCounterValueAsFloat(info.id); break;
                case "Double": value = fd.GetCounterValueAsDouble(info.id); break;
                default: break;
            }
            counters.Add(new { frame = f, thread = threadKey, info.name, type, value,
                unit = metadata != null && metadata.Length > 0 ? metadata[0].unit.ToString() : "unknown" });
        }
    }
    if (!any) missingFrames++;
    if ((f - first) % 10 == 9) await Task.Yield();
}
if (truncated) warnings.Add("达到采样数量或时间预算；缩小帧范围后重试分析，不需重新采集");
if (detailed && details.Count >= detailLimit) warnings.Add("详细采样达到 maxDetails 上限，仅导出前部匹配采样");
if (detailed && stackCount == 0) warnings.Add("匹配采样没有调用栈；确认采集时 Call Stacks 开关及符号可用性，无法事后补采");
if (missingFrames > 0) warnings.Add("部分帧或指定线程不可用；录制中帧可能已被历史缓冲区覆盖");
if (unnamedMarkers > 0) warnings.Add("部分采样没有 Marker 名称，已标记 unnamed；不能据此做方法归因");
if (countersTruncated) warnings.Add("计数器明细达到 maxDetails 上限，已截断");
var hotspots = markers.Values.OrderByDescending(x => x.self).Take(top).Select(x => new { x.name, thread = x.thread.ToString(), totalMs = x.total, selfMs = x.self, x.calls }).ToArray();
SaveArtifact("profiler", new { context, threads = threads.Values, frames, hotspots, details, counters, warnings });
return DiagnosticResult(threads.Count == 0 ? "blocked" : truncated || missingFrames > 0 || countersTruncated || (detailed && details.Count >= detailLimit) ? "partial" : "completed",
    new { threads = threads.Count, samples, missingFrames, callstackSamples = stackCount, flowEvents = flowCount, counterValues = counters.Count, hotspots });

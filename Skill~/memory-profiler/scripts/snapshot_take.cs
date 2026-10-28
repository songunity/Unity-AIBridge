using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using Unity.Profiling.Memory;

var sb = new StringBuilder();

string folder = Path.Combine(Application.dataPath, "../MemorySnapshots");
if (!Directory.Exists(folder))
    Directory.CreateDirectory(folder);

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

return sb.ToString();

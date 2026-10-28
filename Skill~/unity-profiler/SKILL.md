---
name: unity-profiler
description: 通过 AIBridge 分析 Unity 已录制的帧、线程、Marker、GC 调用栈、Flow 与计数器。用于 CPU/GC 性能诊断；不会自动开启 Deep Profiling。
---

# unity-profiler

## 入口

先读取同级 `aibridge/SKILL.md`，定位目标 Unity 项目内 CLI 的绝对路径。通过本技能的 `scripts/run.py` 执行脚本；它合并公共代码和参数，不要把需要公共上下文的脚本片段直接传给 `--file`。

```powershell
python -X utf8 '<技能绝对路径>/scripts/run.py' <脚本名> --cli '<Unity项目>/.aibridge/cli/AIBridgeCLI.exe' --options '{}' --timeout 300000
```

复杂参数用 `--options-file <JSON文件>`。不依赖 Shell 工作目录，不改变 Play Mode、连接目标或采样开关。先做只读状态检查；需要开启录制、Deep Profile、暂停或切换目标时按任务明确操作并记录原状态。

返回 `diagnostic.status/summary/context/artifacts/warnings`。只有 `completed` 返回退出码 0；`blocked/partial/unknown/failed` 不能当作完整成功。完整报告位于 Unity 项目 `.aibridge/diagnostics/`；内存 `.snap` 文件保留在 `MemorySnapshots/`，以 artifacts 返回的路径为准。先读摘要，再按需读文件。

入口先提交并记录原命令 ID，然后等待结果。超时不取消底层采集；按原 ID 查询，不重发拍照或抓帧。`--timeout` 同时设置异步执行等待上限；无法强行中止同步原生调用。不要同时运行多个采集/快照分析任务。

## 选择脚本

- `check_profiler`：录制状态和最近帧；`check_deep_profile`：采样深度检查。
- `inspect_profiler`：默认最近 60 帧、全部线程的概览；`details=true` 导出受预算限制的采样时间线、真实已录制调用栈及 Flow。默认不限制业务命名空间。
- `analyze_frames`：单线程帧分布和 P95/P99；`analyze_deep_profile`：单线程自身耗时及热点；`analyze_gc_deep`：GC 分配汇总。
- `analyze_runtime_snapshot`：当前 Editor 进程的对象与内存、渲染统计，不冒充远端 Player 快照。

`inspect_profiler` 参数：`firstFrame/lastFrame`、`thread`（线程名称子串）、`markerFilter`、`top`（默认30）、`details`、`maxSamples`（默认200000）、`maxDetails`（默认2000）、`budgetMs`（默认10000）。调用栈默认针对 GC.Alloc；`allCallstacks=true` 才读取其他匹配采样的已录制栈。旧汇总脚本支持 `firstFrame/lastFrame/threadIndex`，不能将不同线程耗时相加当作帧耗时。

GC Call Stacks 与 Deep Profile 是不同开关：先检查现有数据，需要时重新采集；空调用栈不等于没有分配。GPU 时间只在帧的可用性标志通过时报告，否则为 null。Counter 未支持的数据类型保留类型并返回 null，不填零。Flow 仅表示已有记录的调度关联，不保证完整因果链。

报告记录连接目标、Unity 版本和采样模式；比较前后保持这些条件一致。帧可能被循环缓冲覆盖，缺失、截断和抽样结果必须标明。切换目标后不能把旧帧默认当作新目标数据。

热点默认排除线程根采样；需要时设 `includeRoots=true`。自身耗时仍可能包含等待，不等于 CPU 利用率。没有名称的 Marker 保留为 unnamed，并明确提示不能做方法归因。

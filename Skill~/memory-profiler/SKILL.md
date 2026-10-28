---
name: memory-profiler
description: 通过 AIBridge 拍摄、读取和比较 Unity 内存快照，按需查询引用关系。分析需要兼容的 Memory Profiler 包；采集和托管堆分析会影响现场。
---

# memory-profiler

## 入口

先读取同级 `aibridge/SKILL.md`，定位目标 Unity 项目内 CLI 的绝对路径。通过本技能的 `scripts/run.py` 执行脚本；它合并公共代码和参数，不要把需要公共上下文的脚本片段直接传给 `--file`。

```powershell
python -X utf8 '<技能绝对路径>/scripts/run.py' <脚本名> --cli '<Unity项目>/.aibridge/cli/AIBridgeCLI.exe' --options '{}' --timeout 300000
```

复杂参数用 `--options-file <JSON文件>`。不依赖 Shell 工作目录，不改变 Play Mode、连接目标或采样开关。先做只读状态检查；需要开启录制、Deep Profile、暂停或切换目标时按任务明确操作并记录原状态。

返回 `diagnostic.status/summary/context/artifacts/warnings`。只有 `completed` 返回退出码 0；`blocked/partial/unknown/failed` 不能当作完整成功。完整报告位于 Unity 项目 `.aibridge/diagnostics/`；内存 `.snap` 文件保留在 `MemorySnapshots/`，以 artifacts 返回的路径为准。先读摘要，再按需读文件。

入口先提交并记录原命令 ID，然后等待结果。超时不取消底层采集；按原 ID 查询，不重发拍照或抓帧。`--timeout` 同时设置异步执行等待上限；无法强行中止同步原生调用。不要同时运行多个采集/快照分析任务。

## 选择脚本

- `snapshot_take`：只拍照，不依赖分析包。
- `snapshot_analyze` / `snapshot_analyze_full`：拍照后分析 Native / Native+Managed。
- `snapshot_load` / `managed_analyze`：用 `snapshot` 指定已有文件，分析 Native / Managed。
- `snapshot_compare`：显式指定 `snapshot` 与 `baseline`，串行加载并比较聚合数据。
- `snapshot_details`：快照对象、分配区域及引用查询。`nativeIndex` 指定快照中的 Native 对象索引；`depth`（默认3）、`maxNodes`（默认100）、`top`（默认30）限制查询。

分析适配以 `com.unity.memoryprofiler` 1.1.0 源码为基准，缺包或接口不匹配明确失败，不自动安装依赖。托管爬取通过 `ManagedDataCrawler.Crawl(snapshot)` 推进；阶段间让出更新，但每个内部阶段仍可能长时间占用主线程。

拍照前检查编译/导入状态。可用 `targetId` 校验当前 Profiler 连接目标；实际目标还要结合快照元数据确认，不能凭 API 的 isRemote 参数推断。`nativeStacks=true` 额外请求 NativeAllocationSites/NativeStackTraces，增加采集开销；现有文件缺少的分配栈不能事后补出。

一次只加载一个快照，所有路径释放 reader/snapshot。不自动 GC；仅 `collectAfterAnalysis=true` 才主动执行并报告干预。对比保留聚合结果，不同时持有两份全量快照对象列表。名称分组和快照索引不是跨快照对象身份；引用查询达到深度/节点上限时，不把边界当作 GC Root，也不单凭增长认定泄漏。

1.1.0 的 DynamicArray 枚举器存在末项遗漏，公共适配仅对该已核对版本补读末项并校验 Count；其他版本数量不一致时失败，不能默默少统计一个对象。

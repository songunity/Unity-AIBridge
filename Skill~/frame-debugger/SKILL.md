---
name: frame-debugger
description: 通过 AIBridge 检查和捕获 Unity Frame Debugger 事件、渲染状态与 Shader 绑定，分析 Draw Call 和 Batch Break；不提供逐事件 GPU 时间。
---

# frame-debugger

## 入口

先读取同级 `aibridge/SKILL.md`，定位目标 Unity 项目内 CLI 的绝对路径。通过本技能的 `scripts/run.py` 执行脚本；它合并公共代码和参数，不要把需要公共上下文的脚本片段直接传给 `--file`。

```powershell
python -X utf8 '<技能绝对路径>/scripts/run.py' <脚本名> --cli '<Unity项目>/.aibridge/cli/AIBridgeCLI.exe' --options '{}' --timeout 300000
```

复杂参数用 `--options-file <JSON文件>`。不依赖 Shell 工作目录，不改变 Play Mode、连接目标或采样开关。先做只读状态检查；需要开启录制、Deep Profile、暂停或切换目标时按任务明确操作并记录原状态。

返回 `diagnostic.status/summary/context/artifacts/warnings`。只有 `completed` 返回退出码 0；`blocked/partial/unknown/failed` 不能当作完整成功。完整报告位于 Unity 项目 `.aibridge/diagnostics/`；内存 `.snap` 文件保留在 `MemorySnapshots/`，以 artifacts 返回的路径为准。先读摘要，再按需读文件。

入口先提交并记录原命令 ID，然后等待结果。超时不取消底层采集；按原 ID 查询，不重发拍照或抓帧。`--timeout` 同时设置异步执行等待上限；无法强行中止同步原生调用。不要同时运行多个采集/快照分析任务。

## 选择脚本

- `check_frame_debugger`：只读检查能力，不打开窗口。
- `capture_frame_debugger`：捕获事件，保存渲染状态、RenderTarget Load/Store、Shader 参数绑定及 Compute 信息。
- `analyze_frame_debugger`：必须通过 `capture` 指定捕获 JSON，不默认取最新文件。
- `analyze_particle_materials`：当前场景粒子材质分布。

捕获参数：`budgetMs`（默认120000）、`maxEvents`（默认3000）。捕获前确保 Play Mode 已暂停；脚本记录并恢复原 Frame Debugger 启用状态和事件上限。每个事件有限等待索引匹配，取不到的数据列入 skipped，不以固定几毫秒延迟假定就绪。

内部 API 按 Unity 2022.3.62f3 核对，其他版本需要能力检查。事件数、Draw Call 和 GPU 耗时不同；Frame Debugger 数据没有逐事件 GPU 时间，不作时间归因。Shader 数组和嵌套字段有限额，截断信息在 warnings 中。Unity 对象只导出名称、类型和实例 ID，不递归序列化对象。

---
name: yooasset-debugger
description: 通过 AIBridge 检查 YooAsset 多资源包的 Provider、引用数、加载状态和依赖，保存及比较文件快照。零引用不是泄漏结论。
---

# yooasset-debugger

## 入口

先读取同级 `aibridge/SKILL.md`，定位目标 Unity 项目内 CLI 的绝对路径。通过本技能的 `scripts/run.py` 执行脚本；它合并公共代码和参数，不要把需要公共上下文的脚本片段直接传给 `--file`。

```powershell
python -X utf8 '<技能绝对路径>/scripts/run.py' <脚本名> --cli '<Unity项目>/.aibridge/cli/AIBridgeCLI.exe' --options '{}' --timeout 300000
```

复杂参数用 `--options-file <JSON文件>`。不依赖 Shell 工作目录，不改变 Play Mode、连接目标或采样开关。先做只读状态检查；需要开启录制、Deep Profile、暂停或切换目标时按任务明确操作并记录原状态。

返回 `diagnostic.status/summary/context/artifacts/warnings`。只有 `completed` 返回退出码 0；`blocked/partial/unknown/failed` 不能当作完整成功。完整报告位于 Unity 项目 `.aibridge/diagnostics/`；内存 `.snap` 文件保留在 `MemorySnapshots/`，以 artifacts 返回的路径为准。先读摘要，再按需读文件。

入口先提交并记录原命令 ID，然后等待结果。超时不取消底层采集；按原 ID 查询，不重发拍照或抓帧。`--timeout` 同时设置异步执行等待上限；无法强行中止同步原生调用。不要同时运行多个采集/快照分析任务。

## 选择脚本

- `yooasset_debug`：当前全部资源包概况，`package` 可限定包名，`top` 默认30。
- `yoo_snapshot_save`：保存独立 JSON 快照，返回文件路径，不写 PlayerPrefs。
- `yoo_snapshot_compare`：用 `baseline` 指定基线；校验格式、项目和目标，按资源包与资源路径聚合比较。

需要 Play Mode 且 YooAsset 已初始化。支持 `GetDiagnosticReport/PackageDataList/ReferenceCount/ElapsedMilliseconds`，并识别旧的 `GetDebugReport/PackageDatas/RefCount/LoadingTime` 字段；缺失接口明确失败，不返回伪造空报告。

零引用、仍在加载、等待 Bundle 清理和 Handle 泄漏是不同现象。不要为了验证而自动释放资源或触发卸载。Provider 引用、Bundle 依赖和卸载策略一起分析，连续可比快照仅提供残留线索，不自动判定泄漏。

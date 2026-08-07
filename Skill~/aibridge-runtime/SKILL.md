---
name: aibridge-runtime
description: 通过 AI Bridge CLI 直接连接正在运行的 Unity Player（无需启动 Unity Editor），也可连接已启用 Runtime Bridge 的 Editor Play Mode；用于发现 Runtime target、检查状态和日志、采样性能、截图、查询或点击 UGUI，以及发送已编译 DLL 执行。当用户提到 Player、真机、运行包、Runtime Bridge、runtime CLI、运行时 UI 或运行时性能诊断时使用。
---

# AI Bridge Runtime Skill

## 核心边界

- 直接使用 `AIBridgeCLI runtime <subcommand>` 连接 Runtime Bridge，不经过 Unity Editor 命令队列。
- 先证明 target 在线，再执行操作；命令存在不代表 Player 正在运行。
- `runtime exec` 只发送已编译 DLL，不编译 C# 源码。不要把 `RuntimeExecuteCommand_Execute` 当作独立 Runtime 路径。
- 需要操作 Unity Editor、资源、场景或普通 Editor Play Mode 命令面时改用 `$aibridge`；验证 Play Mode 中的 Runtime Bridge 链路仍使用本 Skill。

## 前置条件

- Player 构建时已启用并注入 Runtime Bridge，或者 Play Mode 场景中已创建 `AIBridgeRuntime`。
- 目标进程正在运行，所用 transport 已启用。
- CLI 通常位于 Unity 项目根目录的 `.aibridge/cli/AIBridgeCLI`；Windows 文件名为 `AIBridgeCLI.exe`。
- 始终添加 `--raw`；耗时操作显式设置 `--timeout <ms>`。
- HTTP 目标需要鉴权时添加 `--token <token>`，不得在输出中泄露 token。

Windows PowerShell 中，CLI 路径包含空格时使用调用运算符：

```powershell
& "D:\path with spaces\.aibridge\cli\AIBridgeCLI.exe" runtime status --transport http --url http://127.0.0.1:27182 --raw
```

## 连接工作流

### HTTP transport

已知地址时优先直接检查状态：

```bash
AIBridgeCLI runtime status --transport http --url http://127.0.0.1:27182 --raw --timeout 10000
```

地址未知时扫描局域网，再使用返回的 `reachableUrl`：

```bash
AIBridgeCLI runtime discover --raw --timeout 5000
AIBridgeCLI runtime status --transport http --url http://192.168.1.10:27182 --raw
```

### File transport

先列出 target，再选择明确的 target。独立 Player 的 Runtime 目录通常位于 `Application.persistentDataPath/.aibridge/runtime`，不要默认使用 Unity 项目下的 `.aibridge/runtime`。

```bash
AIBridgeCLI runtime list_targets --runtime-dir <runtime-dir> --raw
AIBridgeCLI runtime status --transport file --runtime-dir <runtime-dir> --target <target-id> --raw
```

只有在 `status` 返回 `ready: true` 且 target、平台、场景符合预期后，才继续执行后续命令。

## 常用命令

```bash
# 健康检查与状态
AIBridgeCLI runtime ping --transport http --url <url> --raw
AIBridgeCLI runtime status --transport http --url <url> --raw

# 日志
AIBridgeCLI runtime logs --transport http --url <url> --logType Error --count 100 --includeStackTrace true --raw
AIBridgeCLI runtime logs_clear --transport http --url <url> --raw

# 性能与能力
AIBridgeCLI runtime perf --transport http --url <url> --durationMs 5000 --intervalMs 100 --hitchThresholdMs 50 --raw --timeout 15000
AIBridgeCLI runtime handlers --transport http --url <url> --raw

# 截图
AIBridgeCLI runtime screenshot --transport http --url <url> --raw --timeout 15000
```

`runtime perf` 是轻量级摘要采样，不等同于 Unity Profiler。

## Runtime UGUI

按“快照或查找 → raycast → 点击 → 截图或状态验证”的顺序操作：

```bash
AIBridgeCLI runtime ui_snapshot --transport http --url <url> --maxResults 100 --includeDisabled true --raw
AIBridgeCLI runtime ui_find --transport http --url <url> --keyword Start --maxResults 100 --raw
AIBridgeCLI runtime ui_raycast --transport http --url <url> --path "Canvas/Button" --maxResults 20 --raw
AIBridgeCLI runtime ui_click --transport http --url <url> --path "Canvas/Button" --raw
```

也可使用 `--instanceId <id>`，或对 `ui_raycast`、`ui_click` 使用 `--x <x> --y <y>`。不要只凭命令成功判断 UI 已达到预期；继续获取截图或业务状态验证结果。

## 执行已编译 DLL

仅在用户明确授权运行代码、Runtime 开启代码执行能力且 DLL 来源可信时执行：

```bash
AIBridgeCLI runtime exec --dll probe.dll --transport http --url <url> --riskAccepted true --raw --timeout 30000
```

需要时添加 `--entryType Namespace.Type --methodName Method`。`runtime exec --code` 不受支持；不要为了绕过限制自动启动 Unity Editor。

## 故障判断

- `connection refused`：先判定 Player 或 HTTP endpoint 未运行，不要判定 CLI 功能损坏。
- `target_not_found`：检查 `--runtime-dir`、target 是否过期以及 Player 是否仍在运行。
- `401` 或鉴权失败：确认 `--token`，不要移除 Runtime 端鉴权。
- UI 找不到或无法点击：检查当前场景、Canvas、EventSystem、遮挡层和 raycast 结果。
- DLL 执行失败：分别确认代码执行开关、`riskAccepted`、HybridCLR/运行环境兼容性、入口类型和方法。

使用 `AIBridgeCLI runtime --help` 查询当前 CLI 支持的子命令；以实际 CLI 输出为准。

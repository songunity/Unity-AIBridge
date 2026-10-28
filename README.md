# AI Bridge

[English](./README_EN.md) | 中文

仓库测试：`./Tools~/Test.ps1`；包含隔离 Unity 的完整验证使用 `-IncludeEditor`，详见 [本地测试入口](./RELEASING.md#本地测试入口)。

AI 编码助手与 Unity Editor 之间的文件通信框架。

## 核心亮点：动态执行 C# 代码

AI Bridge 最强大的能力是让 AI 助手**直接在 Unity 中执行任意 C# 代码**——不需要创建脚本文件、不需要等待编译、不需要重启 Play 模式。

```bash
# AI 助手可以直接执行任何 Unity API
AIBridgeCLI CodeExecuteCommand_Execute --code '
using UnityEngine;
var go = GameObject.Find("Player");
go.GetComponent<Rigidbody>().mass = 5f;
return go.transform.position;
'
```

这意味着：
- **无限扩展性** — 即使没有预置命令，AI 也能通过代码完成任何操作
- **快速原型验证** — 无需创建 MonoBehaviour，直接执行逻辑片段
- **运行时调试** — Play 模式下实时查询和修改游戏状态
- **复杂操作** — 批量修改材质、生成程序化内容、调用项目自定义 API

较长的代码可以保存到 `.aibridge/code/` 目录，通过 `--file` 参数执行。

## 功能特性

- **GameObject** - 创建、删除、查找、重命名、复制、切换激活状态
- **Transform** - 位置、旋转、缩放、父子层级、LookAt
- **Component/Inspector** - 获取/设置属性、添加/移除组件、搜索属性
- **Scene** - 加载、获取层级树、获取活动场景
- **Prefab** - 实例化、保存、解包、应用覆盖、获取层级
- **Asset** - 搜索、刷新、按过滤器查找
- **编辑器控制** - 播放/暂停/停止、编译、获取状态、日志输出
- **截图 & GIF** - 捕获游戏视图（F12）、录制动画 GIF（F11），AsyncGPUReadback + 后台线程编码
- **输入模拟** - 点击、拖拽、长按 GameObject（运行时）
- **日志捕获** - 获取控制台日志、精确时间戳捕获模式
- **代码执行** - 在编辑器或运行时动态执行 C# 代码片段（意味着即使没有预置命令，也能通过代码实现任何 Unity API 操作）
- **Runtime Bridge** - 连接打包后的 Player，读取运行状态、日志、截图、性能数据，并在启用 Runtime 代码执行后发送已编译 DLL 执行
- **批量命令** - 单次调用执行多个命令
- **菜单项** - 执行任意 Unity 编辑器菜单项
- **测试运行** - 集成 Unity TestRunner API

## 架构

```
┌─────────────────┐         ┌──────────────────────────────────────┐
│   AI Assistant  │         │           Unity Editor               │
│  (Claude, etc.) │         │                                      │
└────────┬────────┘         │  ┌─────────────┐  ┌──────────────┐  │
         │                  │  │ CommandWatch │  │ CommandRegistry│ │
         │ 调用             │  │  (轮询扫描)  │  │  ([AIBridge]) │  │
         ▼                  │  └──────┬──────┘  └──────────────┘  │
┌─────────────────┐         │         │                            │
│   AIBridgeCLI   │         │         ▼                            │
│  (命令行工具)    │         │  ┌─────────────┐                    │
└────────┬────────┘         │  │ 执行命令并   │                    │
         │                  │  │ 写入结果     │                    │
         │ 写入 JSON        │  └─────────────┘                    │
         ▼                  └──────────────────────────────────────┘
┌──────────────────────────────────────────────────────────────────┐
│                    .aibridge/ (文件交换目录)                       │
│  ├── commands/    ← CLI 写入命令 JSON                            │
│  ├── results/     ← Unity 写入结果 JSON                          │
│  ├── code/        ← C# 脚本文件                                  │
│  ├── screenshots/ ← 截图和 GIF                                   │
│  └── cli/         ← CLI 可执行文件                                │
└──────────────────────────────────────────────────────────────────┘
```

## 为什么选择 AI Bridge？（对比 Unity MCP）

| 特性         | AI Bridge    | Unity MCP        |
| ------------ | ------------ | ---------------- |
| 通信方式     | 文件通信     | WebSocket 长连接 |
| Unity 编译时 | **正常工作** | 连接断开         |
| 端口冲突     | 无           | 可能导致重连失败 |
| 多工程支持   | **支持**     | 不支持           |
| 稳定性       | **高**       | 受编译/重启影响  |
| 上下文消耗   | **低**       | 较高             |
| 扩展性       | 简单接口     | 需了解 MCP 协议  |

**MCP 的问题**：Unity MCP 使用 WebSocket 长连接。当 Unity 重新编译时（开发过程中频繁发生），连接会断开。端口冲突还可能导致无法重连，使用体验较差。

**AI Bridge 方案**：通过文件通信，AI Bridge 从根源上完美解决了这些问题。命令以 JSON 文件写入，结果以文件读取——简单、稳定、可靠，不受 Unity 状态影响。

## 安装

### 通过 Unity Package Manager

1. 打开 Unity Package Manager（Window > Package Manager）
2. 点击 "+" > "Add package from git URL"
3. 输入：`https://github.com/songunity/Unity-AIBridge.git`

### 手动安装

1. 下载或克隆此仓库
2. 将整个文件夹复制到 Unity 项目的 `Packages` 目录

## 系统要求

- Unity 2021.3 或更高版本

> **无外部依赖**：CLI 使用 AOT（Native AOT）编译为独立可执行文件，不需要 .NET Runtime。JSON 序列化使用内置的 Newtonsoft.Json（随 CLI 打包），不会与项目中的 DLL 冲突。

## 快捷键

| 快捷键 | 功能 | 条件 |
|--------|------|------|
| F12 | 截图 Game View | Play 模式 |
| F11 | 开始/停止 GIF 录制 | Play 模式 |

菜单路径：`Tools > AIBridge`

## 快速开始

### 0. 首次安装配置

安装 AI Bridge 后，需要进行以下初始化步骤：

1. **打开设置窗口**：`Window > AIBridge`
2. **安装 Skill 到 Agent**：切换到 `Tools` 标签，点击 **"Copy To Agent"** 按钮，将 Skill 文档安装到 agent的skills 目录
3. **配置自动扫描**（可选）：
   - 切换到 `Commands` 标签
   - 启用 **"Auto Scan on Startup"** 选项
   - 在 **"Scan Assemblies"** 文本框中配置要扫描的程序集（默认：`Assembly-CSharp-Editor-firstpass;Assembly-CSharp`）
   - 如果你的自定义命令在其他程序集中，需要添加到此列表，多个程序集用分号分隔

**注意：** 如果包安装在 `Library/PackageCache`（不可修改），则自动扫描会被强制启用。

### 1. 添加自定义命令

创建一个静态类，使用 `[AIBridge]` 特性标记方法：

```csharp
using AIBridge.Editor;
using System.Collections;
using System.ComponentModel;

public static class MyCustomCommand
{
    [AIBridge("创建一个具有特定设置的自定义立方体")]
    public static IEnumerator CreateCustomCube(
        [Description("立方体名称")] string name = "CustomCube",
        [Description("立方体大小")] float size = 1.0f)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.localScale = Vector3.one * size;

        yield return CommandResult.Success($"创建了 {name}，大小为 {size}");
    }
}
```

**关键要点：**

- 方法必须是 `static` 并返回 `IEnumerator`
- 可以使用yield return new WaitForSeconds或者yield return new WaitUntil
- 使用 `[AIBridge]` 特性添加描述
- 使用 `[Description]` 为参数添加文档（可选，不写则的字段名）
- 返回 `CommandResult.Success()` 或 `CommandResult.Failure()`

### 2. 刷新命令列表

添加自定义命令后，需要重新扫描并生成 Skill 文档：

1. 打开 `Window > AIBridge` 窗口
2. 切换到 `Commands` 标签
3. 点击 **"Refresh Command List"** 按钮（如果启用了 Auto Scan，此按钮会隐藏，命令会自动扫描）

**这个操作会：**
- 扫描指定程序集中的所有命令
- 更新命令注册表
- 自动重新生成 `Skill~/aibridge/SKILL.md` 的 Editor 命令清单
- 自动更新已安装的 Agent Skill 文档

**重要：** 每次添加或修改自定义命令后，都需要点击此按钮来更新 Skill 文档，这样 AI 助手才能识别你的新命令。

### 3. 使用命令

使用 CLI 工具或让 AI 助手调用你的命令：

```bash
AIBridgeCLI.exe MyCustomCommand_CreateCustomCube --name "MyCube" --size 2.0
```

### 4. 使用 Runtime Bridge

Runtime Bridge 主要用于连接**打包后的 Player**。它适合调试真机包、独立 Player，以及 Editor 命令无法直接看到的打包后运行时数据。

Unity Editor 下的 Play Mode 仍然推荐使用原来的 `CodeExecuteCommand_Execute` 和 Editor 命令；只有需要模拟或验证打包后 Runtime Bridge 链路时，才需要在 Play Mode 中创建 `AIBridgeRuntime`。

常见用途：

- 查看 Runtime 目标状态、当前场景、平台、HTTP 入口和能力
- 读取 Player 内存日志，或清理 Runtime 日志缓存
- 截取 Player 运行时截图
- 采样 FPS、帧耗时等性能数据
- 基于 HybridCLR，在显式启用后把自己组织并编译好的 DLL 发送到 Runtime 执行

默认情况下，**Compile Runtime Bridge 是关闭的**。如需使用：

1. 打开 `Window > AIBridge > Runtime`
2. 勾选 **Compile Runtime Bridge**
3. 按需要配置 HTTP、Token、Allowed Actions 等选项
4. 打开 `Window > AIBridge > Players` 查看 File Transport / HTTP / LAN 发现到的 Runtime targets

启用 **Compile Runtime Bridge** 后，构建 Player 时会自动注入并创建 `AIBridgeRuntime`；关闭后不编译、不注入。Editor Play Mode 如需调试 Runtime Bridge 链路，可以在 Runtime 面板中手动创建或应用场景 Runtime 对象。

Runtime 代码执行依赖 HybridCLR。调用方可以自行组织 C# 代码、编译成 DLL，再通过 Runtime Bridge 发送到 Player 中加载执行；`runtime exec --dll` 只负责发送已编译 DLL，不负责在 CLI 侧编译源码。

常用 CLI：

```bash
# 列出本机 Runtime targets
AIBridgeCLI.exe runtime list_targets --raw

# 扫描局域网 Runtime targets
AIBridgeCLI.exe runtime discover --udpPort 27183 --raw

# 获取 Runtime 状态
AIBridgeCLI.exe runtime status --transport file --target latest --raw
AIBridgeCLI.exe runtime status --transport http --url http://127.0.0.1:27182 --raw

# 读取 Runtime 日志
AIBridgeCLI.exe runtime logs --transport file --target latest --logType Error --count 100 --raw

# 截取 Runtime 截图
AIBridgeCLI.exe runtime screenshot --transport file --target latest --raw

# 执行已编译 DLL（需要 HybridCLR 和启用 Runtime 代码执行）
AIBridgeCLI.exe runtime exec --dll probe.dll --transport http --url http://127.0.0.1:27182 --riskAccepted true --raw
```

## 命令注册

### 自动扫描模式

在 `Window > AIBridge` 的 `Commands` 标签中启用 **"Auto Scan on Startup"**：

- Unity 启动时会自动扫描并注册命令
- 在 **"Scan Assemblies"** 文本框中指定要扫描的程序集（默认：`Assembly-CSharp-Editor-firstpass;Assembly-CSharp`）
- 多个程序集用分号（`;`）分隔
- 如果你的自定义命令在其他程序集中（如 `MyCustomCommands`），需要添加到列表中：`Assembly-CSharp-Editor-firstpass;Assembly-CSharp;MyCustomCommands`

**注意：** 如果包安装在 `Library/PackageCache`（不可修改），则自动扫描会被强制启用。

### 手动刷新模式

如果禁用自动扫描：

1. 命令将从内置的 `CommandRegistry.AutoRegister()` 方法注册
2. 添加新命令后，需要手动在设置窗口的 `Commands` 标签点击 **"Refresh Command List"** 按钮
3. 这会重新扫描所有程序集并更新 Skill 文档

## Skill 文档

包内提供两个独立 Skill：

- `Skill~/aibridge/SKILL.md`：连接并自动化已打开的 Unity Editor，包括 Editor Play Mode。
- `Skill~/aibridge-runtime/SKILL.md`：无需 Unity Editor，直接连接正在运行的 Player Runtime Bridge。

`aibridge` Skill 的命令清单会自动生成。可以编辑其他内容，但不要修改 `<!-- AUTO-GENERATED-COMMANDS-START -->` 与 `<!-- AUTO-GENERATED-COMMANDS-END -->` 之间的内容。Runtime Skill 根据独立 `runtime` CLI 命令面维护，不参与 Editor 命令扫描。

### 安装 Skill 到 Agent 目录

**首次安装（必需）：**

1. 打开 `Window > AIBridge` 窗口
2. 切换到 `Tools` 标签
3. 点击 **"Copy To Agent"** 按钮

**复制逻辑：**
- 系统会先扫描项目根目录中已存在的 AI 编辑器目录（`.agents`、`.cursor`、`.factory`、`.claude`、`.codex` 等）
- 如果找到任何已存在的目录，会分别复制到 `skills/aibridge/` 和 `skills/aibridge-runtime/`
- 如果没有找到任何 AI 编辑器目录，会自动创建 `.agents` 目录并复制两个 Skill

**示例：**
- 如果项目中已有 `.factory` 目录，两个 Skill 会被复制到 `.factory/skills/aibridge/` 和 `.factory/skills/aibridge-runtime/`
- 如果项目中同时有 `.factory` 和 `.cursor` 目录，两个目录都会被更新
- 如果项目中没有任何 AI 编辑器目录，会创建 `.agents/skills/` 下的两个 Skill

### 更新 Skill 文档

当你添加或修改自定义命令后：

**方法 1：自动更新（推荐）**
- 在 `Commands` 标签点击 **"Refresh Command List"** 按钮
- 这会自动重新生成 Skill 文档并更新所有已安装的 Agent 目录

**方法 2：手动更新**
- 在 `Tools` 标签点击 **"Generate Skill"** 按钮重新生成文档
- 然后点击 **"Copy To Agent"** 按钮更新 Agent 目录

**使用方法：** Editor 操作使用 `aibridge`；独立 Player Runtime Bridge 操作使用 `aibridge-runtime`。

## 许可证

MIT License

## 贡献

欢迎贡献！请随时提交 Pull Request。

### Editor C# 编译缓存

`CodeExecuteCommand_Execute` 在当前程序集域内复用相同代码的编译入口，每次仍重新执行代码，返回值和异步任务不复用；日志按请求执行期间收集，并发请求的日志来源尚未隔离。缓存以原始代码为键，命中时不再包装和解析代码。程序集加载时立即检查引用变化；同路径 DLL 的文件变化最多一秒后复核，程序集重载后缓存清空。

缓存最多保留 64 个入口。达到上限后，按 FIFO 淘汰最早加入的入口并缓存新代码；清空字典不会卸载 Mono 已加载的动态程序集。此缓存不作用于远程 Player 的 `CompileToBytes` 路径。

通过 `AIBridgeCLI CodeExecuteCommand_CacheStatus --raw` 查询当前缓存条目、命中次数、编译次数与引用重建次数。代码结果使用协议 2 的 `data.returnValue` 和 `data.logs`。

异步等待使用真实经过时间计时，保留 120 秒上限；超时表示停止等待，不代表用户任务已被取消。

### 可选诊断 Skill

通过 `Window/AIBridge/Install Profiler Skills` 安装 `unity-profiler`、`frame-debugger`、`memory-profiler`、`yooasset-debugger`。它们与基础桥接 Skill 分开加载；安装器同时复制脚本、公共参数入口和结果处理代码，不会自动安装 Memory Profiler 或 YooAsset 包。

在安装后的技能目录执行 `python scripts/run.py <脚本名> --cli <目标项目CLI绝对路径> --options-file <参数JSON>`。诊断入口提交一次后按原 ID 等待，不重放超时采集；返回明确状态、摘要、采样上下文和完整数据文件。详细用法见各技能的 `SKILL.md`。仓库内开发时可用 `python Skill~/diagnostics-common/run.py <脚本名> --scripts Skill~/<技能名>/scripts --cli <CLI路径>`。

Memory 分析以官方 1.1.0 源码适配，快照依赖、采集选项、引用查询深度及兼容限制均在技能中明确说明。Frame Debugger 不提供逐事件 GPU 时间；Profiler 的已录制调用栈与 Flow 不代表所有线程因果关系完整。

### 调度、帮助与耗时

Editor 用文件通知唤醒命令检查，空闲时每 100ms 补扫，收到命令后的一秒内每 10ms 检查；实际频率仍受 Editor 更新频率和主线程负载限制。CLI 等待结果使用文件通知，并以 500ms 轮询补偿丢失的通知；通知不可用时回退到 50ms 轮询。同步命令当次推进，Batch 在 8ms 时间预算内连续执行；单条同步代码不可抢占，异步子项仍完成后才执行下一项。

`Help --command <名称> --raw` 优先读取匹配 Editor 会话的 `.aibridge/command-catalog.json`。`--offline` 禁止提交 Editor 命令；Editor 关闭后可读取上次目录，`data.helpCatalog` 标注来源会话、生成时间和是否存在会话元数据。没有目录或会话不匹配时明确失败，升级后需由匹配 Editor 重新生成；扫描命令注册表时也会更新目录。

耗时字段可区分请求发现、排队、协程调度和主动执行：`discoveryMs`、`queuedMs`、`dispatchMs`、`activeMs`；`executionMs` 包含 yield/异步等待。`cliWaitMs` 为提交加等待总时间，`cliSubmitMs` 为提交部分，`resultDeliveryMs` 包含结果序列化、发布重试及 CLI 观察。代码结果额外提供 `initializationMs`、`referenceMs`、`compileMs`，Compile 提供全程 `compileTotalMs`。父 Batch 的主动耗时不累计异步子协程所有帧的执行时间；这些字段不能直接当作 CPU Profiler 数据。

编译诊断返回错误编号、原始探针行列和相关代码行。项目/CLI 路径错误或心跳过期时，先检查现场；不要重复提交相同帮助或写操作。

## Editor 协议 2：调用迁移

本次是接口变更，Editor 包与 CLI 必须成套升级；Runtime Bridge 协议不变。

- 代码执行直接返回 `data.returnValue` 与 `data.logs`，删除 `ReturnValue/Output` 文本。探针直接返回数据对象，不再拼接 `HARNESS_RESULT:` 或预先序列化 JSON。
- 日志读取 `level/message/stackTrace`，不要在业务字符串中搜索异常词。日志范围是执行期间，尚未隔离并发来源。
- Batch 默认遇错停止，任一子项失败使外层失败并返回非零退出码；`stopOnError=false` 仅允许继续余项，不改变失败结论。未执行项为 skipped。
- `--no-wait` 提交后用 `command status --id <id>`、`command result --id <id> [--wait --timeout <ms>]` 查询。等待超时不取消、不删除、不重放命令；保留结果可重复读取 10 分钟。
- `executionTimeoutMs` 默认 120000，仅控制代码异步结果等待。超时不取消底层 Task，无法确认的最终业务结果不可通过重新执行写操作来确认。
- 新字段包含 `protocolVersion=2`、`errorCode`、`status` 和 `timings`。命令完成与业务成功分别验证；unknown 不代表未执行。
- 状态文件被占用时保留命令，在后续 Editor 更新中重试；执行前持续失败超过 2 秒，返回 `STATUS_WRITE_FAILED`，明确该命令未执行。执行后的结果发布失败只重试发布，最多保留 10 分钟，不重放命令；CLI 等待超时仍应查询原 ID。文件读取方应允许 `FileShare.Delete`，不要先删除旧状态文件绕过原子替换。

当前 mildSLG 需在下载新版本时同步迁移 Harness 驱动、内嵌探针与用例、GM 玩家 ID 读取以及蓝湖模板执行器日志判断；旧运行报告无需迁移。新版本发布前不替换该项目包和脚本。

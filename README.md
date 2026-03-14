# AI Bridge

[English](./README_EN.md) | 中文

AI 编码助手与 Unity Editor 之间的文件通信框架。

## 功能特性

- **GameObject** - 创建、删除、查找、重命名、复制、切换激活状态
- **Transform** - 位置、旋转、缩放、父子层级、LookAt
- **Component/Inspector** - 获取/设置属性、添加/移除组件
- **Scene** - 加载、保存、获取层级、创建新场景
- **Prefab** - 实例化、保存、解包、应用覆盖
- **Asset** - 搜索、导入、刷新、按过滤器查找
- **编辑器控制** - 编译、撤销/重做、播放模式、聚焦窗口
- **截图 & GIF** - 捕获游戏视图、录制动画 GIF
- **批量命令** - 高效执行多个命令
- **代码执行** - 在编辑器或运行时动态执行 C# 代码

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
3. 输入：`https://github.com/wang-er-s/AIBridge.git`

### 手动安装

1. 下载或克隆此仓库
2. 将整个文件夹复制到 Unity 项目的 `Packages` 目录

## 系统要求

- Unity 2021.3 或更高版本
- .NET 6.0 Runtime（用于 CLI 工具）
- Newtonsoft.Json (com.unity.nuget.newtonsoft-json)

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
- 自动重新生成 `Skill~/SKILL.md` 文档
- 自动更新已安装的 Agent Skill 文档

**重要：** 每次添加或修改自定义命令后，都需要点击此按钮来更新 Skill 文档，这样 AI 助手才能识别你的新命令。

### 3. 使用命令

使用 CLI 工具或让 AI 助手调用你的命令：

```bash
AIBridgeCLI.exe MyCustomCommand_CreateCustomCube --name "MyCube" --size 2.0
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

`Skill~/SKILL.md` 文件是为 AI 助手（如 Droid、Claude、GPT 等）自动生成的文档。包含：

- 所有已注册命令的名称和描述（内置 + 自定义）
- 每个命令的参数详情（类型和描述）
- 使用示例
- CLI 语法

你可以自己添加需要的内容，但是不要在 <!-- AUTO-GENERATED-COMMANDS-START --> <!-- AUTO-GENERATED-COMMANDS-END -->之中添加

### 安装 Skill 到 Agent 目录

**首次安装（必需）：**

1. 打开 `Window > AIBridge` 窗口
2. 切换到 `Tools` 标签
3. 点击 **"Copy To Agent"** 按钮

**复制逻辑：**
- 系统会先扫描项目根目录中已存在的 AI 编辑器目录（`.cursor`、`.agent`、`.factory`、`.claude`、`.codex` 等）
- 如果找到任何已存在的目录，会将 Skill 文档复制到这些目录的 `skills/aibridge/` 子目录中
- 如果没有找到任何 AI 编辑器目录，会自动创建 `.agent` 目录并复制 Skill 文档

**示例：**
- 如果项目中已有 `.factory` 目录，Skill 会被复制到 `.factory/skills/aibridge/SKILL.md`
- 如果项目中同时有 `.factory` 和 `.cursor` 目录，两个目录都会被更新
- 如果项目中没有任何 AI 编辑器目录，会创建 `.agent/skills/aibridge/SKILL.md`

### 更新 Skill 文档

当你添加或修改自定义命令后：

**方法 1：自动更新（推荐）**
- 在 `Commands` 标签点击 **"Refresh Command List"** 按钮
- 这会自动重新生成 Skill 文档并更新所有已安装的 Agent 目录

**方法 2：手动更新**
- 在 `Tools` 标签点击 **"Generate Skill"** 按钮重新生成文档
- 然后点击 **"Copy To Agent"** 按钮更新 Agent 目录

**使用方法：** AI 助手会自动读取 `.factory/skills/aibridge/SKILL.md` 文件，从而识别所有可用的 Unity Editor 控制命令。

## 许可证

MIT License

## 贡献

欢迎贡献！请随时提交 Pull Request。

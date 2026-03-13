# AI Bridge

[English](./README.md) | 中文

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
- 使用 `[AIBridge]` 特性添加描述
- 使用 `[Description]` 为参数添加文档
- 返回 `CommandResult.Success()` 或 `CommandResult.Failure()`

### 2. 扫描命令

打开 `AIBridge/Settings` 窗口（菜单：`AIBridge/Settings`），点击 **"Scan Commands"** 按钮来注册你的自定义命令。

### 3. 生成 Skill 文档

在同一设置窗口中，点击 **"Generate Skill"** 按钮，自动生成 `Skill~/SKILL.md` 文档供 AI 助手使用。

**重要：** 生成 Skill 文档时，会自动扫描并包含所有已注册命令的完整描述、参数说明和使用示例，包括内置命令和你的自定义命令。

### 4. 使用命令

使用 CLI 工具或让 AI 助手调用你的命令：

```bash
AIBridgeCLI.exe MyCustomCommand_CreateCustomCube --name "MyCube" --size 2.0
```

## 命令注册

### 自动扫描模式

在 `AIBridge/Settings` 中启用 **"Auto Scan on Startup"**，Unity 启动时会自动扫描并注册命令。你可以指定要扫描的程序集（默认：`Assembly-CSharp-Editor-firstpass;Assembly-CSharp`）。

**注意：** 如果包安装在 `Library/PackageCache`（不可修改），则强制启用自动扫描。

### 手动注册

如果禁用自动扫描，命令将从内置的 `CommandRegistry.AutoRegister()` 方法注册。添加新命令后，在设置窗口点击 **"Scan Commands"**。

## Skill 文档

`Skill~/SKILL.md` 文件是为 AI 助手（如 Claude、GPT 等）自动生成的文档。包含：

- 所有已注册命令的名称和描述（内置 + 自定义）
- 每个命令的参数详情（类型和描述）
- 使用示例
- CLI 语法

**重新生成：** 添加或修改命令后，在 `AIBridge/Settings` 中点击 **"Generate Skill"**，系统会自动扫描所有命令并生成完整文档。

**使用方法：** 将 `Skill~/SKILL.md` 分享给你的 AI 助手，以启用 Unity Editor 控制功能。详细的命令参考和示例请查看该文件。

## 安装 Skill 到 AI 编辑器

为了让 Claude Code 或 Cursor 自动识别 AIBridge Skill，你可以通过菜单一键安装：

### 安装到 Claude Code

菜单：`AIBridge/Install to Claude Code (Symlink)`

这会在项目根目录创建 `.claude/skills/aibridge` 符号链接，指向 `Skill~/SKILL.md`。

### 安装到 Cursor

菜单：`AIBridge/Install to Cursor (Symlink)`

这会在项目根目录创建 `.cursor/skills/aibridge` 符号链接，指向 `Skill~/SKILL.md`。

**注意：**
- 使用符号链接方式，当你重新生成 Skill 文档时，AI 编辑器会自动获取最新版本
- Windows 系统创建符号链接可能需要管理员权限
- 如果符号链接创建失败，会自动回退到文件复制模式

## 许可证

MIT License

## 贡献

欢迎贡献！请随时提交 Pull Request。

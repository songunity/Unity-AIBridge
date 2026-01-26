# AI Bridge

[English](./README.md) | 中文

AI 编码助手与 Unity Editor 之间的文件通信框架。

## 概述

AI Bridge 使 AI 编码助手（如 Claude、GPT 等）能够通过简单的基于文件的协议与 Unity Editor 进行通信。这使得 AI 能够：

- 创建和操作 GameObject
- 修改 Transform 和 Component
- 加载和保存场景
- 捕获截图和 GIF 录制
- 执行菜单项
- 以及更多功能...

## 安装

### 通过 Unity Package Manager

1. 打开 Unity Package Manager（Window > Package Manager）
2. 点击 "+" > "Add package from git URL"
3. 输入：`https://github.com/liyingsong99/AIBridge.git`

### 手动安装

1. 下载或克隆此仓库
2. 将整个文件夹复制到 Unity 项目的 `Packages` 目录

## 系统要求

- Unity 2021.3 或更高版本
- .NET 6.0 Runtime（用于 CLI 工具）
- Newtonsoft.Json (com.unity.nuget.newtonsoft-json)

## 包结构

```
cn.lys.aibridge/
├── package.json
├── README.md
├── README_CN.md
├── Editor/
│   ├── cn.lys.aibridge.Editor.asmdef
│   ├── Core/
│   │   ├── AIBridge.cs              # 主入口点
│   │   ├── CommandWatcher.cs        # 命令文件监视器
│   │   └── CommandQueue.cs          # 命令处理队列
│   ├── Commands/
│   │   ├── ICommand.cs              # 命令接口
│   │   ├── CommandRegistry.cs       # 命令注册表
│   │   └── ...                      # 各种命令实现
│   ├── Models/
│   │   ├── CommandRequest.cs        # 请求模型
│   │   └── CommandResult.cs         # 结果模型
│   └── Utils/
│       ├── AIBridgeLogger.cs        # 日志工具
│       └── ComponentTypeResolver.cs  # 组件类型解析器
├── Runtime/
│   ├── cn.lys.aibridge.Runtime.asmdef
│   ├── AIBridgeRuntime.cs           # 运行时单例 MonoBehaviour
│   ├── AIBridgeRuntimeData.cs       # 运行时数据类
│   └── IAIBridgeHandler.cs          # 扩展接口
└── Tools~/
    ├── CLI/
    │   └── AIBridgeCLI.exe          # 命令行工具
    ├── AIBridgeCLI/                 # CLI 源代码
    └── Exchange/
        ├── commands/                # 命令文件写入此处
        ├── results/                 # 结果文件返回此处
        └── screenshots/             # 截图保存此处
```

## 使用方法

### 编辑器模式

AI Bridge 在 Unity Editor 打开时自动启动。命令从 `Tools~/Exchange/commands/` 目录处理。

#### 菜单项
- `AIBridge/Process Commands Now` - 立即处理待处理的命令
- `AIBridge/Toggle Auto-Processing` - 启用/禁用自动命令处理

### CLI 工具

CLI 工具（`AIBridgeCLI.exe`）提供命令行接口用于发送命令。

```bash
# 显示帮助
AIBridgeCLI --help

# 发送日志消息
AIBridgeCLI editor log --message "Hello from AI!"

# 创建 GameObject
AIBridgeCLI gameobject create --name "MyCube" --primitiveType Cube

# 设置 Transform 位置
AIBridgeCLI transform set_position --path "MyCube" --x 1 --y 2 --z 3

# 获取场景层级
AIBridgeCLI scene get_hierarchy

# 捕获截图
AIBridgeCLI screenshot game

# 录制 GIF
AIBridgeCLI screenshot gif --frameCount 60 --fps 20
```

### 可用命令

| 命令 | 描述 |
|------|------|
| `editor` | 编辑器操作（日志、撤销、重做、播放模式等） |
| `compile` | 编译操作（unity、dotnet） |
| `gameobject` | GameObject 操作（创建、销毁、查找等） |
| `transform` | Transform 操作（位置、旋转、缩放、父级） |
| `inspector` | Component/Inspector 操作 |
| `selection` | 选择操作 |
| `scene` | 场景操作（加载、保存、层级） |
| `prefab` | 预制体操作（实例化、保存、解包） |
| `asset` | AssetDatabase 操作 |
| `menu_item` | 调用 Unity 菜单项 |
| `get_logs` | 获取 Unity 控制台日志 |
| `batch` | 执行多个命令 |
| `screenshot` | 捕获截图和 GIF 录制 |
| `focus` | 将 Unity Editor 置于前台（仅 CLI） |

### 运行时扩展

若需运行时（Play 模式）支持，在场景中添加 `AIBridgeRuntime` 组件：

```csharp
// 方式 1：通过代码添加
if (AIBridgeRuntime.Instance == null)
{
    var go = new GameObject("AIBridgeRuntime");
    go.AddComponent<AIBridgeRuntime>();
}

// 方式 2：通过 Inspector 添加
// 创建空 GameObject 并添加 AIBridgeRuntime 组件
```

#### 实现自定义处理器

```csharp
using AIBridge.Runtime;

public class MyCustomHandler : IAIBridgeHandler
{
    public string[] SupportedActions => new[] { "my_action", "another_action" };

    public AIBridgeRuntimeCommandResult HandleCommand(AIBridgeRuntimeCommand command)
    {
        switch (command.Action)
        {
            case "my_action":
                // 处理命令
                return AIBridgeRuntimeCommandResult.FromSuccess(command.Id, new { result = "success" });

            case "another_action":
                // 处理另一个命令
                return AIBridgeRuntimeCommandResult.FromSuccess(command.Id);

            default:
                return null; // 未处理
        }
    }
}

// 注册处理器
AIBridgeRuntime.Instance.RegisterHandler(new MyCustomHandler());
```

## 命令协议

命令是放置在 `Tools~/Exchange/commands/` 中的 JSON 文件：

```json
{
    "id": "cmd_123456789",
    "type": "gameobject",
    "params": {
        "action": "create",
        "name": "MyCube",
        "primitiveType": "Cube"
    }
}
```

结果返回在 `Tools~/Exchange/results/` 中：

```json
{
    "id": "cmd_123456789",
    "success": true,
    "data": {
        "name": "MyCube",
        "instanceId": 12345,
        "path": "MyCube"
    },
    "executionTime": 15
}
```

## 许可证

MIT License

## 贡献

欢迎贡献！请随时提交 Pull Request。

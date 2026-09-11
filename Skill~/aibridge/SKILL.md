---
name: aibridge
description: 通过 AI Bridge CLI 操作已打开的 Unity Editor、场景和资源，执行 C#、控制 Play Mode、检查和点击 UI、截图。独立 Player 使用 aibridge-runtime。
---

# AI Bridge

## 连接与命令选择

- 定位 Unity 项目根（含 Assets 和 ProjectSettings），使用 `.aibridge/cli/AIBridgeCLI.exe`；macOS/Linux 无 `.exe`。Editor 与独立 Player 的连接链路分开。
- 每次使用 `--raw`。先记录当前 Editor 状态，默认附加现有会话，定位已有对象；仅在任务需要时进入或退出 Play Mode，不机械创建 Canvas、Button 或重启游戏。
- 首次使用不熟悉的命令，通过 `Help --command <命令>` 查参数；同版本同会话复用已查帮助。`Compile --raw --timeout 300000` 直接调用即可，其结果已包含编译错误。
- 相关只读查询合并到 Batch 或同一 C# 探针中。有依赖的操作按顺序执行，等待真正的业务就绪条件。
- 遵循项目指令中的资源修改、账号、测试数据及现场保留约定；业务验证交给项目测试/GM 流程，不用客户端假数据替代真实服务端交互。

## 参数与结果

PowerShell 的带空格路径使用调用运算符：`& "<路径>/AIBridgeCLI.exe" ...`。短代码可用单引号；复杂 JSON 优先 `--stdin`，避免 Shell 引号和转义差异。长代码保存到 `.aibridge/code/`，通过 `--file <完整路径>` 执行。

代码只包含 using 与方法体逻辑。查询直接 `return` 基本类型、普通对象、数组或字典，不先 Debug.Log 再查控制台；Unity 对象应返回所需字段，不直接返回整个对象。

协议 2 的代码执行结果使用 `data.returnValue`、`data.logs`，没有旧的 `ReturnValue/Output` 文本和业务标记前缀。日志包含 `level/message/stackTrace`。外层 `success` 表示命令执行是否成功，业务返回值仍需按任务检查。

- 错误检查读取 `errorCode/error` 和日志级别，不在普通返回字符串中搜索 `Exception:`。
- `logScope=executionWindow` 表示捕获执行期间日志，并发来源尚未隔离；精确日志验证采用顺序操作。
- CLI 与 Editor 包必须同时升级；协议不匹配明确报错，不回退旧格式。

## 批量执行与长任务

`Batch` 接收 `commands` 数组，各项含 `type`、`params`。默认 `stopOnError=true`，任一失败使批次失败，余项标记 `skipped`。独立查询可显式设为 false 继续执行，但外层仍报告失败。检查每项结果，不把批次已结束当作业务全部通过。

Batch 顺序执行，不提供回滚或自动业务等待。打开页面的命令结束不等于异步加载完成；点击前检查目标条件。独立提交的命令可能交错执行，不等于 Unity 多线程操作。

- `--timeout` 是 CLI 等待结果的期限（默认 5000 毫秒）。代码命令的 `executionTimeoutMs` 是异步结果等待期限（默认 120000 毫秒），两者独立。
- `--no-wait` 返回命令 ID 和 submitted，仅说明已提交。随后使用 `command status --id <id> --raw` 或 `command result --id <id> --wait --timeout <ms> --raw`。
- CLI `WAIT_TIMEOUT` 不取消命令；先查询原 ID，禁止直接重发写操作。
- 代码异步等待超时也不取消底层 Task，不支持强制中止同步代码。此时业务最终结果需要另行观察，不能靠重放确认。
- 状态包括 submitted、queued、running、completed、failed、unknown。域重载或失去会话后的 unknown 不代表未执行；保留结果可重复读取，终态结果保留 10 分钟。

## UI 检查与验证

- 验证无法点击时优先 `UIAutomationCommand_Find/Raycast/Click`，核对实际 raycast 命中、事件目标、目标是否被遮挡及业务结果。直接调用函数或发送事件只能证明对应逻辑，不能证明用户能点到。
- `NoEventSystem` 先检查 Play Mode、场景加载和现有 EventSystem 是否启用，不直接添加组件改变现场。
- 实时同步测试保持页面打开，不主动重开页面或重新请求数据；刷新后的正确结果不能证明实时同步成功。
- 创建隔离测试 UI 只用于用户任务需要的本地显示/原型验证，不用它代替现有项目业务 UI。
- Play Mode 调整先记录原状态。用户要求先改运行实例时，先预览并截图；已授权修改资源时，再用同组参数同步 Prefab。
- 区分运行实例预览、Prefab 已保存和重新加载验证。最终资源验证需要重新加载并打开目标界面；用户要求暂不重启时，明确尚未验证重新加载，不擅自打断现场。

## 资源与编译

资源搜索使用 `AssetDatabaseCommand_Find`，具体过滤参数查 Help；加载资源数据通过 C# `UnityEditor.AssetDatabase.LoadAssetAtPath`。不要使用已失效的 AssetDatabaseCommand_Search/Load。

代码变化确需编译验证时调用 Compile；编译和程序集重载会影响会话及缓存，按项目测试流程统一安排，避免每个小查询都重新编译。

## 编译缓存与耗时

同一探针保持代码内容稳定，不拼入无意义时间戳。缓存仅复用编译入口，每次重新执行，返回值和 Task 不复用；程序集引用变化或域重载后失效。

`CodeExecuteCommand_CacheStatus` 按需查询，不在每条命令前后固定调用。区分提交次数、真实编译次数和命中数。结果中的排队、执行、CLI 等待及代码准备/异步等待耗时用于定位开销，不把局部加速当作整个业务流程加速。

<!-- AUTO-GENERATED-COMMANDS-START -->
## 命令分类

-- **Compile** - 编译代码，并返回编译结果，如果有报错是会直接返回，不需要再查看Log

### AssetDatabase

- **AssetDatabaseCommand_Find** - 通过 AssetDatabase 过滤器查找资源
- **AssetDatabaseCommand_Refresh** - 刷新资源数据库

### Batch

- **Batch** - 顺序执行命令，默认遇错停止；任一失败则批次失败，每项返回执行状态。

### CodeExecute

- **CodeExecuteCommand_CacheStatus** - 查询 Editor C# 编译缓存统计
- **CodeExecuteCommand_Execute** - 在 Unity Editor 执行 C# 方法体或文件，返回结构化 returnValue 和 logs。长代码使用 --file 完整路径。

### Editor

- **EditorCommand_GetState** - 获取当前编辑器状态（播放/暂停/编译状态）
- **EditorCommand_Log** - 向 Unity 控制台输出日志消息
- **EditorCommand_Pause** - 切换或设置暂停状态
- **EditorCommand_Play** - 进入播放模式
- **EditorCommand_Stop** - 退出播放模式

### GameObject

- **GameObjectCommand_Create** - 在场景中创建新的 GameObject
- **GameObjectCommand_Destroy** - 销毁 GameObject
- **GameObjectCommand_Find** - 在场景中查找GameObject
- **GameObjectCommand_GetInfo** - 获取 GameObject 的详细信息
- **GameObjectCommand_SetActive** - 设置 GameObject 的激活或非激活状态

### GetLogs

- **GetLogsCommand_StartCapture** - 开始捕获日志到缓冲区（精准模式），捕获的日志带毫秒级时间戳
- **GetLogsCommand_StopCapture** - 停止捕获日志，返回捕获的日志总数
- **Log** - 从 Unity 编辑器获取控制台日志

### Help

- **Help** - 获取特定命令的详细信息

### InputSimulation

- **InputSimulationCommand_Click** - 通过路径模拟点击 GameObject（仅 Editor Play Mode）
- **InputSimulationCommand_ClickAt** - 在屏幕坐标处模拟点击（仅 Editor Play Mode）
- **InputSimulationCommand_ClickByInstanceId** - 通过实例 ID 模拟点击 GameObject（仅 Editor Play Mode）
- **InputSimulationCommand_Drag** - 通过路径模拟从一个对象拖动到另一个对象（仅 Editor Play Mode）
- **InputSimulationCommand_DragByInstanceId** - 通过实例 ID 模拟从一个对象拖动到另一个对象（仅 Editor Play Mode）
- **InputSimulationCommand_LongPress** - 通过路径模拟长按 GameObject（仅 Editor Play Mode）
- **InputSimulationCommand_LongPressByInstanceId** - 通过实例 ID 模拟长按 GameObject（仅 Editor Play Mode）

### Inspector

- **InspectorCommand_AddComponent** - 向 GameObject 添加组件
- **InspectorCommand_GetComponents** - 获取 GameObject 上的所有组件
- **InspectorCommand_GetProperties** - 获取组件的序列化属性
- **InspectorCommand_RemoveComponent** - 从 GameObject 移除组件
- **InspectorCommand_SetProperty** - 设置组件上的序列化属性

### MenuItem

- **MenuItemCommand_Execute** - 通过路径执行 Unity 编辑器菜单项

### Prefab

- **PrefabCommand_Apply** - 将预制体实例的覆盖应用回预制体资源
- **PrefabCommand_GetInfo** - 获取资源或实例的预制体信息
- **PrefabCommand_Instantiate** - 在场景中实例化预制体
- **PrefabCommand_Save** - 将 GameObject 保存为预制体资源
- **PrefabCommand_Unpack** - 解包预制体实例

### Scene

- **SceneCommand_GetActive** - 获取当前激活场景的信息
- **SceneCommand_GetHierarchy** - 获取场景层级结构树
- **SceneCommand_Load** - 在编辑器中加载场景

### Screenshot

- **ScreenshotCommand_Gif** - 捕获多个截图并合成 GIF，至少需要 15 秒超时
- **ScreenshotCommand_Image** - 捕获 Game 视图的截图

### Selection

- **SelectionCommand_Clear** - 清除当前选择
- **SelectionCommand_Get** - 获取当前选择的GameObject
- **SelectionCommand_Set** - 设置当前选择的Object，可以同时传递多个参数

### Transform

- **TransformCommand_Get** - 获取 GameObject 的 Transform 数据
- **TransformCommand_LookAt** - 使 GameObject 朝向目标位置
- **TransformCommand_Reset** - 重置 Transform 为默认值
- **TransformCommand_SetParent** - 设置 GameObject 的父级
- **TransformCommand_SetPosition** - 设置 GameObject 的位置
- **TransformCommand_SetRotation** - Set rotation of a GameObject (Euler angles)
- **TransformCommand_SetScale** - 设置 GameObject 的缩放

<!-- AUTO-GENERATED-COMMANDS-END -->

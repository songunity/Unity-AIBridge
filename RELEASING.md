# 版本发布

发布由 `.github/workflows/build-cli.yml` 自动完成。推送版本 Tag 后，GitHub Actions 会构建所有平台的 CLI、生成 Unity Package 的 `.tgz` 文件，并创建或更新 GitHub Release。

## 版本规则

项目使用语义化版本号：

- 修复问题：递增补丁版本，例如 `1.3.0` → `1.3.1`
- 增加向后兼容功能：递增次版本，例如 `1.3.0` → `1.4.0`
- 包含不兼容修改：递增主版本，例如 `1.3.0` → `2.0.0`

Tag 支持 `v1.3.1` 和 `1.3.1` 两种格式。Tag 去掉可选的 `v` 前缀后，必须与 `package.json` 中的 `version` 完全一致。

## 发布步骤

以下命令以发布 `1.3.1` 为例，在仓库根目录执行。

1. 确认工作区状态并检查当前版本：

```powershell
git status --short
node -p "require('./package.json').version"
```

2. 将 `package.json` 中的 `version` 修改为新版本，然后验证包内容：

```powershell
$Version = "1.3.1"
$PackageVersion = node -p "require('./package.json').version"
if ($PackageVersion -ne $Version) { throw "package.json version mismatch" }

npm pack --dry-run
git diff --check
```

3. 提交并推送版本修改：

```powershell
$Version = "1.3.1"
git add package.json
git commit -m "build(release): 升级版本至 $Version"
git push origin main
```

4. 创建并推送版本 Tag：

```powershell
$Version = "1.3.1"
git tag -a "v$Version" -m "v$Version"
git push origin "v$Version"
```

不要在推送 Tag 后继续修改该 Tag 指向的提交。发布内容需要调整时，应发布新的补丁版本。

## 自动化产物

Tag 推送后，工作流会：

1. 构建 `win-x64`、`linux-x64`、`osx-x64` 和 `osx-arm64` CLI
2. 校验 Tag 与 `package.json` 版本一致
3. 使用 `npm pack` 生成 `com.sh.aibridge-<版本>.tgz`
4. 生成对应的 `com.sh.aibridge-<版本>.tgz.sha256`
5. 创建 GitHub Release；如果对应 Release 已存在，则覆盖上传同名产物

Unity 只需导入 `.tgz` 文件；`.sha256` 文件仅用于校验下载文件的完整性。

## 发布验证

```powershell
$Version = "1.3.1"
gh run list --workflow build-cli.yml --limit 5
gh release view "v$Version"
```

Release 页面应包含：

- `com.sh.aibridge-<版本>.tgz`
- `com.sh.aibridge-<版本>.tgz.sha256`

如果工作流因版本不一致失败，修正 `package.json` 后发布新的版本 Tag。不要移动已经推送或发布的 Tag。

## 协议 2 发布检查

发布前验证 CLI 测试、Skill 和四平台构建均通过；UPM 包必须包含同版本 Editor、CLI 和 Skill。发布说明标注不兼容的返回格式与 Batch 默认行为，并附 README 的脚本迁移说明。mildSLG 等下载正式包时再成套迁移调用脚本，不提前手工替换本地包。

## 本地测试入口

在仓库根目录运行，使用 PowerShell 7 和 .NET 10 SDK。

```powershell
# 快速检查：CLI 自动测试、Skill 校验、git diff --check，不启动 Unity。
./Tools~/Test.ps1

# 完整检查：额外构建 Windows AOT CLI、准备并启动隔离 Unity、执行协议回归。
./Tools~/Test.ps1 -IncludeEditor -UnityPath 'D:/Softwares-Dev/Unity/2022.3.62f3/Editor/Unity.exe'
```

`-UnityPath` 替换为本机安装路径。省略时先读取 `UNITY_EDITOR_PATH`，再尝试复用当前唯一运行的 Unity 安装路径；只读取安装位置，不操作其业务工程。完整检查还需要 Python 3、Unity 有效授权，以及 Windows Native AOT 所需的 Visual Studio C++ 工具链。当前隔离配置已在 Unity 2022.3.62f3 验证，其他版本不视为已验证。

入口自动完成：

1. 执行 CLI 测试、Skill 与差异检查；任何阶段失败返回非零退出码。
2. 在 `Temp/protocol-validation/UnityProject` 创建或复用空工程，复制当前仓库包，准备内置模块和 Test Framework 1.1.33，不读取业务工程配置。
3. 清除隔离副本中已从源码删除的非 meta 文件，保留 Library 缓存；拒绝覆盖含目录链接或正在运行的隔离工程。
4. 构建新 CLI，隐藏启动隔离 Unity，按本次进程 ID 等待协议 2 就绪，运行 `Tools~/ValidateEditorProtocol.py`。
5. 成功或失败后尝试关闭本次启动的隔离 Unity，保留工程与日志。无法正常关闭时报告 PID 和失败，不结束其他 Unity 进程。

首次运行需要还原依赖和导入空工程，后续复用缓存。启动等待默认 300 秒，可用 `-StartupTimeoutSeconds` 调整。发布、真实业务或当前游戏项目不会由此入口触发。

### 覆盖范围

| 层次 | 验证内容 |
|---|---|
| CLI 自动测试 | 参数绑定、JSON 类型、文件交接、重复 ID、结果保留、超时后查询、会话失效、Windows 原子替换读取；生产 CommandWatcher 的状态锁、输入删除失败、结果发布重试与重复完成回调 |
| 隔离 Unity | 真实 Roslyn 执行、结构化返回及异常日志、不支持返回值、缓存命中后返回新值、Batch 停止/继续/跳过、超时后取回结果、域重载未知状态、编译流程；真实拒绝删除共享的文件锁下恢复执行、持续锁明确失败、完成状态锁保留结果 |
| 额外检查 | 原始 JSON 错误输出、Runtime CLI 列表输出格式、逐条与 Batch 的只读查询计时 |

CLI 自动测试主要在临时文件目录验证通信边界，不能替代真实 Unity 执行。隔离测试也不验证真实业务服务器、UI 点击与遮挡、截图效果或 Player/真机通信；这些应按相关功能另行验证。协议脚本报告中的 `checks` 是命令交互数，不是独立测试用例数。

### 报告与失败排查

每次入口运行写入独立目录：`Temp/protocol-validation/runs/<时间戳>/`。

- `summary.json`：整体结果、各阶段退出码和耗时；完整测试包含 Unity 路径、PID、CLI SHA256 与关闭状态。
- `*.trx`、`cli-tests.log`：CLI 测试结果和构建输出。
- `publish-win-x64.log`、`editor.log`：AOT 构建及隔离 Unity 日志。
- `editor-results.json`、`editor-protocol.log`：逐条命令参数、返回数据、退出码、耗时、失败信息和性能对比。
- `editor-shutdown.log`：本次隔离实例的退出请求结果。

先看 `summary.json` 的失败阶段，再看对应日志。协议验证会故意触发异常、未知命令和超时；应以断言结果判断，不能仅因日志出现错误文字就判整个测试失败。性能值是同组三个只读查询的三次采样中位数，不代表整个业务流程的加速比例。

仅需调试协议用例，且隔离工程已加载当前代码和 CLI 时，可单独运行：

```powershell
python -X utf8 -B Tools~/ValidateEditorProtocol.py --report Temp/protocol-validation/manual-results.json
```

底层脚本检查目标工程与协议版本，并保留失败记录；它不会自动准备工程或关闭手工启动的 Unity。不要使用 `python -O` 禁用断言。

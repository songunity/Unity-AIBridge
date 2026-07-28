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

# Plan: AIBridge 窗口 i18n + UIToolkit 统一

## 目标

两个 Editor 窗口统一使用 UIToolkit，支持中英文切换。

## 背景

- `AIBridgeSettingsWindow` — 已用 UIToolkit，文本硬编码在 UXML
- `AIBridgeRuntimeSettingsWindow` — 用 IMGUI，已有 `AIBridgeEditorText.T()` 国际化
- 已有基础设施：`AIBridgeEditorText.T()`、`AIBridgeEditorLanguage` 枚举、`AIBridgeProjectSettings.EditorLanguage`

## 约束

- 不改 `AIBridgeEditorText` 接口
- 不引入第三方 i18n 框架
- 不动 Runtime 窗口的业务逻辑（只换 UI 层）
- 不碰无关代码
- 匹配现有代码风格

## 执行步骤

### Phase 1：Settings 窗口加 i18n

- [x] 1.1 UXML 中 header 区域加 `DropdownField name="language-selector"`
- [x] 1.2 `AIBridgeSettingsWindow.cs` 新增 `ApplyLocalization()` 方法，用 `AIBridgeEditorText.T()` 设置所有可见文本
- [x] 1.3 `CreateGUI()` 末尾调用 `ApplyLocalization()`
- [x] 1.4 语言选择器绑定回调：切换语言 → 保存设置 → 重新调用 `ApplyLocalization()`
- [x] 1.5 编译验证通过

### Phase 2：Runtime 窗口迁移到 UIToolkit

- [x] 2.1 创建 `Editor\UI\AIBridgeRuntimeSettingsWindow.uxml`，按现有 IMGUI 布局还原结构
- [x] 2.2 重写 `AIBridgeSettingsWindow_Runtime.cs`：`CreateGUI()` 加载 UXML，`Q<>()` 绑定控件，去掉 `OnGUI()`
- [x] 2.3 Runtime 窗口同样加语言选择器 + `ApplyLocalization()`
- [x] 2.4 功能与原 IMGUI 版本一致（所有设置项可读写）
- [x] 2.5 编译验证通过

### Phase 3：样式统一

- [x] 3.1 评估两个窗口 USS 重合度，决定是否提取公共 USS
- [x] 3.2 确保两个窗口视觉风格一致（间距、字体、颜色、按钮）
- [x] 3.3 最终编译验证

## 涉及文件

| 文件 | 操作 |
|------|------|
| `Editor\UI\AIBridgeSettingsWindow.uxml` | 修改（加语言选择器） |
| `Editor\Tools\AIBridgeSettingsWindow.cs` | 修改（加 i18n 逻辑） |
| `Editor\UI\AIBridgeRuntimeSettingsWindow.uxml` | 新建 |
| `Editor\Tools\AIBridgeSettingsWindow_Runtime.cs` | 重写 |
| `Editor\UI\AIBridgeSettingsWindow.uss` | 可能修改 |

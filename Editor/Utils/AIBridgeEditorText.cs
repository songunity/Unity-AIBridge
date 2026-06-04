namespace AIBridge.Editor
{
    internal enum AIBridgeEditorTextKey
    {
        AIBridgeSettingsTitle,
        AIBridgeSettingsSubtitle,
        AIBridgeRuntimeTitle,
        AIBridgeRuntimeSubtitle,
        AIBridgePlayersTitle,
        Actions,
        AgentRequiredMessage,
        AgentClaude,
        AgentCodex,
        AgentKiro,
        AllowReleaseBuild,
        AllowedActions,
        AllowedActionsHelp,
        ApplySceneRuntime,
        AppliedRuntimeSettingsMessage,
        Auth,
        AuthToken,
        AuthTokenHelp,
        AutoScanAssemblies,
        AutoScanAssembliesHelp,
        AutoInjectDevelopmentBuild,
        BindUrl,
        BridgeSettings,
        BuildInjectionSettings,
        Cache,
        Cancel,
        ClearCache,
        ClearCacheConfirm,
        ClearScreenshotCache,
        CliPath,
        ColorCount,
        CommandQueue,
        CommandRegistration,
        Commands,
        CompileRuntimeBridge,
        CompileRuntimeBridgeHelp,
        CliCommands,
        CopyAgent,
        CopyDiscoverCli,
        CopyHttpCli,
        CopyHttpStatus,
        CopyHttpStatusCli,
        CopyLaunchArgs,
        CopyListCli,
        CopyLogsCli,
        CopyScreenshotCli,
        CopyStatusCli,
        CreateRuntimeObject,
        DebugLogging,
        DefaultTargetId,
        Delete,
        DeleteCache,
        DeleteDiscoveryCache,
        DeleteDiscoveryCacheMessage,
        DeleteFailed,
        DeleteRuntimeTargetCache,
        DeleteRuntimeTargetCacheMessage,
        Device,
        DirectoryInformation,
        DiscoverCache,
        Discovered,
        DiscoveryUdpPort,
        DurationSeconds,
        EnableAIBridge,
        EnableHttpTransport,
        EnableLanDiscovery,
        EnableRuntimeBridge,
        EnableRuntimeCodeExecution,
        FileTransportTargets,
        Fps,
        FoundLanTargets,
        General,
        GenerateAndInstallSkill,
        GenerateSkill,
        GifRecorder,
        GifRecordingSettings,
        GifRecordingHelp,
        Health,
        Heartbeat,
        HeartbeatInterval,
        HttpBindAddress,
        HttpEntry,
        HttpLanDiscoveredTargets,
        HttpPort,
        HttpTransportSettings,
        HttpUrl,
        HybridClrHelp,
        InfoRuntimeConfigPath,
        KeepRunningInBackground,
        Kind,
        LanScan,
        LastSeen,
        Loading,
        LogBufferSize,
        Maintenance,
        MaxResultBytes,
        NoFileTransportTargets,
        NoLanDiscoveredTargets,
        NoToken,
        Now,
        Ok,
        Online,
        Open,
        OpenDirectory,
        OpenPlayersPanel,
        Path,
        Platform,
        Process,
        Product,
        Project,
        QuickSkillHelp,
        QuickSkillInstall,
        Reachable,
        RefreshedCount,
        Refresh,
        RefreshCommandList,
        RegisteredCommands,
        ReleaseWarning,
        Remote,
        ResetAllSettings,
        ResetSettings,
        ResetSettingsConfirm,
        ResolvedInfo,
        Runtime,
        RuntimeBridge,
        RuntimeBridgeHelp,
        RuntimeBehaviorLimits,
        RuntimeCapabilities,
        RuntimeCliCopied,
        RuntimeCodeWarning,
        RuntimeConfig,
        RuntimeConfigPath,
        RuntimeConfigWritten,
        RuntimeDirectory,
        RuntimeDiscoveryCliCopied,
        RuntimeHttpCliCopied,
        RuntimeLaunchArgsCopied,
        RuntimeHttpEntry,
        RuntimeInfo,
        ScanAssemblies,
        ScanAssembliesHelp,
        ScanFailed,
        ScanLan,
        ScanningUdp,
        Scale,
        Scene,
        ScreenshotCacheCleared,
        Screenshots,
        SecuritySettings,
        Settings,
        SettingsReset,
        Shortcuts,
        ShortcutsHelp,
        SkillDocHelp,
        SkillDocumentation,
        SkillGeneratedInstalled,
        SkillInstallation,
        SourceNic,
        Stale,
        StaleDiscoveryDeleted,
        StaleRuntimeDeleted,
        Success,
        TargetCount,
        TokenRequired,
        Tools,
        TotalRegisteredCommands,
        Transport,
        Unreachable,
        Url,
        Warning,
        WriteRuntimeConfig,
        Yes,
        No
    }

    internal static class AIBridgeEditorText
    {
        public static AIBridgeEditorLanguage Language
        {
            get { return AIBridgeProjectSettings.Instance.EditorLanguage; }
        }

        public static readonly string[] LanguageLabels =
        {
            "English",
            "简体中文"
        };

        public static readonly AIBridgeEditorLanguage[] LanguageValues =
        {
            AIBridgeEditorLanguage.English,
            AIBridgeEditorLanguage.SimplifiedChinese
        };

        public static int GetLanguageIndex(AIBridgeEditorLanguage language)
        {
            for (var i = 0; i < LanguageValues.Length; i++)
            {
                if (LanguageValues[i] == language)
                {
                    return i;
                }
            }

            return 0;
        }

        public static string Get(AIBridgeEditorTextKey key, params object[] args)
        {
            var template = GetTemplate(key);
            return args == null || args.Length == 0 ? template : string.Format(template, args);
        }

        public static string T(string english, string simplifiedChinese)
        {
            return For(Language, english, simplifiedChinese);
        }

        public static string For(AIBridgeEditorLanguage language, string english, string simplifiedChinese)
        {
            return language == AIBridgeEditorLanguage.SimplifiedChinese ? simplifiedChinese : english;
        }

        public static string[] LogRetrievalTypeLabels
        {
            get
            {
                return Language == AIBridgeEditorLanguage.SimplifiedChinese
                    ? new[] { "全部", "Info 及以上", "Warning 及以上", "Error" }
                    : new[] { "All", "Info and above", "Warning and above", "Error only" };
            }
        }

        private static string GetTemplate(AIBridgeEditorTextKey key)
        {
            switch (key)
            {
                case AIBridgeEditorTextKey.AIBridgeSettingsTitle: return T("AI Bridge Settings", "AI Bridge 设置");
                case AIBridgeEditorTextKey.AIBridgeSettingsSubtitle: return T("Configure AI Bridge behavior and tools", "配置 AI Bridge 行为和工具");
                case AIBridgeEditorTextKey.AIBridgeRuntimeTitle: return T("AIBridge Runtime", "AIBridge Runtime");
                case AIBridgeEditorTextKey.AIBridgeRuntimeSubtitle: return T("Configure Runtime Bridge for Play Mode and Player builds", "配置 Play Mode 和 Player Build 的 Runtime Bridge");
                case AIBridgeEditorTextKey.AIBridgePlayersTitle: return T("AIBridge Players", "AIBridge Players");
                case AIBridgeEditorTextKey.Actions: return T("Actions", "操作");
                case AIBridgeEditorTextKey.AgentRequiredMessage: return T("Please select at least one agent.", "请至少选择一个 Agent。");
                case AIBridgeEditorTextKey.AgentClaude: return "Claude Code (.claude)";
                case AIBridgeEditorTextKey.AgentCodex: return "Codex (.agents)";
                case AIBridgeEditorTextKey.AgentKiro: return "Kiro (.kiro)";
                case AIBridgeEditorTextKey.AllowReleaseBuild: return T("Allow Runtime Bridge In Release Build", "允许 Release Build 启用 Runtime Bridge");
                case AIBridgeEditorTextKey.AllowedActions: return T("Allowed Actions", "允许的 Actions");
                case AIBridgeEditorTextKey.AllowedActionsHelp: return T("Allowed Actions accepts comma, semicolon, or newline separated runtime action names, including built-in actions (e.g. runtime.status, runtime.code.execute). Empty means all built-in actions are allowed; custom actions are allowed in Editor/Development Build and blocked in Release Build.", "Allowed Actions 支持用逗号、分号或换行分隔 Runtime action 名称，包括内置 action（如 runtime.status、runtime.code.execute）。为空时所有内置 action 允许；自定义 action 在 Editor/Development Build 允许，Release Build 阻止。");
                case AIBridgeEditorTextKey.ApplySceneRuntime: return T("Apply To Scene Runtime", "应用到场景 Runtime");
                case AIBridgeEditorTextKey.AppliedRuntimeSettingsMessage: return T("Applied Runtime Bridge settings to {0} scene runtime object(s).", "已将 Runtime Bridge 设置应用到 {0} 个场景 Runtime 对象。");
                case AIBridgeEditorTextKey.Auth: return T("Auth", "鉴权");
                case AIBridgeEditorTextKey.AuthToken: return T("Auth Token", "鉴权 Token");
                case AIBridgeEditorTextKey.AuthTokenHelp: return T("Empty token means Runtime commands do not require authentication.", "Token 为空时，Runtime 命令不要求鉴权。");
                case AIBridgeEditorTextKey.AutoScanAssemblies: return T("Auto-scan Assemblies", "自动扫描程序集");
                case AIBridgeEditorTextKey.AutoScanAssembliesHelp: return T("When enabled, commands are scanned at runtime. When disabled, commands are pre-registered in code for better performance.", "启用后会在运行时扫描命令；禁用后命令会在代码中预注册以提升性能。");
                case AIBridgeEditorTextKey.AutoInjectDevelopmentBuild: return T("Auto Inject In Development Build", "Development Build 自动注入");
                case AIBridgeEditorTextKey.BindUrl: return T("Bind URL", "监听 URL");
                case AIBridgeEditorTextKey.BridgeSettings: return T("Bridge Settings", "Bridge 设置");
                case AIBridgeEditorTextKey.BuildInjectionSettings: return T("Build & Injection", "构建与注入");
                case AIBridgeEditorTextKey.Cache: return T("CACHE", "缓存");
                case AIBridgeEditorTextKey.Cancel: return T("Cancel", "取消");
                case AIBridgeEditorTextKey.ClearCache: return T("Clear Cache", "清除缓存");
                case AIBridgeEditorTextKey.ClearCacheConfirm: return T("Are you sure you want to clear the screenshot cache?", "确定要清除截图缓存吗？");
                case AIBridgeEditorTextKey.ClearScreenshotCache: return T("Clear Screenshot Cache", "清除截图缓存");
                case AIBridgeEditorTextKey.CliPath: return T("CLI Path:", "CLI 路径：");
                case AIBridgeEditorTextKey.ColorCount: return T("Color Count", "颜色数");
                case AIBridgeEditorTextKey.CommandQueue: return T("Command Queue:", "命令队列：");
                case AIBridgeEditorTextKey.CommandRegistration: return T("Command Registration", "命令注册");
                case AIBridgeEditorTextKey.Commands: return T("Commands", "命令");
                case AIBridgeEditorTextKey.CompileRuntimeBridge: return T("Compile Runtime Bridge", "编译 Runtime Bridge");
                case AIBridgeEditorTextKey.CompileRuntimeBridgeHelp: return T("Enable this for builds that should include Runtime Bridge code. Turn it off if you want the package build process to exclude Runtime Bridge code completely.", "需要在打包流程中包含 Runtime Bridge 代码时启用；如果完全不想让包里包含 Runtime Bridge 代码，请关闭。");
                case AIBridgeEditorTextKey.CliCommands: return T("CLI Commands", "CLI 命令");
                case AIBridgeEditorTextKey.CopyAgent: return T("Copy to Agent", "复制到 Agent");
                case AIBridgeEditorTextKey.CopyDiscoverCli: return T("Copy Discover CLI", "复制发现命令");
                case AIBridgeEditorTextKey.CopyHttpCli: return T("Copy HTTP CLI", "复制 HTTP 命令");
                case AIBridgeEditorTextKey.CopyHttpStatus: return T("Copy HTTP Status", "复制 HTTP 状态");
                case AIBridgeEditorTextKey.CopyHttpStatusCli: return T("Copy HTTP Status CLI", "复制 HTTP 状态命令");
                case AIBridgeEditorTextKey.CopyLaunchArgs: return T("Copy Launch Args", "复制启动参数");
                case AIBridgeEditorTextKey.CopyListCli: return T("Copy List CLI", "复制列表命令");
                case AIBridgeEditorTextKey.CopyLogsCli: return T("Copy Logs CLI", "复制日志命令");
                case AIBridgeEditorTextKey.CopyScreenshotCli: return T("Copy Screenshot CLI", "复制截图命令");
                case AIBridgeEditorTextKey.CopyStatusCli: return T("Copy Status CLI", "复制状态命令");
                case AIBridgeEditorTextKey.CreateRuntimeObject: return T("Create Runtime Object", "创建 Runtime 对象");
                case AIBridgeEditorTextKey.DebugLogging: return T("Debug Logging", "调试日志");
                case AIBridgeEditorTextKey.DefaultTargetId: return T("Default Target Id", "默认 Target Id");
                case AIBridgeEditorTextKey.Delete: return T("Delete", "删除");
                case AIBridgeEditorTextKey.DeleteCache: return T("Delete Cache", "删除缓存");
                case AIBridgeEditorTextKey.DeleteDiscoveryCache: return T("Delete Discovery Cache", "删除发现缓存");
                case AIBridgeEditorTextKey.DeleteDiscoveryCacheMessage: return T("Delete stale discovered target cache for '{0}'?", "删除已过期发现目标 '{0}' 的缓存？");
                case AIBridgeEditorTextKey.DeleteFailed: return T("Delete Failed", "删除失败");
                case AIBridgeEditorTextKey.DeleteRuntimeTargetCache: return T("Delete Runtime Target Cache", "删除 Runtime 目标缓存");
                case AIBridgeEditorTextKey.DeleteRuntimeTargetCacheMessage: return T("Delete stale Runtime target cache for '{0}'?", "删除已过期 Runtime 目标 '{0}' 的缓存？");
                case AIBridgeEditorTextKey.Device: return T("Device", "设备");
                case AIBridgeEditorTextKey.DirectoryInformation: return T("Directory Information", "目录信息");
                case AIBridgeEditorTextKey.DiscoverCache: return T("Discovery Cache", "发现缓存");
                case AIBridgeEditorTextKey.Discovered: return T("DISCOVERED", "已发现");
                case AIBridgeEditorTextKey.DiscoveryUdpPort: return T("Discovery UDP Port", "发现 UDP 端口");
                case AIBridgeEditorTextKey.DurationSeconds: return T("Duration (seconds)", "时长（秒）");
                case AIBridgeEditorTextKey.EnableAIBridge: return T("Enable AI Bridge", "启用 AI Bridge");
                case AIBridgeEditorTextKey.EnableHttpTransport: return T("Enable HTTP Transport", "启用 HTTP Transport");
                case AIBridgeEditorTextKey.EnableLanDiscovery: return T("Enable LAN Discovery", "启用局域网自动发现");
                case AIBridgeEditorTextKey.EnableRuntimeBridge: return T("Enable Runtime Bridge", "启用 Runtime Bridge");
                case AIBridgeEditorTextKey.EnableRuntimeCodeExecution: return T("Enable Runtime Code Execution", "启用 Runtime 代码执行");
                case AIBridgeEditorTextKey.FileTransportTargets: return T("File Transport Targets", "File Transport 目标");
                case AIBridgeEditorTextKey.Fps: return T("FPS", "帧率");
                case AIBridgeEditorTextKey.FoundLanTargets: return T("Found {0} reachable / {1} discovered", "发现 {0} 个可达 / {1} 个响应");
                case AIBridgeEditorTextKey.General: return T("General", "通用");
                case AIBridgeEditorTextKey.GenerateAndInstallSkill: return T("Generate and Install Skill", "生成并安装 Skill");
                case AIBridgeEditorTextKey.GenerateSkill: return T("Generate SKILL.md", "生成 SKILL.md");
                case AIBridgeEditorTextKey.GifRecorder: return T("GIF Recorder", "GIF 录制");
                case AIBridgeEditorTextKey.GifRecordingSettings: return T("GIF Recording Settings", "GIF 录制设置");
                case AIBridgeEditorTextKey.GifRecordingHelp: return T("Uses streaming encoding: frames are encoded and written to disk immediately, minimizing memory usage.", "使用流式编码：帧会立即编码并写入磁盘，尽量减少内存占用。");
                case AIBridgeEditorTextKey.Health: return T("Health", "Health");
                case AIBridgeEditorTextKey.Heartbeat: return T("Heartbeat", "Heartbeat");
                case AIBridgeEditorTextKey.HeartbeatInterval: return T("Heartbeat Interval", "Heartbeat 间隔");
                case AIBridgeEditorTextKey.HttpBindAddress: return T("HTTP Bind Address", "HTTP 监听地址");
                case AIBridgeEditorTextKey.HttpEntry: return T("HTTP Entry", "HTTP 入口");
                case AIBridgeEditorTextKey.HttpLanDiscoveredTargets: return T("HTTP / LAN Discovered Targets", "HTTP / 局域网发现目标");
                case AIBridgeEditorTextKey.HttpPort: return T("HTTP Port", "HTTP 端口");
                case AIBridgeEditorTextKey.HttpTransportSettings: return T("HTTP Transport Settings", "HTTP Transport 设置");
                case AIBridgeEditorTextKey.HttpUrl: return T("HTTP URL", "HTTP URL");
                case AIBridgeEditorTextKey.HybridClrHelp: return T("HybridCLR package is not installed. Runtime code execution will stay disabled to avoid IL2CPP Assembly.Load failures.", "当前未安装 HybridCLR 包。Runtime 代码执行会保持关闭，避免 IL2CPP 下 Assembly.Load 失败。");
                case AIBridgeEditorTextKey.InfoRuntimeConfigPath: return T("Runtime Config path:", "Runtime 配置路径：");
                case AIBridgeEditorTextKey.KeepRunningInBackground: return T("Keep Running In Background", "后台保持运行");
                case AIBridgeEditorTextKey.Kind: return T("Kind", "类型");
                case AIBridgeEditorTextKey.LanScan: return T("LAN Scan", "局域网扫描");
                case AIBridgeEditorTextKey.LastSeen: return T("Last Seen", "最后发现");
                case AIBridgeEditorTextKey.Loading: return T("Loading...", "加载中...");
                case AIBridgeEditorTextKey.LogBufferSize: return T("Log Buffer Size", "日志缓存数量");
                case AIBridgeEditorTextKey.Maintenance: return T("Maintenance", "维护");
                case AIBridgeEditorTextKey.MaxResultBytes: return T("Max Result Bytes", "最大结果字节数");
                case AIBridgeEditorTextKey.NoFileTransportTargets: return T("No file transport Runtime targets found. Start Play Mode or a built Player with AIBridgeRuntime enabled, or run LAN discovery for phone targets.", "未找到 File transport Runtime 目标。请启动挂有 AIBridgeRuntime 的 Play Mode/Player，或对手机目标执行局域网发现。");
                case AIBridgeEditorTextKey.NoLanDiscoveredTargets: return T("No LAN-discovered HTTP targets found. Keep Scan LAN checked and refresh after the Player is running on the same network.", "未发现局域网 HTTP 目标。请保持“扫描局域网”勾选，并在同一网络中的 Player 运行后刷新。");
                case AIBridgeEditorTextKey.NoToken: return T("No token", "无 Token");
                case AIBridgeEditorTextKey.Now: return T("now", "刚刚");
                case AIBridgeEditorTextKey.Ok: return T("OK", "确定");
                case AIBridgeEditorTextKey.Online: return T("ONLINE", "在线");
                case AIBridgeEditorTextKey.Open: return T("Open", "打开");
                case AIBridgeEditorTextKey.OpenDirectory: return T("Open Directory", "打开目录");
                case AIBridgeEditorTextKey.OpenPlayersPanel: return T("Open Players Panel", "打开 Players 面板");
                case AIBridgeEditorTextKey.Path: return T("Path", "路径");
                case AIBridgeEditorTextKey.Platform: return T("Platform", "平台");
                case AIBridgeEditorTextKey.Process: return T("Process", "进程");
                case AIBridgeEditorTextKey.Product: return T("Product", "产品");
                case AIBridgeEditorTextKey.Project: return T("Project", "项目");
                case AIBridgeEditorTextKey.QuickSkillHelp: return T("Generate SKILL.md and copy to selected agent directories", "生成 SKILL.md 并复制到选定的 Agent 目录");
                case AIBridgeEditorTextKey.QuickSkillInstall: return T("Quick Skill Install", "快速 Skill 安装");
                case AIBridgeEditorTextKey.Reachable: return T("REACHABLE", "可达");
                case AIBridgeEditorTextKey.RefreshedCount: return T("Refreshed: {0}", "刷新：{0}");
                case AIBridgeEditorTextKey.Refresh: return T("Refresh", "刷新");
                case AIBridgeEditorTextKey.RefreshCommandList: return T("Refresh Command List", "刷新命令列表");
                case AIBridgeEditorTextKey.RegisteredCommands: return T("Registered Commands", "已注册命令");
                case AIBridgeEditorTextKey.ReleaseWarning: return T("Release Build Runtime Bridge is a debugging backdoor. Use an auth token and restrict Allowed Actions before shipping.", "Release Build Runtime Bridge 是调试入口。发布前请设置鉴权 Token 并限制 Allowed Actions。");
                case AIBridgeEditorTextKey.Remote: return T("Remote", "远端");
                case AIBridgeEditorTextKey.ResetAllSettings: return T("Reset All Settings", "重置所有设置");
                case AIBridgeEditorTextKey.ResetSettings: return T("Reset Settings", "重置设置");
                case AIBridgeEditorTextKey.ResetSettingsConfirm: return T("Are you sure you want to reset all settings to default?", "确定要将所有设置重置为默认值吗？");
                case AIBridgeEditorTextKey.ResolvedInfo: return T("Resolved Info", "解析后的信息");
                case AIBridgeEditorTextKey.Runtime: return T("Runtime", "Runtime");
                case AIBridgeEditorTextKey.RuntimeBridge: return T("Runtime Bridge", "Runtime Bridge");
                case AIBridgeEditorTextKey.RuntimeBridgeHelp: return T("Runtime Bridge lets AIBridgeCLI connect to AIBridgeRuntime inside Play Mode or a built Player. Release builds remain disabled unless explicitly allowed.", "Runtime Bridge 允许 AIBridgeCLI 连接 Play Mode 或已编译 Player 内的 AIBridgeRuntime。Release Build 默认关闭，除非显式允许。");
                case AIBridgeEditorTextKey.RuntimeBehaviorLimits: return T("Runtime Behavior & Limits", "运行行为与限制");
                case AIBridgeEditorTextKey.RuntimeCapabilities: return T("Runtime Capabilities", "Runtime 能力");
                case AIBridgeEditorTextKey.RuntimeCliCopied: return T("[AIBridge] Runtime CLI command copied.", "[AIBridge] Runtime CLI 命令已复制。");
                case AIBridgeEditorTextKey.RuntimeCodeWarning: return T("Runtime code execution loads Roslyn-compiled DLLs in Player by Assembly.Load. Keep it for trusted debugging builds only.", "Runtime 代码执行会在 Player 中通过 Assembly.Load 加载 Roslyn 编译的 DLL。仅用于可信调试构建。");
                case AIBridgeEditorTextKey.RuntimeConfig: return T("Runtime Config", "Runtime 配置");
                case AIBridgeEditorTextKey.RuntimeConfigPath: return T("Runtime Config path:", "Runtime 配置路径：");
                case AIBridgeEditorTextKey.RuntimeConfigWritten: return T("[AIBridge] Runtime config written: {0}", "[AIBridge] Runtime 配置已写入：{0}");
                case AIBridgeEditorTextKey.RuntimeDirectory: return T("Runtime Directory", "Runtime 目录");
                case AIBridgeEditorTextKey.RuntimeDiscoveryCliCopied: return T("[AIBridge] Runtime discovery CLI command copied.", "[AIBridge] Runtime 自动发现 CLI 命令已复制。");
                case AIBridgeEditorTextKey.RuntimeHttpCliCopied: return T("[AIBridge] Runtime HTTP CLI command copied.", "[AIBridge] Runtime HTTP CLI 命令已复制。");
                case AIBridgeEditorTextKey.RuntimeLaunchArgsCopied: return T("[AIBridge] Runtime launch arguments copied.", "[AIBridge] Runtime 启动参数已复制。");
                case AIBridgeEditorTextKey.RuntimeHttpEntry: return T("Runtime HTTP Entry", "Runtime HTTP 入口");
                case AIBridgeEditorTextKey.RuntimeInfo: return T("Runtime Info", "Runtime 信息");
                case AIBridgeEditorTextKey.ScanAssemblies: return T("Scan Assemblies", "扫描程序集");
                case AIBridgeEditorTextKey.ScanAssembliesHelp: return T("Separate multiple assemblies with semicolons (e.g., Assembly-CSharp;Assembly-CSharp-Editor)", "多个程序集请用分号分隔（例如 Assembly-CSharp;Assembly-CSharp-Editor）");
                case AIBridgeEditorTextKey.ScanFailed: return T("Scan failed: {0}", "扫描失败：{0}");
                case AIBridgeEditorTextKey.ScanLan: return T("Scan LAN", "扫描局域网");
                case AIBridgeEditorTextKey.ScanningUdp: return T("Scanning UDP {0}...", "正在扫描 UDP {0}...");
                case AIBridgeEditorTextKey.Scale: return T("Scale", "缩放");
                case AIBridgeEditorTextKey.Scene: return T("Scene", "场景");
                case AIBridgeEditorTextKey.ScreenshotCacheCleared: return T("Screenshot cache cleared.", "截图缓存已清除。");
                case AIBridgeEditorTextKey.Screenshots: return T("Screenshots:", "截图：");
                case AIBridgeEditorTextKey.SecuritySettings: return T("Security", "安全");
                case AIBridgeEditorTextKey.Settings: return T("Settings", "设置");
                case AIBridgeEditorTextKey.SettingsReset: return T("Settings reset to default.", "设置已重置为默认值。");
                case AIBridgeEditorTextKey.Shortcuts: return T("Shortcuts", "快捷键");
                case AIBridgeEditorTextKey.ShortcutsHelp: return T("F12 - Screenshot Game View (Play Mode)\nF11 - Start/Stop GIF Recording (Play Mode)", "F12 - 截取 Game View（播放模式）\nF11 - 开始/停止 GIF 录制（播放模式）");
                case AIBridgeEditorTextKey.SkillDocHelp: return T("Generate SKILL.md file for Droid integration", "为 Droid 集成生成 SKILL.md 文件");
                case AIBridgeEditorTextKey.SkillDocumentation: return T("Skill Documentation", "Skill 文档");
                case AIBridgeEditorTextKey.SkillGeneratedInstalled: return T("Skill generated and installed to: {0}", "Skill 已生成并安装到：{0}");
                case AIBridgeEditorTextKey.SkillInstallation: return T("Skill Installation", "Skill 安装");
                case AIBridgeEditorTextKey.SourceNic: return T("Source NIC", "来源网卡");
                case AIBridgeEditorTextKey.Stale: return T("STALE", "已过期");
                case AIBridgeEditorTextKey.StaleDiscoveryDeleted: return T("[AIBridge] Stale Runtime discovery cache deleted: {0}", "[AIBridge] 已删除过期 Runtime 发现缓存：{0}");
                case AIBridgeEditorTextKey.StaleRuntimeDeleted: return T("[AIBridge] Stale Runtime target cache deleted: {0}", "[AIBridge] 已删除过期 Runtime 目标缓存：{0}");
                case AIBridgeEditorTextKey.Success: return T("Success", "成功");
                case AIBridgeEditorTextKey.TargetCount: return T("Targets: {0}", "目标数：{0}");
                case AIBridgeEditorTextKey.TokenRequired: return T("Token required", "需要 Token");
                case AIBridgeEditorTextKey.Tools: return T("Tools", "工具");
                case AIBridgeEditorTextKey.TotalRegisteredCommands: return T("Total registered commands: {0}", "已注册命令总数：{0}");
                case AIBridgeEditorTextKey.Transport: return T("Transport", "传输");
                case AIBridgeEditorTextKey.Unreachable: return T("unreachable", "不可达");
                case AIBridgeEditorTextKey.Url: return T("URL", "URL");
                case AIBridgeEditorTextKey.Warning: return T("Warning", "警告");
                case AIBridgeEditorTextKey.WriteRuntimeConfig: return T("Write Runtime Config", "写入 Runtime 配置");
                case AIBridgeEditorTextKey.Yes: return T("Yes", "是");
                case AIBridgeEditorTextKey.No: return T("No", "否");
                default: return key.ToString();
            }
        }
    }
}

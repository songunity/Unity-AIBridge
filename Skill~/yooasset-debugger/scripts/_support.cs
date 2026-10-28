JObject ReadYooReport() {
    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("YooAsset.YooAssets")).FirstOrDefault(t => t != null);
    if (type == null) throw new NotSupportedException("YooAsset 未加载");
    var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    var method = type.GetMethod("GetDiagnosticReport", flags) ?? type.GetMethod("GetDebugReport", flags);
    if (method == null) throw new NotSupportedException("当前 YooAsset 没有支持的诊断接口");
    if (!Application.isPlaying) throw new InvalidOperationException("YooAsset 检查需要 Play Mode");
    var raw = method.Invoke(null, null) ?? throw new InvalidOperationException("YooAsset 报告为空");
    object Field(object value, params string[] names) {
        foreach (var name in names) { var field = value.GetType().GetField(name); if (field != null) return field.GetValue(value); }
        throw new MissingFieldException(value.GetType().FullName, string.Join("/", names));
    }
    var packages = (System.Collections.IEnumerable)Field(raw, "PackageDataList", "PackageDatas");
    var rows = new JArray(); var packageNames = new List<string>();
    foreach (var package in packages) {
        string name = (string)Field(package, "PackageName");
        if (options["package"] != null && name != (string)options["package"]) continue;
        packageNames.Add(name);
        foreach (var provider in (System.Collections.IEnumerable)Field(package, "ProviderInfos")) {
            var dependencies = provider.GetType().GetField("Dependencies")?.GetValue(provider)
                ?? provider.GetType().GetField("BundleInfos")?.GetValue(provider);
            rows.Add(new JObject { ["package"] = name, ["asset"] = Convert.ToString(Field(provider, "AssetPath")),
                ["references"] = Convert.ToInt64(Field(provider, "ReferenceCount", "RefCount")),
                ["elapsedMs"] = Convert.ToInt64(Field(provider, "ElapsedMilliseconds", "LoadingTime")),
                ["state"] = Convert.ToString(Field(provider, "Status")),
                ["dependencies"] = dependencies == null ? JValue.CreateNull() : JToken.FromObject(dependencies) });
        }
    }
    if (options["package"] != null && packageNames.Count == 0) throw new ArgumentException("指定资源包不在报告中");
    if (packageNames.Count == 0) throw new InvalidOperationException("YooAsset 没有已初始化的资源包");
    warnings.Add("references=0 仅是零引用线索，不代表泄漏；仍需检查加载状态、依赖和卸载策略");
    return new JObject { ["schemaVersion"] = 1, ["project"] = (string)context["project"], ["target"] = ProfilerDriver.connectedProfiler,
        ["api"] = method.Name, ["assembly"] = type.Assembly.FullName, ["frame"] = Convert.ToInt64(Field(raw, "FrameCount")),
        ["packages"] = JArray.FromObject(packageNames), ["assets"] = rows, ["capturedAtUtc"] = DateTime.UtcNow.ToString("O") };
}

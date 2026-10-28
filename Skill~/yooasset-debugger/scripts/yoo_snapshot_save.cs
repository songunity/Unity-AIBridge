if (!Application.isPlaying) return DiagnosticResult("blocked", "需要已初始化 YooAsset 的 Play Mode");
var report = ReadYooReport();
var path = SaveArtifact("yoo_snapshot", report);
return DiagnosticResult("completed", new { snapshot = path, packages = report["packages"], providers = ((JArray)report["assets"]).Count });

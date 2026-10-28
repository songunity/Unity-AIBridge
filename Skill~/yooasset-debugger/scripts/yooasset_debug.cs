if (!Application.isPlaying) return DiagnosticResult("blocked", "需要已初始化 YooAsset 的 Play Mode");
var report = ReadYooReport();
var rows = (JArray)report["assets"];
SaveArtifact("yooasset", report);
return DiagnosticResult("completed", new { packages = report["packages"], providers = rows.Count,
    zeroReferences = rows.Count(x => (long)x["references"] == 0),
    top = rows.OrderByDescending(x => (long)x["references"]).Take(Limit("top", 30, 200)).ToArray() });

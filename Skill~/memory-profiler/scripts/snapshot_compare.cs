string current = (string)options["snapshot"], baseline = (string)options["baseline"];
if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(baseline)) return DiagnosticResult("blocked", "请指定 snapshot 和 baseline");
Dictionary<string, (long count, ulong bytes)> Read(string path) {
    var loaded = OpenMemorySnapshot(path);
    try { return ReadMemoryGroups(loaded.snapshot); }
    finally { try { (loaded.snapshot as IDisposable)?.Dispose(); } finally { (loaded.reader as IDisposable)?.Dispose(); } }
}
// 仅保留聚合数据后释放，再加载下一快照，不持有两份全量对象数组。
var before = Read(baseline); var after = Read(current);
var changes = before.Keys.Union(after.Keys).Select(key => {
    before.TryGetValue(key, out var a); after.TryGetValue(key, out var b);
    return new { identity = JsonConvert.DeserializeObject<string[]>(key), countDelta = b.count - a.count, bytesDelta = (long)b.bytes - (long)a.bytes };
}).Where(x => x.countDelta != 0 || x.bytesDelta != 0).OrderByDescending(x => x.bytesDelta).ToArray();
warnings.Add("按类型和名称分组的净变化，不是对象身份或泄漏证明；确认两个快照的采集目标和场景可比");
context["snapshot"] = Path.GetFullPath(current); context["baseline"] = Path.GetFullPath(baseline);
SaveArtifact("memory_compare", new { context, changes, warnings });
return DiagnosticResult("completed", new { changedGroups = changes.Length, top = changes.Take(Limit("top", 30, 200)).ToArray() });

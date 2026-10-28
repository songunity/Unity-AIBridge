if (!Application.isPlaying) return DiagnosticResult("blocked", "需要已初始化 YooAsset 的 Play Mode");
string baseline = (string)options["baseline"];
if (string.IsNullOrWhiteSpace(baseline) || !File.Exists(baseline)) return DiagnosticResult("blocked", "请通过 baseline 指定已有 YooAsset 快照");
var before = JObject.Parse(File.ReadAllText(baseline));
var after = ReadYooReport();
if ((int?)before["schemaVersion"] != 1 || (string)before["project"] != (string)after["project"] || (int?)before["target"] != (int?)after["target"])
    throw new InvalidDataException("快照格式、项目或连接目标不匹配");
if (!JToken.DeepEquals(before["packages"], after["packages"])) warnings.Add("比较前后的资源包集合不同");
string Key(JToken row) => JsonConvert.SerializeObject(new[] { (string)row["package"], (string)row["asset"] });
var oldRows = ((JArray)before["assets"]).GroupBy(Key).ToDictionary(g => g.Key, g => new { references = g.Sum(x => (long)x["references"]), count = g.Count() });
var newRows = ((JArray)after["assets"]).GroupBy(Key).ToDictionary(g => g.Key, g => new { references = g.Sum(x => (long)x["references"]), count = g.Count() });
var changes = oldRows.Keys.Union(newRows.Keys).Select(key => new {
    identity = JsonConvert.DeserializeObject<string[]>(key),
    beforeReferences = oldRows.ContainsKey(key) ? oldRows[key].references : (long?)null,
    afterReferences = newRows.ContainsKey(key) ? newRows[key].references : (long?)null,
    beforeProviders = oldRows.ContainsKey(key) ? oldRows[key].count : 0,
    afterProviders = newRows.ContainsKey(key) ? newRows[key].count : 0
}).Where(x => x.beforeReferences != x.afterReferences || x.beforeProviders != x.afterProviders).ToArray();
SaveArtifact("yoo_compare", new { baseline, current = after, changes, warnings });
return DiagnosticResult("completed", new { changed = changes.Length, changes = changes.Take(Limit("top", 30, 200)).ToArray() });

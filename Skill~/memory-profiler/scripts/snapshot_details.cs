string path = (string)options["snapshot"];
if (string.IsNullOrWhiteSpace(path)) return DiagnosticResult("blocked", "请指定 snapshot 文件");
var loaded = OpenMemorySnapshot(path);
context["snapshot"] = Path.GetFullPath(path);
try {
    var snapshot = loaded.snapshot;
    var groups = ReadMemoryGroups(snapshot);
    var summary = new Dictionary<string, object>();
    foreach (var name in new[] { "NativeObjects", "NativeAllocations", "NativeAllocationSites", "NativeCallstackSymbols", "NativeMemoryRegions", "NativeMemoryLabels", "NativeRootReferences", "GcHandles", "Connections", "ManagedHeapSections" })
        summary[name] = Convert.ToInt64(MemoryMember(MemoryMember(snapshot, name), "Count"));
    summary["topGroups"] = groups.OrderByDescending(x => x.Value.bytes).Take(Limit("top", 30, 200))
        .Select(x => new { identity = JsonConvert.DeserializeObject<string[]>(x.Key), x.Value.count, x.Value.bytes }).ToArray();
    var references = new List<object>();
    if (options["nativeIndex"] != null) {
        int index = (int)options["nativeIndex"];
        if (index < 0 || index >= Convert.ToInt64(summary["NativeObjects"])) throw new ArgumentOutOfRangeException("nativeIndex");
        var crawler = loaded.assembly.GetType("Unity.MemoryProfiler.Editor.ManagedDataCrawler", true);
        var iterator = (System.Collections.IEnumerator)crawler.GetMethod("Crawl").Invoke(null, new[] { snapshot });
        try { while (iterator.MoveNext()) await Task.Yield(); }
        finally { (iterator as IDisposable)?.Dispose(); }
        var objectType = loaded.assembly.GetType("Unity.MemoryProfiler.Editor.ObjectData", true);
        var connectionType = loaded.assembly.GetType("Unity.MemoryProfiler.Editor.ObjectConnection", true);
        var start = objectType.GetMethod("FromNativeObjectIndex").Invoke(null, new object[] { snapshot, index });
        var incoming = connectionType.GetMethod("GetAllReferencingObjects", new[] { snapshot.GetType(), objectType });
        var outgoing = connectionType.GetMethod("GetAllReferencedObjects", new[] { snapshot.GetType(), objectType });
        if (incoming == null || outgoing == null) throw new NotSupportedException("Memory Profiler 引用查询 API 不兼容");
        int maxNodes = Limit("maxNodes", 100, 1000), maxDepth = Limit("depth", 3, 8);
        var queue = new Queue<(object value, int depth)>(); queue.Enqueue((start, 0));
        var seen = new HashSet<string>();
        string Identity(object obj) => MemoryMember(obj, "dataType") + ":" + MemoryMember(obj, "nativeObjectIndex") + ":" + MemoryMember(obj, "hostManagedObjectPtr") + ":" + MemoryMember(obj, "fieldIndex");
        while (queue.Count > 0 && references.Count < maxNodes) {
            var item = queue.Dequeue(); string id = Identity(item.value); if (!seen.Add(id)) continue;
            var parents = ((Array)incoming.Invoke(null, new[] { snapshot, item.value })).Cast<object>().ToArray();
            var children = ((Array)outgoing.Invoke(null, new[] { snapshot, item.value })).Cast<object>().ToArray();
            references.Add(new { id, item.depth, type = objectType.GetMethod("GenerateTypeName").Invoke(item.value, new[] { snapshot }),
                referencedBy = parents.Take(maxNodes).Select(Identity).ToArray(), referencesTo = children.Take(maxNodes).Select(Identity).ToArray() });
            if (item.depth < maxDepth) foreach (var parent in parents.Take(maxNodes)) queue.Enqueue((parent, item.depth + 1));
        }
        if (queue.Count > 0) warnings.Add("引用查询达到节点预算，结果不是完整根路径证明");
        warnings.Add("引用图按快照索引识别对象，不保证跨快照身份；深度限制处不等同于 GC Root");
    }
    var meta = MemoryMember(snapshot, "MetaData");
    context["snapshotMetadata"] = JObject.FromObject(meta);
    if (Convert.ToInt64(summary["NativeAllocationSites"]) == 0 || Convert.ToInt64(summary["NativeCallstackSymbols"]) == 0)
        warnings.Add("快照没有完整 Native 分配位置或栈符号；不能从当前文件补出未采集的数据");
    SaveArtifact("memory_details", new { context, summary, references, warnings });
    return DiagnosticResult("completed", new { summary, referenceNodes = references.Count });
} finally {
    try { (loaded.snapshot as IDisposable)?.Dispose(); }
    finally { (loaded.reader as IDisposable)?.Dispose(); }
}

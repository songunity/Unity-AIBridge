object MemoryMember(object value, string name) {
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    var type = value.GetType();
    var field = type.GetField(name, flags); if (field != null) return field.GetValue(value);
    var property = type.GetProperty(name, flags); if (property != null) return property.GetValue(value);
    throw new MissingMemberException(type.FullName, name);
}
(System.Reflection.Assembly assembly, object reader, object snapshot) OpenMemorySnapshot(string path) {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("请指定已有快照", path);
    var assembly = System.Reflection.Assembly.Load("Unity.MemoryProfiler.Editor");
    var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
    context["memoryProfilerVersion"] = info?.version ?? assembly.GetName().Version.ToString();
    var readerType = assembly.GetType("Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader", true);
    var snapshotType = assembly.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot", true);
    object reader = Activator.CreateInstance(readerType);
    try {
        var error = readerType.GetMethod("Open", new[] { typeof(string) }).Invoke(reader, new object[] { path });
        if (error.ToString() != "Success") throw new IOException("读取快照失败: " + error);
        var snapshot = Activator.CreateInstance(snapshotType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { reader }, null);
        return (assembly, reader, snapshot);
    } catch { (reader as IDisposable)?.Dispose(); throw; }
}
IEnumerable<object> MemoryValues(object array) {
    var iterator = (System.Collections.IEnumerator)array.GetType().GetMethod("GetEnumerator").Invoke(array, null);
    long expected = Convert.ToInt64(MemoryMember(array, "Count")), read = 0;
    try {
        while (iterator.MoveNext()) { read++; yield return iterator.Current; }
        // 1.1.0 的 MoveNext 为 ++nextIndex < Count；false 时 Current 仍指向最后一项。
        // 只适配已核对的版本和枚举器，不对任意 IEnumerable 忽略返回值。
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(array.GetType().Assembly);
        if (expected > 0 && read == expected - 1 && package?.version == "1.1.0"
            && iterator.GetType().FullName.StartsWith("Unity.MemoryProfiler.Editor.Containers.DynamicArrayEnumerator", StringComparison.Ordinal)) {
            read++; yield return iterator.Current;
        }
        if (read != expected) throw new InvalidDataException("Memory Profiler 数组枚举数量与 Count 不一致");
    }
    finally { (iterator as IDisposable)?.Dispose(); }
}
Dictionary<string, (long count, ulong bytes)> ReadMemoryGroups(object snapshot) {
    var native = MemoryMember(snapshot, "NativeObjects");
    var names = (string[])MemoryMember(native, "ObjectName");
    var types = (string[])MemoryMember(MemoryMember(snapshot, "NativeTypes"), "TypeName");
    var groups = new Dictionary<string, (long count, ulong bytes)>();
    using var sizes = MemoryValues(MemoryMember(native, "Size")).GetEnumerator();
    using var indices = MemoryValues(MemoryMember(native, "NativeTypeArrayIndex")).GetEnumerator();
    int index = 0;
    while (sizes.MoveNext() && indices.MoveNext()) {
        int ti = Convert.ToInt32(indices.Current);
        string key = JsonConvert.SerializeObject(new[] { ti >= 0 && ti < types.Length ? types[ti] : "Unknown", index < names.Length ? names[index] : "" });
        groups.TryGetValue(key, out var previous); groups[key] = (previous.count + 1, previous.bytes + Convert.ToUInt64(sizes.Current)); index++;
    }
    if (index != Convert.ToInt64(MemoryMember(native, "Count"))) throw new InvalidDataException("快照对象数组长度不一致");
    return groups;
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

var options = JObject.Parse(__OPTIONS_JSON__);
var artifacts = new List<string>();
var warnings = new List<string>();
string diagnosticStatus = "completed";
var context = new Dictionary<string, object> {
    ["unityVersion"] = Application.unityVersion,
    ["project"] = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
    ["profilerTarget"] = ProfilerDriver.connectedProfiler,
    ["profileEditor"] = ProfilerDriver.profileEditor,
    ["deepProfiling"] = ProfilerDriver.deepProfiling,
    ["callstacks"] = ProfilerDriver.memoryRecordMode.ToString(),
    ["scene"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
    ["capturedAtUtc"] = DateTime.UtcNow.ToString("O")
};
var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../.aibridge/diagnostics"));
// 不将某一版本 Newtonsoft 的 JToken 暴露给宿主序列化器，避免跨程序集被当作 IEnumerable。
object PlainToken(JToken token) {
    if (token is JObject obj) return obj.Properties().ToDictionary(p => p.Name, p => PlainToken(p.Value));
    if (token is JArray array) return array.Select(PlainToken).ToArray();
    return token is JValue value ? value.Value : null;
}
object DiagnosticResult(string status, object summary) => PlainToken(JToken.FromObject(new { status, summary, context, artifacts, warnings }));
string SaveArtifact(string name, object value) {
    Directory.CreateDirectory(outputDirectory);
    var path = Path.Combine(outputDirectory, name + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".json");
    File.WriteAllText(path, JsonConvert.SerializeObject(value, Formatting.Indented)); artifacts.Add(path); return path;
}
int Limit(string key, int fallback, int max) {
    int value = (int?)options[key] ?? fallback;
    if (value < 1 || value > max) throw new ArgumentOutOfRangeException(key, "参数范围为 1.." + max);
    return value;
}
async Task<object> RunDiagnostic() {
__DIAGNOSTIC_BODY__
}
try {
    var result = await RunDiagnostic();
    if (result is string text) {
        SaveArtifact("report", new { text, context });
        return DiagnosticResult(diagnosticStatus, text);
    }
    return result;
} catch (Exception ex) {
    var cause = ex.GetBaseException();
    SaveArtifact("failure", new { error = cause.Message, stack = cause.StackTrace, context });
    return DiagnosticResult(cause is NotSupportedException || cause is FileNotFoundException ? "blocked" : "failed",
        new { error = cause.Message, type = cause.GetType().FullName });
}

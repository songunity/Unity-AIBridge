using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

var sb = new StringBuilder();

var latestFile = (string)options["capture"];
if (string.IsNullOrWhiteSpace(latestFile) || !File.Exists(latestFile)) return DiagnosticResult("blocked", "请通过 capture 指定抓帧 JSON");
var json = File.ReadAllText(latestFile);

var root = JObject.Parse(json);
var eventArray = root["events"] as JArray ?? throw new InvalidDataException("events 缺失");
var events = eventArray.Cast<JObject>().Select(item => item.Properties().ToDictionary(p => p.Name, p => p.Value.ToString())).ToList();
if ((int?)root["capturedCount"] != (int?)root["eventCount"]) { diagnosticStatus = "partial"; warnings.Add("输入是部分捕获，统计不代表完整帧"); }

sb.AppendLine($"=== Frame Debugger 分析 ({Path.GetFileName(latestFile)}) ===");
sb.AppendLine($"总事件数: {events.Count}");

// 1. 按事件类型统计
sb.AppendLine("\n--- 事件类型分布 ---");
var byType = events.GroupBy(e => Get(e, "type")).OrderByDescending(g => g.Count());
foreach (var g in byType)
    sb.AppendLine($"  {g.Key}: {g.Count()} ({g.Count() * 100 / events.Count}%)");

// 2. Draw Call 统计
int totalDrawCalls = events.Sum(e => GetInt(e, "drawCalls"));
sb.AppendLine($"\n--- Draw Call 统计 ---");
sb.AppendLine($"总 Draw Calls: {totalDrawCalls}");

// 按 shader 统计 draw calls
sb.AppendLine("\n按 Shader 统计 Draw Calls (Top 15):");
var byShader = events.Where(e => !string.IsNullOrEmpty(Get(e, "shader")))
    .GroupBy(e => Get(e, "shader"))
    .Select(g => new { Shader = g.Key, DC = g.Sum(e => GetInt(e, "drawCalls")), Events = g.Count(), Verts = g.Sum(e => GetInt(e, "verts")) })
    .OrderByDescending(x => x.DC).Take(15);
foreach (var s in byShader)
    sb.AppendLine($"  {s.Shader}: dc={s.DC} events={s.Events} verts={s.Verts:N0}");

// 3. Batch Break 原因统计
sb.AppendLine("\n--- Batch Break 原因 ---");
var byBreak = events.Where(e => !string.IsNullOrEmpty(Get(e, "batchBreak")))
    .GroupBy(e => Get(e, "batchBreak")).OrderByDescending(g => g.Count());
foreach (var g in byBreak)
    sb.AppendLine($"  {g.Key}: {g.Count()}");

// 4. RenderTarget 切换
sb.AppendLine("\n--- RenderTarget 使用 ---");
var byRT = events.GroupBy(e => Get(e, "rt")).OrderByDescending(g => g.Count());
foreach (var g in byRT)
{
    var first = g.First();
    sb.AppendLine($"  {g.Key} ({Get(first, "rtW")}x{Get(first, "rtH")}): {g.Count()} events, {g.Sum(e => GetInt(e, "drawCalls"))} dc");
}

// 5. Shader Pass 分布
sb.AppendLine("\n--- Shader Pass 分布 (Top 10) ---");
var byPass = events.Where(e => !string.IsNullOrEmpty(Get(e, "pass")))
    .GroupBy(e => $"{Get(e, "shader")} / {Get(e, "pass")}")
    .OrderByDescending(g => g.Sum(e => GetInt(e, "drawCalls"))).Take(10);
foreach (var g in byPass)
    sb.AppendLine($"  {g.Key}: dc={g.Sum(e => GetInt(e, "drawCalls"))} events={g.Count()}");

// 6. 顶点数 Top 10
sb.AppendLine("\n--- 顶点数 Top 10 事件 ---");
var topVerts = events.OrderByDescending(e => GetInt(e, "verts")).Take(10);
foreach (var e in topVerts)
    sb.AppendLine($"  [{Get(e, "idx")}] {Get(e, "type")} go={Get(e, "go")} shader={Get(e, "shader")} verts={GetInt(e, "verts"):N0} instances={Get(e, "instances")}");

// 7. SRP Batcher 兼容性
int srpBatch = events.Count(e => Get(e, "type") == "SRPBatch");
int notCompatible = events.Count(e => Get(e, "batchBreak").Contains("not compatible with SRP batcher"));
int diffMaterial = events.Count(e => Get(e, "batchBreak").Contains("different materials"));
int diffShader = events.Count(e => Get(e, "batchBreak").Contains("different shaders"));
sb.AppendLine("\n--- SRP Batcher 兼容性 ---");
sb.AppendLine($"  SRPBatch 事件: {srpBatch}");
sb.AppendLine($"  不兼容 SRP Batcher: {notCompatible}");
sb.AppendLine($"  不同材质打断: {diffMaterial}");
sb.AppendLine($"  不同 Shader 打断: {diffShader}");

// 8. Shader Keywords 统计
sb.AppendLine("\n--- Shader Keywords 频率 (Top 15) ---");
var kwCount = new Dictionary<string, int>();
foreach (var e in events)
{
    var kw = Get(e, "keywords");
    if (string.IsNullOrEmpty(kw)) continue;
    foreach (var k in kw.Split(' '))
    {
        if (string.IsNullOrEmpty(k)) continue;
        if (kwCount.ContainsKey(k)) kwCount[k]++; else kwCount[k] = 1;
    }
}
foreach (var kv in kwCount.OrderByDescending(x => x.Value).Take(15))
    sb.AppendLine($"  {kv.Key}: {kv.Value}");

return sb.ToString();

// --- helpers ---

string Get(Dictionary<string, string> d, string key) => d.TryGetValue(key, out var v) ? v : "";
int GetInt(Dictionary<string, string> d, string key) => d.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : 0;

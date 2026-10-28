using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

var sb = new StringBuilder();
var renderers = UnityEngine.Object.FindObjectsOfType<ParticleSystemRenderer>(true);

sb.AppendLine($"=== 场景中 ParticleSystemRenderer 统计 ({renderers.Length} 个) ===");

var byShader = new Dictionary<string, List<string>>();
var byMaterial = new Dictionary<string, (string shader, int count, List<string> gos)>();

foreach (var r in renderers)
{
    if (r.sharedMaterial == null) continue;
    var mat = r.sharedMaterial;
    var shaderName = mat.shader != null ? mat.shader.name : "null";
    var matName = mat.name;
    var goPath = GetPath(r.transform);

    if (!byMaterial.ContainsKey(matName))
        byMaterial[matName] = (shaderName, 1, new List<string> { goPath });
    else
    {
        var v = byMaterial[matName];
        byMaterial[matName] = (v.shader, v.count + 1, v.gos);
        if (v.gos.Count < 3) v.gos.Add(goPath);
    }
}

// 按 shader 汇总
var shaderSummary = new Dictionary<string, (int matCount, int totalCount)>();
foreach (var kv in byMaterial)
{
    var shader = kv.Value.shader;
    if (!shaderSummary.ContainsKey(shader))
        shaderSummary[shader] = (1, kv.Value.count);
    else
    {
        var v = shaderSummary[shader];
        shaderSummary[shader] = (v.matCount + 1, v.totalCount + kv.Value.count);
    }
}

sb.AppendLine("\n--- 按 Shader 汇总 ---");
foreach (var kv in shaderSummary.OrderByDescending(x => x.Value.totalCount))
    sb.AppendLine($"  {kv.Key}: {kv.Value.totalCount} 个渲染器, {kv.Value.matCount} 种材质");

sb.AppendLine("\n--- 按材质汇总 (Top 30) ---");
foreach (var kv in byMaterial.OrderByDescending(x => x.Value.count).Take(30))
{
    var v = kv.Value;
    sb.AppendLine($"  [{v.shader}] {kv.Key}: {v.count} 个");
    foreach (var go in v.gos)
        sb.AppendLine($"    -> {go}");
}

return sb.ToString();

string GetPath(Transform t)
{
    var parts = new List<string>();
    while (t != null) { parts.Add(t.name); t = t.parent; }
    parts.Reverse();
    return string.Join("/", parts.Take(4));
}

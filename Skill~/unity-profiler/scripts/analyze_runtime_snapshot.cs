using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;

using System;
using System.Reflection;
using System.Text;
using System.Linq;

var sb = new StringBuilder();

sb.AppendLine("=== Memory Overview ===");
sb.AppendLine($"Allocated: {Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F2} MB");
sb.AppendLine($"Reserved:  {Profiler.GetTotalReservedMemoryLong() / 1048576f:F2} MB");
sb.AppendLine($"Unused:    {Profiler.GetTotalUnusedReservedMemoryLong() / 1048576f:F2} MB");
sb.AppendLine($"Mono Used: {Profiler.GetMonoUsedSizeLong() / 1048576f:F2} MB");
sb.AppendLine($"Mono Heap: {Profiler.GetMonoHeapSizeLong() / 1048576f:F2} MB");
sb.AppendLine($"GfxDriver: {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f:F2} MB");

var unityStatsType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.UnityStats");
Func<string, object> stat = name => unityStatsType.GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
float statFrameMs = Convert.ToSingle(stat("frameTime")) * 1000f;
float statRenderMs = Convert.ToSingle(stat("renderTime")) * 1000f;
long statScreenBytes = Convert.ToInt64(stat("screenBytes"));
long statRtBytes = Convert.ToInt64(stat("renderTextureBytes"));

sb.AppendLine();
sb.AppendLine("=== UnityStats GameView ===");
sb.AppendLine($"CPU: main {statFrameMs:F2}ms render thread {statRenderMs:F2}ms");
sb.AppendLine($"Batches: {stat("batches")} Saved by batching: dynamic {stat("dynamicBatchedDrawCalls")} static {stat("staticBatchedDrawCalls")} instanced {stat("instancedBatchedDrawCalls")}");
sb.AppendLine($"DrawCalls: {stat("drawCalls")} SetPass: {stat("setPassCalls")} Shadow casters: {stat("shadowCasters")}");
sb.AppendLine($"Tris: {stat("triangles")} Verts: {stat("vertices")}");
sb.AppendLine($"Screen: {stat("screenRes")} {statScreenBytes / 1048576f:F2} MB");
sb.AppendLine($"RenderTextures: count={stat("renderTextureCount")} memory={statRtBytes / 1048576f:F2} MB changes={stat("renderTextureChanges")}");
sb.AppendLine($"Texture Memory: count={stat("usedTextureCount")} memory={Convert.ToInt64(stat("usedTextureMemorySize")) / 1048576f:F2} MB");
sb.AppendLine($"VBO: count={stat("vboTotal")} memory={Convert.ToInt64(stat("vboTotalBytes")) / 1048576f:F2} MB uploads={stat("vboUploads")} uploadBytes={Convert.ToInt64(stat("vboUploadBytes")) / 1024f:F2} KB");
sb.AppendLine($"IB uploads: count={stat("ibUploads")} uploadBytes={Convert.ToInt64(stat("ibUploadBytes")) / 1024f:F2} KB");
sb.AppendLine($"Visible skinned meshes: {stat("visibleSkinnedMeshes")}");
sb.AppendLine($"Animation components playing: {stat("animationComponentsPlaying")}");
sb.AppendLine($"Animator components playing: {stat("animatorComponentsPlaying")}");
sb.AppendLine($"Audio: level={Convert.ToSingle(stat("audioLevel")):F4} clipping={Convert.ToSingle(stat("audioClippingAmount")):F4} dsp={Convert.ToSingle(stat("audioDSPLoad")) * 100f:F2}% stream={Convert.ToSingle(stat("audioStreamLoad")) * 100f:F2}%");

var textures = Resources.FindObjectsOfTypeAll<Texture>().Where(x => x != null && !(x is RenderTexture)).ToList();
var rts = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(x => x != null).ToList();
var mats = Resources.FindObjectsOfTypeAll<Material>().Where(x => x != null).ToList();
var meshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(x => x != null).ToList();

long texBytes = textures.Sum(x => Profiler.GetRuntimeMemorySizeLong(x));
long rtBytes = rts.Sum(x => Profiler.GetRuntimeMemorySizeLong(x));
long matBytes = mats.Sum(x => Profiler.GetRuntimeMemorySizeLong(x));
long meshBytes = meshes.Sum(x => Profiler.GetRuntimeMemorySizeLong(x));

sb.AppendLine();
sb.AppendLine("=== Object Memory ===");
sb.AppendLine($"Texture:       count={textures.Count} memory={texBytes / 1048576f:F2} MB");
sb.AppendLine($"RenderTexture: count={rts.Count} memory={rtBytes / 1048576f:F2} MB");
sb.AppendLine($"Material:      count={mats.Count} memory={matBytes / 1048576f:F2} MB");
sb.AppendLine($"Mesh:          count={meshes.Count} memory={meshBytes / 1048576f:F2} MB");

sb.AppendLine();
sb.AppendLine("Top RenderTextures:");
foreach (var rt in rts.OrderByDescending(x => Profiler.GetRuntimeMemorySizeLong(x)).Take(10))
    sb.AppendLine($"  {Profiler.GetRuntimeMemorySizeLong(rt) / 1048576f:F2} MB {rt.width}x{rt.height} {rt.name}");

sb.AppendLine();
sb.AppendLine("Top Textures:");
foreach (var tex in textures.OrderByDescending(x => Profiler.GetRuntimeMemorySizeLong(x)).Take(10))
    sb.AppendLine($"  {Profiler.GetRuntimeMemorySizeLong(tex) / 1048576f:F2} MB {tex.GetType().Name} {tex.width}x{tex.height} {tex.name}");

var canvases = Resources.FindObjectsOfTypeAll<Canvas>().Where(x => x != null).ToList();
var graphics = Resources.FindObjectsOfTypeAll<Graphic>().Where(x => x != null).ToList();
var rawImages = Resources.FindObjectsOfTypeAll<RawImage>().Where(x => x != null).ToList();
var textType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TMPro.TMP_Text")).FirstOrDefault(t => t != null);
var texts = textType == null ? new List<Component>() : Resources.FindObjectsOfTypeAll(textType).OfType<Component>().ToList();
if (textType == null) warnings.Add("TextMeshPro 未加载，文本对象统计不可用");
var canvasRenderers = Resources.FindObjectsOfTypeAll<CanvasRenderer>().Where(x => x != null).ToList();

sb.AppendLine();
sb.AppendLine("=== UI Objects ===");
sb.AppendLine($"Canvas:         total={canvases.Count} active={canvases.Count(x => x.gameObject.activeInHierarchy)}");
sb.AppendLine($"Graphic:        total={graphics.Count} active={graphics.Count(x => x.gameObject.activeInHierarchy)} raycastTarget={graphics.Count(x => x.raycastTarget)}");
sb.AppendLine($"RawImage:       total={rawImages.Count} active={rawImages.Count(x => x.gameObject.activeInHierarchy)} renderTexture={rawImages.Count(x => x.texture is RenderTexture)}");
sb.AppendLine($"TMP_Text:       total={texts.Count} active={texts.Count(x => x.gameObject.activeInHierarchy)}");
sb.AppendLine($"CanvasRenderer: total={canvasRenderers.Count} active={canvasRenderers.Count(x => x.gameObject.activeInHierarchy)}");

var activeCanvases = canvases.Where(x => x.gameObject.activeInHierarchy).OrderBy(x => x.sortingLayerID).ThenBy(x => x.sortingOrder).ThenBy(x => x.name).ToList();
sb.AppendLine();
sb.AppendLine("=== UI Layers ===");
foreach (var canvas in activeCanvases.Take(20))
{
    string layer = SortingLayer.IDToName(canvas.sortingLayerID);
    int graphicCount = graphics.Count(x => x.gameObject.activeInHierarchy && x.canvas == canvas);
    sb.AppendLine($"  order={canvas.sortingOrder} layer={layer} mode={canvas.renderMode} override={canvas.overrideSorting} graphics={graphicCount} {canvas.name}");
}

var activeGraphics = graphics.Where(x => x.gameObject.activeInHierarchy && x.canvas != null).ToList();
var batchKeys = activeGraphics.GroupBy(x =>
{
    var mat = x.materialForRendering;
    var tex = x.mainTexture;
    string shader = mat != null && mat.shader != null ? mat.shader.name : "<none>";
    string matName = mat != null ? mat.name : "<none>";
    string texName = tex != null ? tex.name : "<none>";
    return x.canvas.name + "|" + shader + "|" + matName + "|" + texName;
}).OrderByDescending(x => x.Count()).Take(12);

sb.AppendLine();
sb.AppendLine("=== UI Batch Groups Estimate ===");
sb.AppendLine($"Active Graphics: {activeGraphics.Count}");
foreach (var g in batchKeys)
{
    var first = g.First();
    var mat = first.materialForRendering;
    var tex = first.mainTexture;
    string shader = mat != null && mat.shader != null ? mat.shader.name : "<none>";
    string matName = mat != null ? mat.name : "<none>";
    string texName = tex != null ? tex.name : "<none>";
    sb.AppendLine($"  {g.Count()} canvas={first.canvas.name} shader={shader} mat={matName} tex={texName}");
}

sb.AppendLine();
sb.AppendLine("Active RawImages with RenderTexture:");
foreach (var raw in rawImages.Where(x => x.gameObject.activeInHierarchy && x.texture is RenderTexture).Take(10))
{
    var rt = (RenderTexture)raw.texture;
    sb.AppendLine($"  {rt.width}x{rt.height} {raw.name}");
}

var psr = Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>().Where(x => x != null).ToList();
sb.AppendLine();
sb.AppendLine("=== Particle Renderers ===");
sb.AppendLine($"ParticleSystemRenderer: total={psr.Count} active={psr.Count(x => x.gameObject.activeInHierarchy)}");
foreach (var g in psr.Where(x => x.sharedMaterial != null).GroupBy(x => x.sharedMaterial.shader != null ? x.sharedMaterial.shader.name : "<missing>").OrderByDescending(x => x.Count()).Take(8))
    sb.AppendLine($"  {g.Count()} {g.Key}");

return sb.ToString();

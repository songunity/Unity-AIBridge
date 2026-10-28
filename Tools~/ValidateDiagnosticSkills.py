"""验证分发诊断脚本的 Unity 语义编译与无采集前置分支。"""
import json
from pathlib import Path
import re


def compose(root, skill, name, options=None, compile_only=False):
    directory = root / "Skill~" / skill / "scripts"
    source = (directory / name).read_text(encoding="utf-8-sig")
    support = directory / "_support.cs"
    if support.exists():
        source += "\n" + support.read_text(encoding="utf-8-sig")
    pattern = r"(?m)^using (?:static )?[A-Za-z_][A-Za-z0-9_.]*;[ \t]*$"
    usings = re.findall(pattern, source)
    source = re.sub(pattern, "", source)
    if compile_only:
        source = "return DiagnosticResult(\"completed\", \"semantic compile\");\n" + source
    common = (root / "Skill~/diagnostics-common/_common.cs").read_text(encoding="utf-8-sig")
    return "\n".join(usings) + "\n" + common.replace("__DIAGNOSTIC_BODY__", source).replace("__OPTIONS_JSON__", json.dumps(json.dumps(options or {})))


def validate(call, root, project):
    compiled = []
    errors = []
    for skill in ("unity-profiler", "frame-debugger", "memory-profiler", "yooasset-debugger"):
        for script in sorted((root / "Skill~" / skill / "scripts").glob("*.cs")):
            if script.name.startswith("_"):
                continue
            result = call("CodeExecuteCommand_Execute", {"code": compose(root, skill, script.name, compile_only=True)}, expected=None)
            if not result.get("success"):
                errors.append({"script": skill + "/" + script.name, "error": result.get("error")})
                continue
            assert result["data"]["returnValue"]["status"] == "completed", result
            compiled.append(skill + "/" + script.name)
    assert not errors, errors
    for skill, name in [("memory-profiler", "snapshot_compare.cs"), ("frame-debugger", "analyze_frame_debugger.cs"), ("yooasset-debugger", "yoo_snapshot_compare.cs")]:
        result = call("CodeExecuteCommand_Execute", {"code": compose(root, skill, name)})
        assert result["data"]["returnValue"]["status"] == "blocked", result
    capture = project / ".aibridge/diagnostics/escaped-frame.json"
    capture.parent.mkdir(parents=True, exist_ok=True)
    capture.write_text(json.dumps({"eventCount": 2, "capturedCount": 1, "events": [{"type": "Draw", "shader": 'A {"quoted"}', "drawCalls": 2}]}), encoding="utf-8")
    result = call("CodeExecuteCommand_Execute", {"code": compose(root, "frame-debugger", "analyze_frame_debugger.cs", {"capture": str(capture)})})
    assert result["data"]["returnValue"]["warnings"], result
    # 只在隔离工程采样并恢复全部录制开关。
    call("CodeExecuteCommand_Execute", {"code": '''using UnityEditorInternal;
bool enabled = ProfilerDriver.enabled, editor = ProfilerDriver.profileEditor;
try {
    ProfilerDriver.profileEditor = true; ProfilerDriver.enabled = true;
    for (int i = 0; i < 20; i++) {
        UnityEngine.Profiling.Profiler.BeginSample("AIBridge.Diagnostics.Marker");
        try { System.GC.KeepAlive(new byte[1024]); } finally { UnityEngine.Profiling.Profiler.EndSample(); }
        await System.Threading.Tasks.Task.Delay(50);
    }
    return true;
} finally { ProfilerDriver.enabled = enabled; ProfilerDriver.profileEditor = editor; }'''})
    result = call("CodeExecuteCommand_Execute", {"code": compose(root, "unity-profiler", "inspect_profiler.cs", {"details": True, "maxSamples": 50000, "maxDetails": 100, "budgetMs": 5000})}, options=("--timeout", "30000"))
    inspection = result["data"]["returnValue"]
    assert inspection["status"] in ("completed", "partial"), inspection
    assert inspection["summary"]["samples"] > 0 and inspection["summary"]["threads"] > 0, inspection
    available = call("CodeExecuteCommand_Execute", {"code": 'using System.Linq; return System.AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Unity.MemoryProfiler.Editor");'})
    memory = {"executed": False, "reason": "optional Memory Profiler package absent"}
    if available["data"]["returnValue"]:
        result = call("CodeExecuteCommand_Execute", {"code": compose(root, "memory-profiler", "snapshot_take.cs")}, options=("--timeout", "150000"))
        taken = result["data"]["returnValue"]
        assert taken["status"] == "completed", taken
        snapshot = next(path for path in taken["artifacts"] if path.endswith(".snap"))
        for name, options in [("snapshot_load.cs", {"snapshot": snapshot}),
                              ("managed_analyze.cs", {"snapshot": snapshot}),
                              ("snapshot_details.cs", {"snapshot": snapshot, "nativeIndex": 0, "depth": 1, "maxNodes": 10}),
                              ("snapshot_compare.cs", {"snapshot": snapshot, "baseline": snapshot})]:
            result = call("CodeExecuteCommand_Execute", {"code": compose(root, "memory-profiler", name, options)}, options=("--timeout", "150000"))
            assert result["data"]["returnValue"]["status"] == "completed", result
        memory = {"executed": True, "snapshot": snapshot, "nativeManagedReferencesAndCompare": True}
    return {"passed": True, "compiledScripts": compiled, "preflight": True, "escapedJson": True, "profiler": inspection["summary"], "memory": memory}

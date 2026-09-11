"""仅在仓库 Temp/protocol-validation 隔离工程验证 Editor 协议，不连接业务项目。"""
import argparse
import json
import statistics
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "Temp/protocol-validation/UnityProject"
CLI = PROJECT / ".aibridge/cli/AIBridgeCLI.exe"
REPORT = ROOT / "Temp/protocol-validation/results.json"
records = []


def call(command, parameters=None, expected=True, options=()):
    args = [str(CLI), command]
    if command == "command":
        args.append(options[0])
        options = options[1:]
    args += ["--raw", "--timeout", "15000", *options]
    if parameters is not None:
        args.append("--stdin")
    started = time.perf_counter()
    entry = {"command": command, "parameters": parameters, "options": list(options)}
    records.append(entry)
    try:
        process = subprocess.run(args, input=json.dumps(parameters) if parameters is not None else None,
                                 capture_output=True, text=True, encoding="utf-8", timeout=55)
        entry.update({"exitCode": process.returncode, "stdout": process.stdout, "stderr": process.stderr})
        result = json.loads(process.stdout)
        entry["result"] = result
        del entry["stdout"]
    except subprocess.TimeoutExpired as exc:
        entry["error"] = str(exc)
        raise
    finally:
        entry["elapsedMs"] = round((time.perf_counter() - started) * 1000, 2)
        REPORT.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    assert result["protocolVersion"] == 2, result
    if expected is not None:
        assert result["success"] == expected, result
        assert (process.returncode == 0) == expected, (process.returncode, result)
    return result


def code(body, expected=True, **kwargs):
    return call("CodeExecuteCommand_Execute", {"code": body, **kwargs}, expected)


def query(action, command_id, expected=True, wait=False):
    return call("command", expected=expected, options=(action, "--id", command_id, *( ("--wait",) if wait else () )))


def main():
    if not __debug__:
        raise RuntimeError("验证脚本不能使用 python -O，断言必须启用。")
    assert CLI.is_file(), "先通过 Tools~/Test.ps1 -IncludeEditor 准备隔离工程与 CLI"
    metadata = json.loads((PROJECT / ".aibridge/editor-instance.json").read_text(encoding="utf-8"))
    assert Path(metadata["projectRoot"]).resolve() == PROJECT.resolve(), "禁止连接隔离工程以外的 Unity"
    assert metadata["protocolVersion"] == 2, "隔离工程必须加载协议 2 的仓库代码"
    call("EditorCommand_GetState")
    typed = code('UnityEngine.Debug.LogWarning("expected warning"); return new {number=42,flag=true,text="Exception: data",empty=(string)null,items=new[]{1,2}};')
    assert typed["data"]["returnValue"] == {"number": 42, "flag": True, "text": "Exception: data", "empty": None, "items": [1, 2]}
    assert typed["data"]["logs"][0]["level"] == "Warning"
    failure = code('UnityEngine.Debug.Log("before failure"); throw new System.Exception("expected failure");', False)
    assert failure["errorCode"] == "EXECUTION_FAILED" and failure["data"]["logs"]
    assert code('return new {obj=UnityEngine.ScriptableObject.CreateInstance<UnityEngine.ScriptableObject>()};', False)["errorCode"] == "UNSUPPORTED_RETURN_VALUE"
    assert code('var list=new System.Collections.Generic.List<object>(); list.Add(list); return list;', False)["errorCode"] == "UNSUPPORTED_RETURN_VALUE"
    body = 'return new {fresh=System.Guid.NewGuid().ToString()};'
    first, second = code(body), code(body)
    assert first["data"]["returnValue"] != second["data"]["returnValue"]
    assert second["data"]["timings"]["cacheHit"]

    commands = [{"type": "CodeExecuteCommand_Execute", "params": {"code": "return 1;"}},
                {"type": "MissingCommand", "params": {}},
                {"type": "CodeExecuteCommand_Execute", "params": {"code": "return 3;"}}]
    stopped = call("Batch", {"commands": commands}, False)
    assert stopped["data"]["skippedCount"] == 1 and stopped["data"]["successCount"] == 1
    continued = call("Batch", {"commands": commands, "stopOnError": False}, False)
    assert continued["data"]["successCount"] == 2 and continued["data"]["failureCount"] == 1
    assert call("Batch", {"commands": [commands[0], commands[2]]})["data"]["successCount"] == 2

    late = call("CodeExecuteCommand_Execute", {"code": "await System.Threading.Tasks.Task.Delay(1000); return 77;"}, False, ("--timeout", "100"))
    assert late["errorCode"] == "WAIT_TIMEOUT"
    complete = query("result", late["id"], wait=True)
    assert complete["data"]["returnValue"] == 77
    assert query("result", late["id"])["data"]["returnValue"] == 77
    assert query("status", late["id"])["status"] == "completed"
    timeout = code('await System.Threading.Tasks.Task.Delay(400); return 12;', False, executionTimeoutMs=20)
    assert timeout["status"] == "unknown" and timeout["errorCode"] == "EXECUTION_WAIT_TIMEOUT"
    assert query("status", timeout["id"])["status"] == "unknown"
    assert query("status", "absent_validation_id")["status"] == "unknown"

    # 同组只读查询做三次对照，不将业务时间与传输时间混在一起。
    group = [{"type": "CodeExecuteCommand_Execute", "params": {"code": "return 42;"}}] * 3
    code("return 42;")
    singles, batches = [], []
    for _ in range(3):
        started = time.perf_counter()
        for _ in group:
            code("return 42;")
        singles.append(round((time.perf_counter() - started) * 1000, 2))
        started = time.perf_counter()
        call("Batch", {"commands": group})
        batches.append(round((time.perf_counter() - started) * 1000, 2))
    performance = {"singleMs": singles, "batchMs": batches, "singleMedianMs": statistics.median(singles), "batchMedianMs": statistics.median(batches)}

    # 域重载时仍在等待的请求不能被标为失败、成功或自动重放。
    pending = call("CodeExecuteCommand_Execute", {"code": "await System.Threading.Tasks.Task.Delay(60000); return 99;"}, options=("--no-wait",))
    deadline = time.monotonic() + 10
    while query("status", pending["id"])["status"] != "running":
        assert time.monotonic() < deadline, "pending command did not start"
        time.sleep(0.1)
    metadata = PROJECT / ".aibridge/editor-instance.json"
    old_session = json.loads(metadata.read_text())["sessionId"]
    code('UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorUtility.RequestScriptReload(); return "reload requested";')
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        if metadata.exists():
            current = json.loads(metadata.read_text())
            if current["sessionId"] != old_session:
                break
        time.sleep(0.25)
    else:
        raise AssertionError("domain reload did not finish")
    assert query("status", pending["id"])["status"] == "unknown"
    assert call("CodeExecuteCommand_CacheStatus")["data"]["entries"] == 0
    call("Compile", options=("--timeout", "45000"))
    invalid = subprocess.run([str(CLI), "-invalid", "--raw"], capture_output=True, text=True, encoding="utf-8")
    assert invalid.returncode != 0 and json.loads(invalid.stdout)["errorCode"] == "CLI_ERROR"
    runtime = subprocess.run([str(CLI), "runtime", "list_targets", "--raw"], capture_output=True, text=True, encoding="utf-8")
    assert runtime.returncode == 0 and "protocolVersion" not in json.loads(runtime.stdout)
    summary = {"passed": True, "checks": len(records), "rawErrors": True, "runtimeFormatUnchanged": True, "performance": performance}
    records.append(summary)
    REPORT.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, default=REPORT, help="结果文件；统一入口按运行目录保存")
    args = parser.parse_args()
    REPORT = args.report.resolve()
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text("[]", encoding="utf-8")
    try:
        main()
    except Exception as exc:
        records.append({"passed": False, "error": str(exc), "checks": len(records)})
        REPORT.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
        raise

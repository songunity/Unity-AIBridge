"""通过 AIBridge 执行诊断脚本，保留原命令 ID，输出结构化摘要。"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("script", help="scripts 目录中的脚本名，不含 .cs 亦可")
    parser.add_argument("--cli", required=True, type=Path)
    parser.add_argument("--options", default="{}", help="JSON 参数对象")
    parser.add_argument("--options-file", type=Path)
    parser.add_argument("--timeout", type=int, default=300000)
    parser.add_argument("--scripts", type=Path, default=Path(__file__).resolve().parent)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("timeout 必须大于零")
    options = json.loads(args.options_file.read_text(encoding="utf-8-sig") if args.options_file else args.options)
    if not isinstance(options, dict):
        parser.error("options 必须为对象")
    directory = args.scripts.resolve()
    script = (directory / (args.script if args.script.endswith(".cs") else args.script + ".cs")).resolve()
    if script.parent != directory or not script.is_file() or script.name.startswith("_"):
        parser.error("脚本必须位于指定 scripts 目录")
    cli = args.cli.resolve(strict=True)
    project = next((p for p in cli.parents if (p / "Assets").is_dir() and (p / "ProjectSettings").is_dir()), None)
    if project is None:
        parser.error("CLI 必须位于目标 Unity 项目内")
    common = directory / "_common.cs"
    if not common.exists():
        common = Path(__file__).resolve().with_name("_common.cs")
    source = script.read_text(encoding="utf-8-sig")
    support = directory / "_support.cs"
    if support.exists():
        source += "\n" + support.read_text(encoding="utf-8-sig")
    # 分发脚本的 using 全部独占一行；保留空行使诊断行号稳定。
    usings = re.findall(r"(?m)^using (?:static )?[A-Za-z_][A-Za-z0-9_.]*;[ \t]*$", source)
    source = re.sub(r"(?m)^using (?:static )?[A-Za-z_][A-Za-z0-9_.]*;[ \t]*$", "", source)
    code = "\n".join(usings) + "\n" + common.read_text(encoding="utf-8-sig")
    code = code.replace("__DIAGNOSTIC_BODY__", source)
    code = code.replace("__OPTIONS_JSON__", json.dumps(json.dumps(options, ensure_ascii=False, sort_keys=True), ensure_ascii=False))
    probe_dir = project / ".aibridge/code"
    probe_dir.mkdir(parents=True, exist_ok=True)
    probe = probe_dir / ("diagnostic_" + hashlib.sha256(code.encode()).hexdigest() + ".cs")
    if not probe.exists():
        probe.write_text(code, encoding="utf-8")
    def invoke(arguments):
        process = subprocess.run([str(cli), *arguments, "--raw"], capture_output=True, text=True, encoding="utf-8", timeout=(args.timeout / 1000) + 15)
        return json.loads(process.stdout)
    # 先获取 ID，宿主进程或等待超时都不会导致写任务被自动重放。
    submitted = invoke(["CodeExecuteCommand_Execute", "--file", str(probe), "--executionTimeoutMs", str(args.timeout), "--no-wait"])
    if not submitted.get("success"):
        print(json.dumps(submitted, ensure_ascii=False)); return 1
    command_id = submitted["id"]
    print("已提交诊断，命令 ID=" + command_id, file=sys.stderr)
    try:
        result = invoke(["command", "result", "--id", command_id, "--wait", "--timeout", str(args.timeout)])
    except (subprocess.TimeoutExpired, json.JSONDecodeError):
        print(json.dumps({"status": "unknown", "commandId": command_id, "error": "结果未确认；查询原 ID，禁止重放采集。"}, ensure_ascii=False)); return 2
    data = (result.get("data") or {}).get("returnValue")
    if not result.get("success"):
        print(json.dumps(result, ensure_ascii=False)); return 1
    print(json.dumps({"commandId": command_id, "diagnostic": data}, ensure_ascii=False))
    return 0 if isinstance(data, dict) and data.get("status") == "completed" else 2


if __name__ == "__main__":
    raise SystemExit(main())

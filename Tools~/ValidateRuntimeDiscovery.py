"""Isolated Editor regression using loopback HTTP devices only."""
import json
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def validate(call, project):
    cache = project / ".aibridge/runtime/discovery-cache.json"
    cache.parent.mkdir(parents=True, exist_ok=True)
    previous = cache.read_bytes() if cache.exists() else None
    servers = []
    active = 0
    peak = 0
    guard = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def do_GET(self):
            nonlocal active, peak
            with guard:
                active += 1
                peak = max(peak, active)
            try:
                time.sleep(2 if self.server.kind == "slow" else 0.15)
                self.send_response(401 if self.server.kind == "auth" else 200)
                self.end_headers()
                self.wfile.write(json.dumps({"targetId": self.server.identity, "platform": "Test"}).encode())
            except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
                pass
            finally:
                with guard:
                    active -= 1

    prefix = 'var type = typeof(AIBridge.Editor.AIBridge).Assembly.GetType("AIBridge.Editor.AIBridgeRuntimeBridgeEditorUtility"); '

    def code(body):
        result = call("CodeExecuteCommand_Execute", {"code": prefix + body})
        return result["data"]["returnValue"]

    try:
        targets = []
        for index, kind in enumerate(["ok"] * 4 + ["auth", "slow"]):
            server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
            server.daemon_threads = True
            server.kind, server.identity = kind, "refresh-test-" + str(index)
            servers.append(server)
            threading.Thread(target=server.serve_forever, daemon=True).start()
            targets.append({"targetId": server.identity, "url": "http://127.0.0.1:" + str(server.server_port),
                            "lastSeenUtc": datetime.now(timezone.utc).isoformat(), "reachable": True})
        original = json.dumps({"targets": targets}).encode()
        cache.write_bytes(original)
        result = code('var result = type.GetMethod("DiscoverLanTargets").Invoke(null, new object[] {500, 27183, "", false, true}); '
                      'System.AppDomain.CurrentDomain.SetData("AIBridge.RefreshTest", result); '
                      'return new { success = result.GetType().GetField("Success").GetValue(result), count = result.GetType().GetField("Count").GetValue(result) };')
        assert result == {"success": True, "count": 6}, result
        assert 2 <= peak <= 4, peak
        assert cache.read_bytes() == original, "Background task wrote the cache before completion was accepted"
        code('var result = System.AppDomain.CurrentDomain.GetData("AIBridge.RefreshTest"); '
             '((System.Action)result.GetType().GetField("Publish", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(result))(); return true;')
        saved = json.loads(cache.read_text(encoding="utf-8-sig"))["targets"]
        assert len(saved) == 6 and sum(t["reachable"] for t in saved) == 4
        assert any((t.get("healthError") or "").startswith("HTTP 401") for t in saved)
        assert next(t for t in saved if t["targetId"] == "refresh-test-5")["healthError"]
        assert code('return ((System.Collections.ICollection)type.GetMethod("ListDiscoveredTargets").Invoke(null, null)).Count;') == 6
        cache.write_text("{broken", encoding="utf-8")
        assert code('return new { count = ((System.Collections.ICollection)type.GetMethod("ListDiscoveredTargets").Invoke(null, null)).Count, '
                    'error = type.GetProperty("DiscoveryCacheError").GetValue(null) != null };') == {"count": 6, "error": True}
        cache.write_bytes(original)
        assert code('var window = UnityEngine.ScriptableObject.CreateInstance<AIBridge.Editor.AIBridgeRuntimeSettingsWindow>(); '
                    'var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic; '
                    'var result = System.AppDomain.CurrentDomain.GetData("AIBridge.RefreshTest"); '
                    'var factory = typeof(System.Threading.Tasks.Task).GetMethod("FromResult").MakeGenericMethod(result.GetType()); '
                    'window.GetType().GetField("_pendingRefresh", flags).SetValue(window, factory.Invoke(null, new[] {result})); '
                    'window.GetType().GetMethod("OnDisable", flags).Invoke(window, null); '
                    'var cleared = window.GetType().GetField("_pendingRefresh", flags).GetValue(window) == null; '
                    'UnityEngine.Object.DestroyImmediate(window); return cleared;') is True
        assert cache.read_bytes() == original, "Closed window published a stale result"
        return {"passed": True, "devices": 6, "maxConcurrentChecks": peak,
                "authAndTimeout": True, "deferredPublication": True, "corruptCachePreserved": True, "closedWindow": True}
    finally:
        for server in servers:
            server.shutdown()
            server.server_close()
        if previous is None:
            cache.unlink(missing_ok=True)
        else:
            cache.write_bytes(previous)

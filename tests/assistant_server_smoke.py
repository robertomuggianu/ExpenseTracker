"""Exercise the actual HTTP host against an in-memory Ollama fixture. No real expenses or models needed."""
import concurrent.futures
import http.client
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = Path(__file__).resolve().parents[1]
KEY = "0123456789abcdef" * 4  # Fixture credential, never a real user key.
state = {"ready": True, "truncated": False, "hold": False}
started = threading.Event()
release = threading.Event()


class OllamaFixture(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def reply(self, body):
        encoded = json.dumps(body).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def do_GET(self):
        assert self.path == "/api/tags"
        self.reply({"models": [{"name": "qwen2.5:3b"}] if state["ready"] else []})

    def do_POST(self):
        assert self.path == "/api/chat"
        if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
            parts = []
            while True:
                size = int(self.rfile.readline().strip().split(b";", 1)[0], 16)
                if size == 0:
                    while self.rfile.readline().strip():
                        pass
                    break
                parts.append(self.rfile.read(size))
                assert self.rfile.read(2) == b"\r\n"
            body = json.loads(b"".join(parts))
        else:
            body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        assert body["model"] == "qwen2.5:3b" and body["stream"] is False
        assert body["think"] is False
        assert len(body["messages"]) == 2
        started.set()
        if state["hold"]:
            release.wait(10)
        self.reply({"done": True, "done_reason": "length" if state["truncated"] else "stop",
                    "message": {"content": "Le spese sono aumentate di 60 euro, pari al 20%."}})


def request(port, method, path, body=None, key=KEY):
    connection = http.client.HTTPConnection("127.0.0.1", port, timeout=15)
    try:
        headers = {"Content-Type": "application/json"}
        if key is not None:
            headers["X-Assistant-Key"] = key
        connection.request(method, path, body, headers)
        response = connection.getresponse()
        return response.status, response.read().decode()
    finally:
        connection.close()


def main():
    fixture = ThreadingHTTPServer(("127.0.0.1", 0), OllamaFixture)
    threading.Thread(target=fixture.serve_forever, daemon=True).start()
    with socket.socket() as reservation:
        reservation.bind(("127.0.0.1", 0))
        port = reservation.getsockname()[1]
    env = os.environ.copy()
    env.update(Assistant__AccessKey=KEY, Assistant__Model="qwen2.5:3b",
               Assistant__OllamaAddress=f"http://127.0.0.1:{fixture.server_port}/")
    dll = ROOT / "src/ExpenseTracker.AssistantServer/bin/Debug/net10.0/ExpenseTracker.AssistantServer.dll"
    if not dll.exists():
        raise RuntimeError("Build the AssistantServer in Debug before running this smoke test.")
    with tempfile.TemporaryFile(mode="w+b") as log:
        process = subprocess.Popen(["dotnet", str(dll), "--urls", f"http://127.0.0.1:{port}"],
                                   env=env, cwd=ROOT, stdout=log, stderr=log,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        try:
            for _ in range(100):
                if process.poll() is not None:
                    log.seek(0)
                    raise RuntimeError(log.read().decode(errors="replace"))
                try:
                    status, _ = request(port, "GET", "/health")
                    if status == 200:
                        break
                except (ConnectionError, OSError):
                    pass
                time.sleep(0.1)
            else:
                raise RuntimeError("Assistant server did not start.")
            assert request(port, "GET", "/health", key=None)[0] == 401
            assert request(port, "GET", "/health", key="wrong")[0] == 401
            state["ready"] = False
            assert request(port, "GET", "/health")[0] == 503
            state["ready"] = True
            period = {"from": "2026-10-01", "through": "2026-10-09", "income": 1000,
                      "expense": 360, "movementCount": 2, "categories": [{"name": "Cibo", "expense": 360}]}
            previous = dict(period, **{"from": "2026-09-01", "through": "2026-09-09", "expense": 300,
                                      "categories": [{"name": "Cibo", "expense": 300}]})
            report = {"kind": 1, "currency": "EUR", "current": period, "previous": previous,
                      "expenseChange": 60, "expenseChangePercent": 20}
            body = json.dumps(report)
            status, result = request(port, "POST", "/reports", body)
            assert status == 200, (status, result)
            assert json.loads(result)["model"] == "qwen2.5:3b"
            assert request(port, "POST", "/reports", json.dumps(dict(report, expenseChange=99)))[0] == 400
            assert request(port, "POST", "/reports", "{")[0] == 400
            assert request(port, "POST", "/reports", " " * 40000)[0] == 413
            state["truncated"] = True
            assert request(port, "POST", "/reports", body)[0] == 502
            state["truncated"] = False
            state["hold"] = True
            started.clear()
            with concurrent.futures.ThreadPoolExecutor() as pool:
                pending = pool.submit(request, port, "POST", "/reports", body)
                assert started.wait(5)
                assert request(port, "POST", "/reports", body)[0] == 429
                release.set()
                assert pending.result()[0] == 200
            print("HTTP smoke passed: auth, health/model readiness, report, validation, size limit, truncated answer, concurrency.")
        finally:
            release.set()
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
            fixture.shutdown()
            fixture.server_close()


if __name__ == "__main__":
    main()

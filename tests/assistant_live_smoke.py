"""Optional end-to-end check with the locally installed Qwen2.5. Only invented facts are sent."""
import json
import os
import socket
import subprocess
import tempfile
import time
import sys
from assistant_server_smoke import KEY, ROOT, request

sys.stdout.reconfigure(encoding="utf-8")


def main():
    with socket.socket() as reservation:
        reservation.bind(("127.0.0.1", 0))
        port = reservation.getsockname()[1]
    env = os.environ.copy()
    env.update(Assistant__AccessKey=KEY, Assistant__Model="qwen2.5:3b",
               Assistant__OllamaAddress="http://127.0.0.1:11434/")
    dll = ROOT / "src/ExpenseTracker.AssistantServer/bin/Debug/net10.0/ExpenseTracker.AssistantServer.dll"
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
                raise RuntimeError("Assistant/Ollama not ready.")
            period = {"from": "2026-10-01", "through": "2026-10-09", "income": 1000,
                      "expense": 360, "movementCount": 3, "categories": [{"name": "Cibo", "expense": 360}]}
            previous = dict(period, **{"from": "2026-09-01", "through": "2026-09-09", "expense": 300,
                                      "categories": [{"name": "Cibo", "expense": 300}]})
            reports = [
                {"kind": 0, "currency": "EUR", "current": period, "previous": None,
                 "expenseChange": None, "expenseChangePercent": None},
                {"kind": 1, "currency": "EUR", "current": period, "previous": previous,
                 "expenseChange": 60, "expenseChangePercent": 20},
                {"kind": 1, "currency": "EUR", "current": period,
                 "previous": dict(previous, expense=0, income=0, movementCount=0, categories=[]),
                 "expenseChange": 360, "expenseChangePercent": None},
            ]
            for index, report in enumerate(reports, 1):
                start = time.monotonic()
                # Local models may need extra time to load on the first call.
                from http.client import HTTPConnection
                connection = HTTPConnection("127.0.0.1", port, timeout=150)
                try:
                    connection.request("POST", "/reports", json.dumps(report),
                                       {"X-Assistant-Key": KEY, "Content-Type": "application/json"})
                    response = connection.getresponse()
                    result = response.read().decode()
                    assert response.status == 200, (response.status, result)
                    content = json.loads(result)
                    assert content["model"] == "qwen2.5:3b" and content["text"].strip()
                    print(f"Report {index} ({time.monotonic() - start:.1f}s): {content['text']}", flush=True)
                finally:
                    connection.close()
            print("Live HTTP checks passed. Review the generated text for factual accuracy.")
        finally:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)


if __name__ == "__main__":
    main()

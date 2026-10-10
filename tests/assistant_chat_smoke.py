"""HTTP checks for the bounded chat protocol. --live uses Qwen with invented data only."""
import argparse
import json
import os
import socket
import subprocess
import tempfile
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from http.client import HTTPConnection
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.stdout.reconfigure(encoding="utf-8")
KEY = "0123456789abcdef" * 4
PLAN = dict(status="answer", period="current", operation="total", measure="expenses", category="", comparison="sameDays")
STATE = {"content": None, "truncated": False}
MODEL = {"name": "qwen3.5:9b"}

class Fixture(BaseHTTPRequestHandler):
    def log_message(self, *_): pass
    def reply(self, value):
        data = json.dumps(value).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)
    def do_GET(self): self.reply({"models": [{"name": MODEL["name"]}]})
    def do_POST(self):
        if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
            chunks = []
            while True:
                length = int(self.rfile.readline().strip(), 16)
                if not length:
                    self.rfile.readline()
                    break
                chunks.append(self.rfile.read(length)); self.rfile.read(2)
            body = json.loads(b"".join(chunks))
        else:
            body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        content = STATE["content"] or (json.dumps(PLAN) if "format" in body else "You spent 360.00 EUR this month.")
        self.reply(dict(done=True, done_reason="length" if STATE["truncated"] else "stop", message={"content": content}))

def call(port, path, body=None, key=KEY):
    connection = HTTPConnection("127.0.0.1", port, timeout=210)
    try:
        connection.request("POST" if body is not None else "GET", path,
                           json.dumps(body) if body is not None else None,
                           {"Content-Type": "application/json", "X-Assistant-Key": key})
        response = connection.getresponse()
        text = response.read().decode()
        return response.status, json.loads(text) if text else None
    finally: connection.close()

def main(live, boundary_only=False, period_regression=False, model="qwen3.5:9b"):
    MODEL["name"] = model
    fixture = None
    if not live:
        fixture = ThreadingHTTPServer(("127.0.0.1", 0), Fixture)
        threading.Thread(target=fixture.serve_forever, daemon=True).start()
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0)); port = s.getsockname()[1]
    env = os.environ.copy()
    env.update(Assistant__AccessKey=KEY, Assistant__Model=model,
               Assistant__OllamaAddress=f"http://127.0.0.1:{11434 if live else fixture.server_port}/")
    dll = ROOT / "src/ExpenseTracker.AssistantServer/bin/UiVerification/ExpenseTracker.AssistantServer.dll"
    with tempfile.TemporaryFile() as log:
        process = subprocess.Popen(["dotnet", str(dll), "--urls", f"http://127.0.0.1:{port}"], cwd=ROOT, env=env,
                                   stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            for _ in range(100):
                if process.poll() is not None:
                    log.seek(0); raise RuntimeError(log.read().decode(errors="replace"))
                try:
                    if call(port, "/health")[0] == 200: break
                except OSError: pass
                time.sleep(.1)
            else: raise RuntimeError("Host/Ollama not ready")
            cases = [
                ("How much did I spend this month?", None, (0, 1, 0, None)),
                ("And last month?", dict(period=0, operation=1, measure=0, category=None, comparison=0), (1, 1, 0, None)),
                ("Check the data for last month.", dict(period=0, operation=1, measure=0, category=None, comparison=0), (1, None, 0, None)),
                ("Compare my spending this month with last month.", None, (2, 1, 0, None)),
                ("How much did I spend on Cibo last month?", None, (1, 1, 0, "Cibo")),
                ("What is my daily average spending this month?", None, (0, 3, 0, None)),
                ("What was my largest expense this month?", None, (0, 5, 0, None)),
                ("How many income transactions did I record this month?", None, (0, 6, 1, None)),
                ("Which categories increased most compared with last month?", None, (2, 2, 0, None)),
            ] if live else [("How much did I spend this month?", None, (0, 1, 0, None))]
            if boundary_only: cases = []
            if period_regression:
                cases = [case for case in cases if case[0] in ("And last month?", "Check the data for last month.")]
            for question, context, expected in cases:
                start = time.monotonic()
                request = dict(question=question, today="2026-10-10", currency="EUR", categories=["Cibo", "Trasporti", "Stipendio"],
                               context=context, previousQuestion="How much did I spend this month?" if context else None)
                status, result = call(port, "/chat/interpret", request)
                assert status == 200, (status, result)
                plan = result.get("plan")
                print(f"{question} ({time.monotonic()-start:.1f}s): {result}", flush=True)
                assert result["status"] == 0 and plan, result
                assert (plan["period"], plan["measure"], plan["category"]) == (expected[0], expected[2], expected[3]), result
                if expected[1] is not None: assert plan["operation"] == expected[1], result
            evidence = dict(question="How much did I spend this month?", plan=dict(period=0, operation=1, measure=0, category=None, comparison=0),
                            currency="EUR", facts="Currency: EUR. Current month: 01/10/2026 – 10/10/2026. 2 transactions. Expenses: 360,00 EUR.",
                            calculatedAnswer="From 01/10/2026 to 10/10/2026 you spent 360,00 EUR.",
                            periods=[{"from":"2026-10-01", "through":"2026-10-10", "movementCount":2, "measureMovementCount":2}])
            if period_regression:
                evidence.update(question="Check the data for last month.", plan=dict(period=1, operation=1, measure=0, category=None, comparison=0),
                                facts="Currency: EUR. Previous month: 01/09/2026 – 30/09/2026. 3 transactions in scope; 3 expense transactions. Expenses: 300,00 EUR.",
                                calculatedAnswer="Previous month: 01/09/2026 – 30/09/2026. Expenses: 300,00 EUR.",
                                periods=[{"from":"2026-09-01", "through":"2026-09-30", "movementCount":3, "measureMovementCount":3}])
            status, answer = call(port, "/chat/answer", evidence)
            assert status == 200 and answer["text"], (status, answer)
            print(f"Answer: {answer}", flush=True)
            if period_regression:
                assert "300" in answer["text"] and "current month" not in answer["text"].lower(), answer
            if live and not period_regression:
                for question, expected_status in [("How much will I spend in November?", 2), ("How much did I spend on UnknownCategory?", 1)]:
                    status, result = call(port, "/chat/interpret", dict(question=question, today="2026-10-10", currency="EUR",
                                                                      categories=["Cibo", "Trasporti", "Stipendio"]))
                    assert status == 200 and result["status"] == expected_status, result
                    print(f"Boundary: {question}: {result['status']}", flush=True)
            elif not live:
                assert call(port, "/chat/interpret", {}, key="wrong")[0] == 401
                assert call(port, "/chat/interpret", dict(question="x", today="2026-10-10", currency="EUR", categories=[], context={"period":999,"operation":1,"measure":0,"comparison":0}))[0] == 400
                assert call(port, "/chat/answer", dict(evidence, calculatedAnswer="You spent 999 EUR."))[0] == 400
                assert call(port, "/chat/answer", dict(evidence, periods=None))[0] == 426
                STATE["truncated"] = True
                status, result = call(port, "/chat/answer", evidence)
                assert status == 200 and result["usedCalculatedAnswer"] and result["text"] == evidence["calculatedAnswer"]
                STATE["truncated"] = False; STATE["content"] = "You spent 999 EUR."
                assert call(port, "/chat/answer", evidence)[1]["usedCalculatedAnswer"]
                STATE["content"] = "Expenses 360.00 EUR. No expenses were recorded."
                assert call(port, "/chat/answer", evidence)[1]["usedCalculatedAnswer"]
                STATE["content"] = "{}"
                assert call(port, "/chat/interpret", dict(question="x", today="2026-10-10", currency="EUR", categories=[]))[0] == 502
            print("Chat HTTP checks passed.", flush=True)
        finally:
            process.terminate()
            try: process.wait(timeout=5)
            except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)
            if fixture: fixture.shutdown()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(); parser.add_argument("--live", action="store_true")
    parser.add_argument("--boundary-only", action="store_true")
    parser.add_argument("--period-regression", action="store_true")
    parser.add_argument("--model", default="qwen3.5:9b")
    args = parser.parse_args()
    main(args.live, args.boundary_only, args.period_regression, args.model)

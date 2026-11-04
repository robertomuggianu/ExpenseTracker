#!/usr/bin/env python3
"""Real iOS Simulator smoke against Appium/XCUITest; Python standard library only.

Requires a freshly created CI simulator. The simulator itself is never erased:
  python3 tests/ios_simulator_smoke.py --udid UUID --app /path/ExpenseTracker.app \
      --artifacts artifacts/ios-smoke --server http://127.0.0.1:4723

Driver extensions: https://appium.github.io/appium-xcuitest-driver/latest/reference/execute-methods/
No AI service, developer backdoor, database injection, or fixed-coordinate taps.
"""

import argparse
import base64
from contextlib import contextmanager
from datetime import datetime, timezone
from decimal import Decimal
import json
import os
from pathlib import Path
import plistlib
import re
import signal
import subprocess
import sys
import tempfile
import time
import traceback
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


ELEMENT = "element-6066-11e4-a52e-4f735466cecf"


def prebuild_wda(udid, artifacts, report):
    """Compile the pinned WDA outside Appium's server-readiness timeout."""
    modules = Path(__file__).resolve().parent / "ios-appium" / "node_modules"
    candidates = [modules / "appium-xcuitest-driver" / "node_modules" / "appium-webdriveragent",
                  modules / "appium-webdriveragent"]
    package = next((p for p in candidates if (p / "WebDriverAgent.xcodeproj").is_dir()), None)
    if package is None:
        raise FileNotFoundError("Pinned WebDriverAgent project missing; run npm ci in tests/ios-appium")
    # Keep large intermediate files outside uploaded artifacts; retain the complete build log.
    derived = Path(os.environ.get("RUNNER_TEMP", tempfile.gettempdir())) / ("expensetracker-wda-" + udid)
    derived.mkdir(parents=True, exist_ok=True)
    command = ["xcodebuild", "build-for-testing", "-project", str(package / "WebDriverAgent.xcodeproj"),
               "-scheme", "WebDriverAgentRunner", "-configuration", "Debug", "-sdk", "iphonesimulator",
               "-destination", f"platform=iOS Simulator,id={udid}", "-derivedDataPath", str(derived),
               "CODE_SIGNING_ALLOWED=NO", "RUN_CLANG_STATIC_ANALYZER=NO",
               "COMPILER_INDEX_STORE_ENABLE=NO", "GCC_TREAT_WARNINGS_AS_ERRORS=0"]
    record = {"command": command, "status": "running", "log": "wda-build.log", "derivedDataPath": str(derived)}
    report["wdaBuild"] = record
    started = time.monotonic()
    print("[iOS smoke] Prebuilding WebDriverAgent (see wda-build.log)", flush=True)
    with (artifacts / "wda-build.log").open("w", encoding="utf-8") as output:
        output.write(json.dumps(command) + "\n")
        output.flush()
        process = subprocess.Popen(command, cwd=package, stdout=output, stderr=subprocess.STDOUT,
                                   start_new_session=True)
        try:
            record["exitCode"] = process.wait(timeout=600)
        except subprocess.TimeoutExpired:
            record["status"] = "timeout"
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=5)
            raise TimeoutError("WebDriverAgent prebuild exceeded 600 seconds; see wda-build.log")
        finally:
            record["seconds"] = round(time.monotonic() - started, 2)
    if record["exitCode"]:
        record["status"] = "failed"
        raise RuntimeError(f"WebDriverAgent prebuild failed with exit {record['exitCode']}; see wda-build.log")
    products = derived / "Build" / "Products"
    if not list(products.glob("*.xctestrun")) or not (products / "Debug-iphonesimulator" / "WebDriverAgentRunner-Runner.app").is_dir():
        record["status"] = "failed"
        raise RuntimeError("WDA prebuild did not produce its simulator runner and xctestrun file")
    record["status"] = "passed"
    print(f"[iOS smoke] WebDriverAgent prebuild passed in {record['seconds']}s", flush=True)
    return derived


class ProtocolError(RuntimeError):
    def __init__(self, code, message):
        self.code = code
        super().__init__(f"{code}: {message}")


class Driver:
    def __init__(self, server, artifacts, deadline):
        self.server = server.rstrip("/")
        self.artifacts = artifacts
        self.deadline = deadline
        self.session = None
        self.events = artifacts / "commands.jsonl"

    def log(self, **event):
        event["time"] = datetime.now(timezone.utc).isoformat()
        with self.events.open("a", encoding="utf-8") as output:
            output.write(json.dumps(event, ensure_ascii=False) + "\n")

    def request(self, method, route, payload=None, timeout=30, diagnostic=False):
        remaining = self.deadline - time.monotonic()
        if not diagnostic and remaining <= 0:
            raise TimeoutError("The smoke scenario deadline expired")
        timeout = min(timeout, max(1, remaining)) if not diagnostic else min(timeout, 15)
        path = f"/session/{self.session}{route}" if self.session else route
        body = None if payload is None else json.dumps(payload).encode("utf-8")
        request = Request(self.server + path, data=body, method=method,
                          headers={"Content-Type": "application/json; charset=utf-8"})
        started = time.monotonic()
        try:
            try:
                with urlopen(request, timeout=timeout) as response:
                    result = json.loads(response.read().decode("utf-8"))
            except HTTPError as error:
                raw = error.read().decode("utf-8", errors="replace")
                try:
                    result = json.loads(raw)
                except ValueError:
                    raise ProtocolError(f"HTTP {error.code}", raw[:1500]) from error
            value = result.get("value", result)
            if isinstance(value, dict) and value.get("error"):
                raise ProtocolError(value["error"], value.get("message", "No message"))
            self.log(method=method, route=path, seconds=round(time.monotonic() - started, 3),
                     request=payload, status="ok")
            return value
        except (ProtocolError, URLError, TimeoutError, OSError) as error:
            self.log(method=method, route=path, seconds=round(time.monotonic() - started, 3),
                     request=payload, status="error", error=str(error))
            raise

    def start(self, udid, app, wda_derived_data):
        value = self.request("POST", "/session", {"capabilities": {"alwaysMatch": {
            "platformName": "iOS", "appium:automationName": "XCUITest", "appium:udid": udid,
            # CI has already created and booted an empty simulator. A full reset would
            # erase it and restart the expensive first boot, also killing log/video capture.
            "appium:app": str(app), "appium:noReset": False, "appium:fullReset": False,
            "appium:language": "it", "appium:locale": "it_IT",
            "appium:skipSyncUiDialogTranslation": True,
            "appium:isHeadless": True,
            "appium:usePrebuiltWDA": True, "appium:derivedDataPath": str(wda_derived_data),
            "appium:enforceAppInstall": True, "appium:newCommandTimeout": 90,
            "appium:wdaLaunchTimeout": 180000, "appium:wdaConnectionTimeout": 30000,
            "appium:simulatorStartupTimeout": 300000,
            "appium:wdaStartupRetries": 1, "appium:waitForIdleTimeout": 1,
            "appium:connectHardwareKeyboard": False, "appium:forceSimulatorSoftwareKeyboardPresence": True,
            "appium:autoAcceptAlerts": False, "appium:showXcodeLog": False,
        }, "firstMatch": [{}]}}, timeout=480)
        self.session = value["sessionId"]
        self.request("POST", "/timeouts", {"implicit": 0, "script": 30000, "pageLoad": 30000})
        return value.get("capabilities", {})

    def execute(self, command, args=None, diagnostic=False):
        return self.request("POST", "/execute/sync", {"script": "mobile: " + command,
                            "args": [args or {}]}, diagnostic=diagnostic)

    def elements(self, using, value):
        # Recycled MAUI cells keep a constant AutomationId. The semantic label
        # identifies their bound record without changing an immutable native ID.
        if using == "accessibility id":
            for prefix, native_id in (("Transactions.Item.", "Transactions.Item"),
                                      ("Transaction.Category.", "Transaction.Category")):
                if value.startswith(prefix):
                    using = "-ios predicate string"
                    value = f"name == {json.dumps(native_id)} AND label == {json.dumps(value[len(prefix):], ensure_ascii=False)}"
                    break
        return [item[ELEMENT] for item in self.request("POST", "/elements", {"using": using, "value": value})]

    def visible(self, element):
        try:
            return self.request("GET", f"/element/{element}/displayed") is True
        except ProtocolError as error:
            if error.code in ("stale element reference", "no such element"):
                return False
            raise

    def once(self, identifier):
        return next((e for e in self.elements("accessibility id", identifier) if self.visible(e)), None)

    def find(self, identifier, timeout=12, scroll=False, direction="up"):
        until = min(self.deadline, time.monotonic() + timeout)
        last_scroll = 0
        while time.monotonic() < until:
            matches = self.elements("accessibility id", identifier)
            for element in matches:
                if self.visible(element):
                    return element
            if scroll and time.monotonic() - last_scroll > 1.5:
                if matches:
                    try:
                        self.execute("scrollToElement", {"elementId": matches[0]})
                    except ProtocolError as error:
                        self.log(event="scroll_to_element_fallback", error=str(error))
                        self.execute("swipe", {"direction": direction})
                else:
                    self.execute("swipe", {"direction": direction})
                last_scroll = time.monotonic()
            time.sleep(0.25)
        raise AssertionError(f"Visible accessibility id not found: {identifier}")

    def click(self, identifier, scroll=True, direction="up"):
        # A stale element retry is safe only before the click succeeds.
        for attempt in range(2):
            element = self.find(identifier, scroll=scroll, direction=direction)
            try:
                self.request("POST", f"/element/{element}/click", {})
                return
            except ProtocolError as error:
                if error.code != "stale element reference" or attempt:
                    raise

    def text(self, identifier, scroll=False, direction="up"):
        element = self.find(identifier, scroll=scroll, direction=direction)
        values = []
        for attribute in ("value", "label", "name"):
            value = self.request("GET", f"/element/{element}/attribute/{attribute}")
            if value is not None:
                values.append(str(value))
        return " | ".join(values)

    def fill(self, identifier, value):
        element = self.find(identifier, scroll=True)
        self.request("POST", f"/element/{element}/click", {})
        self.request("POST", f"/element/{element}/clear", {})
        self.request("POST", f"/element/{element}/value", {"text": value, "value": list(value)})
        self.dismiss_keyboard()

    def dismiss_keyboard(self):
        if not self.execute("isKeyboardShown"):
            return
        try:
            self.execute("hideKeyboard", {"keys": ["Done", "Fine", "Return", "Invio"]})
        except ProtocolError as error:
            self.log(event="keyboard_hide_fallback", error=str(error))
        if not self.execute("isKeyboardShown"):
            return
        buttons = self.elements("-ios predicate string",
            "type == 'XCUIElementTypeButton' AND (label IN {'Done', 'Fine', 'Return', 'Invio'})")
        for button in buttons:
            if self.visible(button):
                self.request("POST", f"/element/{button}/click", {})
                if not self.execute("isKeyboardShown"):
                    return
        # These pages enable HideSoftInputOnTapped: click an actual native title.
        self.click("Page.Title", direction="down")
        if self.execute("isKeyboardShown"):
            self.execute("swipe", {"direction": "down"})
        if self.execute("isKeyboardShown"):
            raise AssertionError("The native keyboard could not be dismissed")

    def expect_text(self, identifier, expected, scroll=False, timeout=12):
        until = min(self.deadline, time.monotonic() + timeout)
        actual = ""
        while time.monotonic() < until:
            actual = self.text(identifier, scroll=scroll)
            if expected in actual:
                return
            time.sleep(0.25)
        raise AssertionError(f"{identifier}: expected {expected!r}, received {actual!r}")

    def expect_money(self, identifier, expected, scroll=False, direction="up"):
        until = min(self.deadline, time.monotonic() + 10)
        actual = ""
        while time.monotonic() < until:
            actual = self.text(identifier, scroll=scroll, direction=direction)
            amounts = re.findall(r"-?\d+(?:[.,]\d{2})", actual)
            if any(Decimal(amount.replace(",", ".")) == Decimal(expected) for amount in amounts):
                return
            time.sleep(0.25)
        raise AssertionError(f"{identifier}: expected amount {expected}, received {actual!r}")

    def expect_absent(self, identifier, timeout=10):
        until = min(self.deadline, time.monotonic() + timeout)
        while time.monotonic() < until:
            if not self.elements("accessibility id", identifier):
                return
            time.sleep(0.3)
        raise AssertionError(f"Element remains after deletion: {identifier}")

    def capture(self, name, source=False, diagnostic=False):
        errors = []
        try:
            encoded = self.request("GET", "/screenshot", diagnostic=diagnostic)
            (self.artifacts / f"{name}.png").write_bytes(base64.b64decode(encoded))
        except Exception as error:
            errors.append(f"screenshot: {error}")
        if source:
            for extension, command in (("xml", None), ("json", "source")):
                try:
                    value = self.request("GET", "/source", diagnostic=diagnostic) if command is None else \
                        self.execute(command, {"format": "json"}, diagnostic=diagnostic)
                    if extension == "json":
                        if isinstance(value, str):
                            try:
                                value = json.loads(value)
                            except ValueError:
                                pass
                        value = json.dumps(value, ensure_ascii=False, indent=2)
                    (self.artifacts / f"{name}.{extension}").write_text(value, encoding="utf-8")
                except Exception as error:
                    errors.append(f"{extension} source: {error}")
        if errors:
            self.log(event="artifact_errors", errors=errors)
            if not diagnostic:
                raise AssertionError("Could not collect required artifacts: " + "; ".join(errors))
        return errors

    def close(self):
        if self.session:
            self.request("DELETE", "", diagnostic=True)
            self.session = None


class Smoke:
    def __init__(self, driver, report, bundle):
        self.driver = driver
        self.report = report
        self.bundle = bundle

    @contextmanager
    def step(self, name):
        print(f"[iOS smoke] {name}", flush=True)
        started = time.monotonic()
        record = {"name": name, "status": "running"}
        self.report["steps"].append(record)
        try:
            yield
            record["status"] = "passed"
        except Exception as error:
            record.update(status="failed", error=str(error))
            raise
        finally:
            record["seconds"] = round(time.monotonic() - started, 2)

    def navigate(self, destination, heading=None):
        d = self.driver
        d.click("Navigation.Menu", direction="down")
        d.click("Navigation." + destination)
        if destination == "Home":
            d.find("Home.Balance")
        else:
            d.expect_text("Page.Title", heading)

    def restart(self):
        self.driver.execute("terminateApp", {"bundleId": self.bundle})
        self.driver.execute("activateApp", {"bundleId": self.bundle})
        # A new process can restore its previous route or return to Home.
        self.driver.find("Navigation.Menu", timeout=25, scroll=True, direction="down")

    def run(self):
        d = self.driver
        with self.step("fresh launch and Home"):
            d.find("Home.Balance", timeout=30)
            d.capture("01-home")
        with self.step("create account from empty storage"):
            self.navigate("Accounts", "Conti")
            d.find("Accounts.New")
            if d.elements("accessibility id", "Accounts.Edit"):
                raise AssertionError("Fresh-install precondition failed: an account already exists")
            d.click("Accounts.New")
            d.fill("Accounts.Name", "CI Wallet")
            d.fill("Accounts.OpeningBalance", "100,00")
            d.click("Accounts.Save")
            d.expect_text("Accounts.RowName", "CI Wallet", scroll=True)
            d.expect_money("Accounts.TotalBalance", "100.00", scroll=True, direction="down")
            d.capture("02-account-created")
        with self.step("read and update account"):
            d.click("Accounts.Edit")
            d.expect_text("Accounts.Name", "CI Wallet")
            d.expect_money("Accounts.OpeningBalance", "100.00")
            d.fill("Accounts.Name", "CI Wallet Renamed")
            d.fill("Accounts.OpeningBalance", "125,00")
            d.click("Accounts.Save")
            d.expect_text("Accounts.RowName", "CI Wallet Renamed", scroll=True)
            d.expect_money("Accounts.TotalBalance", "125.00", scroll=True, direction="down")
            d.capture("03-account-updated")
        for destination, heading in (("Transactions", "Movimenti"), ("Statistics", "Statistiche"),
                                     ("Categories", "Categorie"), ("Export", "Esporta e importa"),
                                     ("Planning", "Pianifica")):
            with self.step("native navigation: " + destination):
                self.navigate(destination, heading)
                d.capture("page-" + destination.lower())
        with self.step("create transaction"):
            self.navigate("NewTransaction", "Nuovo movimento")
            d.fill("Transaction.Amount", "12,50")
            d.click("Transaction.Category.Affitto")
            d.click("Transaction.Details")
            d.fill("Transaction.Description", "CI smoke expense")
            d.fill("Transaction.Notes", "Created by native iOS smoke")
            d.capture("04-transaction-editor")
            d.click("Transaction.Save")
            d.expect_text("Page.Title", "Movimenti")
            d.click("Transactions.Item.CI smoke expense")
            d.expect_money("Transactions.Detail.Amount", "-12.50")
            d.expect_text("Transactions.Detail.Notes", "Created by native iOS smoke", scroll=True)
            d.capture("05-transaction-created")
        with self.step("update transaction"):
            d.click("Transactions.Detail.Edit")
            d.expect_text("Page.Title", "Modifica movimento")
            d.fill("Transaction.Amount", "18,75")
            # Editing an existing movement opens its details in the view model.
            d.fill("Transaction.Description", "CI smoke updated")
            d.fill("Transaction.Notes", "Updated and persisted by native iOS smoke")
            d.click("Transaction.Save")
            d.expect_text("Page.Title", "Movimenti")
            d.click("Transactions.Item.CI smoke updated")
            d.expect_money("Transactions.Detail.Amount", "-18.75")
            d.expect_text("Transactions.Detail.Notes", "Updated and persisted by native iOS smoke", scroll=True)
            d.capture("06-transaction-updated")
            d.click("Transactions.Detail.Close")
        with self.step("terminate and reactivate: account and transaction persistence"):
            self.restart()
            self.navigate("Accounts", "Conti")
            d.expect_text("Accounts.RowName", "CI Wallet Renamed", scroll=True)
            d.expect_money("Accounts.TotalBalance", "106.25", scroll=True, direction="down")
            d.click("Accounts.Edit")
            d.expect_text("Accounts.Name", "CI Wallet Renamed")
            d.expect_money("Accounts.OpeningBalance", "125.00")
            d.click("Accounts.Cancel")
            self.navigate("Transactions", "Movimenti")
            d.click("Transactions.Item.CI smoke updated")
            d.expect_money("Transactions.Detail.Amount", "-18.75")
            d.expect_text("Transactions.Detail.Notes", "Updated and persisted by native iOS smoke", scroll=True)
            d.capture("07-persistence")
        with self.step("delete transaction with explicit confirmation"):
            d.click("Transactions.Detail.Delete")
            d.click("Dialog.Accept", scroll=False)
            d.expect_absent("Transactions.Item.CI smoke updated")
            d.expect_text("Transactions.Count", "0 moviment")
            d.capture("08-transaction-deleted")
        with self.step("terminate and reactivate: deletion and balance persistence"):
            self.restart()
            self.navigate("Transactions", "Movimenti")
            d.expect_text("Transactions.Count", "0 moviment")
            d.expect_absent("Transactions.Item.CI smoke updated")
            self.navigate("Accounts", "Conti")
            d.expect_text("Accounts.RowName", "CI Wallet Renamed", scroll=True)
            d.expect_money("Accounts.TotalBalance", "125.00", scroll=True, direction="down")
            d.capture("09-deletion-persisted", source=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server", default="http://127.0.0.1:4723")
    parser.add_argument("--udid", required=True)
    parser.add_argument("--app", required=True, type=Path)
    parser.add_argument("--artifacts", required=True, type=Path)
    args = parser.parse_args()
    args.artifacts.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    report = {"status": "failed", "startedAt": datetime.now(timezone.utc).isoformat(),
              "udid": args.udid, "app": str(args.app.resolve()), "steps": [], "freshInstall": True}
    # Bound prebuild + session + UI to eighteen minutes. Four diagnostic requests
    # may use another minute, leaving report/CI headroom in the twenty-minute step.
    driver = Driver(args.server, args.artifacts, started + 1080)
    try:
        app = args.app.resolve(strict=True)
        with (app / "Info.plist").open("rb") as info:
            metadata = plistlib.load(info)
        if "iPhoneSimulator" not in metadata.get("CFBundleSupportedPlatforms", []):
            raise ValueError("--app must be an iPhoneSimulator .app; refusing to reset or launch a physical-device build")
        bundle = metadata["CFBundleIdentifier"]
        report["bundleId"] = bundle
        inventory = subprocess.run(["xcrun", "simctl", "list", "devices", "--json"],
                                   capture_output=True, text=True, check=True, timeout=30)
        devices = json.loads(inventory.stdout).get("devices", {})
        simulator = next(({**device, "runtime": runtime} for runtime, group in devices.items()
                          for device in group if device.get("udid", "").lower() == args.udid.lower()
                          and device.get("isAvailable") is True and ".iOS-" in runtime), None)
        if simulator is None:
            raise ValueError("--udid must identify an available iOS Simulator; refusing to reset an unverified device")
        report["simulator"] = simulator
        wda_derived_data = prebuild_wda(args.udid, args.artifacts, report)
        report["capabilities"] = driver.start(args.udid, app, wda_derived_data)
        report["startupSeconds"] = round(time.monotonic() - started, 2)
        Smoke(driver, report, bundle).run()
        report["status"] = "passed"
    except Exception as error:
        report["error"] = str(error)
        report["traceback"] = traceback.format_exc()
        (args.artifacts / "failure.txt").write_text(report["traceback"], encoding="utf-8")
        print(report["traceback"], file=sys.stderr, flush=True)
        if driver.session:
            report["artifactErrors"] = driver.capture("failure", source=True, diagnostic=True)
    finally:
        try:
            driver.close()
        except Exception as error:
            report["cleanupError"] = str(error)
        report["seconds"] = round(time.monotonic() - started, 2)
        (args.artifacts / "result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"[iOS smoke] {report['status']} in {report['seconds']}s; artifacts: {args.artifacts}", flush=True)
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    sys.exit(main())

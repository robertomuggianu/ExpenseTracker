"""Android emulator smoke checks; no transaction/account/category writes.

Requires an installed app, Python 3 and ANDROID_HOME (or --adb).
Screenshots, accessibility trees and results stay in the ignored artifacts folder.
Run: python tests/android_ui_smoke.py --serial emulator-5554
"""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import time
import xml.etree.ElementTree as ET


class Device:
    def __init__(self, adb, serial, output):
        self.command = [str(adb), "-s", serial]
        self.output = output
        output.mkdir(parents=True, exist_ok=True)

    def run(self, *args, binary=False):
        result = subprocess.run(self.command + list(args), capture_output=True, check=True, timeout=45)
        return result.stdout if binary else result.stdout.decode("utf-8", errors="replace")

    def tree(self):
        # A dump can fail during a page transition; retry once with a fresh hierarchy.
        for attempt in range(2):
            try:
                self.run("shell", "rm", "-f", "/sdcard/expense-smoke.xml")
                self.run("shell", "uiautomator", "dump", "/sdcard/expense-smoke.xml")
                return ET.fromstring(self.run("exec-out", "cat", "/sdcard/expense-smoke.xml"))
            except (subprocess.CalledProcessError, ET.ParseError, RuntimeError):
                if attempt:
                    raise
                time.sleep(1)

    def wait(self, predicate, label):
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            try:
                root = self.tree()
            except (subprocess.CalledProcessError, ET.ParseError, RuntimeError):
                time.sleep(1)
                continue
            if predicate(root):
                return root
            time.sleep(.3)
        raise AssertionError(f"Timed out waiting for {label}")

    @staticmethod
    def has(root, label):
        return any(label in (n.get("text"), n.get("content-desc")) for n in root.iter("node"))

    def tap(self, label):
        root = self.wait(lambda r: self.has(r, label), label)
        nodes = [n for n in root.iter("node") if label in (n.get("text"), n.get("content-desc"))]
        buttons = [n for n in nodes if n.get("class") == "android.widget.Button" and n.get("clickable") == "true"]
        nodes = buttons or nodes
        described = [n for n in nodes if n.get("content-desc") == label]
        nodes = described or nodes
        clickable = [n for n in nodes if n.get("clickable") == "true"]
        nodes = clickable or nodes
        if len(nodes) != 1:
            raise AssertionError(f"Ambiguous control: {label}")
        x1, y1, x2, y2 = map(int, re.findall(r"\d+", nodes[0].get("bounds")))
        if x2 <= x1 or y2 <= y1:
            raise AssertionError(f"Control has no visible bounds: {label}")
        self.run("shell", "input", "tap", str((x1+x2)//2), str((y1+y2)//2))

    def capture(self, name):
        root = self.tree()
        ET.ElementTree(root).write(self.output / f"{name}.xml", encoding="utf-8")
        (self.output / f"{name}.png").write_bytes(self.run("exec-out", "screencap", "-p", binary=True))
        return root

    def top(self):
        root = self.tree()
        if self.has(root, "Apri menu"):
            return
        bounds = list(root.iter("node"))[0].get("bounds")
        x1, y1, x2, y2 = map(int, re.findall(r"\d+", bounds))
        for _ in range(5):
            self.run("shell", "input", "swipe", str((x1+x2)//2), str(y1+(y2-y1)//4),
                     str((x1+x2)//2), str(y1+(y2-y1)*4//5), "350")
            if self.has(self.tree(), "Apri menu"):
                return
        raise AssertionError("Page menu is not reachable")

    def navigate(self, page):
        self.top()
        self.tap("Apri menu")
        self.wait(lambda r: self.has(r, "ExpenseTracker"), "drawer")
        self.tap(page)
        return self.wait(
            lambda r: not self.has(r, "ExpenseTracker") and
            (any(n.get("text") == page and n.get("class") == "android.widget.TextView" for n in r.iter("node"))
             if page != "Home" else self.has(r, "Spese")), page)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", default="emulator-5554")
    parser.add_argument("--adb", default=str(Path(os.environ.get("ANDROID_HOME", "")) / "platform-tools" / ("adb.exe" if os.name == "nt" else "adb")))
    parser.add_argument("--output", type=Path, default=Path("artifacts/android-smoke"))
    args = parser.parse_args()
    device = Device(args.adb, args.serial, args.output)
    if device.run("shell", "getprop", "ro.kernel.qemu").strip() != "1":
        raise RuntimeError("This smoke script is restricted to an Android emulator.")
    package = "com.companyname.expensetracker"
    device.run("shell", "monkey", "-p", package, "-c", "android.intent.category.LAUNCHER", "1")
    device.wait(lambda r: device.has(r, "Apri menu"), "application")
    results = []

    def passed(name):
        results.append({"check": name, "passed": True})
        print(f"PASS {name}", flush=True)

    try:
        for page, slug, expected in [
            ("Conti", "accounts", "Saldo totale"),
            ("Movimenti", "transactions", "Filtri"),
            ("Statistiche", "statistics", "Mese, selezionato"),
            ("Categorie", "categories", "Cerca una categoria"),
            ("Esporta e importa", "export", "Scegli file e importa"),
            ("Pianifica", "planning", "Budget"),
            ("Nuovo movimento", "editor", "Importo"),
        ]:
            device.navigate(page)
            device.wait(lambda r: device.has(r, expected), expected)
            device.capture(slug)
            passed(f"navigation/{slug}")

        device.navigate("Home")
        root = device.tree()
        original_hidden = device.has(root, "Mostra saldo totale")
        device.tap("Mostra saldo totale" if original_hidden else "Nascondi saldo totale")
        device.wait(lambda r: device.has(r, "Nascondi saldo totale" if original_hidden else "Mostra saldo totale"), "balance toggle")
        device.tap("Nascondi saldo totale" if original_hidden else "Mostra saldo totale")
        device.wait(lambda r: device.has(r, "Mostra saldo totale" if original_hidden else "Nascondi saldo totale"), "balance restored")
        passed("home/balance-visibility-restored")

        device.tap("Cambia conto per tutta l'app")
        device.tap("Cerca")
        device.run("shell", "input", "text", "zzznomatchQA")
        device.wait(lambda r: device.has(r, "Nessun risultato. Prova un altro nome."), "empty search feedback")
        device.capture("choice-empty")
        device.tap("Cancella ricerca")
        device.wait(lambda r: device.has(r, "Tutti i conti"), "choices restored")
        device.tap("Annulla")
        device.wait(lambda r: device.has(r, "Cambia conto per tutta l'app"), "account unchanged after cancel")
        device.capture("home")
        passed("choice/search-clear-cancel")

        for page in ("Home", "Movimenti", "Statistiche"):
            device.navigate(page)
            device.tap("Anno")
            device.wait(lambda r: device.has(r, "Anno, selezionato"), "year selected")
            device.tap("Periodo")
            device.wait(lambda r: device.has(r, "Dal") and device.has(r, "Al"), "custom dates")
            device.tap("Mese")
            root = device.wait(lambda r: device.has(r, "Mese, selezionato"), "month restored")
            if page == "Statistiche":
                assert not device.has(root, "Aggiorna"), "Manual refresh must be removed"
                assert not any(n.get("content-desc", "").startswith("Valuta del report:") for n in root.iter("node"))
            device.capture("period-" + page.lower())
            passed("periods/" + page.lower())

        device.navigate("Pianifica")
        device.tap("Ricorrenze")
        device.tap("+  Nuova ricorrenza")
        device.wait(lambda r: device.has(r, "Nome, es. Netflix o stipendio"), "new recurring form")
        device.capture("recurring-form")
        # Leave the unsaved editor with its own cancel action, scrolling if necessary.
        for _ in range(5):
            if device.has(device.tree(), "Annulla"):
                break
            device.run("shell", "input", "swipe", "540", "1850", "540", "650", "400")
        device.tap("Annulla")
        device.wait(lambda r: not device.has(r, "Nome, es. Netflix o stipendio"), "recurring form dismissed")
        passed("planning/new-cancel")
        device.navigate("Home")
    except Exception as error:
        results.append({"check": "interrupted", "passed": False, "error": str(error)})
        try:
            device.capture("failure")
        except Exception:
            pass
        raise
    finally:
        (args.output / "results.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()

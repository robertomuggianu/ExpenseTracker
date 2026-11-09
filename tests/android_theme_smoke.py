"""Verify sun/moon theme controls and restart persistence on an Android emulator.

Requires an updated debuggable build already installed. Does not save financial records.
Run: python tests/android_theme_smoke.py --adb PATH_TO_ADB
Screenshots and accessibility trees are written under ignored artifacts/.
"""
import argparse
import json
import os
import time
from pathlib import Path
import xml.etree.ElementTree as ET

from android_ui_smoke import Device


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", default="emulator-5554")
    parser.add_argument("--adb", default=str(Path(os.environ.get("ANDROID_HOME", "")) / "platform-tools/adb.exe"))
    parser.add_argument("--output", type=Path, default=Path("artifacts/android-themes"))
    parser.add_argument("--persistence-only", action="store_true", help="Check theme selection and restart without repeating screen navigation")
    args = parser.parse_args()
    device = Device(args.adb, args.serial, args.output)
    if device.run("shell", "getprop", "ro.kernel.qemu").strip() != "1":
        raise RuntimeError("This test is restricted to an Android emulator.")
    package = "com.companyname.expensetracker"
    results = []
    original_theme = None

    def saved_theme():
        xml = device.run("exec-out", "run-as", package, "cat",
                         "shared_prefs/" + package + "_preferences.xml")
        value = ET.fromstring(xml).find("./string[@name='app-appearance']")
        return value.text if value is not None else "dark"

    def launch():
        device.run("shell", "monkey", "-p", package, "-c", "android.intent.category.LAUNCHER", "1")
        time.sleep(8)
        device.wait(lambda r: device.has(r, "Apri menu"), "application")
        print("CHECK application ready", flush=True)

    def select_theme(label):
        device.top()
        device.tap("Apri menu")
        device.tap(label)
        device.wait(lambda r: device.has(r, "ExpenseTracker"), "theme selected")
        device.run("shell", "input", "keyevent", "4")
        device.wait(lambda r: not device.has(r, "ExpenseTracker"), "drawer closed")

    def passed(name):
        results.append({"check": name, "passed": True})
        print("PASS " + name, flush=True)

    try:
        device.run("shell", "am", "force-stop", package)
        launch()
        device.navigate("Home")
        device.tap("Apri menu")
        device.wait(lambda r: device.has(r, "Tema chiaro Avorio"), "appearance controls")
        original_theme = "Tema chiaro Avorio" if saved_theme() == "light" else "Tema scuro Grafite"
        assert not device.has(device.tree(), "Sistema"), "System option must not be shown"
        device.run("shell", "input", "keyevent", "4")
        for label, slug in [("Tema scuro Grafite", "graphite"), ("Tema chiaro Avorio", "ivory")]:
            print("CHECK " + slug, flush=True)
            select_theme(label)
            if not args.persistence_only:
                device.capture(slug + "-home")
                device.tap("Periodo precedente")
                device.capture(slug + "-home-previous-month")
                device.tap("Periodo successivo")
                for page, suffix in [("Movimenti", "transactions"), ("Nuovo movimento", "editor"), ("Statistiche", "statistics"), ("Categorie", "categories")]:
                    device.navigate(page)
                    device.capture(slug + "-" + suffix)
                device.navigate("Home")
                device.tap("Cambia conto per tutta l'app")
                device.wait(lambda r: device.has(r, "Annulla"), "account selector")
                device.capture(slug + "-selector")
                device.tap("Annulla")
            device.run("shell", "am", "force-stop", package)
            launch()
            assert saved_theme() == ("dark" if slug == "graphite" else "light"), "Theme was not persisted"
            device.tap("Apri menu")
            device.wait(lambda r: device.has(r, label), "appearance controls after restart")
            device.capture(slug + "-persisted")
            device.run("shell", "input", "keyevent", "4")
            passed(slug + ("/selection-restart" if args.persistence_only else "/navigation-selectors-restart"))

    except Exception as error:
        results.append({"check": "interrupted", "passed": False, "error": str(error)})
        try:
            device.capture("failure")
        except Exception:
            pass
        raise
    finally:
        if original_theme:
            select_theme(original_theme)
        (args.output / "results.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()

"""Focused period/account checks on an emulator; does not create financial records."""
import argparse
import json
from pathlib import Path
from android_ui_smoke import Device


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--adb', required=True)
    parser.add_argument('--serial', default='emulator-5554')
    parser.add_argument('--restore-theme', choices=('Grafite', 'Avorio'), default='Grafite', help='Appearance to restore after checking Avorio')
    parser.add_argument('--output', type=Path, default=Path('artifacts/filter-scope-final'))
    args = parser.parse_args()
    device = Device(args.adb, args.serial, args.output)
    assert device.run('shell', 'getprop', 'ro.kernel.qemu').strip() == '1', 'Emulator required'
    device.run('shell', 'monkey', '-p', 'com.companyname.expensetracker', '-c', 'android.intent.category.LAUNCHER', '1')
    device.wait(lambda r: device.has(r, 'Apri menu'), 'app ready')
    results = []

    def passed(check):
        results.append({'check': check, 'passed': True})
        print('PASS ' + check, flush=True)

    def theme(name):
        device.tap('Apri menu')
        device.tap(name)
        device.run('shell', 'input', 'keyevent', '4')
        device.wait(lambda r: not device.has(r, 'ExpenseTracker'), 'drawer dismissed')

    try:
        device.navigate('Home')
        device.tap('Apri menu')
        screenshot = device.run('exec-out', 'screencap', '-p', binary=True)
        (args.output / 'original-appearance.png').write_bytes(screenshot)
        device.run('shell', 'input', 'keyevent', '4')

        device.tap("Cambia conto per tutta l'app")
        device.wait(lambda r: device.has(r, 'Tutti i conti'), 'account choices')
        device.capture('account-sheet')
        device.tap('Annulla')
        device.wait(lambda r: device.has(r, "Cambia conto per tutta l'app"), 'home after account cancel')
        passed('home/account-sheet-cancel')

        for page in ('Home', 'Movimenti', 'Statistiche'):
            device.navigate(page)
            device.tap('Anno')
            device.wait(lambda r: device.has(r, 'Anno, selezionato'), 'year selected')
            device.tap('Periodo')
            device.wait(lambda r: device.has(r, 'Dal') and device.has(r, 'Al'), 'custom dates visible')
            device.capture('custom-' + page.lower())
            device.tap('Mese')
            tree = device.wait(lambda r: device.has(r, 'Mese, selezionato'), 'month restored')
            if page == 'Statistiche':
                assert not device.has(tree, 'Aggiorna')
                assert not any(n.get('content-desc', '').startswith('Valuta del report:') for n in tree.iter('node'))
            device.capture(page.lower())
            passed('period/' + page.lower())
        device.navigate('Home')
        theme('Tema chiaro Avorio')
        try:
            for page in ('Home', 'Movimenti', 'Statistiche'):
                device.navigate(page)
                device.wait(lambda r: device.has(r, 'Mese, selezionato'), 'month visible in ivory')
                device.capture('ivory-' + page.lower())
            passed('ivory/three-period-bars')
        finally:
            theme('Tema scuro Grafite' if args.restore_theme == 'Grafite' else 'Tema chiaro Avorio')
            device.navigate('Home')
    except Exception as error:
        results.append({'check': 'interrupted', 'passed': False, 'error': str(error)})
        device.capture('failure')
        raise
    finally:
        (args.output / 'results.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()

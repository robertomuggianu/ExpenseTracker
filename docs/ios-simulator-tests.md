# Collaudo automatico su iPhone simulato

Il workflow `.github/workflows/ios-simulator-tests.yml` usa un Mac temporaneo di GitHub Actions per compilare l'app e provarla nel simulatore iOS di Xcode. Si può avviare e consultare anche da Windows, senza possedere un Mac o un account Apple Developer.

Il simulatore riceve una nuova installazione dell'app e dati fittizi creati dai test. Non accede al database del telefono, non richiede credenziali dell'utente e non chiama Ollama. Il pacchetto per Sideloadly continua a essere preparato dal workflow separato già esistente.

## Esecuzione

Il primo avvio avviene pubblicando il ramo `codex/ios-simulator-tests`. Il controllo si ripete sui push di quel ramo e di `main` e sulle pull request verso `main`, quando cambiano sorgenti, test, script o il workflow stesso. Una volta integrato il workflow in `main`, è disponibile anche l'avvio manuale da **Actions → iPhone simulator UI tests → Run workflow**.

I risultati sono visibili nella [pagina Actions del progetto](https://github.com/robertomuggianu/ExpenseTracker/actions). Un test è superato soltanto quando termina con esito positivo: la presenza della configurazione o una compilazione riuscita non attestano da sole il funzionamento dell'interfaccia.

## Prove e risultati

Lo script `tests/ios_simulator_smoke.py` esegue 13 controlli in sequenza:

- primo avvio e Home;
- creazione, lettura e modifica di un conto;
- navigazione in Movimenti, Statistiche, Categorie, Esporta e importa, Pianifica;
- creazione e modifica di una spesa, verificando importo e note;
- chiusura e riapertura del processo, con verifica del conto, del movimento e del saldo;
- eliminazione del movimento con conferma esplicita;
- seconda riapertura per verificare che eliminazione e saldo siano persistiti.

La prova interrompe l'esecuzione al primo errore e termina con codice non zero. L'artefatto `iPhone-Simulator-<numero>` contiene risultati JSON, schermate, registrazione video, gerarchia dei controlli in caso di errore, log di Appium e del simulatore, versioni effettive e log di compilazione. Gli artefatti restano disponibili per 14 giorni. La raccolta delle evidenze e lo spegnimento del simulatore sono previsti anche quando un passaggio fallisce.

## Ambiente e limiti

L'app viene compilata per `iossimulator-arm64`, distinto da `ios-arm64` utilizzato per il telefono fisico. SDK .NET, workload iOS e Xcode sono mantenuti coerenti con il workflow IPA. Il dispositivo è creato fra i modelli e i runtime realmente installati sul Mac del runner.

Il pacchetto compilato viene conservato per le esecuzioni successive soltanto quando coincidono sorgenti e configurazione di compilazione. Anche il pacchetto recuperato viene verificato per piattaforma e architettura. Correggere uno scenario di test può così evitare una nuova compilazione; ogni prova usa comunque un nuovo dispositivo simulato.

Appium e il driver XCUITest comandano i controlli nativi dell'app. Gli identificatori di automazione individuano i campi senza dipendere dalla loro posizione sullo schermo. Ogni esecuzione conserva le evidenze utili a distinguere un errore dell'app da un problema di compilazione o avvio del simulatore.

Queste prove coprono il sistema operativo e l'interfaccia nativa iOS, ma non certificano l'assenza di qualsiasi bug. Prestazioni, tastiera, gesti, permessi e comportamento sul telefono fisico restano verifiche complementari. L'integrazione Ollama è esclusa da questo collaudo.

## Verifica della configurazione — 4 novembre 2026

- Suite applicativa locale: 223 test superati, nessuno ignorato.
- Compilazione Android Release degli identificatori iniziali: nessun errore o avviso. I successivi identificatori costanti dei template vengono verificati dalla compilazione iOS.
- Regressione GitHub, compresi i flussi senza reflection JSON: superata.
- Workflow controllato con actionlint 1.7.12; script Python compilato e controllo di rifiuto dei pacchetti `iPhoneOS` esercitato prima di qualsiasi chiamata ad Appium.
- Lockfile npm verificato, con versioni esatte e integrità dei pacchetti; `npm ci --dry-run --ignore-scripts --offline` superato. L'installazione completa sul Mac GitHub è riuscita.

L'esecuzione iOS effettiva sulla revisione `6b9a550` è [disponibile su GitHub Actions](https://github.com/robertomuggianu/ExpenseTracker/actions/runs/41897135446); il suo esito resta distinto dai controlli preliminari sopra.

## Riferimenti

- [Compilare ed eseguire app MAUI nel simulatore iOS](https://learn.microsoft.com/en-us/dotnet/maui/ios/cli?view=net-maui-10.0).
- [Requisiti del driver Appium XCUITest](https://appium.github.io/appium-xcuitest-driver/latest/getting-started/system-requirements/).
- [Avvio manuale dei workflow GitHub](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow): il workflow deve essere presente nel ramo predefinito.

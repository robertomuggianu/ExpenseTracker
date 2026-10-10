# ExpenseTracker

ExpenseTracker è un'applicazione personale offline-first realizzata con .NET MAUI 10, C#, XAML e MVVM. Conti, categorie e movimenti restano nel database SQLite del dispositivo. L'assistente opzionale usa un servizio locale sul PC con Ollama, senza sincronizzazione cloud.

## Funzioni MVP

- gestione di più conti con un solo conto principale e saldo calcolato;
- categorie di entrata/uscita in griglia senza nomi, con 45 icone Tabler e una palette di 32 colori, disattivabili senza eliminare lo storico;
- inserimento, modifica, filtro, dettaglio ed eliminazione dei movimenti;
- Home con valuta automatica del conto principale, navigazione tra mesi e confronto delle spese con il mese precedente;
- statistiche per settimana, mese, anno o periodo personalizzato, grafici a ciambella e andamento giornaliero/mensile con LiveCharts2;
- inserimento rapido con le categorie più usate, conto principale preselezionato e dettagli facoltativi;
- cronologia raggruppata per giorno, ricerca uniforme e filtri automatici, navigazione dal menu laterale;
- assistente AI accessibile dal pulsante in basso a sinistra, con ritorno alla pagina precedente senza perdere i campi compilati;
- esportazione CSV completa, filtrata o del mese selezionato e importazione CSV/XLSX.

L'interfaccia usa un tema scuro verde petrolio con superfici arrotondate, accenti menta e il pulsante di aggiunta ambrato. Il colore di ogni categoria è condiviso tra selezione, movimenti e grafici. Per il mese corrente il confronto usa i giorni trascorsi in entrambi i mesi; per un mese passato usa i due mesi completi.

## Architettura

- `src/ExpenseTracker.Domain`: entità, enumerazioni e regole pure.
- `src/ExpenseTracker.Application`: casi d'uso, filtri, aggregazioni e contratti di persistenza/esportazione. Dipende solo da Domain.
- `src/ExpenseTracker.Infrastructure`: repository SQLite, migrazione/seed e generatore CSV. Dipende da Application.
- `src/ExpenseTracker`: host MAUI e layer Presentation con pagine XAML, ViewModel Toolkit, navigazione Shell e composizione DI.
- `src/ExpenseTracker.AssistantServer`: servizio ASP.NET Core locale per i resoconti dell'assistente con Ollama.
- `tests/ExpenseTracker.Application.Tests`: test xUnit delle regole, aggregazioni, CSV e inizializzazione SQLite.

La direzione dei riferimenti è `Presentation -> Infrastructure/Application -> Domain`. Le ViewModel usano servizi applicativi e non eseguono query SQL.

## Requisiti

- .NET SDK 10 e workload MAUI;
- Android SDK con dispositivo o emulatore per Android;
- Windows App SDK disponibile tramite il workload MAUI per l'esecuzione WinUI su Windows.

Pacchetti principali:

- `CommunityToolkit.Mvvm` 8.4.2;
- `LiveChartsCore.SkiaSharpView.Maui` 2.0.5, MIT e compatibile con MAUI/.NET 10;
- `sqlite-net-pcl` 1.11.284-beta, che usa SQLite 3.53.3 e sostituisce la precedente linea stabile con binari segnalati da NuGet come vulnerabili.

## Database

Il file `expense-tracker.db3` viene creato in `FileSystem.AppDataDirectory`. Lo schema versione 5 contiene:

- `Accounts`;
- `Categories`, con tipo, colore e icona;
- `Transactions`, collegata a `Accounts` e `Categories`, con collegamento dei trasferimenti;
- `AppState`, per budget e ricorrenze.

Gli importi sono salvati in centesimi interi e convertiti in `decimal` nel dominio. Le relazioni usano foreign key con eliminazione protetta. Un indice univoco parziale garantisce al massimo un conto principale. `PRAGMA user_version` abilita migrazioni incrementali future senza cancellare il database.

L'inizializzazione aggiunge soltanto le categorie predefinite mancanti, riconoscendole per identificativi stabili anche dopo una rinomina. I database precedenti vengono migrati automaticamente: ogni categoria conserva lo storico e acquisisce tipo e colore dal vecchio gruppo.

La migrazione alla versione 3 ripara i database nei quali i movimenti fanno ancora riferimento alla tabella temporanea `CategoriesV2`. Conserva i movimenti, verifica le relazioni e annulla la riparazione in caso di dati incoerenti. Le migrazioni 2 e 3 sono transazionali. Per aggiornare un dispositivo interessato, installare la nuova versione sopra quella esistente, senza disinstallare l'app.

Gli importi visualizzati usano la valuta dei conti e dei movimenti, indipendentemente dalla regione del dispositivo. I riepiloghi con più valute mostrano `Valute miste`: non è presente un servizio di conversione valutaria.

La migrazione 4 aggiunge l'icona, suggerita dal nome della categoria, e diversifica i vecchi colori predefiniti. Mantiene i colori personalizzati esterni alla precedente palette. Icone e colori scelti successivamente vengono conservati anche dopo il riavvio. Anche questa migrazione è transazionale.

## Wallet

La valutazione per iOS 26.6.1 e carte BCC Alpi Marittime è in [docs/wallet-valutazione.md](docs/wallet-valutazione.md). La cattura dei pagamenti Wallet non è ancora implementata né verificata sul dispositivo.

## Build, test e avvio

Per avviare l'emulatore Android configurato sul PC, da PowerShell nella cartella del progetto:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-emulator.ps1
```

Lo script usa `MAUI_Emulator_API_36`, rendering software e avvio senza caricare lo snapshot. Questa combinazione ha risolto la schermata nera verificata nell'emulatore il 10/10/2026: i controlli e i dati della Home erano caricati, ma non venivano disegnati. App e database vengono conservati. L'avvio a freddo può richiedere circa un minuto o più: aspettare la schermata iniziale di Android prima di aprire l'app. Lasciare il terminale aperto. Per un altro dispositivo usare `-Avd NOME`; per un SDK in un percorso diverso usare `-AndroidSdk PERCORSO`.

```powershell
dotnet restore ExpenseTracker.slnx
dotnet build src/ExpenseTracker/ExpenseTracker.csproj -f net10.0-android
dotnet test tests/ExpenseTracker.Application.Tests/ExpenseTracker.Application.Tests.csproj
```

Con un emulatore o dispositivo Android collegato:

```powershell
dotnet build src/ExpenseTracker/ExpenseTracker.csproj -t:Run -f net10.0-android
```

Su Windows:

```powershell
dotnet run --project src/ExpenseTracker/ExpenseTracker.csproj -f net10.0-windows10.0.19041.0
```

## Aggiornare l'app durante lo sviluppo

Per modificare la UI senza interrompere l'app, usare **XAML Hot Reload** con una sessione di debug. Il progetto usa XAML a runtime in Debug e la generazione del codice in Release.

In **VS Code** (estensioni .NET MAUI, C# e C# Dev Kit):

1. Aprire la cartella principale del progetto. Aprire un file XAML o C# per attivare le estensioni.
2. Con **Ctrl+Shift+P**, scegliere **.NET MAUI: Select Startup Project** e selezionare `src/ExpenseTracker/ExpenseTracker.csproj`; poi **.NET MAUI: Select Startup Device** e scegliere l'emulatore Android.
3. Aprire **Esegui e debug** con **Ctrl+Shift+D**, scegliere **ExpenseTracker Android (Debug + Hot Reload)** e premere **F5**. Il profilo è già presente in `.vscode/launch.json` e abilita la compilazione Debug.
4. Mantenere attivo il debugger. Modificare lo XAML e salvare con **Ctrl+S**. Se le modifiche non compaiono, premere il pulsante **Hot Reload** (fiamma) nella barra di debug. La configurazione del progetto abilita XAML Hot Reload.

Per le modifiche C#, se la fiamma non è disponibile, cercare **Hot Reload** nelle impostazioni utente di VS Code e abilitare l'opzione di C# Dev Kit. L'attivazione va fatta prima di avviare una nuova sessione di debug. Applicare le modifiche supportate con la fiamma; il codice di inizializzazione già eseguito non viene eseguito nuovamente automaticamente.

In **Visual Studio** su Windows:

1. Aprire `ExpenseTracker.slnx`, impostare `ExpenseTracker` come progetto di avvio, scegliere **Debug** e l'emulatore Android già aperto.
2. In **Debug > Opzioni > XAML Hot Reload**, abilitare XAML Hot Reload e il supporto Android/iOS (.NET MAUI). Per applicare solo modifiche complete, attivare anche l'applicazione al salvataggio del documento.
3. Avviare con **F5**, mantenendo collegato il debugger. Cambiare un file XAML e salvarlo: l'interfaccia si aggiorna durante l'esecuzione. Se necessario, usare il pulsante Hot Reload o **Alt+F10** per ricaricare lo XAML modificato.

Nuovi file, pacchetti e modifiche non supportate richiedono una nuova compilazione e distribuzione. Le modifiche all'avvio, alle registrazioni dei servizi o alle migrazioni richiedono anche che il relativo codice venga rieseguito, quindi riavviare l'app per verificarle.

Il comando `dotnet build ... -t:Run` compila, aggiorna e apre l'app, ma non crea una sessione Hot Reload. L'emulatore può restare aperto tra gli aggiornamenti. Non disinstallare l'app e non cancellarne i dati: l'aggiornamento sopra l'installazione esistente conserva il database.

Riferimenti: [XAML Hot Reload](https://learn.microsoft.com/en-us/dotnet/maui/xaml/hot-reload?view=net-maui-10.0) e [elaborazione XAML in .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/xaml/xamlc?view=net-maui-10.0).

## Esportazione CSV

Apri la sezione **Esportazione**, scegli se esportare tutti i movimenti oppure applicare periodo, conto, tipo e categoria, quindi seleziona **Crea e condividi CSV**. Il file viene scritto nella cache consentita dell'app e passato al pannello di condivisione MAUI.

Il tracciato usa `;`, data ISO 8601 e importi invariant culture:

```text
Id;Data;Tipo;Conto;Categoria;Descrizione;Importo;Valuta;Note
```

Virgolette, punti e virgola e ritorni a capo nei campi testuali vengono gestiti secondo le regole CSV.

## Versione 1.3

Funzioni, correzioni, verifiche e limiti sono descritti in [docs/versione-1.3.md](docs/versione-1.3.md).

## Versione 1.4 · Assistente locale

Resoconto del mese e confronto con i giorni corrispondenti del mese precedente, con totali calcolati in C#, collegamento a Ollama sul PC, chiave di accesso, annullamento e gestione offline. La procedura completa per avviare il servizio e collegare l'iPhone è in [docs/assistente-locale.md](docs/assistente-locale.md).

## Versione 1.4.1 · Interfaccia e stabilità

Home semplificata, lista Movimenti ridisegnata, testate unificate e comandi di Pianifica rivisti. Correzioni e controlli sull'iPhone sono descritti in [docs/versione-1.4.1.md](docs/versione-1.4.1.md).

## Versione 1.5 · Chat sui due mesi

Domande libere su mese corrente e precedente: totali, categorie, medie, conteggi e importo massimo. Il modello interpreta la domanda, l'app calcola i dati e la risposta mostra periodo, valuta e fatti utilizzati. La chat conserva il contesto dell'ultima domanda, con annullamento e risposta calcolata in caso di generazione non verificabile. Teoria, esempi e procedura di prova sono in [docs/chat-due-mesi.md](docs/chat-due-mesi.md). Riavviare anche il servizio sul PC per utilizzare i nuovi endpoint.

La **1.5.1 (build 8)** corregge «scorso mese» interpretato come mese corrente e le dichiarazioni di movimenti assenti in presenza di dati. Date e conteggi sono ora inviati anche in campi strutturati per verificare la risposta. Aggiornare sia app sia servizio.


La **1.5.2 (build 9)** porta la chat in inglese e mostra “Thinking…” sotto la domanda durante l’attesa, con annullamento. Il servizio usa `qwen3.5:9b` senza thinking e risposte brevi. Rafforzati i controlli dei confronti e delle false assenze; aggiornare app e riavviare servizio.

La **1.5.3 (build 10)** gestisce anche gli errori di rete restituiti dal client HTTP nativo Android: interruzione, timeout e annullamento. La domanda resta disponibile per riprovare; la diagnostica degli errori inattesi indica la fase senza registrare domande, importi o chiavi. Questo aggiornamento riguarda l'app e non richiede il riavvio del servizio.

La **1.5.4 (build 11)** separa le destinazioni del menu in sezioni Shell indipendenti, ciascuna con una sola scheda. Evita la gestione iOS delle schede aggiuntive nel menu nativo “Altro”, compatibile con il problema delle testate doppie su Esporta e importa, Pianifica e Assistente. Aggiornati anche i collegamenti ai movimenti e all’editor. Dettagli e verifica iPhone in [docs/versione-1.5.4.md](docs/versione-1.5.4.md).

Le modifiche all’interfaccia della versione 1.6.0 (build 12) sono descritte in [docs/versione-1.6.0.md](docs/versione-1.6.0.md). Provenienza e licenza delle icone: [Tabler Icons](docs/licenses/tabler-icons.md).

La **1.6.1 (build 13)** rende flottanti i pulsanti AI e “+”, rimuovendo la fascia inferiore separata su tutte le pagine. Dettagli in [docs/versione-1.6.1.md](docs/versione-1.6.1.md).

# ExpenseTracker

ExpenseTracker è un'applicazione personale offline-first realizzata con .NET MAUI 10, C#, XAML e MVVM. Conti, categorie e movimenti restano nel database SQLite del dispositivo; non sono presenti backend, login o sincronizzazione cloud.

## Funzioni MVP

- gestione di più conti con un solo conto principale e saldo calcolato;
- macro-categorie e categorie di entrata/uscita disattivabili senza eliminare lo storico;
- inserimento, modifica, filtro, dettaglio ed eliminazione dei movimenti;
- dashboard e statistiche per settimana, mese, anno o periodo personalizzato;
- grafici a torta e linee con LiveCharts2;
- esportazione CSV completa o filtrata e condivisione tramite API MAUI.

## Architettura

- `src/ExpenseTracker.Domain`: entità, enumerazioni e regole pure.
- `src/ExpenseTracker.Application`: casi d'uso, filtri, aggregazioni e contratti di persistenza/esportazione. Dipende solo da Domain.
- `src/ExpenseTracker.Infrastructure`: repository SQLite, migrazione/seed e generatore CSV. Dipende da Application.
- `src/ExpenseTracker`: host MAUI e layer Presentation con pagine XAML, ViewModel Toolkit, navigazione Shell e composizione DI.
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

Il file `expense-tracker.db3` viene creato in `FileSystem.AppDataDirectory`. Lo schema versione 1 contiene:

- `Accounts`;
- `MacroCategories`;
- `Categories`, collegata a `MacroCategories`;
- `Transactions`, collegata a `Accounts` e `Categories`.

Gli importi sono salvati in centesimi interi e convertiti in `decimal` nel dominio. Le relazioni usano foreign key con eliminazione protetta. Un indice univoco parziale garantisce al massimo un conto principale. `PRAGMA user_version` abilita migrazioni incrementali future senza cancellare il database.

Al primo avvio vengono inserite macro-categorie e categorie predefinite. Il seed viene eseguito solo se le tabelle delle categorie sono vuote.

## Build, test e avvio

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

## Esportazione CSV

Apri la sezione **Esportazione**, scegli se esportare tutti i movimenti oppure applicare periodo, conto, tipo, macro-categoria e categoria, quindi seleziona **Crea e condividi CSV**. Il file viene scritto nella cache consentita dell'app e passato al pannello di condivisione MAUI.

Il tracciato usa `;`, data ISO 8601 e importi invariant culture:

```text
Id;Data;Tipo;Conto;Categoria;Descrizione;Importo;Valuta;Note
```

Virgolette, punti e virgola e ritorni a capo nei campi testuali vengono gestiti secondo le regole CSV.

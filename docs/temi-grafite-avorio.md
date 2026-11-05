# Grafite e Avorio

L’app riprende i concept approvati: portafoglio grafite con carte argento, avorio e giallo; superfici arrotondate, numeri in evidenza e giallo per le azioni principali. Icona di avvio, schermata iniziale e marchio nel menu usano lo stesso disegno vettoriale.

Nel menu laterale, il **sole** attiva Avorio (chiaro) e la **luna** attiva Grafite (scuro). Il pulsante selezionato è giallo. Grafite è la scelta iniziale; la preferenza viene ricordata dopo il riavvio. Non è presente l’opzione Sistema. Il cambio aggiorna le schermate già aperte, senza ricaricare i dati o azzerare i campi compilati.

La palette semantica interessa anche menu, ricerca, selettori, finestre di dialogo, assistente, stati selezionati e grafici. I testi secondari e i messaggi usano colori distinti per assicurare contrasto su entrambi i fondi. Le icone di navigazione hanno varianti chiare e scure; i colori personalizzati delle categorie restano gli stessi nella selezione, nei movimenti e nei grafici.

La Home conserva i selettori del periodo, la visibilità del saldo, il passaggio dalla ciambella al riepilogo compatto, la differenza del periodo e il confronto. Il riepilogo delle categorie precede la differenza del periodo. I comandi di inserimento, ricerca, filtro, dettaglio, modifica, duplicazione, importazione, esportazione, pianificazione e assistente restano disponibili. Nessuna modifica allo schema del database o ai calcoli.

## Verifica

- Test applicativi: `dotnet test tests/ExpenseTracker.Application.Tests/ExpenseTracker.Application.Tests.csproj`.
- Compilazione Android: `dotnet build src/ExpenseTracker/ExpenseTracker.csproj -f net10.0-android`.
- Navigazione e flussi esistenti nell’emulatore: `python tests/android_ui_smoke.py`.
- Temi, selettori e persistenza dopo il riavvio: `python tests/android_theme_smoke.py` su una build Debug. Il test ripristina il tema iniziale; non salva movimenti.
- Controllare visivamente entrambi i temi nelle schermate catturate, compresi assi, legenda, occhio del saldo e selezioni. Ripetere su iPhone prima della distribuzione iOS.

Le prove locali e le schermate sono raccolte nella cartella ignorata `artifacts/ui-refresh`.

Collaudo del 4 novembre 2026: 223 test applicativi superati. Compilazione Android riuscita sia con XAML a runtime sia con generazione del codice XAML; la compilazione finale non riporta errori o avvisi. Le schermate dell’emulatore documentano i due temi con dati esistenti e stati vuoti. Il database dell’emulatore è rimasto identico byte per byte alla copia iniziale. La verifica su iPhone rimane da eseguire con Xcode e un dispositivo o simulatore iOS.

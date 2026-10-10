# ExpenseTracker 1.4.1 · Interfaccia e stabilità

Versione visualizzata 1.4.1, build 6. Schema database invariato (5): installare l'aggiornamento sopra l'app esistente per conservare i dati.

## Interfaccia

- Home semplificata: rimossi gli ultimi movimenti e il pulsante CSV mensile. L'esportazione resta nella pagina Esporta e importa.
- Movimenti: ricerca e filtri presenti una sola volta nella testata scorrevole della lista; campi a tutta larghezza, azzeramento dei filtri e cancellazione della ricerca. Schede compatte raggruppate per giorno; toccando una scheda si aprono dettaglio, modifica, duplicazione ed eliminazione. Caricamento progressivo conservato.
- Riepiloghi della lista separati per valuta, riferiti ai movimenti visualizzati e senza trasferimenti. Importi visualizzati con separatori italiani anche quando la regione del dispositivo è diversa.
- Barra nativa di Shell nascosta esplicitamente su tutte le pagine principali: resta la sola testata personalizzata con accesso al menu. Testate di Pianifica e Assistente allineate alla larghezza delle altre pagine.
- Pianifica: selettore a tre sezioni con stato attivo visibile, azioni disposte in griglie e campi coerenti con le altre schermate. Icone aggiunte per Pianifica e Assistente nel menu laterale.

## Correzioni individuate nel controllo del codice

- I filtri ricevuti dalla navigazione durante una query vengono applicati al termine del caricamento, anziché restare in attesa del successivo cambio pagina. Una nuova richiesta riparte dai primi 100 movimenti.
- Un intervallo di date non valido svuota anche totali e dettaglio, evitando riepiloghi vecchi accanto a un errore.
- Il salvataggio di un editor di ricorrenza aperto prima della conferma o della sospensione conserva la scadenza aggiornata e lo stato corrente. Il calendario di una ricorrenza esistente resta invariato.
- Eliminando una ricorrenza aperta nell'editor viene chiuso anche l'editor, impedendo di ricrearla involontariamente.
- Pianifica rilegge i conti selezionati per ID dopo modifiche e archiviazioni; aggiorna i nomi e rimuove i riferimenti non più attivi. I comandi delle sezioni sono disabilitati durante le operazioni.

## Verifica

- 85 test .NET passati: suite esistente e 7 nuove regressioni su filtri durante il caricamento, totali e date, conti, ricorrenze e separatori decimali.
- Build Windows con generazione XAML: nessun errore. NuGet segnala l'indisponibilità della verifica online delle vulnerabilità (NU1900).
- Per eseguire i test mentre il servizio assistente è avviato, usare un percorso di output separato:

```powershell
dotnet test tests/ExpenseTracker.Application.Tests/ExpenseTracker.Application.Tests.csproj --no-restore -p:OutputPath=bin/UiVerification/
```

Non è stata effettuata una verifica visiva sull'iPhone né una compilazione iOS in questo ambiente Windows. I test verificano la logica e la build Windows verifica la generazione XAML; il layout nativo iOS resta da controllare sul dispositivo.

## Controllo sull'iPhone

1. Verificare una sola testata entrando in ogni sezione, anche dopo aver cambiato pagina dal menu.
2. Aprire Movimenti con e senza risultati: ricerca e filtri devono comparire una sola volta e restare utilizzabili. Provare ricerca, azzeramento, date personalizzate e più di 100 movimenti.
3. Toccare una scheda, leggere il dettaglio, modificarla e tornare alla lista. Provare duplicazione e annullamento dell'eliminazione; per un trasferimento verificare l'assenza di modifica e duplicazione.
4. Cambiare le tre sezioni di Pianifica, verificare l'evidenziazione e provare budget, ricorrenze e trasferimenti.
5. Con una ricorrenza in modifica, confermare o saltare la scadenza e poi salvare: la data successiva deve restare avanzata. Sospenderla prima di salvare deve conservarne la sospensione.

La logica dell'assistente locale rimane quella della prima fase. La revisione dell'integrazione LLM è il prossimo intervento dopo il controllo di questa build.

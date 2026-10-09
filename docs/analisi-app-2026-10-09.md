# Analisi ExpenseTracker — 9 ottobre 2026

> Stato storico dell’audit. Le successive correzioni e le funzioni implementate sono documentate in [versione-1.3.md](versione-1.3.md); le prove dei difetti sono ora test di regressione nella suite.

## Ambito e risultato

Revisione del codice corrente, incluse le correzioni precedenti per importi manuali e statistiche. Esaminati dominio, servizi, persistenza SQLite, import/export, view model, navigazione e XAML. Nessuna modifica al comportamento dell'app in questa analisi e nessun accesso ai dati dell'iPhone.

Cinque prove diagnostiche su database temporanei hanno confermato quattro problemi: due prove riguardano la rinomina delle categorie. Il sorgente è conservato in `audit-riproduzioni-2026-10-09.cs.txt`: copiandolo temporaneamente nel progetto test come `.cs` si possono rieseguire le asserzioni del comportamento atteso. Attualmente falliscono, documentando i difetti. Non sono incluse nella suite ordinaria.

La verifica sul dispositivo resta necessaria per tastiera, lifecycle delle pagine, gesti, dimensioni dei testi e prestazioni. Nessuna misurazione di prestazioni su iPhone è stata effettuata.

## Problemi riprodotti

### 1. Alta priorità: rinominare una categoria predefinita impedisce l'inizializzazione al riavvio

- Prova: rinominare `Affitto` oppure `Cafe`, chiudere il repository e riaprirlo.
- Risultato: `SQLiteException: UNIQUE constraint failed: Categories.Id`.
- Causa: `SqliteExpenseRepository.cs:455` cerca le categorie predefinite per nome e tipo, poi tenta di reinserirle con lo stesso ID stabile. Il nome modificato non viene riconosciuto.
- Impatto: i caricamenti che dipendono dall'inizializzazione falliscono. I dati restano nel database; non è prova di cancellazione.
- Correzione: riconoscere gli ID già presenti, preservare le personalizzazioni e verificare riavvio dopo rinomina/cambio tipo, oltre alle migrazioni.

### 2. Alta priorità: virgola ancora interpretata male nell'importazione

- Prova: CSV separato da punto e virgola con importo `6,50`.
- Atteso: 6,50. Risultato: 650.
- Causa: `TransactionImportParser.cs:347` prova prima la cultura invariant consentendo i separatori delle migliaia.
- La correzione precedente copre i campi manuali, non questo parser separato.
- Correzione: regole esplicite per ciascun formato importato, riconoscimento dei separatori e anteprima dei valori; nessuna interpretazione silenziosa dei formati ambigui.

### 3. Alta priorità: export e reimport duplicano i movimenti

- Prova: un movimento salvato, esportazione CSV, reimportazione nello stesso database.
- Atteso: un movimento. Risultato: due.
- Causa: `TransactionImportParser.cs:175-194` ignora la colonna `Id`; `TransactionImportService.cs:125` genera un nuovo ID dal contenuto.
- Inoltre la descrizione esportata non viene letta e viene sostituita dal nome della categoria. Gli ID basati sul contenuto degli import esterni cambiano se viene modificato importo, nota o categoria: reimportare una versione corretta può creare nuovi movimenti.
- Correzione: percorso distinto per CSV nativo, conservazione ID/descrizione/note e provenienza esplicita per import esterni. Provare esportazione, ripristino e secondo import.
- Il CSV attuale non è un backup completo: non contiene saldi iniziali, impostazioni dei conti e aspetto/stato delle categorie.

### 4. Alta priorità se si usano più valute: il conto accetta movimenti in un'altra valuta

- Prova: `TransactionService.SaveAsync` accetta una spesa USD su conto EUR senza errore.
- `Account.CalculateCurrentBalance` somma/sottrae gli importi senza controllare la valuta: 100 EUR meno 10 USD viene trattato come saldo numerico 90, presentato nella valuta del conto.
- È anche possibile cambiare la valuta di un conto già usato (`AccountService.cs:32`).
- Correzione iniziale: imporre valuta del movimento uguale a quella del conto e bloccare il cambio valuta con storico, finché non esiste una gestione esplicita dei cambi.
- I riepiloghi con più valute nascondono il totale dietro `Valute miste`, ma serie e percentuali aggregano comunque importi non confrontabili: separare i report per valuta.

## Ulteriori problemi individuati nel codice

Questi casi non sono stati riprodotti su iPhone.

- **Trasferimenti importati:** `TransactionImportParser.cs:88-105` trasforma un trasferimento XLSX in una sola entrata sul destinatario. Se entrambi i conti sono gestiti, manca l'uscita dall'origine e il report conta un trasferimento come reddito. Verificare il significato dei file di origine prima di correggere eventuali dati storici. Servono trasferimenti con due registrazioni collegate, esclusi da entrate e spese.
- **Importazione parziale:** `TransactionImportService` salva conti, categorie e movimenti progressivamente, senza una transazione complessiva. Una riga con importo zero o un errore successivo può lasciare dati già scritti pur mostrando un errore finale. Validare prima e applicare atomicamente, oppure rendere esplicito il risultato parziale.
- **Modifica dello storico archiviato:** l'editor carica solo conti e categorie attivi; il servizio rifiuta quelli inattivi anche in modifica. Dopo l'archiviazione non è possibile correggere una nota mantenendo gli stessi riferimenti senza riattivarli. Consentire i riferimenti storici esistenti, mantenendo il divieto per nuove assegnazioni.
- **Bozza persa cambiando scheda:** `TransactionEditorPage.OnAppearing` richiama il caricamento e il reset quando non arriva una query. Il ritorno dai selettori è protetto, ma il cambio scheda non conserva una bozza. Verificare su iPhone nuovo movimento e modifica parzialmente compilata.
- **Filtri non uniformi:** Movimenti richiede ancora `Applica`, mentre Analisi ora si aggiorna automaticamente. Non è un errore del pulsante, ma un comportamento incoerente rispetto all'aspettativa emersa durante il test. Mostrare anche un riepilogo dei filtri quando il pannello è chiuso.
- **Dettaglio movimento obsoleto:** il caricamento della lista sostituisce i movimenti ma non aggiorna/azzera `SelectedTransaction`. Dopo modifica o filtro il riquadro può descrivere un oggetto precedente o non più visibile.
- **Date CSV ambigue:** il parser prova prima il formato invariant. Date come `04/05/2026` possono assumere un significato diverso da quello italiano. Per CSV esterni scegliere il formato, per CSV nativi usare ISO esplicito.
- **Crescita dello storico:** l'editor legge tutti i movimenti per ordinare le categorie; Home e saldi rileggono lo storico più volte; la lista non ha paginazione. È un rischio di rallentamento, non una lentezza misurata. Preparare dati sintetici da 1.000, 10.000 e 50.000 movimenti e misurare prima di ottimizzare.

## Sviluppo consigliato, in ordine

### Fase 1 — Dati affidabili

Correggere i quattro problemi riprodotti; rendere l'importazione atomica; definire trasferimenti e valuta del conto. Aggiungere test permanenti sui casi confermati. Creare backup/ripristino completo e versionato, esportabile con il pannello di condivisione e verificato su un database vuoto. Mostrare data dell'ultimo backup riuscito.

Criteri di completamento: rinomina seguita da riavvio senza errori; `6,50` conservato in ogni percorso; export/reimport senza duplicati e senza perdita dei campi; nessun saldo ottenuto sommando valute diverse; errore d'importazione senza scritture parziali; ripristino che conserva movimenti, conti, saldi e categorie.

### Fase 2 — Inserimento quotidiano più rapido

Conservare la bozza cambiando scheda, rendere evidente il salvataggio riuscito, aggiungere `Salva e aggiungi un'altra` e duplicazione di un movimento. Ricordare il conto scelto secondo una preferenza esplicita. Migliorare categorie preferite e ricerca nel selettore. Mostrare gli errori accanto ai campi e verificare che tastiera e testo grande non nascondano le azioni.

Criteri: registrare una spesa abituale con importo, categoria e salvataggio; recuperare una bozza dopo un cambio scheda; nessun doppio movimento con tap rapidi; nessun messaggio di successo prima del salvataggio.

### Fase 3 — Ritrovare e capire le spese

Ricerca per descrizione/note, filtri automatici coerenti e visibili, selettore semplice per mesi e anni precedenti. Toccare una categoria del grafico per vedere i movimenti corrispondenti. Dettaglio in una pagina o pannello richiudibile; totali del periodo coerenti con i filtri e la valuta. Unificare il componente grafico condiviso tra Home e Analisi per evitare nuove differenze.

Criteri: dal totale di una categoria ai suoi movimenti in un passaggio; ricerca combinabile con periodo/conto; ritorno alla lista senza perdere posizione e filtri.

### Fase 4 — Pianificazione personale

Movimenti ricorrenti e abbonamenti, inizialmente come proposte da confermare; budget mensile per categoria con speso e residuo. Distinguere sempre previsioni e movimenti contabilizzati. Valutare solo dopo l'uso reale se aggiungere promemoria, widget o integrazioni di pagamento. L'eventuale integrazione Wallet richiede una prova separata sul dispositivo; non è stata rivalutata in questo audit.

## Prossimo ciclo di test iPhone

1. Installazione sopra la versione esistente e verifica conservazione dati.
2. Importi `6,50`, `6.50`, `0,05`, campo vuoto e separatori misti.
3. Cambio rapido mese/anno/conto, periodo personalizzato invalido e successiva correzione.
4. Rinomina categoria, chiusura completa e riapertura.
5. Nuovo movimento, cambio scheda e ritorno; modifica con categoria archiviata.
6. Export/import su dati di prova; importazione contenente una riga invalida.
7. Tastiera aperta, caratteri grandi, categorie con nomi lunghi, grafici senza dati e con molte categorie.

## Priorità pratica

Il prossimo rilascio dovrebbe concentrarsi su affidabilità, backup e conservazione delle bozze. Ricerca e accesso ai movimenti dai grafici sono i miglioramenti successivi più utili per controllare le spese. Budget e automazioni diventano più efficaci quando saldi e importazioni sono verificabili.

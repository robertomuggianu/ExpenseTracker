# Chat sui due mesi · versione 1.5.3, build 10

Questa fase permette di scrivere domande sui movimenti del mese corrente e precedente. Sotto ogni risposta basata sui dati trovi valuta, date inclusive, filtro categoria, conteggi, risultati calcolati e regole di esclusione. Il database resta sul dispositivo; lo schema rimane 5.

## La teoria applicata nel codice

Integrare un LLM significa assegnargli un compito linguistico all'interno di un flusso software verificabile. Qui il modello interpreta la domanda e presenta i risultati; i calcoli avvengono in C#.

1. L'app invia a `/chat/interpret` domanda, data locale, valuta, catalogo dei nomi delle categorie e contesto dell'ultima richiesta. Ollama restituisce un JSON vincolato a uno schema: periodo, operazione, misura e categoria. Può anche chiedere una precisazione o dichiarare la richiesta fuori perimetro.
2. Il server controlla i valori ammessi e il nome della categoria. Lo schema ammette solo filtri nominati nella domanda o mantenuti in una domanda successiva: il modello non può scegliere una categoria arbitraria dal catalogo. Per un filtro usare il nome presente nell'app; sinonimi non riconosciuti richiedono una precisazione. Riferimenti espliciti come «scorso mese», «mese scorso» e «mese precedente» vincolano il periodo dello schema e prevalgono sul contesto; anche l'app applica questo controllo prima di leggere il database. `AssistantChatService` esegue il piano nel database SQLite: le operazioni disponibili sono definite dal codice. Il modello non genera SQL e non modifica i movimenti.
3. L'app invia a `/chat/answer` domanda e risultati calcolati. Una seconda chiamata a Ollama scrive la risposta in italiano. La risposta di riferimento, costruita in C#, è inviata insieme ai fatti.
4. Risposte vuote, malformate, troncate o con numeri assenti dai fatti vengono sostituite dalla risposta calcolata, indicando nell'interfaccia il motivo. L'app invia anche date e conteggi in campi strutturati: dichiarazioni di movimenti assenti vengono sostituite se almeno un periodo contiene movimenti della misura richiesta. Nei confronti misti questo controllo è conservativo: il risultato C# chiarisce quale periodo è vuoto. Errori di rete e timeout permettono invece di riprovare; il testo della domanda viene ripristinato.

È una forma limitata di selezione di strumenti tramite un piano strutturato. Non usa il ciclo autonomo di un agente né il campo nativo `tools` di Ollama. Gli strumenti sono le query C# autorizzate. Un esempio di piano sul protocollo HTTP, per «quanto ho speso questo mese?»:

```json
{"period":0,"operation":1,"measure":0,"category":null,"comparison":0}
```

Gli enum HTTP sono numerici e serializzati con il contesto JSON generato per AOT. Nel JSON richiesto al modello i valori sono stringhe enumerate: `current`, `total`, `expenses`, categoria vuota e `sameDays`. Lo schema limita la forma dell'output, ma non prova che il modello abbia compreso la domanda.

Lo schema è costruito per ogni domanda: restringe i filtri alle categorie esplicite e, per formulazioni di conteggio come «quante entrate» o «numero di movimenti», ammette solo `count`. Questi controlli sono stati aggiunti dopo prove con Qwen che confondeva un conteggio con un totale o selezionava un filtro non richiesto. Il modello continua a scegliere periodo, misura e le altre operazioni.

Richieste con formulazioni esplicite di previsione o simulazione vengono fermate prima dell'inferenza: nella prova «quanto spenderò a novembre?» il modello aveva tentato di sostituire la richiesta futura con un totale storico. È un controllo conservativo su alcune formulazioni italiane e inglesi; per le altre richieste fuori perimetro rimane anche la classificazione del modello.

La cronologia visibile ha un massimo di 20 messaggi. Al modello passiamo solo l'ultimo piano e la domanda precedente, senza i vecchi risultati numerici: i risultati sono ricalcolati. «And last month?» può conservare misura e categoria. Cambiare valuta o svuotare la chat azzera il contesto. Una richiesta di precisazione conserva la domanda da chiarire. La chat non viene salvata su disco.

## Domande da provare

- «How much did I spend this month?»
- «And last month?»
- «How much did I spend on Cibo last month?» — usare il nome effettivo della categoria.
- «Compare my spending this month with last month.»
- «Compare with the full previous month.»
- «Which categories increased most compared with last month?»
- «What is my daily average spending this month?»
- «What was my largest expense this month?»
- «How many income transactions did I record this month?»
- «What is my income minus expenses?»

Per le categorie mostriamo le prime dieci per importo e il resto aggregato; nel confronto, maggiore aumento e diminuzione sono calcolati su **tutte** le categorie, prima di limitare la visualizzazione.

## Periodi e significato dei dati

- Mese corrente: primo giorno fino a oggi incluso. Mese precedente da solo: mese intero.
- Confronto: stessi giorni trascorsi, limitati alla fine del mese precedente. Il mese precedente intero si usa solo se richiesto esplicitamente; la risposta avvisa che il periodo corrente può essere più corto.
- Una valuta alla volta, tutti i conti, nessuna conversione. Storico delle categorie archiviate incluso. Trasferimenti e date esterne al periodo esclusi.
- Media giornaliera: totale diviso per tutti i giorni del periodo, compresi quelli senza movimenti. Media per movimento: totale diviso per i movimenti della misura scelta. Arrotondamento a due decimali.
- Percentuale: variazione divisa per il valore precedente; con base zero non è calcolabile. Per la differenza entrate−spese non proponiamo una percentuale.
- La differenza entrate−spese non è il saldo del conto, né una misura completa del risparmio. Nessuna registrazione non dimostra l'assenza di spese reali.

Le domande libere sono limitate a queste operazioni. Previsioni, viaggio in Giappone, rendimenti/saldi dei singoli conti, motivi di una spesa, negozi o descrizioni e altri periodi richiedono fasi successive. Domande complesse che richiedono più operazioni contemporanee possono essere interpretate solo parzialmente: separarle e verificare il perimetro mostrato sotto la risposta.

Il controllo numerico non verifica il significato: il modello potrebbe usare un numero corretto per descrivere il fatto sbagliato. I dati visibili permettono di accorgersene. Questa fase non garantisce risposte sempre corrette; serve anche a misurare gli errori di interpretazione del modello piccolo.

## Dati inviati e limiti

Il PC riceve domanda (ed eventuale domanda precedente), nomi delle categorie, piano e fatti calcolati. Per l'importo massimo sono inviati anche importo, categoria e data del singolo movimento, senza identificativo, note, descrizione o nome del conto. Non scrivere dettagli privati nella domanda se non vuoi inviarli al PC. Il servizio non conserva chat o database e non registra il corpo delle richieste. Autenticazione, limite HTTP di 32 KB e una sola inferenza contemporanea restano attivi anche sui nuovi endpoint.

Catalogo massimo: 128 nomi, 120 caratteri per nome, 4096 complessivi. Domande entro 1000 caratteri. Il modello usa un contesto di 8192 token; ogni domanda richiede normalmente due inferenze e può essere più lenta dei resoconti precedenti sulla CPU. La risposta è limitata a 250 token; il piano a 160. `think=false` è impostato per entrambe le chiamate. Sono presenti una bolla “Thinking…” sotto la domanda e il pulsante **Cancel response**. “Thinking…” indica solo l’attesa della risposta e non visualizza il ragionamento del modello. La bolla viene sostituita dalla risposta e rimossa su annullamento o errore. Il timeout è di 180 secondi per chiamata a Ollama e 210 secondi dal client. Lasciare la pagina annulla la richiesta.

Il trasporto locale resta HTTP con chiave di accesso, nella rete privata fidata; la configurazione di rete e firewall è in [Assistente locale](assistente-locale.md). Nessun servizio cloud è coinvolto.

## Avvio e prova locale

1. Lasciare Ollama attivo con `qwen3.5:9b` installato.
2. Arrestare il vecchio servizio Assistente con `Ctrl+C`, poi avviare dalla cartella del progetto:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-assistant.ps1
   ```

   Se la chiave viene rigenerata, aggiornala nell'app. Aggiornare sia app sia servizio alla 1.5.2: la risposta richiede i conteggi strutturati. Il server risponde con 426 se riceve il vecchio formato senza questi conteggi. Un vecchio server può rispondere al controllo della connessione ma non conoscere `/chat/interpret`: in quel caso l'app chiede di riavviarlo dalla versione aggiornata.

3. Con l'emulatore già avviato, aggiornare l'app conservando i dati:

   ```powershell
   dotnet build src/ExpenseTracker/ExpenseTracker.csproj -t:Run -f net10.0-android
   ```

4. Aprire Assistente AI. Nel pulsante **Connection**, impostare `http://10.0.2.2:5188` per l'emulatore Android e la chiave del servizio. Sull'iPhone usare l'IP privato del PC. Toccare **Save and check**, scegliere la valuta, scrivere una domanda e premere **Send**. I pulsanti di esempio compilano il campo e permettono di modificare la domanda prima dell'invio.
5. Verificare testo e fatti, provare una domanda successiva, cambiare valuta e annullare una richiesta. Non disinstallare l'app: l'aggiornamento mantiene le spese importate.

## Verifiche ripetibili

```powershell
dotnet test tests/ExpenseTracker.Application.Tests/ExpenseTracker.Application.Tests.csproj --no-restore -p:OutputPath=bin/UiVerification/
python tests/assistant_chat_smoke.py
python tests/assistant_chat_smoke.py --live --model qwen3.5:9b
dotnet build src/ExpenseTracker/ExpenseTracker.csproj -f net10.0-android -p:MauiXamlInflator=SourceGen
```

Lo script HTTP usa il server prodotto dal comando di test nel percorso `bin/UiVerification` per non interferire con il servizio eventualmente acceso. Senza `--live` usa un Ollama simulato; con `--live` richiede Ollama installato e prova Qwen con dati inventati. Nessun file di spese reali viene letto dalle prove. I test coprono calendario, valute, trasferimenti, privacy dei dettagli, operazioni, categorie oltre la soglia di visualizzazione, baseline zero, contratti HTTP/AOT, contesto, annullamento, autenticazione e sostituzione delle risposte non verificabili.

Verifica del 10/10/2026: **109 test xUnit superati**, build Windows con generazione XAML e build/distribuzione Android senza errori. Pagina Assistente controllata visivamente nell'emulatore. Smoke HTTP con Ollama simulato superato. Con Qwen2.5 locale sono stati verificati otto piani (totale, seguito sul mese scorso, confronto, filtro categoria, media giornaliera, importo massimo, conteggio entrate e confronto categorie), una risposta finale con dati inventati e i casi di previsione/categoria inesistente. Le richieste di interpretazione a modello caricato hanno impiegato circa 6–13 secondi; il caricamento iniziale è stato più lento. La prova sull'iPhone resta da effettuare. NuGet ha segnalato `NU1900` per il controllo remoto delle vulnerabilità non raggiungibile; non è un errore di compilazione.

Riferimenti ufficiali: [output strutturati Ollama](https://docs.ollama.com/capabilities/structured-outputs), [API chat](https://docs.ollama.com/api/chat).

## Correzione 1.5.1

La prova reale «controlla I dati dello scorso mese ?» aveva selezionato ottobre invece di settembre, e il modello aveva aggiunto «Zero movimenti significa assenza di registrazioni» nonostante conteggi positivi. La verifica del database dell'emulatore ha confermato la presenza dei movimenti di settembre: l'importazione non era il problema. Sono stati aggiunti vincoli sui riferimenti espliciti al periodo, un controllo equivalente nell'esecuzione locale e un controllo delle dichiarazioni di assenza basato sui conteggi strutturati. La regola sullo zero non viene più proposta al modello come frase generica da ripetere.

Per riprodurre la prova con la domanda dello screenshot, usando dati inventati e un contesto precedente sul mese corrente:

```powershell
python tests/assistant_chat_smoke.py --live --model qwen3.5:9b --period-regression
```

Verifiche della correzione: **122 test xUnit superati**, smoke HTTP con controllo delle false assenze superato, prova con Qwen della domanda originale e del seguito «e il mese scorso?» superata. Il modello ha selezionato il periodo precedente anche partendo da un contesto sul corrente; per la risposta non verificabile è stato mostrato correttamente il risultato C# del 1–30 settembre. Build Android senza errori; dati importati conservati. Il controllo del database è stato effettuato in sola lettura e non ha inviato movimenti reali a Ollama.


## Chat in inglese · 1.5.2

La pagina, i suggerimenti, i messaggi di stato, le istruzioni del modello e i dati sotto la risposta sono in inglese. Le categorie mantengono il nome originale presente nel database: per esempio “Cibo” resta “Cibo”. Importi e date conservano il formato italiano, dichiarato nelle istruzioni, per non cambiare il significato numerico. Le formulazioni italiane esistenti continuano a essere accettate.

Il modello predefinito del servizio e dello script di avvio è ora `qwen3.5:9b`, senza thinking. È possibile scegliere un altro modello senza cambiare codice:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-assistant.ps1 -Model qwen3.5:4b
```

Il confronto sui giorni equivalenti viene vincolato anche nel JSON e nell’esecuzione locale: un modello non può scegliere arbitrariamente il mese precedente intero. Il controllo delle false assenze comprende ora le frasi italiane mostrate negli screenshot e le formulazioni inglesi. È un controllo conservativo: se una risposta con periodi misti suscita un falso positivo viene mostrata la risposta calcolata dall’app. Non è una verifica completa del significato di tutte le frasi generate.


Verifiche della 1.5.2 (10/10/2026): **144 test xUnit superati**, inclusi indicatore durante entrambe le fasi, sostituzione con la risposta, rimozione su errore/annullamento, inglese, confronto sui giorni equivalenti e frasi di falsa assenza degli screenshot. Smoke HTTP con Ollama simulato superato. Smoke reale con `qwen3.5:9b` senza thinking e dati inventati superato: “And last month?” e “Check the data for last month.” hanno selezionato il periodo precedente; la risposta ha mantenuto date e importo corretti. Le due interpretazioni hanno impiegato 85,7 e 72,2 secondi sul PC, quindi le richieste dell’app possono essere più lente dei prompt brevi nel terminale. Build Android con generazione XAML riuscita e app aggiornata nell’emulatore senza cancellare i dati. Pagina in inglese controllata visivamente; ciclo di vita della bolla verificato nei test del ViewModel. La verifica su iPhone resta da effettuare. Avviso `NU1900` per il controllo remoto delle vulnerabilità non raggiungibile; nessun errore di compilazione.


## Errori di rete Android · 1.5.3

Il messaggio generico dello screenshot è stato riprodotto nell’emulatore: interpretazione e calcolo locale riusciti, poi `System.Net.WebException` dal gestore HTTP Android nella fase `generate-answer`. Il client gestiva `HttpRequestException`, ma non questa eccezione nativa. Una seconda richiesta identica è riuscita, confermando che domanda e dati erano utilizzabili; non è stata individuata la causa esterna dell’interruzione di rete.

La 1.5.3 distingue ora interruzione della connessione, timeout e annullamento anche con `WebException`. La domanda viene ripristinata per riprovare e la bolla di attesa viene rimossa. Gli errori inattesi registrano fase, tipo e stack, senza messaggio dell’eccezione, contenuti delle richieste o chiavi. Nessuna modifica al servizio o al database.

Verifica: 149 test xUnit superati, inclusi errore nativo nella seconda fase, ripristino della domanda e annullamento.

Build Android con generazione XAML riuscita, 1.5.3 build 10 installata senza cancellare i dati. Collaudo completo con Ollama locale nell’emulatore: “How much did I spend this month?” conclusa con risposta pronta e dati 01/10/2026–10/10/2026, EUR, 5 movimenti totali, 3 spese, 207,00 EUR. “Thinking…” presente durante entrambe le fasi e sostituito dalla risposta. Verifica su iPhone ancora da effettuare. Avviso NU1900 per il controllo remoto delle vulnerabilità NuGet non raggiungibile; nessun errore di compilazione.

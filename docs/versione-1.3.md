# ExpenseTracker 1.3 — modifiche e verifica

## Funzioni disponibili

- Importi manuali con virgola o punto, senza interpretare il separatore decimale come migliaia. Errori vicino ai campi; massimo due decimali nell'editor.
- Bozza conservata durante il cambio scheda e persistita durante la compilazione; scarto esplicito. Aggiornamento di conti e categorie al ritorno nell'editor senza cancellare i campi.
- Salva e aggiungi un'altra, duplicazione dei movimenti ordinari, messaggio di salvataggio, categorie preferite e preferenza per ricordare l'ultimo conto.
- Ricerca nei selettori e per descrizione/note nella lista movimenti.
- Filtri automatici, riepilogo dei filtri, mese/anno scelto e valuta esplicita. Lista caricata a gruppi di 100 elementi; i totali della lista si riferiscono agli elementi visualizzati.
- Dettaglio movimento aggiornato dopo le modifiche e richiudibile; ripristino della posizione della lista al ritorno quando i filtri restano invariati.
- Grafico a ciambella condiviso tra Home e Analisi. Tocco su fetta o riga della categoria per aprire i movimenti con periodo, tipo, categoria e valuta applicati.
- Nuova sezione **Altro → Budget, ricorrenze e trasferimenti**, disponibile anche dal menu laterale.
- Budget per mese, categoria e valuta, con speso, residuo e superamento; modifica ed eliminazione.
- Ricorrenze settimanali, mensili e annuali: creazione, modifica dei dati, sospensione, ripresa, eliminazione, salto e conferma della scadenza. Nessun movimento viene generato prima della conferma. Il 31 gennaio continua il 28/29 febbraio e poi il 31 marzo.
- Trasferimenti tra conti nella stessa valuta: addebito e accredito atomici, esclusi dai report entrate/spese. Eliminando una registrazione viene eliminata l'intera coppia. Per correggere un trasferimento lo si elimina e ricrea; la modifica ordinaria è disabilitata.

## Correzioni dei dati

- Le categorie predefinite rinominate vengono riconosciute per ID e non reinserite al riavvio.
- CSV nativo: conservazione di ID, descrizione, note, data e coppie di trasferimento; reimport senza duplicazione.
- Importi importati con virgola o punto decimale; formati con separatori delle migliaia respinti per evitare interpretazioni ambigue.
- Date italiane interpretate giorno/mese/anno; date native ISO preservate.
- Importazione salvata in una transazione: errori di riga o di salvataggio non lasciano conti, categorie o movimenti parziali.
- Importazione XLSX dei trasferimenti con due registrazioni collegate, anziché una sola entrata.
- Blocco delle valute diverse da quella del conto e del cambio valuta per conti con storico. I vecchi movimenti incoerenti non sono convertiti automaticamente: i saldi li escludono e mostrano un avviso di saldo parziale per consentire la correzione manuale.
- Possibile correggere un movimento mantenendo il suo conto o la sua categoria archiviati. I nuovi movimenti richiedono riferimenti attivi.
- Saldi e frequenze delle categorie calcolati in SQLite, senza caricare tutti i movimenti nell'editor. Home legge il periodo richiesto; grafici lunghi aggregati per mese.
- Chiusura dei selettori e delle finestre di conferma protetta dai doppi tap.

## Persistenza e compatibilità

Schema database 5: aggiunta opzionale `Transactions.TransferId`, indice e tabella `AppState` per la pianificazione. Nessuna cancellazione dello storico. Le preferenze dell'editor e la bozza sono salvate nelle preferenze locali dell'app. Versione visualizzata 1.3, build 4.

Le vecchie rettifiche di trasferimento già importate non vengono riconvertite senza il file sorgente. Reimportando lo stesso XLSX, gli ID precedenti del destinatario sono riconosciuti e viene aggiunto l'addebito associato. Per file esterni modificati e privi di ID affidabili non è possibile distinguere automaticamente una correzione da una nuova spesa identica: il messaggio di importazione chiarisce questo limite.

Il CSV rimane un'esportazione dei movimenti, non un backup completo di saldi iniziali, budget e preferenze. Il backup/ripristino completo del punto 1 del piano resta un intervento distinto dai punti 2–4 richiesti qui. Non sono state aggiunte notifiche automatiche né integrazioni Wallet.

## Verifiche

- 57 test superati: unitari, integrazione SQLite e view model, inclusi i quattro difetti riprodotti nell'audit e la migrazione dallo schema 4.
- Test dei view model effettivi per bozze, aggiornamento dei selettori al ritorno, duplicazione, doppi tap, statistiche e dettaglio dei movimenti.
- Compilazione Windows con generazione XAML abilitata, oltre alla compilazione ordinaria.
- La compilazione Windows Release standard richiede un runtime `Microsoft.NETCore.App.Runtime.win-x64` non presente nell'ambiente; è stata verificata la generazione XAML usando la configurazione Debug disponibile.
- Nessuna installazione o verifica visiva sul dispositivo iPhone effettuata in questo ambiente.

## Prova consigliata sull'iPhone

Aggiornare sopra l'installazione esistente. Verificare un importo 6,50, una bozza dopo cambio scheda, una duplicazione, Salva e aggiungi un'altra, ricerca, cambio rapido mese/anno/conto e tocco sui grafici. In Pianifica creare un budget, confermare una ricorrenza dovuta e registrare un trasferimento fra due conti di prova; controllare che il trasferimento non aumenti entrate/spese. Verificare tastiera, caratteri grandi e la posizione della lista dopo una modifica.

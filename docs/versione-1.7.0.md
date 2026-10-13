# ExpenseTracker 1.7.0 — Periodi e stabilità

Build 14. Bundle ID e schema del database invariati; aggiornare sopra la versione esistente.

## Modifiche

- Home: settimana, mese, anno e intervallo personalizzato; differenza sotto il grafico; dettaglio accessibile dalla lista delle categorie.
- Salvataggio: conservazione dei filtri della lista Movimenti, compreso un intervallo di un solo giorno.
- Editor: aggiornamento delle categorie in blocco, protezione da selettori sovrapposti e ricaricamenti al ritorno dal selettore, recupero delle bozze con movimento eliminato.

## Audit del 13 ottobre 2026

Revisione di editor, report, pianificazione, trasferimenti, import/export, persistenza e percorso di distribuzione iOS. Nessuna modifica ai dati del telefono o ai movimenti nell'emulatore durante questo audit.

Difetti confermati e corretti:

1. Serializzazione delle bozze e della pianificazione dipendente dalla reflection. Sei test fallivano disabilitando la reflection JSON, inclusa la conversione entrata/uscita. Ora entrambi usano metadati generati. Anche la formattazione dei nomi categoria nell'assistente locale non dipende più dalla reflection. Questo riproduce il difetto di compatibilità, ma non dimostra da solo la causa del precedente crash su iPhone.
2. Trasferimenti: 1,234 veniva salvato arrotondato anziché rifiutato. Ora sono accettati soltanto importi positivi con massimo due decimali; il test controlla che un errore non scriva movimenti.
3. Home: durante il caricamento del confronto mensile i nuovi totali e l'intervallo di dettaglio potevano convivere con le vecchie categorie. La pubblicazione dei dati avviene ora dopo il completamento delle letture. Test con repository rallentato e verifica del filtro di dettaglio.

Verifica locale: 163 test ordinari superati; 36 test mirati superati con reflection JSON disabilitata. Il workflow IPA ripete questi ultimi prima della compilazione iOS. Non equivale a un test dell'interfaccia su iPhone.

## Limiti e funzioni da valutare

- Manca un backup/ripristino completo e versionato. Il CSV contiene movimenti e trasferimenti, ma non preserva saldi iniziali, impostazioni dei conti, personalizzazioni delle categorie, budget, ricorrenze e preferenze.
- Nei Movimenti i totali riguardano le righe caricate (paginazione iniziale 100), non necessariamente l'intero intervallo. Valutare un totale completo separato e un'etichetta più esplicita.
- Un export filtrato che include solo metà di un trasferimento non è reimportabile come coppia: l'importazione lo rifiuta per proteggere l'integrità.
- L'editor conserva una sola bozza. L'apertura di un altro movimento può sostituirla dopo conferma; non esiste un archivio di bozze.
- Verifica finale su iPhone ancora necessaria: aggiornamento senza disinstallazione, cambio tipo ripetuto, ripristino bozza dopo chiusura, selettori, tastiera, nuovi periodi e ritorno alla lista.

Non sono state aggiunte nuove funzioni oltre a quelle concordate; le voci sopra sono limiti osservati e proposte da valutare.

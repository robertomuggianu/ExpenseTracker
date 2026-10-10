# ExpenseTracker 1.6.0 · Interfaccia e accesso alla chat

Build 12. Aggiornare sopra l'installazione esistente: identificativo e schema SQLite rimangono invariati.

## Modifiche

- Testate senza sottotitoli decorativi e con meno spazio verticale. Conservata la struttura Shell della 1.5.4 che evita il menu nativo iOS “More”.
- Ricerca comune con lente, superficie verde arrotondata e cancellazione del testo, nei movimenti, nelle categorie e nei selettori. Rimosso il SearchBar nativo.
- Categorie in griglia a quattro colonne, con sole icone. I nomi restano per ricerca, modifica e accessibilità; le categorie inattive appaiono attenuate. Toccare un'icona apre la modifica e porta il modulo in vista.
- Catalogo di 45 icone Tabler Outline incluse nell'app. Sostituite le 16 risorse precedenti mantenendo le chiavi salvate: le personalizzazioni esistenti non vengono sovrascritte. Le nuove icone sono selezionabili nell'editor; le nuove categorie/importazioni senza icona esplicita riconoscono altri nomi comuni. Fonte e licenza in `docs/licenses/tabler-icons.md`.
- Rimossa la destinazione Assistente dal menu e dalla pagina Altro. Pulsante AI in basso a sinistra nelle pagine principali, nei moduli e nei selettori/dialoghi dell'app. La chat si apre a schermo intero sopra la pagina corrente; la freccia nella testata e il Back Android riportano alla pagina di partenza. Nei selettori aperti dalla chat non si ripropone il pulsante che aprirebbe la stessa chat.
- Chat mantenuta in memoria per la sessione; nessun salvataggio permanente della conversazione. Il ritorno dalla chat preserva i campi dell'editor, compresa la modifica di un movimento esistente. Chiudere la chat annulla una richiesta ancora in corso.
- Le azioni inferiori hanno spazio riservato, evitando di coprire moduli o righe. In Home e Movimenti, AI e “+” condividono la stessa fascia inferiore.
- Rimossa la scelta della valuta dalla Home. Si usa il conto principale attivo; in sua assenza il primo conto attivo per data di creazione e identificativo; senza conti attivi EUR. I totali e il grafico includono solo quella valuta, che resta indicata negli importi. Statistiche, movimenti e chat conservano i rispettivi selettori.

## Correzione emersa dai test

Il salvataggio di un cambio di conto principale dipendeva dall'ordine degli elementi ricevuti: assegnare prima il nuovo principale violava l'indice univoco SQLite. Ora, nella stessa transazione, vengono salvati prima i conti non principali, poi quello principale. L'indice mantiene la protezione contro due principali contemporanei.

Le prove ripetute della navigazione Android hanno inoltre riprodotto una testata sovrapposta alla barra di stato dopo il ritorno dalla chat. Si richiede nuovamente il calcolo degli inset della finestra dopo navigazione Shell e chiusura della chat, senza cambiare i dati o ricaricare i moduli. Il comportamento è compatibile con il problema MAUI documentato in https://github.com/dotnet/maui/issues/36728; la diagnosi di questo caso deriva dalle schermate osservate nell'emulatore.

## Verifica

156 test automatici superati: aggiunti controlli su Home con valute miste e conti archiviati, ricerca/modifica delle categorie e conservazione delle vecchie chiavi icona. XML e corrispondenza fra 45 chiavi, mappa Tabler e SVG verificati.

Build Android con SourceGen riuscita e installata nell'emulatore conservando i dati. Provate tutte le otto destinazioni del menu: Home, Conti, Movimenti, Statistiche, Categorie, Esporta e importa, Pianifica e Nuovo movimento. Per ciascuna verificate testata singola, pulsante AI, apertura e ritorno dalla chat e posizione della testata sotto la barra di stato. Nell'editor è stata preservata una bozza di modifica già presente, senza salvare movimenti di prova.

Verificati ricerca e cancellazione nelle categorie; apertura della chat da un selettore con mantenimento del filtro non confermato; annullamento del selettore; Back Android per chiudere la chat. Avviso NU1900 per il servizio remoto di verifica vulnerabilità NuGet non raggiungibile; nessun errore di compilazione.

Toccata inoltre una categoria dalla griglia: editor aperto, campi conservati dopo apertura/chiusura della chat e modifica annullata senza salvare.

La compilazione e la verifica visiva iOS richiedono il workflow macOS e una nuova installazione sul telefono; non sono verificabili direttamente da questo ambiente Windows.

Per iPhone: pubblicare il ramo con Git, eseguire il workflow “iPhone IPA for Sideloadly”, scaricare il nuovo IPA e aggiornarlo sopra l'app esistente. Verificare `v1.6.0` nel menu, testate singole e ritorno dalla chat a un modulo compilato. Non serve aggiornare il servizio AI sul PC.

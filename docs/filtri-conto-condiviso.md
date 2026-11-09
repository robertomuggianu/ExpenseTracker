# Filtri «Prima i dati» e conto condiviso

Home, Movimenti e Statistiche usano lo stesso controllo del periodo: Settimana, Mese, Anno e Periodo. Le frecce laterali cambiano l'intervallo; il titolo centrale è una semplice etichetta informativa, senza freccina o azioni al tocco. Periodo mostra le date iniziale e finale. I risultati si aggiornano dopo la selezione, senza un pulsante Aggiorna.

Il pulsante del portafoglio accanto al saldo nella Home sceglie il conto per la consultazione. La scelta viene conservata al riavvio e vale per saldo, riepiloghi, movimenti, statistiche, esportazioni, assistente e pianificazione. «Tutti i conti» ripristina la vista complessiva. La valuta dei report segue il conto; nella vista complessiva viene usata quella del conto principale attivo, senza sommare valute diverse. Movimenti mantiene i totali separati per valuta.

I selettori dell'app sono pulsanti con il nome dell'opzione, senza frecce verso il basso. Le finestre di scelta usano una spunta e un fondo discreto per la selezione. Nei conti vengono mostrate l'icona salvata, la valuta e l'eventuale indicazione «Principale», sia nella Home sia nei moduli dei movimenti e della pianificazione.

Nei nuovi movimenti il conto della Home è il valore iniziale: il campo resta modificabile. Modificare un movimento esistente conserva il suo conto; cambiare il conto nel modulo non cambia la selezione globale. Le bozze già compilate vengono conservate. Conti e Categorie restano cataloghi di gestione, per poter amministrare anche gli altri elementi.

«Azzera filtri» in Movimenti azzera i criteri locali senza cambiare il conto condiviso. I collegamenti dalle categorie mantengono conto, periodo, tipo e valuta del report.

In Esporta, «Esporta tutto» significa tutto lo storico del conto selezionato. Per esportare il backup completo, con entrambe le registrazioni dei trasferimenti, scegliere prima «Tutti i conti» nella Home. Il testo della schermata rende esplicita questa differenza. L'importazione mantiene i conti indicati nel file.

Le ricorrenze vengono filtrate per conto. I nuovi budget possono essere specifici del conto; i budget precedenti restano generali e sono disponibili in «Tutti i conti». I due ambiti non si sovrascrivono. I trasferimenti continuano a permettere la scelta di entrambi i conti.

L'assistente calcola i dati sul conto selezionato, senza inviarne nome o identificativo al servizio. Cambiare conto annulla eventuali richieste e azzera il contesto della conversazione per evitare riferimenti al conto precedente.

L'indicatore del periodo scorre per 190 ms con un'ombra discreta. Il movimento viene disattivato quando iOS richiede Riduci movimento o Android disabilita le animazioni. Il controllo usa gli stessi colori Avorio/Grafite dell'app.

## Verifica

- `AccountScopeTests`: selezione condivisa, persistenza, conto mancante, valute, aggiornamento automatico, esportazione, assistente, editor, ricorrenze e compatibilità dei budget precedenti.
- `tests/android_ui_smoke.py`: navigazione, selettore dei conti, cambio periodo nelle tre schermate, assenza del comando Aggiorna e del filtro valuta in Statistiche.
- `tests/android_filter_scope_smoke.py`: verifica mirata del selettore conti, dei periodi e delle tre schermate in Avorio; ripristina il tema indicato con `--restore-theme` (Grafite per impostazione predefinita).
- Suite .NET: 236 test superati. Build Android Release: nessun errore o avviso.
- La verifica grafica su Android non sostituisce il collaudo su iPhone delle dimensioni native e delle animazioni.

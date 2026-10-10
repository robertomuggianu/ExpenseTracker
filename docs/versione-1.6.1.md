# ExpenseTracker 1.6.1 · Pulsanti flottanti

Build 13. Il pulsante AI e, su Home e Movimenti, il pulsante “+” sono sovrapposti al contenuto della pagina. Rimossa la riga inferiore dedicata: liste e moduli occupano tutta l'area disponibile, fino alla zona sicura del dispositivo.

Solo i singoli pulsanti intercettano i tocchi. Il contenuto scorre anche nello spazio tra i pulsanti. Lo spazio finale all'interno dei contenuti scorrevoli consente di portare gli ultimi elementi sopra i pulsanti; anche dettaglio movimento e selettori mantengono raggiungibili i loro comandi.

Identificativo dell'app e database invariati. Per iPhone creare il nuovo IPA con il workflow macOS e aggiornare l'installazione esistente con Sideloadly. Il servizio AI non richiede modifiche.

## Verifica

Build Android SourceGen riuscita e installata nell'emulatore, senza errori di compilazione; rimane l'avviso NU1900 per il servizio di verifica vulnerabilità NuGet non raggiungibile. File XML validi e controllo degli spazi del diff superato. La verifica iOS richiede il nuovo IPA e il telefono.

Verificate le otto schede del menu con apertura e ritorno dalla chat. Nei Movimenti verificati contenuto visibile dietro ai pulsanti, tocco di una riga nello spazio fra le azioni e scorrimento dei comandi finali del dettaglio sopra i pulsanti. Nell'editor verificati comandi Salva e fine dei dettagli raggiungibili, apertura e annullamento del selettore conto senza sovrapposizione con l'AI. Nessun movimento di prova salvato; bozza esistente conservata.

Nel controllo finale, il ritorno alla Home dal modulo ha riprodotto una testata sotto la barra di stato Android. Il ricalcolo degli inset viene ora ripetuto una volta dopo 350 ms, al termine della chiusura del menu. Ricompilato e verificato il percorso editor → Home → Movimenti → Home, più apertura e ritorno dalla chat: testate sotto la barra di stato in tutti questi passaggi.

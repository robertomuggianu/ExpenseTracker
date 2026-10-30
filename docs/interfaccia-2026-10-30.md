# Home, categorie e inserimento — 30 ottobre 2026

## Modifiche

- Home: intestazione con saldo e schede sempre visibile; scorrendo il riepilogo, la ciambella viene sostituita da una barra orizzontale compatta che resta sopra la lista. La dimensione dei segmenti dipende dagli importi effettivi, con gli stessi colori e lo stesso ordine delle categorie. Un tocco su un segmento apre i movimenti della categoria nel periodo selezionato. Risalendo riappare la ciambella.
- Il comando accanto al saldo mostra o nasconde il totale dei conti anche nel menu laterale. La scelta è salvata nelle preferenze locali e vale anche dopo il riavvio. Quando è nascosto, neppure il testo per il lettore di schermo contiene il saldo. Totali del periodo e calcoli rimangono disponibili.
- Categorie: schede Spese/Entrate, griglia a quattro colonne con icone circolari, nomi su più righe e riquadro Crea. La ricerca si applica alla scheda corrente; la creazione preseleziona il suo tipo. Le categorie disattivate restano modificabili e sono attenuate visivamente.
- Movimento: data sempre visibile subito sotto l'importo, mantenendo il recupero della bozza. Ora, descrizione, note e valuta restano nella sezione facoltativa. I comandi di salvataggio seguono tutti i campi.

Su finestre basse (per esempio un telefono in orizzontale) la Home scorre interamente per mantenere accessibile la lista. Nessuna modifica allo schema del database, alle categorie salvate o allo storico.

## Verifica manuale

1. Nella Home scorrere fino alle categorie e risalire: passaggio tra ciambella e barra senza spostamenti della lista. Cambiare tipo, periodo e mese anche dal riepilogo compatto; provare periodi vuoti e personalizzati.
2. Nascondere il saldo, passare a un'altra pagina e riaprire l'app: il totale resta nascosto. Mostrarlo nuovamente e controllare che il valore sia invariato.
3. Nelle Categorie controllare i nomi lunghi, cercare per nome in entrambe le schede, aprire una categoria e annullare. Da Entrate, usare Crea e verificare il tipo iniziale.
4. Nel nuovo movimento cambiare la data senza aprire i dettagli. Uscire e rientrare per recuperare la bozza; salvare e verificare il giorno nella lista. Ripetere con modifica, duplicazione e Salva e aggiungi un'altra.

La compilazione Android e i test automatici non sostituiscono la verifica finale su iPhone.

## Controlli eseguiti

- 198 test automatici superati, inclusi saldo nascosto persistente, ricerca e creazione per tipo e salvataggio di una data precedente con dettagli chiusi.
- 80 test mirati superati con serializzazione JSON tramite reflection disabilitata.
- Compilazione Android riuscita sia con XAML a runtime sia con generazione del codice XAML, senza errori o avvisi.
- Prova nell'emulatore Android: griglia con nomi, data visibile, saldo nascosto anche nel menu e dopo il riavvio, passaggio ciambella/barra e ritorno. I dati dimostrativi usati per la prova sono stati rimossi ripristinando il database originale, verificato byte per byte.

# ExpenseTracker 1.5.4 · Navigazione e testate iPhone

Versione 1.5.4, build 11. Database e identificativo dell’app invariati: aggiornare sopra l’installazione esistente.

## Diagnosi

La doppia testata osservata su iPhone è compatibile con la barra nativa Shell sopra il componente PageHeader dell’app. Le pagine hanno già `Shell.NavBarIsVisible="False"` in XAML e la stessa proprietà viene applicata alla pagina risolta dal contenitore DI.

La struttura precedente, però, aveva dieci Tab dentro un solo FlyoutItem. Nascondere la barra inferiore non rimuove le schede dalla struttura nativa. MAUI documenta un problema iOS con le destinazioni oltre le prime quattro, gestite dal controller del menu “More”: la barra nativa può ricomparire anche con NavBarIsVisible e TabBarIsVisible disattivati. Le pagine segnalate occupavano quelle posizioni. Il comportamento differente di Android è riportato nella stessa segnalazione: https://github.com/dotnet/maui/issues/8017.

Non è stata osservata direttamente la gerarchia UIKit sul telefono di Roberto: questa è una diagnosi basata sulla struttura del codice, sui sintomi e sulla segnalazione ufficiale, da confermare con la nuova installazione su iPhone.

## Modifica

Ogni destinazione del menu ha ora un FlyoutItem indipendente con un solo Tab. Non esiste più un gruppo da dieci schede che richieda il menu nativo “More”. La testata personalizzata resta su ogni pagina; il menu laterale mantiene ordine, icone e pulsanti. La creazione delle pagine tramite DI e i caricamenti dei ViewModel restano gli stessi.

Aggiornate tutte le rotte interne: menu principale, pagina Altro, apertura dell’editor con transactionId/duplicate e ritorno ai movimenti con filtri e messaggio. Le rotte sono ora della forma `//PlanningRoot/Planning` anziché `//Main/Planning`.

## Controllo iPhone

Generare un nuovo IPA dal ramo aggiornato, verificare 1.5.4 nel menu e installarlo sopra l’app esistente. Non serve modificare il servizio AI sul PC.

1. Aprire Esporta e importa, Pianifica e Assistente dal menu: una sola testata con un solo pulsante menu.
2. Ripetere passando prima per Home e Movimenti, poi tornare alle tre pagine.
3. Provare il selettore di Pianifica e un selettore a scelta; dopo la chiusura resta una sola testata.
4. Aprire un movimento in modifica, tornare alla lista e verificare i collegamenti senza salvare modifiche di prova indesiderate.
5. Controllare anche Categorie e Nuovo movimento, che nella struttura precedente erano oltre le prime quattro schede.

La verifica visiva su iPhone e la compilazione iOS devono essere effettuate con il workflow macOS: non sono disponibili nell’ambiente Windows locale.

## Verifiche locali del 10/10/2026

Build Android con generazione XAML riuscita e 1.5.4 build 11 installata nell’emulatore conservando i dati. Verificati i nove pulsanti del menu: Esporta e importa, Pianifica, Assistente, Categorie, Conti, Statistiche, Nuovo movimento, Movimenti e Home. Le pagine con PageHeader hanno una sola testata e un solo pulsante menu. Aperto inoltre il dettaglio di un movimento e verificata la navigazione a “Modifica movimento” con transactionId, senza salvare né eliminare dati.

Controllo statico: dieci gruppi Shell, una sola sezione per gruppo, 17 riferimenti alle rotte validi e nessun collegamento residuo a //Main. `git diff --check` passato. La suite di 149 test della 1.5.3 non è stata rieseguita: questa modifica riguarda solo struttura Shell, rotte e versione; la verifica di tali cambiamenti è stata eseguita sul dispositivo Android. Avviso NU1900 per la verifica remota delle vulnerabilità NuGet non raggiungibile; nessun errore di compilazione.

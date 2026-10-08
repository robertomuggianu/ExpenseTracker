# Installazione su iPhone da Windows

Il workflow `.github/workflows/ios-sideloadly.yml` compila l'app su un runner
macOS di GitHub e crea un IPA non firmato per un iPhone fisico (ARM64).
Sideloadly deve firmarlo con il tuo account Apple prima dell'installazione.
Non inserire password Apple, certificati o profili nel repository o nei suoi secrets:
questa procedura non li richiede sul server di compilazione.

## Generare e scaricare il file

1. Carica su GitHub il workflow e la versione del codice che vuoi compilare.
   Per vedere il pulsante di avvio manuale, il workflow deve essere presente
   nel branch predefinito del repository. Le modifiche rimaste solo sul PC
   non saranno incluse nella compilazione.
2. Apri https://github.com/robertomuggianu/ExpenseTracker/actions e seleziona
   **iPhone IPA for Sideloadly**.
3. Premi **Run workflow**, seleziona il branch da compilare e conferma.
4. Attendi che l'esecuzione finisca con successo. Aprila e, nella sezione
   **Artifacts**, scarica **ExpenseTracker-iPhone-<numero>**.
5. Estrai lo ZIP scaricato: contiene **ExpenseTracker.ipa**.

Il workflow parte solo manualmente. La disponibilità e gli eventuali costi
dei minuti macOS dipendono dal piano GitHub e dal budget Actions del repository.
Gli artifact scadono dopo 14 giorni: conserva il file IPA sul PC per i rinnovi.

## Installazione e rinnovo

1. Collega l'iPhone al PC e autorizza il computer sul telefono.
2. Apri Sideloadly, seleziona il telefono e trascina **ExpenseTracker.ipa**.
3. Usa il tuo account Apple in Sideloadly e premi **Start**.
4. Segui le richieste di autorizzazione del profilo sviluppatore su iPhone;
   abilita la modalità sviluppatore se richiesta dalla versione di iOS.
5. Con un account gratuito, rinnova la firma entro sette giorni tramite
   Sideloadly, oppure configura il suo rinnovo automatico.

Non occorre ricompilare per rinnovare la stessa versione. Per aggiornare il
codice serve invece un nuovo IPA. Mantieni lo stesso account e bundle ID e
aggiorna l'app senza disinstallarla: il database delle spese è locale.
L'identificativo attuale è `com.companyname.expensetracker`.

## Stato e problemi di compilazione

La procedura è predisposta, ma la prima build su macOS e l'installazione su
iPhone devono ancora essere verificate. Un workflow riuscito verifica la
creazione dell'archivio per iPhone, non il corretto funzionamento sul telefono.

Il workflow fissa .NET SDK **10.0.401**, workload set **10.0.401** e
**Xcode 26.6**, selezionato tramite `DEVELOPER_DIR` sul runner macOS 26.
Questa combinazione usa .NET per iOS **26.5.10318**. Il workload set
**10.0.401.1** richiede invece Xcode 27: non è intercambiabile con 10.0.401.
Il file `global.json` che impedisce la selezione di altri SDK è generato solo
durante il workflow, senza cambiare l'SDK usato sul PC Windows.
Per aggiornare gli strumenti, allineare SDK, workload set e Xcode secondo
le note di rilascio e controllare il passaggio **Show build tool versions**.
Se una build fallisce, non esiste ancora un IPA utilizzabile per quella esecuzione:
aprire il primo passaggio rosso e leggere l'errore.

Riferimenti:
- https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode26.5-10318
- https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-properties
- https://docs.github.com/en/actions/managing-workflow-runs/manually-running-a-workflow
- https://sideloadly.io/faq.html

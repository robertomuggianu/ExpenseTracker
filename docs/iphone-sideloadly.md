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

Il workflow usa l'ultima versione disponibile di .NET 10 e il workload MAUI iOS,
con l'Xcode predefinito del runner macOS 26. Se un aggiornamento introduce una
richiesta di una versione diversa di Xcode, consultare il passaggio di log
**Show build tool versions** e allineare Xcode e workload prima di riprovare.
Se una build fallisce, non esiste ancora un IPA utilizzabile per quella esecuzione:
aprire il primo passaggio rosso e leggere l'errore.

Riferimenti:
- https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-properties
- https://docs.github.com/en/actions/managing-workflow-runs/manually-running-a-workflow
- https://sideloadly.io/faq.html

# Valutazione Wallet per ExpenseTracker

Valutazione dell'8 ottobre 2026 per iOS 26.6.1 e una carta descritta come Bancomat/prepagata di Banca Alpi Marittime. Il modello esatto della carta e l'emittente devono essere verificati; la descrizione non identifica un prodotto univoco.

## Evidenze

- La guida Apple per iOS 26 documenta un trigger Transazione in Comandi Rapidi: si sceglie una carta e l'automazione si attiva al tap. Questo consente una prova locale senza backend. Fonte: [Apple, Transaction triggers](https://support.apple.com/guide/shortcuts/apd65c67538a/ios).
- BCC Alpi Marittime dichiara Apple Pay tra i servizi supportati da CartaBCC Prepaid IBAN. È una verifica del prodotto pubblicato, non una conferma del modello posseduto dall'utente o dell'affidabilità dell'automazione. Fonte: [CartaBCC Prepaid IBAN](https://www.bccalpimarittime.it/catalogo/dettaglio.asp?hProdottoCatalogoID=27581&i_catalogoID=69634&i_menuid=69634).
- FinanceKit richiede un entitlement approvato e distribuzione App Store nei mercati supportati. Apple documenta prodotti supportati negli Stati Uniti e nel Regno Unito. Questi requisiti non corrispondono all'attuale app personale distribuita con Sideloadly in Italia. Fonte: [Get started with FinanceKit](https://developer.apple.com/financekit/).

## Percorso consigliato

La prima integrazione dovrebbe ricevere un nuovo pagamento da Comandi Rapidi e creare una bozza locale: importo, valuta, negoziante, data e conto. L'utente sceglie la categoria e salva. Solo dopo aver verificato la qualità dei dati si potranno aggiungere regole negoziante → categoria e registrazione automatica per regole esplicite.

Per il prototipo si può predisporre un URL dell'app che apre l'editor con i dati ricevuti. Questa soluzione apre l'app e può richiedere lo sblocco: non deve essere presentata come salvataggio silenzioso in background. Per un'integrazione più completa va valutata un'azione nativa App Intents, inclusa la sua compilazione e firma nel percorso iPhone attuale.

L'automazione non dà accesso generale allo storico bancario. Non assumere che copra pagamenti con carta fisica, prelievi, bonifici, Apple Watch o acquisti web/in-app: ogni canale va provato separatamente. Il trigger al tap documentato da Apple costituisce il primo caso da verificare.

## Prima prova sul telefono

1. In Comandi Rapidi aprire Automazione e cercare Transazione/Wallet.
2. Controllare che la carta effettivamente usata in Apple Pay sia selezionabile e impostare l'esecuzione immediata, se disponibile.
3. Aggiungere temporaneamente un'azione che mostri l'input della transazione e verificare quali dettagli si possono estrarre: importo, negoziante, carta ed eventuale valuta.
4. Alla prossima spesa normale con iPhone, confrontare i dati mostrati con ricevuta e movimento bancario. Non serve effettuare un acquisto dedicato al test.
5. Ripetere con la prepagata e con l'eventuale distinta carta di debito. Registrare eventuali campi assenti, ritardi, errori o doppie attivazioni.

Questa prova non è stata eseguita: il dispositivo non è collegato al workspace. L'app non contiene ancora un'azione Comandi Rapidi o un ricevitore URL Wallet.

## Requisiti prima del salvataggio automatico

- Conservare una bozza senza categoria anziché scegliere una categoria arbitraria.
- Gestire importi mancanti e valute diverse senza inventare o convertire valori.
- Separare l'importo ricevuto dal pagamento dall'importo finale contabilizzato, quando differiscono.
- Preferire un identificativo stabile della transazione per la deduplicazione, se disponibile. Non eliminare automaticamente pagamenti solo perché coincidono importo e negoziante.
- Mantenere una provenienza visibile per correggere un movimento e riconciliarlo con un successivo import bancario.

Il prossimo passo concreto è verificare il payload della propria carta tramite l'automazione locale. Il risultato stabilirà se costruire prima un editor precompilato oppure un'azione capace di registrare bozze senza aprire l'app.

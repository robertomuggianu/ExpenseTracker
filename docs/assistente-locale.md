# Assistente locale · prima fase · versione 1.4

Questa pagina descrive il prototipo 1.4 e la configurazione di rete. Il servizio attuale usa per default `qwen3.5:9b`; per riprodurre il prototipo con il vecchio modello usare `scripts/start-assistant.ps1 -Model qwen2.5:3b`. Per la chat attuale della versione 1.5.2 in inglese, i dati inviati e le prove aggiornate vedere [Chat sui due mesi](chat-due-mesi.md).

L'app prepara un riepilogo dal database SQLite dell'iPhone, lo invia al servizio ASP.NET Core sul PC e riceve un testo prodotto da Ollama con `qwen2.5:3b`. Il modello è quello già provato da Roberto. Questa versione offre **Riassumi questo mese** e **Confronta con il mese precedente**. Le domande libere con tool calling saranno una fase successiva.

## Avvio sul PC Windows

1. Avvia Ollama e verifica il modello:

   ```powershell
   ollama list
   ```

   Se manca:

   ```powershell
   ollama pull qwen2.5:3b
   ```

2. Apri PowerShell nella cartella del progetto e avvia:

   ```powershell
   cd C:\Users\Roberto\Desktop\projects\ExpenseTracker
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-assistant.ps1 -Model qwen2.5:3b
   ```

   `ExecutionPolicy Bypass` riguarda soltanto il processo avviato, non cambia la policy di Windows. Serve .NET SDK 10. Lo script genera una chiave casuale, mostra gli indirizzi IP privati disponibili e avvia il servizio sulla porta **5188**. Usa l'indirizzo dell'interfaccia Wi-Fi o Ethernet collegata alla rete dell'iPhone, evitando VPN/interfacce virtuali. Lascia aperto il terminale; `Ctrl+C` arresta il servizio.

   La chiave è temporanea: con il comando sopra, ogni nuovo avvio genera una nuova chiave da reinserire nell'app. Per conservarla tra riavvii, impostare esplicitamente `Assistant__AccessKey` nel terminale prima dell'avvio. Deve contenere almeno 32 caratteri alfanumerici casuali. Non inserirla nel repository.

3. Se Windows chiede l'accesso alla rete per il servizio .NET, consenti la **rete privata**. Se il firewall blocca ancora il collegamento, in PowerShell come amministratore è possibile aggiungere questa regola limitata alla sottorete locale:

   ```powershell
   New-NetFirewallRule -DisplayName 'ExpenseTracker Assistente locale' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5188 -Profile Private -RemoteAddress LocalSubnet
   ```

   Per rimuoverla:

   ```powershell
   Remove-NetFirewallRule -DisplayName 'ExpenseTracker Assistente locale'
   ```

   Lo script non cambia automaticamente il firewall. Non aprire porte sul router. Ollama resta in ascolto soltanto sul PC: non serve impostare `OLLAMA_HOST=0.0.0.0`.

## Configurazione sull'iPhone

1. Compila e installa **1.4, build 5**, sopra l'app esistente con la procedura di sideload abituale, conservando i dati. Il database rimane allo schema 5.
2. Collega PC e iPhone alla stessa rete Wi-Fi di casa; disattiva eventuali reti ospiti con isolamento tra dispositivi.
3. Apri **menu → Assistente AI** oppure **Altro → Assistente AI**.
4. Inserisci l'indirizzo mostrato sul PC, ad esempio `http://192.168.1.20:5188`. `localhost` sull'iPhone indica il telefono, quindi non usarlo per raggiungere il PC.
5. Inserisci la chiave mostrata nel terminale e tocca **Salva e verifica connessione**. Alla richiesta di iOS, consenti l'accesso alla **rete locale**. Se lo hai negato, riabilitalo nelle impostazioni dell'iPhone per ExpenseTracker.
6. Scegli la valuta e tocca una delle due richieste. La prima risposta può richiedere più tempo perché Ollama carica il modello. Puoi annullare la richiesta oppure lasciare la pagina per interromperla.

Indirizzo e chiave vengono conservati sul dispositivo; la chiave usa MAUI SecureStorage. Il testo della conversazione resta in memoria durante la sessione, con un massimo di 20 messaggi, e può essere svuotato. I resoconti sono indipendenti: la cronologia visibile non viene reinviata al modello.

## Dati e confronto

- Il periodo corrente parte dal primo giorno del mese e comprende oggi. I movimenti nei giorni futuri sono esclusi.
- Il confronto usa i giorni dal primo al giorno corrispondente del mese precedente, limitandosi alla fine del mese quando necessario: 1–31 marzo contro 1–28 febbraio, per esempio. Le date sono sempre visibili.
- Una sola valuta per richiesta, senza conversioni. Tutti i conti, anche archiviati, possono contribuire allo storico nella valuta selezionata.
- I trasferimenti sono esclusi. Entrate, spese, differenza entrate/spese e variazione percentuale vengono calcolati in C#.
- Con spese precedenti pari a zero la percentuale resta non calcolabile; non si invia una percentuale inventata.
- Si inviano date, valuta, numero di movimenti, totali e nomi delle prime dieci categorie per importo, con le restanti aggregate in “Altre categorie”. Non si inviano movimenti individuali, descrizioni, note, nomi dei conti o saldi.
- Nessun movimento registrato non dimostra che non ci siano state spese reali. Se entrambi i periodi sono vuoti, l'app lo comunica senza richiedere una generazione.
- Sotto ogni risposta sono mostrati i fatti calcolati localmente, il modello e l'orario. Il testo del modello può comunque contenere errori: confrontarlo con i dati visualizzati.

## Connessione e limiti del prototipo

Il servizio richiede la chiave anche per `/health`, limita le richieste a 32 KB e permette una generazione alla volta. Non conserva resoconti o database e non registra chiavi o contenuti delle richieste. Traduce i riepiloghi in fatti scritti in italiano: al modello fornisce le prime tre categorie per il resoconto e soltanto i totali/variazioni per il confronto. Le risposte vuote, malformate, troncate o con valori numerici assenti dai fatti forniti vengono rifiutate. Il controllo numerico non verifica il significato delle frasi: non garantisce l'assenza di errori. Timeout: 8 secondi per controllare Ollama, 120 per generare; l'app attende fino a 150 secondi e permette l'annullamento.

Il collegamento del prototipo è **HTTP sulla rete privata**, quindi dati e chiave viaggiano senza cifratura: usarlo sulla rete di casa fidata. Il client accetta HTTP soltanto verso IP privati, loopback o nomi `.local`, disabilita i redirect e conserva la verifica standard dei certificati per HTTPS. Per accesso fuori casa serviranno trasporto cifrato e un collegamento dedicato. Le eccezioni ATS iOS/MacCatalyst coprono solo i nomi locali e gli intervalli IP privati/loopback; Android permette HTTP per questo prototipo e il client applica il controllo degli indirizzi locali.

La prima fase non esegue strumenti scelti dal modello e non modifica i movimenti. Budget, ricorrenze, domande libere e analisi annuali non sono ancora parte dell'assistente.

Configurazione del servizio tramite variabili d'ambiente:

| Variabile | Uso |
|---|---|
| `Assistant__AccessKey` | Chiave obbligatoria, almeno 32 caratteri alfanumerici |
| `Assistant__Model` | Predefinito `qwen2.5:3b` |
| `Assistant__OllamaAddress` | Predefinito `http://127.0.0.1:11434/`, includere lo slash finale |

Avvio manuale dopo aver impostato la chiave:

```powershell
dotnet run --project src/ExpenseTracker.AssistantServer --no-launch-profile -- --urls http://0.0.0.0:5188
```

## Verifiche

```powershell
dotnet test tests/ExpenseTracker.Application.Tests/ExpenseTracker.Application.Tests.csproj
dotnet build src/ExpenseTracker/ExpenseTracker.csproj -f net10.0-windows10.0.19041.0 -p:MauiXamlInflator=SourceGen
dotnet build src/ExpenseTracker.AssistantServer/ExpenseTracker.AssistantServer.csproj
python tests/assistant_server_smoke.py
```

I test verificano i periodi, fine mese/anno bisestile, valuta, esclusione trasferimenti e date future, assenza dei dettagli privati nel riepilogo, baseline zero, contratti HTTP, errori, annullamento e protezione dai doppi invii. Lo smoke test avvia il servizio reale contro un Ollama simulato e verifica autenticazione, disponibilità modello, richieste valide/invalide, limite dimensioni, risposte troncate e richieste concorrenti. Non richiede un modello scaricato; richiede Python per questo solo controllo aggiuntivo.

La build Windows verifica C# e XAML. La prova di rete, permessi e layout sul vero iPhone e la qualità del modello con dati reali vanno effettuate sul dispositivo. Per un controllo facoltativo con il modello realmente installato sul PC, usando soltanto dati inventati:

```powershell
python tests/assistant_live_smoke.py
```

Questa prova controlla resoconto, confronto e baseline zero. La verifica della correttezza del testo generato resta manuale.

Verifica del 09/10/2026: 78 test xUnit superati, compilazione del servizio e dell'app Windows con generazione XAML senza errori, smoke HTTP superato. Tre richieste con Qwen2.5 realmente installato sono state eseguite con dati inventati e il testo è stato verificato manualmente: riepilogo 1000/360/640 EUR, confronto +60 EUR/+20%, confronto con baseline zero. La prima versione del messaggio, con fatti in JSON, aveva prodotto affermazioni errate: è stata sostituita dai fatti in italiano. NuGet ha segnalato `NU1900` perché il controllo remoto delle vulnerabilità non era raggiungibile durante il restore; non è un errore di compilazione.

Riferimenti: [API chat Ollama](https://docs.ollama.com/api/chat), [rete locale MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/local-web-services?view=net-maui-10.0), [eccezioni ATS per intervalli IP Apple](https://developer.apple.com/documentation/BundleResources/Information-Property-List/NSAppTransportSecurity/NSExceptionDomains).

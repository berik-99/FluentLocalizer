# Verifica del traduttore nella WebApp

## Dependency injection

La localizzazione del sito passa già da `SiteText(ITranslator translator)`. Ora `Program.cs` registra il traduttore usando il pacchetto `FluentLocalizer.Extensions.DependencyInjection`, tramite `AddFluentLocalizer().WithStore(...)`, anziché registrare direttamente `Translator`. La factory dello store embedded viene risolta dal container. `SiteText` conserva cultura, formattazione dei testi del sito e rendering sicuro dei contenuti rich text.

Il Playground deve usare uno store modificabile e sollevare gli errori di formattazione, mentre il sito usa cataloghi embedded e le policy predefinite. `AddFluentLocalizer` registra un solo `ITranslator` singleton per container: una seconda chiamata sul container principale farebbe risolvere al sito l'ultima registrazione di traduttore, store e opzioni.

`PlaygroundSession`, registrata come transient e risolta una sola volta nello scope di `OwningComponentBase`, registra quindi i propri servizi tramite lo stesso pacchetto in un container dedicato. Espone `ITranslator` e il proprio `PlaygroundStore`; la rimozione del componente dispone lo scope e il container dedicato. Ogni nuova istanza della pagina riceve template e opzioni indipendenti, senza lasciare servizi disposable transient nel container principale per tutta la durata della WebApp. Il test verifica due sessioni diverse insieme al traduttore principale e le policy di errore.

## Indexer: decisione da confermare

L'indexer attuale appartiene a `SiteText`, non alla libreria:

```csharp
public string this[string key] => translator.Get(key).WithCulture(Culture).Resolve();
```

Restituisce subito una stringa, imposta esplicitamente la cultura selezionata sul sito e ha un overload per `LetterCase`. Questi comportamenti non possono essere trasferiti integralmente a un traduttore generico senza prendere ulteriori decisioni sulla sua API.

| Scelta | Utilità | Implicazioni |
| --- | --- | --- |
| Mantenere l'indexer in `SiteText` | Sintassi breve nelle pagine Razor; cultura del sito sempre esplicita | Nessuna modifica al contratto pubblico. È la proposta per questa modifica. |
| Aggiungere `TranslationBuilder this[string key]` | Alias di `Get(key)`; permette `translator["Welcome"].WithCulture("it").WithArg("name", "Ada").Resolve()` | Conserva cultura, argomenti, casing e risoluzione async nella pipeline. Restituirebbe un builder mutabile nuovo a ogni accesso. |
| Aggiungere `string this[string key]` | Risultato immediato per traduzioni semplici | Lookup e formattazione sincroni a ogni accesso; nessuna possibilità di configurare quel risultato con la pipeline. Gli store HTTP devono essere già caricati. |

Un indexer su `Translator` soltanto non sarebbe disponibile dove iniettiamo `ITranslator`. Aggiungerlo anche all'interfaccia richiederebbe di aggiornare tutte le implementazioni esterne, inclusi mock e fake. Non possiamo contare su un'implementazione predefinita dell'interfaccia per mantenere la compatibilità con tutti i runtime supportati: i pacchetti includono `netstandard2.0` e vengono testati anche su .NET Framework. Dopo la `1.0.0` sarebbe una modifica incompatibile da gestire nel versionamento, non un semplice miglioramento della WebApp.

Anche l'alias che restituisce un builder manterrebbe le conversioni implicite e `ToString()` già presenti: il rendering di un builder come testo può quindi attivare una risoluzione sincrona. L'uso esplicito di `ResolveAsync()` resta necessario quando si vuole caricare uno store HTTP in modo asincrono. L'alias non sostituirebbe `SiteText`, che continua ad applicare la cultura selezionata e le funzioni specifiche del sito.

Proposta: mantenere l'indexer locale per ora. Se desideriamo una sintassi pubblica alternativa a `Get`, preferire il ritorno `TranslationBuilder`, con una decisione separata sulla compatibilità dell'interfaccia e sulla release. Nessun indexer è stato aggiunto alla libreria; serve la conferma dell'utente prima di implementarlo.

## Storico API verificato

| Release | Data NuGet | Riscontro |
| --- | --- | --- |
| `0.0.1` | 30 luglio 2026 | I tre pacchetti pubblicati indicano il commit `2c4a8561c3a27456f23304b24e73bac35e1f0066`. Le DLL espongono 17 tipi pubblici. |
| `0.1.0-alpha` | 30 settembre 2026 | I pacchetti indicano il commit `02ba523883111ab7d19a2f594f1a479ee06400a1`, corrispondente al tag. I tipi pubblici diventano 25. |
| `1.0.0` | 2 ottobre 2026 | Rispetto al tag alpha, nei sorgenti delle librerie cambiano solo le tre proprietà di versione dei progetti. |

Le API fluenti del core, i contratti store/traduttore, logging, eccezioni, enum e le estensioni DI erano già nella `0.0.1`. Nell'alpha vengono introdotti `JsonStoreSettings`, `JsonTranslationStoreBase`, `JsonFileStore`, `EmbeddedJsonStore`, `HttpJsonStore` e le tre opzioni dedicate.

La reference registra anche i cambiamenti incompatibili o di comportamento dell'alpha: namespace `FluentLocalizer.Core` → `FluentLocalizer` e `FluentLocalizer.Core.Logging` → `FluentLocalizer.Logging`; accessor `init` → `set` in `TranslationOptions`; correzione di entrambe le registrazioni DI; conversione di `JsonStore` in wrapper obsoleto; `JsonStoreOptions` sealed con base `JsonStoreSettings`, rimozione di `ThrowOnError` e utilizzo di `ThrowOnMissingStore` ereditato.

La selezione `0.0.1` mostra namespace, firme, ereditarietà e comportamento originali. Le reference dei README sono rigenerate dallo stesso catalogo. I link ai sorgenti puntano alla revisione della release selezionata. Lo snapshot indipendente in `Tests/ApiSnapshots/0.0.1.json` deriva dalle DLL pubblicate; non viene generato dal catalogo che verifica.

Fonti primarie: metadati NuGet dei tre pacchetti (`registration5-gz-semver2`), pacchetti `.nupkg` originali e relativi `.nuspec`, sorgenti Git ai commit verificati e confronto dei tag `v0.1.0-alpha` / `v1.0.0`.

- [FluentLocalizer 0.0.1](https://www.nuget.org/packages/FluentLocalizer/0.0.1)
- [Store.Json 0.0.1](https://www.nuget.org/packages/FluentLocalizer.Store.Json/0.0.1)
- [DependencyInjection 0.0.1](https://www.nuget.org/packages/FluentLocalizer.Extensions.DependencyInjection/0.0.1)
- [Confronto alpha / stable](https://github.com/berik-99/FluentLocalizer/compare/v0.1.0-alpha...v1.0.0)

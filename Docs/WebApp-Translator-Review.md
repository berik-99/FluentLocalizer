# Verifica del traduttore nella WebApp

## Dependency injection

`Program.cs` registra il traduttore usando il pacchetto `FluentLocalizer.Extensions.DependencyInjection`, tramite `AddFluentLocalizer().WithStore(...)`. La factory dello store embedded viene risolta dal container. Le pagine e i componenti Razor iniettano direttamente `ITranslator` tramite `_Imports.razor` e compongono le richieste con le API fluent della libreria. Il wrapper `SiteText`, i suoi indexer e il metodo `Format` sono stati rimossi.

Il Playground deve usare uno store modificabile e sollevare gli errori di formattazione, mentre il sito usa cataloghi embedded e le policy predefinite. `AddFluentLocalizer` registra un solo `ITranslator` singleton per container: una seconda chiamata sul container principale farebbe risolvere al sito l'ultima registrazione di traduttore, store e opzioni.

`PlaygroundSession`, registrata come transient e risolta una sola volta nello scope di `OwningComponentBase`, registra quindi i propri servizi tramite lo stesso pacchetto in un container dedicato. Espone `ITranslator` e il proprio `PlaygroundStore`; la rimozione del componente dispone lo scope e il container dedicato. Ogni nuova istanza della pagina riceve template e opzioni indipendenti, senza lasciare servizi disposable transient nel container principale per tutta la durata della WebApp. Il test verifica due sessioni diverse insieme al traduttore principale e le policy di errore.

## Uso diretto delle API fluent

Le traduzioni sono risolte nel punto di utilizzo:

```razor
@Translator.Get("Ui:Overview")
@Translator.Get("Ui:Why").WithCase(LetterCase.Upper)
@Translator.Get("Ui:RemoveArgument").WithArg("name", argument.Name)
@Translator.Get("Demo:Items").Pluralize(3)
```

`BrowserCulture` seleziona soltanto una cultura supportata dalle preferenze del browser. Avvio e cambio lingua impostano `DefaultThreadCurrentCulture` e `DefaultThreadCurrentUICulture`; `Get()` cattura `CurrentUICulture` alla creazione di ogni richiesta. Il layout conserva la cultura selezionata e la passa al corpo della pagina con `CascadingValue<CultureInfo>`. Pagine e pulsanti copia ricevono il parametro di cascata, così il cambio lingua aggiorna anche i componenti già visibili. Le richieste dell'interfaccia usano la cultura corrente senza ripetere `WithCulture`. La demo animata e le traduzioni del playground mantengono la cultura esplicita perché possono mostrare una lingua diversa dall'interfaccia.

Razor rende il builder tramite `ToString()`, mentre i parametri di tipo `string` usano la conversione implicita: entrambi risolvono la richiesta. Non serve quindi ripetere `Resolve()` nel markup. Le etichette inviate a JavaScript restano risolte esplicitamente perché la serializzazione JSON di un builder non usa `ToString()`. Le richieste vengono ricreate durante il rendering, senza conservare builder costruiti prima del cambio lingua.

`RichText` è un componente di presentazione che riceve una stringa già risolta: non conosce chiavi, culture o traduttori. Mantiene il rendering dei segmenti `code`, enfasi e link con `AddContent`, senza trattare il resto del testo come HTML. Anche le etichette inviate a JavaScript vengono risolte direttamente tramite `ITranslator`.

Le API attuali del core coprono queste esigenze senza modifiche al contratto pubblico. `Resolve()` è appropriato per i cataloghi embedded del sito; gli esempi con store HTTP continuano a usare `ResolveAsync()`.

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

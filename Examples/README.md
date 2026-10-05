# Runnable examples

## API reference

For all public types, constructors, method overloads, options and enum values, see the [complete API reference](../README.md#api-reference) or the searchable [WebUI reference](https://berik-99.github.io/FluentLocalizer/docs/api). The examples below demonstrate how these APIs work together.

| Project | Store | Basic use | Advanced use |
|---|---|---|---|
| ConsoleApp | Custom `ITranslationStore` (`MemoryStore`) | Current culture and synchronous `Resolve()` | Async resolution, arguments, plural and gender rules, default arguments, culture fallback, missing keys, casing, formatting errors |
| WorkerApp | `JsonFileStore` | Dependency injection and a nested key from a local JSON file | Culture fallback, namespace bundle, plural and gender formatting, live reload via `FileSystemWatcher` |
| BlazorWebApp | `HttpJsonStore` | Startup `LoadAsync()` followed by synchronous rendering | Manifest-based preload, lazy namespace loading with `ResolveAsync()`, culture fallback, short/regional locale selection, plural updates, explicit refresh |

## Culture selection

`WithCulture(...)` is optional: `translator.Get(key)` captures `CultureInfo.CurrentUICulture` when the builder is created and uses it for template lookup and MessageFormat formatting, including `ResolveAsync()`. Use `WithCulture(...)` only to override that culture for one translation; it does not change the application's ambient culture.

To change the culture used by new requests and other .NET components in the same execution context:

```csharp
using System.Globalization;

var culture = CultureInfo.GetCultureInfo("it-IT");
CultureInfo.CurrentUICulture = culture; // Translation and resource lookup.
CultureInfo.CurrentCulture = culture;   // Standard .NET number/date formatting.

var message = translator.Get("Welcome").WithArg("name", "Ada").Resolve();
var english = translator.Get("Welcome").WithCulture("en-US").WithArg("name", "Ada").Resolve();
```

Existing builders retain their captured culture; create a new builder after changing it. Components that use explicit cultures or cache rendered text need their own update or rerender. On modern .NET, the ambient culture flows through task-based asynchronous operations.

For application-wide thread defaults, set `CultureInfo.DefaultThreadCurrentUICulture` and `CultureInfo.DefaultThreadCurrentCulture` at startup; these do not override explicitly set thread cultures. In ASP.NET Core, configure localization per request/user rather than changing those defaults for each user.

See Microsoft's [CurrentUICulture](https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo.currentuiculture) and [CurrentCulture](https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo.currentculture) documentation.

Run the console and worker with:

```bash
dotnet run --project Examples/FluentLocalizer.Samples.ConsoleApp
dotnet run --project Examples/FluentLocalizer.Samples.WorkerApp
```

Run the Blazor server project with:

```bash
dotnet run --project Examples/FluentLocalizer.Samples.BlazorWebApp/FluentLocalizer.Samples.BlazorWebApp
```

In the worker, edit `Locales/it-IT.common.json` in the build output while it runs to see live reload. In Blazor, `wwwroot/locales/manifest.json` lists the catalogs and namespace files preloaded at startup; click **Load namespace asynchronously** to fetch a separate `*.common.json` file. The refresh button requests the loaded files again from the server. The locale-resolution buttons show manifest selection in both directions: requesting `en-US` finds `en.json`, while requesting `en` finds `en-US.json`. Each case uses a separate manifest so only the intended catalog is available.

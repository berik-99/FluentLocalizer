# Runnable examples

## API reference

For all public types, constructors, method overloads, options and enum values, see the [complete API reference](../README.md#api-reference) or the searchable WebUI reference for [Core](https://berik-99.github.io/FluentLocalizer/docs/core/api) and the [JSON store](https://berik-99.github.io/FluentLocalizer/docs/store-json/api). The examples below demonstrate how these APIs work together.

| Project | Store | Basic use | Advanced use |
|---|---|---|---|
| ConsoleApp | Custom `ITranslationStore` (`MemoryStore`) | Current culture and synchronous `Resolve()` | Async resolution, arguments, plural and gender rules, default arguments, culture fallback, missing keys, casing, formatting errors |
| WorkerApp | `JsonFileStore` | Dependency injection and a nested key from a local JSON file | Culture fallback, namespace bundle, plural and gender formatting, live reload via `FileSystemWatcher` |
| BlazorWebApp | `HttpJsonStore` | Startup `LoadAsync()` followed by synchronous rendering | Manifest-based preload, lazy namespace loading with `ResolveAsync()`, culture fallback, short/regional locale selection, plural updates, explicit refresh |

## Culture selection

**You normally don't call `WithCulture(...)`.** `translator.Get(key)` uses `CultureInfo.CurrentUICulture`, the culture of your user interface, both to look up the template and to format it, with `Resolve()` and `ResolveAsync()` alike. Set the UI culture where your application decides the user's language, and every request follows it:

```csharp
using System.Globalization;

var culture = CultureInfo.GetCultureInfo("it-IT");
CultureInfo.CurrentUICulture = culture; // Translation lookup and message formatting.
CultureInfo.CurrentCulture = culture;   // Standard .NET number/date formatting elsewhere in the app.

var message = translator.Get("Welcome").WithArg("name", "Ada").Resolve(); // Italian
```

`WithCulture(...)` exists only to **force** a culture that differs from the UI culture for one message, such as an email written in the recipient's language or a page that shows the same text in several languages:

```csharp
var subject = translator.Get("Email:Subject").WithCulture(recipientCulture).Resolve();
```

It affects that request only and never changes `CurrentUICulture` or `CurrentCulture`. If you pass the same culture to every request, set `CurrentUICulture` instead: repeating `WithCulture(...)` hides the real culture of the application and ignores later language changes.

`Get(key)` captures the culture when it creates the builder, so create a new builder after changing it. Components that cache rendered text need their own update or rerender. On modern .NET, the ambient culture flows through task-based asynchronous operations.

For application-wide thread defaults, set `CultureInfo.DefaultThreadCurrentUICulture` and `CultureInfo.DefaultThreadCurrentCulture` at startup; these do not override explicitly set thread cultures. In ASP.NET Core, `app.UseRequestLocalization(...)` sets both cultures for each request, so `Get(key)` already follows the user's language; don't change the process-wide defaults per user.

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

None of the samples repeats `WithCulture(...)` on its requests. The console and worker set the UI culture to `it-IT` at startup; the Blazor language selector sets the UI culture and rerenders, so every new request follows it. The only explicit culture is the console's forced German request, which demonstrates the fallback to English. For how the worker's `it-IT.json` and `it-IT.common.json` files relate, see [Organizing JSON files](../Sources/FluentLocalizer.Store.Json/Assets/README.md#organizing-json-files).

# FluentLocalizer documentation

[README](README.md) · [API reference](API-REFERENCE.md) · [Changelog](CHANGELOG.md)

This guide covers how FluentLocalizer resolves messages, how to configure it and how to use the JSON stores and dependency injection. The [website](https://berik-99.github.io/FluentLocalizer/) explains every feature step by step: the [Core guides](https://berik-99.github.io/FluentLocalizer/docs/core) cover templates, requests, culture, errors, logging and dependency injection, and the [JSON store guides](https://berik-99.github.io/FluentLocalizer/docs/store-json) cover each JSON store. Every public type and member is described in the [API reference](API-REFERENCE.md).

- [ICU message templates](#icu-message-templates)
- [Culture selection](#culture-selection)
- [Translation options](#translation-options)
- [JSON stores](#json-stores)
- [Dependency injection](#dependency-injection)

---

## ICU message templates

ICU is the Unicode Consortium's internationalization standard for formatting dates, numbers, plurals, and messages in a culture-aware way. In practice, it defines a common set of rules that let the same message be rendered correctly for different languages and regions, for example by changing plural forms (`one`, `few`, `many`, `other`) or by adapting number and date formatting to the current locale. The [ICU message formatting guide](https://unicode-org.github.io/icu/userguide/format_parse/messages/) describes the template syntax (placeholders, `plural`, `select` and more); the [MessageFormat documentation](https://github.com/jeffijoe/messageformat.net#readme) lists the ICU features supported in .NET. More about ICU in general: https://unicode-org.github.io/icu/.

```csharp
// Template: "{quantity, plural, one {# item} other {# items}} for {name}"
var message = translator
    .Get("Checkout:Items")
    .Pluralize(3)
    .WithArg("name", "Ada")
    .Resolve(); // 3 items for Ada
```

`Pluralize(n)` sets the `quantity` argument and `Genderize(gender)` sets `gender` to `unspecified`, `male`, `female` or `neuter`, for use in `select` expressions. `WithCase(...)` transforms the formatted text.

---

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

---

## Translation options

Customize how missing keys or invalid format strings are handled:

```csharp
var options = new TranslationOptions
{
    MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
    MissingKeyFallbackValue = "Missing '{key}' for culture '{culture}'.",
    FormattingErrorBehavior = FormattingErrorBehavior.ThrowException,
    FormattingErrorExceptionFactory = (key, culture) => new TranslationException(
        key,
        culture,
        $"Format error for {key}")
};
```

| Property | Default | Behavior |
| --- | --- | --- |
| `MissingKeyBehavior` | `ReturnPlaceholder` | `ReturnPlaceholder` returns `[key]`; `ReturnConfiguredValue` returns `MissingKeyFallbackValue`; `ThrowException` throws. |
| `MissingKeyFallbackValue` | `"[{key}]"` | Used only with `ReturnConfiguredValue`. |
| `MissingKeyExceptionFactory` | `null` | Creates the exception thrown with `ThrowException`; defaults to `TranslationException`. |
| `FormattingErrorBehavior` | `ReturnPlaceholder` | `ReturnPlaceholder` returns `FormattingErrorFallbackValue`; `ThrowException` throws. |
| `FormattingErrorFallbackValue` | `"[Format Error]"` | Returned for parse and formatting failures. |
| `FormattingErrorExceptionFactory` | `null` | Creates the exception thrown with `ThrowException`; a `null` result falls back to the missing-key exception. |
| `DefaultArguments` | `null` | Values merged into every request; request values win, names are case-insensitive. |

Fallback values replace `{key}`/`{0}` with the key and `{culture}`/`{1}` with the culture name, case-insensitively and as plain text (not ICU). The translator keeps a reference to the options object, so configure it before creating the translator. `WithOptions(...)` replaces the whole object for a single request.

### Resolution rules worth knowing

- A missing translation is a `null`, empty or whitespace-only template, or a `KeyNotFoundException` thrown by the store. An empty string in a catalog is therefore *missing* and does not fall back to another culture.
- A formatting error is any parse or format failure: unbalanced braces, a placeholder without a value, a non-numeric `plural` value, no matching branch without `other`, an unsupported format style. Missing arguments are never rendered as empty text.
- Any other store exception (I/O, HTTP, `FileNotFoundException`, cancellation) propagates unchanged; it is logged at `Error` first when a logger is configured.
- Every request starts with `quantity = null` and `gender = "unspecified"`; `Pluralize` and `Genderize` overwrite them. Because they are request values, `DefaultArguments` entries for these two names are never used. A `plural` block treats a `null` quantity as `0`.
- If a template contains `{Length}` and no `Length` argument is supplied, it receives the count of non-null arguments, including the built-in `gender`.
- `#` is replaced only directly inside a `plural` branch; inside a `select` nested in a `plural` it stays literal. Nest `plural` inside `select` instead.
- Literal braces are escaped ICU-style with apostrophes (`'{name}'`), and `''` prints one apostrophe; backslashes are not escapes. MessageFormat supports `number` (`integer`, `percent`, `currency`), `date` (`short`, `full`), `time` (`short`), `plural` with `offset:` and `=N`, `select` and `selectordinal`.
- `WithCase` runs after formatting: `Upper`/`Lower` use invariant casing; `CamelCase`, `PascalCase`, `SnakeCase` and `KebabCase` split words and drop spaces and punctuation, so they suit identifiers rather than sentences.
- With `ThrowException`, the default `TranslationException` carries `Key` and `Culture`; formatting exceptions include the formatter's message but do not set `InnerException`. An empty or whitespace key throws `ArgumentException` instead.
- `Translator` is stateless and thread-safe; `TranslationBuilder` is mutable, not thread-safe, and keeps the culture captured by `Get(key)`. `ToString()` and the implicit `string` conversion call `Resolve()`.

---

## JSON stores

The `FluentLocalizer.Store.Json` package provides three stores that share culture fallback, file mappings, JSON parsing, and nested-key lookup:

- `JsonFileStore` reads files from disk and can reload them when they change.
- `EmbeddedJsonStore` reads JSON resources from an assembly.
- `HttpJsonStore` downloads files asynchronously and caches them in memory.

Install the package:

```bash
dotnet add package FluentLocalizer.Store.Json
```

### Organizing the JSON files

A key is a path through nested objects, with `:` between segments, and each string value is an ICU template. This file defines `Welcome`, `Checkout:Title` and `Checkout:Items`:

```json
{
  "Welcome": "Hello {name}!",
  "Checkout": {
    "Title": "Your cart",
    "Items": "{quantity, plural, one {# item} other {# items}}"
  }
}
```

Always nest objects: keys are split at every `:`, so a property named `"Checkout:Title"` is never found. Property names are case-sensitive, and only string values count as templates.

The first segment of a key with two or more segments is its *namespace*. `common:Buttons:Save` can live in the culture file as a top-level object, `{ "common": { "Buttons": { "Save": "Salva" } } }` in `it-IT.json`, or in a namespace file without the namespace object, `{ "Buttons": { "Save": "Salva" } }` in `it-IT.common.json`. The key is the same in both cases. `JsonFileStore` and `HttpJsonStore` accept `it-IT.common.json`, `common.it-IT.json` and `it-IT/common.json`; `EmbeddedJsonStore` reads only the culture file.

Fallback works per key: the store tries the namespace file, the culture file, the neutral culture (`it`) and then `FallbackCulture`, so regional and namespace files only need the strings that differ. Name each language's main catalog after its neutral culture and add regional files only for differences:

```text
Locales/
  en.json            complete catalog, also the fallback (FallbackCulture = "en")
  en-GB.json         only British differences
  it.json            Italian for it-IT, it-CH, it...
  it.checkout.json   optional namespace file
```

With regional-only names such as `it-IT.json`, `JsonFileStore` and `HttpJsonStore` with a manifest throw `FileNotFoundException` for a sibling region like `it-CH` instead of using it; map it with `FileMappings["it-CH"] = "it-IT.json"`. The same selection applies to `FallbackCulture`, so the fallback culture (or its neutral culture) needs a file of its own, and the exception is thrown whatever `ThrowOnMissingStore` says. The [JSON store guide](Sources/FluentLocalizer.Store.Json/Assets/README.md#organizing-json-files) details the lookup order, every supported file name and the differences between stores.

### Files on disk

`JsonFileStore` reads each file the first time a lookup needs it and keeps the parsed document in memory. It reads from the application output directory by default. Add the files explicitly to the consuming project and copy them to both build and publish output:

```xml
<ItemGroup>
  <Content Include="Locales\**\*.json"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

The store indexes the `*.json` files in `ResourcesPath` (default `Locales`) and its subfolders. This is a filesystem store and is not supported in browser applications; use `EmbeddedJsonStore` or `HttpJsonStore` there.

```csharp
using FluentLocalizer;
using FluentLocalizer.Store.Json;

using var store = new JsonFileStore(new JsonFileStoreOptions
{
    ResourcesPath = "Locales",
    FallbackCulture = "en",
    ThrowOnMissingStore = true
});
var translator = new Translator(store);
var greeting = translator.Get("Welcome").WithArg("name", "Ada").Resolve(); // Uses CurrentUICulture.
```

### Embedded resources

For embedded resources, include the locale files as `EmbeddedResource` and create an `EmbeddedJsonStore`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Locales\**\*.json" />
</ItemGroup>
```

The store selects embedded `.json` resources whose manifest names contain the `ResourcesPath` folder (default `Locales`) and identifies each culture by the final file name, such as `it.json`. It reads one file per culture, so keep namespaces as top-level objects, and never embed names such as `common.it-IT.json`: MSBuild moves them to a satellite assembly that the store does not read. Always set `ResourceAssembly`; the default is the entry assembly, which in tests or plugins is not the one containing the files. `EmbeddedJsonStore` holds no resources and is not `IDisposable`.

```csharp
var store = new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
{
    ResourceAssembly = typeof(Program).Assembly,
    ResourcesPath = "Locales"
});
```

### HTTP

For HTTP, publish the locale files as static files on the server (for example under `wwwroot/locales` in an ASP.NET Core or Blazor WebAssembly app), then configure an `HttpClient` with the base address that serves them. `ResourcesPath` (default `locales`, lowercase) is the URL path below that base address; end `BaseAddress` with `/`, otherwise its last segment is dropped when relative URLs are resolved. Call `LoadAsync` before synchronous `Resolve()`, or use `ResolveAsync()` for lazy loading. An optional `manifest.json` lists all culture and namespace files for complete preload and short-to-regional culture selection. `RefreshStoreAsync()` revalidates loaded files with HTTP cache validators when available.

```csharp
using FluentLocalizer.Store.Json;

var client = new HttpClient { BaseAddress = new Uri("https://example.com/") };
using var store = new HttpJsonStore(client, new HttpJsonStoreOptions { ResourcesPath = "locales" });
await store.LoadAsync(["it-IT", "en-US"]);
```

The Blazor WebAssembly sample in `Examples/FluentLocalizer.Samples.BlazorWebApp` uses `+` and `−` buttons to change the unread message count. The pluralized message rerenders immediately, and the language selector switches between the preloaded English and Italian files.

### Shared settings and migration

All options support `FallbackCulture`, `FileMappings`, `ThrowOnMissingStore`, and the optional `MaxDocumentBytes` limit. Catalogs must be strict JSON (no comments or trailing commas); without `ThrowOnMissingStore`, the file and embedded stores skip an invalid file silently. A document larger than `MaxDocumentBytes` always throws. See the [JSON store package guide](Sources/FluentLocalizer.Store.Json/Assets/README.md) for full examples and behavior.

When upgrading from the separate HTTP package, replace `FluentLocalizer.Store.Http` with `FluentLocalizer.Store.Json` and update the namespace to `FluentLocalizer.Store.Json`. The mode-based `JsonStore` and `JsonStoreOptions` API is deprecated; use a store-specific type above.

---

## Dependency injection

Integration with `IServiceCollection` for ASP.NET Core, Worker Services, or Console apps.

```bash
dotnet add package FluentLocalizer.Extensions.DependencyInjection
```

`AddFluentLocalizer(...)` registers `ITranslator` as a singleton and configures `TranslationOptions`; `WithStore(...)` registers the store and `WithLogger()` forwards translation diagnostics to `ILogger<Translator>`.

```csharp
using FluentLocalizer;
using FluentLocalizer.Store.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFluentLocalizer(options =>
{
    options.MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue;
    options.MissingKeyFallbackValue = "[{key}]";
})
.WithStore(_ => new JsonFileStore(new JsonFileStoreOptions
{
    ResourcesPath = "Locales",
    FallbackCulture = "en-US",
    ThrowOnMissingStore = true
}))
.WithLogger();

builder.Services.AddHostedService<NotificationWorker>();

await builder.Build().RunAsync();

public sealed class NotificationWorker(ITranslator translator) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var message = translator
            .Get("Notifications:MessageCount")
            .Pluralize(3)
            .Resolve();

        Console.WriteLine(message); // In CurrentUICulture, for example: You have 3 messages.
        return Task.CompletedTask;
    }
}
```

With a factory, the container creates the store on first use and disposes it with the service provider. `WithStore(store)` also accepts an existing instance; the application then owns it and disposes it itself. See the [dependency injection package guide](Sources/FluentLocalizer.Extensions.DependencyInjection/Assets/README.md) for what gets registered, store factories, options from DI and logging.

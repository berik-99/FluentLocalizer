# FluentLocalizer

[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer)
[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.Store.Json.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer.Store.Json)
[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.Extensions.DependencyInjection.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer.Extensions.DependencyInjection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

**FluentLocalizer** is a lightweight, high-performance .NET library designed for resolving culture-aware translation templates written in the **[ICU message syntax](https://unicode-org.github.io/icu/userguide/format_parse/messages/)** with clean **Fluent APIs**. Templates are parsed and formatted by [**MessageFormat**](https://github.com/jeffijoe/messageformat.net), the open-source .NET library by Jeff Hansen ([@jeffijoe](https://github.com/jeffijoe)): FluentLocalizer would not exist without it.

Explore the [website and browser playground](https://berik-99.github.io/FluentLocalizer/). To run the site locally, use `dotnet run --project Sources/FluentLocalizer.WebApp/FluentLocalizer.WebApp.csproj`. The GitHub Pages workflow publishes the static WebAssembly output from `main`.

Browse the [complete API reference](#api-reference) below or use the searchable [WebUI API reference](https://berik-99.github.io/FluentLocalizer/docs/api).

The WebApp uses FluentLocalizer with `EmbeddedJsonStore` and embedded catalogs in `Sources/FluentLocalizer.WebApp/Locales`. It selects the first supported language from the browser's preferences (English, Italian, French, German, or Spanish), preserves the regional culture for formatting, and falls back to English for unsupported languages or missing translations. The header's language dropdown updates the site without reloading or clearing playground edits and remembers the choice for subsequent visits.

ICU is the Unicode Consortium's internationalization standard for formatting dates, numbers, plurals, and messages in a culture-aware way. In practice, it defines a common set of rules that let the same message be rendered correctly for different languages and regions, for example by changing plural forms (`one`, `few`, `many`, `other`) or by adapting number and date formatting to the current locale. The [ICU message formatting guide](https://unicode-org.github.io/icu/userguide/format_parse/messages/) describes the template syntax (placeholders, `plural`, `select` and more); the [MessageFormat documentation](https://github.com/jeffijoe/messageformat.net#readme) lists the ICU features supported in .NET. More about ICU in general: https://unicode-org.github.io/icu/.

Stop dealing with cumbersome resource files (`.resx`) or rigid formatting string builders. FluentLocalizer makes multi-language management, pluralization, gender-aware translations, and missing-key fallback seamless, extensible, and developer-friendly.

---

## ✨ Features at a Glance

- **Fluent Translation API:** Expressive, readable, and chainable calls for building translation requests.
- **ICU Message Syntax:** Out-of-the-box support for complex placeholders, pluralization, gender formatting, and casing transformations, powered by [MessageFormat](https://github.com/jeffijoe/messageformat.net).
- **Culture-Aware & Fallbacks:** Robust culture resolution with configurable fallback chains (e.g., `it-IT` → `it` → `en-US`).
- **Resilient Error Handling:** Fine-grained options for handling missing translation keys and formatting errors.
- **Extensible Storage (`ITranslationStore`):** Pluggable backend architecture. Use JSON, in-memory stores, or build your own custom provider.
- **First-Class DI Support:** Built-in extension package for native `Microsoft.Extensions.DependencyInjection` integration.

---

## 📦 Packages & Ecosystem

| Package | Description | NuGet |
| :--- | :--- | :--- |
| **`FluentLocalizer`** | Core engine, `Translator`, and `ITranslationStore` abstractions. | [![NuGet](https://img.shields.io/nuget/v/FluentLocalizer.svg?style=flat-square)](https://www.nuget.org/packages/FluentLocalizer) |
| **`FluentLocalizer.Store.Json`** | Official JSON-backed translation store implementation. | [![NuGet](https://img.shields.io/nuget/v/FluentLocalizer.Store.Json.svg?style=flat-square)](https://www.nuget.org/packages/FluentLocalizer.Store.Json) |
| **`FluentLocalizer.Extensions.DependencyInjection`** | Dependency Injection extensions for `IServiceCollection`. | [![NuGet](https://img.shields.io/nuget/v/FluentLocalizer.Extensions.DependencyInjection.svg?style=flat-square)](https://www.nuget.org/packages/FluentLocalizer.Extensions.DependencyInjection) |

---

## 🚀 Quick Start (Core)

Install the core package:

```bash
dotnet add package FluentLocalizer

```

Create a quick in-memory store and resolve your first message:

```csharp
using FluentLocalizer;
using System.Globalization;

public sealed class InMemoryStore : ITranslationStore
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome"] = "Hello {name}!"
    };

    public string? GetTemplate(string key, CultureInfo culture) =>
        _templates.TryGetValue(key, out var value) ? value : null;

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetTemplate(key, culture));
}

var store = new InMemoryStore();
var translator = new Translator(store, new TranslationOptions
{
    DefaultArguments = new Dictionary<string, object?>
    {
        ["name"] = "Ada"
    }
});

var message = translator
    .Get("welcome")
    .Resolve();

Console.WriteLine(message); // Output: Hello Ada!

```

---

## Culture selection

`WithCulture(...)` is optional: `translator.Get(key)` captures `CultureInfo.CurrentUICulture` when the builder is created and uses it for template lookup and message formatting, including `ResolveAsync()`. Use `WithCulture(...)` only to override that culture for one translation; it does not change the application's ambient culture.

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

## ⚙️ Translation Options

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

**Key configurations:**

* `MissingKeyBehavior`: Choose whether to return a placeholder, a fallback string, or throw an exception.
* `MissingKeyFallbackValue`: Custom text when a key isn't found.
* `FormattingErrorBehavior`: Control whether formatting failures trigger an exception or fall back gracefully.
* `DefaultArguments`: Fallback values for placeholders not explicitly supplied in `.WithArg()`.

---

## 📄 JSON Stores

The `FluentLocalizer.Store.Json` package provides three stores that share culture fallback, file mappings, JSON parsing, and nested-key lookup:

- `JsonFileStore` reads files from disk and can reload them when they change.
- `EmbeddedJsonStore` reads JSON resources from an assembly.
- `HttpJsonStore` downloads files asynchronously and caches them in memory.

Install the package:

```bash
dotnet add package FluentLocalizer.Store.Json
```

Use one file per culture under `Locales`. `JsonFileStore` reads files on demand and keeps a bounded cache of recently used templates. Namespace files can be flat or grouped by culture:

```text
Locales/
    en-US.json
    it-IT.json
    it-IT.common.json
    it/common.json
```

`JsonFileStore` recognizes a namespaced key such as `common:Home:Welcome`: it checks the culture's `common` file before the combined culture file, then tries the neutral culture (`it`) and configured fallback. In a namespace file, omit the namespace object and keep only the nested section, for example `{ "Home": { "Welcome": "Benvenuto" } }`. The same key continues to work in `it-IT.json` when that file contains a `common` object.

`JsonFileStore` reads from the application output directory by default. Add the files explicitly to the consuming project and copy them to both build and publish output:

```xml
<ItemGroup>
  <Content Include="Locales\**\*.json"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

The store scans `ResourcesPath` (default `Locales`) recursively for `*.json` files. This is a filesystem store and is not supported in browser applications; use `EmbeddedJsonStore` or `HttpJsonStore` there.

```csharp
using FluentLocalizer;
using FluentLocalizer.Store.Json;

using var store = new JsonFileStore(new JsonFileStoreOptions
{
    ResourcesPath = "Locales",
    FallbackCulture = "en-US",
    ThrowOnMissingStore = true
});
var translator = new Translator(store);
var greeting = translator.Get("Welcome").WithCulture("it-IT").WithArg("name", "Ada").Resolve();
```

For embedded resources, include the locale files as `EmbeddedResource` and create an `EmbeddedJsonStore`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Locales\**\*.json" />
</ItemGroup>
```

The store selects embedded `.json` resources whose manifest names contain the `ResourcesPath` folder (default `Locales`) and converts the final resource name to the culture filename. Set `ResourceAssembly` if the files are embedded in a different assembly.

```csharp
using var store = new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
{
    ResourceAssembly = typeof(Program).Assembly,
    ResourcesPath = "Locales"
});
```

For HTTP, publish the locale files as static files on the server (for example under `wwwroot/locales` in an ASP.NET Core or Blazor WebAssembly app), then configure an `HttpClient` with the base address that serves them. `ResourcesPath` is the URL path below that base address. Call `LoadAsync` before synchronous `Resolve()`, or use `ResolveAsync()` for lazy loading. An optional `manifest.json` lists all culture and namespace files for complete preload and short-to-regional culture selection. `RefreshStoreAsync()` revalidates loaded files with HTTP cache validators when available.

```csharp
using FluentLocalizer.Store.Json;

var client = new HttpClient { BaseAddress = new Uri("https://example.com/") };
using var store = new HttpJsonStore(client, new HttpJsonStoreOptions { ResourcesPath = "locales" });
await store.LoadAsync(["it-IT", "en-US"]);
```

The Blazor WebAssembly sample in `Examples/FluentLocalizer.Samples.BlazorWebApp` uses `+` and `−` buttons to change the unread message count. The pluralized message rerenders immediately, and the language selector switches between the preloaded English and Italian files.

All options support `FallbackCulture`, `FileMappings`, `ThrowOnMissingStore`, and the optional `MaxDocumentBytes` limit. See the [JSON store package guide](Sources/FluentLocalizer.Store.Json/Assets/README.md) for full examples and behavior.

When upgrading from the separate HTTP package, replace `FluentLocalizer.Store.Http` with `FluentLocalizer.Store.Json` and update the namespace to `FluentLocalizer.Store.Json`. The mode-based `JsonStore` and `JsonStoreOptions` API is deprecated; use a store-specific type above.

---
## 🧩 Dependency Injection Integration

Integration with `IServiceCollection` for ASP.NET Core, Worker Services, or Console apps.

```bash
dotnet add package FluentLocalizer.Extensions.DependencyInjection

```

### Registration & Worker Example

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
.WithStore(new JsonFileStore(new JsonFileStoreOptions
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
            .WithCulture("en-US")
            .Pluralize(3)
            .Resolve();

        Console.WriteLine(message); // Output: You have 3 messages.
        return Task.CompletedTask;
    }
}

```

---

## 🛠️ Building & Running Locally

Clone the repository and build using the .NET CLI:

```bash
dotnet restore FluentLocalizer.slnx
dotnet build FluentLocalizer.slnx --configuration Release
dotnet test FluentLocalizer.slnx --configuration Release

```

The library projects target `netstandard2.0`, `net8.0`, and `net10.0`; the samples target `net10.0`. The test projects also target `net472`, so run the full test matrix on Windows. On other platforms, run the `.NET 10` tests with `dotnet test FluentLocalizer.slnx --configuration Release --framework net10.0`.

**Run sample projects:**

See [the examples guide](Examples/README.md) for basic and advanced scenarios in the console, worker, and Blazor apps.

```bash
# Console Sample
dotnet run --project Examples/FluentLocalizer.Samples.ConsoleApp/FluentLocalizer.Samples.ConsoleApp.csproj

# Worker Service Sample
dotnet run --project Examples/FluentLocalizer.Samples.WorkerApp/FluentLocalizer.Samples.WorkerApp.csproj

```

---

## 📂 Repository Layout

```text
├── FluentLocalizer.slnx
├── Assets/                                         # Repository branding assets
├── Sources/
│   ├── FluentLocalizer.Core/                        # Core engine and abstractions
│   ├── FluentLocalizer.Extensions.DependencyInjection/
│   ├── FluentLocalizer.Shared.Polyfill/             # Shared project (.shproj), not a NuGet package
│   └── FluentLocalizer.Store.Json/                  # File, embedded-resource, and HTTP JSON stores
├── Examples/
│   ├── FluentLocalizer.Samples.ConsoleApp/
│   ├── FluentLocalizer.Samples.WorkerApp/
│   └── FluentLocalizer.Samples.BlazorWebApp/
│       ├── FluentLocalizer.Samples.BlazorWebApp/    # ASP.NET Core host
│       └── FluentLocalizer.Samples.BlazorWebApp.Client/ # Blazor WebAssembly client
└── Tests/                                           # Core, JSON store, and DI test projects

```

---

## 🤝 Contributing & Community Support

Contributions make the open-source community an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**!

- 💡 **Have an idea or feature request?** Open an [Issue](https://github.com/berik-99/FluentLocalizer/issues).
- 🐛 **Found a bug?** Submit an [Issue](https://github.com/berik-99/FluentLocalizer/issues) with steps to reproduce it.
- 🔧 **Want to contribute code?** Fork the repo and submit a **Pull Request**. New storage backends (e.g., Redis, Database, YAML) or engine improvements are warmly welcome!

> [!NOTE]
> **🤖 Documentation Notice & AI Disclaimer:**  
> Parts of this documentation were created or refined with the assistance of AI tools. While every effort has been made to ensure accuracy, some details or code samples might contain minor errors or typos. If you spot any inconsistencies, please open an issue or submit a PR — every contribution helps!

---

## 🙏 Acknowledgments

FluentLocalizer is built on [**MessageFormat**](https://github.com/jeffijoe/messageformat.net) by Jeff Hansen ([@jeffijoe](https://github.com/jeffijoe)). Every template that FluentLocalizer resolves, from a simple placeholder to nested `plural` and `select` branches, is parsed and formatted by MessageFormat, an implementation of the [ICU message syntax](https://unicode-org.github.io/icu/userguide/format_parse/messages/) for .NET. FluentLocalizer adds the fluent API, culture handling, stores and fallback policies around it.

Huge thanks to Jeff Hansen and the MessageFormat contributors for providing such a solid foundation for .NET localization! If FluentLocalizer is useful to you, consider giving [MessageFormat a star](https://github.com/jeffijoe/messageformat.net) too.

MessageFormat is distributed under the MIT License. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the full notice.

---

## 📜 License

Distributed under the **MIT License**.

<!-- api-reference:start -->
## API reference

Public types and members declared by FluentLocalizer, grouped by package and namespace. Includes constructors, overloads, properties, enum values, inherited library members and deprecated APIs. Standard inherited .NET members are omitted.

Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. Every type states the version that introduced it; later additions, changes and deprecations are marked on each member. The searchable [WebUI API reference](https://berik-99.github.io/FluentLocalizer/docs/api) uses the same catalog and can show any earlier version.

Documented version: `1.0.0`

- [ITranslator](#api-itranslator)
- [Translator](#api-translator)
- [TranslationBuilder](#api-translationbuilder)
- [TranslationOptions](#api-translationoptions)
- [ITranslationStore](#api-itranslationstore)
- [TranslationException](#api-translationexception)
- [Gender](#api-gender)
- [LetterCase](#api-lettercase)
- [MissingTranslationBehavior](#api-missingtranslationbehavior)
- [FormattingErrorBehavior](#api-formattingerrorbehavior)
- [ITranslationLogger](#api-itranslationlogger)
- [TranslationLogLevel](#api-translationloglevel)
- [JsonStoreSettings](#api-jsonstoresettings)
- [JsonFileStoreOptions](#api-jsonfilestoreoptions)
- [EmbeddedJsonStoreOptions](#api-embeddedjsonstoreoptions)
- [HttpJsonStoreOptions](#api-httpjsonstoreoptions)
- [JsonTranslationStoreBase](#api-jsontranslationstorebase)
- [JsonFileStore](#api-jsonfilestore)
- [EmbeddedJsonStore](#api-embeddedjsonstore)
- [HttpJsonStore](#api-httpjsonstore)
- [JsonStore](#api-jsonstore)
- [JsonStoreOptions](#api-jsonstoreoptions)
- [JsonStoreLocation](#api-jsonstorelocation)
- [FluentLocalizerServiceCollectionExtensions](#api-fluentlocalizerservicecollectionextensions)
- [FluentLocalizerBuilder](#api-fluentlocalizerbuilder)

<a id="api-itranslator"></a>

### ITranslator

Kind: `interface` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Contract for creating translation requests by key.

Implemented by Translator. Each Get call creates a separate mutable builder.

| Member | Parameters, return value and behavior |
| --- | --- |
| `TranslationBuilder Get(string key)` | key is the translation identifier. Returns a new builder; Translator captures CurrentUICulture at this call. A null key throws ArgumentNullException in Translator; empty keys are forwarded to the store. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Interfaces/ITranslator.cs)

<a id="api-translator"></a>

### Translator

Kind: `class` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Inherits: `ITranslator` · Since: `0.0.1`

Default ITranslator implementation, combining a store, translation policies and optional logging.

The translator can be reused after a culture change. Create a new builder for each translation; existing builders retain their captured culture.

| Member | Parameters, return value and behavior |
| --- | --- |
| `Translator(ITranslationStore store, TranslationOptions? options = null, ITranslationLogger? logger = null)` | store supplies templates and must not be null. options defaults to a new TranslationOptions; logger defaults to no logging. Stores the supplied references. Throws ArgumentNullException for a null store. |
| `TranslationBuilder Get(string key)` | Returns a new builder for key using the configured store, options and logger. Captures CurrentUICulture for lookup and ICU message formatting. Throws ArgumentNullException for a null key. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Translator.cs)

<a id="api-translationbuilder"></a>

### TranslationBuilder

Kind: `class` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Mutable fluent request that resolves a template, applies arguments and formats the result.

All With*, Pluralize and Genderize methods return the same builder. Culture is captured at construction; WithCulture is optional and affects this request only. Do not share one mutable builder between concurrent operations.

| Member | Parameters, return value and behavior |
| --- | --- |
| `TranslationBuilder(ITranslationStore store, string key, TranslationOptions? options = null, CultureInfo? culture = null, ITranslationLogger? logger = null)` | store and key identify the template and cannot be null (ArgumentNullException). options defaults to a new TranslationOptions, culture to CurrentUICulture at construction and logger to no logging. |
| `TranslationBuilder WithCulture(CultureInfo culture)` | culture overrides lookup and ICU message formatting for this request. Does not change CurrentUICulture or CurrentCulture. Although the parameter is declared non-nullable, passing null currently selects CurrentCulture, not CurrentUICulture. |
| `TranslationBuilder WithCulture(string cultureName)` | cultureName is a .NET culture name, such as it-IT. Uses CultureInfo.GetCultureInfo. Throws ArgumentNullException for null and CultureNotFoundException for an unrecognized name. |
| `TranslationBuilder WithOptions(TranslationOptions options)` | Replaces this builder's missing-key, formatting and default-argument policies with options. Passing null resets them to a new TranslationOptions despite the non-nullable signature. |
| `TranslationBuilder WithArg(string name, object? value)` | Adds or replaces the named argument. Names are case-insensitive; value may be null. Runtime arguments override configured defaults. A null name throws ArgumentNullException. |
| `TranslationBuilder WithArgs(Dictionary<string, object?> args)` | Merges args into the runtime arguments using WithArg. Matching names are replaced, case-insensitively. A null dictionary is ignored. |
| `TranslationBuilder Pluralize(int quantity)` | Sets the quantity argument to quantity. Equivalent to WithArg("quantity", quantity); the template's plural expression selects the branch. |
| `TranslationBuilder Genderize(Gender gender)` | Sets the gender argument to the lowercase enum name (unspecified, male, female or neuter). Equivalent to WithArg with that string; select expressions choose the template branch. |
| `TranslationBuilder WithCase(LetterCase letterCase)` | Selects a text transformation applied after formatting. Default is AsIs. Upper and Lower use invariant casing; the other transformations split words before recombining them. |
| `string Resolve()` | Synchronously retrieves and formats the template. Returns a message or the configured fallback. Null, empty, whitespace templates and KeyNotFoundException are treated as missing translations. Missing-key or formatting policies may throw; other store errors propagate. HTTP stores require preloading for synchronous lookup. |
| `Task<string> ResolveAsync(CancellationToken cancellationToken = default)` | Asynchronously retrieves the template using cancellationToken, then formats it with the builder's captured culture. Returns the message or configured fallback. Resolution policies match Resolve(); store failures and cancellation propagate. |
| `string ToString()` | Overrides ToString by calling Resolve(). Performs a synchronous lookup each time and has the same fallback and exception behavior. |
| `static implicit operator string(TranslationBuilder builder)` | Converts builder to a string by calling Resolve(). Supports assignments such as string message = translator.Get("Welcome"); performs synchronous resolution. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/TranslationBuilder.cs)

<a id="api-translationoptions"></a>

### TranslationOptions

Kind: `sealed class` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Policies for missing translations, formatting failures and default arguments.

Fallback strings replace {key} and {0} with the key, and {culture} and {1} with the requested culture name. The builder's default arguments include gender = unspecified and quantity = null.

| Member | Parameters, return value and behavior |
| --- | --- |
| `TranslationOptions()` | Creates mutable options with the defaults listed below. |
| `MissingTranslationBehavior MissingKeyBehavior { get; set; }` | Default: ReturnPlaceholder. Selects placeholder, configured value or exception when the template is missing. *Changed in 0.1.0-alpha:* The init-only accessor became a mutable setter, allowing DI configuration callbacks and later assignment. |
| `string MissingKeyFallbackValue { get; set; }` | Default: [{key}]. Used with ReturnConfiguredValue. ReturnPlaceholder always produces [key] independently of this property. *Changed in 0.1.0-alpha:* The init-only accessor became a mutable setter, allowing DI configuration callbacks and later assignment. |
| `Func<string, CultureInfo?, Exception>? MissingKeyExceptionFactory { get; set; }` | Default: null. Optional factory receiving key and culture when missing-key behavior is ThrowException. It must return a non-null exception. Its returned exception is thrown; factory errors propagate. *Changed in 0.1.0-alpha:* The init-only accessor became a mutable setter, allowing DI configuration callbacks and later assignment. |
| `FormattingErrorBehavior FormattingErrorBehavior { get; set; }` | Default: ReturnPlaceholder. Selects the configured formatting fallback or an exception when a template cannot be parsed or formatted. *Changed in 0.1.0-alpha:* The init-only accessor became a mutable setter, allowing DI configuration callbacks and later assignment. |
| `string FormattingErrorFallbackValue { get; set; }` | Default: [Format Error]. Returned with ReturnPlaceholder; supports the fallback tokens described above. *Changed in 0.1.0-alpha:* The init-only accessor became a mutable setter, allowing DI configuration callbacks and later assignment. |
| `Func<string, CultureInfo?, Exception>? FormattingErrorExceptionFactory { get; set; }` | Default: null. Factory receiving key and culture with ThrowException. A null result falls back to CreateMissingKeyException; errors thrown by the factory propagate. *Changed in 0.1.0-alpha:* The init-only accessor became a mutable setter, allowing DI configuration callbacks and later assignment. |
| `IReadOnlyDictionary<string, object?>? DefaultArguments { get; set; }` | Default: null. Arguments merged into each translation. Runtime values replace matching names without regard to case. *Changed in 0.1.0-alpha:* The init-only accessor became a mutable setter, allowing DI configuration callbacks and later assignment. |
| `IReadOnlyDictionary<string, object?> CreateArgumentSet(IReadOnlyDictionary<string, object?>? runtimeArguments)` | Returns a new case-insensitive dictionary merging DefaultArguments and runtimeArguments. Runtime values win; null inputs are allowed and neither input dictionary is modified. |
| `Exception CreateMissingKeyException(string key, CultureInfo? culture)` | Returns the configured factory result or a TranslationException for key and culture. Direct calls do not throw the returned exception. Rejects null, empty or whitespace keys with ArgumentException (ArgumentNullException for null on modern targets); factory failures propagate. |
| `Exception CreateFormattingException(string key, CultureInfo? culture, Exception? innerException)` | Returns the configured factory result, or a TranslationException whose message includes innerException.Message when available. The default exception does not attach innerException as InnerException. A null factory result uses CreateMissingKeyException. Key validation and factory failures match CreateMissingKeyException. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/TranslationOptions.cs)

<a id="api-itranslationstore"></a>

### ITranslationStore

Kind: `interface` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Storage contract returning raw templates before formatting.

Implementations own their fallback rules. A null result or KeyNotFoundException is treated as a missing translation by TranslationBuilder. Other failures propagate.

| Member | Parameters, return value and behavior |
| --- | --- |
| `string? GetTemplate(string key, CultureInfo culture)` | key identifies the template; culture selects the translation. Returns the unformatted template or null. Must complete synchronously; cannot rely on asynchronous loading. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Asynchronous counterpart returning the raw template or null. cancellationToken allows stores performing asynchronous I/O to cancel the operation. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Interfaces/ITranslationStore.cs)

<a id="api-translationexception"></a>

### TranslationException

Kind: `class` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Inherits: `Exception` · Since: `0.0.1`

Exception carrying the translation key and requested culture. Inherits System.Exception.

Standard Exception members, including Message and InnerException, are inherited from .NET.

| Member | Parameters, return value and behavior |
| --- | --- |
| `TranslationException(string key, CultureInfo? culture, string message, Exception? innerException = null)` | Creates an exception with the supplied key, optional culture, message and optional innerException. Does not validate the key. |
| `string Key { get; }` | The translation key supplied to the constructor. |
| `CultureInfo? Culture { get; }` | The requested culture supplied to the constructor; may be null. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Exceptions/TranslationException.cs)

<a id="api-gender"></a>

### Gender

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Enum values accepted by Genderize.

| Member | Parameters, return value and behavior |
| --- | --- |
| `Unspecified` | No explicit gender; supplies unspecified to the template. The default builder value. |
| `Male` | Supplies male to a select expression. |
| `Female` | Supplies female to a select expression. |
| `Neuter` | Supplies neuter to a select expression. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/Gender.cs)

<a id="api-lettercase"></a>

### LetterCase

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Enum transformations applied after message formatting.

| Member | Parameters, return value and behavior |
| --- | --- |
| `AsIs` | Preserves the formatted text; default transformation. |
| `Upper` | Converts text with ToUpperInvariant. |
| `Lower` | Converts text with ToLowerInvariant. |
| `CamelCase` | Joins words in camelCase. |
| `PascalCase` | Joins words in PascalCase. |
| `SnakeCase` | Joins lowercase words with underscores. |
| `KebabCase` | Joins lowercase words with hyphens. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/LetterCase.cs)

<a id="api-missingtranslationbehavior"></a>

### MissingTranslationBehavior

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Enum controlling missing-key resolution.

| Member | Parameters, return value and behavior |
| --- | --- |
| `ReturnPlaceholder` | Returns [key]; default behavior. |
| `ThrowException` | Throws the configured missing-key exception or TranslationException. |
| `ReturnConfiguredValue` | Returns MissingKeyFallbackValue after token replacement. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/MissingTranslationBehavior.cs)

<a id="api-formattingerrorbehavior"></a>

### FormattingErrorBehavior

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Enum controlling template parsing and formatting failures.

| Member | Parameters, return value and behavior |
| --- | --- |
| `ReturnPlaceholder` | Returns FormattingErrorFallbackValue after token replacement; default behavior. |
| `ThrowException` | Throws the configured formatting exception or TranslationException. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core to FluentLocalizer; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/FormattingErrorBehavior.cs)

<a id="api-itranslationlogger"></a>

### ITranslationLogger

Kind: `interface` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer.Logging` · Since: `0.0.1`

Optional contract for translation diagnostics.

| Member | Parameters, return value and behavior |
| --- | --- |
| `void Log(TranslationLogLevel level, string message, Exception? exception = null)` | Writes message at level, with an optional exception. Implement this interface and pass it to Translator, or use the dependency injection logger adapter. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core.Logging to FluentLocalizer.Logging; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Logging/ITranslationLogger.cs)

<a id="api-translationloglevel"></a>

### TranslationLogLevel

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer.Logging` · Since: `0.0.1`

Enum severity levels passed to ITranslationLogger.

| Member | Parameters, return value and behavior |
| --- | --- |
| `Trace` | Most verbose tracing. |
| `Debug` | Detailed resolution diagnostics. |
| `Information` | Informational events. |
| `Warning` | Recoverable issues, including fallback results. |
| `Error` | Failures that stop normal resolution. |
| `Critical` | Unrecoverable failures. |

Changes:

- 0.1.0-alpha: Namespace moved from FluentLocalizer.Core.Logging to FluentLocalizer.Logging; update using directives and references to fully qualified types.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Logging/TranslationLogLevel.cs)

<a id="api-jsonstoresettings"></a>

### JsonStoreSettings

Kind: `class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Since: `0.1.0-alpha`

Common options inherited by every JSON store options type.

Configure options before creating the store. The store-specific options inherit every property listed here.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonStoreSettings()` | Creates shared settings with the defaults below. |
| `string FallbackCulture { get; set; }` | Default: en-US. The final fallback culture after the requested culture and its neutral form. Must be a valid .NET culture name. |
| `bool ThrowOnMissingStore { get; set; }` | Default: false. Missing translation files may throw FileNotFoundException when true. When false, filesystem and embedded stores skip individual I/O or JSON parse errors; HTTP request failures and invalid HTTP JSON still throw. |
| `long MaxDocumentBytes { get; set; }` | Default: 0 (unlimited). Positive values limit one JSON document's size in bytes. Dedicated store constructors reject negative values with ArgumentOutOfRangeException. Oversized documents may be skipped by file/embedded stores when ThrowOnMissingStore is false; HTTP always rejects them. |
| `IDictionary<string, string> FileMappings { get; }` | Mutable, initially empty, case-insensitive mapping from culture names to JSON file names. Add entries through the returned dictionary; the property cannot be replaced. File and HTTP paths are validated as relative JSON paths. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStoreSettings.cs)

<a id="api-jsonfilestoreoptions"></a>

### JsonFileStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

Filesystem options, inheriting FallbackCulture, ThrowOnMissingStore, MaxDocumentBytes and FileMappings from JsonStoreSettings.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonFileStoreOptions()` | Creates filesystem options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: Locales. Directory scanned recursively for JSON files. Relative paths are resolved against AppContext.BaseDirectory; absolute directory paths are supported. |
| `bool ReloadOnChange { get; set; }` | Default: false. Enables a FileSystemWatcher that invalidates cached translations when files change. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonFileStoreOptions.cs)

<a id="api-embeddedjsonstoreoptions"></a>

### EmbeddedJsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

Embedded-resource options, inheriting all JsonStoreSettings properties.

| Member | Parameters, return value and behavior |
| --- | --- |
| `EmbeddedJsonStoreOptions()` | Creates embedded-resource options with all shared defaults. |
| `Assembly? ResourceAssembly { get; set; }` | Default: null. Assembly containing the embedded JSON files. When null, uses the entry assembly, then the executing assembly if no entry assembly exists. |
| `string ResourcesPath { get; set; }` | Default: Locales. Folder portion of manifest resource names used to select embedded JSON files; not a filesystem path. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/EmbeddedJsonStoreOptions.cs)

<a id="api-httpjsonstoreoptions"></a>

### HttpJsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

HTTP options, inheriting all JsonStoreSettings properties.

| Member | Parameters, return value and behavior |
| --- | --- |
| `HttpJsonStoreOptions()` | Creates HTTP options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: locales. Relative URL path under HttpClient.BaseAddress serving JSON catalogs and the optional manifest.json. Absolute URLs, traversal and query/fragment components are rejected. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/HttpJsonStoreOptions.cs)

<a id="api-jsontranslationstorebase"></a>

### JsonTranslationStoreBase

Kind: `abstract class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore` · Since: `0.1.0-alpha`

Abstract ITranslationStore implementation with cached JSON lookup, nested keys and culture fallback.

Inherited by JsonFileStore and EmbeddedJsonStore. Its constructor JsonTranslationStoreBase(JsonStoreSettings options) is used by derived stores. Protected extension points are Options, ResolveCandidates(CultureInfo), SetDocument(string, string), SetDocument(string, JsonElement), RemoveDocument(string), HasCandidate(CultureInfo), FindTemplate(string, CultureInfo) and ClearTemplateCache().

| Member | Parameters, return value and behavior |
| --- | --- |
| `virtual string? GetTemplate(string key, CultureInfo culture)` | Returns a raw template from cached documents using nested colon-separated key and culture fallback, or null for a missing key. Throws FileNotFoundException when no candidate store exists and ThrowOnMissingStore is true. |
| `virtual Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Returns Task.FromResult(GetTemplate(key, culture)). File and embedded stores inherit this synchronous implementation; it performs no asynchronous I/O and does not inspect cancellationToken. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonTranslationStoreBase.cs)

<a id="api-jsonfilestore"></a>

### JsonFileStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonTranslationStoreBase`, `IDisposable` · Since: `0.1.0-alpha`

Filesystem JSON store implementing ITranslationStore and IDisposable, inheriting JsonTranslationStoreBase.

Supports culture and namespace files, bounded template caching and optional reload. Unsupported in browser applications. Copy catalog files to build and publish output.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonFileStore(JsonFileStoreOptions? options = null)` | options defaults to a new JsonFileStoreOptions. Indexes the resources directory and starts a watcher if requested. Throws PlatformNotSupportedException in browsers, ArgumentOutOfRangeException for a negative size limit, and FileNotFoundException for a missing directory/catalog when configured to throw. |
| `override string? GetTemplate(string key, CultureInfo culture)` | Loads needed files on demand and returns the raw template or null. Supports nested keys and namespace-qualified keys such as common:Home:Welcome, requested/neutral/fallback cultures and file mappings. File, path and JSON errors may propagate with ThrowOnMissingStore. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Inherited from JsonTranslationStoreBase. Wraps synchronous GetTemplate in a completed task; cancellationToken is not inspected. |
| `void Dispose()` | Disposes the optional filesystem watcher. Use a using statement for stores owned by the application. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonFileStore.cs)

<a id="api-embeddedjsonstore"></a>

### EmbeddedJsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonTranslationStoreBase` · Since: `0.1.0-alpha`

Embedded JSON store inheriting JsonTranslationStoreBase; usable in browser applications.

Loads matching assembly resources during construction. Lookup methods are inherited; no Dispose method is required.

| Member | Parameters, return value and behavior |
| --- | --- |
| `EmbeddedJsonStore(EmbeddedJsonStoreOptions? options = null)` | options defaults to a new EmbeddedJsonStoreOptions. Loads matching embedded .json resources into memory. Throws ArgumentOutOfRangeException for a negative size limit. Missing resources throw FileNotFoundException when configured; read/parse errors are skipped when ThrowOnMissingStore is false. |
| `string? GetTemplate(string key, CultureInfo culture)` | Inherited from JsonTranslationStoreBase. Returns the raw nested-key template using culture fallback, or null. May throw FileNotFoundException when no candidate store exists and ThrowOnMissingStore is true. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Inherited from JsonTranslationStoreBase. Returns a completed task from the synchronous lookup; cancellationToken is not inspected. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/EmbeddedJsonStore.cs)

<a id="api-httpjsonstore"></a>

### HttpJsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore`, `IDisposable` · Since: `0.1.0-alpha`

HTTP JSON store implementing ITranslationStore and IDisposable, with preload, lazy loading and refresh.

Supports an optional manifest.json for culture/namespace discovery. The caller owns the supplied HttpClient. HTTP failures, invalid JSON and cancellation propagate independently of ThrowOnMissingStore.

| Member | Parameters, return value and behavior |
| --- | --- |
| `HttpJsonStore(HttpClient httpClient, HttpJsonStoreOptions? options = null)` | httpClient serves catalogs from its base address; options defaults to a new HttpJsonStoreOptions. Does not fetch files during construction. Throws ArgumentNullException for a null client, ArgumentOutOfRangeException for a negative size limit, and ArgumentException for invalid resource paths or mappings. |
| `Task LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)` | Preloads culture and the configured fallback, including namespace files listed in the manifest. Returns when loading completes. culture cannot be null; cancellationToken cancels network/loading operations. Missing required files may throw FileNotFoundException. |
| `Task LoadAsync(IEnumerable<string> cultures, CancellationToken cancellationToken = default)` | Preloads distinct culture names and the configured fallback. cultures cannot be null; invalid names throw CultureNotFoundException. Includes manifest namespace files. Returns when loading completes and allows cancellation. |
| `string? GetTemplate(string key, CultureInfo culture)` | Returns a raw template from already loaded files, with culture fallback, or null. Throws InvalidOperationException when required culture catalogs were not preloaded. A namespace absent from loaded documents returns null; use LoadAsync with a manifest or GetTemplateAsync to load it. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Loads the required culture, fallback and key-specific namespace on demand, then returns the raw template or null. Null key/culture are rejected. Pass cancellationToken to cancel loading. Missing required files may throw FileNotFoundException. |
| `Task RefreshStoreAsync(CancellationToken cancellationToken = default)` | Revalidates the manifest and loaded cultures using ETag/Last-Modified when available, then atomically replaces cached snapshots. A failed refresh preserves the previous snapshot. Returns on completion; cancellation and HTTP/JSON failures propagate. |
| `void Dispose()` | Disposes the store's locks and semaphores. Does not dispose the supplied HttpClient. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/HttpJsonStore.cs)

<a id="api-jsonstore"></a>

### JsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore`, `IDisposable` · Since: `0.0.1`

> **Deprecated in 0.1.0-alpha. Use `JsonFileStore` or `EmbeddedJsonStore` instead.**

Deprecated compatibility wrapper for the original mode-based store.

The wrapper forwards to the selected store. It copies fallback, mappings, resource and reload settings, but currently does not copy MaxDocumentBytes.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonStore(JsonStoreOptions? options = null)` | Creates a wrapper using options or new JsonStoreOptions. SearchMode selects filesystem or embedded resources. Unsupported enum values throw InvalidOperationException; filesystem mode is unsupported in browsers. |
| `string? GetTemplate(string key, CultureInfo culture)` | Forwards key and culture to the selected store. Returns its raw template or null; propagates its errors. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Forwards to the selected store. File and embedded implementations wrap synchronous lookup and do not inspect cancellationToken. |
| `void Dispose()` | Disposes the underlying filesystem store's watcher when filesystem mode is selected. |

Changes:

- 0.1.0-alpha: Reimplemented as a compatibility wrapper over JsonFileStore or EmbeddedJsonStore and marked obsolete. Uses dedicated-store loading and validation; filesystem mode is rejected in browsers.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStore.cs)

<a id="api-jsonstoreoptions"></a>

### JsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.0.1`

> **Deprecated in 0.1.0-alpha. Use `JsonFileStoreOptions` or `EmbeddedJsonStoreOptions` or `HttpJsonStoreOptions` instead.**

Deprecated options for JsonStore, inheriting all JsonStoreSettings properties.

JsonStore currently ignores inherited MaxDocumentBytes.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonStoreOptions()` | Creates compatibility options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: Locales. Filesystem directory or embedded-resource folder depending on SearchMode. |
| `JsonStoreLocation SearchMode { get; set; }` | Default: FileSystem. Selects the compatibility store's backend. |
| `Assembly? ResourceAssembly { get; set; }` | Default: null. Assembly containing catalogs when SearchMode is EmbeddedResources. |
| `bool ReloadOnChange { get; set; }` | Default: false. Enables file watching in FileSystem mode. |

Changes:

- 0.1.0-alpha: Became sealed, inherited JsonStoreSettings and was marked obsolete. ThrowOnError was replaced by inherited ThrowOnMissingStore; FallbackCulture and FileMappings moved to the base class, which also adds MaxDocumentBytes.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStoreOptions.cs)

<a id="api-jsonstorelocation"></a>

### JsonStoreLocation

Kind: `enum` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Since: `0.0.1`

Compatibility enum used by deprecated JsonStoreOptions.SearchMode.

| Member | Parameters, return value and behavior |
| --- | --- |
| `FileSystem` | Filesystem backend; default. Unsupported in browser applications. |
| `EmbeddedResources` | Embedded-resource backend. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStoreOptions.cs)

<a id="api-fluentlocalizerservicecollectionextensions"></a>

### FluentLocalizerServiceCollectionExtensions

Kind: `static class` · Package: `FluentLocalizer.Extensions.DependencyInjection` · Namespace: `Microsoft.Extensions.DependencyInjection` · Since: `0.0.1`

Static extension methods registering FluentLocalizer in IServiceCollection.

Registers ITranslator as a singleton. Register ITranslationStore before resolving the translator. WithLogger is optional; without a registered ITranslationLogger, translation logging is disabled.

| Member | Parameters, return value and behavior |
| --- | --- |
| `static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, Action<TranslationOptions>? configure = null)` | services is the DI collection and cannot be null (ArgumentNullException). configure optionally configures IOptions<TranslationOptions>. Returns a FluentLocalizerBuilder for store/logger registration. *Changed in 0.1.0-alpha:* Always registers options services, including when configure is omitted, and validates services against null. |
| `static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, TranslationOptions translationOptions)` | Registers the supplied translationOptions instance through IOptions and adds the translator. Returns a builder. Null services or translationOptions throw ArgumentNullException. Use a typed argument when passing null to avoid overload ambiguity. *Changed in 0.1.0-alpha:* Correctly registers the supplied TranslationOptions instance with IOptions; validates null services and options. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Extensions.DependencyInjection/FluentLocalizerServiceCollectionExtensions.cs)

<a id="api-fluentlocalizerbuilder"></a>

### FluentLocalizerBuilder

Kind: `sealed class` · Package: `FluentLocalizer.Extensions.DependencyInjection` · Namespace: `FluentLocalizer.Core` · Since: `0.0.1`

Mutable registration builder returned by AddFluentLocalizer.

Despite its namespace, this type belongs to the dependency injection package. WithStore and WithLogger return the same builder. ILoggerAdapter is internal and is not part of the public API.

| Member | Parameters, return value and behavior |
| --- | --- |
| `FluentLocalizerBuilder(IServiceCollection services)` | Wraps services for further registration. Normally created by AddFluentLocalizer; constructing it directly does not register ITranslator. |
| `IServiceCollection Services { get; }` | The service collection supplied to the constructor; can be used for additional application registrations. |
| `FluentLocalizerBuilder WithStore(ITranslationStore store)` | Registers store as a singleton ITranslationStore. A null store throws ArgumentNullException. The caller owns an externally created disposable instance. *Changed in 0.1.0-alpha:* Removed a duplicate singleton ITranslationStore registration for the supplied instance. |
| `FluentLocalizerBuilder WithStore(Func<IServiceProvider, ITranslationStore> factory)` | Registers a singleton store factory. factory receives the service provider on resolution. A null factory throws ArgumentNullException. The DI container owns disposable stores created by the factory. |
| `FluentLocalizerBuilder WithLogger()` | Registers a singleton ITranslationLogger adapter using ILogger<Translator>. Configure Microsoft.Extensions.Logging services and providers so ILogger<Translator> can be resolved. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Extensions.DependencyInjection/FluentLocalizerBuilder.cs)

<!-- api-reference:end -->

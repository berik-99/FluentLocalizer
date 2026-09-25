# FluentLocalizer

[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer)
[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.Store.Json.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer.Store.Json)
[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.Extensions.DependencyInjection.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer.Extensions.DependencyInjection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

**FluentLocalizer** is a lightweight, high-performance .NET library designed for resolving culture-aware translation templates using **ICU / MessageFormat** syntax with clean **Fluent APIs**.

ICU is the Unicode Consortium's internationalization standard for formatting dates, numbers, plurals, and messages in a culture-aware way. In practice, it defines a common set of rules that let the same message be rendered correctly for different languages and regions, for example by changing plural forms (`one`, `few`, `many`, `other`) or by adapting number and date formatting to the current locale. You can read more at the official documentation: https://unicode-org.github.io/icu/.

Stop dealing with cumbersome resource files (`.resx`) or rigid formatting string builders. FluentLocalizer makes multi-language management, pluralization, gender-aware translations, and missing-key fallback seamless, extensible, and developer-friendly.

---

## ✨ Features at a Glance

- **Fluent Translation API:** Expressive, readable, and chainable calls for building translation requests.
- **ICU / MessageFormat Support:** Out-of-the-box support for complex placeholders, pluralization, gender formatting, and casing transformations.
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

Use one file per culture under `Locales`:

```text
Locales/
  en-US.json
  it-IT.json
```

`JsonFileStore` reads from the application output directory by default. Add the files explicitly to the consuming project and copy them to both build and publish output:

```xml
<ItemGroup>
  <Content Include="Locales\**\*.json"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

The store scans `ResourcesPath` (default `Locales`) for top-level `*.json` files. This is a filesystem store and is not supported in browser applications; use `EmbeddedJsonStore` or `HttpJsonStore` there.

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

For HTTP, publish the locale files as static files on the server (for example under `wwwroot/locales` in an ASP.NET Core or Blazor WebAssembly app), then configure an `HttpClient` with the base address that serves them. `ResourcesPath` is the URL path below that base address, and the store requests `{ResourcesPath}/{culture}.json`. Call `LoadAsync` before synchronous `Resolve()`, or use `ResolveAsync()` for lazy loading. `RefreshStoreAsync()` reloads cultures already loaded by that store.

```csharp
using FluentLocalizer.Store.Json;

var client = new HttpClient { BaseAddress = new Uri("https://example.com/") };
using var store = new HttpJsonStore(client, new HttpJsonStoreOptions { ResourcesPath = "locales" });
await store.LoadAsync(["it-IT", "en-US"]);
```

The Blazor WebAssembly sample in `Examples/FluentLocalizer.Samples.BlazorWebApp` uses `+` and `−` buttons to change the unread message count. The pluralized message rerenders immediately, and the language selector switches between the preloaded English and Italian files.

All options support `FallbackCulture`, `FileMappings`, and `ThrowOnMissingStore`. See the [JSON store package guide](Sources/FluentLocalizer.Store.Json/Assets/README.md) for full examples and behavior.

When upgrading from the separate HTTP package, replace `FluentLocalizer.Store.Http` with `FluentLocalizer.Store.Json` and update the namespace to `FluentLocalizer.Store.Json`. The mode-based `JsonStore` and `JsonStoreOptions` API is deprecated; use a store-specific type above.

---
## 🧩 Dependency Injection Plugin

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

**Run Sample Projects:**

```bash
# Console Sample
dotnet run --project Examples/FluentLocalizer.Samples.ConsoleApp/FluentLocalizer.Samples.ConsoleApp.csproj

# Worker Service Sample
dotnet run --project Examples/FluentLocalizer.Samples.WorkerApp/FluentLocalizer.Samples.WorkerApp.csproj

```

---

## 📂 Repository Layout

```text
├── Sources/
│   ├── FluentLocalizer.Core/                         # Engine and core abstractions
│   ├── FluentLocalizer.Store.Json/                   # JSON storage provider
│   └── FluentLocalizer.Extensions.DependencyInjection/ # Microsoft DI integrations
├── Examples/                                         # Runnable sample applications
└── Tests/                                            # Unit & Integration tests

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

FluentLocalizer relies on the powerful [**MessageFormat**](https://github.com/jeffijoe/messageformat.net) library by [@jeffijoe](https://github.com/jeffijoe) for parsing and resolving complex ICU / MessageFormat string templates (pluralization, gender, select formats, and custom functions). 

Huge thanks to the author and maintainers of **MessageFormat** for providing such a solid foundation for .NET localization!

---

## 📜 License

Distributed under the **MIT License**.

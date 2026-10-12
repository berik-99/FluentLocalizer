# FluentLocalizer

[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer)
[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.Store.Json.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer.Store.Json)
[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.Extensions.DependencyInjection.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer.Extensions.DependencyInjection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

**FluentLocalizer** is a lightweight, high-performance .NET library designed for resolving culture-aware translation templates written in the **[ICU message syntax](https://unicode-org.github.io/icu/userguide/format_parse/messages/)** with clean **Fluent APIs**. Templates are parsed and formatted by [**MessageFormat**](https://github.com/jeffijoe/messageformat.net), the open-source .NET library by Jeff Hansen ([@jeffijoe](https://github.com/jeffijoe)): FluentLocalizer would not exist without it.

Explore the [website and browser playground](https://berik-99.github.io/FluentLocalizer/).

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

var translator = new Translator(new InMemoryStore());

var message = translator
    .Get("welcome")
    .WithArg("name", "Ada")
    .Resolve();

Console.WriteLine(message); // Output: Hello Ada!

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
```

For a real application, use the [JSON stores](DOCUMENTATION.md#json-stores) and [dependency injection](DOCUMENTATION.md#dependency-injection).

---

## 📚 Documentation

| Where | What you find |
| :--- | :--- |
| [DOCUMENTATION.md](DOCUMENTATION.md) | [ICU templates](DOCUMENTATION.md#icu-message-templates), [culture selection](DOCUMENTATION.md#culture-selection), [translation options and resolution rules](DOCUMENTATION.md#translation-options), [JSON stores](DOCUMENTATION.md#json-stores) and [dependency injection](DOCUMENTATION.md#dependency-injection). |
| [API-REFERENCE.md](API-REFERENCE.md) | Every public type and member of the three packages, generated from the shared API catalog. |
| [CHANGELOG.md](CHANGELOG.md) | The changes of every release. |
| [Website](https://berik-99.github.io/FluentLocalizer/) | Step-by-step [Core guides](https://berik-99.github.io/FluentLocalizer/docs/core) and [JSON store guides](https://berik-99.github.io/FluentLocalizer/docs/store-json), the searchable API reference for [Core](https://berik-99.github.io/FluentLocalizer/docs/core/v1/api) and the [JSON store](https://berik-99.github.io/FluentLocalizer/docs/store-json/v1/api) with earlier versions, and a browser playground. |
| Package READMEs | Shipped with each NuGet package: [Core](Sources/FluentLocalizer.Core/Assets/README.md), [JSON store](Sources/FluentLocalizer.Store.Json/Assets/README.md), [dependency injection](Sources/FluentLocalizer.Extensions.DependencyInjection/Assets/README.md). |
| [Examples](Examples/README.md) | Runnable console, worker and Blazor samples. |

---

## 🛠️ Building & Running Locally

Clone the repository and build using the .NET CLI:

```bash
dotnet restore FluentLocalizer.slnx
dotnet build FluentLocalizer.slnx --configuration Release
dotnet test FluentLocalizer.slnx --configuration Release
```

The library projects target `netstandard2.0`, `net8.0`, and `net10.0`; the samples and the website target `net10.0`. The test projects also target `net472`, so run the full test matrix on Windows. On other platforms, run the `.NET 10` tests with `dotnet test FluentLocalizer.slnx --configuration Release --framework net10.0`. The website's browser script has its own Node.js test: `node Tests/FluentLocalizer.WebApp.Tests/site.test.cjs`.

**Run sample projects:**

See [the examples guide](Examples/README.md) for basic and advanced scenarios in the console, worker, and Blazor apps.

```bash
# Console Sample
dotnet run --project Examples/FluentLocalizer.Samples.ConsoleApp/FluentLocalizer.Samples.ConsoleApp.csproj

# Worker Service Sample
dotnet run --project Examples/FluentLocalizer.Samples.WorkerApp/FluentLocalizer.Samples.WorkerApp.csproj

# Blazor Web App Sample
dotnet run --project Examples/FluentLocalizer.Samples.BlazorWebApp/FluentLocalizer.Samples.BlazorWebApp/FluentLocalizer.Samples.BlazorWebApp.csproj
```

**Run the website:**

```bash
dotnet run --project Sources/FluentLocalizer.WebApp/FluentLocalizer.WebApp.csproj
```

The website is a Blazor WebAssembly app; the GitHub Pages workflow publishes its static output from `main`. It uses FluentLocalizer with `EmbeddedJsonStore` and `LoadOnDemand`: each language has an embedded interface catalog, such as `Sources/FluentLocalizer.WebApp/Locales/it.json`, and a `Docs` namespace file with the guides, such as `it.Docs.json`, so a visitor's browser parses only its language, and the guides only on documentation pages. It selects the first supported language from the browser's preferences (English, Italian, French, German, or Spanish), preserves the regional culture for formatting, and falls back to English for unsupported languages or missing translations. The header's language dropdown updates the site without reloading or clearing playground edits and remembers the choice for subsequent visits.

**Documentation sources:**

- `DOCUMENTATION.md` and the package READMEs are written by hand. The API reference sections, `API-REFERENCE.md` and `CHANGELOG.md` are generated from `Docs/api-reference.json` by `Docs/Update-ApiReference.ps1`; CI fails when they are out of date.
- The website guides are versioned documentation sets: the pages live in `Sources/FluentLocalizer.WebApp/Pages/Docs/Core/V1` and `Pages/Docs/StoreJson/V1`, their text in the `CoreV1` and `StoreJsonV1` objects of the `Docs` namespace files `Sources/FluentLocalizer.WebApp/Locales/*.Docs.json` (keys `Docs:CoreV1:...` and `Docs:StoreJsonV1:...`, one key per paragraph, in all five languages), and each set is registered in `Pages/Docs/DocsSets.cs`. A new store gets its own release scope and documentation set. The WebApp localization test resolves every catalog string in every language with the real formatter.

[Docs/README.md](Docs/README.md) explains the catalog, release scopes, documentation sets and the release process.

---

## 📂 Repository Layout

```text
├── FluentLocalizer.slnx
├── README.md
├── DOCUMENTATION.md                                 # Usage guide: culture, options, JSON stores, DI
├── API-REFERENCE.md                                 # Generated API reference
├── CHANGELOG.md                                     # Generated release notes
├── Assets/                                          # Repository branding assets
├── Docs/                                            # API catalog (api-reference.json), generator and maintainer guides
├── Sources/
│   ├── FluentLocalizer.Core/                        # Core engine and abstractions
│   ├── FluentLocalizer.Extensions.DependencyInjection/
│   ├── FluentLocalizer.Shared.Polyfill/             # Shared project (.shproj), not a NuGet package
│   ├── FluentLocalizer.Store.Json/                  # File, embedded-resource, and HTTP JSON stores
│   └── FluentLocalizer.WebApp/                      # Website: documentation, playground, API reference
├── Examples/
│   ├── FluentLocalizer.Samples.ConsoleApp/
│   ├── FluentLocalizer.Samples.WorkerApp/
│   └── FluentLocalizer.Samples.BlazorWebApp/
│       ├── FluentLocalizer.Samples.BlazorWebApp/    # ASP.NET Core host
│       └── FluentLocalizer.Samples.BlazorWebApp.Client/ # Blazor WebAssembly client
└── Tests/                                           # Core, JSON store, DI and website tests, API snapshots
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

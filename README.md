# FluentLocalizer

[![NuGet Version](https://img.shields.io/nuget/v/FluentLocalizer.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/FluentLocalizer)
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
dotnet add package FluentLocalizer.Core

```

Create a quick in-memory store and resolve your first message:

```csharp
using FluentLocalizer.Core;
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

## 📄 JSON Store Plugin

Load translations directly from JSON files with hot-reloading and fallback support.

```bash
dotnet add package FluentLocalizer.Store.Json

```

### Folder Structure

```text
Locales/
  ├── en-US.json
  └── it-IT.json

```

Place one JSON file per culture in the `Locales` folder. A simple example is shown below:

**Example `en-US.json`:**

```json
{
  "Welcome": "Hello {name}!",
  "Notifications": {
    "MessageCount": "You have {count, plural, =0 {no messages} one {# message} other {# messages}}."
  }
}
```

### Locales folder and output inclusion

By default, the JSON store package includes every `.json` file under `Locales` in the build output, so the translations are copied next to your application binaries when you build. That makes `JsonStoreLocation.FileSystem` the simplest option for most projects.

If you prefer to bundle translations as embedded resources instead, switch the project file to include them as embedded content and configure the store to read embedded files:

```xml
<ItemGroup>
  <EmbeddedResource Include="Locales\**\*.json" />
</ItemGroup>
```

```csharp
var options = new JsonStoreOptions
{
    ResourcesPath = "Locales",
    SearchMode = JsonStoreLocation.EmbeddedResources,
    ResourceAssembly = typeof(Program).Assembly,
    ThrowOnError = true
};
```

Use `FileSystem` when you want files on disk, and `EmbeddedResources` when you want translations baked into the assembly.

### Usage

```csharp
using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;

var options = new JsonStoreOptions
{
    ResourcesPath = "Locales",
    SearchMode = JsonStoreLocation.FileSystem,
    FallbackCulture = "en-US",
    ThrowOnError = true
};

using var store = new JsonStore(options);
var translator = new Translator(store);

var message = translator
    .Get("Welcome")
    .WithArg("name", "Ada")
    .Resolve();

Console.WriteLine(message); // Output: Ciao Ada!

```

---

## 🧩 Dependency Injection Plugin

Integration with `IServiceCollection` for ASP.NET Core, Worker Services, or Console apps.

```bash
dotnet add package FluentLocalizer.Extensions.DependencyInjection

```

### Registration & Worker Example

```csharp
using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFluentLocalizer(options =>
{
    options.MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue;
    options.MissingKeyFallbackValue = "[{key}]";
})
.WithStore(new JsonStore(new JsonStoreOptions
{
    ResourcesPath = "Locales",
    SearchMode = JsonStoreLocation.FileSystem,
    FallbackCulture = "en-US",
    ThrowOnError = true
}))
.WithLogger();

builder.Services.AddHostedService<NotificationWorker>();

await builder.Build().RunAsync();

public sealed class NotificationWorker(ITranslationService translator) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var message = translator
            .Get("Notifications:MessageCount")
            .WithCulture("it-IT")
            .WithArg("quantity", 3)
            .Resolve();

        Console.WriteLine(message); // Output: Hai 3 messaggi non letti.
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

- 💡 **Have an idea or feature request?** Open an [Issue](https://github.com/your-username/FluentLocalizer/issues).
- 🐛 **Found a bug?** Submit an [Issue](https://github.com/your-username/FluentLocalizer/issues) with steps to reproduce it.
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
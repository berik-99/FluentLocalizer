# FluentLocalizer.Core

FluentLocalizer.Core is the core package for building culture-aware translation pipelines in .NET applications. It provides a fluent API around an `ITranslationStore`, with MessageFormat-style interpolation, runtime arguments, and configurable fallback behavior. Under the hood, it uses the MessageFormat engine to support ICU-inspired message formatting patterns.

ICU (International Components for Unicode) is the Unicode standard for culture-aware formatting. It defines how languages handle plurals, numbers, dates, and message selection rules, so the same template can adapt to different locales without custom code. The official reference is https://unicode-org.github.io/icu/. A simple example is a plural rule such as `one{# item}` vs `other{# items}`, which is selected automatically for the current culture.

## Install

```bash
dotnet add package FluentLocalizer.Core
```

## Why use it?

- A fluent builder API for composing translation requests in a readable way
- Culture-aware resolution per request, with support for fallback cultures; `WithCulture(...)` is only needed when you want to override the default `CurrentUICulture`
- ICU-style MessageFormat templates, including named placeholders and message-format constructs
- Built-in helpers for pluralization, gender-aware formatting, and case transformation
- Support for sync and async resolution, plus custom handling for missing keys and formatting errors

## Quick start

The core package defines the translation engine and contracts. You provide a store implementation (for example the official `FluentLocalizer.Store.Json` package or your own custom `ITranslationStore`) and then resolve translations through a fluent builder.

```csharp
using FluentLocalizer.Core;
using System.Collections.Generic;
using System.Globalization;

public sealed class InMemoryStore : ITranslationStore
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome"] = "Hello {name}!"
    };

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        => Task.FromResult(_templates.TryGetValue(key, out var value) ? value : null);

    public string? GetTemplate(string key, CultureInfo culture)
        => _templates.TryGetValue(key, out var value) ? value : null;
}

var store = new InMemoryStore();
var translator = new Translator(store);

var message = translator
    .Get("welcome")
    .WithArg("name", "Ada")
    .Resolve();

Console.WriteLine(message); // Hello Ada!
```

## ICU-style message formatting

FluentLocalizer.Core is built on top of MessageFormat, so templates can use ICU-inspired message formatting patterns such as named arguments and message-format constructs. The fluent API makes it easy to supply these values at runtime.

```csharp
public sealed class InMemoryStore : ITranslationStore
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["items"] = "You have {count, plural, one{# item} other{# items}}.",
        ["profile"] = "{gender, select, female{She} male{He} other{They}} liked {name}.",
        ["welcome"] = "Hello {name}!"
    };

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        => Task.FromResult(_templates.TryGetValue(key, out var value) ? value : null);

    public string? GetTemplate(string key, CultureInfo culture)
        => _templates.TryGetValue(key, out var value) ? value : null;
}

var store = new InMemoryStore();
var translator = new Translator(store);

var itemSummary = translator
    .Get("items")
    .WithCulture("en-US")
    .WithArg("count", 2)
    .Resolve();

var profileMessage = translator
    .Get("profile")
    .WithCulture("en-US")
    .WithArg("name", "Ada")
    .Genderize(Gender.Female)
    .Resolve();
```

## Fluent API in action

You can chain the fluent methods to build expressive translation requests:

```csharp
var message = translator
    .Get("dashboard")
    .WithCulture("it-IT")
    .WithArg("name", "Ada")
    .WithArgs(new Dictionary<string, object?>
    {
        ["city"] = "Rome",
        ["count"] = 3
    })
    .Pluralize(3)
    .Genderize(Gender.Female)
    .WithCase(LetterCase.PascalCase)
    .Resolve();
```

### Available fluent methods

- `WithCulture(...)` selects the culture for the current request when you want to override `CurrentUICulture`
- `WithArg(...)` and `WithArgs(...)` add runtime arguments
- `Pluralize(...)` exposes the `quantity` argument used by plural formatting
- `Genderize(...)` exposes the `gender` argument used by gender-aware formatting
- `WithCase(...)` applies casing transforms such as upper, lower, camelCase, PascalCase, snake_case, or kebab-case
- `WithOptions(...)` overrides the current `TranslationOptions` for a single request
- `Resolve()` resolves synchronously
- `ResolveAsync()` resolves asynchronously

## Logging with ITranslationLogger

FluentLocalizer.Core also supports an optional `ITranslationLogger` contract. This is useful when you want to observe missing keys, formatting errors, or other translation events while keeping the logging behavior fully under your control.

```csharp
using FluentLocalizer.Core;
using FluentLocalizer.Core.Logging;

public sealed class ConsoleTranslationLogger : ITranslationLogger
{
    public void Log(TranslationLogLevel level, string message, Exception? exception = null)
    {
        Console.WriteLine($"[{level}] {message}");

        if (exception is not null)
        {
            Console.WriteLine(exception.Message);
        }
    }
}
```

You can pass the logger to the translator when you create it:

```csharp
var logger = new ConsoleTranslationLogger();
var translator = new Translator(store, options, logger);
```

## Complete configuration example

The following example shows a full setup with a custom store, explicit options, and a logger. It is useful when you want to configure FluentLocalizer once and reuse the translator throughout the application.

```csharp
using FluentLocalizer.Core;
using FluentLocalizer.Core.Logging;
using System.Collections.Generic;
using System.Globalization;

public sealed class DemoStore : ITranslationStore
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome"] = "Hello {name}!",
        ["items"] = "You have {count, plural, one{# item} other{# items}}.",
        ["profile"] = "{gender, select, female{She} male{He} other{They}} liked {name}."
    };

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        => Task.FromResult(_templates.TryGetValue(key, out var value) ? value : null);

    public string? GetTemplate(string key, CultureInfo culture)
        => _templates.TryGetValue(key, out var value) ? value : null;
}

public sealed class DemoLogger : ITranslationLogger
{
    public void Log(TranslationLogLevel level, string message, Exception? exception = null)
    {
        Console.WriteLine($"[{level}] {message}");
    }
}

var store = new DemoStore();
var options = new TranslationOptions
{
    MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
    MissingKeyFallbackValue = "Missing '{key}' for culture '{culture}'.",
    FormattingErrorBehavior = FormattingErrorBehavior.ThrowException,
    DefaultArguments = new Dictionary<string, object?>
    {
        ["name"] = "Ada"
    }
};

var logger = new DemoLogger();
var translator = new Translator(store, options, logger);

var welcome = translator
    .Get("welcome")
    .Resolve();

var summary = translator
    .Get("items")
    .WithArg("count", 2)
    .Resolve();

var profile = translator
    .Get("profile")
    .Genderize(Gender.Female)
    .Resolve();
```

This example shows how the core package can be configured for a real application: default arguments are supplied once, missing keys and formatting errors are handled consistently, and logging is available without needing a DI container.

## Async support

```csharp
var message = await translator
    .Get("welcome")
    .WithArg("name", "Ada")
    .ResolveAsync();
```

## Notes

FluentLocalizer.Core is intentionally small and extensible. It focuses on the translation engine and leaves storage concerns to implementations such as `FluentLocalizer.Store.Json` or your own custom `ITranslationStore`.

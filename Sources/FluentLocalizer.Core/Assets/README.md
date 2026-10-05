# FluentLocalizer

FluentLocalizer is the core package for building culture-aware translation pipelines in .NET applications. It provides a fluent API around an `ITranslationStore`, with templates written in the [ICU message syntax](https://unicode-org.github.io/icu/userguide/format_parse/messages/), runtime arguments, and configurable fallback behavior. Under the hood, templates are parsed and formatted by [MessageFormat](https://github.com/jeffijoe/messageformat.net), the open-source .NET library by Jeff Hansen.

ICU (International Components for Unicode) is the Unicode standard for culture-aware formatting. It defines how languages handle plurals, numbers, dates, and message selection rules, so the same template can adapt to different locales without custom code. The official reference is https://unicode-org.github.io/icu/, and the [message formatting guide](https://unicode-org.github.io/icu/userguide/format_parse/messages/) describes the template syntax. A simple example is a plural rule such as `one{# item}` vs `other{# items}`, which is selected automatically for the current culture.

## Install

For every public type and member in this package, see the [API reference](#api-reference). The [online documentation](https://berik-99.github.io/FluentLocalizer/docs) covers templates, requests, culture, errors and logging step by step.

```bash
dotnet add package FluentLocalizer
```

## Why use it?

- A fluent builder API for composing translation requests in a readable way
- Culture-aware resolution that follows `CultureInfo.CurrentUICulture` automatically, with fallback cultures handled by the store; `WithCulture(...)` only forces a different culture for a single request
- Templates in the ICU message syntax, including named placeholders, `plural` and `select`
- Built-in helpers for pluralization, gender-aware formatting, and case transformation
- Support for sync and async resolution, plus custom handling for missing keys and formatting errors

## Quick start

The core package defines the translation engine and contracts. You provide a store implementation (for example the official `FluentLocalizer.Store.Json` package or your own custom `ITranslationStore`) and then resolve translations through a fluent builder.

```csharp
using FluentLocalizer;
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

## ICU message syntax

Templates use the [ICU message syntax](https://unicode-org.github.io/icu/userguide/format_parse/messages/): named arguments such as `{name}`, plus `plural` and `select` branches that each language can phrase in its own way. FluentLocalizer formats them with [MessageFormat](https://github.com/jeffijoe/messageformat.net), whose documentation lists the ICU features it supports. The fluent API makes it easy to supply the argument values at runtime.

```csharp
public sealed class InMemoryStore : ITranslationStore
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["items"] = "You have {quantity, plural, one{# item} other{# items}}.",
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
    .Pluralize(2)
    .Resolve();

var profileMessage = translator
    .Get("profile")
    .WithArg("name", "Ada")
    .Genderize(Gender.Female)
    .Resolve();
```

## Fluent API in action

You can chain the fluent methods to build expressive translation requests:

```csharp
var message = translator
    .Get("dashboard")
    .WithArg("name", "Ada")
    .WithArgs(new Dictionary<string, object?>
    {
        ["city"] = "Torino",
        ["address"] = "Via Montebello 20"
    })
    .Pluralize(3)
    .Genderize(Gender.Female)
    .WithCase(LetterCase.PascalCase)
    .Resolve();
```

`WithArg` and `WithArgs` use case-insensitive argument names; runtime values override configured default arguments with the same name. `Resolve()` and `ResolveAsync()` return the formatted message. Their result depends on the configured missing-key and formatting-error policies, which can return placeholders, configured fallback strings, or throw exceptions. Nothing is looked up until `Resolve()`/`ResolveAsync()`; `ToString()` and the implicit `string` conversion call `Resolve()` each time.

### Available fluent methods

- `WithCulture(...)` is optional: it forces a culture other than `CurrentUICulture` for the current request only
- `WithArg(...)` and `WithArgs(...)` add runtime arguments
- `Pluralize(...)` sets the `quantity` argument used by plural formatting; it is equivalent to `WithArg("quantity", ...)`.
- `Genderize(...)` sets the `gender` argument used by gender-aware formatting; it is equivalent to `WithArg("gender", ...)`.
- `WithCase(...)` applies casing transforms such as upper, lower, camelCase, PascalCase, snake_case, or kebab-case
- `WithOptions(...)` replaces the whole `TranslationOptions` object for a single request (`null` restores the defaults)
- `Resolve()` resolves synchronously
- `ResolveAsync()` resolves asynchronously, passing the `CancellationToken` to the store

### Template and resolution rules

- Every placeholder needs a value: a missing argument is a formatting error (`[Format Error]` by default), never empty text. `null` values print nothing.
- Each request starts with `quantity = null` and `gender = "unspecified"`. `Pluralize(int)` and `Genderize(Gender)` overwrite them, so `DefaultArguments` entries for these names are never used. A `plural` block treats a `null` quantity as `0`.
- If a template contains `{Length}` and no `Length` argument is supplied, it receives the count of non-null arguments, including the built-in `gender`.
- `select` compares branch names case-sensitively; enums match by name. `plural` and `select` need an `other` branch for unmatched values.
- `#` is replaced only directly inside a `plural` branch, not inside a `select` nested in it: nest `plural` inside `select`.
- MessageFormat supports `number` (default, `integer`, `percent`, `currency` — the currency of the culture), `date` (default/`short`, `full`), `time` (default, `short`), `selectordinal`, `offset:` and `=N` branches. Other styles are formatting errors. Literal braces are quoted with apostrophes (`'{name}'`), `''` prints one apostrophe, backslashes are not escapes.
- `WithCase` runs after formatting. `Upper`/`Lower` use invariant casing; the four identifier casings split words at spaces, punctuation, case and digit boundaries and drop spaces and punctuation.
- A `null`, empty or whitespace template, or a `KeyNotFoundException` from the store, is a missing translation. Other store exceptions propagate unchanged.
- `WithCulture(null)` selects `CultureInfo.CurrentCulture`, not the UI culture. Even when the store serves a fallback culture's template, formatting and plural rules use the request culture.
- `Translator` is stateless and thread-safe; `TranslationBuilder` is mutable and not thread-safe. The translator keeps a reference to its `TranslationOptions`, so later changes to that object affect new requests.

## Translation options

| Property | Default | Behavior |
| --- | --- | --- |
| `MissingKeyBehavior` | `ReturnPlaceholder` | `ReturnPlaceholder` returns `[key]` (ignoring `MissingKeyFallbackValue`); `ReturnConfiguredValue` returns `MissingKeyFallbackValue`; `ThrowException` throws. |
| `MissingKeyFallbackValue` | `"[{key}]"` | Used with `ReturnConfiguredValue`. |
| `MissingKeyExceptionFactory` | `null` | `(key, culture) => Exception` used with `ThrowException`; must not return `null`. |
| `FormattingErrorBehavior` | `ReturnPlaceholder` | `ReturnPlaceholder` returns `FormattingErrorFallbackValue`; `ThrowException` throws. |
| `FormattingErrorFallbackValue` | `"[Format Error]"` | Returned for parse and formatting failures. |
| `FormattingErrorExceptionFactory` | `null` | Used with `ThrowException`; a `null` result falls back to the missing-key exception. |
| `DefaultArguments` | `null` | Merged into every request; request values win. |

Fallback values replace `{key}`/`{0}` and `{culture}`/`{1}` case-insensitively, as plain text. The default exception is a `TranslationException` with `Key` and `Culture`; for formatting errors its message includes the formatter's explanation, but `InnerException` is not set. Custom factory exceptions are thrown as they are. With `ThrowException`, an empty or whitespace key throws `ArgumentException`. `CreateMissingKeyException` and `CreateFormattingException` return the exception without throwing it.

## Logging with ITranslationLogger

FluentLocalizer also supports an optional `ITranslationLogger` contract. This is useful when you want to observe missing keys, formatting errors, or other translation events while keeping the logging behavior fully under your control.

```csharp
using FluentLocalizer;
using FluentLocalizer.Logging;

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

The translator writes `Debug` entries for every request (start, template retrieved — async only —, formatting, completion), a `Warning` whenever a fallback value is returned, and an `Error` before an exception leaves `Resolve()`/`ResolveAsync()`, including store exceptions. `Trace`, `Information` and `Critical` are not used. Formatting warnings do not include the formatter's message. Stores do not log.

You can pass the logger to the translator when you create it:

```csharp
var logger = new ConsoleTranslationLogger();
var translator = new Translator(store, options, logger);
```

## Complete configuration example

The following example shows a full setup with a custom store, explicit options, and a logger. It is useful when you want to configure FluentLocalizer once and reuse the translator throughout the application.

```csharp
using FluentLocalizer;
using FluentLocalizer.Logging;
using System.Collections.Generic;
using System.Globalization;

public sealed class DemoStore : ITranslationStore
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["welcome"] = "Hello {name}!",
        ["items"] = "You have {quantity, plural, one{# item} other{# items}}.",
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
    .Pluralize(2)
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

FluentLocalizer is intentionally small and extensible. It focuses on the translation engine and leaves storage concerns to implementations such as `FluentLocalizer.Store.Json` or your own custom `ITranslationStore`.

## Acknowledgments

Message parsing and formatting are provided by [MessageFormat](https://github.com/jeffijoe/messageformat.net) by Jeff Hansen ([@jeffijoe](https://github.com/jeffijoe)), distributed under the MIT License. Many thanks to its author and contributors. See [THIRD-PARTY-NOTICES.md](https://github.com/berik-99/FluentLocalizer/blob/main/THIRD-PARTY-NOTICES.md) for the full notice.

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

<!-- api-reference:end -->

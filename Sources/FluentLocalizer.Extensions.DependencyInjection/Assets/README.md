# FluentLocalizer.Extensions.DependencyInjection

FluentLocalizer.Extensions.DependencyInjection provides ASP.NET Core and generic .NET hosting integration for FluentLocalizer. It registers `ITranslator` and related services in `IServiceCollection`, so you can consume translations from your application with minimal setup.

## Install

For every public type and member in this package, see the [API reference](#api-reference).

```bash
dotnet add package FluentLocalizer.Extensions.DependencyInjection
```

The JSON store examples also require `FluentLocalizer.Store.Json`:

```bash
dotnet add package FluentLocalizer.Store.Json
```

## Basic registration

```csharp
using FluentLocalizer;
using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddFluentLocalizer(options =>
{
    options.MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue;
    options.MissingKeyFallbackValue = "[{key}]";
})
    .WithStore(new JsonFileStore(new JsonFileStoreOptions
    {
        ResourcesPath = "Locales",
        FallbackCulture = "en-US"
    }))
    .WithLogger();

var provider = services.BuildServiceProvider();
var translator = provider.GetRequiredService<ITranslator>();

var message = translator.Get("Welcome")
    .WithCulture("it-IT")
    .WithArg("name", "Ada")
    .Resolve();
```

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

## Hosted service / worker example

If you are building a hosted service, worker, or background processor, you can register the translator once and inject it into your service class.

```csharp
using FluentLocalizer;
using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFluentLocalizer()
    .WithStore(new JsonFileStore(new JsonFileStoreOptions
    {
        ResourcesPath = "Locales",
        FallbackCulture = "en-US",
        ThrowOnMissingStore = true
    }));

builder.Services.AddHostedService<NotificationWorker>();

await builder.Build().RunAsync();

public sealed class NotificationWorker(ITranslator translator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var message = translator
            .Get("Notifications:MessageCount")
            .WithArg("quantity", 3)
            .Resolve();

        Console.WriteLine(message);
        await Task.CompletedTask;
    }
}
```

This pattern is useful when the translator is reused across several services and you want the store configuration centralized in the host setup.

## What gets registered

`AddFluentLocalizer(...)` registers an `ITranslator` implementation that is created from:

- the `ITranslationStore` you provide
- `TranslationOptions` from DI configuration
- an optional `ITranslationLogger`

This makes the translator available throughout your app via dependency injection.


## Using a custom store factory

If you prefer to resolve the store from the service provider, you can register it lazily:

```csharp
services.AddFluentLocalizer()
    .WithStore(sp => new JsonFileStore(new JsonFileStoreOptions
    {
        ResourcesPath = "Locales",
        FallbackCulture = "en-US"
    }));
```

## Configuring options from DI

You can also configure `TranslationOptions` with the standard `IOptions` pattern:

```csharp
services.AddFluentLocalizer(options =>
{
    options.DefaultArguments = new Dictionary<string, object?>
    {
        ["name"] = "Ada"
    };
});
```

## Logging

To enable logging integration, call `WithLogger()`; it registers an adapter that bridges FluentLocalizer's `ITranslationLogger` to the standard .NET `ILogger` abstraction. This makes the integration compatible with the logging systems you already use in your application, including Serilog, Console, Debug, and other `ILogger`-based providers.

```csharp
services.AddFluentLocalizer()
    .WithLogger();
```

In practice, this means you can keep using your existing logging pipeline and benefit from FluentLocalizer diagnostics without introducing a separate logging model.

## Notes

This package is intentionally lightweight. It wires FluentLocalizer and your chosen store implementation into dependency injection containers.

<!-- api-reference:start -->
## API reference

Public types and members declared by FluentLocalizer, grouped by package and namespace. Includes constructors, overloads, properties, enum values, inherited library members and deprecated APIs. Standard inherited .NET members are omitted.

Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. Every type states the version that introduced it; later additions, changes and deprecations are marked on each member. The searchable [WebUI API reference](https://berik-99.github.io/FluentLocalizer/docs/api) uses the same catalog and can show any earlier version.

Documented version: `1.0.0`

- [FluentLocalizerServiceCollectionExtensions](#api-fluentlocalizerservicecollectionextensions)
- [FluentLocalizerBuilder](#api-fluentlocalizerbuilder)

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

# FluentLocalizer.Extensions.DependencyInjection

FluentLocalizer.Extensions.DependencyInjection provides ASP.NET Core and generic .NET hosting integration for FluentLocalizer. It registers `ITranslator` and related services in `IServiceCollection`, so you can consume translations from your application with minimal setup.

## Install

For every public type and member in this package, see the [API reference](#api-reference). The [online guide](https://berik-99.github.io/FluentLocalizer/docs/core/dependency-injection) covers ASP.NET Core, worker services and Blazor.

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
using FluentLocalizer.Store.Json;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging(); // required by WithLogger() outside a host

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
    .WithArg("name", "Ada")
    .Resolve();
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

## Hosted service / worker example

If you are building a hosted service, worker, or background processor, you can register the translator once and inject it into your service class.

```csharp
using FluentLocalizer;
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

| Service | Lifetime | Registered by |
| --- | --- | --- |
| `ITranslator` | Singleton, created on first resolution | `AddFluentLocalizer` |
| `IOptions<TranslationOptions>` | Singleton | `AddFluentLocalizer` (`AddOptions` + `Configure`, or `Options.Create` for the instance overload) |
| `ITranslationStore` | Singleton | `WithStore` |
| `ITranslationLogger` | Singleton adapter to `ILogger<Translator>` | `WithLogger` (optional) |

- `AddFluentLocalizer` lives in the `Microsoft.Extensions.DependencyInjection` namespace; the returned `FluentLocalizerBuilder` is in `FluentLocalizer.Core`, which you only import to name the type.
- Resolving `ITranslator` without a registered store throws `InvalidOperationException`. Registering `ITranslationStore` or `ITranslationLogger` yourself is equivalent to `WithStore`/`WithLogger`.
- Call `AddFluentLocalizer` once per container: a second call adds another `ITranslator` registration and the last one wins.
- `WithStore(instance)`: the caller owns and disposes the store. `WithStore(factory)`: the container creates it lazily and disposes it (`JsonFileStore`, `HttpJsonStore`) when the provider is disposed.
- The callback overload combines with other `Configure<TranslationOptions>` calls (including configuration binding of the simple properties). The instance overload registers that exact object; other `Configure` calls are ignored. Either way the options are read once, when the singleton translator is created.
- `WithLogger()` requires `ILogger<Translator>`: hosts register logging, a bare `ServiceCollection` needs `AddLogging()`. The category is `FluentLocalizer.Translator`; levels map one-to-one to `LogLevel`.


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

Version numbers shown for a type or member refer to the NuGet package named on that type.

Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. Every type states the version that introduced it; later additions and deprecations are marked on each member, and the changes between versions are listed in the changelog. The searchable [WebUI API reference](https://berik-99.github.io/FluentLocalizer/docs/core/v1/api) uses the same catalog and can show any earlier version; the [changelog](https://berik-99.github.io/FluentLocalizer/changelog?scope=core) lists the changes of every release.

- [FluentLocalizerServiceCollectionExtensions](#api-fluentlocalizerservicecollectionextensions)
- [FluentLocalizerBuilder](#api-fluentlocalizerbuilder)

<a id="api-fluentlocalizerservicecollectionextensions"></a>

### FluentLocalizerServiceCollectionExtensions

Kind: `static class` · Package: `FluentLocalizer.Extensions.DependencyInjection` · Namespace: `Microsoft.Extensions.DependencyInjection` · Since: `0.0.1`

Static extension methods registering FluentLocalizer in IServiceCollection.

Registers ITranslator as a singleton. Register ITranslationStore before resolving the translator. WithLogger is optional; without a registered ITranslationLogger, translation logging is disabled.

| Member | Parameters, return value and behavior |
| --- | --- |
| `static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, Action<TranslationOptions>? configure = null)` | services is the DI collection and cannot be null (ArgumentNullException). configure optionally configures IOptions<TranslationOptions>. Returns a FluentLocalizerBuilder for store/logger registration. |
| `static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, TranslationOptions translationOptions)` | Registers the supplied translationOptions instance through IOptions and adds the translator. Returns a builder. Null services or translationOptions throw ArgumentNullException. Use a typed argument when passing null to avoid overload ambiguity. |

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
| `FluentLocalizerBuilder WithStore(ITranslationStore store)` | Registers store as a singleton ITranslationStore. A null store throws ArgumentNullException. The caller owns an externally created disposable instance. |
| `FluentLocalizerBuilder WithStore(Func<IServiceProvider, ITranslationStore> factory)` | Registers a singleton store factory. factory receives the service provider on resolution. A null factory throws ArgumentNullException. The DI container owns disposable stores created by the factory. |
| `FluentLocalizerBuilder WithLogger()` | Registers a singleton ITranslationLogger adapter using ILogger<Translator>. Configure Microsoft.Extensions.Logging services and providers so ILogger<Translator> can be resolved. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Extensions.DependencyInjection/FluentLocalizerBuilder.cs)

<!-- api-reference:end -->

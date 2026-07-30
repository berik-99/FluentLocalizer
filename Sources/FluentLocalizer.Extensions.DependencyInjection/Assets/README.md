# FluentLocalizer.Extensions.DependencyInjection

FluentLocalizer.Extensions.DependencyInjection provides ASP.NET Core and generic .NET hosting integration for FluentLocalizer. It registers `ITranslator` and related services in `IServiceCollection`, so you can consume translations from your application with minimal setup.

## Install

```bash
dotnet add package FluentLocalizer.Extensions.DependencyInjection
```

## Basic registration

```csharp
using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddFluentLocalizer(options =>
{
    options.MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue;
    options.MissingKeyFallbackValue = "[{key}]";
})
    .WithStore(new JsonStore(new JsonStoreOptions
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

## Hosted service / worker example

If you are building a hosted service, worker, or background processor, you can register the translator once and inject it into your service class.

```csharp
using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFluentLocalizer()
    .WithStore(new JsonStore(new JsonStoreOptions
    {
        ResourcesPath = "Locales",
        SearchMode = JsonStoreLocation.FileSystem,
        FallbackCulture = "en-US",
        ThrowOnError = true
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
    .WithStore(sp => new JsonStore(new JsonStoreOptions
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

This package is intentionally lightweight. It focuses on wiring FluentLocalizer into dependency injection containers so the rest of the translation pipeline remains in `FluentLocalizer.Core` and your chosen store implementation.

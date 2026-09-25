# FluentLocalizer.Store.Http

`FluentLocalizer.Store.Http` implements `ITranslationStore` by downloading culture-specific JSON files over HTTP. It caches parsed files in memory, supports nested JSON keys and culture fallback, and does not depend on the FluentLocalizer dependency-injection package.

## Install

```bash
dotnet add package FluentLocalizer.Store.Http
```

To register `ITranslator` through `IServiceCollection`, install `FluentLocalizer.Extensions.DependencyInjection` separately. The HTTP store itself only requires an `HttpClient` and can also be used without dependency injection.

## Serve the locale files

Place the files in a folder served by your web host. For a Blazor Web App, a typical layout is:

```text
wwwroot/
  locales/
    en-US.json
    it-IT.json
```

Example `it-IT.json`:

```json
{
  "Home": {
    "Title": "Localizzazione con FluentLocalizer",
    "Welcome": "Ciao {name}!",
    "Unread": "{quantity, plural, =0 {Non hai messaggi} one {Hai un messaggio} other {Hai # messaggi}}"
  }
}
```

Nested properties are looked up with colon-separated keys, for example `Home:Welcome` and `Home:Unread`.

## Configure the store

Create an `HttpClient` whose base address is the app's base URI. `ResourcesPath` is a relative path under that base address; a leading slash is accepted and trimmed. The store appends the culture file name, such as `locales/it-IT.json`.

```csharp
using FluentLocalizer;
using FluentLocalizer.Store.Http;
using Microsoft.Extensions.DependencyInjection;

var httpClient = new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
};

var store = new HttpJsonStore(httpClient, new HttpJsonStoreOptions
{
    ResourcesPath = "/locales",
    FallbackCulture = "en-US",
    ThrowOnMissingStore = true
});

builder.Services.AddSingleton(store);
builder.Services.AddFluentLocalizer()
    .WithStore(store);

var app = builder.Build();

// Preload supported locales before components use synchronous Resolve().
await store.LoadAsync(["it-IT", "en-US"]);

await app.RunAsync();
```

`LoadAsync` loads the requested cultures and the configured fallback culture. It also tries the neutral culture file where applicable, such as `it.json` for `it-IT`. The first request for each file is sent with `Cache-Control: no-cache` so the HTTP cache can revalidate it. A full page reload creates a fresh in-memory store; call `LoadAsync` during each app startup to retrieve the current server files.

## Resolve translations

After preloading, synchronous lookups are served from the in-memory cache:

```csharp
var greeting = translator
    .Get("Home:Welcome")
    .WithCulture("it-IT")
    .WithArg("name", "Ada")
    .Resolve();

var unread = translator
    .Get("Home:Unread")
    .WithCulture("it-IT")
    .Pluralize(3)
    .Resolve();
```

You can also skip eager loading and use `ResolveAsync()`. It downloads the requested culture on first use, then reuses the cached JSON for later lookups:

```csharp
var greeting = await translator
    .Get("Home:Welcome")
    .WithCulture("it-IT")
    .WithArg("name", "Ada")
    .ResolveAsync();
```

Calling synchronous `Resolve()` for a culture that has not been loaded throws an `InvalidOperationException`; call `LoadAsync` first or use `ResolveAsync()`.

## Refresh translations

The HTTP store does not watch files in real time. After translations change on the server, refresh the cultures already loaded by the current store instance:

```csharp
await store.RefreshStoreAsync();
```

This downloads the files again, updates the in-memory cache, and asks the HTTP cache to revalidate its response. `RefreshStoreAsync` is asynchronous because it performs network requests.

## Custom file names

Use `FileMappings` when a culture should use a different file name:

```csharp
var options = new HttpJsonStoreOptions
{
    ResourcesPath = "locales",
    FallbackCulture = "en-US"
};
options.FileMappings["it-IT"] = "italiano.json";

var store = new HttpJsonStore(httpClient, options);
```

## Use with dependency injection

The store has no dependency on `FluentLocalizer.Extensions.DependencyInjection`. If the app already registers an `HttpClient`, construct the store with that client and pass the instance to `WithStore` as shown above. `WithStore` is available when the DI integration package is installed; otherwise, construct `Translator` directly with the store.

## Browser applications

The files are downloaded to the browser and are visible to users. Use this store when translations can be public. The filesystem mode of `JsonStore` is not supported in a browser; use embedded resources with `JsonStore` or use `HttpJsonStore` to retrieve files from the host.

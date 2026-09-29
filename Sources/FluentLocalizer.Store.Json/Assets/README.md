# FluentLocalizer JSON stores

`FluentLocalizer.Store.Json` contains three `ITranslationStore` implementations for JSON translation files: `JsonFileStore`, `EmbeddedJsonStore`, and `HttpJsonStore`. They share culture fallback, custom file mappings, nested-key lookup, and JSON parsing behavior.

## Install

```bash
dotnet add package FluentLocalizer.Store.Json
```

Install `FluentLocalizer.Extensions.DependencyInjection` separately when you want `IServiceCollection` integration.

## JSON format and keys

Keep translation files as readable JSON. Nested object properties are addressed with colon-separated keys. The filesystem store reads each needed document on demand and caches its parsed contents until a file change invalidates the cache or the store is disposed.

```json
{
  "Welcome": "Hello {name}!",
  "Notifications": {
    "MessageCount": "You have {quantity} unread messages."
  }
}
```

The `Notifications:MessageCount` key resolves to the nested value. FluentLocalizer then formats the returned template using the requested culture.

`JsonFileStore` and `HttpJsonStore` support these layouts for either a short culture (`it`) or a regional culture (`it-IT`):

| Layout | Example | Key |
|---|---|---|
| Combined file | `it-IT.json` or `it-IT/it-IT.json` | `Notifications:MessageCount` |
| Culture then namespace | `it-IT.common.json` | `common:Notifications:MessageCount` |
| Namespace then culture | `common.it-IT.json` | `common:Notifications:MessageCount` |
| Culture folder | `it-IT/common.json` | `common:Notifications:MessageCount` |

Namespace files contain the nested section directly; combined files contain the namespace as a top-level JSON object. For a key, the mapped file is tried first, followed by culture/namespace files, the combined file and then the configured fallback culture.

An exact culture wins. A regional request such as `it-IT` may use a short `it` catalog. A short request such as `en` uses its own catalog when available; otherwise it selects the alphabetically first regional variant in the catalog, such as `en-GB` before `en-US`. A missing regional variant does not silently use a sibling: if only `en-US` exists, requesting `en-GB` throws `FileNotFoundException`. When no catalog for the requested language exists, the configured fallback culture is still used. `FileMappings` lets a culture use a custom relative JSON path.

## Local files

Configure the consuming application to copy its JSON files to the output and publish directories. The package does not add build rules to the application project:

```xml
<ItemGroup>
  <Content Include="Locales\**\*.json"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

Relative `ResourcesPath` values use the application base directory. The store scans that directory recursively for `*.json` files. `JsonFileStore` requires filesystem access and is not supported in browser applications; use `EmbeddedJsonStore` or `HttpJsonStore` in the browser.

```text
Locales/
  en-US.json
  it-IT.json
  it-IT.common.json
  it/
    common.json
```

```csharp
using FluentLocalizer;
using FluentLocalizer.Store.Json;

using var store = new JsonFileStore(new JsonFileStoreOptions
{
    ResourcesPath = "Locales",
    FallbackCulture = "en-US",
    ReloadOnChange = true,
    ThrowOnMissingStore = true
});

var translator = new Translator(store);
var greeting = translator.Get("Welcome").WithCulture("it-IT").WithArg("name", "Ada").Resolve();
```

`ReloadOnChange` watches filesystem files and is disabled by default. A file event invalidates only that document; a watcher error triggers a new scan and full cache invalidation. The store rejects symbolic links and reparse points in the configured resource directory and matching file paths; keep the directory writable only by trusted processes because a concurrent link replacement cannot be ruled out by a path check. Dispose the store when finished to release the watcher.

## Embedded resources

Embed the locale files in the application assembly. The default resource folder filter is `Locales`; set `ResourceAssembly` when the files are in another assembly.

```xml
<ItemGroup>
  <EmbeddedResource Include="Locales\**\*.json" />
</ItemGroup>
```

The store selects manifest resource names containing the `ResourcesPath` folder (default `Locales`) and recognizes culture files by their final filename, such as `en-US.json`.

```csharp
using var store = new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
{
    ResourceAssembly = typeof(Program).Assembly,
    ResourcesPath = "Locales",
    FallbackCulture = "en-US"
});
```

Embedded resources work in browser applications because they are read from the assembly rather than the filesystem.

## HTTP

Publish the locale files as static assets on the server, such as under `wwwroot/locales` in an ASP.NET Core or Blazor WebAssembly app. `HttpJsonStore` requests `{ResourcesPath}/{culture}.json` below `HttpClient.BaseAddress` and keeps parsed documents in memory. It does not own the supplied `HttpClient`.

```csharp
using FluentLocalizer;
using FluentLocalizer.Store.Json;

var httpClient = new HttpClient { BaseAddress = new Uri("https://example.com/") };
using var store = new HttpJsonStore(httpClient, new HttpJsonStoreOptions
{
    ResourcesPath = "locales",
    FallbackCulture = "en-US",
    ThrowOnMissingStore = true
});

await store.LoadAsync(["it-IT", "en-US"]); // preload for synchronous Resolve()
var translator = new Translator(store);
var message = await translator.Get("Welcome").WithCulture("it-IT").ResolveAsync();
await store.RefreshStoreAsync();
```

`ResolveAsync()` loads the required files on demand and stops downloading when it finds the key. To let HTTP discover available regional variants and preload every namespace for synchronous `Resolve()`, publish a `manifest.json` next to the locale files. It is a JSON array of relative paths:

```json
["en-US.json", "en-US.common.json", "checkout.en-US.json", "it-IT/common.json"]
```

The manifest is optional for existing servers. Without it, the store probes conventional filenames directly, and `LoadAsync()` preloads combined files only. In that mode a short culture cannot discover an arbitrary regional variant, and namespace-only files must first be loaded by `ResolveAsync()`. With a manifest, `LoadAsync()` preloads every listed file for the requested cultures and their fallback; subsequent synchronous lookups require no network. Manifest paths are validated and cannot escape `ResourcesPath`.

`RefreshStoreAsync()` rechecks the manifest and files already loaded, including newly listed files. It sends `Cache-Control: no-cache` and, when supplied by the server, `If-None-Match`/`If-Modified-Since`; a `304 Not Modified` keeps the existing parsed document. The refreshed manifest and all loaded cultures are published together only after every response parses successfully.

Keep source files as ordinary JSON. Configure gzip or Brotli `Content-Encoding` on the server or CDN to reduce transfer size. On desktop/server .NET, configure the caller-owned `HttpClientHandler.AutomaticDecompression` for the desired encodings; on WebAssembly, configure compression in the web server/browser path, since that handler property is not supported in the browser. The store receives decoded JSON from the HTTP stack. The store does not refresh on a timer; call `RefreshStoreAsync()` when the application needs newer values, and use normal HTTP cache headers for the deployment's freshness policy. A preload only covers files chosen by the application and does not make the first browser visit work offline.

The repository's Blazor WebAssembly sample preloads English and Italian at startup. Its `+` and `−` buttons update the plural count in component state, so the localized message changes on each click; the language selector changes the rendered locale.

## Shared settings

All three store options inherit `JsonStoreSettings`:

- `FallbackCulture` selects the fallback culture; defaults to `en-US`.
- `FileMappings` maps culture names to custom JSON file names.
- Mapping values must be relative `.json` paths inside `ResourcesPath`; traversal and absolute paths are rejected. HTTP `ResourcesPath` must be a relative URL path.
- `ThrowOnMissingStore` makes missing translation files throw. When disabled, file and embedded-resource stores skip individual read or parse errors. The HTTP store always throws for failed requests and invalid JSON; when missing files are allowed, lookups with no value return `null`.
- `MaxDocumentBytes` limits each JSON file and HTTP response, including `manifest.json`. The default `0` disables the limit; configure a positive value when catalog files are not fully trusted.

The stores are separate classes so filesystem watching, assembly resource selection, and asynchronous HTTP loading remain explicit. They share the same fallback and JSON key resolution.

Treat translation JSON as deployment data. The stores return text; applications should render it through their UI framework's normal text escaping, not insert it as raw HTML. Large or hostile JSON can still consume substantial memory and formatting time, so deployments accepting untrusted catalogs should enforce response-size budgets at the server or HTTP client boundary.

## Migration from the separate HTTP package

Replace the `FluentLocalizer.Store.Http` package reference with `FluentLocalizer.Store.Json`, and change `using FluentLocalizer.Store.Http;` to `using FluentLocalizer.Store.Json;`. The `HttpJsonStore` and `HttpJsonStoreOptions` types keep their names. `JsonStore` and `JsonStoreOptions` are deprecated; migrate to `JsonFileStore` or `EmbeddedJsonStore` and their corresponding options types.

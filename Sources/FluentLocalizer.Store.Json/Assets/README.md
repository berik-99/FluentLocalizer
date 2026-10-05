# FluentLocalizer JSON stores

`FluentLocalizer.Store.Json` contains three `ITranslationStore` implementations for JSON translation files: `JsonFileStore`, `EmbeddedJsonStore`, and `HttpJsonStore`. They share the JSON format, nested keys, culture fallback and file mappings; [Organizing JSON files](#organizing-json-files) explains how to structure the catalogs and where the stores differ.

## Install

For every public type and member in this package, see the [API reference](#api-reference).

```bash
dotnet add package FluentLocalizer.Store.Json
```

Install `FluentLocalizer.Extensions.DependencyInjection` separately when you want `IServiceCollection` integration.

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

## Organizing JSON files

### Keys and nesting

A key is a path through nested JSON objects, with `:` between segments. Each string value is a template written in the ICU message syntax:

```json
{
  "Welcome": "Hello {name}!",
  "Checkout": {
    "Title": "Your cart",
    "Items": "{quantity, plural, one {# item} other {# items}}"
  }
}
```

`Welcome`, `Checkout:Title` and `Checkout:Items` resolve to the three strings above; nesting can go as deep as you need. The store returns the raw template, and FluentLocalizer formats it with the culture of the request.

- Always nest objects. The key is split at every `:`, so a property literally named `"Checkout:Title"` is never found.
- Property names are case-sensitive: `checkout:title` does not match `Checkout:Title`. Spaces around segments are ignored.
- Only string values are templates. A key that reaches a number, boolean, array, `null` or object is treated as missing.
- `.` has no special meaning: `"Home.Title"` is a single property, reached by the key `Home.Title`.
- Save files as UTF-8.

### Namespaces

The first segment of a key with at least two segments is its *namespace*: in `common:Buttons:Save` the namespace is `common`. A namespace can live in either of two places, and the key stays the same:

1. As a top-level object in the culture file, here `it-IT.json`:

   ```json
   { "common": { "Buttons": { "Save": "Salva" } } }
   ```

2. In a namespace file of its own, here `it-IT.common.json`. Omit the namespace object and start from the next segment:

   ```json
   { "Buttons": { "Save": "Salva" } }
   ```

Namespace files split a large catalog by feature or team, and let `HttpJsonStore` download only the namespaces a page uses. Moving `common` from `it-IT.json` to `it-IT.common.json` requires no code change. Because every multi-segment key has a namespace, `Checkout:Title` can likewise move to `it-IT.Checkout.json` as `{ "Title": "Il tuo carrello" }`.

`JsonFileStore` and `HttpJsonStore` recognize these file names, shown for `it-IT` and identical for a neutral culture such as `it`:

| File | Example | Contains |
|---|---|---|
| Culture file | `it-IT.json` or `it-IT/it-IT.json` | Every namespace as a top-level object |
| Culture, then namespace | `it-IT.common.json` | The `common` namespace only |
| Namespace, then culture | `common.it-IT.json` | The `common` namespace only |
| Culture folder | `it-IT/common.json` | The `common` namespace only |

Culture folders are one level deep; files in deeper subfolders are not recognized. `EmbeddedJsonStore` reads only the culture file (`it-IT.json` or `it.json`): keep namespaces as top-level objects when you embed the catalogs.

### Lookup order and fallback

For `common:Buttons:Save` requested in `it-IT`, `JsonFileStore` tries these sources and returns the first string it finds:

1. the file mapped to the culture in `FileMappings`, if any;
2. the `common` namespace files for `it-IT`, then `it-IT.json`;
3. the same files for the neutral culture `it`;
4. the same files for `FallbackCulture` (default `en-US`) and its neutral culture.

Fallback works per key, not per file: a key missing from `it-IT.common.json` is still looked for in `it-IT.json`, in the `it` files and in the fallback culture. Regional and namespace files only need the strings that differ. `HttpJsonStore` follows the same order, restricted to the files listed in `manifest.json` when one is published. `EmbeddedJsonStore` tries the mapped file, `it-IT.json`, `it.json` and then the fallback culture's files.

### Recommended layout

Name the main catalog of each language after its neutral culture, and add regional files only for the strings that differ:

```text
Locales/
  en.json            complete catalog, also the fallback
  en-GB.json         only British differences
  it.json            Italian for it-IT, it-CH, it...
  it.checkout.json   optional namespace file (JsonFileStore and HttpJsonStore)
```

```csharp
new JsonFileStoreOptions { ResourcesPath = "Locales", FallbackCulture = "en" }
```

With neutral names every store resolves `it-IT`, `it-CH` and `it` to `it.json`, `en-GB` to `en-GB.json` and then `en.json`, and any other language to `en.json`.

Regional-only names behave differently from store to store:

| Request | Available files | `JsonFileStore`, `HttpJsonStore` with manifest | `EmbeddedJsonStore`, `HttpJsonStore` without manifest |
|---|---|---|---|
| `it` | `it-IT.json` | Uses `it-IT.json`, the alphabetically first regional variant | Skips to `FallbackCulture` |
| `it-CH` | `it-IT.json` | Throws `FileNotFoundException`: a sibling region is never used | Skips to `FallbackCulture` |
| `en-GB` | `en-US.json`, the fallback | Throws `FileNotFoundException` | Embedded uses the fallback; HTTP throws `FileNotFoundException` |

To serve a sibling region from an existing file, map it explicitly. This works in every store:

```csharp
var options = new JsonFileStoreOptions();
options.FileMappings["en-GB"] = "en-US.json";
options.FileMappings["it-CH"] = "it-IT.json";
```

Mapping values are relative `.json` paths inside `ResourcesPath`.

## Local files

Configure the consuming application to copy its JSON files to the output and publish directories. The package does not add build rules to the application project:

```xml
<ItemGroup>
  <Content Include="Locales\**\*.json"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

Relative `ResourcesPath` values use the application base directory. The store indexes the `*.json` files in that directory and its subfolders, reads each file the first time a lookup needs it and keeps the parsed document in memory. `JsonFileStore` requires filesystem access and is not supported in browser applications; use `EmbeddedJsonStore` or `HttpJsonStore` in the browser.

```text
Locales/
  en.json
  it.json
  it.common.json
  it-CH/
    common.json
```

```csharp
using FluentLocalizer;
using FluentLocalizer.Store.Json;

using var store = new JsonFileStore(new JsonFileStoreOptions
{
    ResourcesPath = "Locales",
    FallbackCulture = "en",
    ReloadOnChange = true,
    ThrowOnMissingStore = true
});

var translator = new Translator(store);
var greeting = translator.Get("Welcome").WithArg("name", "Ada").Resolve(); // Uses CurrentUICulture.
```

`ReloadOnChange` watches filesystem files and is disabled by default. A file event invalidates only that document; a watcher error triggers a new scan and full cache invalidation. The store rejects symbolic links and reparse points in the configured resource directory and matching file paths; keep the directory writable only by trusted processes because a concurrent link replacement cannot be ruled out by a path check. Dispose the store when finished to release the watcher.

## Embedded resources

Embed the locale files in the application assembly. The default resource folder filter is `Locales`; set `ResourceAssembly` when the files are in another assembly.

```xml
<ItemGroup>
  <EmbeddedResource Include="Locales\**\*.json" />
</ItemGroup>
```

The store loads every `.json` resource whose manifest name contains the `ResourcesPath` folder (default `Locales`) when it is constructed, and identifies each culture file by its final name, such as `en-US.json` or `it.json`. It reads one file per culture: namespace files are not supported, so keep every namespace as a top-level object of the culture file. A neutral request such as `it` does not select `it-IT.json`; name the files after the neutral culture, as in the [recommended layout](#recommended-layout). Resolved templates are kept in a bounded cache of 512 entries.

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

Publish the locale files as static assets on the server, such as under `wwwroot/locales` in an ASP.NET Core or Blazor WebAssembly app. `HttpJsonStore` requests the files described in [Organizing JSON files](#organizing-json-files) from `{ResourcesPath}/` below `HttpClient.BaseAddress`, such as `locales/it-IT.json` or `locales/it-IT.common.json`, and keeps parsed documents in memory. It does not own the supplied `HttpClient`.

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
var message = await translator.Get("Welcome").ResolveAsync();
await store.RefreshStoreAsync();
```

`ResolveAsync()` loads the required files on demand and stops downloading when it finds the key. To let HTTP discover available regional variants and preload every namespace for synchronous `Resolve()`, publish a `manifest.json` next to the locale files. It is a JSON array of relative paths:

```json
["en-US.json", "en-US.common.json", "checkout.en-US.json", "it-IT/common.json"]
```

The manifest is optional for existing servers. Without it, the store probes conventional filenames directly: every candidate name of the lookup order is a separate request, and absent files cost a `404` response each. `LoadAsync()` then preloads culture files only. In that mode a short culture cannot discover an arbitrary regional variant, and namespace-only files must first be loaded by `ResolveAsync()`. With a manifest, `LoadAsync()` preloads every listed file for the requested cultures and their fallback; subsequent synchronous lookups require no network. Manifest paths are validated and cannot escape `ResourcesPath`.

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

<!-- api-reference:start -->
## API reference

Public types and members declared by FluentLocalizer, grouped by package and namespace. Includes constructors, overloads, properties, enum values, inherited library members and deprecated APIs. Standard inherited .NET members are omitted.

Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. Every type states the version that introduced it; later additions, changes and deprecations are marked on each member. The searchable [WebUI API reference](https://berik-99.github.io/FluentLocalizer/docs/api) uses the same catalog and can show any earlier version.

Documented version: `1.0.0`

- [JsonStoreSettings](#api-jsonstoresettings)
- [JsonFileStoreOptions](#api-jsonfilestoreoptions)
- [EmbeddedJsonStoreOptions](#api-embeddedjsonstoreoptions)
- [HttpJsonStoreOptions](#api-httpjsonstoreoptions)
- [JsonTranslationStoreBase](#api-jsontranslationstorebase)
- [JsonFileStore](#api-jsonfilestore)
- [EmbeddedJsonStore](#api-embeddedjsonstore)
- [HttpJsonStore](#api-httpjsonstore)
- [JsonStore](#api-jsonstore)
- [JsonStoreOptions](#api-jsonstoreoptions)
- [JsonStoreLocation](#api-jsonstorelocation)

<a id="api-jsonstoresettings"></a>

### JsonStoreSettings

Kind: `class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Since: `0.1.0-alpha`

Common options inherited by every JSON store options type.

Configure options before creating the store. The store-specific options inherit every property listed here.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonStoreSettings()` | Creates shared settings with the defaults below. |
| `string FallbackCulture { get; set; }` | Default: en-US. The final fallback culture after the requested culture and its neutral form. Must be a valid .NET culture name. |
| `bool ThrowOnMissingStore { get; set; }` | Default: false. Missing translation files may throw FileNotFoundException when true. When false, filesystem and embedded stores skip individual I/O or JSON parse errors; HTTP request failures and invalid HTTP JSON still throw. |
| `long MaxDocumentBytes { get; set; }` | Default: 0 (unlimited). Positive values limit one JSON document's size in bytes. Dedicated store constructors reject negative values with ArgumentOutOfRangeException. Oversized documents may be skipped by file/embedded stores when ThrowOnMissingStore is false; HTTP always rejects them. |
| `IDictionary<string, string> FileMappings { get; }` | Mutable, initially empty, case-insensitive mapping from culture names to JSON file names. Add entries through the returned dictionary; the property cannot be replaced. File and HTTP paths are validated as relative JSON paths. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStoreSettings.cs)

<a id="api-jsonfilestoreoptions"></a>

### JsonFileStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

Filesystem options, inheriting FallbackCulture, ThrowOnMissingStore, MaxDocumentBytes and FileMappings from JsonStoreSettings.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonFileStoreOptions()` | Creates filesystem options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: Locales. Directory scanned recursively for JSON files. Relative paths are resolved against AppContext.BaseDirectory; absolute directory paths are supported. |
| `bool ReloadOnChange { get; set; }` | Default: false. Enables a FileSystemWatcher that invalidates cached translations when files change. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonFileStoreOptions.cs)

<a id="api-embeddedjsonstoreoptions"></a>

### EmbeddedJsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

Embedded-resource options, inheriting all JsonStoreSettings properties.

| Member | Parameters, return value and behavior |
| --- | --- |
| `EmbeddedJsonStoreOptions()` | Creates embedded-resource options with all shared defaults. |
| `Assembly? ResourceAssembly { get; set; }` | Default: null. Assembly containing the embedded JSON files. When null, uses the entry assembly, then the executing assembly if no entry assembly exists. |
| `string ResourcesPath { get; set; }` | Default: Locales. Folder portion of manifest resource names used to select embedded JSON files; not a filesystem path. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/EmbeddedJsonStoreOptions.cs)

<a id="api-httpjsonstoreoptions"></a>

### HttpJsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

HTTP options, inheriting all JsonStoreSettings properties.

| Member | Parameters, return value and behavior |
| --- | --- |
| `HttpJsonStoreOptions()` | Creates HTTP options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: locales. Relative URL path under HttpClient.BaseAddress serving JSON catalogs and the optional manifest.json. Absolute URLs, traversal and query/fragment components are rejected. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/HttpJsonStoreOptions.cs)

<a id="api-jsontranslationstorebase"></a>

### JsonTranslationStoreBase

Kind: `abstract class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore` · Since: `0.1.0-alpha`

Abstract ITranslationStore implementation with cached JSON lookup, nested keys and culture fallback.

Inherited by JsonFileStore and EmbeddedJsonStore. Its constructor JsonTranslationStoreBase(JsonStoreSettings options) is used by derived stores. Protected extension points are Options, ResolveCandidates(CultureInfo), SetDocument(string, string), SetDocument(string, JsonElement), RemoveDocument(string), HasCandidate(CultureInfo), FindTemplate(string, CultureInfo) and ClearTemplateCache().

| Member | Parameters, return value and behavior |
| --- | --- |
| `virtual string? GetTemplate(string key, CultureInfo culture)` | Returns a raw template from cached documents using nested colon-separated key and culture fallback, or null for a missing key. Throws FileNotFoundException when no candidate store exists and ThrowOnMissingStore is true. |
| `virtual Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Returns Task.FromResult(GetTemplate(key, culture)). File and embedded stores inherit this synchronous implementation; it performs no asynchronous I/O and does not inspect cancellationToken. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonTranslationStoreBase.cs)

<a id="api-jsonfilestore"></a>

### JsonFileStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonTranslationStoreBase`, `IDisposable` · Since: `0.1.0-alpha`

Filesystem JSON store implementing ITranslationStore and IDisposable, inheriting JsonTranslationStoreBase.

Supports combined culture files and namespace files, regional/neutral culture selection and optional reload. Parsed files are cached per file until a change invalidates them. Unsupported in browser applications. Copy catalog files to build and publish output.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonFileStore(JsonFileStoreOptions? options = null)` | options defaults to a new JsonFileStoreOptions. Indexes the resources directory and starts a watcher if requested. Throws PlatformNotSupportedException in browsers, ArgumentOutOfRangeException for a negative size limit, and FileNotFoundException for a missing directory/catalog when configured to throw. |
| `override string? GetTemplate(string key, CultureInfo culture)` | Loads needed files on demand and returns the raw template or null. Supports nested keys and namespace-qualified keys such as common:Home:Welcome, requested/neutral/fallback cultures and file mappings. File, path and JSON errors may propagate with ThrowOnMissingStore. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Inherited from JsonTranslationStoreBase. Wraps synchronous GetTemplate in a completed task; cancellationToken is not inspected. |
| `void Dispose()` | Disposes the optional filesystem watcher. Use a using statement for stores owned by the application. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonFileStore.cs)

<a id="api-embeddedjsonstore"></a>

### EmbeddedJsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonTranslationStoreBase` · Since: `0.1.0-alpha`

Embedded JSON store inheriting JsonTranslationStoreBase; usable in browser applications.

Loads matching assembly resources during construction. Reads one combined file per culture, such as it-IT.json or it.json; namespace files are not supported, so keep namespaces as top-level objects. Resolved templates use a bounded cache of 512 entries. Lookup methods are inherited; no Dispose method is required.

| Member | Parameters, return value and behavior |
| --- | --- |
| `EmbeddedJsonStore(EmbeddedJsonStoreOptions? options = null)` | options defaults to a new EmbeddedJsonStoreOptions. Loads matching embedded .json resources into memory. Throws ArgumentOutOfRangeException for a negative size limit. Missing resources throw FileNotFoundException when configured; read/parse errors are skipped when ThrowOnMissingStore is false. |
| `string? GetTemplate(string key, CultureInfo culture)` | Inherited from JsonTranslationStoreBase. Returns the raw nested-key template using culture fallback, or null. May throw FileNotFoundException when no candidate store exists and ThrowOnMissingStore is true. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Inherited from JsonTranslationStoreBase. Returns a completed task from the synchronous lookup; cancellationToken is not inspected. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/EmbeddedJsonStore.cs)

<a id="api-httpjsonstore"></a>

### HttpJsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore`, `IDisposable` · Since: `0.1.0-alpha`

HTTP JSON store implementing ITranslationStore and IDisposable, with preload, lazy loading and refresh.

Supports an optional manifest.json for culture/namespace discovery. The caller owns the supplied HttpClient. HTTP failures, invalid JSON and cancellation propagate independently of ThrowOnMissingStore.

| Member | Parameters, return value and behavior |
| --- | --- |
| `HttpJsonStore(HttpClient httpClient, HttpJsonStoreOptions? options = null)` | httpClient serves catalogs from its base address; options defaults to a new HttpJsonStoreOptions. Does not fetch files during construction. Throws ArgumentNullException for a null client, ArgumentOutOfRangeException for a negative size limit, and ArgumentException for invalid resource paths or mappings. |
| `Task LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)` | Preloads culture and the configured fallback, including namespace files listed in the manifest. Returns when loading completes. culture cannot be null; cancellationToken cancels network/loading operations. Missing required files may throw FileNotFoundException. |
| `Task LoadAsync(IEnumerable<string> cultures, CancellationToken cancellationToken = default)` | Preloads distinct culture names and the configured fallback. cultures cannot be null; invalid names throw CultureNotFoundException. Includes manifest namespace files. Returns when loading completes and allows cancellation. |
| `string? GetTemplate(string key, CultureInfo culture)` | Returns a raw template from already loaded files, with culture fallback, or null. Throws InvalidOperationException when required culture catalogs were not preloaded. A namespace absent from loaded documents returns null; use LoadAsync with a manifest or GetTemplateAsync to load it. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Loads the required culture, fallback and key-specific namespace on demand, then returns the raw template or null. Null key/culture are rejected. Pass cancellationToken to cancel loading. Missing required files may throw FileNotFoundException. |
| `Task RefreshStoreAsync(CancellationToken cancellationToken = default)` | Revalidates the manifest and loaded cultures using ETag/Last-Modified when available, then atomically replaces cached snapshots. A failed refresh preserves the previous snapshot. Returns on completion; cancellation and HTTP/JSON failures propagate. |
| `void Dispose()` | Disposes the store's locks and semaphores. Does not dispose the supplied HttpClient. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/HttpJsonStore.cs)

<a id="api-jsonstore"></a>

### JsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore`, `IDisposable` · Since: `0.0.1`

> **Deprecated in 0.1.0-alpha. Use `JsonFileStore` or `EmbeddedJsonStore` instead.**

Deprecated compatibility wrapper for the original mode-based store.

The wrapper forwards to the selected store. It copies fallback, mappings, resource and reload settings, but currently does not copy MaxDocumentBytes.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonStore(JsonStoreOptions? options = null)` | Creates a wrapper using options or new JsonStoreOptions. SearchMode selects filesystem or embedded resources. Unsupported enum values throw InvalidOperationException; filesystem mode is unsupported in browsers. |
| `string? GetTemplate(string key, CultureInfo culture)` | Forwards key and culture to the selected store. Returns its raw template or null; propagates its errors. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Forwards to the selected store. File and embedded implementations wrap synchronous lookup and do not inspect cancellationToken. |
| `void Dispose()` | Disposes the underlying filesystem store's watcher when filesystem mode is selected. |

Changes:

- 0.1.0-alpha: Reimplemented as a compatibility wrapper over JsonFileStore or EmbeddedJsonStore and marked obsolete. Uses dedicated-store loading and validation; filesystem mode is rejected in browsers.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStore.cs)

<a id="api-jsonstoreoptions"></a>

### JsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.0.1`

> **Deprecated in 0.1.0-alpha. Use `JsonFileStoreOptions` or `EmbeddedJsonStoreOptions` or `HttpJsonStoreOptions` instead.**

Deprecated options for JsonStore, inheriting all JsonStoreSettings properties.

JsonStore currently ignores inherited MaxDocumentBytes.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonStoreOptions()` | Creates compatibility options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: Locales. Filesystem directory or embedded-resource folder depending on SearchMode. |
| `JsonStoreLocation SearchMode { get; set; }` | Default: FileSystem. Selects the compatibility store's backend. |
| `Assembly? ResourceAssembly { get; set; }` | Default: null. Assembly containing catalogs when SearchMode is EmbeddedResources. |
| `bool ReloadOnChange { get; set; }` | Default: false. Enables file watching in FileSystem mode. |

Changes:

- 0.1.0-alpha: Became sealed, inherited JsonStoreSettings and was marked obsolete. ThrowOnError was replaced by inherited ThrowOnMissingStore; FallbackCulture and FileMappings moved to the base class, which also adds MaxDocumentBytes.

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStoreOptions.cs)

<a id="api-jsonstorelocation"></a>

### JsonStoreLocation

Kind: `enum` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Since: `0.0.1`

Compatibility enum used by deprecated JsonStoreOptions.SearchMode.

| Member | Parameters, return value and behavior |
| --- | --- |
| `FileSystem` | Filesystem backend; default. Unsupported in browser applications. |
| `EmbeddedResources` | Embedded-resource backend. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Store.Json/JsonStoreOptions.cs)

<!-- api-reference:end -->

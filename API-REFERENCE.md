# FluentLocalizer API reference

[README](README.md) · [Documentation](DOCUMENTATION.md) · [Changelog](CHANGELOG.md)

<!-- api-reference:start -->
Public types and members declared by FluentLocalizer, grouped by package and namespace. Includes constructors, overloads, properties, enum values, inherited library members and deprecated APIs. Standard inherited .NET members are omitted.

Version numbers shown for a type or member refer to the NuGet package named on that type.

Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. Every type states the version that introduced it; later additions and deprecations are marked on each member, and the changes between versions are listed in the changelog. The searchable WebUI API reference ([Core](https://berik-99.github.io/FluentLocalizer/docs/core/v1/api), [JSON store](https://berik-99.github.io/FluentLocalizer/docs/store-json/v1/api)) uses the same catalog and can show any earlier version; the [changelog](https://berik-99.github.io/FluentLocalizer/changelog) lists the changes of every release.

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
- [FluentLocalizerServiceCollectionExtensions](#api-fluentlocalizerservicecollectionextensions)
- [FluentLocalizerBuilder](#api-fluentlocalizerbuilder)

<a id="api-itranslator"></a>

## ITranslator

Kind: `interface` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Contract for creating translation requests by key.

Implemented by Translator. Each Get call creates a separate mutable builder.

| Member | Parameters, return value and behavior |
| --- | --- |
| `TranslationBuilder Get(string key)` | key is the translation identifier. Returns a new builder; Translator captures CurrentUICulture at this call. A null key throws ArgumentNullException in Translator; empty keys are forwarded to the store. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Interfaces/ITranslator.cs)

<a id="api-translator"></a>

## Translator

Kind: `class` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Inherits: `ITranslator` · Since: `0.0.1`

Default ITranslator implementation, combining a store, translation policies and optional logging.

The translator can be reused after a culture change. Create a new builder for each translation; existing builders retain their captured culture.

| Member | Parameters, return value and behavior |
| --- | --- |
| `Translator(ITranslationStore store, TranslationOptions? options = null, ITranslationLogger? logger = null)` | store supplies templates and must not be null. options defaults to a new TranslationOptions; logger defaults to no logging. Stores the supplied references. Throws ArgumentNullException for a null store. |
| `TranslationBuilder Get(string key)` | Returns a new builder for key using the configured store, options and logger. Captures CurrentUICulture for lookup and ICU message formatting. Throws ArgumentNullException for a null key. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Translator.cs)

<a id="api-translationbuilder"></a>

## TranslationBuilder

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

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/TranslationBuilder.cs)

<a id="api-translationoptions"></a>

## TranslationOptions

Kind: `sealed class` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Policies for missing translations, formatting failures and default arguments.

Fallback strings replace {key} and {0} with the key, and {culture} and {1} with the requested culture name. The builder's default arguments include gender = unspecified and quantity = null.

| Member | Parameters, return value and behavior |
| --- | --- |
| `TranslationOptions()` | Creates mutable options with the defaults listed below. |
| `MissingTranslationBehavior MissingKeyBehavior { get; set; }` | Default: ReturnPlaceholder. Selects placeholder, configured value or exception when the template is missing. |
| `string MissingKeyFallbackValue { get; set; }` | Default: [{key}]. Used with ReturnConfiguredValue. ReturnPlaceholder always produces [key] independently of this property. |
| `Func<string, CultureInfo?, Exception>? MissingKeyExceptionFactory { get; set; }` | Default: null. Optional factory receiving key and culture when missing-key behavior is ThrowException. It must return a non-null exception. Its returned exception is thrown; factory errors propagate. |
| `FormattingErrorBehavior FormattingErrorBehavior { get; set; }` | Default: ReturnPlaceholder. Selects the configured formatting fallback or an exception when a template cannot be parsed or formatted. |
| `string FormattingErrorFallbackValue { get; set; }` | Default: [Format Error]. Returned with ReturnPlaceholder; supports the fallback tokens described above. |
| `Func<string, CultureInfo?, Exception>? FormattingErrorExceptionFactory { get; set; }` | Default: null. Factory receiving key and culture with ThrowException. A null result falls back to CreateMissingKeyException; errors thrown by the factory propagate. |
| `IReadOnlyDictionary<string, object?>? DefaultArguments { get; set; }` | Default: null. Arguments merged into each translation. Runtime values replace matching names without regard to case. |
| `IReadOnlyDictionary<string, object?> CreateArgumentSet(IReadOnlyDictionary<string, object?>? runtimeArguments)` | Returns a new case-insensitive dictionary merging DefaultArguments and runtimeArguments. Runtime values win; null inputs are allowed and neither input dictionary is modified. |
| `Exception CreateMissingKeyException(string key, CultureInfo? culture)` | Returns the configured factory result or a TranslationException for key and culture. Direct calls do not throw the returned exception. Rejects null, empty or whitespace keys with ArgumentException (ArgumentNullException for null on modern targets); factory failures propagate. |
| `Exception CreateFormattingException(string key, CultureInfo? culture, Exception? innerException)` | Returns the configured factory result, or a TranslationException whose message includes innerException.Message when available. The default exception does not attach innerException as InnerException. A null factory result uses CreateMissingKeyException. Key validation and factory failures match CreateMissingKeyException. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/TranslationOptions.cs)

<a id="api-itranslationstore"></a>

## ITranslationStore

Kind: `interface` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Storage contract returning raw templates before formatting.

Implementations own their fallback rules. A null result or KeyNotFoundException is treated as a missing translation by TranslationBuilder. Other failures propagate.

| Member | Parameters, return value and behavior |
| --- | --- |
| `string? GetTemplate(string key, CultureInfo culture)` | key identifies the template; culture selects the translation. Returns the unformatted template or null. Must complete synchronously; cannot rely on asynchronous loading. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Asynchronous counterpart returning the raw template or null. cancellationToken allows stores performing asynchronous I/O to cancel the operation. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Interfaces/ITranslationStore.cs)

<a id="api-translationexception"></a>

## TranslationException

Kind: `class` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Inherits: `Exception` · Since: `0.0.1`

Exception carrying the translation key and requested culture. Inherits System.Exception.

Standard Exception members, including Message and InnerException, are inherited from .NET.

| Member | Parameters, return value and behavior |
| --- | --- |
| `TranslationException(string key, CultureInfo? culture, string message, Exception? innerException = null)` | Creates an exception with the supplied key, optional culture, message and optional innerException. Does not validate the key. |
| `string Key { get; }` | The translation key supplied to the constructor. |
| `CultureInfo? Culture { get; }` | The requested culture supplied to the constructor; may be null. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Exceptions/TranslationException.cs)

<a id="api-gender"></a>

## Gender

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Enum values accepted by Genderize.

| Member | Parameters, return value and behavior |
| --- | --- |
| `Unspecified` | No explicit gender; supplies unspecified to the template. The default builder value. |
| `Male` | Supplies male to a select expression. |
| `Female` | Supplies female to a select expression. |
| `Neuter` | Supplies neuter to a select expression. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/Gender.cs)

<a id="api-lettercase"></a>

## LetterCase

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

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/LetterCase.cs)

<a id="api-missingtranslationbehavior"></a>

## MissingTranslationBehavior

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Enum controlling missing-key resolution.

| Member | Parameters, return value and behavior |
| --- | --- |
| `ReturnPlaceholder` | Returns [key]; default behavior. |
| `ThrowException` | Throws the configured missing-key exception or TranslationException. |
| `ReturnConfiguredValue` | Returns MissingKeyFallbackValue after token replacement. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/MissingTranslationBehavior.cs)

<a id="api-formattingerrorbehavior"></a>

## FormattingErrorBehavior

Kind: `enum` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer` · Since: `0.0.1`

Enum controlling template parsing and formatting failures.

| Member | Parameters, return value and behavior |
| --- | --- |
| `ReturnPlaceholder` | Returns FormattingErrorFallbackValue after token replacement; default behavior. |
| `ThrowException` | Throws the configured formatting exception or TranslationException. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Enums/FormattingErrorBehavior.cs)

<a id="api-itranslationlogger"></a>

## ITranslationLogger

Kind: `interface` · Package: `FluentLocalizer` · Namespace: `FluentLocalizer.Logging` · Since: `0.0.1`

Optional contract for translation diagnostics.

| Member | Parameters, return value and behavior |
| --- | --- |
| `void Log(TranslationLogLevel level, string message, Exception? exception = null)` | Writes message at level, with an optional exception. Implement this interface and pass it to Translator, or use the dependency injection logger adapter. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Logging/ITranslationLogger.cs)

<a id="api-translationloglevel"></a>

## TranslationLogLevel

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

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Core/Logging/TranslationLogLevel.cs)

<a id="api-jsonstoresettings"></a>

## JsonStoreSettings

Kind: `class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Since: `0.1.0-alpha`

Common options inherited by every JSON store options type.

Configure options before creating the store. The store-specific options inherit every property listed here.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonStoreSettings()` | Creates shared settings with the defaults below. |
| `string FallbackCulture { get; set; }` | Default: en-US. The final fallback culture after the requested culture and its neutral form. Must be a valid .NET culture name. |
| `bool ThrowOnMissingStore { get; set; }` | Default: false. Missing translation files may throw FileNotFoundException when true. When false, filesystem and embedded stores skip individual I/O or JSON parse errors; HTTP request failures and invalid HTTP JSON still throw. |
| `long MaxDocumentBytes { get; set; }` | Default: 0 (unlimited). Positive values limit one JSON document's size in bytes. Dedicated store constructors reject negative values with ArgumentOutOfRangeException. Oversized documents always throw, whatever ThrowOnMissingStore says: InvalidDataException in file and embedded stores, HttpRequestException in HttpJsonStore. |
| `IDictionary<string, string> FileMappings { get; }` | Mutable, initially empty, case-insensitive mapping from culture names to JSON file names. Add entries through the returned dictionary; the property cannot be replaced. File and HTTP paths are validated as relative JSON paths. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/JsonStoreSettings.cs)

<a id="api-jsonfilestoreoptions"></a>

## JsonFileStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

Filesystem options, inheriting FallbackCulture, ThrowOnMissingStore, MaxDocumentBytes and FileMappings from JsonStoreSettings.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonFileStoreOptions()` | Creates filesystem options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: Locales. Directory scanned recursively for JSON files. Relative paths are resolved against AppContext.BaseDirectory; absolute directory paths are supported. |
| `bool ReloadOnChange { get; set; }` | Default: false. Enables a FileSystemWatcher that invalidates cached translations when files change. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/JsonFileStoreOptions.cs)

<a id="api-embeddedjsonstoreoptions"></a>

## EmbeddedJsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

Embedded-resource options, inheriting all JsonStoreSettings properties.

| Member | Parameters, return value and behavior |
| --- | --- |
| `EmbeddedJsonStoreOptions()` | Creates embedded-resource options with all shared defaults. |
| `Assembly? ResourceAssembly { get; set; }` | Default: null. Assembly containing the embedded JSON files. When null, uses the entry assembly, then the executing assembly if no entry assembly exists. |
| `string ResourcesPath { get; set; }` | Default: Locales. Folder portion of manifest resource names used to select embedded JSON files; not a filesystem path. |
| `bool LoadOnDemand { get; set; }` | *Since 1.1.0.* Default: false. When true, the constructor only lists the matching resources and checks MaxDocumentBytes; each file is read and parsed on the first lookup that needs it, so cultures that are never requested are not kept in memory. Invalid JSON is then reported by that lookup instead of the constructor. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/EmbeddedJsonStoreOptions.cs)

<a id="api-httpjsonstoreoptions"></a>

## HttpJsonStoreOptions

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonStoreSettings` · Since: `0.1.0-alpha`

HTTP options, inheriting all JsonStoreSettings properties.

| Member | Parameters, return value and behavior |
| --- | --- |
| `HttpJsonStoreOptions()` | Creates HTTP options with all shared defaults. |
| `string ResourcesPath { get; set; }` | Default: locales. Relative URL path under HttpClient.BaseAddress serving JSON catalogs and the optional manifest.json. Absolute URLs, traversal and query/fragment components are rejected. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/HttpJsonStoreOptions.cs)

<a id="api-jsontranslationstorebase"></a>

## JsonTranslationStoreBase

Kind: `abstract class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore` · Since: `0.1.0-alpha`

Abstract ITranslationStore implementation with cached JSON lookup, nested keys and culture fallback.

Inherited by JsonFileStore and EmbeddedJsonStore. Its constructor JsonTranslationStoreBase(JsonStoreSettings options) is used by derived stores. Protected extension points are Options, ResolveCandidates(CultureInfo), SetDocument(string, string), SetDocument(string, JsonElement), RemoveDocument(string), HasCandidate(CultureInfo), FindTemplate(string, CultureInfo) and ClearTemplateCache().

| Member | Parameters, return value and behavior |
| --- | --- |
| `virtual string? GetTemplate(string key, CultureInfo culture)` | Returns a raw template from cached documents using nested colon-separated key and culture fallback, or null for a missing key. Throws FileNotFoundException when no candidate store exists and ThrowOnMissingStore is true. |
| `virtual Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Returns Task.FromResult(GetTemplate(key, culture)). File and embedded stores inherit this synchronous implementation; it performs no asynchronous I/O and does not inspect cancellationToken. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/JsonTranslationStoreBase.cs)

<a id="api-jsonfilestore"></a>

## JsonFileStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonTranslationStoreBase`, `IDisposable` · Since: `0.1.0-alpha`

Filesystem JSON store implementing ITranslationStore and IDisposable, inheriting JsonTranslationStoreBase.

Supports combined culture files and namespace files, regional/neutral culture selection and optional reload. Parsed files are cached per file until a change invalidates them. Unsupported in browser applications. Copy catalog files to build and publish output.

| Member | Parameters, return value and behavior |
| --- | --- |
| `JsonFileStore(JsonFileStoreOptions? options = null)` | options defaults to a new JsonFileStoreOptions. Indexes the resources directory and starts a watcher if requested. Throws PlatformNotSupportedException in browsers, ArgumentOutOfRangeException for a negative size limit, and FileNotFoundException for a missing directory/catalog when configured to throw. With ThrowOnMissingStore, every file is parsed during construction, so invalid JSON throws JsonException there. |
| `override string? GetTemplate(string key, CultureInfo culture)` | Loads needed files on demand and returns the raw template or null. Supports nested keys and namespace-qualified keys such as common:Home:Welcome, requested/neutral/fallback cultures and file mappings. I/O and JSON errors are skipped unless ThrowOnMissingStore is true. Always throws InvalidDataException for a file larger than MaxDocumentBytes, SecurityException for symbolic links or paths outside ResourcesPath, and FileNotFoundException when only a sibling regional catalog exists. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Inherited from JsonTranslationStoreBase. Wraps synchronous GetTemplate in a completed task; cancellationToken is not inspected. |
| `void Dispose()` | Disposes the optional filesystem watcher. Use a using statement for stores owned by the application. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/JsonFileStore.cs)

<a id="api-embeddedjsonstore"></a>

## EmbeddedJsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `JsonTranslationStoreBase` · Since: `0.1.0-alpha`

Embedded JSON store inheriting JsonTranslationStoreBase; usable in browser applications.

Loads matching assembly resources during construction, or on first use when LoadOnDemand is true. JSON is parsed straight from the resource stream. Reads the culture files, such as it-IT.json or it.json, and namespace files named culture first, such as it-IT.common.json or it/common.json, trying a namespace file before the culture file of the same culture. FileMappings values are final file names such as it-IT.json. Files named like common.it-IT.json become MSBuild satellite resources and are not read. Resolved templates use a bounded cache of 512 entries. Lookup methods are inherited; no Dispose method is required.

| Member | Parameters, return value and behavior |
| --- | --- |
| `EmbeddedJsonStore(EmbeddedJsonStoreOptions? options = null)` | options defaults to a new EmbeddedJsonStoreOptions. Loads matching embedded .json resources into memory unless LoadOnDemand is true. Throws ArgumentOutOfRangeException for a negative size limit. Missing resources throw FileNotFoundException when configured; read/parse errors are skipped when ThrowOnMissingStore is false. A resource larger than MaxDocumentBytes always throws InvalidDataException, also when LoadOnDemand is true. |
| `string? GetTemplate(string key, CultureInfo culture)` | Inherited from JsonTranslationStoreBase. Returns the raw nested-key template using culture fallback, or null. May throw FileNotFoundException when no candidate store exists and ThrowOnMissingStore is true. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Inherited from JsonTranslationStoreBase. Returns a completed task from the synchronous lookup; cancellationToken is not inspected. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/EmbeddedJsonStore.cs)

<a id="api-httpjsonstore"></a>

## HttpJsonStore

Kind: `sealed class` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Inherits: `ITranslationStore`, `IDisposable` · Since: `0.1.0-alpha`

HTTP JSON store implementing ITranslationStore and IDisposable, with preload, lazy loading and refresh.

Supports an optional manifest.json for culture/namespace discovery. The caller owns the supplied HttpClient. HTTP failures, invalid JSON and cancellation propagate independently of ThrowOnMissingStore.

| Member | Parameters, return value and behavior |
| --- | --- |
| `HttpJsonStore(HttpClient httpClient, HttpJsonStoreOptions? options = null)` | httpClient serves catalogs from its base address; options defaults to a new HttpJsonStoreOptions. Does not fetch files during construction. Throws ArgumentNullException for a null client, ArgumentOutOfRangeException for a negative size limit, and ArgumentException for invalid resource paths or mappings. |
| `Task LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)` | Preloads culture and the configured fallback, including namespace files listed in the manifest; with a manifest, the files listed for each culture's neutral culture are loaded too, so synchronous lookups follow the same fallback order as GetTemplateAsync. Returns when loading completes. culture cannot be null; cancellationToken cancels network/loading operations. Missing required files may throw FileNotFoundException. |
| `Task LoadAsync(IEnumerable<string> cultures, CancellationToken cancellationToken = default)` | Preloads distinct culture names and the configured fallback. cultures cannot be null; invalid names throw CultureNotFoundException. Includes manifest namespace files and, with a manifest, the files listed for each culture's neutral culture. Returns when loading completes and allows cancellation. |
| `string? GetTemplate(string key, CultureInfo culture)` | Returns a raw template from already loaded files, with culture fallback, or null. Throws InvalidOperationException when required culture catalogs were not preloaded. A namespace absent from loaded documents returns null; use LoadAsync with a manifest or GetTemplateAsync to load it. |
| `Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)` | Loads the required culture, fallback and key-specific namespace on demand, then returns the raw template or null. Null key/culture are rejected. Pass cancellationToken to cancel loading. Missing required files may throw FileNotFoundException. |
| `Task RefreshStoreAsync(CancellationToken cancellationToken = default)` | Revalidates the manifest and loaded cultures using ETag/Last-Modified when available, then atomically replaces cached snapshots. A failed refresh preserves the previous snapshot. Returns on completion; cancellation and HTTP/JSON failures propagate. |
| `void Dispose()` | Disposes the store's locks and semaphores. Does not dispose the supplied HttpClient. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/HttpJsonStore.cs)

<a id="api-jsonstore"></a>

## JsonStore

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

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/JsonStore.cs)

<a id="api-jsonstoreoptions"></a>

## JsonStoreOptions

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

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/JsonStoreOptions.cs)

<a id="api-jsonstorelocation"></a>

## JsonStoreLocation

Kind: `enum` · Package: `FluentLocalizer.Store.Json` · Namespace: `FluentLocalizer.Store.Json` · Since: `0.0.1`

Compatibility enum used by deprecated JsonStoreOptions.SearchMode.

| Member | Parameters, return value and behavior |
| --- | --- |
| `FileSystem` | Filesystem backend; default. Unsupported in browser applications. |
| `EmbeddedResources` | Embedded-resource backend. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/store-json-v1.1.0/Sources/FluentLocalizer.Store.Json/JsonStoreOptions.cs)

<a id="api-fluentlocalizerservicecollectionextensions"></a>

## FluentLocalizerServiceCollectionExtensions

Kind: `static class` · Package: `FluentLocalizer.Extensions.DependencyInjection` · Namespace: `Microsoft.Extensions.DependencyInjection` · Since: `0.0.1`

Static extension methods registering FluentLocalizer in IServiceCollection.

Registers ITranslator as a singleton. Register ITranslationStore before resolving the translator. WithLogger is optional; without a registered ITranslationLogger, translation logging is disabled.

| Member | Parameters, return value and behavior |
| --- | --- |
| `static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, Action<TranslationOptions>? configure = null)` | services is the DI collection and cannot be null (ArgumentNullException). configure optionally configures IOptions<TranslationOptions>. Returns a FluentLocalizerBuilder for store/logger registration. |
| `static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, TranslationOptions translationOptions)` | Registers the supplied translationOptions instance through IOptions and adds the translator. Returns a builder. Null services or translationOptions throw ArgumentNullException. Use a typed argument when passing null to avoid overload ambiguity. |

[Source](https://github.com/berik-99/FluentLocalizer/blob/v1.0.0/Sources/FluentLocalizer.Extensions.DependencyInjection/FluentLocalizerServiceCollectionExtensions.cs)

<a id="api-fluentlocalizerbuilder"></a>

## FluentLocalizerBuilder

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

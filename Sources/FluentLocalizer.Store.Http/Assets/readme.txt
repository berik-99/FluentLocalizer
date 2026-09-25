FluentLocalizer.Store.Http downloads culture-specific JSON translation files over HTTP and caches parsed files in memory. It implements ITranslationStore and can be used directly or registered through the separate dependency-injection package.

Files are expected under the configured relative resources path, for example:

wwwroot/locales/en-US.json
wwwroot/locales/it-IT.json

Configure HttpClient.BaseAddress to the application's base URI and use HttpJsonStoreOptions.ResourcesPath to select the directory.

Load cultures with LoadAsync during startup before using synchronous Resolve(). Alternatively use ResolveAsync() to load a culture on first use. Call RefreshStoreAsync to revalidate and refresh the cultures loaded by the current store instance. A new store instance starts with an empty cache.

ResourcesPath is relative to HttpClient.BaseAddress. JSON object properties can be addressed with colon-separated keys, such as 'Menu:Save'. FallbackCulture and FileMappings control fallback file selection. Set ThrowOnMissingStore to true to throw FileNotFoundException when none of the candidate files exists.

The store does not own or dispose the supplied HttpClient. Dispose the store when finished to release its cached JSON documents.

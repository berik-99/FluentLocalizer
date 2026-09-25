FluentLocalizer.Store.Http downloads culture-specific JSON translation files over HTTP and caches them in memory.

Files are expected under the configured relative resources path, for example:

wwwroot/locales/en-US.json
wwwroot/locales/it-IT.json

Configure HttpClient.BaseAddress to the application's base URI and use HttpJsonStoreOptions.ResourcesPath to select the directory.

Load cultures with LoadAsync during startup for synchronous translation resolution. Call RefreshStoreAsync to revalidate and refresh the cultures loaded by the current store instance.

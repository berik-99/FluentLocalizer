FluentLocalizer.Store.Json provides JSON translation stores for local files, embedded assembly resources, and HTTP endpoints.

Install with 'dotnet add package FluentLocalizer.Store.Json'. Use JsonFileStore with JsonFileStoreOptions for disk files, EmbeddedJsonStore with EmbeddedJsonStoreOptions for assembly resources, or HttpJsonStore with HttpJsonStoreOptions for remote files.

The three stores share FallbackCulture, FileMappings, ThrowOnMissingStore, culture fallback resolution, and colon-separated nested JSON keys. JsonFileStore can watch files with ReloadOnChange. HttpJsonStore supports LoadAsync, ResolveAsync, and RefreshStoreAsync. An optional manifest.json lists culture and namespace files for complete preload and short-to-regional culture selection; refresh uses HTTP validators when available. Without a manifest, asynchronous namespaced lookups probe conventional names and LoadAsync preloads combined culture files. HTTP request failures and invalid HTTP JSON always throw, regardless of ThrowOnMissingStore. File mappings must be relative JSON paths inside ResourcesPath.

See the package README for setup, examples, and configuration details.

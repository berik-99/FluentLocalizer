FluentLocalizer.Store.Json provides JSON translation stores for local files, embedded assembly resources, and HTTP endpoints.

Install with 'dotnet add package FluentLocalizer.Store.Json'. Use JsonFileStore with JsonFileStoreOptions for disk files, EmbeddedJsonStore with EmbeddedJsonStoreOptions for assembly resources, or HttpJsonStore with HttpJsonStoreOptions for remote files.

The three stores share FallbackCulture, FileMappings, ThrowOnMissingStore, culture fallback resolution, and colon-separated nested JSON keys. JsonFileStore can watch files with ReloadOnChange. HttpJsonStore supports LoadAsync, ResolveAsync, and RefreshStoreAsync; synchronous Resolve requires cultures to be preloaded. HTTP request failures and invalid HTTP JSON always throw, regardless of ThrowOnMissingStore.

See the package README for setup, examples, and configuration details.

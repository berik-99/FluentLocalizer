FluentLocalizer.Store.Json

Install with:
dotnet add package FluentLocalizer.Store.Json

Choose a store based on where the JSON files live:
- JsonFileStore reads files from the application filesystem. Copy Locales/**/*.json to the output and publish directories.
- EmbeddedJsonStore reads JSON resources embedded in an assembly.
- HttpJsonStore downloads JSON from a server and supports asynchronous loading and refresh.

All stores support culture fallback, nested colon-separated keys, and file mappings. JsonFileStore can watch files when ReloadOnChange is enabled. HttpJsonStore can preload a manifest.json for complete synchronous lookup.

See README.md in this package for setup examples and store-specific behavior.

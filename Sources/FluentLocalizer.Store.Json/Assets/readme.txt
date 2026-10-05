FluentLocalizer.Store.Json

Install with:
dotnet add package FluentLocalizer.Store.Json

Choose a store based on where the JSON files live:
- JsonFileStore reads files from the application filesystem. Copy Locales/**/*.json to the output and publish directories.
- EmbeddedJsonStore reads JSON resources embedded in an assembly.
- HttpJsonStore downloads JSON from a server and supports asynchronous loading and refresh.

All stores support culture fallback, nested colon-separated keys, and file mappings. JsonFileStore can watch files when ReloadOnChange is enabled. HttpJsonStore can preload a manifest.json for complete synchronous lookup.

WithCulture(...) is optional. Get(key) captures CultureInfo.CurrentUICulture when the builder is created. WithCulture(...) overrides it for that translation only. Set CurrentUICulture before Get(...) to change the default, and also CurrentCulture to align standard .NET number/date formatting. Existing builders keep their culture; other UI components may need a rerender. See README.md for execution-context and thread-default details.

See README.md in this package for setup examples and store-specific behavior.

The API reference section in README.md lists every public type and member in this package, including constructors, overloads, options and enum values. Search all packages at https://berik-99.github.io/FluentLocalizer/docs/api.

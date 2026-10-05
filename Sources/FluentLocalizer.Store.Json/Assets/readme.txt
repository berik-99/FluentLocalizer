FluentLocalizer.Store.Json

Install with:
dotnet add package FluentLocalizer.Store.Json

Choose a store based on where the JSON files live:
- JsonFileStore reads files from the application filesystem. Copy Locales/**/*.json to the output and publish directories.
- EmbeddedJsonStore reads JSON resources embedded in an assembly.
- HttpJsonStore downloads JSON from a server and supports asynchronous loading and refresh.

All stores support culture fallback, nested colon-separated keys (for example "Checkout:Title" for {"Checkout":{"Title":"..."}}), and file mappings. JsonFileStore and HttpJsonStore also read namespace files such as it-IT.common.json; EmbeddedJsonStore reads one file per culture. Name the main catalog after the neutral culture (it.json, en.json) and add regional files (en-GB.json) only for differences. JsonFileStore can watch files when ReloadOnChange is enabled. HttpJsonStore can preload a manifest.json for complete synchronous lookup.

You normally do not need WithCulture(...). Get(key) uses CultureInfo.CurrentUICulture, captured when the builder is created. WithCulture(...) only forces a different culture for one translation, such as an email in the recipient's language. Set CurrentUICulture before Get(...) to change the default, and also CurrentCulture to align standard .NET number/date formatting. Existing builders keep their culture; other UI components may need a rerender. See README.md for execution-context and thread-default details.

See README.md in this package for setup examples and store-specific behavior.

The API reference section in README.md lists every public type and member in this package, including constructors, overloads, options and enum values. Search all packages at https://berik-99.github.io/FluentLocalizer/docs/api.

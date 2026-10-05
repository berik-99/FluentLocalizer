FluentLocalizer

The core package provides Translator, ITranslator, ITranslationStore, and the fluent translation API. It does not include a storage provider.

Install this package with:
dotnet add package FluentLocalizer

Provide your own ITranslationStore implementation, or add the JSON stores package:
dotnet add package FluentLocalizer.Store.Json

Example:
translator.Get("Welcome").WithArg("name", "Ada").Resolve()

Templates use the ICU message syntax (https://unicode-org.github.io/icu/userguide/format_parse/messages/). They are parsed and formatted by MessageFormat by Jeff Hansen (https://github.com/jeffijoe/messageformat.net), MIT License.

You normally do not need WithCulture(...). Get(key) uses CultureInfo.CurrentUICulture, captured when the builder is created. WithCulture(...) only forces a different culture for one translation, such as an email in the recipient's language. Set CurrentUICulture before Get(...) to change the default, and also CurrentCulture to align standard .NET number/date formatting. Existing builders keep their culture; other UI components may need a rerender. See README.md for execution-context and thread-default details.

See README.md in this package for the full guide and examples.

The API reference section in README.md lists every public type and member in this package, including constructors, overloads, options and enum values. Search all packages at https://berik-99.github.io/FluentLocalizer/docs/api.

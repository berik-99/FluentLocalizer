FluentLocalizer

The core package provides Translator, ITranslator, ITranslationStore, and the fluent translation API. It does not include a storage provider.

Install this package with:
dotnet add package FluentLocalizer

Provide your own ITranslationStore implementation, or add the JSON stores package:
dotnet add package FluentLocalizer.Store.Json

Example:
translator.Get("Welcome").WithArg("name", "Ada").Resolve()

See README.md in this package for the full guide and examples.

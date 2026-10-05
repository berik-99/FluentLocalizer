FluentLocalizer.Extensions.DependencyInjection

This package registers FluentLocalizer with Microsoft.Extensions.DependencyInjection. Register a translation store before resolving ITranslator.

Install with:
dotnet add package FluentLocalizer.Extensions.DependencyInjection

For JSON translation files, also install:
dotnet add package FluentLocalizer.Store.Json

Example:
services.AddFluentLocalizer()
    .WithStore(new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = "Locales" }));

You normally do not need WithCulture(...). Get(key) uses CultureInfo.CurrentUICulture, captured when the builder is created. WithCulture(...) only forces a different culture for one translation, such as an email in the recipient's language. Set CurrentUICulture before Get(...) to change the default, and also CurrentCulture to align standard .NET number/date formatting. Existing builders keep their culture; other UI components may need a rerender. See README.md for execution-context and thread-default details.

See README.md in this package for hosted-service, factory, options, and logging examples.

The API reference section in README.md lists every public type and member in this package, including constructors, overloads, options and enum values. Search all packages at https://berik-99.github.io/FluentLocalizer/docs/api.

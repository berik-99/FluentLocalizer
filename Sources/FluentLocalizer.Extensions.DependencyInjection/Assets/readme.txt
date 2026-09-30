FluentLocalizer.Extensions.DependencyInjection

This package registers FluentLocalizer with Microsoft.Extensions.DependencyInjection. Register a translation store before resolving ITranslator.

Install with:
dotnet add package FluentLocalizer.Extensions.DependencyInjection

For JSON translation files, also install:
dotnet add package FluentLocalizer.Store.Json

Example:
services.AddFluentLocalizer()
    .WithStore(new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = "Locales" }));

See README.md in this package for hosted-service, factory, options, and logging examples.

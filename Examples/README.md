# Runnable examples

| Project | Store | Basic use | Advanced use |
|---|---|---|---|
| ConsoleApp | Custom `ITranslationStore` (`MemoryStore`) | Current culture and synchronous `Resolve()` | Async resolution, arguments, plural and gender rules, default arguments, culture fallback, missing keys, casing, formatting errors |
| WorkerApp | `JsonFileStore` | Dependency injection and a nested key from a local JSON file | Culture fallback, namespace bundle, plural and gender formatting, live reload via `FileSystemWatcher` |
| BlazorWebApp | `HttpJsonStore` | Startup `LoadAsync()` followed by synchronous rendering | Manifest-based preload, lazy namespace loading with `ResolveAsync()`, culture fallback, short/regional locale selection, plural updates, explicit refresh |

Run the console and worker with:

```bash
dotnet run --project Examples/FluentLocalizer.Samples.ConsoleApp
dotnet run --project Examples/FluentLocalizer.Samples.WorkerApp
```

Run the Blazor server project with:

```bash
dotnet run --project Examples/FluentLocalizer.Samples.BlazorWebApp/FluentLocalizer.Samples.BlazorWebApp
```

In the worker, edit `Locales/it-IT.common.json` in the build output while it runs to see live reload. In Blazor, `wwwroot/locales/manifest.json` lists the catalogs and namespace files preloaded at startup; click **Load namespace asynchronously** to fetch a separate `*.common.json` file. The refresh button requests the loaded files again from the server. The locale-resolution buttons show manifest selection in both directions: requesting `en-US` finds `en.json`, while requesting `en` finds `en-US.json`. Each case uses a separate manifest so only the intended catalog is available.

# FluentLocalizer.Store.Json

FluentLocalizer.Store.Json is a JSON-backed implementation of `ITranslationStore` for FluentLocalizer. It loads translation templates from `.json` files, resolves them by culture, and can fall back to a default culture when a specific translation is missing.

The message templates used by FluentLocalizer are based on ICU / MessageFormat concepts. ICU is the Unicode standard that defines how messages are formatted across languages, including plural rules, gender selection, and locale-aware numbers and dates. For the official reference, see https://unicode-org.github.io/icu/. For example, the same template can render `1 item` in English and `1 elemento` in Italian when the plural rules are applied according to the active culture.

## Install

```bash
dotnet add package FluentLocalizer.Store.Json
```

## How it works

`JsonStore` reads one or more JSON files and exposes them to `FluentLocalizer.Core` through the `ITranslationStore` contract. A key is resolved by splitting it on `:` and walking the JSON object hierarchy, so a structure like this:

```json
{
  "Welcome": "Hello {name}!",
  "Notifications": {
	"MessageCount": "You have {quantity} unread messages."
  }
}
```

can be resolved with keys such as `Welcome` or `Notifications:MessageCount`.

## Example file layout

A typical layout for filesystem-based translations looks like this:

```text
Locales/
  en-US.json
  it-IT.json
```

Example `it-IT.json`:

```json
{
  "Welcome": "Ciao {name}!",
  "Notifications": {
	"MessageCount": "Hai {quantity} messaggi non letti."
  }
}
```

## Locales folder structure and build output

When you use `FluentLocalizer.Store.Json`, place your translation files in a `Locales` folder at the root of your project. The package's MSBuild targets include every `.json` file under that folder in the build output, so the files are copied to the application output folder by default. This makes `JsonStoreLocation.FileSystem` the easiest option for most apps.

If you want the translations to be shipped as embedded resources instead, change your project file to embed them and point the store to the embedded-resource mode:

```xml
<ItemGroup>
  <EmbeddedResource Include="Locales\**\*.json" />
</ItemGroup>
```

```csharp
var options = new JsonStoreOptions
{
	ResourcesPath = "Locales",
	SearchMode = JsonStoreLocation.EmbeddedResources,
	ResourceAssembly = typeof(MyApp.Program).Assembly,
	ThrowOnError = true
};
```

Use `FileSystem` when you want the files to remain on disk and `EmbeddedResources` when you want the translations to be baked into the assembly.

## Example 1: standalone usage in Program.cs

This is the simplest approach when you want to create the store directly in an application entry point.

```csharp
using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;

var options = new JsonStoreOptions
{
	ResourcesPath = "Locales",
	SearchMode = JsonStoreLocation.FileSystem,
	FallbackCulture = "en-US",
	ThrowOnError = true
};

using var store = new JsonStore(options);
var translator = new Translator(store);

var message = translator
	.Get("Welcome")
	.WithArg("name", "Ada")
	.Resolve();

Console.WriteLine(message);
```

In this example:
- `ResourcesPath` points to the folder that contains your JSON files.
- `SearchMode = FileSystem` tells the store to read files from disk.
- `FallbackCulture` ensures that a fallback language is used when the requested culture is missing.
- `ThrowOnError = true` makes missing files or invalid JSON fail fast.

## Culture resolution

The store automatically tries to resolve translations in this order:

1. the requested culture, for example `it-IT`
2. the neutral culture, for example `it`
3. the configured fallback culture, for example `en-US`

If you provide a custom mapping, it is used before the default file naming convention.

```csharp
var options = new JsonStoreOptions
{
	ResourcesPath = "Locales",
	FallbackCulture = "en-US"
};

options.FileMappings["it-IT"] = "italian.json";
```

### Configuration options explained

- `ResourcesPath`: the folder used to locate translation files. It can be relative or absolute.
- `SearchMode`: selects whether files are loaded from the local filesystem (`FileSystem`) or from embedded resources (`EmbeddedResources`).
- `FallbackCulture`: the culture used when a requested culture or its neutral variant cannot be resolved.
- `ReloadOnChange`: when `true`, the store watches the translation folder and reloads files automatically.
- `ThrowOnError`: when `true`, missing files or invalid JSON cause exceptions; when `false`, the store simply returns `null` for missing values.
- `FileMappings`: lets you override the default file naming convention for a specific culture.
- `ResourceAssembly`: used only with `EmbeddedResources` to identify which assembly should be inspected.

## Nested JSON keys

Because the store walks the JSON object hierarchy, you can structure translations in nested objects and access them with dotted-style keys separated by `:`.

```json
{
  "Dashboard": {
	"Title": "Welcome back",
	"Cards": {
	  "Pending": "You have {count} pending tasks"
	}
  }
}
```

You can resolve them as:

```csharp
var title = translator.Get("Dashboard:Title").Resolve();
var pending = translator.Get("Dashboard:Cards:Pending").WithArg("count", 2).Resolve();
```

## Embedded resources

You can also load translations from embedded resources instead of the filesystem:

```csharp
var options = new JsonStoreOptions
{
	SearchMode = JsonStoreLocation.EmbeddedResources,
	ResourceAssembly = typeof(MyApp.Program).Assembly,
	ThrowOnError = true
};
```

## Reload on change

If you want the store to refresh translations when files change on disk, enable reload mode:

```csharp
var options = new JsonStoreOptions
{
	ResourcesPath = "Locales",
	ReloadOnChange = true
};
```

## Error handling

`ThrowOnError` controls whether missing files or invalid JSON should raise exceptions or simply return `null`.

```csharp
var options = new JsonStoreOptions
{
	ResourcesPath = "Locales",
	ThrowOnError = false
};
```

## Notes

FluentLocalizer.Store.Json is designed to be simple and integration-friendly. It focuses on file discovery, culture fallback, and JSON traversal, while the actual formatting and translation pipeline stays in `FluentLocalizer.Core`.

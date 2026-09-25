FluentLocalizer.Store.Json reads translation templates from JSON files under the Locales folder in the application project. The files are copied to the build output by default.

The message templates used by FluentLocalizer are based on ICU / MessageFormat concepts. ICU is the Unicode standard for culture-aware formatting, covering plurals, numbers, dates, and message selection rules. For the official reference, see https://unicode-org.github.io/icu/.

A simple structure is:

Locales/
  en-US.json
  it-IT.json

Example en-US.json:

{
  "Welcome": "Hello {name}!",
  "Notifications": {
    "MessageCount": "You have {quantity, plural, =0 {no messages} one {# message} other {# messages}}."
  }
}

By default, the package targets include every .json file inside Locales in the build output. If you prefer to ship them as embedded resources instead, change your project file to use EmbeddedResource for the JSON files and set 'JsonStoreOptions.SearchMode' to 'JsonStoreLocation.EmbeddedResources'. Set 'ResourceAssembly' to the assembly containing those resources when needed.

Use 'JsonStoreOptions.FallbackCulture' to select the culture tried after the requested culture and its neutral culture. Use 'FileMappings' when a culture file name does not follow the usual culture name convention.

Set 'JsonStoreOptions.ReloadOnChange' to watch filesystem translation files for changes. Set 'ThrowOnMissingStore' to true to surface file discovery and parsing failures; otherwise unavailable or invalid files are skipped and missing lookups return null.

JsonStoreLocation.FileSystem requires filesystem access and is not supported in browser applications. In a browser, use JsonStoreLocation.EmbeddedResources with this store, or use FluentLocalizer.Store.Http to fetch JSON files from a server at runtime.

Example for embedded resources:

<ItemGroup>
  <EmbeddedResource Include="Locales\**\*.json" />
</ItemGroup>

var options = new JsonStoreOptions
{
    ResourcesPath = "Locales",
    SearchMode = JsonStoreLocation.EmbeddedResources,
    ResourceAssembly = typeof(Program).Assembly,
};

The synchronous and asynchronous ITranslationStore methods both read the in-memory cache; this store does not perform asynchronous file I/O.

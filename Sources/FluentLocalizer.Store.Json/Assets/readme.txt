FluentLocalizer.Store.Json looks for translation files in the /Locales folder in the project root.

The message templates used by FluentLocalizer are based on ICU / MessageFormat concepts. ICU is the Unicode standard for culture-aware formatting, covering plurals, numbers, dates, and message selection rules. For the official reference, see https://unicode-org.github.io/icu/.

A simple structure is:

Locales/
  en-US.json
  it-IT.json

Example en-US.json:

{
  "Welcome": "Hello {name}!",
  "Notifications": {
    "MessageCount": "You have {count, plural, =0 {no messages} one {# message} other {# messages}}."
  }
}

By default, the package targets include every .json file inside Locales in the build output. If you prefer to ship them as embedded resources instead, change your project file to use EmbeddedResource for the JSON files and set 'JsonStoreOptions.SearchMode' to 'JsonStoreLocation.EmbeddedResources'.

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

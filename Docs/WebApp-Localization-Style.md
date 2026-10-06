# WebApp localization style

The WebApp catalogs in `Sources/FluentLocalizer.WebApp/Locales` contain the interface copy and documentation guides in English, Italian, French, German and Spanish. Use the same technical meaning in every language, but write each version as a guide for developers in that language.

## Voice

- Explain the action, when it is useful and what the API returns. Prefer concrete descriptions over metaphors such as templates “living” somewhere, catalogs “answering”, or placeholders being “gaps”.
- Address the reader directly: English “you”, Italian “tu”, French “vous”, German “du”, Spanish “tú”. Keep a conversational tone with precise technical vocabulary.
- Define unfamiliar concepts at their first use. Afterwards, use the same term throughout the interface and guides.
- Keep the useful local terminology. An English source term does not require an English word in every translation, and a technical term does not always need translating.
- Qualify behavioral claims. For example, the default translation policies return placeholders for missing keys and formatting errors; other store errors can still propagate to the caller.

## Terminology for these catalogs

| Concept | English | Italian | French | German | Spanish |
| --- | --- | --- | --- | --- | --- |
| Message template | template | template | modèle | Vorlage | plantilla |
| Named substitution in a template | placeholder | placeholder (segnaposto at first use) | espace réservé | Platzhalter | marcador de posición |
| Value supplied by the application | argument | argomento | argument | Argument | argumento |
| Translation identifier | key | chiave | clé | Schlüssel | clave |
| .NET culture | culture | cultura | culture | Kultur | cultura |
| Translation source component | store | store | store | Store | store |
| Builder pattern | fluent builder | builder fluent | builder à API fluide | Fluent Builder | builder con API fluida |
| Culture fallback | fallback | fallback | repli | Fallback | fallback; explain as a search in an alternative culture |
| Default language for fallback | fallback language | lingua di fallback | langue de repli | Fallback-Sprache | idioma de reserva |
| Compiled-in resources | embedded resources | risorse embedded | ressources incorporées | eingebettete Ressourcen | recursos incrustados |
| Execution time | runtime | runtime | à l’exécution | zur Laufzeit | en tiempo de ejecución |
| Logging | logging | logging | journalisation | Logging | registro de eventos |
| DI integration | dependency injection | dependency injection | injection de dépendances | Dependency Injection | inyección de dependencias |
| Resource ownership period | lifetime | ciclo di vita; lifetime in the DI table | durée de vie | Lebensdauer | ciclo de vida |
| Releasing owned resources | dispose | dispose / rilascio delle risorse | libération des ressources | Ressourcenfreigabe | liberación de recursos |

“Culture” refers to the .NET `CultureInfo` concept, which includes language and formatting conventions; avoid reducing it to language alone when discussing formatting. A regional variant of the same language is not necessarily a geographically neighboring region. Describe it explicitly instead of using “sibling region”, “regione sorella”, “région voisine”, “Nachbarregion” or “región hermana”.

Keep `Translator`, `TranslationBuilder` and `ITranslationStore` unchanged when referring to those API types. A translator as a service is distinct from the person translating a catalog. A placeholder is part of a template; an argument is the value passed by the application. Calling `Dispose()` releases owned resources rather than deleting an object or its files.

## Content that must remain intact

Do not translate API identifiers, package names, namespaces, ICU keywords (`plural`, `select`, `one`, `other`), argument names (`name`, `quantity`, `gender`), filenames, culture codes or URL destinations. Translate surrounding explanations and link labels normally. Preserve the apostrophe quoting around literal ICU expressions in the catalogs: it prevents the site's formatter from interpreting documentation examples as active substitutions.

The shared API reference in `Docs/api-reference.json` remains the English technical catalog used by the WebApp and package READMEs. Its localized navigation and explanatory interface text belong to the language catalogs.

## Verification

Run the existing `WebAppLocalizationTests` in the JSON-store test project. They check matching keys in all five catalogs and resolve every interface and documentation string with the real formatter, with missing-key and formatting policies set to throw. Also compare link destinations and dynamic argument names against the original catalogs. These checks protect functionality; editorial review is still needed for meaning and grammar.

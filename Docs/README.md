# Maintaining the API reference

Edit [api-reference.json](api-reference.json) when a public type or member changes. It contains signatures, descriptions, defaults, return values and relevant exceptions for the three published library packages.

The WebUI embeds this catalog and renders it at `/docs/api`. Generate the root and package README sections from the same catalog:

```powershell
pwsh -File Docs/Update-ApiReference.ps1
pwsh -File Docs/Update-ApiReference.ps1 -Check
```

Keep the generated sections between the `api-reference:start` and `api-reference:end` markers intact. Edit the surrounding guides normally. The reference is in English, matching the package READMEs; WebUI navigation is localized.

`PublicApiReferenceTests` in the JSON store and dependency injection test projects checks the catalog against public types, constructors, overloads, properties and enum values in the compiled libraries. Standard inherited .NET members are omitted. Inherited library methods are listed under concrete stores, shared options under their base class, and deprecated types remain documented for compatibility.

# Maintaining the API reference

Edit [api-reference.json](api-reference.json) when a public type or member changes. It lists the library versions and, for each type, its kind (`class`, `sealed class`, `interface`, `enum`...), direct base types, version history, signatures, descriptions, defaults, return values and relevant exceptions for the three published library packages.

The WebUI embeds this catalog and renders it at `/docs/api`. Generate the root and package README sections from the same catalog:

```powershell
pwsh -File Docs/Update-ApiReference.ps1
pwsh -File Docs/Update-ApiReference.ps1 -Check
```

Keep the generated sections between the `api-reference:start` and `api-reference:end` markers intact. Edit the surrounding guides normally. The reference is in English, matching the package READMEs; WebUI navigation is localized.

## Catalog fields

| Field | Where | Meaning |
| --- | --- | --- |
| `Versions` | root | Every documented version in ascending order: `Version`, optional `Date` (`yyyy-MM-dd`) and `Summary`. A version without `Date` is a preview of the next release. |
| `Since` | type (required), member (optional) | Version that introduced the API. A member without `Since` shares its type's version. |
| `DeprecatedIn` | type, member | Version that marked the API `[Obsolete]`. Required exactly when the code has `[Obsolete]`. |
| `ReplacedBy` | type, member | Suggested replacements, as `Type` or `Type.Member`. Requires `DeprecatedIn`. |
| `RemovedIn` | type, member | Version that removes the API, or a future version that will. Requires `DeprecatedIn`. Keep removed entries in the catalog so older versions stay documented. |
| `Changes` | type, member | Behavior changes after `Since`: `{ "Version": "1.1.0", "Description": "..." }`. |

Enum values show `= N` only when the source declares the value explicitly; the test checks any value written in the catalog.

## Releasing a version

1. Add the version to `Versions` without `Date` while it is in development. The WebUI lists it as a preview; the default view stays on the latest published version.
2. Set `Since` on new types and members, `DeprecatedIn` (and `ReplacedBy`) on APIs marked `[Obsolete]`, `RemovedIn` on removed APIs and add `Changes` entries for behavior changes.
3. When releasing, set `Date`, bump `<Version>` in the library projects and run `Update-ApiReference.ps1`.

The WebUI version selector filters the reference to what existed in that version and summarizes what it added, changed, deprecated and removed. READMEs document the newest version in the catalog.

## Validation

`PublicApiReferenceTests` in the JSON store and dependency injection test projects checks the catalog against the compiled libraries:

- public types, their kind and base types, constructors, overloads, properties and enum values are documented;
- `[Obsolete]` on types and members matches `DeprecatedIn`;
- documented types still exist unless `RemovedIn` says otherwise, and nothing marked as removed still ships;
- versions are valid, ordered and consistent (`Since` ≤ `DeprecatedIn` < `RemovedIn`, members not older than their type), and the library version is listed in `Versions`.

Standard inherited .NET members are omitted. Inherited library methods are listed under concrete stores, shared options under their base class, and deprecated types remain documented for compatibility.

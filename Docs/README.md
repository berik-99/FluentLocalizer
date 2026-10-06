# Maintaining the API reference

Edit [api-reference.json](api-reference.json) when a public type or member changes. It lists the library versions and, for each type, its kind (`class`, `sealed class`, `interface`, `enum`...), direct base types, version history, signatures, descriptions, defaults, return values and relevant exceptions for the three published library packages.

The WebUI embeds this catalog and renders it at `/docs/api`. Generate the root and package README sections from the same catalog:

```powershell
pwsh -File Docs/Update-ApiReference.ps1
pwsh -File Docs/Update-ApiReference.ps1 -Check
```

Keep the generated sections between the `api-reference:start` and `api-reference:end` markers intact. Edit the surrounding guides normally. The reference is in English, matching the package READMEs; WebUI navigation is localized.

For the translated WebApp guides and interface copy, follow [WebApp localization style](WebApp-Localization-Style.md) for tone, terminology and catalog verification.

## Catalog fields

| Field | Where | Meaning |
| --- | --- | --- |
| `Versions` | root | Every documented version in ascending order: `Version`, optional `Date` (`yyyy-MM-dd`), `Summary` and verified `SourceRevision` for implementation links. A version without `Date` is a preview of the next release. |
| `Since` | type (required), member (optional) | Version that introduced the API. A member without `Since` shares its type's version. |
| `DeprecatedIn` | type, member | Version that marked the API `[Obsolete]`. Required exactly when the code has `[Obsolete]`. |
| `ReplacedBy` | type, member | Suggested replacements, as `Type` or `Type.Member`. Requires `DeprecatedIn`. |
| `RemovedIn` | type, member | Version that removes the API, or a future version that will. Requires `DeprecatedIn`. Keep removed entries in the catalog so older versions stay documented. |
| `Changes` | type, member | Behavior changes after `Since`: `{ "Version": "1.1.0", "Description": "..." }`. |
| `EarlierDefinitions` | type, member | Overrides for releases before `Before`, in ascending order. Types may override `Namespace`, `Kind`, `BaseTypes`, `Summary`, `Remarks` or the complete `Members` list. Members may override `Signature` and `Description`. Used for breaking declaration and behavior changes; `Changes` explains the transition. |

Enum values show `= N` only when the source declares the value explicitly; the test checks any value written in the catalog.

## Releasing a version

1. Add the version to `Versions` without `Date` while it is in development. The WebUI lists it as a preview; the default view stays on the latest published version.
2. Set `Since` on new types and members, `DeprecatedIn` (and `ReplacedBy`) on APIs marked `[Obsolete]`, `RemovedIn` on removed APIs and add `Changes` entries for behavior changes.
3. When releasing, set `Date`, bump `<Version>` in the library projects and run `Update-ApiReference.ps1`.

The WebUI version selector filters the reference to what existed in that version and summarizes what it added, changed, deprecated and removed. READMEs document the newest version in the catalog.

`Since` follows the API across documented namespace moves. For example, `ITranslator` was introduced in `0.0.1` as `FluentLocalizer.Core.ITranslator` and moved to `FluentLocalizer.ITranslator` in `0.1.0-alpha`. `EarlierDefinitions` displays the old namespace when selecting `0.0.1`; `Changes` records the breaking move. Implementation links point to the selected release's verified source revision.

## Historical verification

The three packages were first published on `2026-07-30` (`0.0.1`), followed by `2026-09-30` (`0.1.0-alpha`) and `2026-10-02` (`1.0.0`), as verified with NuGet registration metadata. Their published package `.nuspec` repository metadata identifies commit `2c4a8561c3a27456f23304b24e73bac35e1f0066` for `0.0.1` and `02ba523883111ab7d19a2f594f1a479ee06400a1` for the alpha. There is no local `0.0.1` tag, so use the package metadata rather than guessing from later commits that still declared version `0.0.1`.

The original packages export 17 public types. The alpha exports 25, adding the dedicated JSON stores and settings. A source diff from `v0.1.0-alpha` to `v1.0.0` changes only the three project version properties; no public APIs or library behavior changed in that transition.

[Tests/ApiSnapshots/0.0.1.json](../Tests/ApiSnapshots/0.0.1.json) contains normalized declared public signatures reflected from the `net10.0` assemblies of all three published `0.0.1` packages. It is independent of the current catalog and checks the historical WebUI projection, including namespace, inheritance and member presence. Accessor changes and deprecations also have focused assertions. When documenting another historical release, inspect the published package and its verified source revision before adding a snapshot.

## Validation

`PublicApiReferenceTests` in the JSON store and dependency injection test projects checks the catalog against the compiled libraries:

- public types, their kind and base types, constructors, overloads, properties and enum values are documented;
- `[Obsolete]` on types and members matches `DeprecatedIn`;
- documented types still exist unless `RemovedIn` says otherwise, and nothing marked as removed still ships;
- versions are valid, ordered and consistent (`Since` ≤ `DeprecatedIn` < `RemovedIn`, members not older than their type), and the library version is listed in `Versions`.
- historical override boundaries are valid and ordered; the `net10.0` WebApp integration tests compare the initial release against the published-package snapshot and check alpha/stable history and playground isolation.

Standard inherited .NET members are omitted. Inherited library methods are listed under concrete stores, shared options under their base class, and deprecated types remain documented for compatibility.

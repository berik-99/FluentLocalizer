# Maintaining the API reference, changelog and documentation

Edit [api-reference.json](api-reference.json) when a public type or member changes or a version is released. Version numbers shown for a type or member refer to the NuGet package named on that type. The catalog records the release scopes with their versions and release notes and, for each type, its kind (`class`, `sealed class`, `interface`, `enum`...), direct base types, version history, signatures, descriptions, defaults, return values and relevant exceptions.

The WebUI embeds this catalog: it renders the API reference of each documentation set at `/docs/{scope}/v{major}/api` and every release at `/changelog`. Generate the root [API-REFERENCE.md](../API-REFERENCE.md), the API reference sections of the package READMEs and the root [CHANGELOG.md](../CHANGELOG.md) from the same catalog:

```powershell
pwsh -File Docs/Update-ApiReference.ps1
pwsh -File Docs/Update-ApiReference.ps1 -Check
```

Keep the generated sections between the `api-reference:start` and `api-reference:end` markers intact, and do not edit `CHANGELOG.md` by hand. Edit the surrounding guides normally; the root [README.md](../README.md) and [DOCUMENTATION.md](../DOCUMENTATION.md) contain no generated content. The reference and the release notes are in English, matching the package READMEs; WebUI navigation is localized.

For the translated WebApp guides and interface copy, follow [WebApp localization style](WebApp-Localization-Style.md) for tone, terminology and catalog verification.

## Release scopes

Packages are versioned in scopes. Each scope has its own version line, release tag, changelog entries, API reference and documentation sets:

| Scope `Id` | `Name` | Packages | Release tag |
| --- | --- | --- | --- |
| `core` | Core | `FluentLocalizer`, `FluentLocalizer.Extensions.DependencyInjection` | `core-vMAJOR.MINOR.PATCH` |
| `store-json` | JSON store | `FluentLocalizer.Store.Json` | `store-json-vMAJOR.MINOR.PATCH` |

A new store adds a scope named after its release tag prefix, such as `store-sqlite` for `FluentLocalizer.Store.Sqlite`. Every package belongs to exactly one scope, and the versions of a type (`Since`, `DeprecatedIn`, `RemovedIn`, `Changes`) are versions of its scope.

## Catalog fields

| Field | Where | Meaning |
| --- | --- | --- |
| `Scopes` | root | Release scopes: `Id`, display `Name`, `Packages` and `Versions`. |
| `Versions` | scope | Documented versions in ascending order: `Version`, optional `Date` (`yyyy-MM-dd`), `Summary`, verified `SourceRevision` for implementation links and optional `Notes`. A version without `Date` is a preview. |
| `Notes` | version | Release notes for the changelog: `{ "Kind": "Added", "Text": "..." }`, where `Kind` is `Added`, `Changed`, `Deprecated`, `Removed` or `Fixed`. `Text` supports `` `code` ``, `*emphasis*` and `[label](docs/{scope}/v{major}/{page}#{section})` links to the guides. |
| `Since` | type (required), member (optional) | Version that introduced the API. A member without `Since` shares its type's version. |
| `DeprecatedIn` | type, member | Version that marked the API `[Obsolete]`. Required exactly when the code has `[Obsolete]`. |
| `ReplacedBy` | type, member | Suggested replacements, as `Type` or `Type.Member`. Requires `DeprecatedIn`. |
| `RemovedIn` | type, member | Version that removes the API, or a future version that will. Requires `DeprecatedIn`. Keep removed entries in the catalog so older versions stay documented. |
| `Changes` | type, member | Behavior changes after `Since`: `{ "Version": "1.1.0", "Description": "..." }`. |
| `EarlierDefinitions` | type, member | Overrides for releases before `Before`, in ascending order. Types may override `Namespace`, `Kind`, `BaseTypes`, `Summary`, `Remarks` or the complete `Members` list. Members may override `Signature` and `Description`. Used for breaking declaration and behavior changes; `Changes` explains the transition. |

Enum values show `= N` only when the source declares the value explicitly; the test checks any value written in the catalog.

## Changelog

The WebUI changelog and `CHANGELOG.md` list every release of every scope, newest first, and can be filtered by scope (`/changelog?scope=core`). Each release shows its `Summary`, its `Notes` grouped by kind, the public API changes derived from `Since`, `Changes`, `DeprecatedIn` and `RemovedIn`, and links to the documentation set, the API reference of that version and the NuGet packages. Release anchors match the release tags, such as `/changelog#store-json-v1.0.0`.

Write notes for what users need to know: new features, breaking changes, fixes and deprecations, with a link to the guide that explains them. The API reference shows each version as it is, with `Since` and deprecation badges; what changed between versions, including the per-type `Changes` entries, appears only in the changelog.

## Documentation sets

The WebUI documentation is split by scope and versioned by major version. A documentation set is registered in `Sources/FluentLocalizer.WebApp/Pages/Docs/DocsSets.cs` and served under `docs/{scope}/v{major}`:

| Set | Pages | Catalog object |
| --- | --- | --- |
| `core` 1.x | `Pages/Docs/Core/V1`, plus the shared API reference | `Docs:CoreV1` |
| `store-json` 1.x | `Pages/Docs/StoreJson/V1`, plus the shared API reference | `Docs:StoreJsonV1` |
| `core` 0.x, `store-json` 0.x | `Pages/Docs/DocsExperimental.razor`, which explains why the experimental 0.x releases have no guides, plus the shared API reference | `Docs:Experimental` |

The API reference of a set lists the versions of its scope with the same major number. The guides, the API reference and the changelog share a sticky toolbar: scope tabs switch to the other scope at the same major version, and the version menu switches major version (in the API reference, any released version of the scope). Both keep the current page when the other set has it. Sets of an earlier major version show a link to the latest one. The latest set is the newest major version with a published release; `docs/{scope}` and `docs/{scope}/{page}` open it, and addresses from before versioning (such as `docs/templates` or `docs/api#api-jsonfilestore`) redirect to it.

Guide text links to versioned routes, such as `[culture](docs/core/v1/culture)`, so a frozen set keeps pointing at matching pages. Within a major version, keep updating its set and mark additions with the version that introduced them: put `<DocsSince Version="1.1.0" />` before the paragraph, list item or table cell, which renders the localized "Since 1.1.0" badge, and say in the text what earlier versions did when that matters.

The guide text lives in the `Docs` namespace files, `Locales/{language}.Docs.json`, without the `Docs` level; the interface strings stay in `Locales/{language}.json`. The site creates its `EmbeddedJsonStore` with `LoadOnDemand`, so the documentation text is parsed only when a documentation page needs it. Keep keys used outside `Pages/Docs` in the `Ui` object, otherwise every page parses the documentation file.

To start the documentation of a new major version:

1. Copy the latest set's page folder and namespace, for example `Pages/Docs/Core/V1` to `Pages/Docs/Core/V2`.
2. Copy its catalog object in all five `Docs` namespace files (`Sources/FluentLocalizer.WebApp/Locales/{language}.Docs.json`), for example `CoreV1` to `CoreV2` for the keys `Docs:CoreV1:...` and `Docs:CoreV2:...`, and update the links in the copy to `docs/core/v2/...`.
3. Register the new set in `DocsSets.All` with its pages, then edit only the new set. The previous set stays unchanged.
4. Add the new major version to the scope's `Versions` without `Date`. The new set becomes the default when that version gets a `Date`.

## Releasing a version

Release tags select the NuGet packages and their version:

| Tag | Packages released |
| --- | --- |
| `core-vMAJOR.MINOR.PATCH` | `FluentLocalizer` and `FluentLocalizer.Extensions.DependencyInjection` |
| `store-<name>-vMAJOR.MINOR.PATCH` | One `FluentLocalizer.Store.<Name>` package; lowercase its suffix and replace dots with hyphens for `<name>` (for example, `store-json-v1.0.1`) |

CI still builds and tests the full solution. A release tag packs and publishes only its selected package or packages. Keep each library project's `<Version>` at its latest published version: update Core and DI before a `core-v*` tag, or update only the selected store before its `store-*-v*` tag. A store's NuGet dependency on Core uses the Core project's version, so a store-only release does not inherit the store's version as its Core dependency.

1. Add the version to its scope's `Versions` without `Date` while it is in development. The WebUI lists it as a preview; the default view stays on the latest published version.
2. Set `Since` on new types and members, `DeprecatedIn` (and `ReplacedBy`) on APIs marked `[Obsolete]`, `RemovedIn` on removed APIs and add `Changes` entries for behavior changes.
3. Write the release `Summary` and `Notes`.
4. When releasing, set the version's `Date` and `SourceRevision` (the release tag), update the scope's `<Version>` values and run `Update-ApiReference.ps1`.

The WebUI version selector filters the reference to what existed in that version and links to its changelog entry. `API-REFERENCE.md` and the package READMEs document the newest version of each package's scope in the catalog.

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
- every package belongs to one scope; versions are valid, ordered and consistent within their scope (`Since` ≤ `DeprecatedIn` < `RemovedIn`, members not older than their type), and each library version is listed in its scope's `Versions`;
- release notes have a known kind and link only to versioned documentation routes;
- historical override boundaries are valid and ordered; the `net10.0` WebApp integration tests compare the initial release against the published-package snapshot and check alpha/stable history and playground isolation.

Standard inherited .NET members are omitted. Inherited library methods are listed under concrete stores, shared options under their base class, and deprecated types remain documented for compatibility.

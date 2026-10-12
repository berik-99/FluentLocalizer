param([switch]$Check)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'api-reference.json') -Raw | ConvertFrom-Json
$tick = [char]96
$site = 'https://berik-99.github.io/FluentLocalizer/'

function Compare-ApiVersion([string]$Left, [string]$Right) {
    $l = $Left.Split('-', 2); $r = $Right.Split('-', 2)
    $result = ([version]$l[0]).CompareTo([version]$r[0])
    if ($result -ne 0) { return $result }
    if ($l.Count -ne $r.Count) { return $(if ($l.Count -gt $r.Count) { -1 } else { 1 }) }
    if ($l.Count -eq 1) { return 0 }
    return [string]::CompareOrdinal($l[1], $r[1])
}

# Each package belongs to one scope, which owns its version numbers: Core (core and DI) or one store.
$scopeOf = @{}
foreach ($scope in $catalog.Scopes) {
    foreach ($package in $scope.Packages) { $scopeOf[$package] = $scope }
}

# The README documents the newest listed version of the package's scope; APIs removed by then are left out.
function Get-Current($Type) { $scopeOf[$Type.Package].Versions[-1] }

function Test-Available($Entry, [string]$Current) {
    -not $Entry.RemovedIn -or (Compare-ApiVersion $Entry.RemovedIn $Current) -gt 0
}

# The WebUI shows the documentation set of the newest published major version, such as docs/core/v1.
function Get-DocsRoute($Scope) {
    $published = @($Scope.Versions | Where-Object { $_.Date })
    $version = if ($published) { $published[-1].Version } else { $Scope.Versions[-1].Version }
    'docs/' + $Scope.Id + '/v' + $version.Split('.')[0]
}

# "Deprecated in 1.0.0. Use `A` or `B` instead. Removal planned in 2.0.0."
function Get-Lifecycle($Entry) {
    if (-not $Entry.DeprecatedIn) { return $null }
    $text = 'Deprecated in ' + $Entry.DeprecatedIn + '.'
    if ($Entry.ReplacedBy) {
        $text += ' Use ' + (($Entry.ReplacedBy | ForEach-Object { $tick + $_ + $tick }) -join ' or ') + ' instead.'
    }
    if ($Entry.RemovedIn) { $text += ' Removal planned in ' + $Entry.RemovedIn + '.' }
    return $text
}

function Write-Generated([string]$RelativePath, [string]$Updated, [string]$Content) {
    if ($Check) {
        if ($Updated -ne $Content) { throw "Generated documentation is out of date: $RelativePath. Run Docs/Update-ApiReference.ps1." }
    } else {
        [IO.File]::WriteAllText((Join-Path $root $RelativePath), $Updated, [Text.UTF8Encoding]::new($false))
    }
}

# API-REFERENCE.md keeps its title outside the generated block, so its types are second-level headings.
$targets = @(
    @{ Path = 'API-REFERENCE.md'; Package = $null; Heading = ''; TypeHeading = '##' },
    @{ Path = 'Sources/FluentLocalizer.Core/Assets/README.md'; Package = 'FluentLocalizer' },
    @{ Path = 'Sources/FluentLocalizer.Store.Json/Assets/README.md'; Package = 'FluentLocalizer.Store.Json' },
    @{ Path = 'Sources/FluentLocalizer.Extensions.DependencyInjection/Assets/README.md'; Package = 'FluentLocalizer.Extensions.DependencyInjection' }
)

foreach ($target in $targets) {
    $path = Join-Path $root $target.Path
    $scopes = if ($target.Package) { @($scopeOf[$target.Package]) } else { @($catalog.Scopes) }
    if ($scopes.Count -eq 1) {
        $reference = '[WebUI API reference](' + $site + (Get-DocsRoute $scopes[0]) + '/api)'
        $changelog = '[changelog](' + $site + 'changelog?scope=' + $scopes[0].Id + ')'
    } else {
        $reference = 'WebUI API reference (' + (($scopes | ForEach-Object { '[' + $_.Name + '](' + $site + (Get-DocsRoute $_) + '/api)' }) -join ', ') + ')'
        $changelog = '[changelog](' + $site + 'changelog)'
    }
    $heading = if ($null -ne $target.Heading) { $target.Heading } else { '## API reference' }
    $typeHeading = if ($target.TypeHeading) { $target.TypeHeading } else { '###' }
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('<!-- api-reference:start -->')
    if ($heading) {
        $lines.Add($heading)
        $lines.Add('')
    }
    $lines.Add('Public types and members declared by FluentLocalizer, grouped by package and namespace. Includes constructors, overloads, properties, enum values, inherited library members and deprecated APIs. Standard inherited .NET members are omitted.')
    $lines.Add('')
    $lines.Add('Version numbers shown for a type or member refer to the NuGet package named on that type.')
    $lines.Add('')
    $lines.Add('Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. Every type states the version that introduced it; later additions and deprecations are marked on each member, and the changes between versions are listed in the changelog. The searchable ' + $reference + ' uses the same catalog and can show any earlier version; the ' + $changelog + ' lists the changes of every release.')
    $lines.Add('')
    $types = @($catalog.Types | Where-Object { (-not $target.Package -or $_.Package -eq $target.Package) -and (Test-Available $_ (Get-Current $_).Version) })
    foreach ($type in $types) {
        $lines.Add('- [' + $type.Name + '](#api-' + $type.Name.ToLowerInvariant() + ')')
    }
    $lines.Add('')
    foreach ($type in $types) {
        $current = Get-Current $type
        $lines.Add('<a id="api-' + $type.Name.ToLowerInvariant() + '"></a>')
        $lines.Add('')
        $lines.Add($typeHeading + ' ' + $type.Name)
        $lines.Add('')
        $meta = 'Kind: ' + $tick + $type.Kind + $tick + ' · Package: ' + $tick + $type.Package + $tick + ' · Namespace: ' + $tick + $type.Namespace + $tick
        if ($type.BaseTypes.Count -gt 0) {
            $meta += ' · Inherits: ' + (($type.BaseTypes | ForEach-Object { $tick + $_ + $tick }) -join ', ')
        }
        $meta += ' · Since: ' + $tick + $type.Since + $tick
        $lines.Add($meta)
        $lines.Add('')
        $lifecycle = Get-Lifecycle $type
        if ($lifecycle) {
            $lines.Add('> **' + $lifecycle + '**')
            $lines.Add('')
        }
        $lines.Add($type.Summary)
        $lines.Add('')
        if ($type.Remarks) {
            $lines.Add($type.Remarks)
            $lines.Add('')
        }
        $lines.Add('| Member | Parameters, return value and behavior |')
        $lines.Add('| --- | --- |')
        foreach ($member in @($type.Members | Where-Object { Test-Available $_ $current.Version })) {
            $signature = $member.Signature.Replace('|', '&#124;')
            $notes = @()
            if ($member.Since -and $member.Since -ne $type.Since) { $notes += '*Since ' + $member.Since + '.*' }
            $memberLifecycle = Get-Lifecycle $member
            if ($memberLifecycle) { $notes += '**' + $memberLifecycle + '**' }
            $description = (@($notes) + $member.Description) -join ' '
            $lines.Add('| ' + $tick + $signature + $tick + ' | ' + $description.Replace('|', '&#124;') + ' |')
        }
        $lines.Add('')
        $sourceRevision = if ($current.SourceRevision) { $current.SourceRevision } else { 'main' }
        $source = 'https://github.com/berik-99/FluentLocalizer/blob/' + $sourceRevision + '/' + $type.Source
        $lines.Add('[Source](' + $source + ')')
        $lines.Add('')
    }
    $lines.Add('<!-- api-reference:end -->')
    $block = $lines -join "`n"
    $content = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    $pattern = '(?s)<!-- api-reference:start -->.*?<!-- api-reference:end -->'
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) {
        $updated = $content.Substring(0, $match.Index) + $block + $content.Substring($match.Index + $match.Length)
    } else {
        $updated = $content.TrimEnd() + "`n`n" + $block + "`n"
    }
    Write-Generated $target.Path $updated $content
}

# CHANGELOG.md: every release of every scope, in the same order as the WebUI changelog.
function Get-Events($Scope, [string]$Version) {
    $first = $Version -eq $Scope.Versions[0].Version
    foreach ($type in @($catalog.Types | Where-Object { $Scope.Packages -contains $_.Package })) {
        $typeAdded = -not $first -and $type.Since -eq $Version
        if ($typeAdded) { [pscustomobject]@{ Kind = 'Added'; Target = $type.Name; Description = $null } }
        foreach ($change in @($type.Changes | Where-Object { $_ -and $_.Version -eq $Version })) { [pscustomobject]@{ Kind = 'Changed'; Target = $type.Name; Description = $change.Description } }
        if ($type.DeprecatedIn -eq $Version) { [pscustomobject]@{ Kind = 'Deprecated'; Target = $type.Name; Description = $null } }
        if ($type.RemovedIn -eq $Version) { [pscustomobject]@{ Kind = 'Removed'; Target = $type.Name; Description = $null } }
        foreach ($member in @($type.Members)) {
            $target = $type.Name + '.' + $member.Signature
            if (-not $typeAdded -and -not $first -and $member.Since -eq $Version) { [pscustomobject]@{ Kind = 'Added'; Target = $target; Description = $null } }
            foreach ($change in @($member.Changes | Where-Object { $_ -and $_.Version -eq $Version })) { [pscustomobject]@{ Kind = 'Changed'; Target = $target; Description = $change.Description } }
            if ($member.DeprecatedIn -eq $Version) { [pscustomobject]@{ Kind = 'Deprecated'; Target = $target; Description = $null } }
            if ($member.RemovedIn -eq $Version) { [pscustomobject]@{ Kind = 'Removed'; Target = $target; Description = $null } }
        }
    }
}

$kinds = @('Added', 'Changed', 'Deprecated', 'Removed', 'Fixed')
$entries = [Collections.Generic.List[object]]::new()
for ($index = 0; $index -lt $catalog.Scopes.Count; $index++) {
    foreach ($release in $catalog.Scopes[$index].Versions) {
        $entries.Add([pscustomobject]@{ Scope = $catalog.Scopes[$index]; Order = $index; Release = $release })
    }
}
# Unreleased previews first, then newest first; releases published together follow the order of the scopes.
$sorted = [Collections.Generic.List[object]]::new($entries)
$sorted.Sort([Comparison[object]] {
    param($a, $b)
    $result = ([bool]$a.Release.Date).CompareTo([bool]$b.Release.Date)
    if ($result -ne 0) { return $result }
    $result = [string]::CompareOrdinal($b.Release.Date, $a.Release.Date)
    if ($result -ne 0) { return $result }
    $result = $a.Order.CompareTo($b.Order)
    if ($result -ne 0) { return $result }
    return Compare-ApiVersion $b.Release.Version $a.Release.Version
})

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Changelog')
$lines.Add('')
$lines.Add('<!-- Generated by Docs/Update-ApiReference.ps1 from Docs/api-reference.json. Edit the release notes there. -->')
$lines.Add('')
$lines.Add('Every release of the FluentLocalizer packages, newest first. The [WebUI changelog](' + $site + 'changelog) shows the same notes with links to the API reference of each version.')
foreach ($entry in $sorted) {
    $release = $entry.Release
    $lines.Add('')
    $lines.Add('## ' + $entry.Scope.Name + ' ' + $release.Version + $(if ($release.Date) { ' (' + $release.Date + ')' } else { ' (not released yet)' }))
    $lines.Add('')
    $lines.Add('Packages: ' + (($entry.Scope.Packages | ForEach-Object { $tick + $_ + $tick }) -join ', '))
    if ($release.Summary) {
        $lines.Add('')
        $lines.Add($release.Summary)
    }
    foreach ($kind in $kinds) {
        $notes = @($release.Notes | Where-Object { $_ -and $_.Kind -eq $kind })
        if ($notes.Count -eq 0) { continue }
        $lines.Add('')
        $lines.Add('### ' + $kind)
        $lines.Add('')
        foreach ($note in $notes) { $lines.Add('- ' + $note.Text.Replace('](docs/', '](' + $site + 'docs/')) }
    }
    $events = @(Get-Events $entry.Scope $release.Version)
    $lines.Add('')
    if ($release.Version -eq $entry.Scope.Versions[0].Version) {
        $lines.Add('First release of these packages: every public API is new.')
        continue
    }
    if ($events.Count -eq 0) {
        $lines.Add('No public API changes in this version.')
        continue
    }
    $lines.Add('<details><summary>Public API changes (' + $events.Count + ')</summary>')
    $lines.Add('')
    foreach ($kind in $kinds) {
        foreach ($event in @($events | Where-Object Kind -eq $kind)) {
            $line = '- ' + $kind + ': ' + $tick + $event.Target + $tick
            if ($event.Description) { $line += ' — ' + $event.Description }
            $lines.Add($line)
        }
    }
    $lines.Add('')
    $lines.Add('</details>')
}
$changelog = ($lines -join "`n") + "`n"
$changelogPath = Join-Path $root 'CHANGELOG.md'
$existing = if (Test-Path -LiteralPath $changelogPath) { [IO.File]::ReadAllText($changelogPath).Replace("`r`n", "`n") } else { '' }
Write-Generated 'CHANGELOG.md' $changelog $existing

if ($Check) { Write-Output 'API-REFERENCE.md, the package README API references and CHANGELOG.md match the shared catalog.' }

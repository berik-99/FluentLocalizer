param([switch]$Check)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'api-reference.json') -Raw | ConvertFrom-Json
$tick = [char]96
# The README documents the newest listed version; APIs removed by then are left out.
$current = $catalog.Versions[-1].Version

function Compare-ApiVersion([string]$Left, [string]$Right) {
    $l = $Left.Split('-', 2); $r = $Right.Split('-', 2)
    $result = ([version]$l[0]).CompareTo([version]$r[0])
    if ($result -ne 0) { return $result }
    if ($l.Count -ne $r.Count) { return $(if ($l.Count -gt $r.Count) { -1 } else { 1 }) }
    if ($l.Count -eq 1) { return 0 }
    return [string]::CompareOrdinal($l[1], $r[1])
}

function Test-Available($Entry) {
    -not $Entry.RemovedIn -or (Compare-ApiVersion $Entry.RemovedIn $current) -gt 0
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
$targets = @(
    @{ Path = 'README.md'; Package = $null },
    @{ Path = 'Sources/FluentLocalizer.Core/Assets/README.md'; Package = 'FluentLocalizer' },
    @{ Path = 'Sources/FluentLocalizer.Store.Json/Assets/README.md'; Package = 'FluentLocalizer.Store.Json' },
    @{ Path = 'Sources/FluentLocalizer.Extensions.DependencyInjection/Assets/README.md'; Package = 'FluentLocalizer.Extensions.DependencyInjection' }
)

foreach ($target in $targets) {
    $path = Join-Path $root $target.Path
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('<!-- api-reference:start -->')
    $lines.Add('## API reference')
    $lines.Add('')
    $lines.Add('Public types and members declared by FluentLocalizer, grouped by package and namespace. Includes constructors, overloads, properties, enum values, inherited library members and deprecated APIs. Standard inherited .NET members are omitted.')
    $lines.Add('')
    $lines.Add('Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. Every type states the version that introduced it; later additions, changes and deprecations are marked on each member. The searchable [WebUI API reference](https://berik-99.github.io/FluentLocalizer/docs/api) uses the same catalog and can show any earlier version.')
    $lines.Add('')
    $lines.Add('Documented version: ' + $tick + $current + $tick)
    $lines.Add('')
    $types = @($catalog.Types | Where-Object { (-not $target.Package -or $_.Package -eq $target.Package) -and (Test-Available $_) })
    foreach ($type in $types) {
        $lines.Add('- [' + $type.Name + '](#api-' + $type.Name.ToLowerInvariant() + ')')
    }
    $lines.Add('')
    foreach ($type in $types) {
        $lines.Add('<a id="api-' + $type.Name.ToLowerInvariant() + '"></a>')
        $lines.Add('')
        $lines.Add('### ' + $type.Name)
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
        foreach ($member in @($type.Members | Where-Object { Test-Available $_ })) {
            $signature = $member.Signature.Replace('|', '&#124;')
            $notes = @()
            if ($member.Since -and $member.Since -ne $type.Since) { $notes += '*Since ' + $member.Since + '.*' }
            $memberLifecycle = Get-Lifecycle $member
            if ($memberLifecycle) { $notes += '**' + $memberLifecycle + '**' }
            $description = (@($notes) + $member.Description) -join ' '
            foreach ($change in @($member.Changes)) {
                if ($change) { $description += ' *Changed in ' + $change.Version + ':* ' + $change.Description }
            }
            $lines.Add('| ' + $tick + $signature + $tick + ' | ' + $description.Replace('|', '&#124;') + ' |')
        }
        $lines.Add('')
        if ($type.Changes) {
            $lines.Add('Changes:')
            $lines.Add('')
            foreach ($change in $type.Changes) { $lines.Add('- ' + $change.Version + ': ' + $change.Description) }
            $lines.Add('')
        }
        $source = 'https://github.com/berik-99/FluentLocalizer/blob/main/' + $type.Source
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
    if ($Check) {
        if ($updated -ne $content) { throw "API reference is out of date: $($target.Path). Run Docs/Update-ApiReference.ps1." }
    } else {
        [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false))
    }
}
if ($Check) { Write-Output 'All README API references match the shared catalog.' }

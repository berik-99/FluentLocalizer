param([switch]$Check)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'api-reference.json') -Raw | ConvertFrom-Json
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
    $lines.Add('Signatures use C# notation; parameter names, defaults, return values and relevant exceptions are described alongside each member. The searchable [WebUI API reference](https://berik-99.github.io/FluentLocalizer/docs/api) uses the same catalog.')
    $lines.Add('')
    $types = @($catalog | Where-Object { -not $target.Package -or $_.Package -eq $target.Package })
    foreach ($type in $types) {
        $lines.Add('- [' + $type.Name + '](#api-' + $type.Name.ToLowerInvariant() + ')')
    }
    $lines.Add('')
    foreach ($type in $types) {
        $lines.Add('<a id="api-' + $type.Name.ToLowerInvariant() + '"></a>')
        $lines.Add('')
        $lines.Add('### ' + $type.Name)
        $lines.Add('')
        $lines.Add('Package: ' + [char]96 + $type.Package + [char]96 + ' · Namespace: ' + [char]96 + $type.Namespace + [char]96)
        $lines.Add('')
        $lines.Add($type.Summary)
        $lines.Add('')
        if ($type.Remarks) {
            $lines.Add($type.Remarks)
            $lines.Add('')
        }
        $lines.Add('| Member | Parameters, return value and behavior |')
        $lines.Add('| --- | --- |')
        foreach ($member in $type.Members) {
            $signature = $member.Signature.Replace('|', '&#124;')
            $description = $member.Description.Replace('|', '&#124;')
            $lines.Add('| ' + [char]96 + $signature + [char]96 + ' | ' + $description + ' |')
        }
        $lines.Add('')
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

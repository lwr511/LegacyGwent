$ErrorActionPreference = 'Stop'
$workspace = 'C:\UnityProjects\LegacyGwent'
$source = (Resolve-Path -LiteralPath "$workspace\work\DynamicCards\Probe\Library\DynamicCardsBundles\StandaloneWindows64").Path
$target = (Resolve-Path -LiteralPath "$workspace\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card\Library\DynamicCardsBundles\StandaloneWindows64").Path
if ($source -ne "$workspace\work\DynamicCards\Probe\Library\DynamicCardsBundles\StandaloneWindows64" -or $target -ne "$workspace\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card\Library\DynamicCardsBundles\StandaloneWindows64") { throw 'Unexpected bundle directory' }
$build = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'build-result.json') -Raw | ConvertFrom-Json
$runtime = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-result.json') -Raw | ConvertFrom-Json
$equivalent = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'source-equivalence.json') -Raw | ConvertFrom-Json
if (-not $build.passed -or $build.scenes -ne 1279 -or -not $runtime.passed -or $equivalent.differentBuildInputs.Count) { throw 'Verification is incomplete' }
$index = Get-Content -LiteralPath (Join-Path $source 'cards.index.json') -Raw | ConvertFrom-Json
$names = @('cards.bundle', 'cards.index.json') + @($index.parts | ForEach-Object file)
foreach ($name in $names) {
    if ([IO.Path]::GetFileName($name) -ne $name -or $name -notmatch '^cards(\.bundle|\.index\.json|-[\w-]+\.bundle)$') { throw 'Invalid package filename' }
    $item = Get-Item -LiteralPath (Join-Path $source $name)
    if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected package type' }
}
# The editor-ready marker is only restored after every new file and manifest is installed.
$marker = Join-Path $target 'cards.bundle.editor-ready'
if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker -Force }
foreach ($item in Get-ChildItem -LiteralPath $target -Force) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected cache link' }
    if ($item.PSIsContainer) {
        if ($item.Name -ne 'install-staging' -or @(Get-ChildItem -LiteralPath $item.FullName -Force).Count) { throw 'Unexpected cache subdirectory' }
        [IO.Directory]::Delete($item.FullName, $false)
    } else { Remove-Item -LiteralPath $item.FullName -Force }
}
foreach ($name in $names) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $target $name)
    if ($name.EndsWith('.bundle')) {
        foreach ($suffix in @('.inputs', '.manifest')) {
            $metadata=Join-Path $source ($name+$suffix)
            if (Test-Path -LiteralPath $metadata) {
                $item=Get-Item -LiteralPath $metadata
                if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected package metadata type' }
                Copy-Item -LiteralPath $metadata -Destination (Join-Path $target ($name+$suffix))
            }
        }
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'editor-files.json') -Destination (Join-Path $target 'cards.bundle.editor-files.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'source-hashes.json') -Destination (Join-Path $target 'cards.source-hashes.json')
[IO.File]::WriteAllText($marker, [DateTime]::UtcNow.ToString('O'))
# Remove obsolete top-level probe packages; keep only this build's indexed payload and metadata.
foreach ($item in Get-ChildItem -LiteralPath $source -File) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected probe cache link' }
    if ($item.Name -match '^(cards.*\.bundle)(\.inputs|\.manifest)?$' -and $Matches[1] -notin $names) {
        Remove-Item -LiteralPath $item.FullName -Force
    }
}
$result = [ordered]@{installed=$true;scenes=1279;files=$names.Count;bundleBytes=(Get-ChildItem -LiteralPath $target -Filter '*.bundle' | Measure-Object Length -Sum).Sum;editorReady=(Test-Path -LiteralPath $marker);androidWorkRemoved=(-not (Test-Path -LiteralPath "$workspace\work\AndroidBuild"))}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'delivery-result.json') -Encoding utf8
$result | ConvertTo-Json -Compress

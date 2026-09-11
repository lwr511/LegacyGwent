$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath 'C:\UnityProjects\LegacyGwent\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card').Path
$content = (Resolve-Path -LiteralPath (Join-Path $project 'Assets/DynamicCards/Content')).Path
if ($content -ne 'C:\UnityProjects\LegacyGwent\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card\Assets\DynamicCards\Content') { throw 'Unexpected content root' }
$plan = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'plan.json') -Raw | ConvertFrom-Json
if ($plan.externalDependencies.Count -ne 0) { throw 'Cross-directory content dependencies require relocation' }
$paths = foreach ($relative in $plan.remove) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $project $relative))
    if (-not $absolute.StartsWith($content+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside content' }
    if ($absolute.StartsWith($content+'\Latest\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Latest content is protected' }
    $item = Get-Item -LiteralPath $absolute -Force
    if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Only regular files may be removed' }
    $absolute
}
Write-Output "CONTENT_CLEANUP_VERIFIED files=$($paths.Count)"
foreach ($path in $paths) { Remove-Item -LiteralPath $path -Force }
# Remove empty legacy directories only; never recurse during deletion.
$dirs = Get-ChildItem -LiteralPath $content -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending
foreach ($dir in $dirs) {
    if ($dir.FullName -eq (Join-Path $content 'Latest') -or $dir.FullName.StartsWith($content+'\Latest\')) { continue }
    if ($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Unexpected content link' }
    if (-not @(Get-ChildItem -LiteralPath $dir.FullName -Force).Count) {
        [IO.Directory]::Delete($dir.FullName, $false)
        if (Test-Path -LiteralPath ($dir.FullName+'.meta')) { Remove-Item -LiteralPath ($dir.FullName+'.meta') -Force }
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'catalog.json') -Destination (Join-Path $content 'catalog.json')
$marker = Join-Path $project 'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready'
if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker -Force }
Write-Output 'LATEST_ONLY_CONTENT_APPLIED'

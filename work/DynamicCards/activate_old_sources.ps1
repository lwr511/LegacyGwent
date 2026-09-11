$ErrorActionPreference = 'Stop'
$repoRoot = 'C:\UnityProjects\LegacyGwent'
$projectRoot = Join-Path $repoRoot 'src\Cynthia.Card.Unity\src\Cynthia.Unity.Card'
$stageRoot = Join-Path $repoRoot 'work\DynamicCards\OldSourcesStage'
$backupRoot = Join-Path $repoRoot 'work\DynamicCards\BeforeOldSources-20260908'
$audit = Get-Content -LiteralPath (Join-Path $stageRoot 'audit.json') -Raw | ConvertFrom-Json
if ($audit.cards -ne 580 -or $audit.missingGuids.Count -ne 0 -or $audit.duplicateGuids.Count -ne 0 -or $audit.duplicateArtIds.Count -ne 0) { throw 'Staged audit did not pass' }
if (!(Test-Path -LiteralPath (Join-Path $stageRoot 'editor-stopped.flag'))) { throw 'Editor has not stopped' }
if (!(Test-Path -LiteralPath (Join-Path $stageRoot 'texture-settings.json'))) { throw 'Texture settings incomplete' }
function Move-WorkspaceItem([string]$sourcePath, [string]$destinationPath) {
    $sourceFull = [IO.Path]::GetFullPath($sourcePath)
    $destinationFull = [IO.Path]::GetFullPath($destinationPath)
    if (!$sourceFull.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or !$destinationFull.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Move escaped workspace' }
    if (Test-Path -LiteralPath $destinationFull) { throw "Destination exists: $destinationFull" }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destinationFull)) -Force | Out-Null
    if (Test-Path -LiteralPath $sourceFull) { Move-Item -LiteralPath $sourceFull -Destination $destinationFull }
}
if (Test-Path -LiteralPath $backupRoot) { throw 'Backup already exists; inspect before resuming' }
Move-WorkspaceItem (Join-Path $projectRoot 'Assets\DynamicCards\Content') (Join-Path $backupRoot 'Content')
Move-WorkspaceItem (Join-Path $stageRoot 'Assets\DynamicCards\Content') (Join-Path $projectRoot 'Assets\DynamicCards\Content')
Copy-Item -LiteralPath (Join-Path $backupRoot 'Content\catalog.json.meta') -Destination (Join-Path $projectRoot 'Assets\DynamicCards\Content\catalog.json.meta')
Move-WorkspaceItem (Join-Path $projectRoot 'Library\DynamicCardsBundles\StandaloneWindows64') (Join-Path $backupRoot 'EditorBundles')
foreach ($sourceFile in Get-ChildItem -LiteralPath (Join-Path $stageRoot 'Assets\DynamicCards\Shaders') -Recurse -File) {
    $relative = $sourceFile.FullName.Substring((Join-Path $stageRoot 'Assets').Length + 1)
    $destination = Join-Path (Join-Path $projectRoot 'Assets') $relative
    if (!(Test-Path -LiteralPath $destination)) {
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destination
    }
}
Set-Content -LiteralPath (Join-Path $stageRoot 'activated.flag') -Value ([DateTime]::UtcNow.ToString('O'))
Write-Output "OLD_SOURCES_ACTIVATED backup=$backupRoot"

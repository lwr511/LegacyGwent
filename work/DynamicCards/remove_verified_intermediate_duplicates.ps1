$ErrorActionPreference='Stop'
$taskProject=[IO.Path]::GetFullPath('C:\UnityProjects\LegacyGwent')+'\'
$taskRoot=$taskProject+'work\DynamicCards'
$taskManifest="$taskRoot\removed_duplicate_intermediates.jsonl"
$taskTotal=0L
function Remove-VerifiedDuplicate([string]$source,[string]$retained){
 $source=[IO.Path]::GetFullPath($source);$retained=[IO.Path]::GetFullPath($retained)
 if(-not $source.StartsWith($taskProject,[StringComparison]::OrdinalIgnoreCase) -or -not $retained.StartsWith($taskProject,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped project'}
 if($source.Equals($retained,[StringComparison]::OrdinalIgnoreCase)){throw 'Same source and retained file'}
 if(-not (Test-Path -LiteralPath $retained -PathType Leaf)){return}
 $taskItem=Get-Item -LiteralPath $source
 if($taskItem.Length -ne (Get-Item -LiteralPath $retained).Length){return}
 $taskHash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
 if($taskHash -ne (Get-FileHash -LiteralPath $retained -Algorithm SHA256).Hash){return}
 [pscustomobject]@{removed=$source;retained=$retained;sha256=$taskHash;bytes=$taskItem.Length}|ConvertTo-Json -Compress|Add-Content -LiteralPath $taskManifest -Encoding utf8
 Remove-Item -LiteralPath $source
 $script:taskTotal+=$taskItem.Length
}
$taskPairs=@(
 @('LatestAudio','Probe\Assets\DynamicCards\Content\Latest\Audio','*.wav'),
 @('LegacyAudio','Probe\Assets\DynamicCards\Content\Legacy2017\Audio','*.wav'),
 @('Audio','Probe\Assets\DynamicCards\Content\Audio','*.wav'),
 @('LatestAtlases','Probe\Assets\DynamicCards\Content\Latest\Atlases','*.png')
)
foreach($taskPair in $taskPairs){
 foreach($taskItem in Get-ChildItem -LiteralPath "$taskRoot\$($taskPair[0])" -File -Filter $taskPair[2]){
  Remove-VerifiedDuplicate $taskItem.FullName "$taskRoot\$($taskPair[1])\$($taskItem.Name)"
 }
 Write-Output "DUPLICATES_VERIFIED $($taskPair[0]) cumulativeBytes=$taskTotal"
}
if(-not (Get-Process Smoke,Queue -ErrorAction SilentlyContinue)){
 $taskOldBundle=$taskProject+'src\Cynthia.Card.Unity\src\Cynthia.Unity.Card\Library\DynamicCardsBundles\StandaloneWindows64\cards.bundle'
 foreach($taskBuild in @('native\Smoke','queue\Queue')){
  Remove-VerifiedDuplicate "$taskRoot\BuildVerification\$($taskBuild)_Data\StreamingAssets\DynamicCards\cards.bundle" $taskOldBundle
 }
 Remove-VerifiedDuplicate "$taskRoot\BuildVerification\queue_final\Queue_Data\StreamingAssets\DynamicCards\cards.bundle" "$taskRoot\BuildVerification\native_final\Smoke_Data\StreamingAssets\DynamicCards\cards.bundle"
}
Write-Output "VERIFIED_DUPLICATE_CLEANUP_DONE bytes=$taskTotal"

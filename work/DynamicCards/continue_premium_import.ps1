param([int]$ImportProcessId=9852)
$ErrorActionPreference='Stop'
$taskRoot='C:\UnityProjects\LegacyGwent\work\DynamicCards'
while(Get-Process -Id $ImportProcessId -ErrorAction SilentlyContinue){Start-Sleep -Seconds 10}
if(-not (Select-String -LiteralPath "$taskRoot\latest_all_unity_import.log" -SimpleMatch 'LATEST_COMPLETE_IMPORT_FINISHED' -Quiet)){throw 'Latest import did not finish successfully'}
& 'C:\Program Files\Python312\python.exe' "$taskRoot\apply_original_material_states.py" Native
if($LASTEXITCODE -ne 0){throw 'Native material restoration failed'}
& 'C:\Program Files\Python312\python.exe' "$taskRoot\apply_original_material_states.py" Legacy
if($LASTEXITCODE -ne 0){throw 'Legacy material restoration failed'}
Copy-Item -LiteralPath "$taskRoot\SourceAnimationImporter.cs" -Destination "$taskRoot\Probe\Assets\Editor\SourceAnimationImporter.cs"
Copy-Item -LiteralPath "$taskRoot\PremiumPostImportEditor.cs" -Destination "$taskRoot\Probe\Assets\Editor\PremiumPostImportEditor.cs"
Copy-Item -LiteralPath "$taskRoot\PremiumValidationEditor.cs" -Destination "$taskRoot\Probe\Assets\Editor\PremiumValidationEditor.cs"
Copy-Item -LiteralPath "$taskRoot\GeraltTimelineSmoke.cs" -Destination "$taskRoot\Probe\Assets\GeraltTimelineSmoke.cs"
Copy-Item -LiteralPath 'C:\UnityProjects\LegacyGwent\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card\Assets\DynamicCards\Runtime\DynamicCardView.cs' -Destination "$taskRoot\Probe\Assets\DynamicCards\Runtime\DynamicCardView.cs"
Copy-Item -LiteralPath "$taskRoot\QueueSmoke.cs" -Destination "$taskRoot\Probe\Assets\QueueSmoke.cs"
Copy-Item -LiteralPath 'C:\UnityProjects\LegacyGwent\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card\Assets\DynamicCards\Editor\DynamicCardEditorCache.cs' -Destination "$taskRoot\Probe\Assets\DynamicCards\Editor\DynamicCardEditorCache.cs"
Copy-Item -LiteralPath 'C:\UnityProjects\LegacyGwent\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card\Assets\DynamicCards\Editor\DynamicCardBuild.cs' -Destination "$taskRoot\Probe\Assets\DynamicCards\Editor\DynamicCardBuild.cs"
$taskProc=Start-Process 'C:\Program Files\Unity\Editor\Unity.exe' -ArgumentList '-batchmode -projectPath C:\UnityProjects\LegacyGwent\work\DynamicCards\Probe -executeMethod PremiumPostImportEditor.Run -quit -logFile C:\UnityProjects\LegacyGwent\work\DynamicCards\premium_post_import.log' -WindowStyle Hidden -PassThru
Write-Output "POST_IMPORT_STARTED $($taskProc.Id)"
$taskProc.WaitForExit()
if($taskProc.ExitCode -ne 0){throw "Post import failed: $($taskProc.ExitCode)"}
Write-Output 'POST_IMPORT_FINISHED'

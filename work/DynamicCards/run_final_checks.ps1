$ErrorActionPreference = 'Stop'
$taskRoot = 'C:/UnityProjects/LegacyGwent/work/DynamicCards'
$clientRoot = 'C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
$unityExe = 'C:/Program Files/Unity/Editor/Unity.exe'
while (Get-Process -Id 16596 -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 5 }
if (-not (Select-String -Path "$taskRoot/final_materials2.log" -Pattern 'DYNAMIC_MATERIALS_READY' -Quiet)) { exit 1 }
if (-not (Select-String -Path "$taskRoot/client_import_all4.log" -Pattern 'DYNAMIC_CONTENT_READY cards=255 particles=3128' -Quiet)) {
    Set-Content "$taskRoot/final_check_status.txt" 'Import failed; checks not started.'
    exit 1
}
Copy-Item -LiteralPath "$taskRoot/DynamicCardVerification.cs" -Destination "$clientRoot/Assets/DynamicCards/Editor/DynamicCardVerification.cs"
foreach ($check in @('Audit','Bundle','Runtime')) {
    Set-Content "$taskRoot/final_check_status.txt" "Running $check"
    $method = switch ($check) {
        'Audit' { 'Assets.Script.DynamicCards.Editor.DynamicCardContentAudit.Run' }
        'Bundle' { 'Assets.Script.DynamicCards.Editor.DynamicCardContentAudit.Bundle' }
        'Runtime' { 'Assets.Script.DynamicCards.Editor.DynamicCardVerification.Run' }
    }
    $quitFlag = if ($check -eq 'Runtime') { '' } else { '-quit' }
    $process = Start-Process -FilePath $unityExe -ArgumentList "-batchmode $quitFlag -projectPath $clientRoot -executeMethod $method -logFile $taskRoot/final_$check.log" -WindowStyle Hidden -PassThru
    Set-Content "$taskRoot/final_check_pid.txt" $process.Id
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        Set-Content "$taskRoot/final_check_status.txt" "$check failed: $($process.ExitCode)"
        exit 1
    }
}
Set-Content "$taskRoot/final_check_status.txt" 'Checks complete'

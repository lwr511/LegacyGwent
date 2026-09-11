$ErrorActionPreference='Stop'
$taskRoot='C:/UnityProjects/LegacyGwent/work/DynamicCards'
$clientRoot='C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
foreach($check in @('Runtime','Settings','Bundle')) {
    $method=switch($check) {
        'Runtime' {'Assets.Script.DynamicCards.Editor.DynamicCardVerification.Run'}
        'Settings' {'Assets.Script.DynamicCards.Editor.DynamicCardVerification.Settings'}
        'Bundle' {'Assets.Script.DynamicCards.Editor.DynamicCardContentAudit.Bundle'}
    }
    $quitFlag=if($check -eq 'Runtime'){''}else{'-quit'}
    Set-Content "$taskRoot/release_status.txt" "Running $check"
    $p=Start-Process -FilePath 'C:/Program Files/Unity/Editor/Unity.exe' -ArgumentList "-batchmode $quitFlag -projectPath $clientRoot -executeMethod $method -logFile $taskRoot/release_$check.log" -WindowStyle Hidden -PassThru
    Set-Content "$taskRoot/release_pid.txt" $p.Id
    $p.WaitForExit()
    if($p.ExitCode -ne 0){Set-Content "$taskRoot/release_status.txt" "$check failed $($p.ExitCode)";exit 1}
}
Set-Content "$taskRoot/release_status.txt" 'Complete'

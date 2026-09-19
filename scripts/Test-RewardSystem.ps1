param([string]$LanAddress='',[switch]$WithUnityUi,[switch]$FullRegression)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$work=Join-Path $repo ('work/RewardSystem/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path (Join-Path $work 'data') -Force | Out-Null
if(!$LanAddress){$LanAddress=(Get-NetIPConfiguration | Where-Object {$_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up'} | Select-Object -First 1).IPv4Address.IPAddress}
if(!$LanAddress -or !(Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -eq $LanAddress -and $_.AddressState -eq 'Preferred'})){throw 'LAN address must be active on this test machine.'}
if(Get-NetTCPConnection -State Listen | Where-Object {$_.LocalPort -in 28021,5016}){throw 'Isolated test ports are already in use.'}
if($WithUnityUi -and !(Test-Path -LiteralPath (Join-Path $repo 'work/RewardSystem/ui-editor-ready.txt'))){throw 'Refresh the Unity project to load RewardUiVerification.cs, enter Play mode, then retry -WithUnityUi.'}
$mongo=Join-Path $repo 'work/LocalServer/mongodb/mongodb-win32-x86_64-2012plus-4.2.25/bin/mongod.exe'
$dotnet=Join-Path $repo 'work/LocalServer/dotnet/dotnet.exe'
$dll=Join-Path $repo 'src/Cynthia.Card/test/RewardSystemTest/bin/Debug/netcoreapp3.0/RewardSystemTest.dll'
$evidencePaths=@(
    'src/Cynthia.Card/src/Cynthia.Card.Server/Startup.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/DailyQuestProgressJsonConverter.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/Services/GwentGameService/GwentServerService.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/Services/GwentGameService/GwentDatabaseService.DailyQuests.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/Services/GwentGameService/GwentDatabaseService.Premium.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/Services/GwentGameService/GwentDatabaseService.InitialPowder.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/Services/GwentGameService/InitialPowderGrantService.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/Services/GwentGameService/InitialPowderOptions.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/InitialPowder.json',
    'src/Cynthia.Card/src/Cynthia.Card.Common/Models/DailyQuests.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Common/Models/PremiumCollection.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Common/Models/DeckModel.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/GwentServerModels/GwentServerGame.cs',
    'src/Cynthia.Card/src/Cynthia.Card.Server/GwentServerModels/GwentMatchs.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Runtime/DailyQuestClient.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Runtime/PremiumCollectionClient.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Runtime/PremiumCollectionPanel.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Runtime/CardCopyBadge.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/Script/LoginScript/AOThelper.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Runtime/DailyQuestTicker.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Runtime/DailyQuestPanel.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/Script/EditorMenu/EditorInfo.cs',
    'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/Editor/RewardUiVerification.cs')
$evidencePaths+=Get-ChildItem (Join-Path $repo 'src/Cynthia.Card/test/RewardSystemTest'),(Join-Path $repo 'src/Cynthia.Card/test/RewardClientTest') -File -Filter '*.cs' | ForEach-Object {$_.FullName}
$evidencePaths+=@($dll,(Join-Path (Split-Path $dll) 'Cynthia.Card.Server.dll'),(Join-Path (Split-Path $dll) 'Cynthia.Card.Common.dll'))
$hashes=foreach($path in $evidencePaths){$full=if([IO.Path]::IsPathRooted($path)){$path}else{Join-Path $repo $path};$hash=Get-FileHash -LiteralPath $full -Algorithm SHA256;@{path=$full;sha256=$hash.Hash}}
@{utc=[DateTime]::UtcNow.ToString('O');head=(& git -C $repo rev-parse HEAD);files=$hashes;fullRegression=[bool]$FullRegression;unityUi=[bool]$WithUnityUi} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $work 'source-manifest.json')
function Start-TestMongo {
    $data=Join-Path $work 'data';$log=Join-Path $work 'mongo.log'
    $process=Start-Process -FilePath $mongo -ArgumentList @('--bind_ip','127.0.0.1','--port','28021','--dbpath',('"'+$data+'"'),'--logpath',('"'+$log+'"'),'--logappend','--setParameter','enableTestCommands=1') -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    while(!(Get-NetTCPConnection -State Listen -LocalPort 28021 -ErrorAction SilentlyContinue)){if($process.HasExited -or [DateTime]::UtcNow -gt $deadline){throw 'Isolated Mongo failed to start'};Start-Sleep -Milliseconds 200}
    return $process
}
function Stop-OwnedProcess($process,$expectedPath) {
    if(!$process){return}
    $current=Get-Process -Id $process.Id -ErrorAction SilentlyContinue
    if($current -and $current.Path -eq $expectedPath){Stop-Process -Id $current.Id;Wait-Process -Id $current.Id -Timeout 15 -ErrorAction SilentlyContinue}
}
$mongoProcess=$null;$suite=$null
try {
    $mongoProcess=Start-TestMongo
    $env:DOTNET_MULTILEVEL_LOOKUP='0'
    $arguments=@(('"'+$dll+'"'),('"'+$repo+'"'),('"'+$work+'"'),$LanAddress)
    if($WithUnityUi){$arguments+='ui'}
    if($FullRegression){$arguments+='full'}
    $suite=Start-Process -FilePath $dotnet -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $work 'suite.log') -RedirectStandardError (Join-Path $work 'suite-errors.log')
    # Retain the native handle while polling; Windows PowerShell can otherwise lose ExitCode after process exit.
    $suiteHandle=$suite.Handle
    @{work=$work;lanAddress=$LanAddress;process=$suite.Id;mongo=$mongoProcess.Id} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo 'work/RewardSystem/latest.json')
    Write-Output "Running complete reward matrix at $LanAddress`:5016. Evidence: $work"
    $deadline=[DateTime]::UtcNow.AddMinutes(15)
    while(!$suite.HasExited) {
        if([DateTime]::UtcNow -gt $deadline){throw 'Reward suite timed out'}
        if((Test-Path (Join-Path $work 'mongo-restart.request')) -and !(Test-Path (Join-Path $work 'mongo-restarted'))) {
            Stop-OwnedProcess $mongoProcess $mongo
            $mongoProcess=Start-TestMongo
            Set-Content -LiteralPath (Join-Path $work 'mongo-restarted') -Value 'isolated MongoDB process restarted with existing disk data'
        }
        Start-Sleep -Milliseconds 300
    }
    $suite.WaitForExit()
    $suiteExitCode=$suite.ExitCode
    @{exitCode=$suiteExitCode;hasExited=$suite.HasExited} | ConvertTo-Json | Set-Content (Join-Path $work 'process-exit.json')
    Get-Content -LiteralPath (Join-Path $work 'suite.log') -Tail 12
    if(!(Test-Path (Join-Path $work 'results.json'))){throw (Get-Content -Raw (Join-Path $work 'suite-errors.log'))}
    $result=Get-Content -Raw -LiteralPath (Join-Path $work 'results.json') | ConvertFrom-Json
    if(!$result.passed -or $null -eq $suiteExitCode -or $suiteExitCode -ne 0){throw "Reward suite failed: $($result.failed) checks, process exit code '$suiteExitCode'; see $work"}
    Write-Output "PASS $($result.count) reward-system checks."
} finally {
    Stop-OwnedProcess $suite $dotnet;Stop-OwnedProcess $mongoProcess $mongo
    $uiRequest=Join-Path $repo 'work/RewardSystem/ui.request'
    if(Test-Path -LiteralPath $uiRequest){
        $pendingRequest=Get-Content -Raw -LiteralPath $uiRequest | ConvertFrom-Json
        if($pendingRequest.work -eq $work){Remove-Item -LiteralPath $uiRequest}
    }
}

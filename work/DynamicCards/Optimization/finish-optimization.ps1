$ErrorActionPreference='Stop'
$r=$PSScriptRoot
$work=Split-Path $r -Parent
$repo='C:\UnityProjects\LegacyGwent'
$main="$repo\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card"
$probe="$work\Probe"
$unity='C:\Program Files\Unity\Editor\Unity.exe'
$python='C:\Program Files\Python312\python.exe'
function RunUnity([string]$method,[string]$log,[bool]$quit){
    $arguments=@('-batchmode','-projectPath',$probe.Replace('\','/'),'-executeMethod',$method,'-logFile',"$r/$log")
    if($quit){$arguments+='-quit'}
    $p=Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $p.Id | Set-Content -LiteralPath "$r/active.pid"
    $p.WaitForExit()
    if($p.ExitCode -ne 0){throw "Unity failed: $method ($($p.ExitCode))"}
}
try {
    Write-Output 'WAITING_FOR_FULL_BUILD'
    $buildPid=[int](Get-Content -LiteralPath "$r/active.pid")
    if(Get-Process -Id $buildPid -ErrorAction SilentlyContinue){Wait-Process -Id $buildPid -ErrorAction Stop}
    $build=Get-Content -LiteralPath "$r/build-result.json" -Raw | ConvertFrom-Json
    if(-not $build.passed -or $build.scenes -ne 1279){throw 'Build verification failed'}
    Write-Output 'FULL_BUILD_VERIFIED'
    Copy-Item -LiteralPath "$main/Assets/DynamicCards/Editor/DynamicCardEditorCache.cs" -Destination "$probe/Assets/DynamicCards/Editor/DynamicCardEditorCache.cs"
    $smoke=Get-Content -LiteralPath "$probe/Assets/LatestOnlySmoke.cs" -Raw
    [IO.File]::WriteAllText("$probe/Assets/LatestOnlySmoke.cs",$smoke.Replace('../LatestOnly/runtime-result.json','../Optimization/runtime-result.json'))
    RunUnity 'CardCacheRegression.Run' 'cache-final.log' $true
    if(-not (Get-Content "$r/cache-test.json" -Raw | ConvertFrom-Json).passed){throw 'Cache regression failed'}
    Write-Output 'CACHE_REGRESSION_VERIFIED'
    $env:DYNAMIC_BENCH_MODE='optimized'
    RunUnity 'PageLoadBenchmarkEditor.Run' 'page-optimized.log' $false
    $page=Get-Content "$r/page-optimized.json" -Raw | ConvertFrom-Json
    if($page.cards -ne 20 -or -not $page.bundles){throw 'Page benchmark failed'}
    $baseline=Get-Content "$r/page-bundle-base.json" -Raw | ConvertFrom-Json
    if($page.totalMs -gt $baseline.totalMs*1.35 -or $page.p95FrameMs -gt 30 -or $page.maxFrameMs -gt 500){throw 'Compressed bundle loading needs further performance review before publishing'}
    Write-Output ('PAGE_VERIFIED '+($page | ConvertTo-Json -Compress))
    RunUnity 'LatestOnlySmokeEditor.Run' 'runtime.log' $false
    if(-not (Get-Content "$r/runtime-result.json" -Raw | ConvertFrom-Json).passed){throw 'Runtime verification failed'}
    Write-Output 'RUNTIME_VERIFIED'
    & $python "$r/publish-source.py"
    if($LASTEXITCODE -ne 0){throw 'Source publishing failed'}
    & "$r/remove-duplicate-main-textures.ps1" -Apply
    & $python "$work/audit_content_guids.py" --client
    if($LASTEXITCODE -ne 0){throw 'Source audit failed'}
    $audit=Get-Content "$work/client_guid_audit.json" -Raw | ConvertFrom-Json
    if($audit.missing.Count -ne 0 -or @($audit.duplicateGuids.PSObject.Properties).Count -ne 0){throw 'Invalid source references'}
    Copy-Item "$work/client_guid_audit.json" "$r/final-guid-audit.json"
    & $python "$r/prepare-manifest.py"
    if($LASTEXITCODE -ne 0){throw 'Manifest validation failed'}
    & "$r/install-packages.ps1"
    Write-Output 'OPTIMIZATION_DELIVERED'
} catch {
    $_ | Out-String | Write-Output
    exit 1
}

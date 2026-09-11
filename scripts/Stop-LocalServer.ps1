$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$local = Join-Path $repo 'work\LocalServer'
$pidFile = Join-Path $local 'server.pid'
if (Test-Path $pidFile) {
    $serverId = [int](Get-Content $pidFile)
    $process = Get-Process -Id $serverId -ErrorAction SilentlyContinue
    if ($process) {
        $command = (Get-CimInstance Win32_Process -Filter "ProcessId=$serverId").CommandLine
        if ($process.Path -ne (Join-Path $local 'dotnet\dotnet.exe') -or
            $command -notlike '*Cynthia.Card.Server.dll*') { throw 'Server PID no longer identifies this local server.' }
        Stop-Process -Id $serverId
        Wait-Process -Id $serverId -Timeout 15 -ErrorAction SilentlyContinue
    }
}
$mongoPid = Join-Path $local 'mongodb.pid'
if (Test-Path $mongoPid) {
    $databaseId = [int](Get-Content $mongoPid)
    $process = Get-Process -Id $databaseId -ErrorAction SilentlyContinue
    if ($process) {
        $bin = Join-Path $local 'mongodb\mongodb-win32-x86_64-2012plus-4.2.25\bin'
        if ($process.Path -ne (Join-Path $bin 'mongod.exe')) { throw 'MongoDB PID no longer identifies this local database.' }
        & (Join-Path $bin 'mongo.exe') --host 127.0.0.1 --port 28020 admin --quiet --eval 'db.shutdownServer()'
        Wait-Process -Id $databaseId -Timeout 15 -ErrorAction SilentlyContinue
    }
}
Write-Host 'Local services stopped. Database files have been preserved.'

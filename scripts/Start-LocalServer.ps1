param([switch]$Rebuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$local = Join-Path $repo 'work\LocalServer'
$dotnet = Join-Path $local 'dotnet\dotnet.exe'
$mongo = Join-Path $local 'mongodb\mongodb-win32-x86_64-2012plus-4.2.25\bin\mongod.exe'
$server = Join-Path $repo 'src\Cynthia.Card\src\Cynthia.Card.Server'
$dll = Join-Path $server 'bin\Debug\netcoreapp3.0\Cynthia.Card.Server.dll'
if (!(Test-Path $dotnet) -or !(Test-Path $mongo)) { throw "Local tools are missing from $local." }
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:MONGO_CONNECTION_STRING = 'mongodb://127.0.0.1:28020/gwent-diy'
$env:LEGACY_GWENT_LISTEN_URL = 'http://127.0.0.1:5005'
$env:ASPNETCORE_ENVIRONMENT = 'Development'

if ($Rebuild -or !(Test-Path $dll)) {
    Push-Location $local
    try {
        & $dotnet build (Join-Path $server 'Cynthia.Card.Server.csproj') -c Debug --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
    } finally { Pop-Location }
}

function Start-OwnedProcess($Name, $Executable, $Arguments, $Port, $Directory) {
    $pidFile = Join-Path $local "$Name.pid"
    $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    if ($listener) {
        if ((Test-Path $pidFile) -and $listener.OwningProcess -contains [int](Get-Content $pidFile)) {
            Write-Host "$Name already running on $Port."
            return
        }
        throw "Port $Port is occupied by another process; it was left untouched."
    }
    $process = Start-Process $Executable -ArgumentList $Arguments -WorkingDirectory $Directory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $local "logs\$Name.stdout.log") -RedirectStandardError (Join-Path $local "logs\$Name.stderr.log")
    $process.Id | Set-Content $pidFile
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) { throw "$Name exited. Check $local\logs." }
        if (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue) { return }
    }
    throw "$Name did not open port $Port. Check $local\logs."
}

Start-OwnedProcess 'mongodb' $mongo "--bind_ip 127.0.0.1 --port 28020 --dbpath `"$local\data`" --logpath `"$local\logs\mongodb.log`" --logappend" 28020 $local
Start-OwnedProcess 'server' $dotnet "`"$dll`"" 5005 $server
Write-Host 'Local Gwent is running: http://127.0.0.1:5005. Unity: Tools > Legacy Gwent > Server > Local, then restart Play mode.'

param([switch]$Apply)
$ErrorActionPreference='Stop'
$expected='C:\UnityProjects\LegacyGwent\work\DynamicCards\Probe\Assets\DynamicCards\Content\Latest'
$root=(Resolve-Path -LiteralPath $expected).Path
if($root -ne $expected){throw 'Unexpected cleanup root'}
if((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked root'}
$probe='C:\UnityProjects\LegacyGwent\work\DynamicCards\Probe'
$plan=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'texture-dedup.json') -Raw | ConvertFrom-Json
$paths=[Collections.Generic.List[string]]::new()
foreach($relative in $plan.removed){
    $path=[IO.Path]::GetFullPath((Join-Path $probe $relative))
    $canonicalRelative=$plan.pathMap.PSObject.Properties[$relative].Value
    $canonical=[IO.Path]::GetFullPath((Join-Path $probe $canonicalRelative))
    foreach($candidate in @($path,$canonical)){
        if(-not $candidate.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($candidate) -ne '.png'){throw 'Path outside texture root'}
        $item=Get-Item -LiteralPath $candidate
        if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unexpected texture type'}
    }
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $canonical -Algorithm SHA256).Hash){throw 'Texture payloads differ'}
    $meta=Get-Item -LiteralPath ($path+'.meta')
    if($meta.PSIsContainer -or ($meta.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unexpected metadata type'}
    $paths.Add($path);$paths.Add($path+'.meta')
}
Write-Output "VERIFIED_IDENTICAL_DUPLICATE_FILES=$($paths.Count)"
if($Apply){foreach($path in $paths){Remove-Item -LiteralPath $path -Force};Write-Output 'DUPLICATE_TEXTURE_CLEANUP_DONE'}

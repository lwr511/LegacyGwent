$ErrorActionPreference = 'Stop'
Add-Type -Path 'C:/Program Files/Unity/Editor/Data/Managed/Unity.Cecil.dll'
$closureAssemblyPath = 'C:/UnityProjects/LegacyGwent/Builds/Windows-20260913-FlashCardsFinal/DiyGwent_Data/Managed/Assembly-CSharp.dll'
$closureAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($closureAssemblyPath)
try {
 $types = @($closureAssembly.MainModule.Types)
 $library = $types | Where-Object FullName -eq 'Assets.Script.DynamicCards.DynamicCardLibrary'
 $settings = $types | Where-Object FullName -eq 'Assets.Script.DynamicCards.DynamicCardSettings'
 $editor = $types | Where-Object FullName -eq 'EditorInfo'
 if (!$library -or !$settings -or !$editor) { throw 'Missing game code' }
 $normal = $editor.Methods | Where-Object Name -eq 'AutoSetShowCards'
 $calls = @($normal.Body.Instructions | ForEach-Object { [string]$_.Operand })
 $result = [ordered]@{debugTypes=@($types | Where-Object FullName -match 'CollectionDebug').Count;debugLibraryMethods=@($library.Methods | Where-Object Name -in @('DebugKey','LoadDebugCatalog')).Count;inspectionOverride=[bool](@($settings.Properties | Where-Object Name -eq 'InspectionOverride').Count);normalCollectionCall=[bool](@($calls | Where-Object { $_ -like '*SetShowCardInfo*' }).Count);sha256=(Get-FileHash -LiteralPath $closureAssemblyPath -Algorithm SHA256).Hash}
 if ($result.debugTypes -ne 0 -or $result.debugLibraryMethods -ne 0 -or $result.inspectionOverride -or !$result.normalCollectionCall) { throw 'Temporary inspection code remains in player' }
 $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'player-code-check.json')
 $result | ConvertTo-Json
} finally { $closureAssembly.Dispose() }

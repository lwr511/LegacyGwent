$patterns = @('Assets','work')
$files = Get-ChildItem -Path $patterns -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in '.cs','.ps1','.json','.txt','.xml','.cfg','.md','.rsp','.yml' }
$re = [regex]'[A-Za-z]:\\[A-Za-z0-9_ .()&-]+(?:\\[A-Za-z0-9_ .()&-]+)*'
$vals=@{}
foreach($f in $files){
  $text=Get-Content -LiteralPath $f.FullName -Raw -ErrorAction SilentlyContinue
  if($null -eq $text){ continue }
  [void]($re.Matches($text) | ForEach-Object{ $vals[$_.Value]=1 })
}
$vals.Keys | Sort-Object | Select-Object -First 200

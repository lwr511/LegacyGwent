$ErrorActionPreference='Stop'
$ws=[Net.WebSockets.ClientWebSocket]::new()
$cts=[Threading.CancellationTokenSource]::new(30000)
$script:events=[Collections.Generic.List[object]]::new()
function Send-Json($obj){$bytes=[Text.Encoding]::UTF8.GetBytes(($obj|ConvertTo-Json -Depth 20 -Compress)+[char]30);$null=$ws.SendAsync([ArraySegment[byte]]::new($bytes),[Net.WebSockets.WebSocketMessageType]::Text,$true,$cts.Token).GetAwaiter().GetResult()}
function Receive-Json {
 $stream=[IO.MemoryStream]::new();$buffer=[byte[]]::new(65536)
 do{$part=$ws.ReceiveAsync([ArraySegment[byte]]::new($buffer),$cts.Token).GetAwaiter().GetResult();if($part.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close){throw 'Socket closed'};$stream.Write($buffer,0,$part.Count)}while(!$part.EndOfMessage)
 $text=[Text.Encoding]::UTF8.GetString($stream.ToArray());$stream.Dispose()
 foreach($line in $text.Split([char]30)){if($line){$obj=$line|ConvertFrom-Json;if($obj.target){$script:events.Add($obj)};Write-Output $obj}}
}
function Invoke-Hub($id,$target,$arguments){Send-Json @{type=1;invocationId=$id;target=$target;arguments=$arguments};while($true){foreach($msg in @(Receive-Json)){if($msg.type -eq 3 -and $msg.invocationId -eq $id){if($msg.error){throw $msg.error};return $msg.result}}}}
try{
 $null=$ws.ConnectAsync([Uri]'ws://127.0.0.1:5005/hub/gwent',$cts.Token).GetAwaiter().GetResult()
 Send-Json @{protocol='json';version=1};$null=Receive-Json
 $registered=Invoke-Hub '1' 'Register' @('localtest','LocalTest123','Local Test')
 $user=Invoke-Hub '2' 'Login' @('localtest','LocalTest123');if(!$user){throw 'Login returned null'}
 $deck=$user.decks[0];if(!$deck){throw 'No starter deck'}
 $matched=Invoke-Hub '3' 'NewMatchOfPassword' @($deck.id,'ai#f',0)
 if(!$matched){throw 'AI matchmaking rejected'}
 while(!($script:events|Where-Object {$_.target -eq 'MatchResult' -and $_.arguments[0]})){$null=Receive-Json}
 $result=@{passed=$true;registered=$registered;login=$true;starterDecks=$user.decks.Count;aiMatch=$matched;matchResult=$true;events=@($script:events|ForEach-Object target|Select-Object -Unique)}
 $null=Invoke-Hub '4' 'Surrender' @()
 $result|ConvertTo-Json -Depth 5|Set-Content "$PSScriptRoot\verification.json"
 $result|ConvertTo-Json -Depth 5
}finally{$ws.Dispose();$cts.Dispose()}

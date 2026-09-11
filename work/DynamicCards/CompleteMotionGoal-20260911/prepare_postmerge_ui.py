from pathlib import Path
import json
W=Path(__file__).resolve().parent
source=json.loads((W/'stage-play-cards.json').read_text())['cards']
game=json.loads((W/'reported-main-ui/game-card-map.json').read_text(encoding='utf-8-sig'))['cards']
byart={}
for row in game:byart.setdefault(row['art'],row['card'])
ids=['13026','13020','13001','13002'];unused=[];mapped=[]
for row in source:
    matches=[byart[a] for a in row['artIds'] if a in byart]
    if not matches:unused.append(row['id']);continue
    ids.extend(matches);mapped.append(row['id'])
ids=list(dict.fromkeys(ids))
request=dict(run='postmerge-main-ui',cards=ids,waits=[.1,1,3,6],screenshots=True,reopen=False)
(W/'postmerge-ui-request.json').write_text(json.dumps(request,indent=2))
(W/'postmerge-ui-scope.json').write_text(json.dumps(dict(actualUiCardIds=ids,sourceScenes=mapped,noGameArtAlias=unused),indent=2))
print('ACTUAL UI',len(ids),'cards',len(mapped),'source scenes; no current game alias',unused)

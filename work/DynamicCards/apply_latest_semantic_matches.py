from pathlib import Path
import json
root=Path(__file__).resolve().parent
path=root/'Probe/Assets/DynamicCards/Content/catalog.json'
data=json.loads(path.read_text());claimed={a for c in data['cards'] for a in c.get('artIds',[])}
cards={c['id']:c for c in data['cards'] if '/Latest/' in c['prefab']};count=0
for pair in json.loads((root/'latest_semantic_matches.json').read_text()):
 if pair['scene'] not in cards or pair['art'] in claimed:continue
 cards[pair['scene']]['artIds'].append(pair['art']);claimed.add(pair['art']);count+=1
 cards[pair['scene']].setdefault('templateMatches',[]).append(pair)
path.write_text(json.dumps(data,indent=2));print('NEW_TEMPLATE_BINDINGS',count)

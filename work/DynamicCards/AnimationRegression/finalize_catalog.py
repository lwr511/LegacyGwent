from pathlib import Path
import json,re
r=Path(__file__).resolve().parent;w=r.parent;p=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
path=w/'Probe/Assets/DynamicCards/Content/catalog.json';prepared=json.loads(path.read_text())
base=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text())
ids={c['id']:c for c in base['cards']}
for row in json.loads((r/'fallback-plan.json').read_text(encoding='utf8')):
    if row['kind']=='Latest' and row['art'] not in ids[row['id']]['artIds']:ids[row['id']]['artIds'].append(row['art'])
base['cards'] += [c for c in prepared['cards'] if '/Fallback/' in c['prefab']]
assert len({c['id'] for c in base['cards']})==len(base['cards'])
bindings=[a for c in base['cards'] for a in c['artIds']];assert len(bindings)==len(set(bindings))
text=json.dumps(base,indent=2)+'\n';path.write_text(text);(r/'catalog-final.json').write_text(text)
arts=set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"',(w.parents[1]/'src/Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf8')))
byart={a:c['prefab'] for c in base['cards'] for a in c['artIds']}
order=['20158000','20055600','20154000']+sorted(arts-{'20158000','20055600','20154000'},key=lambda a:(byart.get(a,'~'),a))
(r/'all-game-art-ids.json').write_text(json.dumps(dict(ids=order)))
print('CATALOG',len(base['cards']),'MAPPED',len(arts&set(bindings)),'TOTAL',len(arts))

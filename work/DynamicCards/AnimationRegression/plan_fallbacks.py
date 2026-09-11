from pathlib import Path
import json,csv
r=Path(__file__).resolve().parent;w=r.parent;p=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
c=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());bound={a for v in c['cards'] for a in v['artIds']}
nearest=json.loads((r/'Rematch/candidates.json').read_text());old={v['artId']:v for v in csv.DictReader((w/'LatestOnly/card-mapping.csv').open(encoding='utf-8-sig'))}
rows=[]
for art,choices in nearest.items():
    if art in bound:continue
    row=dict(art=art,name=old.get(art,{}).get('names',''))
    if choices[0]['error']<.0005:
        row.update(kind='Latest',id=choices[0]['scene'],evidence=choices[0])
    else:
        previous=old.get(art,{}).get('previousPrefab','')
        if previous:
            scene=previous.split('/')[-2];kind='Legacy2017' if '/Legacy2017/' in previous else 'Native'
            bridge='LegacyBridge2019' if kind=='Legacy2017' else 'Bridge2022'
            exists=(w/bridge/'Assets/PortableCards'/scene/'Card.prefab').exists()
            row.update(kind=kind,id=scene,bridge=bridge,exists=exists,previous=previous)
        else:row.update(kind='Unresolved')
    rows.append(row)
(r/'fallback-plan.json').write_text(json.dumps(rows,indent=2,ensure_ascii=False),encoding='utf8')
for row in rows:print(row)

from pathlib import Path
import json,sys,array,collections
sys.stdout.reconfigure(encoding='utf-8')
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
evidence=json.loads((W/'source-component-evidence.json').read_text());catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards']
records={};docs={}
for row in evidence:
    if row['type']!=198 or not row.get('sourceTargetComponentExists'):continue
    assert row['attribute'] in [2181258151,2883525743]
    source=row['source'];folder={'Thronebreaker':'NativeAnimationData','Legacy2017':'LegacyAnimationData'}[source]
    if source not in docs:docs[source]={(r['id'],r['path']):r for r in json.loads((W.parent/folder/'animations.json').read_text())['animators']}
    record=docs[source][(row['scene'],row['animator'])];clip=next(c for c in record['clips'] if c['name']==row['clip']);track=next(t for t in clip['tracks'] if t['path']==row['path'] and t['attribute']==row['attribute'] and t.get('typeId')==198)
    values=array.array('f');values.frombytes((W.parent/folder/clip['file']).read_bytes());key=(source,row['scene'],row['animator'])
    if key not in records:
        card=next(c for c in catalog if c['sourceId']==row['scene'] and c['sourceVersion']==source)
        records[key]={'source':source,'scene':row['scene'],'animator':row['animator'],'prefab':card['prefab'],'curves':[]}
    records[key]['curves'].append({'clip':row['clip'],'path':row['target'],'sourcePath':row['path'],'property':'EmissionModule.rateOverTime.scalar','duration':clip['duration'],'samples':list(values[track['offset']::clip['columns']])})
(W/'particle-curve-contracts.json').write_text(json.dumps({'records':list(records.values())},indent=2));print('records',len(records),'curves',sum(len(r['curves']) for r in records.values()))

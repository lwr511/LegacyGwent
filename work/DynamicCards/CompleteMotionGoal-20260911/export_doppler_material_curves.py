from pathlib import Path
import json,zlib,array,collections
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');proof=json.loads((W/'latest-renderer-property-proof.json').read_text());records=json.loads((W.parent/'LatestAnimationData/animations.json').read_text())['animators'];catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards'];out={}
for s in [s for s in proof if s['matches']]:
 key=(s['scene'],s['animator']);c=next(c for c in catalog if c['id']==s['scene']);r=next(r for r in records if r['id']==s['scene'] and r['path']==s['animator']);clip=next(cl for cl in r['clips'] if cl['name']==s['clip']);track=next(t for t in clip['tracks'] if t['path']==s['path'] and t['attribute']==s['attribute'] and t['typeId']==s['type']);values=array.array('f');values.frombytes((W.parent/'LatestAnimationData'/clip['file']).read_bytes())
 contract=out.setdefault(key,dict(source='Latest',scene=s['scene'],prefab=c['prefab'],animator=s['animator'],proxies=[],curves=[]))
 for m in s['matches']:
  assert m['type']==0,m
  target=s['animator']+'/'+s['path'];proxy='__SourceMaterial_'+format(zlib.crc32((target+'|'+str(m['slot'])+'|'+m['name']).encode()),'08x')
  if not any(p['path']==proxy for p in contract['proxies']):contract['proxies'].append(dict(path=proxy,target=target,slot=m['slot'],name=m['name'],kind=1))
  contract['curves'].append(dict(proxy=proxy,field='m_Color.'+'rgba'[m['component']],sourceParameter='material.'+m['name']+'.'+'rgba'[m['component']],clip=clip['name'],duration=clip['duration'],samples=list(values[track['offset']::clip['columns']]),sourceAttribute=s['attribute']))
(W/'doppler-material-contracts.json').write_text(json.dumps(dict(records=list(out.values())),indent=2));print('Material source curves',sum(len(r['curves']) for r in out.values()))

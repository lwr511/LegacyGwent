from pathlib import Path
import json,hashlib
from PIL import Image,ImageChops
w=Path(__file__).parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');cache=p/'Library/DynamicCardsBundles/StandaloneWindows64'
d=json.loads((w/'Current/baseline-results.json').read_text());assert d['complete'] and len(d['rows'])==66
for art in ['11221000','11221500']:
 for mode in ['small','large']:
  rows=[r for r in d['rows'] if r['art']==art and r['mode']==mode]; assert len(rows)==11
  assert any('Intro' in a['clips'] for a in rows[0]['animators'])
  assert any('Loop' in a['clips'] for a in rows[-2]['animators'])
  assert rows[-2]['age']>30
  assert any(s['maxVertexDelta']>.1 for s in rows[-2]['skins'])
  assert 1<rows[-1]['age']<3
pixels={}
for part in ['cape','body']:
 a=Image.open(w/f'VaederIsolated/{part}-0.png').convert('RGB');b=Image.open(w/f'VaederIsolated/{part}-3.png').convert('RGB')
 visible=sum(max(x+y)>0 for x,y in zip(a.getdata(),b.getdata()))
 changed=sum(max(x)>2 for x in ImageChops.difference(a,b).getdata())
 pixels[part]={'visiblePixels':visible,'changedPixels':changed,'fraction':changed/visible};assert changed>1000
idx=json.loads((cache/'cards.index.json').read_text());payload=json.loads((w.parent/'TrissVillenFire-20260911/delivery-audit.json').read_text());prior={r['file']:r for r in payload['files']}
parts=[part['file'] for part in idx['parts'] if any('/'+c+'/Card.prefab' in x for c in ['11221001','11320801','11221501'] for x in part['prefabs'])]
checks={}
for name in ['cards.bundle','cards.index.json']+parts:
 value=hashlib.sha256((cache/name).read_bytes()).hexdigest();assert value==prior[name]['sha256'];checks[name]=value
result=dict(status='PASS_ALREADY_WORKING',runtimeSamples=len(d['rows']),isolatedShaderMotion=pixels,currentBundleHashes=checks,mainProjectModified=False)
(w/'result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))

"""Check the delivered source selection, textures, motion, and cache integrity."""
from pathlib import Path
import base64, hashlib, json

w=Path(__file__).parent
p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
cache=p/'Library/DynamicCardsBundles/StandaloneWindows64'
assert (w/'build-result.txt').read_text().startswith('PASS')
assert (cache/'cards.bundle.editor-ready').exists()
before=json.loads((w/'catalog-before.json').read_text())
catalog=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text())
old={c['id']:c for c in before['cards']}
now={c['id']:c for c in catalog['cards']}
assert len(now)==len(old)+1
for ident,entry in old.items():
    if ident=='11210701':
        entry['artIds'].remove('11210700')
    assert now[ident]==entry, ident
ragh=next(c for c in catalog['cards'] if '11210700' in c['artIds'])
assert ragh['sourceVersion']=='Latest' and ragh['sourceId']=='10130101'
assert len([a for c in catalog['cards'] for a in c['artIds']])==len({a for c in catalog['cards'] for a in c['artIds']})
manifest={r['path']:r for r in json.loads((cache/'cards.bundle.editor-files.json').read_text())['files']}
paths=['Assets/DynamicCards/Content/catalog.json']
paths+=[r['path'].replace('\\','/') for r in json.loads((w/'restore-applied.json').read_text())['copies']]
paths+=['Assets/DynamicCards/Content/Old/Legacy2017/Shared/11210601_20908_bigFire_7x5.png']
for asset in paths[:]:
    paths.append(asset+'.meta')
for path in paths:
    assert manifest[path]['hash']==base64.b64encode(hashlib.sha256((p/path).read_bytes()).digest()).decode(),path
report=json.loads((w/'Delivered/baseline-results.json').read_text())
assert report['complete'] and len(report['rows'])==20
for art in ['11210600','11210700']:
    for mode in ['small','large']:
        rows=[r for r in report['rows'] if r['art']==art and r['mode']==mode]
        assert [r['sample'] for r in rows]==[0,1,2,3,4]
        assert rows[3]['age']>18
        assert all(r['animators'] for r in rows)
        assert all(any(s['maxVertexDelta']>.01 for s in r['skins']) for r in rows[1:4])
        if art=='11210600':
            assert all(any(t.endswith('=11210601_20908_bigFire_7x5') for t in r['materialTextures']) for r in rows)
        else:
            assert all(any('/10130101/Pivot/model/dragon' in s['path'] for s in r['skins']) for r in rows)
pixel=(w/'Delivered/texture-pixel-checks.txt').read_text()
assert 'mismatches=0' in pixel and 'maxDelta=0' in pixel
texture=p/'Assets/DynamicCards/Content/Old/Legacy2017/Shared/11210601_20908_bigFire_7x5.png'
assert hashlib.sha256(texture.read_bytes()).hexdigest()=='5918d5be4a4dd1b125272992fd2da58329bd9b423973537d71a2432809c384b3'
assert '  alphaIsTransparency: 0' in Path(str(texture)+'.meta').read_text()
index=json.loads((cache/'cards.index.json').read_text())
payload=['cards.bundle','cards.index.json']+[part['file'] for part in index['parts']]
files=[dict(file=name,bytes=(cache/name).stat().st_size,sha256=hashlib.sha256((cache/name).read_bytes()).hexdigest()) for name in payload]
prior={r['file']:r for r in json.loads((w.parent/'RaghRepair-20260911/delivery-audit.json').read_text())['files']}
changed=[r['file'] for r in files if r['file'] not in prior or r['sha256']!=prior[r['file']]['sha256']]
expected={'cards.bundle','cards.index.json'}|set(json.loads((w/'expected-legacy-parts.json').read_text()))|{'cards-latest-'+str(i).zfill(3)+'.bundle' for i in range(6)}
assert set(changed)<=expected,changed
result=dict(status='PASS',texturePixelCheck=pixel.strip(),catalogScenes=len(now),mappedArts=len({a for c in catalog['cards'] for a in c['artIds']}),verifiedSourceFiles=len(paths),runtimeSamples=len(report['rows']),payloadFiles=len(files),payloadBytes=sum(r['bytes'] for r in files),changedPayload=changed,files=files)
(w/'delivery-audit.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:v for k,v in result.items() if k!='files'},indent=2))

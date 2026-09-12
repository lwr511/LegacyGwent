"""Verify repaired files and the output of the real main-project bundle build."""
from pathlib import Path
import json,hashlib,base64,datetime,re
W=Path(__file__).resolve().parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
B=P/'Library/DynamicCardsBundles/StandaloneWindows64'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.digest()
def utc(s):return datetime.datetime.fromisoformat(s.replace('Z','+00:00'))
status=(W/'post-rotation-bundle-build.txt').read_text()
assert status.startswith('COMPLETE '), 'The actual main bundle build has not finished'
ready=read(W/'rotation-repair-ready-manifest.json');merge=read(W/'main-rotation-delivery.json')
assert merge['complete'] and merge['assetDatabaseImported'] and merge['clips']==873
assert merge['manifestHash']==digest(W/'rotation-repair-ready-manifest.json').hex()
readyTime=(B/'cards.bundle.editor-ready').read_text().strip()
assert utc(readyTime)>=utc(merge['completedUtc']), 'Editor ready marker predates these repairs'
cache={r['path']:r for r in read(B/'cards.bundle.editor-files.json')['files']}
rows=[]
for n,row in enumerate(ready['files'],1):
    for suffix,expected in [('',row['afterHash']),('.meta',row['metaHash'])]:
        rel=row['path']+suffix;p=P/rel;h=digest(p);st=p.stat()
        assert h.hex()==expected, 'Main file differs from verified repair: '+rel
        assert rel in cache and cache[rel]['hash']==base64.b64encode(h).decode(), 'Delivered cache does not cover current main file: '+rel
        assert cache[rel]['length']==st.st_size and cache[rel]['ticks']==st.st_mtime_ns//100+621355968000000000, 'Delivered file metadata is stale: '+rel
    rows.append(dict(path=row['path'],sha256=row['afterHash'],metaSha256=row['metaHash']))
    if n%100==0:print('VERIFIED MAIN AND DELIVERED CACHE',n,'/ 873',flush=True)
catalog=read(P/'Assets/DynamicCards/Content/catalog.json')['cards'];index=read(B/'cards.index.json')
prefabs=[p for part in index['parts'] for p in part['prefabs']]
assert len(catalog)==len(prefabs)==len(set(prefabs))==740
assert set(prefabs)=={c['prefab'] for c in catalog}, 'Catalog coverage differs from built bundle parts'
log=(W/'main-editor-delivery.log').read_text(errors='replace')
built={name:dict(bytes=int(size),reused=reused=='True',sourceHashReads=int(reads)) for name,size,reused,reads in re.findall(r'DYNAMIC_PART_READY (\S+\.bundle) bytes=(\d+) reused=(True|False) sourceHashReads=(\d+)',log)}
affected={str(Path(r['path']).parent/'Card.prefab').replace('\\','/') for r in ready['files']}
assert len(affected)==628 and affected.issubset(set(prefabs)), 'Repaired scene coverage differs from the actual bundle index'
for part in index['parts']:
    assert part['file'] in built, 'No completed build evidence for part: '+part['file']
    if set(part['prefabs']) & affected:assert not built[part['file']]['reused'], 'A part containing repaired animations was unexpectedly reused'
payload=[]
for name in ['cards.bundle','cards.index.json']+[p['file'] for p in index['parts']]:
    p=(B/name).resolve();assert p.is_relative_to(B.resolve())
    if name.endswith('.bundle'):
        assert name in built and built[name]['bytes']==p.stat().st_size
        stamp=(B/(name+'.inputs')).read_text()
        assert stamp.rsplit(':',1)[1]==str(p.stat().st_size), 'Bundle fingerprint size differs'
    payload.append(dict(file=name,bytes=p.stat().st_size,sha256=digest(p).hex()))
report=dict(complete=True,errors=[],verifiedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    mainRepairedClips=len(rows),catalogScenes=len(catalog),bundleParts=len(index['parts']),
    editorReadyUtc=readyTime,buildStatus=status,actualGameUiAccepted=False,
    payloadBytes=sum(r['bytes'] for r in payload),files=rows,payload=payload,buildParts=built)
(W/'post-rotation-delivery-verification.json').write_text(json.dumps(report,indent=2))
print('COMPLETE: 873 repaired main clips and metadata, 740 source scenes,',len(payload),'payload files; actual game UI acceptance remains separate',flush=True)

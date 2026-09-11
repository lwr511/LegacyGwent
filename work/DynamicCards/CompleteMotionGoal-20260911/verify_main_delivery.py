from pathlib import Path
import json,hashlib,base64,datetime
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');B=P/'Library/DynamicCardsBundles/StandaloneWindows64'
def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.digest()
assert (W/'main-bundle-build.txt').read_text().startswith('COMPLETE')
assert (B/'cards.bundle.editor-ready').is_file()
manifest=json.loads((W/'main-merge-manifest.json').read_text());assert manifest['applied']
cache={r['path']:r for r in json.loads((B/'cards.bundle.editor-files.json').read_text())['files']}
expected={r['path']:r['after'] for r in manifest['files']}
for r in json.loads((W/'final-source-findings-applied.json').read_text())['changes']:expected[Path(r['path']).relative_to(P).as_posix()]=r['after']
source=[]
for rel,sha in expected.items():
    actual=digest(P/rel);assert actual.hex()==sha,('SOURCE_CHANGED',rel)
    if rel.startswith(('Assets/DynamicCards/Content/','Assets/DynamicCards/Shaders/')) and rel in cache:
        assert base64.b64encode(actual).decode()==cache[rel]['hash'],('STALE_CACHE',rel)
    source.append(dict(path=rel,sha256=actual.hex(),inCacheManifest=rel in cache))
catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards'];index=json.loads((B/'cards.index.json').read_text())
prefabs=[p for part in index['parts'] for p in part['prefabs']]
assert len(prefabs)==len(set(prefabs))==len(catalog)
assert set(prefabs)=={c['prefab'] for c in catalog}
names=['cards.bundle','cards.index.json']+[r['file'] for r in index['parts']]
payload=[dict(file=n,bytes=(B/n).stat().st_size,sha256=digest(B/n).hex()) for n in names]
game=json.loads((W/'reported-main-ui/game-card-map.json').read_text(encoding='utf-8-sig'))['cards'];covered={a for c in catalog for a in c['artIds']};arts={r['art'] for r in game}
result=dict(complete=True,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),catalogScenes=len(catalog),currentGameArts=len(arts),mappedGameArts=len(arts&covered),unmapped=sorted(arts-covered),sourceFiles=source,payload=payload,totalBytes=sum(r['bytes'] for r in payload))
(W/'main-delivery-verification.json').write_text(json.dumps(result,indent=2));print('VERIFIED',len(source),'sources',len(payload),'payload files',result['totalBytes'],'bytes','mapped',result['mappedGameArts'])

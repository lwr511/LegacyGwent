from pathlib import Path
import sys,json,hashlib,base64,datetime
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import yaml
W=Path(__file__).resolve().parent;G=W.parent/'CompleteMotionGoal-20260911'
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');B=P/'Library/DynamicCardsBundles/StandaloneWindows64'
def digest(f):
    h=hashlib.sha256()
    with f.open('rb') as stream:
        for b in iter(lambda:stream.read(8*1024*1024),b''):h.update(b)
    return h.digest()
manifest=json.loads((G/'main-merge-manifest.json').read_text());stage=Path(manifest['stage'])
expected={r['path']:r['after'] for r in manifest['files']}
for r in json.loads((G/'final-source-findings-applied.json').read_text())['changes']:expected[Path(r['path']).relative_to(P).as_posix()]=r['after']
cache={r['path']:r for r in json.loads((B/'cards.bundle.editor-files.json').read_text())['files']}
rows=[];issues=[];normalized=[];shaderWhitespace=[]
for rel,old in expected.items():
    f=P/rel;got=digest(f);row=dict(path=rel,sha256=got.hex(),expectedSha256=old,byteIdentical=got.hex()==old)
    if got.hex()!=old:
        src=stage/rel
        if f.suffix=='.meta' and src.exists() and digest(src).hex()==old and yaml.safe_load(src.read_text())==yaml.safe_load(f.read_text()):
            normalized.append(rel);row['classification']='Unity metadata whitespace normalization; parsed content identical'
        elif f.suffix=='.shader' and src.exists() and digest(src).hex()==old:
            a=src.read_text().splitlines();b=f.read_text().splitlines()
            if len(a)==len(b) and all(x==y or (x.rstrip()==y.rstrip() and x.startswith(('void dc_v(', 'void dc_f('))) for x,y in zip(a,b)):
                shaderWhitespace.append(rel);row['classification']='Only trailing whitespace on shader function declaration lines changed'
            else:issues.append(dict(problem='changed-merged-source',path=rel))
        else:issues.append(dict(problem='changed-merged-source',path=rel))
    if rel.startswith(('Assets/DynamicCards/Content/','Assets/DynamicCards/Shaders/')) and rel in cache:
        if base64.b64encode(got).decode()!=cache[rel]['hash']:issues.append(dict(problem='cached-source-hash-mismatch',path=rel))
    rows.append(row)
catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text())['cards'];index=json.loads((B/'cards.index.json').read_text())
prefabs=[p for part in index['parts'] for p in part['prefabs']]
if len(prefabs)!=len(set(prefabs)) or set(prefabs)!={c['prefab'] for c in catalog}:issues.append(dict(problem='catalog-bundle-part-coverage'))
files=['cards.bundle','cards.index.json']+[p['file'] for p in index['parts']];payload=[]
for n,name in enumerate(files):
    f=B/name;payload.append(dict(file=name,bytes=f.stat().st_size,sha256=digest(f).hex()))
    if n%100==0:print('HASHED',n+1,'/',len(files),flush=True)
game=json.loads((G/'lands-baseline-20260912/game-card-map.json').read_text(encoding='utf-8-sig'))['cards'];arts={c['art'] for c in game};mapped={a for c in catalog for a in c['artIds']}
report=dict(complete=not issues,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),bundleReady=(B/'cards.bundle.editor-ready').read_text(),catalogScenes=len(catalog),gameArts=len(arts),mappedGameArts=len(arts&mapped),missingArtSources=sorted(arts-mapped),sourceFiles=rows,metadataNormalization=normalized,shaderWhitespaceNormalization=shaderWhitespace,issues=issues,payload=payload,totalBytes=sum(p['bytes'] for p in payload),scope='Main project baseline before September 12 quaternion repairs; not UI motion acceptance')
(W/'baseline-delivery-verification.json').write_text(json.dumps(report,indent=2))
print('COMPLETE',report['complete'],'sources',len(rows),'normalized metadata',len(normalized),'issues',issues,'payload',len(payload),'bytes',report['totalBytes'],flush=True)

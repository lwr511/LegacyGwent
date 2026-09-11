from pathlib import Path
import json,hashlib,base64,shutil,datetime
r=Path(__file__).resolve().parent;w=r.parent;main=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';probe=w/'Probe';cache=Path('Library/DynamicCardsBundles/StandaloneWindows64')
report=json.loads((r/'all-mapped-motion.json').read_text());assert report['complete'] and report['bundles']
assert len(report['rows'])==671
assert not [x for x in report['rows'] if x['status']=='needs-review'],'Unresolved frozen samples'
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for data in iter(lambda:f.read(1024*1024),b''):h.update(data)
    return base64.b64encode(h.digest()).decode()
src=probe/cache;dst=main/cache;index=json.loads((src/'cards.index.json').read_text());names=['cards.bundle','cards.index.json']+[x['file'] for x in index['parts']]
oldnames=json.loads((dst/'cards.index.json').read_text());oldparts=[x['file'] for x in oldnames['parts']]
assert all(digest(src/n)==digest(dst/n) for n in oldparts),'Existing scene package changed'
marker=dst/'cards.bundle.editor-ready';marker.unlink(missing_ok=True)
backup=r/'before-fallback-delivery';backup.mkdir(exist_ok=True)
for name in ['cards.bundle','cards.bundle.inputs','cards.bundle.manifest','cards.index.json','cards.bundle.editor-files.json','cards.source-hashes.json']:
    if (dst/name).exists():shutil.copy2(dst/name,backup/name)
changed=[]
fallback=Path('Assets/DynamicCards/Content/Fallback')
for source in (probe/fallback).rglob('*'):
    if not source.is_file():continue
    rel=source.relative_to(probe);target=main/rel
    assert not target.exists() or digest(source)==digest(target),'Unrelated existing fallback differs'
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target);changed.append(rel.as_posix())
folder_meta=Path(str(fallback)+'.meta');shutil.copy2(probe/folder_meta,main/folder_meta)
catalog=Path('Assets/DynamicCards/Content/catalog.json');shutil.copy2(main/catalog,backup/'catalog.json');shutil.copy2(probe/catalog,main/catalog);changed.append(catalog.as_posix())
for name in ['cards.bundle','cards.bundle.manifest','cards.index.json','cards-fallback-000.bundle','cards-fallback-000.bundle.manifest']:
    shutil.copy2(src/name,dst/name)
# These two packages were built directly; invalidate old build fingerprints, not the verified playback cache.
for name in ['cards.bundle.inputs','cards-fallback-000.bundle.inputs']:
    (dst/name).unlink(missing_ok=True);(src/name).unlink(missing_ok=True)
for project in [main,probe]:
    for name in ['cards.bundle.editor-files.json','cards.source-hashes.json']:
        path=project/cache/name;doc=json.loads(path.read_text(encoding='utf-8-sig'));rows={x['path']:x for x in doc['files']}
        for rel in changed:
            file=project/rel
            if file.suffix=='.meta' and Path(str(file)[:-5]).is_dir():continue
            stat=file.stat();row=dict(path=rel,hash=digest(file))
            if name=='cards.source-hashes.json':row.update(length=stat.st_size,ticks=621355968000000000+stat.st_mtime_ns//100)
            rows[rel]=row
        path.write_text(json.dumps(dict(files=list(rows.values()))),encoding='utf8')
assert all(digest(src/n)==digest(dst/n) for n in names)
marker.write_text(datetime.datetime.now(datetime.timezone.utc).isoformat())
result=dict(passed=True,files=len(names),bytes=sum((dst/n).stat().st_size for n in names),fallbackScenes=7,latestScenes=1279,mapped=650,unmapped=21,oldSceneBundlesUnchanged=True,sourceFiles=len(changed))
(r/'fallback-delivery.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))

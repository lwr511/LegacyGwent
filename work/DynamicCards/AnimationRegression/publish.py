from pathlib import Path
import json,hashlib,base64,shutil,datetime
root=Path(__file__).resolve().parent
repo=root.parents[2]
main=repo/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
probe=root.parent/'Probe'
cache=Path('Library/DynamicCardsBundles/StandaloneWindows64')
source=probe/cache;target=main/cache
assert json.loads((root/'runtime-result.json').read_text())['passed']
audit=json.loads((root/'bundle-audit.json').read_text())
assert audit['cards']==1279
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for data in iter(lambda:f.read(1024*1024),b''):h.update(data)
    return base64.b64encode(h.digest()).decode()
catalog='Assets/DynamicCards/Content/catalog.json'
assert digest(main/catalog)==digest(probe/catalog)
index=json.loads((source/'cards.index.json').read_text())
names=['cards.bundle','cards.index.json']+[p['file'] for p in index['parts']]
changed=[]
for name in names:
    assert Path(name).name==name
    if digest(source/name)!=digest(target/name):changed.append(name)
assert set(changed)<= {'cards.bundle','cards.index.json'},'Unexpected changed scene bundles'
marker=target/'cards.bundle.editor-ready'
marker.unlink(missing_ok=True)
backup=root/'cache-before';backup.mkdir(exist_ok=True)
for name in ['cards.bundle','cards.bundle.inputs','cards.bundle.manifest','cards.index.json']:
    if (target/name).exists():shutil.copy2(target/name,backup/name)
    if (source/name).exists():shutil.copy2(source/name,target/name)
for project in [main,probe]:
    for filename in ['cards.bundle.editor-files.json','cards.source-hashes.json']:
        path=project/cache/filename
        data=json.loads(path.read_text(encoding='utf-8-sig'))
        for row in data['files']:
            if row['path']!=catalog:continue
            stat=(project/catalog).stat();row['hash']=digest(project/catalog)
            if 'length' in row:row['length']=stat.st_size
            if 'ticks' in row:row['ticks']=621355968000000000+stat.st_mtime_ns//100
        path.write_text(json.dumps(data),encoding='utf8')
for name in names:assert digest(source/name)==digest(target/name)
marker.write_text(datetime.datetime.now(datetime.timezone.utc).isoformat())
(root/'delivery.json').write_text(json.dumps(dict(passed=True,files=len(names),changed=changed,sceneBundlesUnchanged=True,bytes=sum((target/n).stat().st_size for n in names)),indent=2))
print('DELIVERED',len(names),'files; changed',changed)

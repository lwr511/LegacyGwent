from pathlib import Path
import json,shutil,hashlib,base64,datetime
r=Path(__file__).resolve().parent;client=r.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
assert 'PREMIUM_BUNDLE_PASS cards=1995' in (r/'premium_full_bundle.log').read_text(errors='replace')
assert 'DYNAMIC_QUEUE_PASS' in (r/'premium_full_queue.log').read_text(errors='replace')
assert 'QUEUE_INFLIGHT_PART_CANCELLATION_PASS' in (r/'premium_full_queue.log').read_text(errors='replace')
assert json.loads((r/'client_card_coverage.json').read_text())['covered']==650
assert not json.loads((r/'client_guid_audit.json').read_text())['missing']
source=r/'Probe/Library/DynamicCardsBundles/StandaloneWindows64'
target=client/'Library/DynamicCardsBundles/StandaloneWindows64'
target.mkdir(parents=True,exist_ok=True)
staging=target/'install-staging';staging.mkdir(exist_ok=True)
index_path=source/'cards.index.json'
if not index_path.exists():index_path=staging/'cards.index.json'
if not index_path.exists():index_path=target/'cards.index.json'
index=json.loads(index_path.read_text());assert index['version']==1
names=['cards.bundle','cards.index.json']+[p['file'] for p in index['parts']]
assert len(set(names))==len(names) and all(Path(n).name==n for n in names)
assert sum(len(p['prefabs']) for p in index['parts'])==1995
snapshot=r/'client_editor_manifest_snapshot.json'
cached={f['path']:f for f in json.loads(snapshot.read_text())['files']} if snapshot.exists() else {}
files=[]
for folder in ['Content','Shaders']:
 for p in (client/'Assets/DynamicCards'/folder).rglob('*'):
  if not p.is_file() or (p.suffix=='.meta' and Path(str(p)[:-5]).is_dir()):continue
  relative=p.relative_to(client).as_posix();stat=p.stat();previous=cached.get(relative)
  if previous and (previous['size'],previous['mtime'])==(stat.st_size,stat.st_mtime_ns):digest=previous['hash']
  else:
   with p.open('rb') as stream:digest=base64.b64encode(hashlib.file_digest(stream,'sha256').digest()).decode()
  files.append(dict(path=relative,hash=digest))
backup=r/'BeforeFinalPremiumCache';backup.mkdir(exist_ok=True)
transaction=backup/'transaction.json'
if not transaction.exists():
 originals=[p.name for p in target.iterdir() if p.is_file() and (p.name.endswith('.bundle') or p.name in ['cards.index.json','cards.bundle.editor-ready','cards.bundle.editor-files.json'])]
 transaction.write_text(json.dumps(dict(originals=originals,new=names,backedUp=False)))
state=json.loads(transaction.read_text());assert state['new']==names
for name in names:
 if (source/name).exists():
  assert not (staging/name).exists(),name
  shutil.move(str(source/name),str(staging/name))
 if not state['backedUp']:assert (staging/name).exists(),name
if not state['backedUp']:
 for name in state['originals']:
  if not (backup/name).exists():shutil.move(str(target/name),str(backup/name))
 state['backedUp']=True;transaction.write_text(json.dumps(state))
for name in names:
 if (staging/name).exists():
  assert not (target/name).exists(),name
  shutil.move(str(staging/name),str(target/name))
 assert (target/name).exists(),name
(target/'cards.bundle.editor-files.json').write_text(json.dumps(dict(files=files),separators=(',',':')))
(target/'cards.bundle.editor-ready').write_text(datetime.datetime.now(datetime.timezone.utc).isoformat())
total=sum((target/n).stat().st_size for n in names)
(r/'installed_premium_cache.json').write_text(json.dumps(dict(path=str(target),bytes=total,manifestFiles=len(files),cards=1995,parts=len(index['parts']),payloadFiles=len(names)),indent=2))
print('PREMIUM_CACHE_INSTALLED',total,'files',len(files),'parts',len(index['parts']),flush=True)

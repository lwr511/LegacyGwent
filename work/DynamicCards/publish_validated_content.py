from pathlib import Path
import json,shutil,hashlib
root=Path(__file__).resolve().parent;source=root/'Probe/Assets/DynamicCards'
target=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards'
report=json.loads((root/'premium_structure_audit.json').read_text())
assert report['cards']==1995 and not report['issues'],'Run full structural validation first'
assert 'LATEST_COMPLETE_IMPORT_FINISHED' in (root/'latest_all_unity_import.log').read_text(errors='replace'),'Full source animation import is incomplete'
assert 'GERALT_TIMELINE_PASS' in (root/'geralt_timeline_final.log').read_text(errors='replace'),'Run the source timeline regression check first'
backup=root/'BeforeCompletePremiumImport';backup.mkdir(exist_ok=True)
changes=[]
def same(a,b):
 if a.stat().st_size!=b.stat().st_size:return False
 with a.open('rb') as left,b.open('rb') as right:
  while True:
   block=left.read(1024*1024)
   if block!=right.read(1024*1024):return False
   if not block:return True
def publish(path):
 relative=path.relative_to(source);destination=target/relative
 if destination.exists():
  if path.suffix=='.meta' and Path(str(path)[:-5]).is_dir():return
  if same(path,destination):return
  saved=backup/relative;saved.parent.mkdir(parents=True,exist_ok=True)
  if not saved.exists():shutil.copy2(destination,saved)
 if shutil.disk_usage(target).free < max(5*1024**3,path.stat().st_size+1024**3):
  raise RuntimeError('Copy paused before writing: less than reserved disk space remains; safe to resume this script.')
 destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,destination)
 changes.append(str(relative))
for folder in ['Content/Legacy2017','Content/Latest','Shaders']:
 for index,path in enumerate(sorted((source/folder).rglob('*'))):
  if path.is_file():publish(path)
  if index and index%5000==0:print('PUBLISH_PROGRESS',folder,index,flush=True)
 print('PUBLISHED_FOLDER',folder,flush=True)
# Publish the catalog last so the new bindings never point at a partially copied scene.
publish(source/'Content/catalog.json')
(root/'complete_premium_publish.json').write_text(json.dumps(dict(files=changes,cards=report['cards']),indent=2))
print('COMPLETE_PREMIUM_PUBLISH_DONE',len(changes),flush=True)

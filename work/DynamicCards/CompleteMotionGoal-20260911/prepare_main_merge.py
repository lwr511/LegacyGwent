from pathlib import Path
import json,hashlib,shutil,collections
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');H=W/'OgoImportHarness';backup=W/'MainMergeBackup';changes=json.loads((W/'staged-asset-change-inventory.json').read_text())['changed'];items={}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for r in changes:
 src=H/r['path'];dst=P/r['path'];assert sha(dst)==r['before'],('MAIN_CHANGED',r['path']);assert sha(src)==r['after'],('STAGE_CHANGED',r['path']);items[r['path']]=dict(path=r['path'],before=r['before'],after=r['after'],new=False)
 sm=Path(str(src)+'.meta');dm=Path(str(dst)+'.meta')
 assert sm.read_bytes()==dm.read_bytes(),('META_DRIFT',r['path'])
for relative in ['Assets/DynamicCards/Runtime/SourceParticles','Assets/DynamicCards/Runtime/SourceLightning','Assets/DynamicCards/Shaders/SourcePostFx','Assets/DynamicCards/Shaders/SourceCandles','Assets/DynamicCards/Shaders/SourceLightning','Assets/DynamicCards/Content/SourcePostFx','Assets/DynamicCards/Content/SourceCandles','Assets/DynamicCards/Content/SourceLightning']:
 folder=H/relative;assert folder.is_dir()
 for src in [Path(str(folder)+'.meta'),*folder.rglob('*')]:
  if not src.is_file():continue
  rel=src.relative_to(H).as_posix();assert not (P/rel).exists(),('NEW_PATH_EXISTS',rel);items[rel]=dict(path=rel,before=None,after=sha(src),new=True)
for name in ['DynamicCardPostEffect','DynamicCardBloom','DynamicCardGlitch','DynamicCardPostProcessRenderer']:
 for suffix in ['.cs','.cs.meta']:
  rel='Assets/DynamicCards/Runtime/'+name+suffix;src=H/rel;assert src.is_file() and not (P/rel).exists();items[rel]=dict(path=rel,before=None,after=sha(src),new=True)
for r in items.values():
 if r['new']:continue
 p=P/r['path'];dest=backup/r['path'];dest.parent.mkdir(parents=True,exist_ok=True)
 if dest.exists():assert sha(dest)==r['before'],('BACKUP_CHANGED',r['path'])
 else:shutil.copy2(p,dest)
 meta=Path(str(p)+'.meta')
 if meta.exists():shutil.copy2(meta,Path(str(dest)+'.meta'))
manifest=dict(main=str(P),stage=str(H),backup=str(backup),files=list(items.values()),applied=False)
(W/'main-merge-manifest.json').write_text(json.dumps(manifest,indent=2));print('BACKUP READY existing',sum(not r['new'] for r in items.values()),'new',sum(r['new'] for r in items.values()),'bytes',sum((H/r['path']).stat().st_size for r in items.values()))

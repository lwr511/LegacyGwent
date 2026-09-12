"""Apply the verified repair set while the main Unity editor is closed."""
from pathlib import Path
import json,hashlib,shutil,datetime,time
W=Path(__file__).resolve().parent
manifestPath=W/'rotation-repair-ready-manifest.json'
manifest=json.loads(manifestPath.read_text())
backup=json.loads((W/'rotation-original-backup-manifest.json').read_text())
P=Path(manifest['mainProject']).resolve();S=(W/'RepairStaging').resolve();B=Path(backup['backupRoot']).resolve()
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for chunk in iter(lambda:f.read(8*1024*1024),b''):h.update(chunk)
    return h.hexdigest()
def within(root,relative):
    target=(root/relative).resolve();assert target.is_relative_to(root),str(target);return target
manifestHash=digest(manifestPath)
assert manifest['ready'] and backup['complete'] and backup['readyManifestSha256']==manifestHash
assert manifest['clipCount']==backup['clipCount']==len(manifest['files'])==873
assert len({r['path'] for r in manifest['files']})==873
progress=W/'main-rotation-delivery-progress.txt'
started=time.time()
def step(phase,n):
    line=f'{phase} {n}/873 {datetime.datetime.now(datetime.timezone.utc).isoformat()}'
    progress.write_text(line);print(line,'elapsed',round(time.time()-started),flush=True)
step('PYTHON_PREFLIGHT',0)
for n,row in enumerate(manifest['files'],1):
    assert row['path'].startswith('Assets/DynamicCards/Content/') and row['path'].endswith('.anim')
    src=within(P,row['path']);stage=within(S,row['path']);original=within(B,row['path'])
    assert stage==Path(row['staged']).resolve()
    assert digest(src)==digest(original)==row['beforeHash'], 'Main original or backup changed: '+row['path']
    assert digest(stage)==row['afterHash'], 'Repair copy changed: '+row['path']
    for p in [src,stage,original]:assert digest(Path(str(p)+'.meta'))==row['metaHash'], 'GUID metadata changed: '+str(p)
    if n%50==0:step('PYTHON_PREFLIGHT',n)
report=dict(complete=False,startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    manifestHash=manifestHash,clips=0,paths=[],assetDatabaseImported=False,editorClosedDuringCopy=True)
changed=[]
try:
    for n,row in enumerate(manifest['files'],1):
        dst=within(P,row['path']);temp=dst.with_name(dst.name+'.rotation-repair-tmp')
        assert not temp.exists(), 'Unfinished copy needs inspection: '+str(temp)
        shutil.copy2(row['staged'],temp)
        assert digest(temp)==row['afterHash'], 'Copy hash mismatch: '+row['path']
        changed.append(row);temp.replace(dst)
        report['paths'].append(row['path']);report['clips']=n
        if n%50==0:step('COPIED',n)
    for row in manifest['files']:
        dst=within(P,row['path'])
        assert digest(dst)==row['afterHash'] and digest(Path(str(dst)+'.meta'))==row['metaHash'], 'Post-copy verification failed: '+row['path']
except Exception:
    for row in changed:
        dst=within(P,row['path']);shutil.copy2(within(B,row['path']),dst)
        assert digest(dst)==row['beforeHash'], 'Rollback verification failed: '+row['path']
    raise
report.update(complete=True,completedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(W/'main-rotation-delivery.json').write_text(json.dumps(report,indent=2))
step('COPIED_AND_VERIFIED',873)
print('Main animation repairs merged; Unity asset import and bundle rebuild remain pending.',flush=True)

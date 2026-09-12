"""Back up the exact audited originals; never write into the Unity project."""
from pathlib import Path
import json,hashlib,shutil,datetime,time
W=Path(__file__).resolve().parent
manifestPath=W/'rotation-repair-ready-manifest.json'
manifest=json.loads(manifestPath.read_text())
assert manifest['ready'] and not manifest['mergedIntoMain']
P=Path(manifest['mainProject']).resolve()
backup=(W/'BeforeRotationMerge').resolve()
assert backup.is_relative_to(W.resolve()) and not backup.is_relative_to(P)
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.hexdigest()
required=sum((P/r['path']).stat().st_size for r in manifest['files'] if not (backup/r['path']).exists())
assert shutil.disk_usage(W).free>required+512*1024*1024, 'Insufficient backup space'
report=dict(complete=False,startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    readyManifestSha256=digest(manifestPath),mainProject=str(P),backupRoot=str(backup),
    mainAnimationFilesModified=False,files=[])
output=W/'rotation-original-backup-manifest.json'
def save():
    temporary=output.with_suffix('.json.tmp');temporary.write_text(json.dumps(report,indent=2));temporary.replace(output)
started=time.time()
for i,row in enumerate(manifest['files']):
    record=dict(path=row['path'],originalHash=row['beforeHash'],metaHash=row['metaHash'])
    for suffix,expected in [('',row['beforeHash']),('.meta',row['metaHash'])]:
        source=(P/(row['path']+suffix)).resolve();dest=(backup/(row['path']+suffix)).resolve()
        assert source.is_relative_to(P) and dest.is_relative_to(backup)
        before=source.stat()
        assert digest(source)==expected, 'Current main file differs from the audited original: '+str(source)
        dest.parent.mkdir(parents=True,exist_ok=True)
        if not dest.exists():
            temporary=dest.with_name(dest.name+'.partial')
            assert not temporary.exists(), 'Unfinished prior copy needs inspection: '+str(temporary)
            shutil.copy2(source,temporary)
            assert digest(temporary)==expected, 'Backup copy hash mismatch: '+str(source)
            temporary.replace(dest)
        else:assert digest(dest)==expected, 'Existing backup hash mismatch: '+str(dest)
        after=source.stat()
        assert (before.st_size,before.st_mtime_ns)==(after.st_size,after.st_mtime_ns), 'Main source changed during backup'
    record['bytes']=(backup/row['path']).stat().st_size
    report['files'].append(record)
    if (i+1)%50==0 or i+1==len(manifest['files']):
        save();print('BACKED UP',i+1,'/',len(manifest['files']),'elapsed',round(time.time()-started),'seconds',flush=True)
report.update(complete=True,completedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              clipCount=len(report['files']),bytes=sum(r['bytes'] for r in report['files']))
save();print('COMPLETE',report['clipCount'],'original clips and metadata; main animations untouched',flush=True)

from pathlib import Path
import json, hashlib, shutil, datetime
W=Path(__file__).resolve().parent
mf=W/'main-merge-manifest.json'
m=json.loads(mf.read_text()); P=Path(m['main']); H=Path(m['stage'])
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
assert not m['applied']
for r in m['files']:
    p=P/r['path']; s=H/r['path']
    assert sha(s)==r['after'], ('STAGE_DRIFT', r['path'])
    assert (not p.exists()) if r['new'] else sha(p)==r['before'], ('MAIN_DRIFT',r['path'])
    if not r['new']: assert sha(Path(m['backup'])/r['path'])==r['before']
for r in m['files']:
    p=P/r['path']; p.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(H/r['path'],p)
for r in m['files']: assert sha(P/r['path'])==r['after'],r['path']
m['applied']=True; m['appliedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
mf.write_text(json.dumps(m,indent=2))
print('APPLIED AND HASH VERIFIED',len(m['files']))

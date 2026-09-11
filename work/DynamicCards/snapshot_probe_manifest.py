from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib,base64,json,datetime
r=Path(__file__).resolve().parent;client=r/'Probe'
def capture(p):
 before=p.stat()
 with p.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').digest()
 after=p.stat();assert (before.st_size,before.st_mtime_ns)==(after.st_size,after.st_mtime_ns),str(p)
 return dict(path=p.relative_to(client).as_posix(),hash=base64.b64encode(digest).decode(),size=after.st_size,mtime=after.st_mtime_ns)
paths=[p for folder in ['Content','Shaders'] for p in (client/'Assets/DynamicCards'/folder).rglob('*') if p.is_file() and not(p.suffix=='.meta' and Path(str(p)[:-5]).is_dir())]
with ThreadPoolExecutor(max_workers=4) as pool:files=list(pool.map(capture,paths))
(r/'probe_editor_manifest_snapshot.json').write_text(json.dumps(dict(files=files,created=datetime.datetime.now(datetime.timezone.utc).isoformat()),separators=(',',':')))
print('PROBE_MANIFEST_SNAPSHOT',len(files),flush=True)

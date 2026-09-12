"""Populate the existing build hash cache from freshly verified main files.

This only caches source SHA256 computations. Bundle fingerprints, bundle data,
delivery manifests and ready markers remain exclusively owned by the real build.
"""
from pathlib import Path
import json,hashlib,base64,shutil
W=Path(__file__).resolve().parent
ready=json.loads((W/'rotation-repair-ready-manifest.json').read_text())
delivery=json.loads((W/'main-rotation-delivery.json').read_text())
assert delivery['complete'] and delivery['clips']==873
P=Path(ready['mainProject'])
cachePath=P/'Library/DynamicCardsBundles/StandaloneWindows64/cards.source-hashes.json'
raw=cachePath.read_bytes();cache=json.loads(raw);entries={r['path']:r for r in cache['files']}
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.digest()
def ticks(st):return st.st_mtime_ns//100+621355968000000000
changedPaths={r['path'] for r in ready['files']}
matches=0
for row in cache['files']:
    if row['path'] in changedPaths:continue
    p=P/row['path']
    if p.exists():
        st=p.stat()
        if st.st_size==row['length'] and ticks(st)==row['ticks']:matches+=1
        if matches>=10:break
assert matches>=10, 'Cannot validate the existing timestamp convention'
updated=[]
for n,row in enumerate(ready['files'],1):
    p=P/row['path'];before=p.stat();h=digest(p);after=p.stat()
    assert h.hex()==row['afterHash'] and (before.st_mtime_ns,before.st_size)==(after.st_mtime_ns,after.st_size), 'Main repair changed during hashing'
    entry=dict(path=row['path'],hash=base64.b64encode(h).decode(),length=after.st_size,ticks=ticks(after))
    entries[row['path']]=entry;updated.append(entry)
    if n%100==0:print('VERIFIED BUILD HASH',n,'/ 873',flush=True)
assert cachePath.read_bytes()==raw, 'The build is concurrently changing its source cache'
backup=W/'cards.source-hashes.before-rotation-warm-cache.json'
if not backup.exists():backup.write_bytes(raw)
cache['files']=list(entries.values());temporary=cachePath.with_suffix('.json.tmp')
temporary.write_text(json.dumps(cache,separators=(',',':')));temporary.replace(cachePath)
(W/'verified-source-hash-cache-update.json').write_text(json.dumps(dict(complete=True,count=len(updated),
    cache=str(cachePath),beforeHash=hashlib.sha256(raw).hexdigest(),afterHash=digest(cachePath).hex(),
    bundleFingerprintsChanged=False,bundleDataChanged=False,deliveryManifestChanged=False,readyMarkerChanged=False,files=updated),indent=2))
print('Prepared',len(updated),'verified source hashes; actual bundle rebuild is still required',flush=True)

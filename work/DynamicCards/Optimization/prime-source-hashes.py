from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib,base64,json
r=Path(__file__).resolve().parent
probe=r.parent/'Probe'
paths=[p for p in (probe/'Assets').rglob('*') if p.is_file()]
def entry(p):
    before=p.stat()
    with p.open('rb') as f:h=hashlib.file_digest(f,'sha256').digest()
    after=p.stat()
    assert before.st_mtime_ns==after.st_mtime_ns and before.st_size==after.st_size
    return {'path':p.relative_to(probe).as_posix(),'hash':base64.b64encode(h).decode(),'length':after.st_size,'ticks':621355968000000000+after.st_mtime_ns//100}
items=[]
with ThreadPoolExecutor(max_workers=6) as pool:
    for i,item in enumerate(pool.map(entry,paths),1):
        items.append(item)
        if i%10000==0:print('HASHED',i,'/',len(paths),flush=True)
(probe/'Library/DynamicCardsBundles/StandaloneWindows64/cards.source-hashes.json').write_text(json.dumps({'files':items}),encoding='utf8')
print('SOURCE_HASH_CACHE_READY',len(items),flush=True)

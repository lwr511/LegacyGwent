from pathlib import Path
import re,json,shutil
from concurrent.futures import ThreadPoolExecutor
root=Path(__file__).resolve().parent;probe=root/'Probe'
audit=json.loads((root/'probe_guid_audit.json').read_text())
bases=[(root/'LatestBridge2022/Assets/PortableCards',probe/'Assets/DynamicCards/Content/Latest'),(root/'LegacyShaders',probe/'Assets/DynamicCards/Shaders/Legacy')]
def read_meta(pair):
 path,base,target=pair;m=re.search(r'^guid: ([a-f0-9]{32})',path.read_text(encoding='utf8'),re.M)
 return (m[1],(Path(str(path)[:-5]),base,target)) if m else None
jobs=[(p,base,target) for base,target in bases for p in base.rglob('*.meta')]
with ThreadPoolExecutor(max_workers=12) as pool:index=dict(v for v in pool.map(read_meta,jobs) if v)
copied=set();missing=[]
def copy_guid(guid):
 if guid in copied:return
 copied.add(guid)
 if guid not in index:missing.append(guid);return
 path,base,target=index[guid];destination=target/path.relative_to(base)
 if destination.exists():return
 destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,destination);shutil.copy2(str(path)+'.meta',str(destination)+'.meta')
 if path.suffix in ['.controller','.prefab','.mat','.asset']:
  for ref in set(re.findall(r'guid: ([a-f0-9]{32})',path.read_text(encoding='utf8'))):
   if ref in index:copy_guid(ref)
 print('DEPENDENCY_RESTORED',destination.relative_to(probe),flush=True)
for guid,path in audit['missing']:
 if 'Latest' in path or 'Legacy2017' in path:copy_guid(guid)
(root/'dependency_restore.json').write_text(json.dumps(dict(resolvedGuids=sorted(copied-set(missing)),unresolvedGuids=missing),indent=2))
print('DEPENDENCY_RESTORE_DONE',len(copied),'unresolved',len(missing),flush=True)

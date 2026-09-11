from pathlib import Path
import re,json,collections,sys
from concurrent.futures import ThreadPoolExecutor
root=Path(__file__).resolve().parent;client=root/'Probe'
if '--client' in sys.argv:client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
def meta(path):
 m=re.search(r'^guid: ([a-f0-9]{32})',path.read_text(encoding='utf8'),re.M)
 return (m[1],str(path.relative_to(client))) if m else None
with ThreadPoolExecutor(max_workers=12) as pool:
 pairs=[p for p in pool.map(meta,(client/'Assets').rglob('*.meta')) if p]
ids=collections.defaultdict(list)
for guid,path in pairs:ids[guid].append(path)
def check(path):
 refs=set(re.findall(r'guid: ([a-f0-9]{32})',path.read_text(encoding='utf8')))
 return [(guid,str(path.relative_to(client))) for guid in refs if not guid.startswith('00000000') and guid not in ids]
paths=[p for p in (client/'Assets/DynamicCards/Content').rglob('*') if p.suffix in ['.prefab','.mat','.controller']]
with ThreadPoolExecutor(max_workers=8) as pool:
 missing=[item for result in pool.map(check,paths) for item in result]
result=dict(files=len(paths),missing=missing,duplicateGuids={g:p for g,p in ids.items() if len(p)>1})
(root/('client_guid_audit.json' if '--client' in sys.argv else 'probe_guid_audit.json')).write_text(json.dumps(result,indent=2))
print('CONTENT_GUID_AUDIT files',len(paths),'missing',len(missing),'duplicateGuids',len(result['duplicateGuids']),flush=True)

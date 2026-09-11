from pathlib import Path
import json,re,hashlib
root=Path(__file__).resolve().parent
client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
content=(client/'Assets/DynamicCards/Content').resolve()
probe=(root/'Probe/Assets/DynamicCards/Content').resolve()
plan=json.loads((root/'prune_probe_baked_plan.json').read_text())
candidate={content/Path(item['path']).resolve().relative_to(probe):Path(item['backup']) for item in plan}
references=set()
for folder in [client/'Assets']:
 for path in folder.rglob('*'):
  if path.suffix not in ['.prefab','.unity','.controller','.asset'] or path in candidate:continue
  if path.suffix=='.asset' and content in path.parents:continue
  references.update(re.findall(r'guid: ([a-f0-9]{32})',path.read_text(encoding='utf8',errors='ignore')))
def digest(path):
 h=hashlib.sha256()
 # Unity rewrites Windows line endings without changing the serialized asset.
 with path.open('r',encoding='utf8',newline=None) as f:
  for b in iter(lambda:f.read(4*1024*1024),''):h.update(b.encode('utf8'))
 return h.hexdigest()
removed=[];retained=[]
for path,backup in candidate.items():
 path=path.resolve();assert content in path.parents
 if not path.exists():continue
 meta=Path(str(path)+'.meta');guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1]
 if guid in references or not backup.exists() or digest(path)!=digest(backup):retained.append(str(path));continue
 size=path.stat().st_size
 # Exact backup and reference check precede each deletion. No directory is removed.
 path.unlink();meta.unlink();removed.append(dict(path=str(path),backup=str(backup),bytes=size))
(root/'primary_replaced_clip_cleanup.json').write_text(json.dumps(dict(removed=removed,retained=retained),indent=2))
print('PRUNED',len(removed),'bytes',sum(x['bytes'] for x in removed),'RETAINED',len(retained))

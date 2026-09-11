"""Consolidate byte-identical PNGs with identical import settings in the build probe."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib,json,re,collections
root=Path(__file__).resolve().parent
repo=root.parents[2]
main=repo/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
probe=root.parent/'Probe'
content=probe/'Assets/DynamicCards/Content/Latest'
groups=collections.defaultdict(list)
def signature(p):
    meta=Path(str(p)+'.meta').read_text(encoding='utf8')
    normalized=re.sub(r'^guid: .*\n','',meta,flags=re.M)
    return (hashlib.sha256(p.read_bytes()).hexdigest(),normalized)
files=sorted((main/'Assets/DynamicCards/Content/Latest').rglob('*.png'))
with ThreadPoolExecutor(max_workers=6) as pool:
    for p,key in zip(files,pool.map(signature,files)):groups[key].append(p)
guid_map={};path_map={};removed=[]
def guid(p):return re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
for items in groups.values():
    canonical=items[0];canonical_path=canonical.relative_to(main).as_posix()
    for p in items[1:]:
        original=p.relative_to(main).as_posix();path_map[original]=canonical_path
        guid_map[guid(p).encode()]=guid(canonical).encode();removed.append(original)
assert guid_map and len(guid_map)==len(removed)
pattern=re.compile(rb'guid: ([a-f0-9]{32})')
changed=[]
def atomic_write(p,data):
    assert content.resolve() in p.resolve().parents
    temp=p.with_name(p.name+'.dedup-tmp');temp.write_bytes(data);temp.replace(p)
for p in content.rglob('*'):
    if p.suffix in {'.prefab','.mat','.controller','.anim','.asset'}:
        data=p.read_bytes()
        if not any(m[1] in guid_map for m in pattern.finditer(data)):continue
        assert data.startswith(b'%YAML'),'Unexpected referenced binary asset: '+str(p)
        replacement=pattern.sub(lambda m:b'guid: '+guid_map.get(m[1],m[1]),data)
        atomic_write(p,replacement);changed.append(p.relative_to(probe).as_posix())
    elif p.suffix=='.json':
        def rewrite(value):
            if isinstance(value,str):return path_map.get(value,value)
            if isinstance(value,list):return [rewrite(v) for v in value]
            if isinstance(value,dict):return {k:rewrite(v) for k,v in value.items()}
            return value
        before=json.loads(p.read_text(encoding='utf8'));after=rewrite(before)
        if before!=after:atomic_write(p,json.dumps(after,indent=2).encode());changed.append(p.relative_to(probe).as_posix())
# Break possible hardlinks before Unity updates canonical importer sidecars.
for p in content.rglob('*.png.meta'):
    atomic_write(p,p.read_bytes())
plan=dict(beforePng=len(files),afterPng=len(files)-len(removed),removed=removed,changed=changed,pathMap=path_map,guidMap={k.decode():v.decode() for k,v in guid_map.items()})
(root/'texture-dedup.json').write_text(json.dumps(plan,indent=2),encoding='utf8')
print('DEDUP_REWIRED',len(removed),'textures;',len(changed),'changed assets',flush=True)

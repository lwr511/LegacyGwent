from pathlib import Path
import hashlib,shutil,json
base=Path(__file__).resolve().parent
primary=base.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content'
probe=base/'Probe/Assets/DynamicCards/Content'
changed=[]
# Only update existing production assets or generated procedural dependencies.
# Probe-only raw shader copies and diagnostic assets must never enter the client.
paths={p.relative_to(primary) for p in primary.rglob('*') if p.is_file()}
for p in probe.rglob('*'):
    if p.is_file() and ('Generated' in p.relative_to(probe).parts or p.name=='Generated.meta'):
        paths.add(p.relative_to(probe))
for relative in sorted(paths):
    src=probe/relative;dst=primary/relative
    if not src.is_file():continue
    if dst.exists() and src.read_bytes()==dst.read_bytes():continue
    dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst);changed.append(str(relative))
(base/'prepared_content_sync.json').write_text(json.dumps(changed,indent=2))
print('PREPARED_CONTENT_SYNC',len(changed))

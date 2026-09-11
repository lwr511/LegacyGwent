from pathlib import Path
import json,re,shutil,hashlib

work=Path(__file__).resolve().parent
project=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
backup=work.parent/'BeforeOldSources-20260908/Content'
stage=work/'Restore'
candidates=json.loads((work/'missing-mapping-candidates.json').read_text())
old=json.loads((backup/'catalog.json').read_text(encoding='utf-8-sig'))
wanted={c['id']:c['arts'] for c in candidates}
entries=[dict(c,artIds=wanted[c['id']]) for c in old['cards'] if c['id'] in wanted]
guid_pattern=re.compile(rb'guid:\s*([0-9a-f]{32})')
index={};duplicates=[]
for root,prefix,priority in [(backup,'Assets/DynamicCards/Content',0),(project/'Assets','Assets',1)]:
    count=0
    for meta in root.rglob('*.meta'):
        match=guid_pattern.search(meta.read_bytes());asset=Path(str(meta)[:-5])
        if not match or not asset.is_file():continue
        guid=match[1].decode();relative=prefix+'/'+asset.relative_to(root).as_posix()
        if guid in index and index[guid][1]!=relative and priority==index[guid][2]:duplicates.append([guid,index[guid][1],relative])
        if guid not in index or priority>=index[guid][2]:index[guid]=(asset,relative,priority)
        count+=1
    print('Indexed',root,count,flush=True)
assert not duplicates,duplicates[:10]
paths={v[1]:v for v in index.values()}
pending=[p for c in entries for p in [c['prefab'],c.get('audio')] if p]
visited=set();missing=[];copies=[]
while pending:
    relative=pending.pop()
    if relative in visited:continue
    visited.add(relative)
    item=paths.get(relative)
    if not item:missing.append(relative);continue
    source,relative,priority=item
    if source.suffix.lower() in ['.prefab','.anim','.controller','.mat','.asset']:
        raw=source.read_bytes()
        if raw.startswith(b'%YAML'):
            for guid in set(guid_pattern.findall(raw)):
                if guid.startswith(b'0000000000000000'):continue
                dependency=index.get(guid.decode())
                if dependency:pending.append(dependency[1])
                else:missing.append(relative+' -> '+guid.decode())
    if priority==0:
        target=stage/relative;target.parent.mkdir(parents=True,exist_ok=True)
        for f in [source,Path(str(source)+'.meta')]:
            dest=target if f==source else Path(str(target)+'.meta')
            if not dest.exists():shutil.copy2(f,dest)
        copies.append({'path':relative,'bytes':source.stat().st_size})
report={'entries':len(entries),'artIds':sum(len(e['artIds']) for e in entries),'dependencies':len(visited),'files':copies,'bytes':sum(c['bytes'] for c in copies),'missing':sorted(set(missing))}
(work/'restore-plan.json').write_text(json.dumps(report,indent=2))
(work/'restore-entries.json').write_text(json.dumps(entries,indent=2))
print({k:v for k,v in report.items() if k!='files'},flush=True)
assert not missing,'Unresolved dependencies: review before installing'

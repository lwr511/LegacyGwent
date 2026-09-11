from pathlib import Path
import json,re,shutil,hashlib,sys
sys.stdout.reconfigure(encoding='utf-8')
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');H=W/'OgoImportHarness';H.mkdir(exist_ok=True)
reference=W.parent/'MotionIntegrity/FinalHarness'
for folder in ['ProjectSettings','Packages']:shutil.copytree(reference/folder,H/folder,dirs_exist_ok=True)
contracts=json.loads((W/'ogo-animation-contracts.json').read_text())['records']
guids={}
for meta in (P/'Assets/DynamicCards').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)
    if m:guids[m[1]]=Path(str(meta)[:-5])
pending=[P/r['prefab'] for r in contracts]
pending.extend(f for f in (P/'Assets/DynamicCards/Runtime').glob('*.cs') if f.name!='DynamicCardSettingRow.cs')
pending.extend((P/'Assets/DynamicCards/Resources').glob('*.*'))
seen=set();files=[];missing=set()
while pending:
    f=pending.pop()
    if f in seen or f.suffix=='.meta':continue
    seen.add(f)
    if not f.is_file():continue
    relative=f.relative_to(P);target=H/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,target)
    meta=Path(str(f)+'.meta')
    if meta.exists():shutil.copy2(meta,Path(str(target)+'.meta'))
    if f.suffix in ['.prefab','.controller','.anim','.asset','.mat']:
        for guid in set(re.findall(rb'guid: ([a-f0-9]{32})',f.read_bytes())):
            guid=guid.decode()
            if guid.startswith('0000000000'):continue
            dep=guids.get(guid)
            if dep:pending.append(dep)
            else:missing.add(guid)
    files.append({'path':str(relative).replace('\\','/'),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size})
for r in contracts:
    f=P/r['prefab'];shutil.copy2(f.parent/'conversion.json',(H/r['prefab']).parent/'conversion.json')
(H/'Assets/Editor').mkdir(exist_ok=True)
(W/'ogo-harness-baseline.json').write_text(json.dumps({'files':files,'missingGuids':sorted(missing)},indent=2))
print('COPIED',len(files),'bytes',sum(r['bytes'] for r in files),'missing',missing,flush=True)
assert not missing

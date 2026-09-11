from pathlib import Path
import json,re,shutil,hashlib,subprocess,sys
sys.stdout.reconfigure(encoding='utf-8')
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');H=W/'OgoImportHarness'
index=W/'main-guid-index.json'
guids={k:Path(v) for k,v in json.loads(index.read_text()).items()} if index.exists() else {}
if len(guids)<1000:
    result=subprocess.run(['rg','--no-ignore','--no-heading','-n','^guid: ','-g','*.meta',str(P/'Assets/DynamicCards')],capture_output=True,check=True,encoding='utf-8')
    guids={guid:Path(path[:-5]) for path,guid in re.findall(r'^(.*?):\d+:guid: ([a-f0-9]{32})$',result.stdout,re.M)}
    index.write_text(json.dumps({k:str(v) for k,v in guids.items()}))
contracts=json.loads(Path(sys.argv[1]).read_text())['records'];pending=[P/r['prefab'] for r in contracts];seen=set();files=[];missing=set()
while pending:
    f=pending.pop()
    if f in seen:continue
    seen.add(f)
    if not f.is_file():continue
    target=H/f.relative_to(P)
    copied=not target.exists()
    if copied:
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,target)
        meta=Path(str(f)+'.meta')
        if meta.exists():shutil.copy2(meta,Path(str(target)+'.meta'))
    if f.suffix in ['.prefab','.controller','.anim','.asset','.mat']:
        for guid in set(re.findall(rb'guid: ([a-f0-9]{32})',f.read_bytes())):
            guid=guid.decode()
            if guid.startswith('0000000000'):continue
            dep=guids.get(guid)
            if dep:pending.append(dep)
            else:missing.add(guid)
    if copied:files.append({'path':str(f.relative_to(P)).replace('\\','/'),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size})
for r in contracts:
    f=P/r['prefab'];shutil.copy2(f.parent/'conversion.json',(H/r['prefab']).parent/'conversion.json')
baseline=W/'ogo-harness-baseline.json';data=json.loads(baseline.read_text());data['files'].extend(files);data['missingGuids']=sorted(missing);baseline.write_text(json.dumps(data,indent=2))
print('EXTRA copied',len(files),'bytes',sum(r['bytes'] for r in files),'missing',missing,flush=True);assert not missing

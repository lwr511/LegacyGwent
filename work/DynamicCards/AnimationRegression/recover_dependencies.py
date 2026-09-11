from pathlib import Path
import subprocess,re,json,shutil,hashlib,base64,datetime
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');main=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';probe=w/'Probe';r=w/'AnimationRegression'
def index(root):
    out=subprocess.run(['rg','--no-heading','^guid: ', 'Assets','-g','*.meta'],cwd=root,capture_output=True,text=True,encoding='utf8',check=True).stdout
    return {line.rsplit(':guid: ',1)[1].strip():root/line.rsplit(':guid: ',1)[0] for line in out.splitlines()}
m=index(main);p=index(probe);todo=[x[0] for x in json.loads((w/'client_guid_audit.json').read_text())['missing']];added=[];unresolved=[]
while todo:
    guid=todo.pop()
    if guid in m or guid.startswith('00000000'):continue
    meta=p.get(guid)
    if not meta:unresolved.append(guid);continue
    source=Path(str(meta)[:-5]);assert source.is_file(),str(source)
    target=main/'Assets/DynamicCards/Content/Fallback/Recovered'/guid/source.name
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target);shutil.copy2(meta,str(target)+'.meta');m[guid]=Path(str(target)+'.meta');added.extend([target,Path(str(target)+'.meta')])
    if source.suffix in ['.prefab','.mat','.controller','.anim','.asset']:
        todo.extend(re.findall(r'guid: ([a-f0-9]{32})',source.read_text(encoding='utf8',errors='ignore')))
assert not unresolved,unresolved
cache=main/'Library/DynamicCardsBundles/StandaloneWindows64'
for name in ['cards.bundle.editor-files.json','cards.source-hashes.json']:
    path=cache/name;doc=json.loads(path.read_text(encoding='utf-8-sig'));rows={x['path']:x for x in doc['files']}
    for file in added:
        rel=file.relative_to(main).as_posix();stat=file.stat();row=dict(path=rel,hash=base64.b64encode(hashlib.sha256(file.read_bytes()).digest()).decode())
        if name=='cards.source-hashes.json':row.update(length=stat.st_size,ticks=621355968000000000+stat.st_mtime_ns//100)
        rows[rel]=row
    path.write_text(json.dumps(dict(files=list(rows.values()))),encoding='utf8')
(cache/'cards.bundle.editor-ready').write_text(datetime.datetime.now(datetime.timezone.utc).isoformat())
result=dict(files=len(added),bytes=sum(f.stat().st_size for f in added),unresolved=unresolved,paths=[f.relative_to(main).as_posix() for f in added]);(r/'fallback-dependency-recovery.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='paths'}))
missing=[]
paths=[f for f in (main/'Assets/DynamicCards/Content/Fallback').rglob('*') if f.suffix in ['.prefab','.mat','.controller','.anim']]
for f in paths:
    for guid in set(re.findall(r'guid: ([a-f0-9]{32})',f.read_text(encoding='utf8',errors='ignore'))):
        if not guid.startswith('00000000') and guid not in m:missing.append([str(f.relative_to(main)),guid])
(r/'fallback-final-guid-audit.json').write_text(json.dumps(dict(files=len(paths),missing=missing),indent=2));assert not missing,missing[:10];print('FALLBACK_FINAL_GUID_AUDIT',len(paths),'missing',len(missing))

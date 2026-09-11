from pathlib import Path
import json,re,shutil,concurrent.futures
W=Path(__file__).resolve().parent;S=W/'OldSourcesStage';P=W.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';probe=W/'Probe'
report=json.loads((S/'report.json').read_text());needed={s.split()[1] for s in report['missing'] if s.startswith('GUID ')}
index={}
def read(p):
 m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)
 return (m[1],Path(str(p)[:-5])) if m else None
paths=list((probe/'Assets/PortableCards').rglob('*.meta'))+list((S/'Assets').rglob('*.meta'))+list((P/'Assets/DynamicCards/Runtime').glob('*.meta'))+list((P/'Assets/DynamicCards/Shaders').rglob('*.meta'))
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
 for pair in pool.map(read,paths):
  if pair:index[pair[0]]=pair[1]
remap={'3e080548dacc61344a5969d8317f5acc':read(P/'Assets/DynamicCards/Runtime/DynamicCardAnimatedMaterialProperty.cs.meta')[0]}
done=set();missing=[];copied=[]
def copy(guid):
 if guid in remap or guid in done or guid.startswith('00000000'):return
 done.add(guid);asset=index.get(guid)
 if not asset:missing.append(guid);return
 if asset.is_relative_to(S) or asset.is_relative_to(P):return
 target=S/'Assets/DynamicCards/Content/Old/Thronebreaker/Dependencies'/asset.relative_to(probe/'Assets/PortableCards')
 target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(asset,target);shutil.copy2(Path(str(asset)+'.meta'),Path(str(target)+'.meta'));copied.append(str(target))
 try:body=asset.read_text(encoding='utf-8-sig')
 except UnicodeDecodeError:return
 for child in set(re.findall(r'guid: (\w{32})',body)):copy(child)
for g in needed:copy(g)
changed=[]
for path in (S/'Assets').rglob('*.anim'):
 body=path.read_text(encoding='utf-8-sig');new=body
 for old,current in remap.items():new=new.replace(old,current)
 if new!=body:path.write_text(new,encoding='utf-8');changed.append(str(path))
(S/'dependency-completion.json').write_text(json.dumps(dict(copied=copied,remapped=changed,missing=missing),indent=2));print('COMPLETE',len(copied),len(changed),'MISSING',missing,flush=True)

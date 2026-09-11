from pathlib import Path
import json,re,zlib,mmap
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity');p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');ids={x['id'] for x in json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text())['cards'] if '/Old/Legacy2017/' in x['prefab']};records=json.loads((w.parent/'LegacyAnimationData/animations.json').read_text())['animators'];rows=[];missing=[];checked=0;seen={}
for r in records:
 if r['id'] not in ids:continue
 prefix='Source_'+format(zlib.crc32(r['path'].encode()),'08x');folder=p/'Assets/DynamicCards/Content/Old/Legacy2017'/r['id']
 for layer in r.get('layers',[]):
  for state in layer['states']:
   for clip in state['clips']:
    safe=''.join(c if c.isalnum() or c in '_-' else '_' for c in clip);f=folder/(prefix+'_'+safe+'.anim')
    if not f.exists():f=folder/('Global_'+prefix+'_'+safe+'.anim')
    if not f.exists():missing.append([r['id'],r['path'],clip]);continue
    if str(f) in seen:
     if seen[str(f)]!=state['loop']:missing.append([r['id'],r['path'],clip,'conflicting state loop flags'])
     continue
    seen[str(f)]=state['loop'];checked+=1
    with f.open('rb') as stream:
     with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as b:
      match=re.search(rb'm_LoopTime: ([01])',b);assert match,str(f);actual=bool(int(match[1]))
    if actual!=state['loop']:rows.append({'scene':r['id'],'path':r['path'],'clip':clip,'file':str(f),'before':actual,'after':state['loop']})
report={'checked':checked,'changes':rows,'missing':missing};(w/'legacy-loop-audit.json').write_text(json.dumps(report,indent=2));print('checked',checked,'mismatches',len(rows),'unmatched',len(missing));print(rows[:6]);print(missing[:6])

from pathlib import Path
import json,re,zlib,mmap
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity');p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');ids={x['sourceId'] if 'sourceId' in x else x['id'] for x in json.loads((w/'restore-entries.json').read_text())};records=json.loads((w.parent/'LatestAnimationData/animations.json').read_text())['animators'];rows=[];missing=[];checked=0;seen={}
for r in records:
 if r['id'] not in ids:continue
 prefix='Source_'+format(zlib.crc32(r['path'].encode()),'08x');folder=p/'Assets/DynamicCards/Content/Latest'/r['id']
 for layer in r.get('layers',[]):
  for state in layer['states']:
   for clip in state['clips']:
    safe=''.join(c if c.isalnum() or c in '_-' else '_' for c in clip);f=folder/(prefix+'_'+safe+'.anim')
    if not f.exists():f=folder/('Global_'+prefix+'_'+safe+'.anim')
    if not f.exists():missing.append([r['id'],r['path'],clip]);continue
    if str(f) in seen:assert seen[str(f)]==state['loop'];continue
    seen[str(f)]=state['loop'];checked+=1
    with f.open('rb') as stream:
     with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as b:
      match=re.search(rb'm_LoopTime: ([01])',b);assert match,str(f);actual=bool(int(match[1]))
    if actual!=state['loop']:rows.append({'scene':r['id'],'path':r['path'],'clip':clip,'file':str(f),'before':actual,'after':state['loop']})
report={'checked':checked,'changes':rows,'missing':missing};(w/'latest-loop-audit.json').write_text(json.dumps(report,indent=2));print('checked',checked,'mismatches',len(rows),'unmatched',len(missing));print(rows[:6]);print(missing[:6])

import json,re,zlib
from pathlib import Path
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');p=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';out=w/'TimingRepair';out.mkdir(exist_ok=True);catalog=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text())['cards'];changes=[];ambiguous=[];missing=[];checked=0
for source,folder in [('Thronebreaker','NativeAnimationData'),('Legacy2017','LegacyAnimationData')]:
 active={c['id']:p/c['prefab'] for c in catalog if c['sourceVersion']==source}
 for rec in json.loads((w/folder/'animations.json').read_text())['animators']:
  if rec['id'] not in active:continue
  expected={}
  for layer in rec.get('layers',[]):
   for state in layer['states']:
    for clip in state['clips']:expected.setdefault(clip,set()).add(state['loop'])
  prefix='Source_'+format(zlib.crc32(rec['path'].encode())&0xffffffff,'08x')
  for clip,flags in expected.items():
   if len(flags)!=1:ambiguous.append([source,rec['id'],rec['path'],clip,list(flags)]);continue
   safe=re.sub(r'[^a-zA-Z0-9_\-]','_',clip)
   candidates=list(active[rec['id']].parent.glob('*'+prefix+'_'+safe+'.anim'))
   if len(candidates)!=1:missing.append([rec['id'],prefix,clip,len(candidates)]);continue
   f=candidates[0];m=re.search(r'm_LoopTime: (\d+)',f.read_text());checked+=1
   if m and bool(int(m[1]))!=next(iter(flags)):changes.append(dict(source=source,id=rec['id'],path=rec['path'],clip=clip,file=str(f),before=int(m[1]),after=int(next(iter(flags)))))
result=dict(checked=checked,changes=changes,ambiguous=ambiguous,missing=missing);(out/'loop-audit.json').write_text(json.dumps(result,indent=2));print('checked',checked,'changed',len(changes),'cards',len({c['id'] for c in changes}),'ambiguous',len(ambiguous),'missing',len(missing));print(json.dumps(changes[:12],ensure_ascii=False));print('missing',missing[:6])

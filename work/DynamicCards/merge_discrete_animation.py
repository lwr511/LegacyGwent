from pathlib import Path
import json,shutil,struct,zlib,math
r=Path(__file__).resolve().parent
fresh=json.loads((r/'DiscreteAnimationData/animations.json').read_text())
original=r/'LatestAnimationData/animations.json';doc=json.loads(original.read_text())
backup=r/'BeforeDiscreteAnimationRepair';backup.mkdir(exist_ok=True)
shutil.copy2(original,backup/'animations.json')
names=json.loads((r/'raw_1738_materials.json').read_text())
conversion=json.loads((r/'Probe/Assets/DynamicCards/Content/Latest/17380101/conversion.json').read_text())
for record in fresh['animators']:
 for clip in record['clips']:
  source=r/'DiscreteAnimationData'/clip['file'];dest=r/'LatestAnimationData'/clip['file']
  if dest.exists():shutil.copy2(dest,backup/dest.name)
  shutil.copy2(source,dest)
  if clip['name'] not in ('VFXIntro','VFXLoop'):continue
  pathid=17 if clip['name']=='VFXIntro' else 16
  raw=json.loads((r/('raw_1738_'+clip['name']+'_'+str(pathid)+'.json')).read_text())
  stream=raw['m_MuscleClip']['m_Clip']['data']['m_StreamedClip'];buf=struct.pack('<%dI'%len(stream['data']),*stream['data']);pos=0;curves={}
  while pos+8<=len(buf):
   time,count=struct.unpack_from('<fi',buf,pos);pos+=8
   for _ in range(count):
    index,*values=struct.unpack_from('<i4f',buf,pos);pos+=20
    curves.setdefault(index,[]).append((time,values[3]))
  offset=0;clip['pointerTracks']=[]
  for binding in raw['m_ClipBindingConstant']['genericBindings']:
   if binding['isPPtrCurve']:
    paths={t['path'] for t in clip['tracks'] if zlib.crc32(t['path'].encode())==binding['path']};assert len(paths)==1
    keys={}
    for time,value in curves[offset]:
     if time>clip['duration']:continue
     ptr=raw['m_ClipBindingConstant']['pptrCurveMapping'][round(value)]
     options=[m['asset'] for m in conversion['materials'] if m['originalName']==names[str(ptr['m_PathID'])]];assert len(options)==1
     keys[max(0,time-raw['m_MuscleClip']['m_StartTime'])]=options[0]
    clip['pointerTracks'].append(dict(path=paths.pop(),slot=binding['attribute'],keys=[dict(time=t,asset=a) for t,a in sorted(keys.items())]))
   offset+=({1:3,2:4,3:3,4:3}.get(binding['attribute'],1) if binding['typeID']==4 else 1)
  assert offset==clip['columns']
 records=[a for a in doc['animators'] if a['id']==record['id'] and a['path']==record['path']];assert len(records)==1
 records[0]['clips']=record['clips']
original.write_text(json.dumps(doc,indent=2))
print('DISCRETE_ANIMATION_REPAIRED 17380101 float-layout material-switch-track')

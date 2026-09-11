from pathlib import Path
import json,sys,zlib,collections
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).parent;base=w.parent
sources={'LegacyAnimationData':Path('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),'LatestAnimationData':Path('C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')};report=[]
for kind,source in sources.items():
 p=base/kind/'animations.json';doc=json.loads(p.read_text(encoding='utf-8'));targets=collections.defaultdict(list)
 for rec in doc['animators']:
  for clip in rec['clips']:
   if any(t['attribute']==4 and t.get('typeId',0)==0 and not t.get('hasRotationOrder') for t in clip['tracks']):targets[(rec['id'],clip['file'])].append(clip)
 print(kind,'remaining clips',len(targets),flush=True)
 if kind=='LegacyAnimationData':env=UnityPy.load(str(source));objects={(zlib.crc32(o.assets_file.name.encode()),o.path_id):o for o in env.objects if o.type.name=='AnimationClip'}
 for (scene,file),clips in targets.items():
  ids=Path(file).stem.split('_');pid=int(ids[-1]);crc=int(ids[-2]) if len(ids)>2 else None
  if kind=='LatestAnimationData':
   if not (source/scene).exists():report.append({'data':kind,'file':file,'error':'scene not found'});continue
   env=UnityPy.load(str(source/scene));cs=[o for o in env.objects if o.type.name=='AnimationClip' and o.path_id==pid]
  else:cs=[o for (c,i),o in objects.items() if i==pid and (c==crc if crc is not None else scene in o.assets_file.name)]
  if len(cs)!=1:report.append({'data':kind,'file':file,'error':'clip not found'});continue
  b={v['path']:v for v in cs[0].read_typetree()['m_ClipBindingConstant']['genericBindings'] if v['typeID']==4 and v['attribute']==4}
  for clip in clips:
   for t in clip['tracks']:
    if t['attribute']!=4 or t.get('typeId',0)!=0:continue
    h=zlib.crc32(t['path'].encode()) if t['path'] else 0
    if h not in b:report.append({'data':kind,'file':file,'error':'path not found','path':t['path']});continue
    raw=b[h]['customType'];order=raw-10 if 10<=raw<=15 else raw
    if order not in range(6):raise Exception(('unsupported order',kind,file,raw))
    t['rotationOrder']=order;t['hasRotationOrder']=True
 p.write_text(json.dumps(doc,indent=2),encoding='utf-8')
 print(kind,'remaining tracks',sum(1 for r in doc['animators'] for c in r['clips'] for t in c['tracks'] if t['attribute']==4 and t.get('typeId',0)==0 and not t.get('hasRotationOrder')),flush=True)
(w/'euler-contract-upgrade-unresolved.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print('unresolved',report,flush=True)

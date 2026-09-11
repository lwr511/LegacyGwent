from pathlib import Path
import sys,json,collections
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
base=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes');w=Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionRepair-20260910');rows=[]
for id in ['16550101','19850101','16740101']:
 e=UnityPy.load(str(base/id));print('SOURCE',id,collections.Counter(o.type.name for o in e.objects if o.type.name in ['Animator','Animation','AnimationClip','SkinnedMeshRenderer']))
 for o in e.objects:
  if o.type.name not in ['Animator','Animation','SkinnedMeshRenderer']:continue
  d=o.read_typetree();n=o.read().m_GameObject.deref().read().m_Name
  rows.append({'scene':id,'file':o.assets_file.name,'id':o.path_id,'type':o.type.name,'name':n,'data':d})
  if o.type.name in ['Animator','Animation']:print(o.type.name,n,d)
  else:print('SKIN',n,'bones',len(d.get('m_Bones',[])),'enabled',d.get('m_Enabled'))
(w/'source-components.json').write_text(json.dumps(rows,indent=2))

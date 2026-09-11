exec(open(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/SourceFraming-20260910/find_latest_bundles.py').read().split('rows=[]')[0])
from collections import Counter
paths=list((base/'gui/prefabs').glob('*'))+list((base/'scenes').rglob('*'))+list(base.parent.parent.glob('*.assets'))
for p in paths:
 if not p.is_file() or p.suffix=='.manifest':continue
 env=UnityPy.load(str(p))
 for o in env.objects:
  if o.type.name=='Camera':
   d=o.read_typetree();print(p.name,'CAM',o.path_id,d.get('field of view'),d.get('m_GameObject'),flush=True)
  elif o.type.name=='GameObject':
   name=o.read().m_Name
   if any(x in name.lower() for x in ['render','appearance','appereance']):print(p.name,'GO',o.path_id,name,flush=True)

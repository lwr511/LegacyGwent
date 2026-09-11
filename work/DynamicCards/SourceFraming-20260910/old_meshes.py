from pathlib import Path
import sys,json
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
base=Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data')
for p in [base/'sharedassets2.assets',base/'StreamingAssets/AssetBundles/cardassets/prefabs',base/'StreamingAssets/AssetBundles/cardassets/fronts/meshdata']:
 env=UnityPy.load(str(p))
 for o in env.objects:
  if o.type.name=='Mesh':
   m=o.read()
   if p.name=='sharedassets2.assets' and 'card' not in m.m_Name.lower() and 'mask' not in m.m_Name.lower():continue
   h=MeshHandler(m);h.process();print('MESH',p.name,o.path_id,m.m_Name,'vertices',h.m_Vertices[:20] if h.m_Vertices else None,'uvs',h.m_UV0[:20] if h.m_UV0 else None)

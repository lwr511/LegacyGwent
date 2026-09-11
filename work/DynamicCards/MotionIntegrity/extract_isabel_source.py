from pathlib import Path
import sys,json,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity');source='C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes';env=UnityPy.load(source);rows=[]
for o in env.objects:
 if '15720100' not in o.assets_file.name:continue
 if o.type.name=='Animator':
  d=o.read()
  if d.m_Avatar.path_id:
   a=d.m_Avatar.read_typetree();(w/'isabel-avatar.json').write_text(json.dumps(a,indent=2));print('Avatar keys',list(a),flush=True)
 if o.type.name=='SkinnedMeshRenderer':
  d=o.read();m=d.m_Mesh.read();a=d.m_Mesh.read_typetree();print('skin',d.m_GameObject.read().m_Name,'bones',len(d.m_Bones),'mesh hashes',len(a.get('m_BoneNameHashes',[])),'skinWeights',len(a.get('m_Skin',[])),flush=True);(w/'isabel-source-mesh.json').write_text(json.dumps(a,indent=2));(w/'isabel-source-skin.json').write_text(json.dumps(o.read_typetree(),indent=2))

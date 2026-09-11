import sys,json,re
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
dest=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content'
env=UnityPy.load(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
result={}
for obj in env.objects:
    if obj.type.name not in ['MeshRenderer','SkinnedMeshRenderer','ParticleSystemRenderer']:continue
    match=re.search(r'(\d{8})',obj.assets_file.name)
    if not match:continue
    renderer=obj.read();materials=renderer.m_Materials
    if not materials or materials[0].path_id==0:
        name=renderer.m_GameObject.read().m_Name
        result.setdefault(match[1],[]).append(name)
for path in dest.glob('*/conversion.json'):
    data=json.loads(path.read_text());data['sourceNullMaterials']=result.get(data['id'],[])
    path.write_text(json.dumps(data,indent=2),encoding='utf8')
(root/'source_null_renderers.json').write_text(json.dumps(result,indent=2))
print('Source renderers with no material:',sum(map(len,result.values())))

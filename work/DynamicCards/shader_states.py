import sys,json
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
env=UnityPy.load(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
result={}
rendering={}
for obj in env.objects:
    if obj.type.name!='Shader':continue
    tree=obj.read_typetree();form=tree.get('m_ParsedForm',{});name=form.get('m_Name',tree.get('m_Name',''))
    result[name]=[p.get('m_State',{}) for sub in form.get('m_SubShaders',[]) for p in sub.get('m_Passes',[])]
    rendering[name]=dict(states=result[name],tags=form.get('m_SubShaders',[{}])[0].get('m_Tags',{}))
(root/'shader_states.json').write_text(json.dumps(result,indent=2))
(root/'shader_rendering.json').write_text(json.dumps(rendering,indent=2))
print('shaders',len(rendering))
print('core tags',rendering.get('Custom/Cards/CardCore/ImageLayerShader')) if False else None
print(rendering['Custom/Cards/CardCore/ImageLayerShader']['tags'])

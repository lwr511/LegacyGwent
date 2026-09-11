from pathlib import Path
import sys,json
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).resolve().parent;env=UnityPy.load('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes');rows=[]
for o in env.objects:
 if o.type.name=='ParticleSystem':
  d=o.read()
  if '1045' in o.assets_file.name or 'emit' in d.m_GameObject.read().m_Name:rows.append({'file':o.assets_file.name,'name':d.m_GameObject.read().m_Name,'components':[x.component.deref().type.name for x in d.m_GameObject.read().m_Component]})
(w/'earthquake-original-emitter.json').write_text(json.dumps(rows,indent=2));print(rows)

from pathlib import Path
import json,sys
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/GasconColor-20260913');s=Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets');rows=[]
for group in ['standard/high','premium/high','premium/uber']:
 e=UnityPy.load(str(s/'textures'/group))
 for o in e.objects:
  if o.type.name!='Texture2D':continue
  d=o.read()
  if '1528' not in d.m_Name:continue
  name=group.replace('/','-')+'-'+d.m_Name+'.png';d.image.save(w/name);rows.append(dict(group=group,name=d.m_Name,pathID=o.path_id,file=o.assets_file.name,output=name,width=d.m_Width,height=d.m_Height))
(w/'source-textures.json').write_text(json.dumps(rows,indent=2));print(rows)

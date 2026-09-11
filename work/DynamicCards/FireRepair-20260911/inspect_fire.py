import sys,json,re
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent
P=Path(r'C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
source=Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes')
print('Loading source',flush=True)
env=UnityPy.load(str(source))
rows=[]
for o in env.objects:
 if o.type.name!='Material':continue
 d=o.read()
 if not any(s in d.m_Name.lower() for s in ['henselt','brouver']):continue
 row=dict(name=d.m_Name,file=o.assets_file.name,id=o.path_id,textures=[])
 for prop,slot in d.m_SavedProperties.m_TexEnvs:
  if not slot.m_Texture.path_id:continue
  try:
   tex=slot.m_Texture.deref();td=tex.read()
   item=dict(property=prop,name=td.m_Name,id=tex.path_id,file=tex.assets_file.name,width=td.m_Width,height=td.m_Height)
   row['textures'].append(item)
   if any(s in d.m_Name.lower() for s in ['fire','flame']):
    td.image.save(W/(str(tex.path_id)+'-'+re.sub('[^a-zA-Z0-9_-]','_',td.m_Name)+'.png'))
  except Exception as e:row['textures'].append(dict(property=prop,error=str(e)))
 rows.append(row)
(W/'source-materials.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(rows,indent=2),flush=True)

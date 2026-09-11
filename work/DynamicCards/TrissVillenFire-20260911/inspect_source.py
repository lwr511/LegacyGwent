from pathlib import Path
import sys,json
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).parent
env=UnityPy.load('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes')
rows=[]
for o in env.objects:
 if o.type.name!='Material':continue
 d=o.read()
 if d.m_Name not in ['VillenTiles','HandFire_left 1','HandFire_left_1','FireLargePreMul','FireSmallTree']:continue
 row=dict(name=d.m_Name,file=o.assets_file.name,id=o.path_id,textures=[],floats=dict(d.m_SavedProperties.m_Floats))
 for prop,slot in d.m_SavedProperties.m_TexEnvs:
  if not slot.m_Texture.path_id:continue
  tex=slot.m_Texture.deref();t=tex.read()
  file=t.m_Name.replace('/','_')+'.png';t.image.save(w/file)
  row['textures'].append(dict(property=prop,name=t.m_Name,id=tex.path_id,file=tex.assets_file.name,colorSpace=getattr(t,'m_ColorSpace',None),scale=str(slot.m_Scale),offset=str(slot.m_Offset),export=file))
 rows.append(row)
(w/'source-materials.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(rows,indent=2),flush=True)

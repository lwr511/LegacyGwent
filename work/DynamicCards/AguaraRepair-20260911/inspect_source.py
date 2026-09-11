import sys,json
from pathlib import Path
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).parent
env=UnityPy.load('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes')
rows=[]
for o in env.objects:
    if o.type.name!='Material':continue
    d=o.read()
    if d.m_Name!='[20005601]AguaraFoxForm_Cloud':continue
    row=dict(id=o.path_id,file=o.assets_file.name,name=d.m_Name,textures=[])
    for prop,slot in d.m_SavedProperties.m_TexEnvs:
        if not slot.m_Texture.path_id:continue
        obj=slot.m_Texture.deref();t=obj.read()
        row['textures'].append(dict(property=prop,id=obj.path_id,file=obj.assets_file.name,name=t.m_Name))
        if prop=='_MainTex':t.image.save(w/('source-'+str(o.path_id)+'.png'))
    rows.append(row)
(w/'source-materials.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(rows,indent=2),flush=True)

from pathlib import Path
import json,sys
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).resolve().parent
env=UnityPy.load('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes')
rows=[]
for o in env.objects:
 if o.type.name!='Material':continue
 d=o.read()
 if not any(x in d.m_Name for x in ['CroneBrewess','201615','201618']):continue
 row={'name':d.m_Name,'file':o.assets_file.name,'id':o.path_id,'textures':[]}
 for key,te in d.m_SavedProperties.m_TexEnvs:
  t=te.m_Texture;entry={'property':key,'id':t.path_id,'scale':str(te.m_Scale),'offset':str(te.m_Offset)}
  if t.path_id:
   try:
    tex=t.read();entry.update(name=tex.m_Name,type=t.deref().type.name)
    if t.deref().type.name=='Texture2D':
     target=w/'SourceVisuals'/(str(o.path_id)+'-'+key+'.png');target.parent.mkdir(exist_ok=True);tex.image.save(target);entry['image']=str(target)
   except Exception as e:entry['error']=str(e)
  row['textures'].append(entry)
 rows.append(row)
(w/'source-visual-materials.json').write_text(json.dumps(rows,indent=2));print('materials',len(rows))

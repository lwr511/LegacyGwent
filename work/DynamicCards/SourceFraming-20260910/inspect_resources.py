from pathlib import Path
import sys,json,re
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
out=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards\SourceFraming-20260910')
root=Path(r'E:\Hbackup\FileRecv\Gwent\Gwent\Gwent_Data')
for name in ['resources.assets','level0']:
 env=UnityPy.load(str(root/name));rows=[];errors=[]
 for o in env.objects:
  if o.type.name not in ['GameObject','Camera','MonoBehaviour']:continue
  try:d=o.read_typetree()
  except Exception as e:
   if len(errors)<2:errors.append(str(e))
   continue
  keep=o.type.name=='Camera' or (o.type.name=='GameObject' and re.search('CoreCamera|CardRender|AppearanceAttach|Perspective',d.get('m_Name',''),re.I)) or (o.type.name=='MonoBehaviour' and any(k in d for k in ['m_RenderingCamera','m_ApperenceAttachParent','m_CardRTRendererPrefab']))
  if keep:rows.append({'file':o.assets_file.name,'id':o.path_id,'type':o.type.name,'data':d})
 (out/(name+'.json')).write_text(json.dumps(rows,indent=2),encoding='utf-8')
 print(name,'selected',len(rows),'errors',errors)
 for r in rows:
  if r['type']=='GameObject':print(r['id'],r['data']['m_Name'])
  elif r['type']=='MonoBehaviour':print('script',r['id'],list(r['data']))

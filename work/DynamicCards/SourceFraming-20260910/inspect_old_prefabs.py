from pathlib import Path
import sys,json,re
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
out=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards\SourceFraming-20260910')
base=Path(r'E:\Hbackup\FileRecv\Gwent\Gwent\Gwent_Data\StreamingAssets\AssetBundles')
for rel in ['cardassets/prefabs','gui/prefabs/global_base']:
 env=UnityPy.load(str(base/rel)); rows=[]
 for o in env.objects:
  if o.type.name not in ['GameObject','Camera','MonoBehaviour','Transform','Mesh','Material']:continue
  try:d=o.read_typetree()
  except:continue
  keep=o.type.name=='Camera' or (o.type.name=='GameObject' and re.search('CoreCamera|CardRender|AppearanceAttach|Perspective',d.get('m_Name',''),re.I)) or (o.type.name=='MonoBehaviour' and any(k in d for k in ['m_RenderingCamera','m_ApperenceAttachParent','RenderingPasses']))
  if keep:rows.append({'file':o.assets_file.name,'id':o.path_id,'type':o.type.name,'data':d})
 (out/(rel.replace('/','-')+'.json')).write_text(json.dumps(rows,indent=2),encoding='utf-8')
 print(rel,'objects',len(list(env.objects)),'selected',len(rows))
 for r in rows:
  if r['type']=='Camera':print('camera',r['id'],r['data'].get('m_GameObject'),r['data'].get('field of view'))
  if r['type']=='GameObject':print('go',r['id'],r['data']['m_Name'])

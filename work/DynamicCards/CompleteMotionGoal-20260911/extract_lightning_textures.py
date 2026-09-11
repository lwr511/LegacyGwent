from pathlib import Path
import json,sys,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.classes import PPtr
W=Path(__file__).resolve().parent;out=W/'SourceLightningTextures';out.mkdir(exist_ok=True);rs=json.loads((W/'lightning-source-records.json').read_text());textures=[]
for source,location in [('Legacy2017','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),('Thronebreaker','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')]:
 env=UnityPy.load(location);scripts={}
 for o in env.objects:
  if o.type.name!='MonoBehaviour':continue
  m=re.search(r'\d{8}',o.assets_file.name)
  if not m:continue
  d=o.read()
  try:name=d.m_Script.read().m_ClassName
  except:continue
  if name=='LightningTool':scripts[(m[0],d.m_GameObject.path_id)]=o
 for r in [r for r in rs if r['source']==source]:
  for s in [s for s in r['scripts'] if s['type']=='LightningTool']:
   obj=scripts[(r['scene'],s['data']['m_GameObject']['m_PathID'])]
   for prop in ['LightningTexture','TurbulenceTexture']:
    raw=s['data'][prop];ptr=PPtr(m_FileID=raw['m_FileID'],m_PathID=raw['m_PathID'],assetsfile=obj.assets_file);texobj=ptr.deref();tex=texobj.read();tree=texobj.read_typetree();file=f'{source}_{texobj.assets_file.name}_{texobj.path_id}.png';tex.image.save(out/file);textures.append(dict(source=source,scene=r['scene'],path=s['path'],property=prop,file=file,colorSpace=tree.get('m_ColorSpace'),mipCount=tree.get('m_MipCount'),settings=tree['m_TextureSettings']))
 print(source,'textures',len(textures),flush=True)
(W/'lightning-original-textures.json').write_text(json.dumps(textures,indent=2))

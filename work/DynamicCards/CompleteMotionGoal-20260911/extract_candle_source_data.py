from pathlib import Path
import json,sys,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent;out=W/'SourceCandles';out.mkdir(exist_ok=True);rs=json.loads((W/'candle-source-records.json').read_text());helpers=json.loads((W/'source-helper-contracts.json').read_text())['records'];evidence=[];textures=[]
for source,location in [('Legacy2017','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),('Thronebreaker','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')]:
 env=UnityPy.load(location);objs={};scripts={}
 for o in env.objects:
  if o.type.name not in ['MonoBehaviour','GameObject']:continue
  m=re.search(r'\d{8}',o.assets_file.name)
  if not m:continue
  if o.type.name=='GameObject':objs[(m[0],o.path_id)]=o
  else:
   d=o.read()
   try:name=d.m_Script.read().m_ClassName
   except:continue
   if name=='CandleFireTool':scripts[(m[0],d.m_GameObject.path_id)]=o
 if source=='Legacy2017':
  effects=json.loads((W.parent/'second_source_effects.json').read_text())
  for r in helpers:
   for s in r['components']:
    ss=next(v for v in effects[r['scene']]['scripts'] if v['type']==s['kind'] and v['path']==s['path']);go=objs[(r['scene'],ss['data']['m_GameObject']['m_PathID'])].read();types=[c.component.deref().type.name for c in go.m_Component];evidence.append(dict(scene=r['scene'],path=s['path'],kind=s['kind'],sourceComponents=types))
 for r in [r for r in rs if r['source']==source]:
  obj=scripts[(r['scene'],r['data']['m_GameObject']['m_PathID'])];d=obj.read()
  for prop in ['CandleTexture','NoiseTexture']:
   ptr=getattr(d,prop,None)
   if ptr is None:
    from UnityPy.classes import PPtr
    raw=r['data'][prop];ptr=PPtr(m_FileID=raw['m_FileID'],m_PathID=raw['m_PathID'],assetsfile=obj.assets_file)
   texobj=ptr.deref();tex=texobj.read();tree=texobj.read_typetree();file=f'{source}_{texobj.assets_file.name}_{texobj.path_id}.png';tex.image.save(out/file);textures.append(dict(source=source,scene=r['scene'],path=r['path'],property=prop,file=file,colorSpace=tree.get('m_ColorSpace'),mipCount=tree.get('m_MipCount'),settings=tree['m_TextureSettings']))
 print(source,'textures',len(textures),flush=True)
(W/'helper-original-component-evidence.json').write_text(json.dumps(evidence,indent=2));(W/'candle-original-textures.json').write_text(json.dumps(textures,indent=2))

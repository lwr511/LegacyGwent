from pathlib import Path
from collections import defaultdict
import json,sys,re,zlib
sys.stdout.reconfigure(encoding='utf-8');sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent;rows=json.loads((W/'binding-suspects-source-values.json').read_text());result=[]
for source,base,location in [('Legacy2017','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data','StreamingAssets/AssetBundles/cardassets/scenes'),('Thronebreaker','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data','StreamingAssets/bundledassets/cardassets/scenes'),('Latest','C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data','StreamingAssets/bundledassets/cardassets/scenes')]:
 selected=[r for r in rows if r['source']==source and r['type'] in [23,137,199]];ids={r['scene'] for r in selected};scene=Path(base)/location
 files=[str(scene/i) for i in ids] if source=='Latest' else [str(scene)]
 files.extend(str(p) for p in [Path(base)/'globalgamemanagers.assets',Path(base)/'resources.assets'] if p.exists())
 env=UnityPy.load(*files);cache={};paths={}
 def path(o):
  key=(o.assets_file.name,o.path_id)
  if key not in cache:
   d=o.read();cache[key]=(path(d.m_Father.deref())+'/' if d.m_Father.path_id else '')+d.m_GameObject.read().m_Name
  return cache[key]
 for o in env.objects:
  if o.type.name=='Transform':paths[path(o)]=o.read().m_GameObject.read()
 for r in selected:
  target='/'.join(v for v in [r['animator'],r['path']] if v);go=paths.get(target);e=dict(r,sourceTargetExists=go is not None,matches=[],materials=[])
  if go:
   renderers=[c.component.read() for c in go.m_Component if c.component.deref().type.value==r['type']]
   for rend in renderers:
    for slot,ptr in enumerate(rend.m_Materials):
     if not ptr.path_id:continue
     try:
      mat=ptr.read();sh=mat.m_Shader.read_typetree()['m_ParsedForm'];e['materials'].append(dict(slot=slot,name=mat.m_Name,shader=sh['m_Name']))
      for prop in sh['m_PropInfo']['m_Props']:
       name=prop['m_Name'];key=name+'_ST' if prop['m_Type']==4 else name
       if zlib.crc32(key.encode())&0xfffffff==r['attribute']&0xfffffff:e['matches'].append(dict(slot=slot,name=name,type=prop['m_Type'],component=(r['attribute']>>28)&3,defaults=prop.get('m_DefValue',[prop.get('m_DefValue_0'),prop.get('m_DefValue_1'),prop.get('m_DefValue_2'),prop.get('m_DefValue_3')])))
     except Exception as ex:e['materials'].append(dict(slot=slot,error=str(ex)))
  result.append(e)
 print(source,'renderer suspects',len(selected),'effective matches',sum(bool(r['matches']) for r in result if r['source']==source),flush=True)
 (W/'source-renderer-property-proof.json').write_text(json.dumps(result,indent=2))

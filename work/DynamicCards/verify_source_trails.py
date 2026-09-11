from pathlib import Path
import sys,json
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
r=Path(__file__).resolve().parent;p=r/'source_null_material_slots.json';doc=json.loads(p.read_text());proof=[]
for kind,id,file in [('Legacy','14210801',r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),('Latest','11950101',r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes/11950101')]:
 env=UnityPy.load(file)
 def path(o):
  d=o.read();return (path(d.m_Father)+'/' if d.m_Father.path_id else '')+d.m_GameObject.read().m_Name
 for o in env.objects:
  if o.type.name!='TrailRenderer' or id not in o.assets_file.name:continue
  d=o.read();g=d.m_GameObject.read();tr=next(c.component for c in g.m_Component if c.component.deref().type.name=='Transform');name=path(tr)
  proof.append(dict(source=kind,id=id,path=name,enabled=d.m_Enabled,materials=[v.path_id for v in d.m_Materials]))
  for slot,v in enumerate(d.m_Materials):
   if not v.path_id:doc['slots'].append(dict(source=kind,id=id,type='TrailRenderer',name=g.m_Name,path=name,slot=slot))
p.write_text(json.dumps(doc,indent=2));(r/'source_trail_evidence.json').write_text(json.dumps(proof,indent=2));print(proof)

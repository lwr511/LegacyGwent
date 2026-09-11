from pathlib import Path
import sys,json
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).resolve().parent;root=w.parent
rows=[]
for name in ['source_effects.json','second_source_effects.json','latest_source_effects.json']:
 for scene,c in json.loads((root/name).read_text()).items():
  for s in c['scripts']:
   for i,a in enumerate(s['data'].get('SharedTextureAssigments',[])):
    rows.append({'source':name,'scene':scene,'material':s['references'].get('SharedTextureAssigments.%s.Material'%i),'properties':a['Assigments']})
(w/'shared-texture-contracts.json').write_text(json.dumps(rows,indent=2));print('contracts',len(rows),'scenes',len({(r['source'],r['scene']) for r in rows}),flush=True)
env=UnityPy.load('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/textures/premium/high')
names=[]
for o in env.objects:
 if o.type.name=='Texture2D':
  d=o.read();names.append({'name':d.m_Name,'file':o.assets_file.name,'id':o.path_id})
  if any(x in d.m_Name.lower() for x in ['13220','crone','shared']):
   print('texture',d.m_Name,flush=True)
   target=w/'SourceSharedTextures'/(d.m_Name+'.png');target.parent.mkdir(exist_ok=True);d.image.save(target)
(w/'legacy-premium-texture-inventory.json').write_text(json.dumps(names,indent=2));print('textures',len(names))

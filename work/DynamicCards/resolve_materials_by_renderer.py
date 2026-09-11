from pathlib import Path
import sys,json,re,collections,gc
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
r=Path(__file__).resolve().parent
sources={'Native':r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','Legacy':r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'}
for kind,source in sources.items():
 pending=json.loads((r/(kind.lower()+'_ambiguous_material_states.json')).read_text())
 wanted={x['material']['asset']:x['material'] for x in pending}
 uses=collections.defaultdict(list)
 base=r/'Probe/Assets/DynamicCards/Content'
 paths=(base/'Legacy2017').glob('*/conversion.json') if kind=='Legacy' else base.glob('*/conversion.json')
 for path in paths:
  doc=json.loads(path.read_text());matches=[m for m in doc['materials'] if m['asset'] in wanted]
  if not matches:continue
  body=(path.parent/'Card.prefab').read_text();objects={int(i):(int(t),b) for t,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- !u!|\Z)',body,re.S)}
  for material in matches:
   guid=re.search(r'^guid: (\w+)',(r/'Probe'/(material['asset']+'.meta')).read_text(),re.M)[1]
   for typeid,chunk in objects.values():
    if typeid not in (23,137,199) or guid not in chunk:continue
    go=int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',chunk)[1]);name=re.search(r'^  m_Name: (.*)$',objects[go][1],re.M)[1].strip('"')
    materials=re.search(r'  m_Materials:\n((?:  - .*\n)*)',chunk)
    if materials:
     for slot,line in enumerate(materials[1].splitlines()):
      if guid in line:uses[material['asset']].append((doc['id'],name,slot,material['originalName']))
 keys={key for rows in uses.values() for key in rows};index=collections.defaultdict(list)
 env=UnityPy.load(source)
 for obj in env.objects:
  if obj.type.name not in ('MeshRenderer','SkinnedMeshRenderer','ParticleSystemRenderer'):continue
  match=re.search(r'\d{8}',obj.assets_file.name)
  if not match:continue
  renderer=obj.read();name=renderer.m_GameObject.read().m_Name
  for slot,ptr in enumerate(renderer.m_Materials):
   if not ptr.path_id:continue
   material=ptr.read_typetree();key=(match[0],name,slot,material['m_Name'])
   if key not in keys:continue
   state=dict(queue=material['m_CustomRenderQueue'],renderType=dict(material.get('stringTagMap',[])).get('RenderType',''))
   if state not in index[key]:index[key].append(state)
 exact=json.loads((r/(kind.lower()+'_exact_material_states.json')).read_text());resolved={};remaining=[]
 for item in pending:
  asset=item['material']['asset'];options=[];complete=bool(uses[asset])
  for key in uses[asset]:
   if not index[key]:complete=False
   for state in index[key]:
    if state not in options:options.append(state)
  if complete and len(options)==1:exact[asset]=options[0];resolved[asset]=dict(state=options[0],rendererUses=uses[asset])
  else:remaining.append(item)
 (r/(kind.lower()+'_exact_material_states.json')).write_text(json.dumps(exact,indent=2))
 (r/(kind.lower()+'_renderer_material_evidence.json')).write_text(json.dumps(resolved,indent=2))
 (r/(kind.lower()+'_ambiguous_material_states.json')).write_text(json.dumps(remaining,indent=2))
 print('RENDERER_MATERIAL_STATES',kind,'resolved',len(resolved),'remaining',len(remaining),flush=True)
 del env;gc.collect()

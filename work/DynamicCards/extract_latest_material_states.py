from pathlib import Path
import sys,json,collections
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
source=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets')
def materials(path):
 result={}
 for o in UnityPy.load(str(path)).objects:
  if o.type.name!='Material':continue
  d=o.read_typetree();result[d['m_Name']]=dict(queue=d['m_CustomRenderQueue'],renderType=dict(d.get('stringTagMap',[])).get('RenderType',''))
 return result
common={}
for name in ['dependencies/vfx/common','dependencies/vfx/cards','dependencies/shaderlibrary']:
 common.update(materials(source/name))
result={};missing=[]
for index,metadata in enumerate(sorted((root/'LatestBridge2022/Assets/PortableCards').glob('*/conversion.json'))):
 card=json.loads(metadata.read_text());states=dict(common);states.update(materials(source/'cardassets/scenes'/card['id']))
 for item in card['materials']:
  state=states.get(item['originalName'])
  if state is None:missing.append(dict(card=card['id'],material=item))
  else:result[item['asset']]=state
 if index%100==0:print('MATERIAL_STATES',index,flush=True)
(root/'latest_material_states.json').write_text(json.dumps(result,indent=2))
(root/'latest_material_states_missing.json').write_text(json.dumps(missing,indent=2))
print('MATERIAL_STATES_DONE',len(result),'missing',len(missing),flush=True)

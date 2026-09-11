from pathlib import Path
import sys,json,re,collections,gc
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
sources={'Native':r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','Legacy':r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'}
for kind,path in sources.items():
 result=collections.defaultdict(lambda:collections.defaultdict(list))
 env=UnityPy.load(path)
 for obj in env.objects:
  if obj.type.name!='Material':continue
  match=re.search(r'\d{8}',obj.assets_file.name)
  if not match:continue
  data=obj.read_typetree();state=dict(queue=data['m_CustomRenderQueue'],renderType=dict(data.get('stringTagMap',[])).get('RenderType',''))
  values=result[match[0]][data['m_Name']]
  if state not in values:values.append(state)
 (root/(kind.lower()+'_card_material_states.json')).write_text(json.dumps(result,indent=2))
 print('CARD_MATERIAL_STATES',kind,len(result),flush=True)
 del env;gc.collect()

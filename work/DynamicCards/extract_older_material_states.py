from pathlib import Path
import sys,json,collections,gc
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
sources={'Native':r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','Legacy':r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'}
for kind,path in sources.items():
 result=collections.defaultdict(list)
 env=UnityPy.load(path)
 for o in env.objects:
  if o.type.name!='Material':continue
  d=o.read_typetree();state=dict(queue=d['m_CustomRenderQueue'],renderType=dict(d.get('stringTagMap',[])).get('RenderType',''))
  if state not in result[d['m_Name']]:result[d['m_Name']].append(state)
 (root/(kind.lower()+'_original_material_states.json')).write_text(json.dumps(result,indent=2));print('ORIGINAL_MATERIAL_STATES',kind,len(result),'ambiguous',sum(len(v)>1 for v in result.values()),flush=True)
 del env;gc.collect()

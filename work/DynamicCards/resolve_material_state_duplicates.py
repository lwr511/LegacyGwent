from pathlib import Path
import sys,json,re,collections,gc
root=Path(__file__).resolve().parent
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
sources={'Native':r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','Legacy':r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'}
for kind,source in sources.items():
 missing=json.loads((root/(kind.lower()+'_material_state_unresolved.json')).read_text());names={m['originalName'] for m in missing}
 index=collections.defaultdict(list);env=UnityPy.load(source)
 for obj in env.objects:
  if obj.type.name!='Material':continue
  d=obj.read_typetree()
  if d['m_Name'] not in names:continue
  props=d['m_SavedProperties'];record=dict(queue=d['m_CustomRenderQueue'],renderType=dict(d.get('stringTagMap',[])).get('RenderType',''),floats=dict(props.get('m_Floats',[])),colors=dict(props.get('m_Colors',[])))
  if record not in index[d['m_Name']]:index[d['m_Name']].append(record)
 resolved={};unresolved=[]
 for material in missing:
  body=(root/'Probe'/material['asset']).read_text()
  floats={k:float(v) for k,v in re.findall(r'^    - (\S+): (-?[\d.eE+-]+)\s*$',body,re.M)}
  colors={}
  for match in re.finditer(r'^    - (\S+): \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}',body,re.M):
   colors[match[1]]=dict(zip('rgba',map(float,match.groups()[1:])))
  options=[]
  for state in index[material['originalName']]:
   if any(k not in floats or abs(floats[k]-v)>max(.00001,abs(v)*.00001) for k,v in state['floats'].items()):continue
   if any(k not in colors or any(abs(colors[k][a]-v[a])>.00001 for a in 'rgba') for k,v in state['colors'].items()):continue
   simple={k:state[k] for k in ['queue','renderType']}
   if simple not in options:options.append(simple)
  if len(options)==1:resolved[material['asset']]=options[0]
  else:unresolved.append(dict(material=material,matchingStates=len(options)))
 (root/(kind.lower()+'_exact_material_states.json')).write_text(json.dumps(resolved,indent=2))
 (root/(kind.lower()+'_ambiguous_material_states.json')).write_text(json.dumps(unresolved,indent=2))
 print('RESOLVED_MATERIAL_DUPLICATES',kind,len(resolved),'remaining',len(unresolved),flush=True)
 del env;gc.collect()

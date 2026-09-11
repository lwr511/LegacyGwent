from pathlib import Path
import sys,json
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
base=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets')
rows=[]
for p in list((base/'gui/prefabs').glob('*'))+list((base/'scenes').rglob('*')):
 if not p.is_file() or p.suffix=='.manifest':continue
 env=UnityPy.load(str(p))
 for o in env.objects:
  if o.type.name!='Transform':continue
  d=o.read();name=d.m_GameObject.deref().read().m_Name
  if not any(x in name.lower() for x in ['cardrtrender','appereance','appearance','corecamera']):continue
  chain=[];t=d
  while t.m_Father.path_id:
   t=t.m_Father.deref().read();chain.append(t.m_GameObject.deref().read().m_Name)
  row={'bundle':str(p.relative_to(base)),'file':o.assets_file.name,'id':o.path_id,'name':name,'parents':chain,'transform':o.read_typetree()}
  rows.append(row);print(row['bundle'],name,row['transform']['m_LocalPosition'],chain,flush=True)
Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/SourceFraming-20260910/latest-bundle-renderers.json').write_text(json.dumps(rows,indent=2))
print('TOTAL',len(rows),flush=True)

from pathlib import Path
import sys,json
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
out=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards\SourceFraming-20260910')
for tag,root in [('Thronebreaker',Path(r'C:\SteamLibrary\steamapps\common\Thronebreaker The Witcher Tales\Thronebreaker_Data')),('Latest',Path(r'C:\SteamLibrary\steamapps\common\GWENT The Witcher Card Game\Gwent_Data'))]:
 rows=[]
 for p in list(root.glob('sharedassets*.assets'))+[root/'resources.assets']:
  env=UnityPy.load(str(p))
  for o in env.objects:
   if o.type.name!='Transform':continue
   d=o.read();name=d.m_GameObject.deref().read().m_Name
   if name not in ['Appereance','Appearance','CardRTRenderer','CoreCamera','Camera','Card']:continue
   chain=[];p2=d
   while p2.m_Father.path_id:
    p2=p2.m_Father.deref().read();chain.append(p2.m_GameObject.deref().read().m_Name)
   if not ('CardRTRenderer' in chain or name=='CardRTRenderer'):continue
   go=d.m_GameObject.deref().read();cams=[c.component.deref().read_typetree() for c in go.m_Component if c.component.deref().type.name=='Camera']
   row={'file':p.name,'id':o.path_id,'name':name,'parents':chain,'transform':o.read_typetree(),'cameras':cams};rows.append(row)
   print(tag,p.name,name,row['transform']['m_LocalPosition'], 'fov',[c.get('field of view') for c in cams],flush=True)
 (out/(tag+'-renderer-hierarchy.json')).write_text(json.dumps(rows,indent=2))
 print(tag,'rows',len(rows),flush=True)

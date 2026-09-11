from pathlib import Path
import sys,json
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(r'E:\Hbackup\FileRecv\Gwent\Gwent\Gwent_Data');out=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards\SourceFraming-20260910');rows=[]
for p in sorted(root.glob('sharedassets*.assets')):
 env=UnityPy.load(str(p))
 for o in env.objects:
  if o.type.name!='Camera':continue
  d=o.read();go=d.m_GameObject.deref().read();chain=[]
  t=next(c.component.deref() for c in go.m_Component if c.component.deref().type.name=='Transform')
  while t:
   td=t.read();chain.append({'name':td.m_GameObject.deref().read().m_Name,'position':t.read_typetree()['m_LocalPosition'],'rotation':t.read_typetree()['m_LocalRotation'],'scale':t.read_typetree()['m_LocalScale']});t=td.m_Father.deref() if td.m_Father.path_id else None
  r={'file':p.name,'id':o.path_id,'name':go.m_Name,'camera':o.read_typetree(),'chain':chain};rows.append(r);print(p.name,go.m_Name,'fov',r['camera'].get('field of view'),'chain',chain[:2])
(out/'old-cameras.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')

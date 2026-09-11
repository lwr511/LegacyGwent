from pathlib import Path
import sys,json,re,gc
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).resolve().parent;root=w/'FinalHarness/Library/DynamicCardsBundles/StandaloneWindows64';idx=json.loads((root/'cards.index.json').read_text());canonical=json.loads((w.parent/'source_null_material_slots.json').read_text())['slots'];allowed={(x['source'],x['type'],'Card/'+x['path'],x['slot']) for x in canonical};issues=[];checked=0
def source_null(kind,typ,full,slot):
 target=full.removeprefix('Card/').split('/')
 for x in canonical:
  if x['source']!=kind or x['type']!=typ or x['slot']!=slot or x['name']!=target[-1]:continue
  original=x['path'].split('/')
  if original[0]!=target[0]:continue
  iterator=iter(target)
  if all(any(piece==candidate for candidate in iterator) for piece in original):return True
 return False
for part in idx['parts']:
 env=UnityPy.load(str(root/part['file']));cache={};kind='Native' if 'thronebreaker' in part['file'] else 'Legacy' if 'legacy2017' in part['file'] else 'Latest'
 def path(ptr):
  o=ptr.deref() if hasattr(ptr,'deref') else ptr;key=o.path_id
  if key not in cache:
   t=o.read();cache[key]=(path(t.m_Father)+'/' if t.m_Father.path_id else '')+t.m_GameObject.read().m_Name
  return cache[key]
 for obj in env.objects:
  typ=obj.type.name
  if typ not in ['SkinnedMeshRenderer','MeshRenderer','ParticleSystemRenderer']:continue
  d=obj.read();checked+=1;go=d.m_GameObject.read();ps=None;full=None;scene=None
  for slot,mat in enumerate(d.m_Materials or [None]):
   if mat is not None and mat.path_id:
    material=mat.read()
    if material.m_Shader.path_id:continue
    reason='material has no shader'
   else:
    if typ=='ParticleSystemRenderer':
     if slot==1:
      ps=next((c.component.deref() for c in go.m_Component if c.component.deref().type.name=='ParticleSystem'),None)
      if ps is not None and not ps.read_typetree().get('TrailModule',{}).get('enabled',False):continue
     if d.m_RenderMode==5:continue # Unity ParticleSystemRenderMode.None; Mesh is 4.
    transform=next(c.component for c in go.m_Component if c.component.deref().type.name=='Transform');full=path(transform);match=re.search(r'/([0-9]{8})/',full);scene=match[1] if match else ''
    if source_null(kind,typ,full,slot):continue
    reason='unexpected null material'
   if full is None:
    transform=next(c.component for c in go.m_Component if c.component.deref().type.name=='Transform');full=path(transform)
   mesh_info=None
   if typ=='MeshRenderer':
    mf=next((c.component.read() for c in go.m_Component if c.component.deref().type.name=='MeshFilter'),None)
    mesh_info={'hasMesh':mf is not None and bool(mf.m_Mesh.path_id)}
   tr=next(c.component for c in go.m_Component if c.component.deref().type.name=='Transform');active=True
   while tr.path_id:
    td=tr.read();active=active and bool(td.m_GameObject.read().m_IsActive);tr=td.m_Father
   issues.append({'part':part['file'],'path':full,'type':typ,'slot':slot,'reason':reason,'enabled':d.m_Enabled,'activeHierarchy':active,'meshInfo':mesh_info})
 del env;gc.collect()
(w/'final-packed-material-audit.json').write_text(json.dumps({'checkedRenderers':checked,'issues':issues},indent=2));print('renderers',checked,'issues',len(issues));print(issues[:12])

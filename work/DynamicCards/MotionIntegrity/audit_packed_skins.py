from pathlib import Path
import sys,json,re,gc
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
w=Path(__file__).resolve().parent;root=w/'Harness/Library/DynamicCardsBundles/StandaloneWindows64';idx=json.loads((root/'cards.index.json').read_text());report=[];checked=0
for part in idx['parts']:
 env=UnityPy.load(str(root/part['file']));cache={}
 def path(ptr):
  o=ptr.deref() if hasattr(ptr,'deref') else ptr;key=o.path_id
  if key not in cache:
   t=o.read();cache[key]=(path(t.m_Father)+'/' if t.m_Father.path_id else '')+t.m_GameObject.read().m_Name
  return cache[key]
 for obj in env.objects:
  if obj.type.name!='SkinnedMeshRenderer':continue
  d=obj.read();checked+=1
  if d.m_Bones and all(b.path_id for b in d.m_Bones):continue
  if not d.m_Mesh.path_id:continue
  mesh=d.m_Mesh.read();helper=MeshHandler(mesh);helper.process()
  used={i for indices,weights in zip(helper.m_BoneIndices or [],helper.m_BoneWeights or []) for i,weight in zip(indices,weights) if weight>0}
  absent=sorted(i for i in used if i>=len(d.m_Bones) or d.m_Bones[i].path_id==0)
  if absent:
   go=d.m_GameObject.read();transform=next(c.component for c in go.m_Component if c.component.deref().type.name=='Transform');report.append({'part':part['file'],'path':path(transform),'bones':len(d.m_Bones),'missingWeightedIndices':absent,'usedCount':len(used)})
 (w/'packed-skin-audit.json').write_text(json.dumps({'complete':False,'checked':checked,'issues':report},indent=2));print('PACKED_SKIN',part['file'],'checked',checked,'issues',len(report),flush=True);del env;gc.collect()
(w/'packed-skin-audit.json').write_text(json.dumps({'complete':True,'checked':checked,'issues':report},indent=2))

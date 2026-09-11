from pathlib import Path
import sys,json,re,gc,collections
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
r=Path(__file__).resolve().parent
sources={'Native':Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes'),'Legacy':Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),'Latest':Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')}
result=[];skins=[]
issues=json.loads((r/'premium_structure_audit.json').read_text())['issues']
wanted=set()
for issue in issues:
 if ': missing skin bone ' not in issue:continue
 file,name=issue.split(': missing skin bone ');id=file.split('/')[-2]
 kind='Latest' if '/Latest/' in file else 'Legacy' if '/Legacy2017/' in file else 'Native'
 wanted.add((kind,id,name))
for kind,source in sources.items():
 paths=[source] if kind!='Latest' else [source/key for key in json.loads((r/'latest_source_effects.json').read_text())]
 states=collections.defaultdict(set)
 for path in paths:
  env=UnityPy.load(str(path))
  cache={}
  def transform_path(ptr):
   obj=ptr.deref() if callable(getattr(ptr,'deref',None)) else ptr;key=(obj.assets_file.name,obj.path_id)
   if key not in cache:
    t=obj.read();name=t.m_GameObject.read().m_Name
    cache[key]=(transform_path(t.m_Father)+'/' if t.m_Father.path_id else '')+name
   return cache[key]
  def transform_of(go):return next(c.component.deref() for c in go.m_Component if c.component.deref().type.name=='Transform')
  for obj in env.objects:
   if obj.type.name not in ('MeshRenderer','SkinnedMeshRenderer','ParticleSystemRenderer'):continue
   match=re.search(r'\d{8}',obj.assets_file.name)
   if not match:continue
   d=obj.read();go=d.m_GameObject.read();name=go.m_Name;tr=transform_of(go);fullpath=transform_path(tr)
   if not d.m_Materials:states[(match[0],obj.type.name,name,fullpath,0)].add(True)
   for slot,ptr in enumerate(d.m_Materials):states[(match[0],obj.type.name,name,fullpath,slot)].add(ptr.path_id==0)
   if (kind,match[0],name) in wanted:
    avatars=[];ancestor=tr;active=True
    while ancestor:
     data=ancestor.read();node=data.m_GameObject.read();active=active and bool(node.m_IsActive)
     for component in node.m_Component:
      if component.component.deref().type.name=='Animator':
       animator=component.component.read()
       if animator.m_Avatar.path_id:
        avatars.append(dict(root=transform_path(ancestor),paths=[dict(hash=h,path=p) for h,p in animator.m_Avatar.read_typetree().get('m_TOS',[])]))
     ancestor=data.m_Father.deref() if data.m_Father.path_id else None
    mesh=d.m_Mesh.read_typetree()
    skins.append(dict(source=kind,id=match[0],name=name,path=fullpath,active=active,enabled=bool(d.m_Enabled),bones=[transform_path(p) if p.path_id else '' for p in d.m_Bones],hashes=mesh.get('m_BoneNameHashes',[]),avatars=avatars))
  del env
 for (id,type,name,path,slot),values in states.items():
  if values=={True}:result.append(dict(source=kind,id=id,type=type,name=name,path=path,slot=slot))
 print('SOURCE_NULL_SLOTS',kind,sum(x['source']==kind for x in result),flush=True);gc.collect()
(r/'source_null_material_slots.json').write_text(json.dumps(dict(slots=result),indent=2))
(r/'source_skin_binding_contracts.json').write_text(json.dumps(dict(skins=skins),indent=2))

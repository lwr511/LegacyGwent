from pathlib import Path
import sys,json,zlib
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
r=Path(__file__).resolve().parent
env=UnityPy.load(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes')
cache={}
def path(o):
 if callable(getattr(o,'deref',None)):o=o.deref()
 key=(o.assets_file.name,o.path_id)
 if key not in cache:
  d=o.read();cache[key]=(path(d.m_Father)+'/' if d.m_Father.path_id else '')+d.m_GameObject.read().m_Name
 return cache[key]
rows=[]
for o in env.objects:
 if o.type.name=='Avatar':
  for h,n in o.read_typetree().get('m_TOS',[]):
   if h==1819506819:rows.append(dict(type='avatarMatch',file=o.assets_file.name,path=n))
 if o.type.name=='TrailRenderer' and any(k in o.assets_file.name for k in ['14210801','11950101']):
  d=o.read();rows.append(dict(type='trail',file=o.assets_file.name,name=d.m_GameObject.read().m_Name,enabled=d.m_Enabled,materials=[m.path_id for m in d.m_Materials]))
 if '122306' not in o.assets_file.name:continue
 if o.type.name=='Transform':
  p=path(o);parts=p.split('/')
  for i in range(len(parts)):
   if zlib.crc32('/'.join(parts[i:]).encode())==1819506819:rows.append(dict(type='hashMatch',file=o.assets_file.name,path=p))
 elif o.type.name=='SkinnedMeshRenderer':
  d=o.read()
  if not d.m_Mesh.path_id:continue
  m=d.m_Mesh.read_typetree();hs=m.get('m_BoneNameHashes',[])
  if 1819506819 in hs:
   ix=hs.index(1819506819);rows.append(dict(type='skin',file=o.assets_file.name,name=d.m_GameObject.read().m_Name,index=ix,bone=path(d.m_Bones[ix]) if d.m_Bones[ix].path_id else '',hashes=hs,bindposes=m['m_BindPose'],bones=[path(b) if b.path_id else '' for b in d.m_Bones]))
(r/'last_bone_details.json').write_text(json.dumps(rows,indent=2))
print('LAST_BONE_DETAILS',[(v['type'],v.get('path',v.get('name'))) for v in rows])

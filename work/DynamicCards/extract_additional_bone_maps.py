from pathlib import Path
import sys,json,re,collections,gc
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
r=Path(__file__).resolve().parent;p=r/'source_skin_binding_contracts.json';doc=json.loads(p.read_text())
sources={'Native':Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes'),'Legacy':Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),'Latest':Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')}
for kind,source in sources.items():
 wanted={s['id'] for s in doc['skins'] if s['source']==kind};maps=collections.defaultdict(lambda:collections.defaultdict(set))
 for file in ([source/key for key in wanted] if kind=='Latest' else [source]):
  env=UnityPy.load(str(file));cache={}
  def path(obj):
   if callable(getattr(obj,'deref',None)):obj=obj.deref()
   key=(obj.assets_file.name,obj.path_id)
   if key not in cache:
    d=obj.read();cache[key]=(path(d.m_Father)+'/' if d.m_Father.path_id else '')+d.m_GameObject.read().m_Name
   return cache[key]
  for obj in env.objects:
   match=re.search(r'\d{8}',obj.assets_file.name)
   if not match or match[0] not in wanted:continue
   if obj.type.name=='SkinnedMeshRenderer':
    d=obj.read()
    if not d.m_Mesh.path_id:continue
    hashes=d.m_Mesh.read_typetree().get('m_BoneNameHashes',[])
    for h,bone in zip(hashes,d.m_Bones):
     if bone.path_id:maps[match[0]][h].add(path(bone))
   elif obj.type.name=='Animator':
    d=obj.read()
    if not d.m_Avatar.path_id:continue
    go=d.m_GameObject.read();tr=next(c.component for c in go.m_Component if c.component.deref().type.name=='Transform');anchor=path(tr)
    for h,name in d.m_Avatar.read_typetree().get('m_TOS',[]):maps[match[0]][h].add(anchor+'/'+name)
  del env;gc.collect()
 for skin in doc['skins']:
  if skin['source']==kind:skin['mappedPaths']=[dict(hash=h,path=name) for h,paths in maps[skin['id']].items() for name in paths]
 print('SOURCE_BONE_MAPS',kind,len(maps),flush=True)
p.write_text(json.dumps(doc,indent=2))

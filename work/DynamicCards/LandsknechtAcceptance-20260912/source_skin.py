from pathlib import Path
import sys,json,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent
S=Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets')
env=UnityPy.load(str(S/'scenes'))
def path(p):
    d=p.read();return (path(d.m_Father)+'/' if d.m_Father.path_id else '')+d.m_GameObject.read().m_Name
rows=[]
paths={}
for o in env.objects:
    if '15210100' not in o.assets_file.name or o.type.name!='Animator':continue
    d=o.read()
    if d.m_Avatar.path_id:
        paths.update(d.m_Avatar.read_typetree().get('m_TOS',[]))
for o in env.objects:
    if '15210100' not in o.assets_file.name or o.type.name!='SkinnedMeshRenderer':continue
    d=o.read();mesh=d.m_Mesh.read_typetree()
    row=dict(name=d.m_GameObject.read().m_Name,mesh=mesh['m_Name'],bones=[path(b) if b.path_id else '' for b in d.m_Bones],hashes=mesh.get('m_BoneNameHashes',[]),bindposes=mesh.get('m_BindPose'),rootBone=path(d.m_RootBone) if d.m_RootBone.path_id else '')
    rows.append(row)
    row['resolvedBones']=[paths.get(h,'MISSING:'+str(h)) for h in row['hashes']]
    print(row['name'],row['mesh'],[p.split('/')[-1] for p in row['resolvedBones']],flush=True)
(W/'source-skins.json').write_text(json.dumps(rows,indent=2))

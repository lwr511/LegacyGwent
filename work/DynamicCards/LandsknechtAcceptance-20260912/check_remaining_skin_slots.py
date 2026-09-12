from pathlib import Path
import sys,json,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
W=Path(__file__).resolve().parent
S=Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
env=UnityPy.load(str(S));rows=[]
def path(p):
    if not p.path_id:return ''
    d=p.read();return (path(d.m_Father)+'/' if d.m_Father.path_id else '')+d.m_GameObject.read().m_Name
for o in env.objects:
    if '12130100' not in o.assets_file.name or o.type.name!='SkinnedMeshRenderer':continue
    d=o.read();mesh=d.m_Mesh.read();hashes=mesh.m_BoneNameHashes or []
    if 4176565552 not in hashes:continue
    h=MeshHandler(mesh);h.process();used=sorted({i for inds,weights in zip(h.m_BoneIndices or [],h.m_BoneWeights or []) for i,w in zip(inds,weights) if w>0})
    rows.append(dict(skin=d.m_GameObject.read().m_Name,hashes=hashes,bones=[path(b) for b in d.m_Bones],used=used))
(W/'fire-bomb-source-slot.json').write_text(json.dumps(rows,indent=2))
for row in rows:print(row['skin'],'used',row['used'],'root hash slot',[(i,row['bones'][i] if i<len(row['bones']) else 'ABSENT') for i,h in enumerate(row['hashes']) if h==4176565552])

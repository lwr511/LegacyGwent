from pathlib import Path
import re
p=Path(r'C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/Latest/10090101')
s=p.joinpath('Card.prefab').read_text(); docs=re.split(r'^--- !u!',s,flags=re.M)[1:]; names={}; trs={}; anim=[]
for d in docs:
 typ,oid=re.match(r'(\d+) &(-?\d+)',d).groups(); oid=int(oid)
 if typ=='1':names[oid]=re.search(r'  m_Name: (.*)',d)[1]
 if typ=='4':trs[oid]=(int(re.search(r'm_GameObject: \{fileID: (-?\d+)',d)[1]),int(re.search(r'm_Father: \{fileID: (-?\d+)',d)[1]))
 if typ=='95':anim.append((re.search(r'm_GameObject: \{fileID: (-?\d+)',d)[1],re.search(r'm_Controller: (.*)',d)[1]))
def path(i):
 if not i:return ''
 g,parent=trs[i];return path(parent)+'/'+names[g]
for i,(g,par) in trs.items():
 if 'geralt_head' in names[g] or names[g]=='model':print(path(i))
print('ANIMATORS',[(names[int(g)],c) for g,c in anim])
import struct,zlib
mesh=p.parent/'Shared/10090101_152272_Skinned_mesh.asset'
hs=struct.unpack('<'+'I'*(len(re.search(r'm_BoneNameHashes: (\w+)',mesh.read_text())[1])//8),bytes.fromhex(re.search(r'm_BoneNameHashes: (\w+)',mesh.read_text())[1]))
for d in docs:
 if not d.startswith('137 '):continue
 boneblock=d.split('  m_Bones:\n')[1].split('  m_BlendShapeWeights:')[0]
 bones=[int(x) for x in re.findall(r'fileID: (-?\d+)',boneblock)]
 print('counts',len(bones),len(hs))
 bad=[]
 for i,(b,h) in enumerate(zip(bones,hs)):
  bp=path(b); segs=bp.strip('/').split('/')
  if not any(zlib.crc32('/'.join(segs[j:]).encode())==h for j in range(len(segs))):bad.append((i,bp,h))
 print('BONE_HASH_MISMATCH',bad[:8],len(bad))
import json,sys
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np
r=Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/LatestAnimationData')
actors=json.loads((r/'animations.json').read_text())['animators']
for a in actors:
 if a['id']!='10090101':continue
 print('ACTOR',a['path'])
 for clip in a['clips']:
  print('CLIP',clip['name'],clip['duration'])
  arr=np.fromfile(r/clip['file'],dtype='<f4').reshape(clip['frames'],clip['columns'])
  for tr in clip['tracks']:
   if tr['path'].endswith('Geralt_Swordmaster_geralt_head_AuxSHJnt'):
    print(tr,'first',arr[0,tr['offset']:tr['offset']+tr['dimension']].tolist(),'last',arr[-1,tr['offset']:tr['offset']+tr['dimension']].tolist())
for d in docs:
 if d.startswith('4 ') and 'm_GameObject:' in d:
  g=int(re.search(r'm_GameObject: \{fileID: (-?\d+)',d)[1])
  if names[g]=='Geralt_Swordmaster_geralt_head_AuxSHJnt': print('REST_HEAD',d[:650])

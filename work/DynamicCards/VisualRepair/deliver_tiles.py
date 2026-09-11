from pathlib import Path
import re,shutil
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');p=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';f=p/'Assets/DynamicCards/Content/Old/Legacy2017/11210701/Card.prefab'
src=w/'FocusedProbe/Assets/VisualRepairTiles';dest=f.parent/'Tiles';shutil.copytree(src,dest,dirs_exist_ok=True)
b=f.read_text();backup=w/'VisualRepair/Golden-prefab-before.txt'
if not backup.exists():backup.write_text(b)
blocks=re.split(r'(?=^--- !u!)',b,flags=re.M)
node=next(x for x in blocks if 'm_Name: NEU_[553]Villentretenmerth_tiles\n' in x);go=re.search(r'&(-?\d+)',node)[1]
cam=next(x for x in blocks if 'm_Name: CamPosition\n' in x);camtr=re.search(r'component: \{fileID: (\d+)\}',cam)[1]
guid=re.search(r'guid: (\w+)',(p/'Assets/DynamicCards/Runtime/DynamicCardTileMotion.cs.meta').read_text())[1]
refs=[]
for name in ['TileA','TileC']:
 t=dest/name/'Card.prefab';s=t.read_text();bs=re.split(r'(?=^--- !u!)',s,flags=re.M);root=next(x for x in bs if 'm_Name: Card\n' in x);rt=re.search(r'component: \{fileID: (\d+)\}',root)[1];tr=next(x for x in bs if x.startswith('--- !u!4 &'+rt+'\n'));child=re.search(r'm_Children:\s+- \{fileID: (\d+)\}',tr)[1];ct=next(x for x in bs if x.startswith('--- !u!4 &'+child+'\n'));childgo=re.search(r'm_GameObject: \{fileID: (\d+)\}',ct)[1];tg=re.search(r'guid: (\w+)',Path(str(t)+'.meta').read_text())[1];refs.append(f'  - {{fileID: {childgo}, guid: {tg}, type: 3}}')
 t.rename(t.with_name('Tile.prefab'));Path(str(t)+'.meta').rename(t.with_name('Tile.prefab.meta'))
id='899990011210701001'
if guid not in b:
 b=b.replace(node,node.replace('  m_Layer:',f'  - component: {{fileID: {id}}}\n  m_Layer:',1))
 b+=f'''--- !u!114 &{id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  TilePrefabs:
'''+ '\n'.join(refs)+f'''
  WorldSpaceCamPos: {{fileID: {camtr}}}
  StartPoint: {{x: 2, y: 0, z: -20}}
  EndPoint: {{x: 2, y: 0, z: 174.4}}
  Speed: 50
  TileSpacing: 48.6
  TileNum: 6
  NoiseSpeed: {{x: 0, y: 0.2}}
'''
 f.write_text(b)
print('STAGED',go,camtr,len(list(dest.rglob('*'))))

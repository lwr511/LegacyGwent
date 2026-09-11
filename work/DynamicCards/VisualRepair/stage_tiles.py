from pathlib import Path
import json,re,shutil
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');src=w/'LegacyBridge2019';dest=w/'FocusedProbe/Assets/VisualRepairTiles';dest.mkdir(exist_ok=True)
manifest={x['source']:x for x in json.loads((w/'legacy_shader_manifest.json').read_text())};idx={}
for f in list((src/'Assets/PortableCards/Shared').glob('Tile*.meta'))+list((src/'Assets/PortableCards/TileA').glob('*.meta'))+list((src/'Assets/PortableCards/TileC').glob('*.meta')):
 m=re.search(r'^guid: (\w+)',f.read_text(),re.M)
 if m:idx[m[1]]=Path(str(f)[:-5])
matsh={m['asset']:manifest[m['shader']]['guid'] for name in ['TileA','TileC'] for m in json.loads((src/f'Assets/PortableCards/{name}/conversion.json').read_text())['materials']}
seen=set()
def cp(f):
 if f in seen:return
 seen.add(f);to=dest/f.relative_to(src/'Assets/PortableCards');to.parent.mkdir(parents=True,exist_ok=True)
 if f.suffix in ['.prefab','.mat','.asset']:
  try:b=f.read_text()
  except UnicodeDecodeError:b=None
  if b is not None:
   if f.suffix=='.mat':b=re.sub(r'(m_Shader: \{fileID: )[^}]+}',lambda m:m[1]+'4800000, guid: '+matsh[f.relative_to(src).as_posix()]+', type: 3}',b)
   for g in set(re.findall(r'guid: (\w{32})',b)):
    if g in idx:cp(idx[g])
   to.write_text(b)
  else:shutil.copy2(f,to)
 else:shutil.copy2(f,to)
 shutil.copy2(str(f)+'.meta',str(to)+'.meta')
for name in ['TileA','TileC']:cp(src/f'Assets/PortableCards/{name}/Card.prefab')
main=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
for s in ['VFX/Effects/CurvedWorld/AdditiveCurvedWorld','Custom/Cards/CardCore/ImageLayerShaderCurvedWorld']:
 f=main/'Assets/DynamicCards/Shaders/Legacy'/manifest[s]['file'];to=w/'FocusedProbe'/f.relative_to(main);to.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,to);shutil.copy2(str(f)+'.meta',str(to)+'.meta')
print('COPIED',len(seen),'bytes',sum(f.stat().st_size for f in dest.rglob('*') if f.is_file()))

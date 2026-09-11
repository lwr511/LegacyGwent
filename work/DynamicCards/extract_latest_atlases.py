from pathlib import Path
import sys,json,hashlib
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
source=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/textures/premium/uber')
out=root/'LatestAtlases';out.mkdir(exist_ok=True)
for bundle in sorted(source.iterdir()):
 if not bundle.name.isdigit():continue
 destination=out/(bundle.name+'.png')
 if destination.exists():continue
 env=UnityPy.load(str(bundle))
 textures=[o.read() for o in env.objects if o.type.name=='Texture2D']
 primary=next((t for t in textures if t.m_Name==bundle.name),textures[0] if len(textures)==1 else None)
 assert primary is not None,(bundle,[t.m_Name for t in textures])
 primary.image.save(destination)
 for texture in textures:
  if texture is not primary:texture.image.save(out/(texture.m_Name+'.png'))
 guid=hashlib.md5(('DynamicCards/LatestAtlas/'+bundle.name).encode()).hexdigest()
 Path(str(destination)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\nTextureImporter:\n  externalObjects: {}\n  textureType: 0\n  alphaSource: 1\n  sRGBTexture: 1\n  maxTextureSize: 4096\n  textureCompression: 2\n')
 print('ATLAS',bundle.name,flush=True)

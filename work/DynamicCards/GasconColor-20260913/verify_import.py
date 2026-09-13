from pathlib import Path
import json,sys,hashlib
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from PIL import Image,ImageChops
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/GasconColor-20260913');p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');src=Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets')
checks=[]
for source,target in [('premium-high-15280100.png',p/'Assets/DynamicCards/Content/Old/Thronebreaker/Shared/15280100_23360_15280100.png'),('standard-high-15280000.png',p/'Assets/StreamingAssets/CollectionDebug/Thronebreaker_15280100.png')]:
 a=Image.open(w/source).convert('RGBA');b=Image.open(target).convert('RGBA');assert a.size==b.size
 error=max(x[1] for x in ImageChops.difference(a,b).getextrema());checks.append(dict(source=source,target=str(target),maxChannelDifference=error,sourcePixelsSha256=hashlib.sha256(a.tobytes()).hexdigest(),importedPixelsSha256=hashlib.sha256(b.tobytes()).hexdigest()))
e=UnityPy.load(str(src/'scenes'));materials=[]
for o in e.objects:
 if o.type.name!='Material' or '15280100' not in o.assets_file.name:continue
 m=o.read();materials.append(dict(name=m.m_Name,file=o.assets_file.name,pathID=o.path_id,floats=dict(m.m_SavedProperties.m_Floats),colors={k:dict(r=v.r,g=v.g,b=v.b,a=v.a) for k,v in m.m_SavedProperties.m_Colors}))
result=dict(textures=checks,sourceMaterials=materials);(w/'source-verification.json').write_text(json.dumps(result,indent=2));print(json.dumps(dict(textures=checks,materials=[m for m in materials if 'cavalry' in m['name'] or 'horsemen' in m['name']]),indent=2))

from pathlib import Path
import sys,json,re,gc
root=Path(__file__).resolve().parent
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy,numpy as np
from PIL import Image
exec((root/'source_catalog.py').read_text().split('def signature(image):')[1].split('client=Path')[0].join(['def signature(image):','']))
project=root.parents[1];client=project/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
text=(project/'src/Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf8')
arts=set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"',text))
catalog=json.loads((root/'Probe/Assets/DynamicCards/Content/catalog.json').read_text())['cards']
covered={a for c in catalog for a in c['artIds']};remaining=arts-covered
images=[]
for art in sorted(remaining):
 path=client/'Assets/Addressables/Cards'/(art+'.png')
 if path.exists():images.append((art,signature(Image.open(path))))
matrix=np.stack([s for _,s in images]);best={a:dict(error=1) for a,_ in images};matches=[]
sources=[('Native',Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets')),
 ('Legacy',Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets')),
 ('Latest',Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets'))]
for kind,source in sources:
 known=set(json.loads((root/({'Native':'source_effects.json','Legacy':'second_source_effects.json','Latest':'latest_source_effects.json'}[kind])).read_text()))
 paths=list((source/'textures/standard/high').iterdir()) if kind=='Latest' else [source/'textures/standard/high']
 for index,path in enumerate(paths):
  if path.is_file() and kind=='Latest' and not path.name.isdigit():continue
  env=UnityPy.load(str(path))
  for obj in env.objects:
   if obj.type.name!='Texture2D':continue
   data=obj.read();m=re.search(r'\d{8}',data.m_Name)
   if not m:continue
   scene=m[0][:-1]+'1' if kind=='Legacy' else m[0][:4]+('0100' if kind=='Native' else '0101')
   if scene not in known:continue
   scores=np.mean((matrix-signature(data.image))**2,axis=1)
   for i,(art,_) in enumerate(images):
    error=float(scores[i]);record=dict(art=art,source=kind,scene=scene,error=error,standard=data.m_Name)
    if error<best[art]['error']:best[art]=record
    if error<.0005:matches.append(record)
  del env
  if index%100==0:print('REMAINING_MATCH',kind,index,flush=True)
 gc.collect()
(root/'remaining_project_matches.json').write_text(json.dumps(dict(matches=matches,best=best,missingImages=sorted(remaining-{a for a,_ in images})),indent=2))
print('REMAINING_PROJECT_MATCHES',len(images),len({m['art'] for m in matches}),flush=True)

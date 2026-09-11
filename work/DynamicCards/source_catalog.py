"""Read the installed source; write all conversion evidence into this workspace."""
import sys, json, re
from pathlib import Path
OLD = Path(r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work')
sys.path.insert(0, str(OLD/'python_deps'))
import UnityPy
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parent
SOURCE=Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets')
def vec(v): return [v.x,v.y,v.z]
env=UnityPy.load(str(SOURCE/'scenes'))
cards={}
for obj in env.objects:
    if obj.type.name=='GameObject':
        match=re.search(r'(\d{8})',obj.assets_file.name)
        if match: cards.setdefault(match[1],dict(id=match[1],particles=0,materials=[],scripts=[]))
for obj in env.objects:
    match=re.search(r'(\d{8})',obj.assets_file.name)
    if not match or match[1] not in cards: continue
    c=cards[match[1]]
    if obj.type.name=='ParticleSystem': c['particles']+=1
    elif obj.type.name=='Material': c['materials'].append(obj.read().m_Name)
    elif obj.type.name=='MonoBehaviour':
        try:
            tree=obj.read_typetree(); data=obj.read()
            name=data.m_Script.deref().read().m_Name
            c['scripts'].append(dict(type=name,data=tree))
        except Exception: pass
(ROOT/'source_catalog.json').write_text(json.dumps(list(cards.values()),indent=2),encoding='utf8')
print('Source scenes',len(cards),flush=True)
# Compare low-resolution color structure instead of matching unrelated numeric IDs.
def signature(image):
    image=image.convert('RGB')
    pixels=np.asarray(image); mask=pixels.max(axis=2)>8
    xs=np.where(mask.mean(axis=0)>0.02)[0]; ys=np.where(mask.mean(axis=1)>0.02)[0]
    if len(xs) and len(ys): image=image.crop((int(xs[0]),int(ys[0]),int(xs[-1])+1,int(ys[-1])+1))
    # Static art has no frame; cover-crop to the same portrait ratio.
    w,h=image.size; ratio=0.685
    if w/h>ratio: image=image.crop(((w-h*ratio)/2,0,(w+h*ratio)/2,h))
    else: image=image.crop((0,(h-w/ratio)/2,w,(h+w/ratio)/2))
    return np.asarray(image.resize((16,24)),dtype=np.float32).reshape(-1)/255
client=Path(r'C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
legacy=[]
for p in (client/'Assets/Addressables/Cards').glob('*.png'):
    if re.fullmatch(r'\d+',p.stem): legacy.append((p.stem,signature(Image.open(p))))
matrix=np.stack([s for _,s in legacy])
matches=[]
standard=UnityPy.load(str(SOURCE/'textures/standard/high'))
for obj in standard.objects:
    if obj.type.name!='Texture2D': continue
    data=obj.read(); art=re.search(r'\d{8}',data.m_Name)
    if not art: continue
    # Premium IDs use 01 where the standard equivalent uses 00.
    premium=art[0][:4]+'0100'
    if premium not in cards: continue
    sig=signature(data.image)
    distances=np.mean((matrix-sig)**2,axis=1); ix=np.argsort(distances)[:3]
    matches.append(dict(sourceId=premium,standardName=data.m_Name,candidates=[dict(artId=legacy[i][0],mse=float(distances[i])) for i in ix]))
(ROOT/'art_matches.json').write_text(json.dumps(matches,indent=2),encoding='utf8')
print('Image candidates',len(matches),'Legacy images',len(legacy),flush=True)

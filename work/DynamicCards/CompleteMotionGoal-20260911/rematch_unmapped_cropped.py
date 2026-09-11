from pathlib import Path
import sys,json,re,gc
W=Path(__file__).resolve().parent;B=W.parent
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy,numpy as np
from PIL import Image,ImageOps,ImageDraw
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
out=W/'cropped-rematch';out.mkdir(exist_ok=True)
missing=json.loads((W/'missing-source-id-inventory.json').read_text());arts=[r['art'] for r in missing]
def crop(im):
    if im.mode=='RGBA' and im.getextrema()[3][0]<im.getextrema()[3][1]:
        box=im.getchannel('A').getbbox()
        if box:im=im.crop(box)
    im=im.convert('RGB');px=np.asarray(im).astype(np.int16)
    gx=np.max(np.abs(np.diff(px,axis=1)),axis=2);gy=np.max(np.abs(np.diff(px,axis=0)),axis=2)
    xs=np.where((gx>3).mean(axis=0)>.025)[0];ys=np.where((gy>3).mean(axis=1)>.025)[0]
    if len(xs) and len(ys):im=im.crop((int(xs[0]),int(ys[0]),int(xs[-1])+1,int(ys[-1])+1))
    return im
def sig(im):return np.asarray(ImageOps.fit(crop(im),(32,46)),dtype=np.float32).reshape(-1)/255
matrix=np.stack([sig(Image.open(P/'Assets/Addressables/Cards'/(a+'.png'))) for a in arts]);best={a:[] for a in arts}
roots={'Legacy':Path('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/textures/standard/high'),'Native':Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/textures/standard/high'),'Latest':Path('C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/textures/standard/high')}
for kind,root in roots.items():
    known=set(json.loads((B/{'Native':'source_effects.json','Legacy':'second_source_effects.json','Latest':'latest_source_effects.json'}[kind]).read_text()))
    paths=[root] if kind!='Latest' else [p for p in root.iterdir() if p.name.isdigit()]
    for i,path in enumerate(paths):
        env=UnityPy.load(str(path))
        for obj in env.objects:
            if obj.type.name!='Texture2D':continue
            d=obj.read();m=re.search(r'\d{8}',d.m_Name)
            if not m:continue
            scene=m[0][:-1]+'1' if kind=='Legacy' else m[0][:4]+('0100' if kind=='Native' else '0101')
            if scene not in known:continue
            im=d.image;scores=np.mean((matrix-sig(im))**2,axis=1)
            for a,score in zip(arts,scores):
                if len(best[a])<3 or score<best[a][-1]['error']:
                    best[a].append(dict(source=kind,scene=scene,standard=d.m_Name,error=float(score)))
                    best[a].sort(key=lambda r:r['error']);del best[a][3:]
                    crop(im).save(out/(kind+'-'+scene+'.png'))
        del env
        if i%250==0:print(kind,i,flush=True)
    gc.collect()
(out/'candidates.json').write_text(json.dumps(best,indent=2))
for page in range(3):
    subset=arts[page*7:(page+1)*7];sheet=Image.new('RGB',(760,240*len(subset)),'#202020');draw=ImageDraw.Draw(sheet)
    for row,a in enumerate(subset):
        im=crop(Image.open(P/'Assets/Addressables/Cards'/(a+'.png')));sheet.paste(ImageOps.contain(im,(175,205)),(0,row*240+25));draw.text((0,row*240+2),a,fill='white')
        for col,c in enumerate(best[a],1):
            im=Image.open(out/(c['source']+'-'+c['scene']+'.png'));sheet.paste(ImageOps.contain(im,(175,205)),(col*190,row*240+25));draw.text((col*190,row*240+2),c['source']+' '+c['scene'],fill='white')
    sheet.save(out/('sheet-'+str(page)+'.jpg'))
print('COMPLETE',len(best),[(a,b[0]) for a,b in best.items() if b[0]['error']<.002],flush=True)

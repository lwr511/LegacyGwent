from pathlib import Path
import sys,json,re,xml.etree.ElementTree as ET
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np
from PIL import Image,ImageDraw
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
r=Path(__file__).resolve().parent; work=r.parent; project=work.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
out=r/'Rematch';out.mkdir(exist_ok=True)
catalog=json.loads((project/'Assets/DynamicCards/Content/catalog.json').read_text())
bound={a for c in catalog['cards'] for a in c['artIds']}; scenes={c['id'] for c in catalog['cards']}
arts=set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"',(work.parents[1]/'src/Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf8')))
missing=sorted(arts-bound)
def sig(im):
    im=im.convert('RGB');px=np.asarray(im).astype(np.int16);gx=np.max(np.abs(np.diff(px,axis=1)),axis=2);gy=np.max(np.abs(np.diff(px,axis=0)),axis=2);xs=np.where((gx>3).mean(axis=0)>.01)[0];ys=np.where((gy>3).mean(axis=1)>.01)[0]
    if len(xs) and len(ys):im=im.crop((int(xs[0]),int(ys[0]),int(xs[-1])+1,int(ys[-1])+1))
    w,h=im.size;ratio=.685
    if w/h>ratio:im=im.crop(((w-h*ratio)/2,0,(w+h*ratio)/2,h))
    else:im=im.crop((0,(h-w/ratio)/2,w,(h+w/ratio)/2))
    return np.asarray(im.resize((16,24)),dtype=np.float32).reshape(-1)/255
missing=[a for a in missing if (project/f'Assets/Addressables/Cards/{a}.png').exists()]
matrix=np.stack([sig(Image.open(project/f'Assets/Addressables/Cards/{a}.png')) for a in missing]); candidates={a:[] for a in missing}
source=Path(json.loads((work/'third_source_inventory.json').read_text())['source'])/'textures/standard/high'
for i,p in enumerate(source.iterdir()):
    if not p.name.isdigit():continue
    for obj in UnityPy.load(str(p)).objects:
        if obj.type.name!='Texture2D':continue
        t=obj.read();m=re.search(r'\d{8}',t.m_Name)
        if not m:continue
        scene=m[0][:4]+'0101'
        if scene not in scenes:continue
        im=t.image; scores=np.mean((matrix-sig(im))**2,axis=1)
        for a,score in zip(missing,scores):
            items=candidates[a]
            if len(items)<3 or score<items[-1]['error']:
                items.append(dict(scene=scene,error=float(score),texture=t.m_Name));items.sort(key=lambda x:x['error']);del items[3:]
                im.convert('RGB').resize((137,200)).save(out/(scene+'.jpg'))
    if i%150==0:print('REMATCH',i,flush=True)
(out/'candidates.json').write_text(json.dumps(candidates,indent=2))
for start in range(0,len(missing),6):
    sheet=Image.new('RGB',(680,6*230),(35,35,35));d=ImageDraw.Draw(sheet)
    for row,a in enumerate(missing[start:start+6]):
        sheet.paste(Image.open(project/f'Assets/Addressables/Cards/{a}.png').convert('RGB').resize((137,200)),(0,row*230+25));d.text((0,row*230+4),a,fill='white')
        for col,item in enumerate(candidates[a],1):
            sheet.paste(Image.open(out/(item['scene']+'.jpg')),(col*170,row*230+25));d.text((col*170,row*230+4),item['scene']+' %.4f'%item['error'],fill='white')
    sheet.save(out/('sheet-%02d.png'%(start//6)))
print('REMATCH_DONE',len(missing),flush=True)

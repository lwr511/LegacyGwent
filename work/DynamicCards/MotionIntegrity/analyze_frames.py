from pathlib import Path
import json,sys,math
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np
from PIL import Image,ImageDraw
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');rows=[]
cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());entries={a:c for c in cat['cards'] for a in c['artIds']}
for f in (w/'PostFrames').glob('*-large.png'):
 art=f.name[:-10];reference=p/'Assets/Addressables/Cards'/(art+'.png')
 if not reference.exists():continue
 a=np.asarray(Image.open(reference).convert('RGB').crop((0,0,497,713)).resize((70,100)),dtype=float)/255
 margin=entries[art].get('topMargin',0);side=math.ceil(margin*648/947*.5);im=Image.open(f).convert('RGB');scale=im.width/1024;im=im.crop(tuple(v*scale for v in (189+side,43+margin,837-side,990)));b=np.asarray(im.resize((70,100)),dtype=float)/255
 gray_a=a.mean(2);gray_b=b.mean(2);den=np.linalg.norm(gray_a-gray_a.mean())*np.linalg.norm(gray_b-gray_b.mean());corr=float(np.sum((gray_a-gray_a.mean())*(gray_b-gray_b.mean()))/max(den,1e-8))
 rows.append({'art':art,'brightnessRatio':float(b.mean()/max(a.mean(),.001)),'skyBrightnessRatio':float(b[:30].mean()/max(a[:30].mean(),.001)),'darkFraction':float((gray_b<.015).mean()),'magentaFraction':float(((b[:,:,0]>.7)&(b[:,:,1]<.2)&(b[:,:,2]>.7)).mean()),'correlation':corr})
rows.sort(key=lambda r:r['correlation']);(w/'visual-metrics.json').write_text(json.dumps(rows,indent=2));print('Analyzed',len(rows));print('lowest correlations',[(r['art'],round(r['correlation'],3),round(r['brightnessRatio'],2)) for r in rows[:12]])

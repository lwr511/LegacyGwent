from pathlib import Path
import json,sys,math
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps');import numpy as np
from PIL import Image
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());entries={a:c for c in cat['cards'] for a in c['artIds']};rows=[];checked=0
for f in (w/'FinalFrames').glob('*.png'):
 art,mode=f.name.split('-')[:2];mode=mode.removesuffix('.png');im=Image.open(f).convert('RGBA');m=entries[art].get('topMargin',0);side=math.ceil(m*648/947*.5);k=im.width/1024
 if mode=='miniature':
  width=min(.633,.19*100/34);height=width/(100/34);bounds=((.5015-width/2)*im.width,(1-.615-height/2)*im.height,(.5015+width/2)*im.width,(1-.615+height/2)*im.height)
 else:bounds=tuple(v*k for v in (189+side,43+m,837-side,990))
 a=np.asarray(im.crop(bounds).resize((130,190)));clear=(a.max(2)<3);checked+=1
 if clear.mean()>.001:rows.append({'art':art,'mode':mode,'file':f.name,'fraction':float(clear.mean())})
(w/'final-clear-region-audit.json').write_text(json.dumps({'checkedFrames':checked,'threshold':.001,'candidates':rows},indent=2));print('checked',checked,'candidates',len(rows),json.dumps(rows),flush=True)

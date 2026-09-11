from pathlib import Path
import json,math
from PIL import Image,ImageDraw
w=Path(__file__).resolve().parent
p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());entries={a:c for c in cat['cards'] for a in c['artIds']}
files=sorted((w/'PostFrames').glob('*-large.png'))
for page in range((len(files)+47)//48):
 sheet=Image.new('RGB',(1200,1104),(35,35,35));draw=ImageDraw.Draw(sheet)
 for i,f in enumerate(files[page*48:page*48+48]):
  art=f.name[:-10];m=entries[art].get('topMargin',0);side=math.ceil(m*648/947*.5);im=Image.open(f).convert('RGB');scale=im.width/1024
  im=im.crop(tuple(v*scale for v in (189+side,43+m,837-side,990))).resize((114,163))
  ref=p/'Assets/Addressables/Cards'/(art+'.png');x=i%6*200;y=i//6*138
  # A pair per cell: static reference and runtime portrait.
  im=im.resize((85,120));sheet.paste(im,(x+96,y+16))
  if ref.exists():sheet.paste(Image.open(ref).convert('RGB').crop((0,0,497,713)).resize((85,120)),(x,y+16))
  draw.text((x,y),art,fill='white')
 sheet.save(w/('all-cards-%02d.jpg'%page),quality=90)
print('sheets',page+1,'cards',len(files))

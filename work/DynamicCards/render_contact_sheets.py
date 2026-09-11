import sys
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parent/'RenderAudit'
files=sorted(root.glob('[0-9]*.png'))
for page in range((len(files)+63)//64):
    canvas=Image.new('RGB',(1024,1440),(28,29,33));draw=ImageDraw.Draw(canvas)
    for index,path in enumerate(files[page*64:page*64+64]):
        tile=Image.open(path).convert('RGBA').crop((71,16,313,371)).resize((120,160))
        x=(index%8)*128;y=(index//8)*180
        canvas.paste(tile,(x,y),tile);draw.text((x+20,y+162),path.stem,fill='white')
    canvas.save(root/('sheet_%d.jpg'%page))
print('cards',len(files))

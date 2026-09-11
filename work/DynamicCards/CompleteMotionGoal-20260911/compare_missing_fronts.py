from pathlib import Path
import sys,json
W=Path(__file__).resolve().parent; B=W.parent
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from PIL import Image,ImageOps,ImageDraw
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
missing=json.loads((W/'missing-source-id-inventory.json').read_text())
best=json.loads((B/'remaining_project_matches.json').read_text())['best']
records=[dict(r,closest=best.get(r['art'])) for r in missing]
roots={'Legacy':Path('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/textures/standard/high'),'Native':Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/textures/standard/high'),'Latest':Path('C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/textures/standard/high')}
originals={}
for kind,root in roots.items():
    wanted={r['closest']['standard'] for r in records if r['closest'] and r['closest']['source']==kind}
    if not wanted:continue
    paths=[root] if kind!='Latest' else [root/n for n in wanted]
    for path in paths:
        env=UnityPy.load(str(path))
        for obj in env.objects:
            if obj.type.name!='Texture2D':continue
            d=obj.read()
            if d.m_Name in wanted:originals[(kind,d.m_Name)]=d.image.copy()
    print(kind,'loaded',len(originals),flush=True)
out=W/'missing-comparison';out.mkdir(exist_ok=True)
for page in range(3):
    rows=records[page*7:(page+1)*7];sheet=Image.new('RGB',(1000,370*((len(rows)+1)//2)), '#202020');draw=ImageDraw.Draw(sheet)
    for i,r in enumerate(rows):
        x=(i%2)*500;y=(i//2)*370;b=r['closest']
        ours=Image.open(P/'Assets/Addressables/Cards'/(r['art']+'.png'))
        img=ImageOps.contain(ours.convert('RGB'),(228,316));sheet.paste(img,(x,y+25))
        draw.text((x+4,y+4),r['art'],fill='white')
        if b:
            original=originals.get((b['source'],b['standard']))
            if original:
                img=ImageOps.contain(original.convert('RGB'),(228,316));sheet.paste(img,(x+250,y+25))
            draw.text((x+250,y+4),b['source']+' '+b['scene'],fill='white')
            draw.text((x+250,y+345),'MSE '+str(round(b['error'],6)),fill='white')
    sheet.save(out/('page-'+str(page)+'.jpg'))
(out/'records.json').write_text(json.dumps(records,indent=2))
print('COMPLETE',len(records))

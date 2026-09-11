import sys,json,re
from pathlib import Path
root=Path(__file__).resolve().parent
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy,numpy as np
from PIL import Image
exec((root/'source_catalog.py').read_text().split('def signature(image):')[1].split('client=Path')[0].join(['def signature(image):','']))
client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
legacy=[(p.stem,signature(Image.open(p))) for p in (client/'Assets/Addressables/Cards').glob('*.png') if p.stem.isdigit()]
matrix=np.stack([s for _,s in legacy])
inventory=json.loads((root/'third_source_inventory.json').read_text());inventory['scenes']={c['id'] for c in inventory['cards']}
def source_objects():
    for i,p in enumerate((Path(inventory['source'])/'textures/standard/high').iterdir()):
        if not p.name.isdigit():continue
        yield from UnityPy.load(str(p)).objects
        if i%100==0:print('LATEST_MATCH_PROGRESS',i,flush=True)
catalog=json.loads((client/'Assets/DynamicCards/Content/catalog.json').read_text())['cards']
existing={a for c in catalog for a in c['artIds']}
result=[]
for o in source_objects():
    if o.type.name!='Texture2D':continue
    data=o.read();match=re.search(r'\d{8}',data.m_Name)
    if not match:continue
    premium=match[0][:4]+'0101'
    if premium not in inventory['scenes']:continue
    scores=np.mean((matrix-signature(data.image))**2,axis=1)
    ids=[legacy[i][0] for i in np.where(scores<.0005)[0]]
    result.append({'id':premium,'standard':data.m_Name,'artIds':ids,'newArtIds':[a for a in ids if a not in existing],'bestError':float(scores.min())})
(root/'latest_source_matches.json').write_text(json.dumps(result,indent=2))
print('LEGACY_ART',len(legacy),'SOURCE_MATCHED',sum(bool(c['artIds']) for c in result),'NEW_SCENES_NEEDED',sum(bool(c['newArtIds']) for c in result),'NEW_ART_IDS',len({a for c in result for a in c['newArtIds']}),flush=True)

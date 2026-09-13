from pathlib import Path
import json,sys
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy,numpy as np
from PIL import Image
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/GasconColor-20260913');root=Path('C:/UnityProjects/LegacyGwent/Builds/Windows-20260913-FlashCardsFinal/DiyGwent_Data/StreamingAssets/DynamicCards');idx=json.loads((root/'cards.index.json').read_text());part=next(x for x in idx['parts'] if any('/15280100/Card.prefab' in p for p in x['prefabs']));e=UnityPy.load(str(root/part['file']));seen=set();rows=[];scripts=[];source=json.loads((w/'source-verification.json').read_text());sourceByName={r['name']:r for r in source['sourceMaterials']};comparisons=[]
def walk(ptr):
 g=ptr.read()
 for cp in g.m_Component:
  o=cp.component.deref()
  if o.type.name in ['Transform','RectTransform']:
   for c in o.read().m_Children:walk(c.read().m_GameObject)
  elif o.type.name=='MonoBehaviour':scripts.append(str(o.read().m_Script.read().m_Name))
  elif o.type.name in ['MeshRenderer','SkinnedMeshRenderer','ParticleSystemRenderer']:
   for mp in o.read().m_Materials:
    if not mp.path_id or mp.path_id in seen:continue
    seen.add(mp.path_id);m=mp.read();rows.append(dict(renderer=g.m_Name,material=m.m_Name,textures={k:(v.m_Texture.read().m_Name if v.m_Texture.path_id else None) for k,v in m.m_SavedProperties.m_TexEnvs}))
    original=next((s for s in sourceByName.values() if s['name'].replace('[','_').replace(']','_') in m.m_Name),None)
    # Exported names have one sanitized separator per nonalphanumeric character.
    if original is None:
     import re
     original=next((s for s in sourceByName.values() if m.m_Name.endswith(re.sub(r'[^A-Za-z0-9_.-]','_',s['name']))),None)
    assert original is not None,m.m_Name
    for key,val in original['floats'].items():assert abs(dict(m.m_SavedProperties.m_Floats)[key]-val)<1e-5,(m.m_Name,key)
    for key,col in original['colors'].items():
     actual=dict(m.m_SavedProperties.m_Colors)[key];assert all(abs(getattr(actual,ch)-col[ch])<1e-5 for ch in 'rgba'),(m.m_Name,key)
for path,ptr in e.container.items():
 if path.endswith('/15280100/card.prefab'):walk(ptr)
for o in e.objects:
 if o.type.name!='Texture2D':continue
 d=o.read()
 if d.m_Name!='15280100_23360_15280100':continue
 d.image.save(w/'packaged-atlas.png');a=np.asarray(Image.open(w/'premium-high-15280100.png').convert('RGBA')).astype(float);b=np.asarray(d.image.convert('RGBA')).astype(float);mask=a[:,:,3]>128;diff=(b-a)[mask,:3]
 comparisons.append(dict(texture=d.m_Name,maxAbsDifference=float(np.abs(diff).max()),meanRgbBias=diff.mean(axis=0).tolist(),meanAbsRgbError=np.abs(diff).mean(axis=0).tolist()))
assert len(rows)==10 and len(comparisons)==1 and not scripts,(len(rows),comparisons,scripts)
(w/'packaged-verification.json').write_text(json.dumps(dict(part=part['file'],materials=rows,materialParametersMatchSource=True,scripts=scripts,textureComparisons=comparisons),indent=2));print('Packaged materials:',len(rows),'all source parameters match; scripts:',scripts,'texture:',comparisons)

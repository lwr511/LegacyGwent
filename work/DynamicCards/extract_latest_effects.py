import sys,json,re,struct,subprocess,collections,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parent
OLD=Path(r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work')
sys.path.insert(0,str(OLD/'python_deps'))
import UnityPy
BASE=Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets')
source=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')
cards={p.name:dict(id=p.name) for p in source.iterdir() if p.name.isdigit()}
def source_objects():
    for i,key in enumerate(sorted(cards)):
        env=UnityPy.load(str(source/key))
        yield from env.objects
        if i%50==0:print('LATEST_EFFECTS_PROGRESS',i,flush=True)
def resolve(owner,ref):
    if not isinstance(ref,dict) or not ref.get('m_PathID'): return None
    from UnityPy.classes import PPtr
    try: return PPtr(m_FileID=ref['m_FileID'],m_PathID=ref['m_PathID'],assetsfile=owner.assets_file).deref()
    except Exception:return None
def path(obj):
    if obj is None:return ''
    d=obj.read()
    if obj.type.name not in ['GameObject','Transform','RectTransform']:
        return path(d.m_GameObject.deref()) if hasattr(d,'m_GameObject') else d.m_Name if hasattr(d,'m_Name') else ''
    if obj.type.name=='GameObject':
        t=next((c.component.deref() for c in d.m_Component if c.component.deref().type.name in ['Transform','RectTransform']),None)
        return path(t)
    name=d.m_GameObject.deref().read().m_Name
    return (path(d.m_Father.deref())+'/' if d.m_Father.path_id else '')+name
effects={k:dict(scripts=[],events=[],textureAssignments=[]) for k in cards}
for obj in source_objects():
    m=re.search(r'\d{8}',obj.assets_file.name)
    if not m or m[0] not in cards:continue
    e=effects[m[0]]
    if obj.type.name=='MonoBehaviour':
        try:
            d=obj.read();name=d.m_Script.deref().read().m_Name;t=obj.read_typetree();p=path(obj)
            references={}
            def visit(v,key=''):
                if isinstance(v,dict):
                    if 'm_PathID' in v:references[key]=path(resolve(obj,v))
                    else:
                        for k,w in v.items():visit(w,key+'.'+k if key else k)
                elif isinstance(v,list):
                    for i,w in enumerate(v):visit(w,key+'.'+str(i))
            visit(t)
            e['scripts'].append(dict(type=name,path=p,data=t,references=references))
            if name=='PremiumCardsMeshMaterialHandler':
                for a in t.get('PremiumTextureAssigments',[]):
                    material=resolve(obj,a['Material'])
                    if material:e['textureAssignments'].append(dict(material=material.read().m_Name,properties=a['Assigments']))
        except Exception as ex:print('Script issue',m[0],str(ex),flush=True)
    elif obj.type.name=='AnimationClip':
        t=obj.read_typetree()
        for event in t.get('m_Events',[]):e['events'].append(dict(clip=t['m_Name'],**event))
(ROOT/'latest_source_effects.json').write_text(json.dumps(effects,indent=2),encoding='utf8')
print('Effect metadata',len(effects),flush=True)

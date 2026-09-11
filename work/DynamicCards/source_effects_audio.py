import sys,json,re,struct,subprocess,collections,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parent
OLD=Path(r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work')
sys.path.insert(0,str(OLD/'python_deps'))
import UnityPy
BASE=Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets')
env=UnityPy.load(str(BASE/'bundledassets/cardassets/scenes'))
cards={x['id']:x for x in json.loads((ROOT/'source_catalog.json').read_text())}
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
effects={k:dict(scripts=[],events=[],textureAssignments=[],sharedTextureAssignments=[],nonRenderingPaths=[]) for k in cards}
for obj in env.objects:
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
                for a in t.get('SharedTextureAssigments',[]):
                    material=resolve(obj,a['Material'])
                    if material:e['sharedTextureAssignments'].append(dict(material=material.read().m_Name,properties=a['Assigments']))
        except Exception as ex:print('Script issue',m[0],str(ex),flush=True)
    elif obj.type.name in ['MeshRenderer','SkinnedMeshRenderer','ParticleSystemRenderer']:
        renderer=obj.read()
        if not any(material.path_id for material in renderer.m_Materials):e['nonRenderingPaths'].append(path(obj))
    elif obj.type.name=='AnimationClip':
        t=obj.read_typetree()
        for event in t.get('m_Events',[]):e['events'].append(dict(clip=t['m_Name'],**event))
(ROOT/'source_effects.json').write_text(json.dumps(effects,indent=2),encoding='utf8')
print('Effect metadata',len(effects),flush=True)

# Link by the original event ID, not by a fuzzy card name.
inclusion=json.loads((BASE/'audio/event_inclusion.json').read_text())['InclusionMapping']
event_banks={str(event):bank for bank,events in inclusion.items() if bank.lower().endswith('_pre') for event in events}
audio_ids={}
for item in ET.parse(OLD/'defs_CardAudio.xml').iter('CardAudio'):
    if item.get('id'):
        banks={event_banks[str(int(s.get('event'))&0xffffffff)] for s in item.iter('SoundEffect') if str(int(s.get('event'))&0xffffffff) in event_banks}
        if len(banks)==1:audio_ids[item.get('id')]=banks.pop()
card_banks=collections.defaultdict(set)
for item in ET.parse(OLD/'defs_Templates.xml').getroot():
    if item.get('AudioId') in audio_ids:card_banks[item.get('ArtId','')+'0100'].add(audio_ids[item.get('AudioId')])
def fnv(s):
    h=2166136261
    for c in s.lower().encode():h=((h*16777619)&0xffffffff)^c
    return h
data=(BASE/'audio/cards.pck').read_bytes();p=28+struct.unpack_from('<I',data,12)[0];count=struct.unpack_from('<I',data,p)[0];p+=4;entries={}
for _ in range(count):
    ident,block,size,off,language=struct.unpack_from('<5I',data,p);p+=20;entries[ident]=data[off*block:off*block+size]
out=ROOT/'Audio';out.mkdir(exist_ok=True);result={};distribution=collections.Counter()
for card in cards:
    banks=card_banks.get(card,set())
    if len(banks)!=1:continue
    bank=next(iter(banks));b=entries.get(fnv(bank))
    if b is None:continue
    chunks={};p=0
    while p+8<=len(b):
        tag=b[p:p+4];size=struct.unpack_from('<I',b,p+4)[0];chunks[tag]=b[p+8:p+8+size];p+=8+size
    idx=chunks.get(b'DIDX',b'');media=[struct.unpack_from('<III',idx,p) for p in range(0,len(idx),12)]
    distribution[len(media)]+=1;files=[]
    for ident,off,size in media:
        raw=out/(str(ident)+'.wem');dest=out/(str(ident)+'.wav')
        if not dest.exists():
            raw.write_bytes(chunks[b'DATA'][off:off+size])
            proc=subprocess.run([str(OLD/'vgmstream/vgmstream-cli.exe'),'-i','-o',str(dest),str(raw)],capture_output=True)
            if proc.returncode:print('Audio decode failed',card,ident,flush=True);continue
        files.append(dict(file=dest.name,bytes=size))
    result[card]=dict(bank=bank,files=files)
(ROOT/'source_audio.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print('Audio cards',len(result),'media per bank',dict(distribution),flush=True)

from pathlib import Path
import json,sys,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.classes import PPtr
W=Path(__file__).resolve().parent;effects=json.loads((W.parent/'second_source_effects.json').read_text());ids={r['scene'] for r in json.loads((W/'hayate-contracts.json').read_text())['records']};env=UnityPy.load('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes');objects={};cache={};rows=[]
for o in env.objects:
    if o.type.name!='GameObject':continue
    m=re.search(r'\d{8}',o.assets_file.name)
    if m and m[0] in ids:objects.setdefault((m[0],o.path_id),[]).append(o)
def transform(o):
    if o.type.name=='Transform':return o
    if o.type.name=='GameObject':return next(c.component.deref() for c in o.read().m_Component if c.component.deref().type.name=='Transform')
    raise ValueError(o.type.name)
def route(tr):
    key=(tr.assets_file.name,tr.path_id)
    if key in cache:return cache[key]
    t=tr.read();name=t.m_GameObject.read().m_Name;prior=[];ordinal=0
    if t.m_Father.path_id:
        parent=t.m_Father.read();prior=route(t.m_Father.deref())
        for child in parent.m_Children:
            if child.path_id==tr.path_id:break
            if child.read().m_GameObject.read().m_Name==name:ordinal+=1
    cache[key]=prior+[(name,ordinal)];return cache[key]
for scene in sorted(ids):
    for s in effects[scene]['scripts']:
        if s['type']!='Hayate':continue
        selfref=s['data']['m_GameObject'];candidates=[o for o in objects[(scene,selfref['m_PathID'])] if '/'.join(n for n,k in route(transform(o)))==s['path']];assert len(candidates)==1,(scene,s['path'],len(candidates))
        owner=candidates[0];selfroute=route(transform(owner));refs=[]
        for field,path in s['references'].items():
            if field.split('.')[0] not in {'Turbulence','transformParticle','followTransform','attractors','meshTarget','skinnedMeshTarget'} or not path:continue
            raw=s['data']
            for part in field.split('.'):raw=raw[int(part)] if isinstance(raw,list) else raw[part]
            target=PPtr(m_FileID=raw['m_FileID'],m_PathID=raw['m_PathID'],assetsfile=owner.assets_file).deref();parts=route(transform(target));assert '/'.join(n for n,k in parts)==path
            refs.append({'field':field,'ordinals':[k for n,k in parts]})
        rows.append({'scene':scene,'path':s['path'],'objectId':selfref['m_PathID'],'ordinals':[k for n,k in selfroute],'references':refs})
(W/'hayate-source-object-routes.json').write_text(json.dumps(rows,indent=2));print('ROUTES',len(rows),'nonzero',[(r['scene'],r['objectId'],r['ordinals']) for r in rows if any(r['ordinals'])])

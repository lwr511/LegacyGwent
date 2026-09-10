import sys,json,struct,zlib,bisect,re
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy,numpy as np
root=Path(__file__).resolve().parent
out=root/'DiscreteAnimationData';out.mkdir(exist_ok=True)
needed={'17380101'}
def source_objects():
    source=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')
    shared=UnityPy.Environment();visited=set()
    for i,key in enumerate(sorted(needed)):
        env=UnityPy.load(str(source/key))
        base=source.parent.parent
        def dependencies(file):
            if file in visited:return
            visited.add(file)
            manifest=Path(str(file)+'.manifest')
            if not manifest.exists():return
            for line in manifest.read_text().split('Dependencies:')[-1].splitlines():
                if '/bundledassets/' not in line:continue
                dep=base/line.split('/bundledassets/')[-1].strip()
                if '/textures/' in str(dep):continue
                dependencies(dep)
                shared.load_file(str(dep))
        dependencies(source/key)
        env.cabs.update(shared.cabs)
        yield from env.objects
        if i%50==0:
            print('LATEST_ANIMATION_PROGRESS',i,flush=True)
            (out/'animations.checkpoint.json').write_text(json.dumps({'animators':result,'unmatched':unmatched}))

def transform(go):return next(c.component.deref() for c in go.read().m_Component if c.component.deref().type.name=='Transform')
def path(o):
    d=o.read();name=d.m_GameObject.deref().read().m_Name
    return (path(d.m_Father.deref())+'/' if d.m_Father.path_id else '')+name
def source_rotation_order(binding):
    if binding['typeID'] != 4 or binding['attribute'] != 4:
        return 4
    order = binding['customType']
    # Unity 5's Euler binding codes are 10..15; later serialized clips use 0..5.
    if 10 <= order <= 15:
        order -= 10
    if order not in range(6):
        raise ValueError('Unsupported source Euler rotation order: ' + str(binding['customType']))
    return order

result=[];unmatched=[]
for o in source_objects():
    match=re.search(r'\d{8}',o.assets_file.name)
    if not match or match[0] not in needed or o.type.name!='Animator':continue
    d=o.read()
    if not d.m_Controller.path_id:continue
    tr=transform(d.m_GameObject.deref());anim_path=path(tr);paths={0:''}
    def visit(tr,relative=''):
        paths[zlib.crc32(relative.encode())]=relative
        for child in tr.read().m_Children:
            obj=child.deref();name=obj.read().m_GameObject.deref().read().m_Name
            visit(obj,(relative+'/' if relative else '')+name)
    visit(tr)
    direct_paths=set(paths)
    if d.m_Avatar.path_id:
        for h,p in d.m_Avatar.deref().read_typetree().get('m_TOS',[]):paths.setdefault(h,p)
    controller_obj=d.m_Controller.deref();controller=controller_obj.read_typetree();record={'id':match[0],'path':anim_path,'clips':[]}
    for ref in controller['m_AnimationClips']:
        if not ref['m_PathID']:continue
        from UnityPy.classes import PPtr
        clip_obj=PPtr(m_FileID=ref['m_FileID'],m_PathID=ref['m_PathID'],assetsfile=controller_obj.assets_file).deref()
        t=clip_obj.read_typetree();m=t['m_MuscleClip'];clip=m['m_Clip'];clip=clip.get('data',clip)
        st=clip['m_StreamedClip'];de=clip['m_DenseClip'];cn=clip['m_ConstantClip']['data'];stream=st['curveCount']+st.get('discreteCurveCount',0);dense=de['m_CurveCount']
        dur=m['m_StopTime']-m['m_StartTime'];fps=min(t['m_SampleRate'],30);times=np.linspace(0,dur,min(2701,max(2,round(dur*fps)+1)))
        buf=struct.pack('<%dI'%len(st['data']),*st['data']);p=0;curves={}
        while p+8<=len(buf):
            tm,count=struct.unpack_from('<fi',buf,p);p+=8
            for _ in range(count):
                ix,*co=struct.unpack_from('<i4f',buf,p);p+=20;curves.setdefault(ix,[]).append((tm,co))
        vals=np.zeros((len(times),stream+dense+len(cn)),dtype=np.float32)
        for ix,keys in curves.items():
            if ix>=stream:continue
            kt=[k[0] for k in keys]
            for j,tm in enumerate(times):
                q=max(0,bisect.bisect_right(kt,float(tm)+m['m_StartTime'])-1);a,co=keys[q];dt=float(tm)+m['m_StartTime']-a
                vals[j,ix]=co[3] if abs(a)>1e15 else ((co[0]*dt+co[1])*dt+co[2])*dt+co[3]
        if dense:
            a=np.array(de['m_SampleArray']).reshape(-1,dense);dt=de['m_BeginTime']+np.arange(len(a))/de['m_SampleRate']
            for ix in range(dense):vals[:,stream+ix]=np.interp(times+m['m_StartTime'],dt,a[:,ix])
        if cn:vals[:,stream+dense:]=cn
        tracks=[];offset=0
        for binding in t['m_ClipBindingConstant']['genericBindings']:
            dim={1:3,2:4,3:3,4:3}.get(binding['attribute'],1) if binding['typeID']==4 else 1
            if binding['typeID']==4 and binding['attribute'] in [1,2,3,4]:
                if binding['path'] in paths:tracks.append({'path':paths[binding['path']],'attribute':binding['attribute'],'offset':offset,'dimension':dim,'optional':binding['path'] not in direct_paths,'rotationOrder':source_rotation_order(binding),'hasRotationOrder':True})
                else:unmatched.append({'card':match[0],'clip':t['m_Name'],'hash':binding['path']})
            elif binding['path'] in paths and not binding.get('isPPtrCurve'):
                tracks.append({'path':paths[binding['path']],'attribute':binding['attribute'],'offset':offset,'dimension':1,'typeId':binding['typeID']})
            offset+=dim
        key=match[0]+'_'+str(clip_obj.path_id)
        (out/(key+'.bin')).write_bytes(vals.astype('<f4').tobytes())
        record['clips'].append({'name':t['m_Name'],'duration':dur,'frames':len(times),'columns':vals.shape[1],'file':key+'.bin','tracks':tracks})
    result.append(record)
(out/'animations.json').write_text(json.dumps({'animators':result,'unmatched':unmatched},indent=2))
print('DECODED_ANIMATORS',len(result),'CLIPS',sum(len(r['clips']) for r in result),'UNMATCHED',len(unmatched),flush=True)

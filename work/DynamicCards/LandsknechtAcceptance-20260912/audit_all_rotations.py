"""Compare saved quaternion curves with original decoded samples; never edit assets."""
from pathlib import Path
from collections import Counter
import sys,json,re,zlib,mmap,time,hashlib
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np
import yaml
W=Path(__file__).resolve().parent;D=W.parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards']
tasks=[];missing=[];seen=set()
for source,data,folder in [('Legacy2017','LegacyAnimationData','Old/Legacy2017'),('Thronebreaker','NativeAnimationData','Old/Thronebreaker'),('Latest','LatestAnimationData','Latest')]:
    records=json.loads((D/data/'animations.json').read_text())['animators']
    records={(r['id'],r['path']):r for r in records}
    for card in catalog:
        if '/'+folder+'/' not in card['prefab']:continue
        for (sid,path),record in records.items():
            if sid!=(card.get('sourceId') or card['id']):continue
            prefix='Source_'+format(zlib.crc32(path.encode()),'08x');parent=(P/card['prefab']).parent
            if not (parent/(prefix+'.controller')).exists():prefix='Global_'+prefix
            for clip in {c['file']:c for c in record['clips']}.values():
                filename=prefix+'_'+''.join(ch if ch.isalnum() or ch in '_-' else '_' for ch in clip['name'])+'.anim'
                f=parent/filename
                if str(f) in seen:continue
                seen.add(str(f))
                info=dict(source=source,scene=card['id'],animator=path,clip=clip['name'],file=str(f),data=str(D/data/clip['file']))
                if not f.exists():missing.append(info);continue
                tasks.append((info,clip))
def angular(a,b):
    na=np.linalg.norm(a,axis=-1,keepdims=True);nb=np.linalg.norm(b,axis=-1,keepdims=True)
    a=a/np.maximum(na,1e-30);b=b/np.maximum(nb,1e-30)
    return np.degrees(2*np.arccos(np.clip(np.abs(np.sum(a*b,axis=-1)),0,1)))
def curves(file):
    with open(file,'rb') as stream:
        with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as m:
            a=m.find(b'  m_RotationCurves:');b=m.find(b'  m_CompressedRotationCurves:',a)
            section=m[a:b].decode().replace('\r','')
    result={}
    for block in re.split(r'(?m)^  - curve:',section)[1:]:
        # Unity folds long plain YAML paths at spaces; a single-line regex
        # would collapse many different bones onto the same truncated key.
        path=yaml.safe_load(block[block.index('    path:'):])['path'] or ''
        if path in result:raise ValueError('duplicate decoded curve path '+path)
        times=np.array([float(x) for x in re.findall(r'\btime: ([^\n]+)',block)])
        vectors=np.array([[float(x) for x in re.findall(r'[xyzw]: ([^,}]+)',v)] for v in re.findall(r'(?:value|inSlope|outSlope): \{([^}]+)\}',block)]).reshape(-1,3,4)
        if len(times)!=len(vectors):raise ValueError('key count mismatch '+path)
        result[path]=(times,vectors)
    return result
def evaluate(keys,sample):
    times,vectors=keys
    if len(times)==1:
        v=np.broadcast_to(vectors[0,0],(len(sample),4));return v,v
    ids=np.clip(np.searchsorted(times,sample,side='right')-1,0,len(times)-2)
    a=vectors[ids];b=vectors[ids+1];dt=(times[ids+1]-times[ids])[:,None];t=((sample-times[ids])[:,None]/dt)
    linear=(1-t)*a[:,0]+t*b[:,0]
    actual=(2*t**3-3*t**2+1)*a[:,0]+(t**3-2*t**2+t)*dt*a[:,2]+(-2*t**3+3*t**2)*b[:,0]+(t**3-t**2)*dt*b[:,1]
    return actual,linear
stats=Counter();results=[];errors=[];start=time.time()
def save(complete=False):
    summary=dict(complete=complete,elapsedSeconds=time.time()-start,plannedClips=len(tasks),totals=dict(stats),missingFiles=missing,errors=errors,clips=results)
    (W/'all-rotation-audit.json').write_text(json.dumps(summary,indent=2))
for index,(info,clip) in enumerate(tasks):
    try:
        f=Path(info['file']);before=f.stat();actual=curves(f)
        values=np.fromfile(info['data'],dtype='<f4').reshape(clip['frames'],clip['columns']).astype(float)
        samples=np.linspace(0,clip['duration'],clip['frames']);rows=[];absent=[]
        for track in clip['tracks']:
            if track.get('typeId',0)!=0 or track['attribute']!=2:continue
            stats['sourceRotationTracks']+=1
            path=track['path'];target=path
            if target not in actual:
                matches=[k for k in actual if k.endswith('/'+path) or k.split('/')[-1]==path.split('/')[-1]]
                if len(matches)==1:target=matches[0]
            if target not in actual:
                absent.append(dict(path=path,optional=track.get('optional',False)));stats['unresolvedTracks']+=1;continue
            src=values[:,track['offset']:track['offset']+4];normal=np.linalg.norm(src,axis=1)
            if np.any(normal<.5):
                errors.append(dict(info,problem='invalid-source-quaternion',path=path));continue
            got,linear=evaluate(actual[target],samples);err=angular(src,got);linearerr=angular(src,linear)
            peak=float(np.max(err));lpeak=float(np.max(linearerr));stats['comparedTracks']+=1
            if peak>.05:
                row=dict(path=target,maxErrorDegrees=peak,linearErrorDegrees=lpeak,time=float(samples[np.argmax(err)]),keys=len(actual[target][0]),linearRepairCandidate=lpeak<=.025)
                rows.append(row);stats['tracksOver005Degrees']+=1
                if row['linearRepairCandidate']:stats['linearRepairCandidates']+=1
        after=f.stat()
        if before.st_mtime_ns!=after.st_mtime_ns or before.st_size!=after.st_size:raise ValueError('asset changed during audit')
        results.append(dict(info,bytes=after.st_size,modifiedNs=after.st_mtime_ns,rotationCurves=len(actual),deviations=rows,unresolved=absent))
        stats['scannedClips']+=1
        if rows:stats['clipsWithDeviations']+=1
    except Exception as exc:
        errors.append(dict(info,problem=str(exc)));stats['failedClips']+=1
    if index%20==0 or index==len(tasks)-1:
        print(index+1,'/',len(tasks),info['source'],info['scene'],dict(stats),'seconds',round(time.time()-start),flush=True);save()
save(True)
print('COMPLETE',dict(stats),'errors',len(errors),flush=True)

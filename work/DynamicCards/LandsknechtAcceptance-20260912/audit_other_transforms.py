"""Read-only full source comparison for position, scale and Euler channels."""
from pathlib import Path
from collections import Counter
import sys,json,re,mmap,time
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np,yaml
W=Path(__file__).resolve().parent;D=W.parent
prior=json.loads((W/'all-rotation-audit.json').read_text());assert prior['complete']
metadata={}
for data in ['LegacyAnimationData','NativeAnimationData','LatestAnimationData']:
    for rec in json.loads((D/data/'animations.json').read_text())['animators']:
        for c in rec['clips']:metadata[(str(D/data/c['file']),rec['path'],c['name'])]=c
def parse(section):
    out={}
    for block in re.split(r'(?m)^  - curve:',section)[1:]:
        path=yaml.safe_load(block[block.index('    path:'):])['path'] or ''
        assert path not in out,path
        times=np.array([float(t) for t in re.findall(r'\btime: ([^\n]+)',block)],dtype=np.float32).astype(float)
        vectors=np.array([[float(x) for x in re.findall(r'[xyz]: ([^,}]+)',v)] for v in re.findall(r'(?:value|inSlope|outSlope): \{([^}]+)\}',block)],dtype=np.float32).astype(float).reshape(-1,3,3)
        assert len(times)==len(vectors);out[path]=(times,vectors)
    return out
def evaluate(keys,samples):
    times,vectors=keys
    if len(times)==1:return np.broadcast_to(vectors[0,0],(len(samples),3))
    ids=np.clip(np.searchsorted(times,samples,side='right')-1,0,len(times)-2);a=vectors[ids];b=vectors[ids+1]
    dt=(times[ids+1]-times[ids])[:,None];t=(samples-times[ids])[:,None]/dt
    return (2*t**3-3*t**2+1)*a[:,0]+(t**3-2*t**2+t)*dt*a[:,2]+(-2*t**3+3*t**2)*b[:,0]+(t**3-t**2)*dt*b[:,1]
stats=Counter();rows=[];errors=[];started=time.time()
def save(complete=False):
    (W/'other-transform-audit.json').write_text(json.dumps(dict(complete=complete,elapsedSeconds=time.time()-started,totals=dict(stats),errors=errors,clips=rows),indent=2))
for n,row in enumerate(prior['clips']):
    try:
        f=Path(row['file']);st=f.stat();assert st.st_size==row['bytes'] and st.st_mtime_ns==row['modifiedNs']
        c=metadata[(row['data'],row['animator'],row['clip'])];values=np.fromfile(row['data'],dtype='<f4').reshape(c['frames'],c['columns']).astype(float)
        samples=(np.arange(c['frames'],dtype=np.float32)*np.float32(np.float32(c['duration'])/max(1,c['frames']-1))).astype(float);deviations=[];unresolved=[]
        with f.open('rb') as stream:
            with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as m:
                sections={}
                for attr,begin,end in [(1,'m_PositionCurves','m_ScaleCurves'),(3,'m_ScaleCurves','m_FloatCurves'),(4,'m_EulerCurves','m_PositionCurves')]:
                    a=m.find(('  '+begin+':').encode());b=m.find(('  '+end+':').encode(),a)
                    sections[attr]=parse(m[a:b].decode().replace('\r',''))
        for t in c['tracks']:
            if t.get('typeId',0)!=0 or t['attribute'] not in sections:continue
            attr=t['attribute'];actual=sections[attr];path=t['path'];stats['sourceTracks'+str(attr)]+=1
            if path not in actual:
                matches=[p for p in actual if p.endswith('/'+path) or p.split('/')[-1]==path.split('/')[-1]]
                if len(matches)==1:path=matches[0]
            if path not in actual:
                unresolved.append(dict(path=t['path'],attribute=attr,optional=t.get('optional',False)));stats['unresolvedTracks']+=1;continue
            src=values[:,t['offset']:t['offset']+3];got=evaluate(actual[path],samples);err=abs(got-src)
            if attr==4:err=abs((got-src+180)%360-180)
            tolerance=np.maximum(.0003,np.max(abs(src),axis=0)*1e-6)
            stats['comparedTracks'+str(attr)]+=1
            if np.any(err>tolerance):
                peak=np.unravel_index(np.argmax(err),err.shape)
                deviations.append(dict(path=path,attribute=attr,maxError=float(np.max(err)),tolerance=tolerance.tolist(),time=float(samples[peak[0]])))
                stats['deviatingTracks'+str(attr)]+=1
        rows.append(dict(file=row['file'],source=row['source'],scene=row['scene'],clip=row['clip'],deviations=deviations,unresolved=unresolved));stats['scannedClips']+=1
    except Exception as exc:errors.append(dict(file=row['file'],problem=str(exc)))
    if n%50==0 or n==len(prior['clips'])-1:
        print(n+1,'/',len(prior['clips']),dict(stats),'errors',len(errors),'seconds',round(time.time()-started),flush=True);save()
save(True)

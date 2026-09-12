import sys,json,re,math
from pathlib import Path
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np
W=Path(__file__).resolve().parent
N=W.parent/'NativeAnimationData'
rec=next(x for x in json.loads((N/'animations.json').read_text())['animators'] if x['id']=='15210100')
c=rec['clips'][0]; vals=np.fromfile(N/c['file'],dtype='<f4').reshape(c['frames'],c['columns'])
txt=(W/'before.anim').read_text(); section=txt.split('  m_RotationCurves:')[1].split('  m_CompressedRotationCurves:')[0]
curves={}
for block in re.split(r'(?m)^  - curve:',section)[1:]:
    path=re.search(r'    path: (.*)',block)[1]
    keys=[]
    for k in re.split(r'(?m)^      - serializedVersion:',block)[1:]:
        time=float(re.search(r'\btime: ([^\n]+)',k)[1])
        vs=[np.array([float(x) for x in re.findall(r'[xyzw]: ([^,}]+)',v)]) for v in re.findall(r'(?:value|inSlope|outSlope): \{([^}]+)\}',k)[:3]]
        keys.append((time,*vs))
    curves[path]=keys
def angular(a,b):
    a=a/np.linalg.norm(a,axis=-1,keepdims=True);b=b/np.linalg.norm(b,axis=-1,keepdims=True)
    return np.degrees(2*np.arccos(np.clip(np.abs(np.sum(a*b,axis=-1)),0,1)))
rows=[]
for t in c['tracks']:
    if t['attribute']!=2:continue
    src=vals[:,t['offset']:t['offset']+4].astype(float)
    ks=curves[t['path']]; times=np.array([k[0] for k in ks]); sample=np.linspace(0,c['duration'],c['frames'])
    actual=[];linear=[]
    for tm in sample:
        i=min(len(ks)-2,max(0,int(np.searchsorted(times,tm,side='right'))-1));a,b=ks[i:i+2];dt=b[0]-a[0];f=(tm-a[0])/dt
        actual.append((2*f**3-3*f**2+1)*a[1]+(f**3-2*f**2+f)*dt*a[3]+(-2*f**3+3*f**2)*b[1]+(f**3-f**2)*dt*b[2])
        linear.append(a[1]*(1-f)+b[1]*f)
    actual=np.array(actual);err=angular(src,actual);step=angular(src[:-1],src[1:]);imp=angular(actual[:-1],actual[1:])
    rows.append(dict(path=t['path'],keys=len(ks),maxError=float(max(err)),linearError=float(max(angular(src,np.array(linear)))),errorTime=float(sample[np.argmax(err)]),sourceMaxStep=float(max(step)),importMaxStep=float(max(imp)),sourceSeam=float(angular(src[0],src[-1])),sourceNormRange=[float(min(np.linalg.norm(src,axis=1))),float(max(np.linalg.norm(src,axis=1)))]))
rows.sort(key=lambda x:x['maxError'],reverse=True)
(W/'rotation-comparison.json').write_text(json.dumps(rows,indent=2))
for row in rows[:12]:print({**row,'path':row['path'].split('/')[-1]})

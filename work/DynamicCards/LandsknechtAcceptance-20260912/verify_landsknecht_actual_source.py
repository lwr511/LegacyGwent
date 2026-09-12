"""Compare the recorded foreground skeleton to original exported source samples.

One small, shared clock offset accounts for Animator and LateUpdate startup order.
No per-bone or per-frame alignment is permitted.
"""
from pathlib import Path
import sys, json, argparse
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np

p=argparse.ArgumentParser(); p.add_argument('run',type=Path); args=p.parse_args()
root=args.run.resolve(); report=json.loads((root/'poses.json').read_text(encoding='utf-8-sig'))
assert report['complete'] and report['sourceMesh']=='15210100_183896__3_man'
n=root.parent.parent/'NativeAnimationData'
animator=next(r for r in json.loads((n/'animations.json').read_text())['animators'] if r['id']=='15210100')
assert len(animator['clips'])==1
clip=animator['clips'][0]; assert clip['duration']==8
values=np.fromfile(n/clip['file'],dtype='<f4').reshape(clip['frames'],clip['columns']).astype(float)
source={}
for t in clip['tracks']:
    if t['attribute']!=2: continue
    leaf=t['path'].rsplit('/',1)[-1]
    if leaf in source: raise ValueError('Ambiguous source bone leaf '+leaf)
    source[leaf]=values[:,t['offset']:t['offset']+4]
frames=report['frames']; names=[b['bone'] for b in frames[0]['bones'] if b['bone'] in source]
assert len(names)==len(set(names))
unmatched=sorted({b['bone'] for b in frames[0]['bones']}-set(names))
actual=np.array([[[next(b for b in f['bones'] if b['bone']==name)['rotation'][k] for k in 'xyzw'] for name in names] for f in frames])
actual/=np.linalg.norm(actual,axis=-1,keepdims=True)
src=np.stack([source[name] for name in names],axis=1)
ages=np.array([f['age'] for f in frames])

def error(offset):
    t=((ages+offset)%clip['duration'])*(clip['frames']-1)/clip['duration']
    index=np.minimum(np.floor(t).astype(int),clip['frames']-2)
    f=(t-index)[:,None,None]
    a=src[index]; b=src[index+1]
    b=np.where(np.sum(a*b,axis=-1,keepdims=True)<0,-b,b)
    expected=a*(1-f)+b*f
    expected/=np.linalg.norm(expected,axis=-1,keepdims=True)
    return np.degrees(2*np.arccos(np.clip(np.abs(np.sum(expected*actual,axis=-1)),0,1)))

offsets=np.linspace(-.12,.12,481)
scores=[float(np.mean(error(offset)**2)) for offset in offsets]
offset=float(offsets[int(np.argmin(scores))])
for step in [.0001,.00001,.000001]:
    offsets=offset+np.arange(-6,7)*step
    offset=float(min(offsets,key=lambda v:np.mean(error(v)**2)))
errors=error(offset)
zero_offset_errors=error(0)
rows=[dict(bone=name,maxErrorDegrees=float(errors[:,i].max()),rmsErrorDegrees=float(np.sqrt(np.mean(errors[:,i]**2)))) for i,name in enumerate(names)]
rows.sort(key=lambda r:r['maxErrorDegrees'],reverse=True)
out=dict(complete=True,sourceAnimation='NativeAnimationData/'+clip['file'],sourceDurationSeconds=clip['duration'],
    recordedFrames=len(frames),matchedSourceBoneTracks=len(names),unmatchedBones=unmatched,
    sharedClockOffsetSeconds=offset,clockOffsetSearchBoundSeconds=.12,
    maxErrorDegrees=float(errors.max()),rmsErrorDegrees=float(np.sqrt(np.mean(errors**2))),
    zeroClockOffsetMaxErrorDegrees=float(zero_offset_errors.max()),
    actualSourcePoseWithinPointOneDegree=bool(zero_offset_errors.max()<.1),bones=rows)
(root/'actual-source-pose-comparison.json').write_text(json.dumps(out,indent=2))
print(json.dumps({k:v for k,v in out.items() if k!='bones'}))
print(json.dumps(rows[:10]))

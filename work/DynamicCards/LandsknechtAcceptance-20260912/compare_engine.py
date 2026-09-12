from pathlib import Path
import sys,json
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np
W=Path(__file__).resolve().parent;N=W.parent/'NativeAnimationData'
c=next(r for r in json.loads((N/'animations.json').read_text())['animators'] if r['id']=='15210100')['clips'][0]
values=np.fromfile(N/c['file'],dtype='<f4').reshape(c['frames'],c['columns']).astype(float)
source={t['path']:values[:,t['offset']:t['offset']+4] for t in c['tracks'] if t['attribute']==2}
report={}
for phase in ['before','after']:
    j=json.loads((W/'engine-evaluation'/f'{phase}.json').read_text());rows=[]
    assert j['frames']==c['frames']
    for b in j['bones']:
        got=np.array([[q[k] for k in 'xyzw'] for q in b['rotations']]);src=source[b['path']]
        got/=np.linalg.norm(got,axis=1,keepdims=True);src=src/np.linalg.norm(src,axis=1,keepdims=True)
        err=np.degrees(2*np.arccos(np.clip(np.abs(np.sum(got*src,axis=1)),0,1)))
        rows.append(dict(path=b['path'],errorDegrees=float(max(err)),time=float(np.argmax(err)*j['step'])))
    report[phase]=sorted(rows,key=lambda r:r['errorDegrees'],reverse=True)
    print(phase,'bones',len(rows),'max',max(r['errorDegrees'] for r in rows),'over0.05',sum(r['errorDegrees']>.05 for r in rows))
(W/'engine-evaluation/comparison.json').write_text(json.dumps(report,indent=2))
assert max(r['errorDegrees'] for r in report['after'])<.025
assert max(r['errorDegrees'] for r in report['before'])>10

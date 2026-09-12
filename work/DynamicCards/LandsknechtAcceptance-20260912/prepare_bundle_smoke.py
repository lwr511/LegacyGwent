from pathlib import Path
import json
W=Path(__file__).resolve().parent
audit=json.loads((W/'all-rotation-audit.json').read_text());jobs={j['original']:j for j in json.loads((W/'rotation-repair-jobs.json').read_text())['jobs']}
metadata={}
for data in ['LegacyAnimationData','NativeAnimationData','LatestAnimationData']:
    for r in json.loads((W.parent/data/'animations.json').read_text())['animators']:
        for c in r['clips']:metadata[(str(W.parent/data/c['file']),r['path'],c['name'])]=c
cases=[]
for scene,phase in [('15210100','Loop'),('11221101','Intro'),('11210501','Intro')]:
    row=next(r for r in audit['clips'] if r['scene']==scene and phase in r['clip']);job=jobs[row['file']];c=metadata[(row['data'],row['animator'],row['clip'])]
    cases.append(dict(name=scene+'-'+phase,before=row['file'],after=job['staged'],data=row['data'],frames=c['frames'],columns=c['columns'],duration=c['duration'],tracks=[t for t in c['tracks'] if t.get('typeId',0)==0]))
(W/'bundle-smoke-cases.json').write_text(json.dumps(dict(cases=cases),indent=2))
print('Prepared',len(cases),'source-versus-fixed Animator bundle cases')

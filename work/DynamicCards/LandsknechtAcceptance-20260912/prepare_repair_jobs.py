from pathlib import Path
import json,sys,mmap,re,hashlib
W=Path(__file__).resolve().parent;D=W.parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
audit=json.loads((W/'all-rotation-audit.json').read_text());assert audit['complete'] and not audit['errors']
clips={}
for data in ['LegacyAnimationData','NativeAnimationData','LatestAnimationData']:
    for record in json.loads((D/data/'animations.json').read_text())['animators']:
        for c in record['clips']:clips[(str(D/data/c['file']),record['path'],c['name'])]=c
def digest(f):
    h=hashlib.sha256()
    with f.open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest()
jobs=[];deferred=[]
for row in audit['clips']:
    if not row['deviations']:continue
    f=Path(row['file']);stat=f.stat()
    assert stat.st_size==row['bytes'] and stat.st_mtime_ns==row['modifiedNs'],f
    with f.open('rb') as stream:
        with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as m:
            at=m.find(b'  m_PPtrCurves:');pointerline=m[at:m.find(b'\n',at)].strip()
    if pointerline!=b'm_PPtrCurves: []':
        deferred.append(dict(file=str(f),reason='Object reference animation needs its dependencies in the repair project'));continue
    if any(not d['linearRepairCandidate'] for d in row['deviations']):
        deferred.append(dict(file=str(f),reason='Nonlinear reconstruction needed'));continue
    c=clips[(row['data'],row['animator'],row['clip'])];targets=[]
    for d in row['deviations']:
        tracks=[t for t in c['tracks'] if t.get('typeId',0)==0 and t['attribute']==2]
        exact=[t for t in tracks if t['path']==d['path']]
        if not exact:exact=[t for t in tracks if d['path'].endswith('/'+t['path'])]
        if not exact:exact=[t for t in tracks if d['path'].split('/')[-1]==t['path'].split('/')[-1]]
        assert len(exact)==1,(str(f),d['path'],len(exact))
        targets.append(dict(path=d['path'],offset=exact[0]['offset'],sample=round(d['time']/c['duration']*(c['frames']-1)),beforeExpected=d['maxErrorDegrees']))
    jobs.append(dict(original=str(f),staged=str(W/'RepairStaging'/f.relative_to(P)),data=row['data'],sha256=digest(f),frames=c['frames'],columns=c['columns'],duration=c['duration'],targets=targets))
# The named weapon case and two independent high-error cases run first.
jobs.sort(key=lambda j:(0 if '/15210100/' in j['original'].replace('\\','/') else 1 if '/11221101/' in j['original'].replace('\\','/') else 2 if '/11210501/' in j['original'].replace('\\','/') else 3,j['original']))
(W/'rotation-repair-jobs.json').write_text(json.dumps(dict(jobs=jobs),indent=2))
(W/'rotation-repair-deferred.json').write_text(json.dumps(deferred,indent=2))
print('Prepared',len(jobs),'clip copies;',sum(len(j['targets']) for j in jobs),'bone curves;',len(deferred),'deferred')

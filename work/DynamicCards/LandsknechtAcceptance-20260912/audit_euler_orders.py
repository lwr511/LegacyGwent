from pathlib import Path
import json,re,sys,mmap
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import yaml
W=Path(__file__).resolve().parent;D=W.parent
rotation=json.loads((W/'all-rotation-audit.json').read_text())
other=json.loads((W/'other-transform-audit.json').read_text())
metadata={}
for data in ['LegacyAnimationData','NativeAnimationData','LatestAnimationData']:
    for r in json.loads((D/data/'animations.json').read_text())['animators']:
        for c in r['clips']:metadata[(str(D/data/c['file']),r['path'],c['name'])]=c
rows=[];errors=[]
for r in rotation['clips']:
    c=metadata[(r['data'],r['animator'],r['clip'])]
    tracks=[t for t in c['tracks'] if t.get('typeId',0)==0 and t['attribute']==4]
    if not tracks:continue
    with open(r['file'],'rb') as f, mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ) as m:
        a=m.find(b'  m_EulerCurves:');b=m.find(b'  m_PositionCurves:',a)
        section=m[a:b].decode().replace('\r','')
    actual={}
    for block in re.split(r'(?m)^  - curve:',section)[1:]:
        path=yaml.safe_load(block[block.index('    path:'):])['path'] or ''
        actual[path]=int(re.search(r'\bm_RotationOrder: (\d+)',block)[1])
    for t in tracks:
        path=t['path']
        if path not in actual:
            candidates=[p for p in actual if p.endswith('/'+path) or p.split('/')[-1]==path.split('/')[-1]]
            if len(candidates)==1:path=candidates[0]
        item=dict(file=r['file'],path=t['path'],hasSourceOrder=t.get('hasRotationOrder',False),sourceOrder=t.get('rotationOrder'),importedOrder=actual.get(path))
        item['matches']=item['hasSourceOrder'] and item['sourceOrder']==item['importedOrder']
        rows.append(item)
        if not item['matches']:errors.append(item)
unresolved=[dict(file=r['file'],**t) for report in [rotation,other] for r in report['clips'] for t in r['unresolved']]
result=dict(complete=True,compared=len(rows),errors=errors,allUnresolvedTracksOptional=all(t['optional'] for t in unresolved),unresolved=unresolved,tracks=rows)
(W/'euler-order-and-optional-track-audit.json').write_text(json.dumps(result,indent=2))
print('Euler orders compared:',len(rows),'mismatches:',len(errors),'unresolved:',len(unresolved),'all optional:',result['allUnresolvedTracksOptional'])

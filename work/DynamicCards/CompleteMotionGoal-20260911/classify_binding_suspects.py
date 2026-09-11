from pathlib import Path
from collections import defaultdict,Counter
import json,sys,array
sys.stdout.reconfigure(encoding='utf-8')
W=Path(__file__).resolve().parent
d=json.loads((W/'current-binding-audit.json').read_text())
results=[]
for source,folder in [('Legacy2017','LegacyAnimationData'),('Thronebreaker','NativeAnimationData'),('Latest','LatestAnimationData')]:
    doc=json.loads((W.parent/folder/'animations.json').read_text(encoding='utf-8-sig'))
    records={(r['id'],r['path']):r for r in doc['animators']}
    grouped=defaultdict(list)
    for row in d['issues']:
        if row['source']==source:grouped[(row['scene'],row['animator'],row['clip'])].append(row)
    for (scene,anchor,clipname),suspects in grouped.items():
        rec=records[(scene,anchor)];cl=next(c for c in rec['clips'] if c['name']==clipname)
        values=array.array('f');values.frombytes((W.parent/folder/cl['file']).read_bytes())
        for row in suspects:
            tr=next(t for t in cl['tracks'] if t['path']==row['path'] and t['attribute']==row['attribute'] and t.get('typeId',0)==row['type'])
            spans=[]
            for component in range(tr['dimension']):
                samples=values[tr['offset']+component::cl['columns']]
                spans.append([min(samples),max(samples)])
            results.append(dict(row,sourceValueRanges=spans,sourceChanges=any(b-a>1e-6 for a,b in spans)))
(W/'binding-suspects-source-values.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('varying',Counter(x['type'] for x in results if x['sourceChanges']),'constant',Counter(x['type'] for x in results if not x['sourceChanges']))
print('changing scenes',Counter((x['source'],x['scene']) for x in results if x['sourceChanges']))

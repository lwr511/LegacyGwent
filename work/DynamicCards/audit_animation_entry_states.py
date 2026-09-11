from pathlib import Path
import json,collections
r=Path(__file__).resolve().parent;rows=[]
for kind,folder in [('Native','NativeAnimationData'),('Legacy','LegacyAnimationData'),('Latest','LatestAnimationData')]:
 for actor in json.loads((r/folder/'animations.json').read_text())['animators']:
  for index,layer in enumerate(actor.get('layers',[])):
   states=layer.get('states',[])
   if not states:continue
   start=states[max(0,min(layer.get('defaultState',0),len(states)-1))]
   intro=[s['name'] for s in states if any(word in (s['name']+' '+' '.join(s.get('clips',[]))).lower() for word in ['intro','entry','appear'])]
   reasons=[]
   if not start.get('clips') and any(s.get('clips') for s in states):reasons.append('defaultWithoutMotion')
   if intro and ('loop' in start['name'].lower() or 'idle' in start['name'].lower()):reasons.append('loopDefaultWithIntro')
   if start.get('speed',1)==0:reasons.append('zeroDefaultSpeed')
   if index and layer.get('weight',1)==0:reasons.append('zeroLayerWeight')
   if reasons:rows.append(dict(source=kind,id=actor['id'],path=actor['path'],layer=layer['name'],default=start,reasons=reasons,intro=intro))
(r/'animation_entry_state_audit.json').write_text(json.dumps(rows,indent=2))
print('ANIMATION_ENTRY_STATES',dict(collections.Counter(reason for row in rows for reason in row['reasons'])),flush=True)
print('EXAMPLES',[(x['source'],x['id'],x['layer'],x['default']['name'],x['reasons']) for x in rows[:12]])

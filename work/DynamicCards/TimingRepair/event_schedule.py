from pathlib import Path
import json,collections
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');contracts=json.loads((w/'TimingRepair/source-contracts.json').read_text());d=json.loads((w/'TimingRepair/event-audit.json').read_text());results=[];bad=[]
for row in d['mismatches']:
 ident=row['id'];source='Native' if row['event']['path'].split('/')[0].endswith('00') else 'Legacy';names={e['clip'] for e in row['sourceEvents']};options=[]
 for r in contracts:
  if r['id']!=ident or r['source']!=source:continue
  clips={c['name']:c for c in r['clips']}
  for l in r['layers']:
   start=0;idx=l['defaultState'];visited=set()
   while idx not in visited and 0<=idx<len(l['states']):
    visited.add(idx);s=l['states'][idx];c=clips.get(s['clips'][0]) if len(s['clips'])==1 else None
    if not c:break
    duration=c['duration']/s['speed'];
    if c['name'] in names:options.append(dict(loop=s['loop'],phaseStart=start,period=duration if s['loop'] else 0,speed=s['speed']))
    tr=next((t for t in s['transitions'] if t['unconditional'] and t['hasExitTime']),None)
    if not tr:break
    dest=tr['destination'];dc=clips.get(l['states'][dest]['clips'][0]) if len(l['states'][dest]['clips'])==1 else None
    start+=duration*tr['exitTime'];start-=tr['offset']*dc['duration']/l['states'][dest]['speed'] if dc else 0;idx=dest
 unique=list({tuple(sorted(o.items())):o for o in options}.values())
 if len(unique)!=1:bad.append(dict(id=ident,names=list(names),options=unique))
 else:results.append(dict(id=ident,event=row['event'],timing=unique[0]))
(w/'TimingRepair/event-repairs.json').write_text(json.dumps(results,indent=2));print('EVENT REPAIRS',len(results),'BAD',bad)

from pathlib import Path
import json,math,sys
# Match source clip identity and controller timing, never infer looping from a name.
w=Path(__file__).resolve().parent
kind=sys.argv[1] if len(sys.argv)>1 else 'Native'
source={'Native':'source_effects.json','Legacy':'second_source_effects.json','Latest':'latest_source_effects.json'}[kind]
records=[dict(r,source=kind) for r in json.loads((w/(kind+'AnimationData')/'animations.json').read_text())['animators']]
path=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/catalog.json' if '--client' in sys.argv else w/'Probe/Assets/DynamicCards/Content/catalog.json'
catalog=json.loads(path.read_text());sources={kind:json.loads((w/source).read_text())};changes=[];unknown=[];checked=0
for card in catalog['cards']:
 sourceKind='Latest' if '/Latest/' in card['prefab'] else 'Legacy' if '/Legacy2017/' in card['prefab'] else 'Native'
 if sourceKind!=kind:continue
 schedules={}
 for r in records:
  if r['source']!=kind or r['id']!=card['id']:continue
  clips={c['name']:c for c in r['clips']}
  for l in r.get('layers',[]):
   start=0;arrival=0;idx=l['defaultState'];visited=set()
   while idx not in visited and 0<=idx<len(l['states']):
    visited.add(idx);s=l['states'][idx];c=clips.get(s['clips'][0]) if len(s['clips'])==1 else None
    if not c or s['speed']<=0:break
    duration=c['duration']/s['speed'];tr=next((t for t in s['transitions'] if t['unconditional'] and t['hasExitTime']),None);period=duration if s['loop'] else 0
    if tr and tr['destination']==idx:period=duration*(tr['exitTime']-tr['offset'])
    schedules.setdefault(c['name'],[]).append(dict(loop=s['loop'],phaseStart=start,period=period,speed=s['speed'],entry=arrival,end=start+duration*tr['exitTime']+(tr['duration'] if tr['fixedDuration'] else tr['duration']*duration) if tr and tr['destination']!=idx else -1))
    if not tr:break
    dest=tr['destination'];dc=clips.get(l['states'][dest]['clips'][0]) if len(l['states'][dest]['clips'])==1 else None
    arrival=start+duration*tr['exitTime'];start=arrival;start-=tr['offset']*dc['duration']/l['states'][dest]['speed'] if dc and l['states'][dest]['speed']>0 else 0;idx=dest
 for index,item in enumerate(card.get('particleEvents',[])):
  checked+=1;original=item.get('sourceTime',item['time']);ev=[e for e in sources[kind].get(card['id'],{}).get('events',[]) if abs(e['time']-original)<.0001 and (not e.get('data') or e['data']==item['path'].rsplit('/',1)[-1])];options=[s for e in ev for s in schedules.get(e['clip'],[])];normalized=[]
  for option in options:
   t=option.copy();first=t['phaseStart']+original/t['speed']
   if first<t['entry']-.0001:
    if not t['loop'] or t['period']<=0:continue
    shift=math.ceil((t['entry']-first)/t['period'])*t['period'];t['phaseStart']+=shift;first+=shift
   if t['end']>=0:
    if first>t['end']+.0001:continue
    if t['loop']:
     if first+t['period']<=t['end']+.0001:raise RuntimeError('Finite repeating event requires an explicit end bound')
     t['loop']=False;t['period']=0
   t.pop('entry');t.pop('end');normalized.append(t)
  unique=list({tuple(sorted(o.items())):o for o in normalized}.values())
  if len(unique)!=1:unknown.append(dict(id=card['id'],index=index,options=unique));continue
  t=unique[0]
  after=dict(loop=t['loop'],phaseStart=t['phaseStart'],period=t['period'],sourceTiming=True,sourceTime=original,time=original/t['speed'])
  if any((item.get(k)!=v if isinstance(v,bool) else abs(item.get(k,0)-v)>.0001) for k,v in after.items()):changes.append(dict(id=card['id'],index=index,before=item.copy(),after=after))
report=dict(checked=checked,changes=changes,unknown=unknown)
(w/('phase_contract_audit_'+kind+'.json')).write_text(json.dumps(report,indent=2))
if unknown:raise RuntimeError('Source event schedules require explicit resolution: '+str(unknown[:3]))
cards={c['id']:c for c in catalog['cards']}
for row in changes:cards[row['id']]['particleEvents'][row['index']].update(row['after'])
path.write_text(json.dumps(catalog,indent=2),encoding='utf8')
print('PHASE_CONTRACTS',kind,'events',checked,'changed',len(changes))

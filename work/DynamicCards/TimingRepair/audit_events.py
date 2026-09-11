from pathlib import Path
import json
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');p=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';contracts=json.loads((w/'TimingRepair/source-contracts.json').read_text());cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text())['cards'];sources={s:json.loads((w/f).read_text()) for s,f in [('Native','source_effects.json'),('Legacy','second_source_effects.json')]};rows=[];unresolved=[];count=0
for card in cat:
 kind='Native' if card['sourceVersion']=='Thronebreaker' else 'Legacy';info=sources[kind].get(card['id'],{});records=[r for r in contracts if r['source']==kind and r['id']==card['id']];flags={}
 for r in records:
  for l in r['layers']:
   for s in l['states']:
    for name in s['clips']:flags.setdefault(name,set()).add(s['loop'])
 for e in card.get('particleEvents',[]):
  count+=1;ev=[x for x in info.get('events',[]) if x['functionName']=='PlayEffect' and x['data']==e['path'].split('/')[-1] and abs(x['time']-e.get('sourceTime',e['time']))<.0001];expected={f for x in ev for f in flags.get(x['clip'],[])}
  if len(expected)==1 and e['loop']!=next(iter(expected)):rows.append(dict(id=card['id'],event=e,sourceEvents=ev,expected=next(iter(expected))))
  elif len(expected)!=1:unresolved.append(dict(id=card['id'],event=e,sourceEvents=ev,flags=list(expected)))
result=dict(events=count,mismatches=rows,unresolved=unresolved);(w/'TimingRepair/event-audit.json').write_text(json.dumps(result,indent=2));print('EVENTS',count,'mismatches',len(rows),'unresolved',len(unresolved));print(rows[:3])
f=w/'build_animation_contracts.py';b=f.read_text();(w/'TimingRepair/build_animation_contracts-before.py').write_text(b);b=b.replace("records={(r['id'],r['path']):r for r in doc['animators']}","records={(r['id'],r['path']):r for r in doc['animators']}\nfor record in records.values():\n counts={c['name']:sum(x['name']==c['name'] for x in record['clips']) for c in record['clips']}\n for clip in record['clips']:\n  if counts[clip['name']]>1:clip['name']+='__'+Path(clip['file']).stem.split('_')[-1]")
b=b.replace("candidate=next((v for v in r['clips'] if v['name']==name),None)","candidate=next((v for v in r['clips'] if sourceClip and v['file']==r['id']+'_'+str(sourceClip.path_id)+'.bin'),None)");f.write_text(b)
f=w/'SourceAnimationImporter.cs';b=f.read_text();b=b.replace('var anchor=FindExpanded(root.transform,record.path);','if(record.clips.GroupBy(c=>c.name).Any(g=>g.Count()>1))throw new InvalidOperationException(record.id+": duplicate source clip names; rebuild contracts using clip file IDs first.");\n                    var anchor=FindExpanded(root.transform,record.path);');f.write_text(b)

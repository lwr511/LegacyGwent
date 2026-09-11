from pathlib import Path
import json,collections
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');out=w/'TimingRepair';all=[];conditional=[]
for kind in ['Native','Legacy']:
 for r in json.loads((w/(kind+'AnimationData')/'animations.json').read_text())['animators']:
  all.append(dict(source=kind,id=r['id'],path=r['path'],layers=r.get('layers',[]),clips=[{k:c[k] for k in ['name','file','duration']} for c in r['clips']]))
  for l in r.get('layers',[]):
   for s in l['states']:
    if any(not t['unconditional'] for t in s['transitions']):conditional.append((kind,r['id'],s['name']))
(out/'source-contracts.json').write_text(json.dumps(all));print('CONTRACTS',len(all),'conditional',conditional[:20],len(conditional))
f=w/'SourceAnimationImporter.cs';b=f.read_text();(out/'SourceAnimationImporter-before.cs').write_text(b)
b=b.replace('if(!restored && intro!=null && loop!=null && loop.motion!=null && intro!=loop){var transition=intro.AddTransition(loop);transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;transition.duration=0;}','// Source layers are authoritative, including the absence of transitions.')
b=b.replace('foreach(var state in states){var clip=state.motion as AnimationClip;if(clip==null)continue;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=state==loop || (intro==null && states.Count==1);AnimationUtility.SetAnimationClipSettings(clip,settings);}','for(int stateIndex=0;stateIndex<states.Count;stateIndex++){var clip=states[stateIndex].motion as AnimationClip;if(clip==null)continue;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=data.states[stateIndex].loop;AnimationUtility.SetAnimationClipSettings(clip,settings);}')
f.write_text(b)

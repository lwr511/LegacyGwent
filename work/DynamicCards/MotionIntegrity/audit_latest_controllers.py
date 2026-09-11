from pathlib import Path
import json,re,zlib
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');ids={x['id'] for x in json.loads((w/'restore-entries.json').read_text())};records=json.loads((w.parent/'LatestAnimationData/animations.json').read_text())['animators'];issues=[];checked=0
for r in {(r['id'],r['path']):r for r in records}.values():
 if r['id'] not in ids or not r['clips']:continue
 prefix='Source_'+format(zlib.crc32(r['path'].encode()),'08x');folder=p/'Assets/DynamicCards/Content/Latest'/r['id'];f=folder/(prefix+'.controller')
 if not f.exists():f=folder/('Global_'+prefix+'.controller')
 if not f.exists():issues.append([r['id'],r['path'],'missing controller']);continue
 blocks={i:(ty,b) for ty,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',f.read_text(),re.S)};controller=next(b for ty,b in blocks.values() if ty=='91');layers=[b for b in re.split(r'\n  - serializedVersion:',controller.split('m_AnimatorLayers:',1)[1]) if 'm_StateMachine:' in b];checked+=1
 def text(b,key):
  m=re.search(r'\b'+key+r': ([^\n]*)',b);return m[1].strip().strip("'").replace("''", "'") if m else None
 def ref(b,key):return re.search(r'\b'+key+r': \{fileID: (-?\d+)',b)[1]
 if len(layers)!=len(r['layers']):issues.append([r['id'],r['path'],'layer count']);continue
 for li,(lb,source) in enumerate(zip(layers,r['layers'])):
  sm=blocks[ref(lb,'m_StateMachine')][1];stateids=re.findall(r'm_State: \{fileID: (-?\d+)',sm.split('m_ChildStates:',1)[1].split('m_ChildStateMachines:',1)[0]);states=[blocks[i][1] for i in stateids];names=[text(b,'m_Name') for b in states];expected=source['states'];prefixdesc=[r['id'],r['path'],source['name']]
  if names!=[s['name'] for s in expected]:issues.append(prefixdesc+['state names',names,[s['name'] for s in expected]]);continue
  if expected and text(blocks[ref(sm,'m_DefaultState')][1],'m_Name')!=expected[source['defaultState']]['name']:issues.append(prefixdesc+['default state'])
  if abs(float(text(lb,'m_DefaultWeight'))-(1 if li==0 else source['weight']))>1e-5:issues.append(prefixdesc+['weight'])
  for si,(state,expect) in enumerate(zip(states,expected)):
   for clipname in expect['clips']:
    if not clipname:continue
    safe=''.join(c if c.isalnum() or c in '_-' else '_' for c in clipname)
    clipfile=folder/(f.stem+'_'+safe+'.anim.meta')
    motion=re.search(r'm_Motion: \{fileID: \d+, guid: (\w+)',state)
    if not clipfile.exists() or motion is None or motion[1]!=re.search(r'^guid: (\w+)',clipfile.read_text(),re.M)[1]:issues.append(prefixdesc+[expect['name'],'clip reference',clipname])
   if abs(float(text(state,'m_Speed'))-expect['speed'])>1e-5:issues.append(prefixdesc+[expect['name'],'speed'])
   transids=re.findall(r'fileID: (-?\d+)',state.split('m_Transitions:',1)[1].split('m_StateMachineBehaviours:',1)[0]);actual=[]
   for ti in transids:
    t=blocks[ti][1];actual.append((text(blocks[ref(t,'m_DstState')][1],'m_Name'),float(text(t,'m_ExitTime')),float(text(t,'m_TransitionDuration')),float(text(t,'m_TransitionOffset')),int(text(t,'m_HasExitTime')),int(text(t,'m_HasFixedDuration'))))
   wanted=[(expected[t['destination']]['name'],t['exitTime'],t['duration'],t['offset'],int(t['hasExitTime']),int(t['fixedDuration'])) for t in expect['transitions'] if t['unconditional'] and 0<=t['destination']<len(expected)]
   if len(actual)!=len(wanted) or any(a[0]!=b[0] or any(abs(x-y)>1e-5 for x,y in zip(a[1:],b[1:])) for a,b in zip(actual,wanted)):issues.append(prefixdesc+[expect['name'],'transitions',actual,wanted])
(w/'latest-controller-audit.json').write_text(json.dumps({'checked':checked,'issues':issues},indent=2));print('controllers',checked,'issues',len(issues));print(issues[:10])

from pathlib import Path
import json,re,zlib
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');p=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text())['cards'];active={(('Native' if c['sourceVersion']=='Thronebreaker' else 'Legacy'),c['id']):p/c['prefab'] for c in cat};changes=[];other=[];checked=0
for r in json.loads((w/'TimingRepair/source-contracts.json').read_text()):
 root=active.get((r['source'],r['id']));
 if root is None:continue
 prefix='Source_'+format(zlib.crc32(r['path'].encode())&0xffffffff,'08x');fs=list(root.parent.glob('*'+prefix+'.controller'))
 if len(fs)!=1:continue
 f=fs[0];body=f.read_text();blocks={m[2]:m[0] for m in re.findall(r'(?ms)(^--- !u!(\d+) &(-?\d+)\r?\n.*?)(?=^---|\Z)',body)}
 ctrl=next(b for b in blocks.values() if b.startswith('--- !u!91 '));machines=re.findall(r'm_StateMachine: \{fileID: (-?\d+)\}',ctrl)
 for li,layer in enumerate(r['layers']):
  if li>=len(machines):other.append([r['id'],'missing layer']);continue
  machine=blocks[machines[li]];ids=re.findall(r'm_State: \{fileID: (-?\d+)\}',machine);checked+=len(ids)
  if len(ids)!=len(layer['states']):other.append([r['id'],'statecount']);continue
  for si,s in enumerate(layer['states']):
   block=blocks[ids[si]];trans=block.split('m_Transitions:')[1].split('m_StateMachineBehaviours:')[0];count=len(re.findall(r'fileID: -?\d+',trans))
   if not s['transitions'] and count:changes.append(dict(id=r['id'],path=r['path'],state=s['name'],file=str(f),block=ids[si],before=count))
   elif len(s['transitions'])!=count:other.append([r['id'],s['name'],'transitioncount',len(s['transitions']),count])
result=dict(checkedStates=checked,extraTransitions=changes,other=other);(w/'TimingRepair/transition-audit.json').write_text(json.dumps(result,indent=2));print(result)

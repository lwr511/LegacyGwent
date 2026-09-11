from pathlib import Path
import json,re,sys
root=Path(__file__).resolve().parent
kind=sys.argv[1]
clients=[root/'Probe']
if '--client' in sys.argv:clients=[root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card']
states=json.loads((root/('latest_material_states.json' if kind=='Latest' else kind.lower()+'_original_material_states.json')).read_text())
card_states_path=root/(kind.lower()+'_card_material_states.json')
card_states=json.loads(card_states_path.read_text()) if card_states_path.exists() else {}
exact_path=root/(kind.lower()+'_exact_material_states.json')
exact_states=json.loads(exact_path.read_text()) if exact_path.exists() else {}
shaderfolder=root/({'Native':'ShaderPrograms','Legacy':'LegacyShaderPrograms','Latest':'LatestShaderPrograms'}[kind]);shader_queues={}
def default_queue(shader):
 if shader not in shader_queues:
  p=shaderfolder/re.sub('[^A-Za-z0-9_-]','_',shader)/'metadata.json';queue='Geometry'
  if p.exists():
   info=json.loads(p.read_text());tags=dict(info['m_SubShaders'][0]['m_Tags']['tags']);queue=next((v for k,v in tags.items() if k.lower()=='queue'),'Geometry')
  m=re.fullmatch(r'(\w+)([+-]\d+)?',queue)
  shader_queues[shader]={'Background':1000,'Geometry':2000,'AlphaTest':2450,'Transparent':3000,'Overlay':4000}.get(m[1],2000)+int(m[2] or 0)
 return shader_queues[shader]
changed=0;missing=[]
for client in clients:
 base=client/'Assets/DynamicCards/Content'
 if kind=='Latest':base=base/'Latest'
 if kind=='Legacy':base=base/'Legacy2017'
 for metadata in base.glob('*/conversion.json'):
  if '--cards' in sys.argv and metadata.parent.name not in sys.argv[sys.argv.index('--cards')+1].split(','):continue
  original_metadata=metadata.read_text();data=json.loads(original_metadata)
  for material in data['materials']:
   if kind=='Latest':state=states.get(material['asset'].replace('Assets/DynamicCards/Content/Latest/','Assets/PortableCards/'))
   else:
    values=card_states.get(data['id'],{}).get(material['originalName'],states.get(material['originalName'],[]));state=values[0] if len(values)==1 else None
   if state is None:state=exact_states.get(material['asset'])
   if state is None:missing.append(material);continue
   queue=state['queue'] if state['queue']>=0 else default_queue(material['shader'])
   material.update(hasState=True,queue=queue,renderType=state['renderType']);changed+=1
   # The native client can consume this fix without rerunning an export or touching the source game.
   path=client/material['asset'];body=path.read_text();updated=re.sub(r'm_CustomRenderQueue: -?\d+','m_CustomRenderQueue: '+str(queue),body)
   if updated!=body:path.write_text(updated)
  updated_metadata=json.dumps(data,indent=2)
  if updated_metadata!=original_metadata:metadata.write_text(updated_metadata)
(root/(kind.lower()+'_material_state_unresolved.json')).write_text(json.dumps(missing,indent=2))
print('ORIGINAL_MATERIAL_STATES_APPLIED',kind,changed,'unresolved',len(missing))

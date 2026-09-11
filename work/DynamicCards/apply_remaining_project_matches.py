from pathlib import Path
import json,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parent
path=root/'Probe/Assets/DynamicCards/Content/catalog.json'
data=json.loads(path.read_text());claimed={a for c in data['cards'] for a in c['artIds']}
def kind(card):return 'Latest' if '/Latest/' in card['prefab'] else 'Legacy' if '/Legacy2017/' in card['prefab'] else 'Native'
cards={(kind(c),c['id']):c for c in data['cards']}
evidence=json.loads((root/'remaining_project_matches.json').read_text());matches=list(evidence['matches'])
old=ET.parse(root/'legacy_defs_Templates.xml').getroot();latest=ET.parse(root/'latest_defs_Templates.xml').getroot()
for art in evidence['best']:
 if not art.isdigit():continue
 for template in old:
  definition=template.find('ArtDefinition')
  if definition is not None and definition.get('ArtId','')+'0'==art and ('Legacy',art[:-1]+'1') in cards:
   matches.append(dict(art=art,source='Legacy',scene=art[:-1]+'1',template=template.get('Id'),name=template.get('DebugName'),proof='source ArtDefinition.ArtId'))
 for template in latest:
  source_art=template.get('ArtId','');scene=source_art+'0101'
  if ('Latest',scene) not in cards:continue
  if (len(art)==6 and template.get('Id')==art) or (len(art)==8 and source_art+'0000'==art):
   matches.append(dict(art=art,source='Latest',scene=scene,template=template.get('Id'),name=template.get('DebugName'),proof='source template Id/ArtId'))
added=[]
for match in matches:
 if match['art'] in claimed or (match['source'],match['scene']) not in cards:continue
 card=cards[match['source'],match['scene']];card['artIds'].append(match['art']);claimed.add(match['art']);added.append(match)
path.write_text(json.dumps(data,indent=2))
(root/'additional_project_binding_evidence.json').write_text(json.dumps(added,indent=2))
print('ADDITIONAL_PROJECT_BINDINGS',len(added))

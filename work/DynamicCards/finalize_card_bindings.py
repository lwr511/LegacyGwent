from pathlib import Path
import json
root=Path(__file__).resolve().parent;path=root/'Probe/Assets/DynamicCards/Content/catalog.json'
data=json.loads(path.read_text())
def kind(card):return 'Latest' if '/Latest/' in card['prefab'] else 'Legacy' if '/Legacy2017/' in card['prefab'] else 'Native'
data['cards'].sort(key=lambda c:({'Native':0,'Legacy':1,'Latest':2}[kind(c)],c['id']))
for card in data['cards']:card['artIds']=[a for a in card['artIds'] if not a.startswith('__probe_')]
preferred={'11210300':('Native','10090100'),'11210200':('Native','10080100'),'11210700':('Latest','10130101'),'20023500':('Latest','14740101'),'20027500':('Latest','14840101'),'11310100':('Latest','10400101')}
for art,key in preferred.items():
 target=next(c for c in data['cards'] if (kind(c),c['id'])==key)
 for card in data['cards']:card['artIds']=[a for a in card['artIds'] if a!=art]
 target['artIds'].append(art)
path.write_text(json.dumps(data,indent=2))
(root/'preferred_card_sources.json').write_text(json.dumps(preferred,indent=2))
print('FINAL_CARD_BINDINGS',len(data['cards']),'explicit visual selections',len(preferred))

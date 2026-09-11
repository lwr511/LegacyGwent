from pathlib import Path
import json,re,csv,sys,collections
root=Path(__file__).resolve().parent;project=root.parents[1]
client=project/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card' if '--client' in sys.argv else root/'Probe'
catalog=json.loads((client/'Assets/DynamicCards/Content/catalog.json').read_text())['cards']
def kind(card):return 'Latest' if '/Latest/' in card['prefab'] else 'Legacy' if '/Legacy2017/' in card['prefab'] else 'Native'
available={}
for card in catalog:
 for art in card['artIds']:available.setdefault(art,card)
text=(project/'src/Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf8')
arts=sorted(set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"',text)))
rows=[]
for art in arts:
 card=available.get(art)
 rows.append(dict(art=art,available=bool(card),source=kind(card) if card else '',scene=card['id'] if card else '',prefab=card['prefab'] if card else ''))
label='client' if '--client' in sys.argv else 'probe'
with (root/(label+'_project_card_coverage.csv')).open('w',encoding='utf-8-sig',newline='') as stream:
 writer=csv.DictWriter(stream,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
sources={};variants=[]
for name,file in [('Native','source_effects.json'),('Legacy','second_source_effects.json'),('Latest','latest_source_effects.json')]:
 ids=set(json.loads((root/file).read_text()));imported={c['id']:c for c in catalog if kind(c)==name}
 sources[name]=dict(sourceScenes=len(ids),catalogScenes=len(imported),missingScenes=sorted(ids-imported.keys()),missingPrefabs=[])
 for scene in sorted(ids):
  card=imported.get(scene);exists=bool(card and (client/card['prefab']).is_file())
  if card and not exists:sources[name]['missingPrefabs'].append(scene)
  variants.append(dict(source=name,scene=scene,imported=exists,projectBindings=';'.join(a for a in card.get('artIds',[]) if a in arts) if card else '',allBindings=';'.join(card['artIds']) if card else ''))
with (root/(label+'_source_scene_inventory.csv')).open('w',encoding='utf-8-sig',newline='') as stream:
 writer=csv.DictWriter(stream,fieldnames=list(variants[0]));writer.writeheader();writer.writerows(variants)
selected={(row['source'],row['scene']) for row in rows if row['available']}
summary=dict(project=str(client),sources=sources,projectArtCount=len(arts),covered=sum(row['available'] for row in rows),missingArt=[row['art'] for row in rows if not row['available']],sourceVariants=len(catalog),selectedSourceVariants=len(selected),unselectedSourceVariants=len(catalog)-len(selected),unboundSourceVariants=sum(not row['projectBindings'] for row in variants),countNote='Source variants include editions of the same card; unselected variants are not a unique-card count.')
(root/(label+'_card_coverage.json')).write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))

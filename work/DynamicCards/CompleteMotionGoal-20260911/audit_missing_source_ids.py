from pathlib import Path
import json
W=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards');D=W/'CompleteMotionGoal-20260911';cards=json.loads((D/'reported-main-ui/game-card-map.json').read_text(encoding='utf-8-sig'))['cards'];arts=sorted({c['art'] for c in cards if not c['mapped']});legacy=json.loads((W/'second_source_inventory.json').read_text())['scenes'];latest={c['id'] for c in json.loads((W/'third_source_inventory.json').read_text())['cards']};native={str(c['id']) for c in json.loads((W/'source_catalog.json').read_text())};rows=[]
for art in arts:
 prefix=art[:-2] if len(art)==8 and art.isdigit() else art
 row=dict(art=art,cardIds=[c['card'] for c in cards if c['art']==art],Legacy=[s for s in legacy if s.startswith(prefix)],Native=[s for s in native if s.startswith(prefix)],Latest=[s for s in latest if s.startswith(prefix)])
 rows.append(row)
(D/'missing-source-id-inventory.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2))

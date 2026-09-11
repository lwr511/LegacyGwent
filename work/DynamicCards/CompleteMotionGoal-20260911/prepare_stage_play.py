from pathlib import Path
import json
W=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911');P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');H=W/'OgoImportHarness';p=H/'Assets/DynamicCards/Runtime/SourceParticles/SourceCandleTextureOffset.cs';p.write_text(p.read_text().replace('GetComponent<Renderer>().materials[m_matIndex]','GetComponent<Renderer>().sharedMaterials[m_matIndex]'))
ids=set()
for file in ['ogo-valid-animation-contracts.json','particle-curve-contracts.json','hayate-contracts.json','postfx-contracts.json','source-helper-contracts.json','candle-contracts.json','lightning-contracts.json','doppler-material-contracts.json']:
 p=W/file
 if not p.exists():print('MISSING CONTRACT',file);continue
 for r in json.loads(p.read_text())['records']:ids.add(r['scene'])
cat=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'));cat['cards']=[c for c in cat['cards'] if c['id'] in ids];(W/'stage-play-cards.json').write_text(json.dumps(cat,ensure_ascii=False,indent=2),encoding='utf-8');print('Stage play scenes',len(cat['cards']),len(ids));(H/'Assets/Diagnostics').mkdir(exist_ok=True)

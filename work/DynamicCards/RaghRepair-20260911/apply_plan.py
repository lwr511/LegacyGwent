"""Apply the reviewed dependency closure, verifying every source file first."""
from pathlib import Path
import hashlib, json, shutil

w = Path(__file__).parent
p = Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
plan = json.loads((w / 'restore-plan.json').read_text())
catalog = p / 'Assets/DynamicCards/Content/catalog.json'
data = json.loads(catalog.read_text(encoding='utf-8-sig'))
entry = next(c for c in data['cards'] if '11310100' in c['artIds'])
assert entry == plan['previous'] and entry['id'] == '11310101'
assert not any(c['id'] == plan['restored']['id'] for c in data['cards'])
for row in plan['copies']:
    source, dest = Path(row['source']), p / row['path']
    assert hashlib.sha256(source.read_bytes()).hexdigest() == row['sha256'], source
    assert not dest.exists() and Path(str(source) + '.meta').is_file(), dest
assert not (w / 'catalog-before.json').exists()
shutil.copy2(catalog, w / 'catalog-before.json')
ready = p / 'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready'
if ready.exists():
    shutil.copy2(ready, w / 'ready-before')
    ready.unlink()
for row in plan['copies']:
    source, dest = Path(row['source']), p / row['path']
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, dest)
    shutil.copy2(str(source) + '.meta', str(dest) + '.meta')
entry['artIds'].remove('11310100')
data['cards'].append(plan['restored'])
catalog.write_text(json.dumps(data, indent=2), encoding='utf-8')
plan['applied'] = True
(w / 'restore-applied.json').write_text(json.dumps(plan, indent=2), encoding='utf-8')
print('PASS: restored 40 assets and the Latest/10400101 mapping; existing source partitions retained.')

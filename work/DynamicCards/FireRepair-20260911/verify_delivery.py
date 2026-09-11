import base64
import hashlib
import importlib.util
import json
from pathlib import Path

work = Path(__file__).resolve().parent
project = work.parents[2] / 'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
cache = project / 'Library/DynamicCardsBundles/StandaloneWindows64'
harness_cache = work.parent / 'MotionIntegrity/FinalHarness/Library/DynamicCardsBundles/StandaloneWindows64'
spec = importlib.util.spec_from_file_location('fire_repair', work.parent / 'repair_fire_texture_import.py')
repair = importlib.util.module_from_spec(spec)
spec.loader.exec_module(repair)
assert (work / 'build-result.txt').read_text().startswith('PASS')
assert (work / 'delivery-result.txt').read_text().startswith('PASS')
assert (cache / 'cards.bundle.editor-ready').is_file()
files = {row['path']: row for row in json.loads((cache / 'cards.bundle.editor-files.json').read_text())['files']}
for name in repair.TEXTURES:
    path = 'Assets/DynamicCards/Content/Old/' + name + '.meta'
    digest = base64.b64encode(hashlib.sha256((project / path).read_bytes()).digest()).decode()
    assert files[path]['hash'] == digest, path
    assert 'alphaIsTransparency: 0' in (project / path).read_text(), path
index = json.loads((cache / 'cards.index.json').read_text())
payload = ['cards.bundle', 'cards.index.json'] + [part['file'] for part in index['parts']]
rows = []
for name in payload:
    path = cache / name
    rows.append(dict(file=name, bytes=path.stat().st_size, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
for name in ['cards-thronebreaker-002.bundle', 'cards-legacy2017-001.bundle']:
    assert hashlib.sha256((harness_cache / name).read_bytes()).hexdigest() == next(row['sha256'] for row in rows if row['file'] == name)
result = dict(status='PASS', payloadFiles=len(rows), totalBytes=sum(row['bytes'] for row in rows), repairedTextureManifests=4, testedBundlesMatch=True, files=rows)
(work / 'delivery-audit.json').write_text(json.dumps(result, indent=2))
print(json.dumps({key: value for key, value in result.items() if key != 'files'}, indent=2))

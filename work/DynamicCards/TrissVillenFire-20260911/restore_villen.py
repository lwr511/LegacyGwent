"""Restore Villentretenmerth's animated source; dry-run unless --apply is given."""
from pathlib import Path
import argparse, hashlib, json, re, shutil, subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--apply', action='store_true')
args = parser.parse_args()
project = Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
work = Path(__file__).parent
backup = work.parent / 'BeforeOldSources-20260908/Content'
content = project / 'Assets/DynamicCards/Content'
guid_re = re.compile(r'guid: ([0-9a-f]{32})')

def index(root):
    result = {}
    scan = subprocess.run(['rg', '--no-ignore', '--crlf', '--with-filename', '--no-line-number', '--color', 'never', '-g', '*.meta', '^guid: [0-9a-f]{32}$', str(root)], capture_output=True, text=True, encoding='utf-8', check=True)
    for line in scan.stdout.splitlines():
        match = re.match(r'^(.*):guid: ([0-9a-f]{32})$', line)
        if match:
            asset = Path(match[1]).with_suffix('')
            assert match[2] not in result, (match[2], asset, result.get(match[2]))
            result[match[2]] = asset
    print('Indexed', str(root), len(result), flush=True)
    return result

current = index(project / 'Assets')
old = index(backup)
catalog_path = content / 'catalog.json'
data = json.loads(catalog_path.read_text(encoding='utf-8-sig'))
prior = json.loads((backup / 'catalog.json').read_text(encoding='utf-8-sig'))
entry = next(c for c in prior['cards'] if '11210700' in c.get('artIds', []))
assert entry['id'] == '10130101'
entry.update(sourceVersion='Latest', sourceId='10130101')
existing = [c for c in data['cards'] if '11210700' in c.get('artIds', [])]
assert len(existing) == 1 and existing[0]['id'] in ('11210701', '10130101')
queue = [backup / 'Latest/10130101/Card.prefab', backup / Path(entry['audio']).relative_to('Assets/DynamicCards/Content')]
queue += [p for p in (backup / 'Latest/10130101').iterdir() if p.is_file() and p.suffix != '.meta']
copies, visited, missing = {}, set(), set()
while queue:
    source = queue.pop()
    if source in visited:
        continue
    visited.add(source)
    assert source.is_file(), source
    if source.is_relative_to(backup):
        dest = content / source.relative_to(backup)
        if dest.exists():
            assert source.with_suffix(source.suffix + '.meta').read_bytes() == dest.with_suffix(dest.suffix + '.meta').read_bytes() or re.search(r'^guid: .+$', source.with_suffix(source.suffix + '.meta').read_text(), re.M)[0] == re.search(r'^guid: .+$', dest.with_suffix(dest.suffix + '.meta').read_text(), re.M)[0], dest
            source = dest
        else:
            copies[dest] = source
    if source.suffix not in ('.prefab', '.mat', '.anim', '.controller', '.asset', '.shader'):
        continue
    raw = source.read_bytes()
    if b'\x00' in raw[:200]:
        continue
    for guid in set(guid_re.findall(raw.decode('utf-8', errors='replace'))):
        if guid.startswith('0000000000000000'):
            continue
        dep = current.get(guid) or old.get(guid)
        if dep is None:
            missing.add(guid)
        else:
            queue.append(dep)
assert not missing, sorted(missing)
report = {'applied': args.apply, 'previous': existing[0], 'restored': entry,
          'copies': [{'path': str(d.relative_to(project)), 'source': str(s), 'sha256': hashlib.sha256(s.read_bytes()).hexdigest()} for d, s in sorted(copies.items())],
          'dependencyAssets': len(visited)}
if args.apply:
    saved = work / 'catalog-before.json'
    if not saved.exists():
        shutil.copy2(catalog_path, saved)
    for dest, source in copies.items():
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, dest)
        shutil.copy2(str(source) + '.meta', str(dest) + '.meta')
    if existing[0]['id'] == '11210701':
        # Keep the existing Legacy2017 partitions stable; this scene is no longer mapped.
        existing[0]['artIds'].remove('11210700')
        assert not any(c['id'] == entry['id'] for c in data['cards'])
        data['cards'].append(entry)
        catalog_path.write_text(json.dumps(data, indent=2), encoding='utf-8')
    else:
        assert existing[0] == entry
    ready = project / 'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready'
    if ready.exists():
        shutil.copy2(ready, work / 'ready-before')
        ready.unlink()
(work / ('restore-applied.json' if args.apply else 'restore-plan.json')).write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({'apply': args.apply, 'copyAssets': len(copies), 'dependencyAssets': len(visited)}))

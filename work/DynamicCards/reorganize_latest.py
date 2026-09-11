"""Plan the latest-only content closure; never delete assets from this script."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import collections, json, re, mmap

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1] / 'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
CONTENT = PROJECT / 'Assets/DynamicCards/Content'
OUT = ROOT / 'LatestOnly'
OUT.mkdir(exist_ok=True)
catalog = json.loads((CONTENT / 'catalog.json').read_text(encoding='utf8'))
matches = {c['id']: c for c in json.loads((ROOT / 'latest_source_matches.json').read_text())}
cards = [c for c in catalog['cards'] if '/Latest/' in c['prefab']]
for c in cards:
    c['artIds'] = sorted(set(c['artIds']) | set(matches.get(c['id'], {}).get('artIds', [])))
bindings = collections.defaultdict(list)
for c in cards:
    for a in c['artIds']: bindings[a].append(c['id'])
assert len(cards) == len({c['id'] for c in cards}) == 1279
assert all(len(v) == 1 for v in bindings.values()), 'Ambiguous latest mapping'
(OUT / 'catalog.json').write_text(json.dumps(dict(version=1, cards=cards), indent=2), encoding='utf8')

def meta(p):
    m = re.search(rb'^guid: ([a-f0-9]{32})', p.read_bytes(), re.M)
    return (m[1], Path(str(p)[:-5])) if m else None
with ThreadPoolExecutor(max_workers=8) as pool:
    pairs = [x for x in pool.map(meta, (PROJECT / 'Assets').rglob('*.meta')) if x]
print('META_INDEX_READY', len(pairs), flush=True)
index = dict(pairs)
assert len(index) == len(pairs), 'Duplicate GUIDs'
pattern = re.compile(rb'guid: ([a-f0-9]{32})')
suffixes = {'.prefab', '.mat', '.controller', '.asset', '.anim', '.overrideController', '.meta'}
def refs(p):
    if p.suffix not in suffixes or not p.is_file() or not p.stat().st_size: return set()
    with p.open('rb') as f, mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ) as data:
        found = {m[1] for m in pattern.finditer(data) if not m[1].startswith(b'00000000')}
        if b'3e080548dacc61344a5969d8317f5acc' in found: print('MISSING_GUID_OWNER', str(p), flush=True)
        return found
files = [p for p in (CONTENT / 'Latest').rglob('*') if p.is_file()]
seen = set(files); pending = files; missing = set(); external = set()
while pending:
    print('SCAN_CLOSURE', len(pending), flush=True)
    with ThreadPoolExecutor(max_workers=4) as pool:
        ids = set().union(*pool.map(refs, pending))
    pending = []
    for guid in ids:
        p = index.get(guid)
        if p is None: missing.add(guid.decode()); continue
        if p not in seen:
            seen.add(p); pending.append(p)
            if p.is_file() and CONTENT in p.parents and CONTENT / 'Latest' not in p.parents:
                external.add(p)
print('MISSING_GUIDS', sorted(missing), flush=True)
keep = set(files) | {CONTENT / 'catalog.json', CONTENT / 'catalog.json.meta'}
for p in external:
    keep.update([p, Path(str(p) + '.meta')])
remove = sorted(p for p in CONTENT.rglob('*') if p.is_file() and p not in keep and not (p.suffix == '.meta' and Path(str(p)[:-5]).is_dir()))
arts = set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"', (PROJECT.parents[2] / 'Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf8')))
report = dict(cards=len(cards), covered=len(arts & bindings.keys()), missingArt=sorted(arts-bindings.keys()), externalDependencies=[str(p.relative_to(PROJECT)) for p in sorted(external)], remove=[str(p.relative_to(PROJECT)) for p in remove], removeBytes=sum(p.stat().st_size for p in remove), keptBytes=sum(p.stat().st_size for p in keep if p.is_file()), missingGuids=sorted(missing))
(OUT / 'plan.json').write_text(json.dumps(report, indent=2), encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k not in ('remove','externalDependencies','missingArt')}), flush=True)
print('EXTERNAL_DEPENDENCIES', len(external), 'REMOVE_FILES', len(remove), flush=True)

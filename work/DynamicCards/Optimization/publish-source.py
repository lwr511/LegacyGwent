from pathlib import Path
import json, hashlib, base64, shutil

r=Path(__file__).resolve().parent
repo=r.parents[2]
main=repo/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
probe=r.parent/'Probe'
plan=json.loads((r/'texture-dedup.json').read_text())
build=json.loads((r/'build-result.json').read_text())
assert build['passed'] and build['bytes']<7011978254, 'Optimized packages did not shrink'
page=json.loads((r/'page-optimized.json').read_text())
assert page['cards']==20 and page['bundles']
baseline_page=json.loads((r/'page-bundle-base.json').read_text())
assert page['totalMs']<=baseline_page['totalMs']*1.35 and page['p95FrameMs']<=30 and page['maxFrameMs']<=500, 'Review compressed bundle load performance before publishing'
cache=main/'Library/DynamicCardsBundles/StandaloneWindows64'
baseline={x['path']:x['hash'] for x in json.loads((cache/'cards.bundle.editor-files.json').read_text())['files']}
def digest(p):
    return base64.b64encode(hashlib.sha256(p.read_bytes()).digest()).decode()
paths=set(plan['changed']) | {p.relative_to(probe).as_posix() for p in (probe/'Assets/DynamicCards/Content/Latest').rglob('*.png.meta')}
root=(main/'Assets/DynamicCards/Content/Latest').resolve()
for rel in sorted(paths):
    dst=main/rel; src=probe/rel
    assert root in dst.resolve().parents and src.is_file()
    assert digest(dst) in {baseline.get(rel),digest(src)}, 'Concurrent edit: '+rel
for rel in plan['removed']:
    src=main/rel; canonical=main/plan['pathMap'][rel]
    assert root in src.resolve().parents and root in canonical.resolve().parents
    assert digest(src)==digest(canonical)
    for p in (src,Path(str(src)+'.meta')):
        assert digest(p)==baseline[p.relative_to(main).as_posix()], 'Concurrent edit: '+str(p)
marker=cache/'cards.bundle.editor-ready'
if marker.exists():marker.unlink()
for rel in sorted(paths):
    dst=main/rel; src=probe/rel
    if digest(dst)==digest(src):continue
    temp=r/'publish-file.tmp'
    shutil.copyfile(src,temp)
    temp.replace(dst)
(r/'source-published.json').write_text(json.dumps({'copied':len(paths),'pendingDuplicateRemoval':len(plan['removed'])}))
print('SOURCE_REFERENCES_AND_IMPORTERS_PUBLISHED',len(paths),flush=True)

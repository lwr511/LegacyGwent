"""Record the fully verified repair set; do not change main project assets."""
from pathlib import Path
import json,hashlib,datetime
W=Path(__file__).resolve().parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.hexdigest()
v=read(W/'staged-rotation-verification.json')
smoke=read(W/'bundle-smoke-results.json')
orderSmoke=read(W/'binding-order-smoke-results.json')
jobs=read(W/'rotation-repair-jobs.json')['jobs']
assert v['complete'] and not v['errors'] and len(v['clips'])==len(jobs)==873
assert smoke['complete'] and len(smoke['cases'])==3
assert orderSmoke['complete'] and len(orderSmoke['cases'])==1
engine={r['original']:r for r in map(json.loads,(W/'rotation-repair-engine.jsonl').read_text().splitlines())}
tested=read(W/'bundle-smoke-input-hashes.json')
for r in tested:
    assert digest(Path(r['staged']))==digest(Path(r['smokeInput']))==r['finalExpectedHash'], 'Final stage differs from compiled Animator test input'
orderInput=read(W/'binding-order-smoke-input-hash.json')
assert digest(Path(orderInput['staged']))==orderInput['sha256'], 'Reordered-binding smoke input changed'
files=[]
for r in v['clips']:
    assert engine[r['original']]['preservedOtherCurves'] and engine[r['original']]['preservedKeys']
    files.append(dict(path=Path(r['original']).relative_to(P).as_posix(),staged=r['staged'],
        beforeHash=r['beforeHash'],afterHash=r['afterHash'],bytes=Path(r['staged']).stat().st_size,
        metaHash=digest(Path(r['staged']+'.meta')),maxSourceSampleErrorDegrees=r['maxErrorDegrees']))
report=dict(ready=True,generatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    mainProject=str(P),stage=str(W/'RepairStaging'),mergedIntoMain=False,
    mainBundlesRebuilt=False,actualGameUiAccepted=False,
    clipCount=len(files),correctedBoneTracks=sum(r['bones'] for r in engine.values()),
    sourceScenes=628,bytes=sum(r['bytes'] for r in files),
    maxSourceSampleErrorDegrees=max(r['maxSourceSampleErrorDegrees'] for r in files),
    isolatedAnimatorSmoke=smoke,bindingOrderAnimatorSmoke=orderSmoke,files=files,
    applyRequirements=['Verify current main file hashes against beforeHash before replacing any file',
        'Back up original files and metadata; preserve exact relative paths and GUIDs',
        'Rebuild the main dynamic-card bundles before testing in the actual game UI',
        'Complete per-card visual checks; source-sample agreement alone is not final acceptance'])
(W/'rotation-repair-ready-manifest.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:report[k] for k in ['ready','clipCount','correctedBoneTracks','bytes','maxSourceSampleErrorDegrees','mergedIntoMain','actualGameUiAccepted']}))

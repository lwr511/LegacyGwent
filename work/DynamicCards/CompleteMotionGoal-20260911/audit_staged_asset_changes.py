from pathlib import Path
import json,hashlib,collections
W=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911');P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');H=W/'OgoImportHarness';baseline=json.loads((W/'ogo-harness-baseline.json').read_text());changed=[];drift=[]
for r in baseline['files']:
 p=P/r['path'];h=H/r['path'];actual=hashlib.sha256(p.read_bytes()).hexdigest()
 if actual!=r['sha256']:drift.append(dict(path=r['path'],expected=r['sha256'],current=actual))
 stage=hashlib.sha256(h.read_bytes()).hexdigest()
 if stage!=r['sha256']:changed.append(dict(path=r['path'],before=r['sha256'],after=stage,bytes=h.stat().st_size))
(W/'staged-asset-change-inventory.json').write_text(json.dumps(dict(changed=changed,mainDrift=drift),indent=2));print('CHANGED',len(changed),collections.Counter(Path(r['path']).suffix for r in changed),'BYTES',sum(r['bytes'] for r in changed),'MAIN DRIFT',len(drift));print('OTHER',[r['path'] for r in changed if Path(r['path']).suffix not in ['.prefab','.anim','.cs']]);print('DRIFT',drift[:5])

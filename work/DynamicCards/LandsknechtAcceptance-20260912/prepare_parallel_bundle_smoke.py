"""Create three finalized test-only copies while the full repair batch runs."""
from pathlib import Path
import json, hashlib, shutil
W=Path(__file__).resolve().parent
request=json.loads((W/'bundle-smoke-cases.json').read_text())
entries={r['original']:r for r in map(json.loads,(W/'rotation-repair-engine.jsonl').read_text().splitlines())}
def digest(b):return hashlib.sha256(b).hexdigest()
def section(b):
    a=b.index(b'  m_ClipBindingConstant:');e=b.index(b'  m_AnimationClipSettings:',a)
    return a,e,b[a:e]
records=[]
for case in request['cases']:
    row=entries[case['before']]
    original=Path(case['before']).read_bytes();stage=Path(case['after']).read_bytes()
    assert digest(original)==row['beforeHash'] and digest(stage)==row['afterHash']
    _,_,before=section(original);a,e,after=section(stage)
    if before.replace(b'\r\n',b'\n')!=after.replace(b'\r\n',b'\n'):
        assert after.replace(b'\r\n',b'\n').strip()==b'm_ClipBindingConstant:\n    genericBindings: []\n    pptrCurveMapping: []'
        before=before.replace(b'\r\n',b'\n')
        if b'\r\n' in after:before=before.replace(b'\n',b'\r\n')
        stage=stage[:a]+before+stage[e:]
    dst=W/'BundleSmokeInputs'/case['name']/Path(case['after']).name
    dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(stage)
    records.append(dict(name=case['name'],staged=case['after'],smokeInput=str(dst),finalExpectedHash=digest(stage)))
    case['after']=str(dst)
(W/'bundle-smoke-cases.json').write_text(json.dumps(request,indent=2))
(W/'bundle-smoke-input-hashes.json').write_text(json.dumps(records,indent=2))
lab=W/'UnityBundleLab';assert not lab.exists(), 'Do not replace an existing lab'
(lab/'Assets/Editor').mkdir(parents=True);(lab/'Packages').mkdir();(lab/'ProjectSettings').mkdir()
shutil.copy2(W/'RotationBundleSmoke.cs',lab/'Assets/Editor/RotationBundleSmoke.cs')
shutil.copy2(W/'UnityCurveLab/ProjectSettings/ProjectVersion.txt',lab/'ProjectSettings/ProjectVersion.txt')
(lab/'Packages/manifest.json').write_text(json.dumps(dict(dependencies={f'com.unity.modules.{x}':'1.0.0' for x in ['animation','jsonserialize','assetbundle']})))
print('Prepared independent bundle lab and',len(records),'test-only finalized copies; full repair batch unaffected')

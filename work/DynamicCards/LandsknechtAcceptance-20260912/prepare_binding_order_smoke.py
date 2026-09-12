from pathlib import Path
import json,shutil,hashlib
W=Path(__file__).resolve().parent
def read(p):return json.loads(p.read_text())
fixes={r['original']:r for r in read(W/'binding-metadata-finalization.json')['changes']}
r=next(r for r in read(W/'all-rotation-audit.json')['clips'] if r['scene']=='16220501' and 'Loop' in r['clip'])
fix=fixes[r['file']]
assert hashlib.sha256(Path(fix['staged']).read_bytes()).hexdigest()==fix['afterHash']
metadata={}
for data in ['LegacyAnimationData','NativeAnimationData','LatestAnimationData']:
    for record in read(W.parent/data/'animations.json')['animators']:
        for c in record['clips']:metadata[(str(W.parent/data/c['file']),record['path'],c['name'])]=c
c=metadata[(r['data'],r['animator'],r['clip'])]
case=dict(name='16220501-Loop',before=r['file'],after=fix['staged'],data=r['data'],frames=c['frames'],columns=c['columns'],duration=c['duration'],tracks=[t for t in c['tracks'] if t.get('typeId',0)==0])
(W/'binding-order-smoke-cases.json').write_text(json.dumps(dict(cases=[case]),indent=2))
(W/'binding-order-smoke-input-hash.json').write_text(json.dumps(dict(staged=fix['staged'],sha256=fix['afterHash']),indent=2))
lab=W/'UnityBindingOrderLab';assert not lab.exists()
(lab/'Assets/Editor').mkdir(parents=True);(lab/'Packages').mkdir();(lab/'ProjectSettings').mkdir()
code=(W/'RotationBundleSmoke.cs').read_text().replace('bundle-smoke','binding-order-smoke').replace('BundleSmokePayload','BindingOrderSmokePayload').replace('rotation-smoke','binding-order-smoke')
(lab/'Assets/Editor/RotationBundleSmoke.cs').write_text(code)
shutil.copy2(W/'UnityCurveLab/ProjectSettings/ProjectVersion.txt',lab/'ProjectSettings/ProjectVersion.txt')
shutil.copy2(W/'UnityBundleLab/Packages/manifest.json',lab/'Packages/manifest.json')
print('Prepared reordered-binding smoke case:',case['frames'],'frames;',len(case['tracks']),'transform tracks')

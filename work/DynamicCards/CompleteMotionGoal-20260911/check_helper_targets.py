from pathlib import Path
import json,re
W=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911')
ns={'__file__':str(W/'audit_current_bindings.py')};exec((W/'audit_current_bindings.py').read_text().split('issues=[];rows=[]')[0],ns)
for r in json.loads((W/'source-helper-contracts.json').read_text())['records']:
 p=W/'OgoImportHarness'/r['prefab'];paths,_=ns['prefab_paths'](p);obs=ns['blocks'](p.read_text(encoding='utf-8-sig'))
 for s in r['components']:
  a=ns['source_anchor'](paths,s['path']);go=paths.get(a);types=[ty for ty,b in obs.values() if ns['ref'](b,'m_GameObject')==go];print(r['scene'],s['path'],'TARGET',a,'TYPES',types)

from pathlib import Path
import re,json
r=Path(__file__).resolve().parent;changes=[]
for p in (r/'Probe/Assets/DynamicCards/Content').rglob('Card.prefab'):
 body=p.read_text()
 def fix(m):
  text=re.sub(r'm_UpdateMode: \d+','m_UpdateMode: 2',m[0])
  return re.sub(r'm_KeepAnimatorControllerStateOnDisable: \d+','m_KeepAnimatorControllerStateOnDisable: 1',text)
 updated=re.sub(r'--- !u!95 &.*?(?=\n--- !u!|\Z)',fix,body,flags=re.S)
 if updated!=body:p.write_text(updated);changes.append(str(p.relative_to(r/'Probe')))
(r/'normalized_animator_modes.json').write_text(json.dumps(changes,indent=2))
print('ANIMATOR_PAUSE_SETTINGS_NORMALIZED',len(changes))

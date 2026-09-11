import json
from pathlib import Path
r=Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards')
d=json.loads((r/'latest_source_effects.json').read_text());print(type(d));
if isinstance(d,dict):
 print(list(d)[:3]);
 for k,v in d.items():
  if '100901' in k:print(k,str(v)[:18000])

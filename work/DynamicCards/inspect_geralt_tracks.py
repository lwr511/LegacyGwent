import json
from pathlib import Path
r=Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/LatestAnimationData')
for a in json.loads((r/'animations.json').read_text())['animators']:
 if a['id']=='10090101':
  for c in a['clips']:print(a['path'],c['name'],[(t['path'],t['attribute'],t.get('typeId')) for t in c['tracks'] if t.get('typeId')])

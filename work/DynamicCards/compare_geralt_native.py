import json,sys
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np
r=Path(__file__).resolve().parent
native=json.loads((r/'geralt-native-poses.json').read_text())['rows']
a=next(a for a in json.loads((r/'LatestAnimationData/animations.json').read_text())['animators'] if a['id']=='10090101' and a['path'].endswith('model'))
c=a['clips'][0]; values=np.fromfile(r/'LatestAnimationData'/c['file'],dtype='<f4').reshape(c['frames'],c['columns'])
tracks={(t['path'],t['attribute']):t for t in c['tracks'] if not t.get('typeId')}
errors=[]
for row in native:
 if row['animator']!='model' or row['time']>=4.2:continue
 for attr,key in [(1,'p'),(2,'q'),(3,'s')]:
  t=tracks.get((row['path'],attr))
  if not t:continue
  expected=np.array(list(row[key].values())); sampled=values[round(row['time']*30),t['offset']:t['offset']+t['dimension']]
  error=float(min(np.linalg.norm(expected-sampled),np.linalg.norm(expected+sampled)) if attr==2 else np.linalg.norm(expected-sampled))
  if error>.002:errors.append(dict(time=row['time'],path=row['path'],attribute=attr,error=error,native=expected.tolist(),decoded=sampled.tolist()))
errors.sort(key=lambda x:-x['error']);(r/'geralt-native-comparison.json').write_text(json.dumps(errors,indent=2))
print('DIFFERENCES',len(errors));print(json.dumps(errors[:8],indent=2))

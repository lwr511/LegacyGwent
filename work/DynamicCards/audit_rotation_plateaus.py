import json,re,math,time
from pathlib import Path
r=Path(__file__).resolve().parent
content=r.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content'
vector=re.compile(r'(?:value|outSlope): \{([^}]+)\}')
rows=[]; count=0
for file in content.rglob('*.anim'):
 parts=[];inside=False
 with file.open(encoding='utf-8-sig') as f:
  for line in f:
   if line.startswith('  m_RotationCurves:'):inside=True
   elif inside and line.startswith('  m_CompressedRotationCurves:'):break
   elif inside:parts.append(line)
 count+=1
 bad=[]
 for curve in re.split(r'(?m)^  - curve:', ''.join(parts))[1:]:
  keys=[]
  for key in re.split(r'(?m)^      - serializedVersion:',curve)[1:]:
   t=re.search(r'\btime: ([^\n]+)',key); vs=vector.findall(key)
   if t and len(vs)>=2:keys.append((float(t[1]),*[tuple(float(x) for x in re.findall(r'[xyzw]: ([^,}]+)',v)) for v in vs[:2]]))
  spikes=[]
  for (t,v,s),(nt,nv,_) in zip(keys,keys[1:]):
   if len(v)!=4 or len(nv)!=4:continue
   delta=min(sum((a-b)**2 for a,b in zip(v,nv)),sum((a+b)**2 for a,b in zip(v,nv)))**.5
   if delta<.0001 and (nt-t)*sum(x*x for x in s)**.5>.02:spikes.append(t)
  if spikes:bad.append(dict(path=re.search(r'    path: (.*)',curve)[1],times=spikes))
 if bad:rows.append(dict(file=str(file.relative_to(content)),curves=bad))
result=dict(scanned=count,affectedFiles=len(rows),affectedCards=len(set(x['file'].split('\\')[1] for x in rows)),files=rows)
(r/'rotation-plateau-audit.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:v for k,v in result.items() if k!='files'}))

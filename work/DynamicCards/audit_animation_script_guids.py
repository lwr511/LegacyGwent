from pathlib import Path
import re,json,collections,time
p=Path(r'C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
known={re.search(r'^guid: (\w+)',m.read_text(),re.M)[1]:str(m) for m in (p/'Assets').rglob('*.cs.meta')}
rows=[]; refs=collections.Counter();start=time.time()
for f in (p/'Assets/DynamicCards/Content').rglob('*.anim'):
 s=f.read_text(encoding='utf-8-sig')
 ids=collections.Counter(re.findall(r'script: \{fileID: 11500000, guid: (\w+)',s))
 for g,n in ids.items():
  refs[g]+=n
  if g not in known:rows.append({'file':str(f.relative_to(p)),'guid':g,'bindings':n})
out=Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/animation-script-guid-audit.json');out.write_text(json.dumps({'missing':rows,'references':dict(refs)},indent=2))
print('AUDIT',len(rows),'clips',len({r['file'] for r in rows}),'cards',len({str(Path(r['file']).parent) for r in rows}),'guids',dict(collections.Counter(r['guid'] for r in rows)),'seconds',round(time.time()-start),flush=True)

from pathlib import Path
import json,re
r=Path(__file__).resolve().parent;s=r/'Probe';d=json.loads((r/'client_guid_audit.json').read_text());need={g for g,p in d['missing']};found={}
for folder in ['PortableCards','NativeCard']:
 for p in (s/'Assets'/folder).rglob('*.meta'):
  m=re.search(r'^guid: (\w+)',p.read_text(),re.M)
  if m and m[1] in need:found[m[1]]=str(p.relative_to(s))[:-5]
remaining=[(g,p) for g,p in d['missing'] if g not in found]
(r/'client_external_dependency_plan.json').write_text(json.dumps(dict(found=found,remaining=remaining),indent=2))
print('EXTERNAL',found);print('REMAINING',len(remaining));print(remaining[:10])

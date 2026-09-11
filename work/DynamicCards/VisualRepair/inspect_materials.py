from pathlib import Path
import re,json
p=Path(r'C:\UnityProjects\LegacyGwent\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card'); root=p/'Assets/DynamicCards'; idx={}
for f in (root/'Content/Old/Legacy2017').rglob('*.mat.meta'):
 m=re.search(r'^guid: (\w+)',f.read_text(),re.M)
 if m:idx[m[1]]=Path(str(f)[:-5])
for ident in ['11210701','13210201']:
 body=(root/f'Content/Old/Legacy2017/{ident}/Card.prefab').read_text();guids=set(re.findall(r'guid: (\w{32})',body)); mats=[]
 for g in guids & idx.keys():
  f=idx[g];b=f.read_text();sh=re.search(r'm_Shader: .*?guid: (\w+)',b);mats.append(dict(path=str(f),shader=sh[1] if sh else '',progress=re.findall(r'.*_Progress.*',b)))
 Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards\VisualRepair',ident+'-materials.json').write_text(json.dumps(mats,indent=2))
 print(ident,[(Path(x['path']).name,x['shader'],x['progress']) for x in mats])

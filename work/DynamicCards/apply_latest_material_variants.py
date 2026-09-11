from pathlib import Path
import json,re,shutil
root=Path(__file__).resolve().parent
variants={(v['source'],tuple(v['keywords'])):v for v in json.loads((root/'latest_variant_manifest.json').read_text())}
for path in (root/'Probe/Assets/DynamicCards/Content/Latest').glob('*/conversion.json'):
 data=json.loads(path.read_text())
 for material in data['materials']:
  if not material['shader'].startswith('ShaderLibrary/'):continue
  original=material['asset'].replace('Assets/DynamicCards/Content/Latest/','Assets/PortableCards/')
  body=(root/'LatestBridge2022'/original).read_text();keys=set()
  for field in ['m_ValidKeywords','m_InvalidKeywords']:
   block=re.search(r'  '+field+r':(.*?)(?=\n  [^ -]|\Z)',body,re.S)
   if block:keys.update(re.findall(r'^  - (.+)$',block[1],re.M))
  v=variants.get((material['shader'],tuple(sorted(keys))))
  if not v:raise RuntimeError(str(path)+': missing variant '+str(keys))
  material['portableShader']=v['name']
 path.write_text(json.dumps(data,indent=2))
for folder in ['LatestShaders','LatestVariants']:
 dest=root/'Probe/Assets/DynamicCards/Shaders'/('Latest' if folder=='LatestShaders' else folder);dest.mkdir(parents=True,exist_ok=True)
 for path in (root/folder).iterdir():shutil.copy2(path,dest/path.name)

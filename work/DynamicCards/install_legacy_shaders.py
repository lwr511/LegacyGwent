import json,re,shutil
from pathlib import Path
root=Path(__file__).resolve().parent
client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
manifest=json.loads((root/'legacy_shader_manifest.json').read_text())
mapping={m['source']:m for m in manifest}
dest=client/'Assets/DynamicCards/Shaders/Legacy';dest.mkdir(exist_ok=True)
for item in manifest:
    for suffix in ['', '.meta']:shutil.copy2(root/'LegacyShaders'/(item['file']+suffix),dest/(item['file']+suffix))
missing=set();count=0
for path in (client/'Assets/DynamicCards/Content/Legacy2017').glob('*/conversion.json'):
    for info in json.loads(path.read_text())['materials']:
        shader=mapping.get(info['shader'])
        if shader is None:missing.add(info['shader']);continue
        target=client/info['asset'];body=target.read_text()
        body=re.sub(r'm_Shader: \{[^\n]+\}', 'm_Shader: {fileID: 4800000, guid: '+shader['guid']+', type: 3}',body)
        target.write_text(body);count+=1
print('LEGACY_SHADERS',len(manifest),'MATERIALS',count,'MISSING',sorted(missing))

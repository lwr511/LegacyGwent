import json,re,shutil
from pathlib import Path
root=Path(__file__).resolve().parent
client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
dest=client/'Assets/DynamicCards/Shaders/Native';dest.mkdir(exist_ok=True)
manifest=json.loads((root/'native_shader_manifest.json').read_text());shaders={m['source']:m for m in manifest}
for info in manifest:
    for suffix in ['', '.meta']:shutil.copy2(root/'NativeShaders'/(info['file']+suffix),dest/(info['file']+suffix))
done=set()
for path in (client/'Assets/DynamicCards/Content').glob('*/conversion.json'):
    data=json.loads(path.read_text())
    for material in data['materials']:
        if material['shader'] not in shaders or material['asset'] in done:continue
        done.add(material['asset']);native=shaders[material['shader']]
        source=root/'Bridge2022'/material['asset'].replace('DynamicCards/Content','PortableCards')
        text=source.read_text()
        text=re.sub(r'm_Shader: \{[^\n]+\}','m_Shader: {fileID: 4800000, guid: '+native['guid']+', type: 3}',text)
        text=re.sub(r'\{fileID: \d+, guid: 0{32}, type: 0\}','{fileID: 0}',text)
        (client/material['asset']).write_text(text,encoding='utf8')
print('restored native materials',len(done))

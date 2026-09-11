import json,re
from pathlib import Path
root=Path(__file__).resolve().parent
dest=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content'
states=json.loads((root/'shader_rendering.json').read_text())
count=0
for path in dest.glob('*/conversion.json'):
    data=json.loads(path.read_text())
    for material in data['materials']:
        info=states.get(material['shader'])
        if not info or not info['states']:continue
        state=next((s for s in info['states'] if s.get('m_Name','').lower() not in ['shadowcaster','meta']),info['states'][0])
        source=root/'Bridge2022'/material['asset'].replace('DynamicCards/Content','PortableCards')
        text=source.read_text()
        def value(item):
            key=item.get('name','');match=re.search(r'^    - '+re.escape(key)+r': ([\d.eE+-]+)$',text,re.M)
            return float(match[1]) if match else item['val']
        tags=dict(info['tags'].get('tags',[]));queue=tags.get('QUEUE','Geometry')
        base=re.match(r'(\w+)([+-]\d+)?',queue)
        q={'Background':1000,'Geometry':2000,'AlphaTest':2450,'Transparent':3000,'Overlay':4000}.get(base[1],2000)+int(base[2] or 0)
        custom=re.search(r'm_CustomRenderQueue: (-?\d+)',text)
        if custom and int(custom[1])>=0:q=int(custom[1])
        material.update(hasState=True,srcBlend=value(state['rtBlend0']['srcBlend']),dstBlend=value(state['rtBlend0']['destBlend']),zWrite=value(state['zWrite']),cull=value(state['culling']),queue=q)
        count+=1
    path.write_text(json.dumps(data,indent=2),encoding='utf8')
print('material states',count)

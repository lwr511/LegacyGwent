from pathlib import Path
import json,re,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parent
project=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
path=project/'Assets/DynamicCards/Content/catalog.json'
doc=json.loads(path.read_text(encoding='utf8'))
cards={c['id']:c for c in doc['cards']}
bound={a for c in cards.values() for a in c['artIds']}
templates={e.get('Id'):e for e in ET.parse(root/'latest_defs_Templates.xml').getroot() if e.get('Type')=='CardTemplate'}
arts=set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"',(root.parents[1]/'src/Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf8')))
added=[]
for art in sorted(arts-bound):
    if not re.fullmatch(r'\d{6}00',art):continue
    template=templates.get(art[:6])
    if template is None:continue
    scene=template.get('ArtId','')+'0101'
    if scene not in cards:continue
    cards[scene]['artIds'].append(art)
    added.append(dict(art=art,scene=scene,name=template.get('DebugName'),evidence='latest_defs_Templates.xml: Template Id -> ArtId'))
out=root/'AnimationRegression';out.mkdir(exist_ok=True)
(out/'catalog-before.json').write_bytes(path.read_bytes())
data=json.dumps(doc,indent=2,ensure_ascii=True)+'\n'
path.write_text(data,encoding='utf8')
(root/'Probe/Assets/DynamicCards/Content/catalog.json').write_text(data,encoding='utf8')
(out/'restored-bindings.json').write_text(json.dumps(added,indent=2),encoding='utf8')
print(json.dumps(added,indent=2))

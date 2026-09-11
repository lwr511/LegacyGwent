from pathlib import Path
import json,re,shutil
w=Path(__file__).parent
p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
root='Assets/DynamicCards/Content/Old/Legacy2017/'
particle=root+'Shared/20005601_310072__20005601_AguaraFoxForm_Cloud.mat'
layer=root+'Shared/20005601_310086__20005601_AguaraFoxForm_Cloud.mat'
texture=root+'Shared/11220501_39440_clouds_4x4.png'
conv=p/(root+'20005601/conversion.json')
data=json.loads(conv.read_text())
record=next(a for a in data['textureAssignments'] if a['material']=='[20005601]AguaraFoxForm_Cloud')
assert record=={'material':'[20005601]AguaraFoxForm_Cloud','properties':['_MainTex']}
for f in [p/particle,conv]:
    backup=w/('before-'+f.name)
    assert not backup.exists()
    shutil.copy2(f,backup)
guid=re.search(r'^guid: (\w+)',Path(str(p/texture)+'.meta').read_text(),re.M)[1]
assert guid=='8bc592d0f517d634580f6b25b7a728f1'
text=(p/particle).read_text()
assert 'guid: d5913f9c76d5b834984d205c23ddb5e4' in text
(p/particle).write_text(text.replace('guid: d5913f9c76d5b834984d205c23ddb5e4','guid: '+guid),encoding='utf-8')
record['materialAsset']=layer
data['textureAssignments'].append(dict(material=record['material'],materialAsset=particle,texture=texture,properties=['_MainTex']))
conv.write_text(json.dumps(data,indent=2),encoding='utf-8')
(w/'repair.json').write_text(json.dumps(dict(particleMaterial=particle,atlasMaterial=layer,correctTexture=texture,sourceParticleMaterialId=4,sourceAtlasMaterialId=11),indent=2))
print('PASS: cloud particles use their original shared cloud texture; flowmap layer keeps the card atlas.')

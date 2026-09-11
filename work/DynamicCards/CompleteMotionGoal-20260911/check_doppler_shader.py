from pathlib import Path
import json,re,hashlib
W=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911');p=W/'OgoImportHarness/Assets/DynamicCards/Runtime/SourceLightning/LightningPool.cs';p.write_text((W/'OriginalCode/LightningPool.cs').read_text(encoding='utf-8-sig').replace('namespace GwentUnity;','namespace Assets.Script.DynamicCards.SourceLightning\n{')+'\n}\n');Path(str(p)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'DynamicCards/SourceLightning/LightningPool').hexdigest()+'\n')
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');conv=json.loads((P/'Assets/DynamicCards/Content/Latest/16180101/conversion.json').read_text());mats=[m for m in conv['materials'] if 'SmokeChunks' in m['originalName']];print(mats)
for m in mats:
 code=(P/m['asset']).read_text();guid=re.search('m_Shader:.*guid: (\\w+)',code)[1];index=json.loads((W/'main-guid-index.json').read_text());shader=Path(index[guid]);print('shader',shader);print(shader.read_text()[:1200])

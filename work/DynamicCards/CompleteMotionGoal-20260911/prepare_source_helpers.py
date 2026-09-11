from pathlib import Path
import json,re,hashlib,shutil
W=Path(__file__).resolve().parent;H=W/'OgoImportHarness';P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
for name in ['MeshParticleVelocityAlign','Braenn_BowFix']:
 code=(W/'OriginalCode'/f'{name}.cs').read_text(encoding='utf-8-sig'); code=code.replace('public class ','namespace Assets.Script.DynamicCards.SourceParticles\n{\npublic class ',1)+'\n}\n'
 f=H/'Assets/DynamicCards/Runtime/SourceParticles'/f'{name}.cs';f.write_text('// Recovered original source behavior; namespace isolated.\n'+code,encoding='utf-8')
 Path(str(f)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('DynamicCards/SourceParticles/'+name).encode()).hexdigest()+'\n')
effects=json.loads((W.parent/'second_source_effects.json').read_text());cs=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards'];rs=[]
for c in cs:
 if '/Legacy2017/' not in c['prefab']:continue
 ss=[s for s in effects[c['sourceId']]['scripts'] if s['type'] in ['MeshParticleVelocityAlign','Braenn_BowFix'] and s['data']['m_Enabled']]
 if not ss:continue
 components=[]
 for s in ss:
  data={k:(bool(v) if k=='looping' else v) for k,v in s['data'].items() if not k.startswith('m_') and k!='followingObjects'}
  components.append(dict(kind=s['type'],path=s['path'],dataJson=json.dumps(data),followingObjects=[s['references'][f'followingObjects.{i}'] for i in range(len(s['data'].get('followingObjects',[])))]))
 rs.append(dict(scene=c['sourceId'],prefab=c['prefab'],components=components))
(W/'source-helper-contracts.json').write_text(json.dumps(dict(records=rs),indent=2));print(len(rs),sum(len(r['components']) for r in rs))

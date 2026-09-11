from pathlib import Path
import json,shutil
W=Path(__file__).resolve().parent;H=W/'OgoImportHarness';P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
helper=json.loads((W/'source-helper-contracts.json').read_text());ev=json.loads((W/'helper-original-component-evidence.json').read_text())
for r in helper['records']:
 for s in r['components']:s['sourceHasParticles']='ParticleSystem' in next(e for e in ev if e['scene']==r['scene'] and e['path']==s['path'])['sourceComponents']
(W/'source-helper-contracts.json').write_text(json.dumps(helper,indent=2))
p=H/'Assets/Editor/SourceHelperImport.cs';code=p.read_text(encoding='utf-8-sig').replace('public string kind,path,dataJson;','public string kind,path,dataJson;public bool sourceHasParticles;').replace('c is MeshParticleVelocityAlign && t.GetComponent<ParticleSystem>()==null','c is MeshParticleVelocityAlign && (t.GetComponent<ParticleSystem>()!=null)!=s.sourceHasParticles');code=code.replace('public bool sourceHasParticles;public bool sourceHasParticles;','public bool sourceHasParticles;');p.write_text(code,encoding='utf-8')
for p in (W/'CandleRuntime').glob('*'):
 dest=H/'Assets/DynamicCards/Runtime/SourceParticles'/p.name;shutil.copy2(p,dest)
 if dest.suffix=='.cs' and dest.stem=='SourceCandleFire':
  code=dest.read_text().replace('CandleRenderer.sharedMaterial = CandleMat;','CandleRenderer.sharedMaterial = CandleMat;\n\t\tCandleRenderer.forceRenderingOff = false;');dest.write_text(code,encoding='utf-8')
dest=H/'Assets/DynamicCards/Shaders/SourceCandles';dest.mkdir(parents=True,exist_ok=True)
for p in (W/'CandleShaders').glob('*'):shutil.copy2(p,dest/p.name)
dest=H/'Assets/DynamicCards/Content/SourceCandles';dest.mkdir(parents=True,exist_ok=True)
for p in (W/'SourceCandles').glob('*.png'):shutil.copy2(p,dest/p.name)
textures=json.loads((W/'candle-original-textures.json').read_text());cs=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards'];records=[]
for r in json.loads((W/'candle-source-records.json').read_text()):
 c=next(c for c in cs if c.get('sourceId',c['id'])==r['scene'] and '/'+r['source']+'/' in c['prefab']);data=r['data'];refs=r['refs'];fields={k:(bool(v) if k in ['UseZwriteShader','AdjustToScale'] else v) for k,v in data.items() if not k.startswith('m_') and k not in ['Positions','CandleMeshFilter','CandleRenderer','mesh','CandleMat','CandleTexture','NoiseTexture']}
 ts=[t for t in textures if t['source']==r['source'] and t['scene']==r['scene'] and t['path']==r['path']]
 records.append(dict(source=r['source'],scene=r['scene'],prefab=c['prefab'],path=r['path'],enabled=bool(data['m_Enabled']),dataJson=json.dumps(fields),positions=[refs[f'Positions.{i}'] for i in range(len(data['Positions']))],meshFilter=refs['CandleMeshFilter'],renderer=refs['CandleRenderer'],textures=[dict(property=t['property'],path='Assets/DynamicCards/Content/SourceCandles/'+t['file'],sRGB=t['colorSpace']==1,mips=t['mipCount']>1,filter=t['settings']['m_FilterMode'],wrap=t['settings']['m_WrapU']) for t in ts]))
(W/'candle-contracts.json').write_text(json.dumps(dict(records=records),indent=2));print('Candle components',len(records))

from pathlib import Path
import json,re,shutil
W=Path(__file__).resolve().parent;H=W/'OgoImportHarness';P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');records=json.loads((W/'lightning-source-records.json').read_text());textures=json.loads((W/'lightning-original-textures.json').read_text());out=[]
for r in records:
 conversion=json.loads((P/r['prefab']).with_name('conversion.json').read_text());components=[]
 for s in r['scripts']:
  def clean(v,key=''):
   if isinstance(v,dict):
    if 'm_FileID' in v and 'm_PathID' in v:return None
    return {k:clean(x,k) for k,x in v.items() if not k.startswith('m_') or k in ['m_Curve','m_PreInfinity','m_PostInfinity','m_RotationOrder']}
   if isinstance(v,list):return [bool(x) if key in ['FoldOuts','MaterialFoldOuts'] else clean(x) for x in v]
   if key in ['UseSpeed','UseRandomSections','Looping','PlayOnAwake']:return bool(v)
   return v
  fields=clean(s['data']);refs=[]
  for key,path in s['references'].items():
   if key.startswith('m_') or not path:continue
   if key in ['LightningTexture','TurbulenceTexture']:
    tr=next(t for t in textures if t['source']==r['source'] and t['scene']==r['scene'] and t['path']==s['path'] and t['property']==key)
    refs.append(dict(field=key,kind='Texture',asset='Assets/DynamicCards/Content/SourceLightning/'+tr['file'],sRGB=tr['colorSpace']==1,mips=tr['mipCount']>1,filter=tr['settings']['m_FilterMode'],wrap=tr['settings']['m_WrapU']));continue
   if 'WiggleMaterials' in key:
    matches=[m for m in conversion['materials'] if m['originalName']==path];assert len(matches)==1,(r['scene'],key,path,matches);refs.append(dict(field=key,kind='Material',asset=matches[0]['asset']));continue
   refs.append(dict(field=key,kind='Object',path=path))
  components.append(dict(kind=s['type'],path=s['path'],enabled=bool(s['data']['m_Enabled']),dataJson=json.dumps(fields),references=refs))
 cohort='Legacy' if r['source']=='Legacy2017' else 'Thronebreaker'
 out.append(dict(source=r['source'],scene=r['scene'],prefab=r['prefab'],shader=f'Assets/DynamicCards/Shaders/SourceLightning/{cohort}/Shader_Forge_LightningShader.shader',components=components,events=r['events']))
(W/'lightning-contracts.json').write_text(json.dumps(dict(records=out),indent=2));print('Lightning prefabs',len(out),'components',sum(len(r['components']) for r in out))
for source,dest in [(W/'LightningRuntime',H/'Assets/DynamicCards/Runtime/SourceLightning'),(W/'LightningShaders',H/'Assets/DynamicCards/Shaders/SourceLightning'),(W/'SourceLightningTextures',H/'Assets/DynamicCards/Content/SourceLightning')]:
 shutil.copytree(source,dest,dirs_exist_ok=True)

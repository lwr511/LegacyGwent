from pathlib import Path
import json,shutil
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');original=w/'Harness'
for name in ['FinalHarness','LongHarness']:
 target=w/name;target.mkdir(exist_ok=True)
 for sub in ['ProjectSettings','Packages']:
  shutil.copytree(original/sub,target/sub,dirs_exist_ok=True)
 for sub in ['Runtime','Resources','Content']:
  source=original/'Assets/DynamicCards'/sub
  shutil.copytree(source,target/'Assets/DynamicCards'/sub,dirs_exist_ok=True)
 for f in (p/'Assets/DynamicCards/Runtime').glob('*'):
  if f.name.startswith('DynamicCardSettingRow'):continue
  shutil.copy2(f,target/'Assets/DynamicCards/Runtime'/f.name)
 shutil.copy2(p/'Assets/DynamicCards/Content/catalog.json',target/'Assets/DynamicCards/Content/catalog.json')
 source=(original/'Assets/PostCatalogProbe.cs').read_text()
 probe='FinalCatalogProbe' if name=='FinalHarness' else 'LongCatalogProbe'
 result='final-all' if name=='FinalHarness' else 'final-long'
 frames='FinalFrames' if name=='FinalHarness' else 'LongFrames'
 source=source.replace('PostCatalogProbe',probe).replace('post-all',result).replace('PostFrames',frames)
 source=source.replace('public int invalidMaterials, missingBones, movingTransforms;', 'public int invalidMaterials, missingBones, movingTransforms, unsupportedShaders, unassignedVisible;')
 source=source.replace('missingBones=model.GetComponentsInChildren', 'unsupportedShaders=model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Count(m=>m!=null && (m.shader==null || !m.shader.isSupported)),unassignedVisible=model.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && r.sharedMaterials.All(m=>m==null) && ((r is MeshRenderer && r.GetComponent<MeshFilter>()!=null && r.GetComponent<MeshFilter>().sharedMesh!=null) || (r is SkinnedMeshRenderer && ((SkinnedMeshRenderer)r).sharedMesh!=null) || (r is ParticleSystemRenderer && ((ParticleSystemRenderer)r).renderMode!=ParticleSystemRenderMode.None))),missingBones=model.GetComponentsInChildren')
 source=source.replace('pixels.Add(t.GetPixels32());Destroy(t);','pixels.Add(t.GetPixels32());File.WriteAllBytes(Out+"'+frames+'/"+(string)F(view,"artId")+"-"+((bool)F(view,"preview")?"large":((bool)F(view,"miniature")?"miniature":"small"))+"-early.png",t.EncodeToPNG());Destroy(t);')
 if name=='LongHarness':source=source.replace('new WaitForSecondsRealtime(2);\n            var nodes','new WaitForSecondsRealtime(23);\n            var nodes').replace('new WaitForSecondsRealtime(5)','new WaitForSecondsRealtime(11)')
 (target/'Assets'/(probe+'.cs')).write_text(source);(w/frames).mkdir(exist_ok=True)
cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());allids=sorted({art for c in cat['cards'] for art in c['artIds']});(w/'final-all-ids.json').write_text(json.dumps({'ids':allids}))
scenes={'15720100','15210301','15210601','11780100','14030101','16610101','19450101','13680101','13960101','19250101','19640101','19850101','13230201','11590100','13221601','15660100','13220701','15230711','10450100','20161501','20005601','20800101'}
(w/'final-long-ids.json').write_text(json.dumps({'ids':sorted({art for c in cat['cards'] if c['id'] in scenes for art in c['artIds']})}))
print('Final harnesses prepared',len(allids),'arts')

from pathlib import Path
import shutil, hashlib
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911'); h=w/'OgoImportHarness'
for p in (w/'RuntimeStage').glob('*.cs'):
 d=h/'Assets/DynamicCards/Runtime'/p.name; shutil.copy2(p,d)
 d.with_suffix(d.suffix+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('CompleteMotionSource:'+p.stem).encode()).hexdigest()+'\n')
for src,cohort in [('PostFxShaders','Legacy'),('NativePostFxShaders','Thronebreaker')]:
 d=h/'Assets/DynamicCards/Shaders/SourcePostFx'/cohort; d.mkdir(parents=True,exist_ok=True)
 for p in (w/src).glob('*'): shutil.copy2(p,d/p.name)
d=h/'Assets/DynamicCards/Content/SourcePostFx'; d.mkdir(parents=True,exist_ok=True); shutil.copy2(w/'SourcePostFx/VHS_Static.png',d/'VHS_Static.png')
p=w/'PostFxSourceImport.cs'; s=p.read_text(encoding='utf-8-sig'); marker='var doc=JsonUtility.FromJson<Doc>'
insert='var noiseImporter=(TextureImporter)AssetImporter.GetAtPath("Assets/DynamicCards/Content/SourcePostFx/VHS_Static.png"); noiseImporter.sRGBTexture=true; noiseImporter.alphaIsTransparency=false; noiseImporter.filterMode=FilterMode.Bilinear; noiseImporter.wrapMode=TextureWrapMode.Repeat; noiseImporter.textureCompression=TextureImporterCompression.Uncompressed; noiseImporter.npotScale=TextureImporterNPOTScale.None; noiseImporter.mipmapEnabled=false; noiseImporter.SaveAndReimport();\n        '
if 'var noiseImporter=' not in s: s=s.replace(marker,insert+marker)
p.write_text(s,encoding='utf-8'); shutil.copy2(p,h/'Assets/Editor/PostFxSourceImport.cs')
p=h/'Assets/DynamicCards/Runtime/DynamicCardView.cs'; s=p.read_text(encoding='utf-8-sig'); marker='renderCamera.enabled = false;'
insert='\n            var originalPostEffects = model.GetComponentsInChildren<DynamicCardPostEffect>(true);\n            if (originalPostEffects.Length != 0)\n                cameraObject.AddComponent<DynamicCardPostProcessRenderer>().Effects = originalPostEffects;'
if 'originalPostEffects' not in s: s=s.replace(marker,marker+insert)
p.write_text(s,encoding='utf-8')
print('Staged source post effects and camera bridge')

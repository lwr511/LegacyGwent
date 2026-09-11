from pathlib import Path
import re,hashlib
W=Path(__file__).resolve().parent;out=W/'LightningRuntime';out.mkdir(exist_ok=True)
for name in ['LightningTool','LightningAnimator','LightningToolAnimationListener','AnimationCurveHelper']:
 code=(W/'OriginalCode'/f'{name}.cs').read_text(encoding='utf-8-sig').replace('using RedLogger;\n','').replace('using RedTools;\n','').replace('[ExecuteInEditMode]\n','')
 if 'namespace GwentUnity;' in code:code=code.replace('namespace GwentUnity;','namespace Assets.Script.DynamicCards.SourceLightning\n{')+'\n}\n'
 else:code=code.replace('public static class','namespace Assets.Script.DynamicCards.SourceLightning\n{\npublic static class',1)+'\n}\n'
 code=code.replace('stringBuilder.ResetExt()','stringBuilder.Clear()').replace('Singleton<ShaderStaticRegistry>.Instance.GetShader(EStaticShaderType.Lightning)','SourceShader')
 code=re.sub(r'ShaderKeywordRegistry\.(\w+)',r'"\1"',code)
 code=re.sub(r'RedKit.Logger.LogWarning\(LC.Client, (.*)\);',r'Debug.LogWarning(\1);',code)
 if name=='LightningTool':
  code=code.replace('public Transform ObjectToFace;','public Transform ObjectToFace;\n\tpublic Shader SourceShader;')
  code=re.sub(r'meshFilter.mesh = lightning.Faceing switch\s*\{.*?\};','switch (lightning.Faceing)\n            {\n                case LightningFaceing.Cross: meshFilter.mesh = BakeLightningCross(list, meshHolder3, list2, opacity); break;\n                case LightningFaceing.Camera: meshFilter.mesh = BakeLightningCamFaceing(list, meshHolder3, list2, opacity); break;\n                default: meshFilter.mesh = BakeLightningSingle(list, meshHolder3, list2, opacity); break;\n            }',code,flags=re.S)
 f=out/f'{name}.cs';f.write_text('// Original lightning behavior, with isolated namespace and explicit source shader.\n'+code,encoding='utf-8')
(out/'SourceRange.cs').write_text('namespace Assets.Script.DynamicCards.SourceLightning\n{\n    internal static class SourceRange\n    {\n        internal static float Remap(this float value,float low,float high,float targetLow,float targetHigh)\n        { return (value-low)/(high-low)*(targetHigh-targetLow)+targetLow; }\n    }\n}\n')
for f in out.glob('*.cs'):Path(str(f)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('DynamicCards/SourceLightning/'+f.stem).encode()).hexdigest()+'\n')
for cohort,folder in [('Legacy','PostFxShaderPrograms'),('Thronebreaker','NativePostFxShaderPrograms')]:
 code=(W.parent/'generate_native_shaders.py').read_text(encoding='utf-8').replace('root=Path(__file__).resolve().parent','root=W.parent').replace("output=root/'NativeShaders'",f"output=W/'LightningShaders'/'{cohort}'").replace('output.mkdir(exist_ok=True)','output.mkdir(parents=True,exist_ok=True)').replace("for folder in sorted((root/'ShaderPrograms').iterdir()):",f"for folder in sorted((W/'{folder}').iterdir()):\n    if folder.name != 'Shader_Forge_LightningShader':continue").replace("if '--all' not in sys.argv and 'unicorn_nightmare_cloud' not in folder.name:continue",'').replace('DynamicCards/Native/',f'DynamicCards/SourceLightning/{cohort}/').replace("(root/'native_shader_manifest.json')",f"(W/'lightning-{cohort.lower()}-shader-manifest.json')")
 exec(compile(code,__file__,'exec'),{'W':W,'__file__':__file__})

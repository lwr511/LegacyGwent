from pathlib import Path
import re,hashlib
W=Path(__file__).resolve().parent
code=(W.parent/'generate_native_shaders.py').read_text(encoding='utf-8').replace('root=Path(__file__).resolve().parent','root=W.parent').replace("output=root/'NativeShaders'","output=W/'CandleShaders'").replace("for folder in sorted((root/'ShaderPrograms').iterdir()):","for folder in sorted((W/'NativePostFxShaderPrograms').iterdir()):\n    if folder.name != 'VFX_Effects_Candle_Billboard':continue").replace("if '--all' not in sys.argv and 'unicorn_nightmare_cloud' not in folder.name:continue",'').replace('DynamicCards/Native/','DynamicCards/SourceCandles/').replace("(root/'native_shader_manifest.json')","(W/'candle-shader-manifest.json')")
exec(compile(code,__file__,'exec'),{'W':W,'__file__':__file__})
code=(W/'OriginalCode/CandleFireTool.cs').read_text(encoding='utf-8-sig').replace('using RedLogger;\n','').replace('namespace GwentUnity;','namespace Assets.Script.DynamicCards.SourceParticles\n{').replace('public class CandleFireTool','public class SourceCandleFire').replace('private UpdateTextureOffset TexOffset;','private SourceCandleTextureOffset TexOffset;\n\tpublic Shader SourceShader;')
code=re.sub(r'Shader shader = .*?;', 'Shader shader = SourceShader;',code)
code=re.sub(r'RedKit.Logger.LogError\(LC.Client, (.*)\);',r'Debug.LogError(\1);',code)
code=re.sub(r'ShaderKeywordRegistry\.(\w+)',r'"\1"',code).replace('GetComponent<UpdateTextureOffset>()','GetComponent<SourceCandleTextureOffset>()').replace('AddComponent<UpdateTextureOffset>()','AddComponent<SourceCandleTextureOffset>()').replace('(position.x + position.z).Remap(-1f, 1f, 0f, 1f)','((position.x + position.z + 1f) / 2f)')+'\n}\n'
out=W/'CandleRuntime';out.mkdir(exist_ok=True)
(out/'SourceCandleFire.cs').write_text('// Original card candle mesh, atlas and turbulence behavior.\n'+code,encoding='utf-8')
code=(W/'OriginalCode/UpdateTextureOffset.cs').read_text(encoding='utf-8-sig').replace('public class UpdateTextureOffset','namespace Assets.Script.DynamicCards.SourceParticles\n{\npublic class SourceCandleTextureOffset')+'\n}\n'
(out/'SourceCandleTextureOffset.cs').write_text(code,encoding='utf-8')
for p in out.glob('*.cs'):Path(str(p)+'.meta').write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('DynamicCards/SourceParticles/'+p.stem).encode()).hexdigest()+'\n')

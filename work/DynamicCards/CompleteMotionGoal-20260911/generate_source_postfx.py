from pathlib import Path
import sys
W=Path(__file__).resolve().parent
native='--native' in sys.argv
code=(W.parent/'generate_native_shaders.py').read_text(encoding='utf-8')
code=code.replace('root=Path(__file__).resolve().parent','root=W.parent')
code=code.replace("output=root/'NativeShaders'","output=W/'PostFxShaders'")
code=code.replace("for folder in sorted((root/'ShaderPrograms').iterdir()):","for folder in sorted((W/'PostFxShaderPrograms').iterdir()):\n    if folder.name not in ['Custom_CardPollen_Glitch','Custom_CardPollen_Bloom']:continue")
code=code.replace("if '--all' not in sys.argv and 'unicorn_nightmare_cloud' not in folder.name:continue",'')
code=code.replace('DynamicCards/Native/','DynamicCards/SourcePostFx/Legacy/')
code=code.replace("(root/'native_shader_manifest.json')","(W/'postfx-shader-manifest.json')")
if native:
    code=code.replace("W/'PostFxShaderPrograms'","W/'NativePostFxShaderPrograms'").replace("W/'PostFxShaders'","W/'NativePostFxShaders'")
    code=code.replace('DynamicCards/SourcePostFx/Legacy/','DynamicCards/SourcePostFx/Thronebreaker/').replace("W/'postfx-shader-manifest.json'","W/'native-postfx-shader-manifest.json'")
exec(compile(code,str(W.parent/'generate_native_shaders.py'),'exec'),{'W':W,'__file__':__file__})

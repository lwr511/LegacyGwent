import sys
from pathlib import Path
root=Path(__file__).resolve().parent
sys.argv.append('--all')
if '--generate' not in sys.argv:
    text=(root/'extract_shader_programs.py').read_text().replace("/'ShaderPrograms'","/'LegacyShaderPrograms'")
    text=text.replace('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes')
else:
    text=(root/'generate_native_shaders.py').read_text().replace("'NativeShaders'","'LegacyShaders'").replace("'ShaderPrograms'","'LegacyShaderPrograms'")
    text=text.replace('DynamicCards/Native/','DynamicCards/Legacy/').replace('native_shader_manifest.json','legacy_shader_manifest.json')
exec(compile(text,__file__,'exec'))

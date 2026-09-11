from pathlib import Path
root=Path(__file__).resolve().parent
text=(root/'source_null_renderers.py').read_text().replace('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes').replace("Assets/DynamicCards/Content'","Assets/DynamicCards/Content/Legacy2017'").replace('source_null_renderers.json','legacy_null_renderers.json')
exec(compile(text,__file__,'exec'))

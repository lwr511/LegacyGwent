from pathlib import Path
root=Path(__file__).resolve().parent
text=(root/'extra_effect_assets.py').read_text().replace('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes').replace("'ExtraEffects'","'LegacyExtraEffects'").replace('extra_effect_assets.json','legacy_extra_effect_assets.json')
exec(compile(text,__file__,'exec'))

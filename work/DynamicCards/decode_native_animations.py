from pathlib import Path
root=Path(__file__).resolve().parent
text=(root/'decode_legacy_animations.py').read_text()
text=text.replace("out=root/'LegacyAnimationData'","out=root/'NativeAnimationData'")
text=text.replace("needed=set((root/'legacy_needed.txt').read_text().split())","needed=set(__import__('json').loads((root/'source_effects.json').read_text()))")
text=text.replace('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
exec(compile(text,__file__,'exec'))

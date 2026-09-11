from pathlib import Path
root=Path(__file__).resolve().parent
text=(root/'source_effects_audio.py').read_text().split('# Link by the original event ID')[0]
text=text.replace("BASE=Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets')","BASE=Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets')")
text=text.replace("BASE/'bundledassets/cardassets/scenes'","BASE/'AssetBundles/cardassets/scenes'")
text=text.replace("cards={x['id']:x for x in json.loads((ROOT/'source_catalog.json').read_text())}","cards=json.loads((ROOT/'second_source_inventory.json').read_text())['scenes']")
text=text.replace("ROOT/'source_effects.json'","ROOT/'second_source_effects.json'")
exec(compile(text,__file__,'exec'))

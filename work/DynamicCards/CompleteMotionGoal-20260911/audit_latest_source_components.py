from pathlib import Path
import re
W=Path(__file__).resolve().parent
code=(W/'audit_source_components.py').read_text(encoding='utf-8-sig')
code=re.sub(r"for source,location in \[.*?\]:", "for source,location in [('Latest','C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')]:",code,count=1)
code=code.replace('env=UnityPy.load(location)','env=UnityPy.load(*[str(Path(location)/i) for i in ids])')
code=code.replace("m=re.search(r'\\d{8}',o.assets_file.name)","m=re.search(r'\\d{8}',trpath(o).split('/')[0])")
code=code.replace("W/'source-component-evidence.json'","W/'latest-source-component-evidence.json'")
exec(compile(code,__file__,'exec'),{'__file__':__file__})

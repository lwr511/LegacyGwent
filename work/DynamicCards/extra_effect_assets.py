import sys,json,re,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
env=UnityPy.load(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
out=root/'ExtraEffects';out.mkdir(exist_ok=True)
result={}
for o in env.objects:
    if o.type.name not in ['Texture2D','TextAsset']:continue
    d=o.read();name=d.m_Name
    # These assets are referenced by procedural effect scripts rather than mesh materials.
    if o.type.name=='Texture2D' and any(s in name.lower() for s in ['candle','lightning','emissary']):
        fn=hashlib.sha1((o.assets_file.name+str(o.path_id)).encode()).hexdigest()[:12]+'.png';d.image.save(out/fn);result[name]=fn
    if o.type.name=='TextAsset':
        raw=d.m_Script
        if isinstance(raw,str):raw=raw.encode('utf8',errors='surrogateescape')
        fn=hashlib.sha1((o.assets_file.name+str(o.path_id)).encode()).hexdigest()[:12]+'.bytes';(out/fn).write_bytes(raw);result[name]=fn
        print(name,len(raw),repr(raw[:64]),flush=True)
(root/'extra_effect_assets.json').write_text(json.dumps(result,indent=2))

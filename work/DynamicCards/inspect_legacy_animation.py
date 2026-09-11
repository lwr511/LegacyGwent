import sys,json
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
env=UnityPy.load(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes')
for o in env.objects:
    if '16220301' not in o.assets_file.name:continue
    if o.type.name in ['AnimationClip','AnimatorController']:
        d=o.read_typetree();(root/('legacy_'+o.type.name+'_'+str(o.path_id)+'.json')).write_text(json.dumps(d,indent=2));print(o.type.name,o.path_id,d.get('m_Name'),list(d)[:10])

import sys,json,re
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
base=Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets')
env=UnityPy.load(str(base/'scenes'))
scenes={}
for obj in env.objects:
    match=re.search(r'\d{8}',obj.assets_file.name)
    if not match:continue
    c=scenes.setdefault(match[0],{'particles':0,'version':obj.assets_file.unity_version})
    if obj.type.name=='ParticleSystem':c['particles']+=1
current=json.loads((root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/catalog.json').read_text())['cards']
old={c['id'] for c in current}
result={'source':str(base),'scenes':scenes,'newIds':sorted(set(scenes)-old),'oldOnly':sorted(old-set(scenes))}
(root/'second_source_inventory.json').write_text(json.dumps(result,indent=2))
print('SECOND_SOURCE',len(scenes),'NEW',len(result['newIds']),'VERSIONS',set(c['version'] for c in scenes.values()),flush=True)

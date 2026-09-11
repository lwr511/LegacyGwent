from pathlib import Path
import sys,json
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
r=Path(__file__).resolve().parent
base=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets')
env=UnityPy.Environment();seen=set()
def load(p):
 if p in seen:return
 seen.add(p);env.load_file(str(p))
 manifest=Path(str(p)+'.manifest')
 if manifest.exists():
  for line in manifest.read_text().split('Dependencies:')[-1].splitlines():
   if '/bundledassets/' in line and '/textures/' not in line:load(base/line.split('/bundledassets/')[-1].strip())
load(base/'cardassets/scenes/17380101')
for obj in env.objects:
 if obj.type.name=='AnimationClip':
  d=obj.read_typetree()
  if d['m_Name'].startswith('VFX'):
   (r/('raw_1738_'+d['m_Name']+'_'+str(obj.path_id)+'.json')).write_text(json.dumps(d,indent=2))
   print(obj.assets_file.name,obj.path_id,d['m_Name'],d['m_ClipBindingConstant'],flush=True)

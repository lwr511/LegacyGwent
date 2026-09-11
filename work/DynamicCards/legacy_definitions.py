import sys
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy,zipfile
root=Path(__file__).resolve().parent
base=Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets')
with zipfile.ZipFile(base/'data_definitions') as archive:
    for name in archive.namelist():
        if any(k in name.lower() for k in ['audio','template']):
            (root/('legacy_defs_'+Path(name).name)).write_bytes(archive.read(name));print(name)
env=UnityPy.load(str(base/'AssetBundles/data_definitions'))
for o in env.objects:
    if o.type.name!='TextAsset':continue
    d=o.read()
    if any(k in d.m_Name.lower() for k in ['audio','template']):
        raw=d.m_Script;raw=raw if isinstance(raw,bytes) else raw.encode('utf8',errors='surrogateescape')
        (root/('legacy_defs_'+d.m_Name+'.xml')).write_bytes(raw);print(d.m_Name,len(raw))

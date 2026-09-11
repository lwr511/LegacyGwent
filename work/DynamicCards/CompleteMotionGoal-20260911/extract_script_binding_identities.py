from pathlib import Path
import json,sys,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.classes import PPtr
W=Path(__file__).resolve().parent;rows=[]
for source,location in [('Legacy2017','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),('Thronebreaker','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')]:
    env=UnityPy.load(location)
    for o in env.objects:
        if o.type.name!='AnimationClip':continue
        m=re.search(r'\d{8}',o.assets_file.name)
        if not m:continue
        d=o.read_typetree()
        for b in d.get('m_ClipBindingConstant',{}).get('genericBindings',[]):
            if b['typeID']!=114:continue
            script=b['script'];ref=PPtr(m_FileID=script['m_FileID'],m_PathID=script['m_PathID'],assetsfile=o.assets_file)
            name=ref.read().m_ClassName if ref.path_id else ''
            rows.append({'source':source,'scene':m[0],'clipFileId':o.path_id,'clip':d['m_Name'],'pathHash':b['path'],'attribute':b['attribute'],'script':name})
(W/'script-binding-identities.json').write_text(json.dumps(rows,indent=2));print('SCRIPT_BINDINGS',len(rows))

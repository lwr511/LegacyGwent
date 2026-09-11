from pathlib import Path
import json,sys
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent;out=W/'SourcePostFx';out.mkdir(exist_ok=True);env=UnityPy.load('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes');rows=[]
for o in env.objects:
    if o.type.name!='Texture2D':continue
    d=o.read()
    if d.m_Name!='VHS_Static':continue
    f=out/'VHS_Static.png';d.image.save(f);tree=o.read_typetree();rows.append({'file':str(f),'width':d.m_Width,'height':d.m_Height,'sourceAsset':o.assets_file.name,'sourceId':o.path_id,'colorSpace':tree.get('m_ColorSpace'),'settings':tree['m_TextureSettings']})
(W/'postfx-noise-source.json').write_text(json.dumps(rows,indent=2));print(rows);assert len(rows)==1

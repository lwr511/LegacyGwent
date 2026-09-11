import sys,json,re,hashlib
from pathlib import Path
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
root=Path(__file__).resolve().parent
client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
content=client/'Assets/DynamicCards/Content'
env=UnityPy.load(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
materials={}
for o in env.objects:
    if o.type.name=='Material':materials.setdefault(o.read().m_Name,[]).append(o)
out=content/'OriginalTextures';out.mkdir(exist_ok=True)
done=set();report=[]
for p in content.glob('*/conversion.json'):
    c=json.loads(p.read_text());assign={a['material'] for a in c.get('textureAssignments',[]) if '_MainTex' in a['properties']}
    for info in c['materials']:
        if info['asset'] in done or info['originalName'] in assign or not info['originalName'].startswith('['):continue
        sources=materials.get(info['originalName'],[])
        if not sources:continue
        sources.sort(key=lambda o:c['id'] not in o.assets_file.name)
        source=sources[0].read();ptr=dict(source.m_SavedProperties.m_TexEnvs).get('_MainTex')
        ref='{fileID: 0}';textureName=None
        if ptr is not None and ptr.m_Texture.path_id:
            tex=ptr.m_Texture.deref()
            if tex.type.name!='Texture2D':continue
            data=tex.read();textureName=data.m_Name
            key=hashlib.md5((tex.assets_file.name+str(tex.path_id)).encode()).hexdigest()
            png=out/(key+'.png')
            if not png.exists():data.image.save(png)
            meta=Path(str(png)+'.meta')
            if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+key+'\nTextureImporter:\n  serializedVersion: 10\n  mipmaps:\n    enableMipMap: 1\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 0\n')
            ref='{fileID: 2800000, guid: '+key+', type: 3}'
        target=client/info['asset'];text=target.read_text()
        updated=re.sub(r'(- _MainTex:\s*\n\s*m_Texture: )[^\n]+',lambda m:m[1]+ref,text)
        if updated!=text:
            target.write_text(updated);report.append({'card':c['id'],'material':info['originalName'],'texture':textureName,'asset':info['asset']})
        done.add(info['asset'])
(root/'repaired_source_main_textures.json').write_text(json.dumps(report,indent=2))
print('REPAIRED_MATERIALS',len(report),'CARDS',len({r['card'] for r in report}),flush=True)

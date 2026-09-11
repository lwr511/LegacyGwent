from pathlib import Path
import json,sys,re
sys.stdout.reconfigure(encoding='utf-8')
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent
doc=json.loads((W/'ogo-animation-contracts.json').read_text());results=[]
for source,location in [('Legacy2017','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),('Thronebreaker','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')]:
    records=[r for r in doc['records'] if r['source']==source];ids={r['scene'] for r in records};env=UnityPy.load(location);paths={};cache={}
    def path(o):
        key=(o.assets_file.name,o.path_id)
        if key not in cache:
            t=o.read();cache[key]=(path(t.m_Father.deref())+'/' if t.m_Father.path_id else '')+t.m_GameObject.read().m_Name
        return cache[key]
    for o in env.objects:
        if o.type.name!='Transform':continue
        m=re.search(r'\d{8}',o.assets_file.name)
        if m and m[0] in ids:paths[(m[0],path(o))]=o.read().m_GameObject.read()
    for r in records:
        for proxy in r['proxies']:
            go=paths[(r['scene'],proxy['target'])];renderers=[c.component.read() for c in go.m_Component if c.component.deref().type.name in ['MeshRenderer','SkinnedMeshRenderer','ParticleSystemRenderer']]
            assert len(renderers)==1
            material=renderers[0].m_Materials[proxy['slot']].read();shader=material.m_Shader.read_typetree()
            properties=[p['m_Name'] for p in shader['m_ParsedForm']['m_PropInfo']['m_Props']]
            results.append({'source':source,'scene':r['scene'],**proxy,'originalMaterial':material.m_Name,'originalShader':shader['m_ParsedForm']['m_Name'],'originalHasProperty':proxy['name'] in properties,'originalProperties':properties})
(W/'ogo-original-property-proof.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('PROPERTIES',len(results),'originally absent',[(r['scene'],r['name'],r['originalShader']) for r in results if not r['originalHasProperty']],flush=True)

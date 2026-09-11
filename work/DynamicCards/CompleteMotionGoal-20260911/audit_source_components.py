from pathlib import Path
from collections import defaultdict,Counter
import json,sys,re,zlib
sys.stdout.reconfigure(encoding='utf-8')
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent
suspects=json.loads((W/'binding-suspects-source-values.json').read_text())
results=[]
for source,location in [('Legacy2017','E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),('Thronebreaker','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')]:
    selected=[r for r in suspects if r['source']==source];ids={r['scene'] for r in selected};env=UnityPy.load(location);byid=defaultdict(dict);cache={}
    def trpath(o):
        key=(o.assets_file.name,o.path_id)
        if key in cache:return cache[key]
        d=o.read();name=d.m_GameObject.read().m_Name
        result=(trpath(d.m_Father.deref())+'/' if d.m_Father.path_id else '')+name
        cache[key]=result;return result
    for o in env.objects:
        if o.type.name!='Transform':continue
        m=re.search(r'\d{8}',o.assets_file.name)
        if not m or m[0] not in ids:continue
        byid[m[0]][trpath(o)]=o.read().m_GameObject.deref()
    for row in selected:
        target='/'.join(s for s in [row['animator'],row['path']] if s);go=byid[row['scene']].get(target)
        evidence={'sourceTarget':target,'sourceTargetExists':go is not None}
        if go:
            gd=go.read();components=[c.component.deref() for c in gd.m_Component];evidence['sourceComponentTypes']=[c.type.name for c in components]
            matched=[c for c in components if c.type.value==row['type']];evidence['sourceTargetComponentExists']=bool(matched)
            if row['type']==95 and matched:
                d=matched[0].read();ctrl=d.m_Controller.deref() if d.m_Controller.path_id else None
                if ctrl:
                    co=ctrl.read_typetree();evidence['sourceAnimatorAttributeName']=dict(co['m_TOS']).get(row['attribute'])
            if row['type']==114:
                evidence['sourceScripts']=[]
                for c in matched:
                    d=c.read();evidence['sourceScripts'].append(d.m_Script.read().m_Name if d.m_Script.path_id else '')
            if row['type'] in [23,137,199]:
                evidence['sourceMaterials']=[]
                evidence['sourceMaterialProperties']=[]
                evidence['sourceBlendShapes']=[]
                for c in matched:
                    for mat in c.read().m_Materials:
                        if mat.path_id:
                            try:
                                material=mat.read();evidence['sourceMaterials'].append(material.m_Name)
                                saved=material.m_SavedProperties
                                for kind,items in [('float',saved.m_Floats),('color',saved.m_Colors),('texture',saved.m_TexEnvs)]:
                                    for name,value in items:
                                        attrname=name+'_ST' if kind=='texture' else name
                                        if (zlib.crc32(attrname.encode())&0xfffffff)==(row['attribute']&0xfffffff):
                                            evidence['sourceMaterialProperties'].append({'name':name,'kind':kind,'component':(row['attribute']>>28)&3})
                            except Exception as ex:evidence['sourceMaterials'].append('unresolved:'+str(ex))
                    if row['type']==137:
                        mesh=c.read().m_Mesh
                        if mesh.path_id:
                            md=mesh.read_typetree()
                            for channel in md.get('m_Shapes',{}).get('channels',[]):
                                name=channel['name'];evidence['sourceBlendShapes'].append({'name':name,'hash':zlib.crc32(('blendShape.'+name).encode()),'nameHash':channel.get('nameHash')})
        results.append(dict(row,**evidence))
    print(source,'suspects',len(selected),'absentOriginalComponent',sum(r.get('sourceTargetComponentExists') is False for r in results if r['source']==source),flush=True)
    (W/'source-component-evidence.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('COMPLETE',len(results),flush=True)

from pathlib import Path
import json
root=Path(__file__).resolve().parent
catalog_file=root/'Probe/Assets/DynamicCards/Content/catalog.json'
checkpoint=root/'latest_prepare_catalog_checkpoint.json'
registered={c['prefab'] for c in json.loads(catalog_file.read_text())['cards']}
pending=json.loads(checkpoint.read_text()) if checkpoint.exists() else []
pending=[c for c in pending if c['prefab'] not in registered]
known_prefabs=registered|{c['prefab'] for c in pending}
shader_map={m['source']:m['guid'] for m in json.loads((root/'latest_shader_manifest.json').read_text())}
material_shader_overrides={root/'LatestBridge2022'/m['asset']:shader_map[m['shader']] for p in (root/'LatestBridge2022/Assets/PortableCards').glob('*/conversion.json') for m in json.loads(p.read_text())['materials'] if m['shader'] in shader_map}
text=(root/'prepare_content.py').read_text()
text=text.replace("BRIDGE=ROOT/'Bridge2022'","BRIDGE=ROOT/'LatestBridge2022'")
text=text.replace("DEST=CLIENT/'Assets/DynamicCards/Content'","DEST=CLIENT/'Assets/DynamicCards/Content/Latest'")
text=text.replace("ROOT/'source_effects.json'","ROOT/'latest_source_effects.json'")
text=text.replace("ROOT/'source_audio.json'","ROOT/'latest_audio.json'").replace("ROOT/'Audio'","ROOT/'LatestAudio'")
text=text.replace("ROOT/'extra_effect_assets.json'","ROOT/'latest_extra_effect_assets.json'").replace("ROOT/'ExtraEffects'","ROOT/'LatestExtraEffects'")
text=text.replace("matches=json.loads((ROOT/'art_matches.json').read_text())","matches=[dict(sourceId=c['id'],candidates=[dict(artId=a,mse=0) for a in c['artIds']]) for c in json.loads((ROOT/'latest_source_matches.json').read_text())]")
start=text.index('done=set()');end=text.index('copied=set()',start)
text=text[:start]+"done={p.parent.name for p in (BRIDGE/'Assets/PortableCards').glob('*/conversion.json')}\n"+text[end:]
text=text.replace("text=re.sub(r'm_Shader: \\{[^\\n]+\\}', 'm_Shader: {fileID: 0}',text)","pass # Keep the Unity 2019 native shader dependency.")
text=text.replace("'Assets/DynamicCards/Content/'","'Assets/DynamicCards/Content/Latest/'")
text=text.replace("(DEST/'catalog.json').write_text(json.dumps(dict(version=1,cards=catalog),indent=2),encoding='utf8')", "catalog_path=DEST.parent/'catalog.json'\nexisting=json.loads(catalog_path.read_text())\nexisting['cards']=[c for c in existing['cards'] if c['prefab'] not in {v['prefab'] for v in catalog}]+catalog\ncatalog_path.write_text(json.dumps(existing,indent=2),encoding='utf8')")
text=text.split("if (ROOT/'shader_rendering.json').exists():")[0]
text=text.replace("ROOT/'content_inventory.json'","ROOT/'latest_content_inventory.json'")
for old,new in [('m_textureName','m_TextureName'),('m_matIndex','m_MatIndex'),('m_xOffsetMulti','m_XOffsetMulti'),('m_yOffsetMulti','m_YOffsetMulti')]:text=text.replace("d['"+old+"']","d['"+new+"']")
import sys
text=text.replace("CLIENT=ROOT.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'","CLIENT=ROOT/'Probe'")
text=text.replace("done={p.parent.name for p in (BRIDGE/'Assets/PortableCards').glob('*/conversion.json')}","done={p.parent.name for p in (BRIDGE/'Assets/PortableCards').glob('*/conversion.json') if not (DEST/p.parent.name/'conversion.json').exists()}\n"+"done &= set(sys.argv[sys.argv.index('--cards')+1].split(',')) if '--cards' in sys.argv else done")
text=text.replace("endswith('_'+ident)","endswith('_'+ident[:-2]+'00')").replace("glob('*_'+ident+'.png')","glob('*_'+ident[:-2]+'00'+'.png')")
text=text.replace("if not (DEST/p.parent.name/'conversion.json').exists()","if 'Assets/DynamicCards/Content/Latest/'+p.parent.name+'/Card.prefab' not in known_prefabs")
text=text.replace('catalog=[]','catalog=list(pending)')
text=text.replace('catalog.append(c)',"catalog.append(c)\n    checkpoint.write_text(json.dumps(catalog,indent=2))\n    print('LATEST_PREPARED',ident,flush=True)")
exec(compile(text,__file__,'exec'))
# Preserve the original handler's per-property atlas assignments, including non-MainTex slots.
for path in DEST.glob('*/conversion.json'):
    data=json.loads(path.read_text())
    if not data.get('atlas'):continue
    meta=Path(str(CLIENT/data['atlas'])+'.meta').read_text()
    atlas_guid=re.search(r'^guid: (\w+)',meta,re.M)[1]
    mapping={}
    for material in data['materials']:
        mapping.setdefault(material['originalName'],[]).append(CLIENT/material['asset'])
    for assignment in data['textureAssignments']:
        assigned_texture = assignment.get('texture') or data['atlas']
        assigned_meta = Path(str(CLIENT/assigned_texture)+'.meta').read_text()
        assigned_guid = re.search(r'^guid: (\w+)', assigned_meta, re.M)[1]
        for material in mapping.get(assignment['material'],[]):
            body=material.read_text()
            for prop in assignment['properties']:
                body=re.sub(r'(- '+re.escape(prop)+r':\s*\n\s*m_Texture: )[^\n]+',lambda m:m[1]+'{fileID: 2800000, guid: '+assigned_guid+', type: 3}',body)
            material.write_text(body)

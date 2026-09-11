from pathlib import Path
import json,re,subprocess
r=Path(__file__).resolve().parent;w=r.parent;probe=w/'Probe'
plan=json.loads((r/'fallback-plan.json').read_text(encoding='utf8'))
master=probe/'Assets/DynamicCards/Content/catalog.json'
doc=json.loads(master.read_text());(r/'catalog-before-fallback.json').write_bytes(master.read_bytes())
for kind,bridge,prefix in [('Native','Bridge2022','native'),('Legacy2017','LegacyBridge2019','legacy')]:
    selected={v['id']:v for v in plan if v['kind']==kind}
    audio_dir=w/('LegacyAudio' if kind=='Legacy2017' else 'Audio')
    audio_map=json.loads((w/('legacy_audio.json' if kind=='Legacy2017' else 'source_audio.json')).read_text())
    for ident in selected:
        for item in audio_map.get(ident,{}).get('files',[]):
            wav=audio_dir/item['file']
            if not wav.exists():
                subprocess.run([r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/vgmstream/vgmstream-cli.exe','-o',str(wav),str(wav.with_suffix('.wem'))],check=True,stdout=subprocess.DEVNULL)
    shader_map={m['source']:m['guid'] for m in json.loads((w/(prefix+'_shader_manifest.json')).read_text())}
    material_shader_overrides={w/bridge/m['asset']:shader_map[m['shader']] for ident in selected for m in json.loads((w/bridge/'Assets/PortableCards'/ident/'conversion.json').read_text())['materials'] if m['shader'] in shader_map}
    text=(w/'prepare_content.py').read_text()
    text=text.replace("BRIDGE=ROOT/'Bridge2022'",f"BRIDGE=ROOT/'{bridge}'")
    text=text.replace("CLIENT=ROOT.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'","CLIENT=ROOT/'Probe'")
    text=text.replace("DEST=CLIENT/'Assets/DynamicCards/Content'",f"DEST=CLIENT/'Assets/DynamicCards/Content/Fallback/{kind}'")
    text=text.replace("'Assets/DynamicCards/Content/'",f"'Assets/DynamicCards/Content/Fallback/{kind}/'")
    start=text.index('done=set()');end=text.index('copied=set()',start)
    text=text[:start]+f'done={set(selected)!r}\n'+text[end:]
    text=text.split("if (ROOT/'shader_rendering.json').exists():")[0]
    text=text.replace("ROOT/'content_inventory.json'",f"ROOT/'AnimationRegression/{kind}-inventory.json'")
    if kind=='Legacy2017':
        text=text.replace("ROOT/'source_effects.json'","ROOT/'second_source_effects.json'").replace("ROOT/'source_audio.json'","ROOT/'legacy_audio.json'").replace("ROOT/'Audio'","ROOT/'LegacyAudio'").replace("ROOT/'extra_effect_assets.json'","ROOT/'legacy_extra_effect_assets.json'").replace("ROOT/'ExtraEffects'","ROOT/'LegacyExtraEffects'")
    ns=dict(__file__=str(w/'prepare_content.py'),material_shader_overrides=material_shader_overrides)
    exec(compile(text,'prepare_selected_fallbacks','exec'),ns)
    dest=ns['DEST']; cards=ns['catalog']
    for c in cards:c['artIds']=[v['art'] for v in plan if v['kind']==kind and v['id']==c['id']]
    # Restore atlas property assignments exactly as in the original import pipeline.
    for path in dest.glob('*/conversion.json'):
        data=json.loads(path.read_text())
        if not data.get('atlas'):continue
        guid=re.search(r'^guid: (\w+)',Path(str(probe/data['atlas'])+'.meta').read_text(),re.M)[1]
        mats={m['originalName']:probe/m['asset'] for m in data['materials']}
        for assignment in data['textureAssignments']:
            mat=mats.get(assignment['material'])
            if mat is None:continue
            body=mat.read_text()
            for prop in assignment['properties']:body=re.sub(r'(- '+re.escape(prop)+r':\s*\n\s*m_Texture: )[^\n]+',lambda m:m[1]+'{fileID: 2800000, guid: '+guid+', type: 3}',body)
            mat.write_text(body)
    doc['cards']+=cards
byid={c['id']:c for c in doc['cards']}
for row in plan:
    if row['kind']=='Latest':byid[row['id']]['artIds'].append(row['art'])
master.write_text(json.dumps(doc,indent=2),encoding='utf8')
# Apply the existing camera/UV/jiggle converter only to restored entries.
text=(w/'prepare_controllers.py').read_text()
text=text.replace("catalog=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/catalog.json'","catalog=root/'Probe/Assets/DynamicCards/Content/catalog.json'")
text=text.replace("effects=json.loads((root/'source_effects.json').read_text())","effects=json.loads((root/'source_effects.json').read_text());effects.update(json.loads((root/'second_source_effects.json').read_text()))")
text=text.replace("for card in data['cards']:\n","for card in data['cards']:\n    if '/Fallback/' not in card['prefab']:continue\n")
ns=dict(__file__=str(w/'prepare_controllers.py'));exec(compile(text,'fallback_controllers','exec'),ns)
doc=ns['data'];effects=ns['effects']
for card in doc['cards']:
    if '/Fallback/' not in card['prefab']:continue
    card['initialTransforms']=[dict(path=s['path'],position=s['data']['localPos'],rotation=s['data']['localRot'],scale=s['data']['localScl']) for s in effects[card['id']]['scripts'] if s['type']=='OnStartTransformModification' and s['data'].get('m_Enabled',1)]
master.write_text(json.dumps(doc,indent=2),encoding='utf8')
print('FALLBACK_PREPARED',sum('/Fallback/' in c['prefab'] for c in doc['cards']),flush=True)

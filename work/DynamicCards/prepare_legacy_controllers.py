from pathlib import Path
root=Path(__file__).resolve().parent
text=(root/'prepare_controllers.py').read_text()
text=text.replace("root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/catalog.json'", "root/'Probe/Assets/DynamicCards/Content/catalog.json'")
text=text.replace("for card in data['cards']:\n", "for card in data['cards']:\n    if '/Legacy2017/' not in card['prefab']:continue\n")
text=text.replace("effects=json.loads((root/'source_effects.json').read_text())","effects=json.loads((root/'source_effects.json').read_text());effects.update(json.loads((root/'second_source_effects.json').read_text()))")
exec(compile(text,__file__,'exec'))
for card in data['cards']:
    if '/Legacy2017/' not in card['prefab']:continue
    card['initialTransforms']=[dict(path=s['path'],position=s['data']['localPos'],rotation=s['data']['localRot'],scale=s['data']['localScl']) for s in effects[card['id']]['scripts'] if s['type']=='OnStartTransformModification' and s['data'].get('m_Enabled',1)]
    for script in effects[card['id']]['scripts']:
        if script['type']=='RocheAnimationEvents':
            event=next((e for e in effects[card['id']]['events'] if e['functionName']=='RocheGroupSwitch'),None)
            if event:card.update(beforeCut=script['references']['firstGroup'],afterCut=script['references']['secoundGroup'],cutTime=event['time'])
catalog.write_text(json.dumps(data,indent=2),encoding='utf8')

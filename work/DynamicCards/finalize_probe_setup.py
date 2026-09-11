from pathlib import Path
import json, shutil
base=Path(__file__).resolve().parent
primary=base.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
probe=base/'Probe'
content=probe/'Assets/DynamicCards/Content'
catalog=json.loads((content/'catalog.json').read_text())
moved=0
for card in catalog['cards']:
    if '/Legacy2017/' not in card['prefab']:continue
    source=content/card['id']/'Generated'
    target=probe/Path(card['prefab']).parent/'Generated'
    if not source.exists():continue
    source.resolve().relative_to(content.resolve());target.resolve().relative_to((content/'Legacy2017').resolve())
    if target.exists():raise RuntimeError('Generated target already exists: '+str(target))
    shutil.move(str(source),str(target));moved+=1
    meta=source.with_name(source.name+'.meta')
    if meta.exists():shutil.move(str(meta),str(target)+'.meta')
for p in (primary/'Assets/DynamicCards/Runtime').iterdir():
    if p.is_file() and not p.name.startswith('DynamicCardSettingRow'):
        shutil.copy2(p,probe/'Assets/DynamicCards/Runtime'/p.name)
shutil.copy2(primary/'Assets/DynamicCards/Editor/DynamicCardContentImporter.cs',probe/'Assets/DynamicCards/Editor/DynamicCardContentImporter.cs')
print('FINAL_PROBE_SETUP moved_generated',moved)

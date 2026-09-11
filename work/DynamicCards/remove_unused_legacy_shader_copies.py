import json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/Legacy2017'
root=root.resolve();references=set()
for p in root.rglob('*.mat'):references.update(re.findall(r'guid: ([a-f0-9]{32})',p.read_text()))
removed=[]
for p in root.rglob('*.asset'):
    with p.open('rb') as f:header=f.read(256)
    if b'--- !u!48 ' not in header:continue
    meta=Path(str(p)+'.meta')
    match=re.search(r'^guid: (\w+)',meta.read_text(),re.M)
    if match and match[1] not in references:
        assert p.resolve().is_relative_to(root) and meta.resolve().is_relative_to(root)
        removed.append(str(p));p.unlink();meta.unlink()
(Path(__file__).resolve().parent/'removed_unused_legacy_shaders.json').write_text(json.dumps(removed,indent=2))
print('REMOVED_UNREFERENCED_CONVERSION_SHADERS',len(removed))

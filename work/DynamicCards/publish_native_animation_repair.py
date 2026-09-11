from pathlib import Path
import json,shutil,hashlib
root=Path(__file__).resolve().parent
source=root/'Probe/Assets/DynamicCards/Content'
target=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content'
backup=root/'BeforeNativeAnimationRepair';backup.mkdir(exist_ok=True)
changes=[]
for folder in sorted(source.iterdir()):
 if not folder.is_dir() or not folder.name.isdigit():continue
 for path in folder.iterdir():
  if not(path.name.startswith(('Source_','Global_Source_')) or path.name in ['Card.prefab','Card.prefab.meta']):continue
  relative=path.relative_to(source);dest=target/relative
  if dest.exists():
   if dest.read_bytes()==path.read_bytes():continue
   saved=backup/relative;saved.parent.mkdir(parents=True,exist_ok=True)
   if not saved.exists():shutil.copy2(dest,saved)
  dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest);changes.append(str(relative))
 print('NATIVE_PUBLISHED',folder.name,flush=True)
catalog=target/'catalog.json';saved=backup/'catalog.json'
if not saved.exists():shutil.copy2(catalog,saved)
doc=json.loads(catalog.read_text());repairs={c['id']:c for c in json.loads((source/'catalog.json').read_text())['cards'] if '/Latest/' not in c['prefab'] and '/Legacy2017/' not in c['prefab']}
doc['cards']=[repairs.get(c['id'],c) if '/Latest/' not in c['prefab'] and '/Legacy2017/' not in c['prefab'] else c for c in doc['cards']]
catalog.write_text(json.dumps(doc,indent=2))
(root/'native_animation_publish.json').write_text(json.dumps(changes,indent=2));print('NATIVE_PUBLISH_DONE',len(changes),flush=True)

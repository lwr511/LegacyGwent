from pathlib import Path
import json,shutil
p=Path.cwd(); w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity'); src=w/'Restore'; backup=w.parent/'BeforeOldSources-20260908/Content'
cat=p/'Assets/DynamicCards/Content/catalog.json'
shutil.copy2(cat,w/'catalog-before-supplement.json')
entries=json.loads((w/'restore-entries.json').read_text()); data=json.loads(cat.read_text(encoding='utf-8-sig')); ids={c['id'] for c in data['cards']}; arts={a for c in data['cards'] for a in c['artIds']}
assert not any(a in arts for c in entries for a in c['artIds'])
ready=p/'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready'
if ready.exists(): shutil.copy2(ready,w/'ready-before-supplement');ready.unlink()
for file in src.rglob('*'):
 if file.is_file():
  dest=p/file.relative_to(src);dest.parent.mkdir(parents=True,exist_ok=True)
  if dest.exists():assert dest.read_bytes()==file.read_bytes(),str(dest)
  else:shutil.copy2(file,dest)
for c in entries:
 ident=c['id'];c['sourceVersion']='Latest';c['sourceId']=ident
 if ident in ids:c['id']='latest-'+ident
 conv=backup/'Latest'/ident/'conversion.json'
 if conv.exists():shutil.copy2(conv,p/'Assets/DynamicCards/Content/Latest'/ident/'conversion.json')
data['cards']+=entries
cat.write_text(json.dumps(data,indent=2),encoding='utf-8')
print('Installed',len(entries),'scenes;',len(arts)+sum(len(c['artIds']) for c in entries),'mapped arts',flush=True)

from pathlib import Path
import re,json,shutil
from concurrent.futures import ThreadPoolExecutor
root=Path(__file__).resolve().parent;source=root/'Probe';target=root/'FocusedProbe'
selected={('Native','10090100'),('Native','10080100')}|{('Latest',s) for s in ['10080101','10090101','10130101','10400101','14740101','14840101']}|{('Legacy',s) for s in ['20023501','20027501','11310101','11210701']}
def kind(c):return 'Latest' if '/Latest/' in c['prefab'] else 'Legacy' if '/Legacy2017/' in c['prefab'] else 'Native'
data=json.loads((source/'Assets/DynamicCards/Content/catalog.json').read_text());data['cards']=[c for c in data['cards'] if (kind(c),c['id']) in selected]
def read_meta(p):
 m=re.search(r'^guid: ([a-f0-9]{32})',p.read_text(encoding='utf8'),re.M)
 return (m[1],Path(str(p)[:-5])) if m else None
with ThreadPoolExecutor(max_workers=12) as pool:index=dict(x for x in pool.map(read_meta,(source/'Assets/DynamicCards').rglob('*.meta')) if x)
copied=set()
def copy(p):
 if p in copied:return
 copied.add(p);out=target/p.relative_to(source);out.parent.mkdir(parents=True,exist_ok=True)
 shutil.copy2(p,out)
 if Path(str(p)+'.meta').exists():shutil.copy2(str(p)+'.meta',str(out)+'.meta')
 if p.suffix in ['.prefab','.mat','.controller','.asset']:
  for guid in set(re.findall(r'guid: ([a-f0-9]{32})',p.read_text(encoding='utf8'))):
   if guid in index:copy(index[guid])
for c in data['cards']:
 c['artIds'].append('verify-'+kind(c)+'-'+c['id'])
 prefab=source/c['prefab'];copy(prefab);copy(prefab.parent/'conversion.json')
 if c['audio']:copy(source/c['audio'])
 metadata=json.loads((prefab.parent/'conversion.json').read_text())
 if metadata.get('atlas'):copy(source/metadata['atlas'])
 for material in metadata['materials']:copy(source/material['asset'])
for folder in ['ProjectSettings','Packages','Assets/DynamicCards/Runtime','Assets/DynamicCards/Shaders']:
 shutil.copytree(source/folder,target/folder,dirs_exist_ok=True)
for file in ['DynamicCardContentImporter.cs','DynamicCardBuild.cs','DynamicCardEditorCache.cs']:
 copy(source/'Assets/DynamicCards/Editor'/file)
for file in ['GeraltSmoke.cs','GeraltTimelineSmoke.cs']:copy(source/'Assets'/file)
for file in ['GeraltSmokeEditor.cs','PremiumValidationEditor.cs']:copy(source/'Assets/Editor'/file)
(target/'Assets/DynamicCards/Content/catalog.json').write_text(json.dumps(data,indent=2))
(target/'Assets/Editor/FocusedEditor.cs').write_text('using UnityEditor; public static class FocusedEditor { public static void Timeline(){Assets.Script.DynamicCards.Editor.DynamicCardContentImporter.Prepare(); PremiumValidationEditor.Timeline();} public static void Visual(){Assets.Script.DynamicCards.Editor.DynamicCardContentImporter.Prepare();GeraltSmokeEditor.Run();} }')
print('FOCUSED_PROBE_PREPARED',len(data['cards']),len(copied),flush=True)

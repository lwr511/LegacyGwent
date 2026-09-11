from pathlib import Path
import json,time,shutil,subprocess,hashlib
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity');p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');src=p/'Library/DynamicCardsBundles/StandaloneWindows64';dest=w/'Harness/Library/DynamicCardsBundles/StandaloneWindows64';result=w/'main-build-result.txt'
while True:
 if result.exists():
  message=result.read_text()
  if not message.startswith('OK '):raise RuntimeError(message)
  if (src/'cards.bundle.editor-ready').exists():break
 time.sleep(5)
index=json.loads((src/'cards.index.json').read_text());names=['cards.bundle','cards.index.json','cards.bundle.editor-ready']+[x['file'] for x in index['parts']];report=[]
for name in names:
 assert Path(name).name==name
 target=dest/name
 if target.exists():target.unlink() # Keep immutable baseline files hard-linked in SkyHarness intact.
 shutil.copy2(src/name,target)
 def digest(path):
  with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
 sha=digest(target);assert sha==digest(src/name);report.append({'file':name,'bytes':target.stat().st_size,'sha256':sha})
shutil.copy2(p/'Assets/DynamicCards/Content/catalog.json',w/'Harness/Assets/DynamicCards/Content/catalog.json')
(w/'post-package-hashes.json').write_text(json.dumps(report,indent=2))
proc=subprocess.Popen(['C:/Program Files/Unity/Editor/Unity.exe','-batchmode','-projectPath',str(w/'Harness'),'-executeMethod','PostCatalogProbe.Run','-logFile',str(w/'post-all.log'),'-screen-width','640','-screen-height','480'],creationflags=subprocess.CREATE_NO_WINDOW)
(w/'post-launch.json').write_text(json.dumps({'pid':proc.pid,'payloadBytes':sum(x['bytes'] for x in report),'copiedFiles':len(names)}));print('Started post regression',proc.pid,flush=True)

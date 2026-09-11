from pathlib import Path
import json,shutil,hashlib,subprocess,os
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');src=p/'Library/DynamicCardsBundles/StandaloneWindows64'
assert (w/'main-build-result.txt').read_text().startswith('OK')
assert (src/'cards.bundle.editor-ready').exists()
index=json.loads((src/'cards.index.json').read_text());names=['cards.bundle','cards.index.json']+[x['file'] for x in index['parts']];report=[]
def digest(path):
 with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
for name in names:
 target=w/'FinalHarness/Library/DynamicCardsBundles/StandaloneWindows64'/name;target.parent.mkdir(parents=True,exist_ok=True)
 if target.exists():target.unlink()
 shutil.copy2(src/name,target);sha=digest(target);assert sha==digest(src/name);report.append({'file':name,'bytes':target.stat().st_size,'sha256':sha})
 other=w/'LongHarness/Library/DynamicCardsBundles/StandaloneWindows64'/name;other.parent.mkdir(parents=True,exist_ok=True)
 if other.exists():other.unlink()
 os.link(target,other)
for harness in ['FinalHarness','LongHarness']:
 shutil.copy2(src/'cards.bundle.editor-ready',w/harness/'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready')
(w/'final-package-hashes.json').write_text(json.dumps(report,indent=2));launch=[]
for harness,method,log in [('FinalHarness','FinalCatalogProbe.Run','final-all.log'),('LongHarness','LongCatalogProbe.Run','final-long.log')]:
 proc=subprocess.Popen(['C:/Program Files/Unity/Editor/Unity.exe','-batchmode','-projectPath',str(w/harness),'-executeMethod',method,'-logFile',str(w/log),'-screen-width','640','-screen-height','480'],creationflags=subprocess.CREATE_NO_WINDOW);launch.append({'harness':harness,'pid':proc.pid})
(w/'final-launch.json').write_text(json.dumps(launch,indent=2));print(launch,'payloadBytes',sum(x['bytes'] for x in report),flush=True)

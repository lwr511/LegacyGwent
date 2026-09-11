from pathlib import Path
import json,hashlib,base64,shutil,subprocess
w=Path(__file__).resolve().parent
p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
assert (w/'main-build-result.txt').read_text().startswith('OK ')
b=p/'Library/DynamicCardsBundles/StandaloneWindows64';h=w/'FinalHarness/Library/DynamicCardsBundles/StandaloneWindows64'
for name in ['cards.bundle','cards.bundle.editor-ready','cards.bundle.editor-files.json']:
 target=h/name
 if target.exists():target.unlink()
 shutil.copy2(b/name,target)
old=json.loads((w/'package-hashes-before-edge-repair.json').read_text());rows=[]
for row in old:
 a=b/row['file'];c=h/row['file'];sha=hashlib.sha256(a.read_bytes()).hexdigest();assert hashlib.sha256(c.read_bytes()).hexdigest()==sha
 rows.append(dict(file=row['file'],bytes=a.stat().st_size,sha256=sha))
changed=[x['file'] for x,y in zip(rows,old) if x['sha256']!=y['sha256']];assert changed==['cards.bundle'],changed
(w/'final-package-hashes.json').write_text(json.dumps(rows,indent=2))
(w/'final-delivery-verification.json').write_text(json.dumps(dict(files=len(rows),allSha256Match=True,bytes=sum(x['bytes'] for x in rows),editorReady=(b/'cards.bundle.editor-ready').exists(),changedAfterFullRegression=changed,unchangedSceneParts=25),indent=2))
m=json.loads((b/'cards.bundle.editor-files.json').read_text());before={r['path']:r for r in json.loads((w/'manifest-before-edge-repair.json').read_text())['files']};changes=[]
for row in m['files']:
 if row['hash']==before[row['path']]['hash']:continue
 a=p/row['path'];assert base64.b64encode(hashlib.sha256(a.read_bytes()).digest()).decode()==row['hash'];changes.append(row['path'])
assert changes==['Assets/DynamicCards/Content/catalog.json'],changes
(w/'final-source-manifest-update.json').write_text(json.dumps(dict(files=len(m['files']),unchangedHashes=len(m['files'])-len(changes),changedFilesVerified=changes,baseline='final-source-manifest-verification.json',allMatch=True),indent=2))
rows=[]
for name in [r['file'] for r in json.loads((w/'final-runtime-source-hashes.json').read_text())['files']]:
 a=p/'Assets/DynamicCards/Runtime'/name
 c=next((w/'FinalHarness/Assets').rglob(a.name));assert a.read_bytes()==c.read_bytes(),a.name
 rows.append(dict(file=a.name,sha256=hashlib.sha256(a.read_bytes()).hexdigest()))
(w/'final-runtime-source-hashes.json').write_text(json.dumps(dict(allMatch=True,files=rows),indent=2))
for name in ['final-edge-results.json','final-edge-progress.json']:
 a=w/name
 if a.exists():shutil.copy2(a,w/('before-thumbnail-'+name));a.unlink()
proc=subprocess.Popen(['C:/Program Files/Unity/Editor/Unity.exe','-batchmode','-projectPath',str(w/'FinalHarness'),'-executeMethod','FinalEdgeRegression.Run','-logFile',str(w/'final-edge.log')],creationflags=subprocess.CREATE_NO_WINDOW)
print('Final edge test PID',proc.pid,flush=True)

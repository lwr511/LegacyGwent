from pathlib import Path
import json,hashlib,base64,time
p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity');f=p/'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-files.json';d=json.loads(f.read_text());total=0;begin=time.time()
for row in d['files']:
 source=p/row['path'];before=source.stat()
 with source.open('rb') as stream:actual=base64.b64encode(hashlib.file_digest(stream,'sha256').digest()).decode()
 after=source.stat();assert before.st_size==after.st_size and before.st_mtime_ns==after.st_mtime_ns,row['path'];assert actual==row['hash'],row['path'];row['length']=after.st_size;row['ticks']=621355968000000000+after.st_mtime_ns//100;total+=after.st_size
# Verify only: do not rewrite the final build manifest.
report={'files':len(d['files']),'bytes':total,'seconds':time.time()-begin,'allSha256Match':True};(w/'final-source-manifest-verification.json').write_text(json.dumps(report,indent=2));print(report,flush=True)

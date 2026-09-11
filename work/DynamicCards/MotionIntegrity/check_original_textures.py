from pathlib import Path
import json,hashlib,base64
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity');d=json.loads((w/'before-source-manifest.json').read_text());checked=0;changed=[]
for row in d['files']:
 path=Path(row['path'])
 if not (row['path'].endswith('.png') or row['path'].endswith('.png.meta')):continue
 with path.open('rb') as stream:actual=base64.b64encode(hashlib.file_digest(stream,'sha256').digest()).decode()
 checked+=1
 if actual!=row['hash']:changed.append(row['path'])
(w/'original-texture-preservation.json').write_text(json.dumps({'checked':checked,'changed':changed},indent=2));print('Original textures/importers',checked,'changed',len(changed),flush=True)

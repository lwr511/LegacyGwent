from pathlib import Path
import hashlib,base64,json,os,time
p=Path.cwd();out=p/'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-files.json';files=[]
for root in ['Assets/DynamicCards/Content','Assets/DynamicCards/Shaders']:
 for f in (p/root).rglob('*'):
  if not f.is_file() or (f.suffix=='.meta' and Path(str(f)[:-5]).is_dir()):continue
  with f.open('rb') as s:h=hashlib.file_digest(s,'sha256').digest()
  files.append(dict(path=f.relative_to(p).as_posix(),hash=base64.b64encode(h).decode()))
Path(str(out)+'.tmp').write_text(json.dumps(dict(files=files),separators=(',',':')));os.replace(str(out)+'.tmp',out)
print('MANIFEST',len(files))

from pathlib import Path
import hashlib,base64,json,os
p=Path.cwd();root=p/'Library/DynamicCardsBundles/StandaloneWindows64';out=root/'cards.bundle.editor-files.json';files=[];reused=0;hashed=0
cache={x['path']:x for x in json.loads((root/'cards.source-hashes.json').read_text())['files']}
for assetRoot in ['Assets/DynamicCards/Content','Assets/DynamicCards/Shaders']:
 for f in (p/assetRoot).rglob('*'):
  if not f.is_file() or (f.suffix=='.meta' and Path(str(f)[:-5]).is_dir()):continue
  name=f.relative_to(p).as_posix();s=f.stat();entry=cache.get(name)
  # Reuse the same dependency digest verified by the just-completed bundle build.
  if entry and entry['length']==s.st_size and entry['ticks']==621355968000000000+s.st_mtime_ns//100:h=entry['hash'];reused+=1
  else:
   with f.open('rb') as stream:h=base64.b64encode(hashlib.file_digest(stream,'sha256').digest()).decode()
   hashed+=1
  files.append(dict(path=name,hash=h))
Path(str(out)+'.tmp').write_text(json.dumps(dict(files=files),separators=(',',':')));os.replace(str(out)+'.tmp',out)
Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards\TimingRepair\manifest-result.json').write_text(json.dumps(dict(files=len(files),reused=reused,hashed=hashed)))

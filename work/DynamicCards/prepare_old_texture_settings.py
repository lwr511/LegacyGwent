from pathlib import Path
import re,struct,json
W=Path(__file__).resolve().parent;S=W/'OldSourcesStage'; changed=0
for meta in (S/'Assets/DynamicCards/Content/Old').rglob('*.png.meta'):
 body=meta.read_text(encoding='utf-8-sig')
 if not re.search(r'^  textureType: 0$',body,re.M):continue
 with Path(str(meta)[:-5]).open('rb') as f:header=f.read(24)
 if len(header)!=24 or min(struct.unpack('>II',header[16:24]))<4:continue
 pattern=r'  - serializedVersion: \d+\n    buildTarget: Standalone\n.*?(?=  - serializedVersion:|  spriteSheet:)'
 match=re.search(pattern,body,re.S)
 if not match:
  default=re.search(pattern.replace('Standalone','DefaultTexturePlatform'),body,re.S)
  if not default:raise RuntimeError('No platform settings '+str(meta))
  body=body[:default.end()]+default[0].replace('DefaultTexturePlatform','Standalone')+body[default.end():]
  match=re.search(pattern,body,re.S)
 block=match[0]
 for field,value in [('textureFormat',29),('compressionQuality',80),('crunchedCompression',1),('overridden',1)]:
  block=re.sub(r'(    '+field+r': )-?\d+',lambda m:m[1]+str(value),block)
 new=body[:match.start()]+block+body[match.end():]
 if new!=body:meta.write_text(new,encoding='utf-8');changed+=1
(S/'texture-settings.json').write_text(json.dumps(dict(changed=changed,format='DXT5Crunched',quality=80)));print('TEXTURE_SETTINGS',changed,flush=True)

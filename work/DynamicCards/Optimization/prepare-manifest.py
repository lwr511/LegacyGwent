from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib,base64,json,re,subprocess
r=Path(__file__).resolve().parent
repo=r.parents[2]
main=repo/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
probe=r.parent/'Probe'
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
    return base64.b64encode(h.digest()).decode()
files=[p for name in ['Content','Shaders'] for p in (main/'Assets/DynamicCards'/name).rglob('*') if p.is_file() and not (p.suffix=='.meta' and Path(str(p)[:-5]).is_dir())]
def entry(p):
    before=p.stat();hash_value=digest(p);after=p.stat()
    assert before.st_mtime_ns==after.st_mtime_ns and before.st_size==after.st_size,'File changed during verification: '+str(p)
    return dict(path=p.relative_to(main).as_posix(),hash=hash_value,length=after.st_size,ticks=621355968000000000+after.st_mtime_ns//100)
result=[]
with ThreadPoolExecutor(max_workers=6) as pool:
    for i,item in enumerate(pool.map(entry,files),1):
        result.append(item)
        if i%10000==0:print('MANIFEST_HASHED',i,'/',len(files),flush=True)
(r/'editor-files.json').write_text(json.dumps(dict(files=result)),encoding='utf8')
(r/'source-hashes.json').write_text(json.dumps(dict(files=result)),encoding='utf8')
old=json.loads((probe/'Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-files.json').read_text())
old={x['path']:x['hash'] for x in old['files']}
differences=[]
for item in result:
    path=item['path']
    if '/Latest/' not in path and '/Shaders/' not in path:continue
    if item['hash']!=old.get(path) and (not (probe/path).is_file() or digest(probe/path)!=item['hash']):differences.append(path)
unused=[]
known={'Assets/DynamicCards/Shaders/SourcePortableCard.shader','Assets/DynamicCards/Shaders/SourcePortableCard.shader.meta','Assets/DynamicCards/Shaders/Native/Unlit_Transparent.shader','Assets/DynamicCards/Shaders/Native/Unlit_Transparent.shader.meta'}
if differences and set(differences)==known:
    guids=[re.search(r'^guid: (\w+)',(main/p).read_text(),re.M)[1] for p in differences if p.endswith('.meta')]
    search=subprocess.run(['rg','--no-ignore','-q','|'.join(guids),str(main/'Assets/DynamicCards/Content/Latest'),'-g','*.mat','-g','*.prefab','-g','*.controller'])
    names=['DynamicCards/Compatibility/PortableCard','DynamicCards/Native/Unlit_Transparent']
    runtime='\n'.join(p.read_text(encoding='utf8') for p in (main/'Assets/DynamicCards/Runtime').glob('*.cs'))
    fallback='\n'.join(line for p in (main/'Assets/DynamicCards/Shaders').rglob('*.shader') for line in p.read_text(encoding='utf8').splitlines() if 'Fallback' in line or 'UsePass' in line)
    if search.returncode==1 and not any(n in runtime or n in fallback for n in names):
        unused=differences;differences=[]
(r/'source-equivalence.json').write_text(json.dumps(dict(files=len(result),differentBuildInputs=differences,unusedModuleFilesAbsentFromProbe=unused),indent=2))
assert not differences,'Build-project inputs differ: '+str(differences[:10])
print('MANIFEST_READY_AND_BUILD_INPUTS_MATCH',len(result),flush=True)

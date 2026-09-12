"""Verify each imported skin slot against the original mesh bone-path hashes."""
from pathlib import Path
from collections import Counter
import sys,json,re,struct,zlib,time
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import yaml
W=Path(__file__).resolve().parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');C=P/'Assets/DynamicCards/Content'
catalog=json.loads((C/'catalog.json').read_text(encoding='utf-8-sig'))['cards']
meshpaths={};cache={};errors=[];issues=[];stats=Counter();rows=[];started=time.time()
for f in C.rglob('*.asset.meta'):
    m=re.search(r'^guid: (\w+)',f.read_text(),re.M)
    if m:
        if m[1] in meshpaths:errors.append(dict(problem='duplicate-guid',files=[str(meshpaths[m[1]]),str(f)]))
        meshpaths[m[1]]=Path(str(f)[:-5])
def refs(b,name):
    m=re.search(r'  '+name+r':([^\n]*)(.*?)(?=\n  \w+:)',b,re.S)
    return re.findall(r'fileID: (-?\d+)',m[0]) if m else []
def ref(b,name):
    m=re.search(r'  '+name+r': \{fileID: (-?\d+)',b);return m[1] if m else None
def hashes(guid):
    if guid not in cache:
        f=meshpaths[guid]
        value=None
        with f.open(encoding='utf-8-sig') as stream:
            for line in stream:
                if line.startswith('  m_BoneNameHashes:'):
                    v=line.partition(':')[2].strip();value=[] if v in ('','[]') else list(struct.unpack('<'+'I'*(len(v)//8),bytes.fromhex(v)));break
        cache[guid]=value
    return cache[guid]
for card in catalog:
    file=P/card['prefab']
    try:
        text=file.read_text(encoding='utf-8-sig')
        blocks={i:(int(t),b) for t,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',text,re.S)}
        names={}
        for i,(t,b) in blocks.items():
            if t!=1:continue
            match=re.search(r'^  m_Name: (.*(?:\n    [^\n]+)*)',b,re.M)
            value=match[1] if match else ''
            names[i]=yaml.safe_load(value) if value.startswith(('"',"'")) else ' '.join(s.strip() for s in value.splitlines())
        transforms={i:(ref(b,'m_GameObject'),ref(b,'m_Father')) for i,(t,b) in blocks.items() if t in (4,224)}
        paths={}
        def path(i):
            if i not in paths:
                go,parent=transforms[i];paths[i]=(path(parent)+'/' if parent!='0' else '')+names[go]
            return paths[i]
        for i,(ty,b) in blocks.items():
            if ty!=137:continue
            m=re.search(r'  m_Mesh: \{fileID: -?\d+, guid: (\w+)',b)
            if not m:continue
            hs=hashes(m[1]);bones=refs(b,'m_Bones');skin=names.get(ref(b,'m_GameObject'),'')
            stats['skins']+=1
            if not hs:stats['withoutPathHashes']+=1;continue
            stats['hashedSkins']+=1
            if len(hs)!=len(bones):issues.append(dict(scene=card['id'],file=str(file),skin=skin,problem='bone-count',hashes=len(hs),bones=len(bones)))
            for slot,(h,bone) in enumerate(zip(hs,bones)):
                stats['slots']+=1
                if bone=='0':issues.append(dict(scene=card['id'],file=str(file),skin=skin,slot=slot,hash=h,problem='null-bone'));continue
                parts=path(bone).split('/');suffixes=['/'.join(parts[n:]) for n in range(len(parts))]
                if h in [zlib.crc32(s.encode()) for s in suffixes]:stats['exactPathHashMatches']+=1
                else:issues.append(dict(scene=card['id'],file=str(file),skin=skin,slot=slot,hash=h,path=path(bone),problem='path-hash-mismatch'))
        stats['scenes']+=1
    except Exception as exc:errors.append(dict(scene=card['id'],file=str(file),problem=str(exc)))
    if stats['scenes']%50==0:
        print(dict(stats),'issues',len(issues),'errors',len(errors),flush=True)
result=dict(complete=True,elapsedSeconds=time.time()-started,totals=dict(stats),issues=issues,errors=errors)
(W/'skin-path-hash-audit.json').write_text(json.dumps(result,indent=2))
print('COMPLETE',dict(stats),'issues',len(issues),'errors',len(errors),flush=True)

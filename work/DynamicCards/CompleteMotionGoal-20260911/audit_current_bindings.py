from pathlib import Path
from collections import Counter, defaultdict
import json, re, zlib, mmap, sys, yaml
sys.stdout.reconfigure(encoding='utf-8')
W=Path(__file__).resolve().parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards']
def crc(s): return zlib.crc32(s.encode()) if s else 0
def val(body,key,default=''):
    m=re.search(r'^  '+key+r': (.*)$',body,re.M);return m[1].strip() if m else default
def ref(body,key):
    m=re.search(r'\b'+key+r': \{fileID: (-?\d+)',body);return m[1] if m else None
def blocks(text):return {i:(ty,b) for ty,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',text,re.S)}
def prefab_paths(file):
    objs=blocks(file.read_text(encoding='utf-8-sig'))
    def scalar(s):return yaml.safe_load(s) if s.startswith(('"',"'")) else s
    names={i:scalar(val(b,'m_Name')) for i,(ty,b) in objs.items() if ty=='1'}
    trs={i:(ref(b,'m_GameObject'),ref(b,'m_Father')) for i,(ty,b) in objs.items() if ty=='4'}
    def path(i):
        go,pa=trs[i];return (path(pa)+'/' if pa!='0' else '')+names[go]
    full={i:path(i) for i in trs};root=next(s for i,s in full.items() if trs[i][1]=='0')
    paths={s[len(root)+1:]:trs[i][0] for i,s in full.items()}
    go_paths={go:path for path,go in paths.items()}; animators=[]
    for i,(ty,b) in objs.items():
        if ty!='95':continue
        g=re.search(r'm_Controller: \{fileID: \d+, guid: (\w+)',b)
        animators.append({'path':go_paths.get(ref(b,'m_GameObject')),'guid':g[1] if g else None,'enabled':val(b,'m_Enabled')})
    return paths,animators
def source_anchor(paths,source):
    if source in paths:return source
    parts=source.split('/');found=[]
    for p in paths:
        if not p or p.split('/')[-1]!=parts[-1]:continue
        k=0
        for v in p.split('/'):
            if k<len(parts) and v==parts[k]:k+=1
        if k==len(parts):found.append(p)
    found.sort(key=lambda p:len(p.split('/')))
    if found and (len(found)==1 or len(found[0].split('/'))<len(found[1].split('/'))):return found[0]
def clip_bindings(f):
    with f.open('rb') as stream:
        with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as b:
            start=b.find(b'  m_ClipBindingConstant:');end=b.find(b'    pptrCurveMapping:',start)
            section=b[start:end if end>=0 else start+2000000]
            bindings={tuple(int(x) for x in row) for row in re.findall(rb'path: (\d+)\r?\n\s+attribute: (\d+)\r?\n\s+script: [^\n]+\n\s+typeID: (\d+)',section)}
            length=re.search(rb'm_StopTime: ([-\d.eE+]+)',b)
            return bindings,float(length[1]) if length else None
issues=[];rows=[];totals=Counter();optional_absent=[]
for source,data,subfolder in [('Legacy2017','LegacyAnimationData','Old/Legacy2017'),('Thronebreaker','NativeAnimationData','Old/Thronebreaker'),('Latest','LatestAnimationData','Latest')]:
    records=json.loads((W.parent/data/'animations.json').read_text(encoding='utf-8-sig'))['animators']
    byid=defaultdict(list)
    for r in {(r['id'],r['path']):r for r in records}.values():byid[r['id']].append(r)
    for c in catalog:
        if '/'+subfolder+'/' not in c['prefab']:continue
        folder=(P/c['prefab']).parent;paths,anims=prefab_paths(folder/'Card.prefab')
        for r in byid[c.get('sourceId') or c['id']]:
            if not r['clips']:continue
            prefix='Source_'+format(crc(r['path']),'08x');controller=folder/(prefix+'.controller');global_root=False
            if not controller.exists():prefix='Global_'+prefix;controller=folder/(prefix+'.controller');global_root=True
            base={'source':source,'scene':c['id'],'animator':r['path']}
            if not controller.exists():issues.append(dict(base,problem='missing-controller'));continue
            guid=re.search(r'^guid: (\w+)',Path(str(controller)+'.meta').read_text(),re.M)[1]
            attached=[a for a in anims if a['guid']==guid]
            if len(attached)!=1:issues.append(dict(base,problem='controller-attachment',attached=attached));continue
            anchor=attached[0]['path'];expected_anchor='' if global_root else source_anchor(paths,r['path'])
            if anchor!=expected_anchor:issues.append(dict(base,problem='anchor-mismatch',actual=anchor,expected=expected_anchor))
            if attached[0]['enabled']!='1':issues.append(dict(base,problem='animator-disabled'))
            relative={p[len(anchor)+1:] if anchor else p for p in paths if not anchor or p.startswith(anchor+'/')};relative.add('')
            totals['controllers']+=1
            for cl in {cl['file']:cl for cl in r['clips']}.values():
                name=''.join(ch if ch.isalnum() or ch in '_-' else '_' for ch in cl['name']);f=folder/(prefix+'_'+name+'.anim')
                cb=dict(base,clip=cl['name']); totals['expectedClips']+=1
                if not f.exists():issues.append(dict(cb,problem='missing-clip'));continue
                actual,length=clip_bindings(f); totals['foundClips']+=1
                if abs(length-cl['duration'])>.003:issues.append(dict(cb,problem='clip-duration',sourceLength=cl['duration'],actualLength=length))
                matched=Counter()
                for t in cl['tracks']:
                    tp=t['path'];kind=t.get('typeId',0);attr=t['attribute'];target=tp
                    if global_root:
                        original=source_anchor(paths,r['path']);target='/'.join(s for s in [original,tp] if s)
                        if target not in relative and not t.get('optional'):
                            candidates=[p for p in paths if p.split('/')[-1]==tp.split('/')[-1]]
                            if len(candidates)==1:target=candidates[0]
                    if target not in relative:
                        suffix=[p for p in relative if p.endswith('/'+target)]
                        if len(suffix)==1:target=suffix[0]
                    if target not in relative and not t.get('optional'):
                        leaf=[p for p in relative if p.split('/')[-1]==target.split('/')[-1]]
                        if len(leaf)==1:target=leaf[0]
                    if target not in relative:
                        if t.get('optional') or tp=='InfoHolderRoot' or tp.startswith('InfoHolderRoot/'):
                            matched['sourceAuxiliaryAbsent']+=1
                            optional_absent.append(dict(cb,path=tp,optional=bool(t.get('optional'))))
                        else:issues.append(dict(cb,problem='missing-track-target',path=tp))
                        continue
                    target_type=4 if kind==0 else kind
                    target_attr=2883525743 if kind==198 and attr==2181258151 else attr
                    if (crc(target),target_attr,target_type) in actual:matched['exact']+=1;continue
                    # Material and replacement MonoBehaviour property names differ from source hashes.
                    # Preserve these as explicit suspects for property-level review, not silent passes.
                    candidates=[(a,ty) for ph,a,ty in actual if ph==crc(target)]
                    issues.append(dict(cb,problem='track-binding-not-exact',path=tp,target=target,attribute=attr,type=kind,actualCandidates=candidates))
                rows.append(dict(cb,file=str(f),length=length,sourceTracks=len(cl['tracks']),actualBindings=len(actual),**matched))
                totals.update(matched)
        if len(rows)%100<5:print(source,c['id'],'clips',totals['foundClips'],'issues',len(issues),flush=True)
    print('FINISHED',source,dict(totals), 'issues',len(issues),flush=True)
    (W/'current-binding-audit.json').write_text(json.dumps({'complete':False,'totals':dict(totals),'issues':issues,'clips':rows,'sourceAuxiliaryAbsent':optional_absent},indent=2),encoding='utf-8')
(W/'current-binding-audit.json').write_text(json.dumps({'complete':True,'totals':dict(totals),'issues':issues,'clips':rows,'sourceAuxiliaryAbsent':optional_absent},indent=2),encoding='utf-8')
print('COMPLETE',dict(totals),dict(Counter(i['problem'] for i in issues)),flush=True)

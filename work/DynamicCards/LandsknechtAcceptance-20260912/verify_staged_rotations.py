from pathlib import Path
import sys,json,re,mmap,time,hashlib,ctypes
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import numpy as np,yaml
W=Path(__file__).resolve().parent;D=W.parent
audit=json.loads((W/'all-rotation-audit.json').read_text());originals={r['file']:r for r in audit['clips']}
metadata={}
for data in ['LegacyAnimationData','NativeAnimationData','LatestAnimationData']:
    for record in json.loads((D/data/'animations.json').read_text())['animators']:
        for c in record['clips']:metadata[(str(D/data/c['file']),record['path'],c['name'])]=c
def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as stream:
        for b in iter(lambda:stream.read(8*1024*1024),b''):h.update(b)
    return h.hexdigest()
def parse(text):
    section=text.split('  m_RotationCurves:',1)[1].split('  m_CompressedRotationCurves:',1)[0];out={}
    for block in re.split(r'(?m)^  - curve:',section)[1:]:
        path=yaml.safe_load(block[block.index('    path:'):])['path'] or '';assert path not in out
        times=np.array([float(v) for v in re.findall(r'\btime: ([^\n]+)',block)],dtype=np.float32).astype(float)
        vectors=np.array([[float(v) for v in re.findall(r'[xyzw]: ([^,}]+)',s)] for s in re.findall(r'(?:value|inSlope|outSlope): \{([^}]+)\}',block)],dtype=np.float32).astype(float).reshape(-1,3,4)
        out[path]=(times,vectors)
    return out
def evaluate(keys,samples):
    times,vec=keys
    if len(times)==1:return np.broadcast_to(vec[0,0],(len(samples),4))
    ix=np.clip(np.searchsorted(times,samples,side='right')-1,0,len(times)-2);a,b=vec[ix],vec[ix+1];dt=(times[ix+1]-times[ix])[:,None];t=(samples-times[ix])[:,None]/dt
    return (2*t**3-3*t**2+1)*a[:,0]+(t**3-2*t**2+t)*dt*a[:,2]+(-2*t**3+3*t**2)*b[:,0]+(t**3-t**2)*dt*b[:,1]
def angle(a,b):
    a=a/np.linalg.norm(a,axis=1,keepdims=True);b=b/np.linalg.norm(b,axis=1,keepdims=True)
    return np.degrees(2*np.arccos(np.clip(abs(np.sum(a*b,axis=1)),0,1)))
watch=len(sys.argv)>1
handle=None
if watch:
    api=ctypes.windll.kernel32;api.OpenProcess.restype=ctypes.c_void_p;api.GetExitCodeProcess.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)];api.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=api.OpenProcess(0x1000,False,int(sys.argv[1]));assert handle,'Repair process is no longer running'
prior=W/'staged-rotation-verification.json'
report=json.loads(prior.read_text()) if prior.exists() else dict(complete=False,clips=[],errors=[])
finalization=W/'binding-metadata-finalization.json'
finalized={r['original']:r for r in json.loads(finalization.read_text())['changes']} if finalization.exists() else {}
if finalized:
    report['errors']=[r for r in report['errors'] if r['original'] not in finalized]
    report['complete']=False
seen={r['original'] for r in report['clips']+report['errors']}
try:
    while True:
        log=(W/'rotation-repair-engine.jsonl').read_text();entries=list({r['original']:r for r in [json.loads(s) for s in log[:log.rfind('\n')+1].splitlines()]}.values())
        for entry in entries:
            if entry['original'] in seen:continue
            seen.add(entry['original'])
            try:
                original=Path(entry['original']);stage=Path(entry['staged']);row=originals[str(original)];c=metadata[(row['data'],row['animator'],row['clip'])]
                assert digest(original)==entry['beforeHash'],'Main source changed after staging'
                expectedHash=entry['afterHash']
                if entry['original'] in finalized:
                    fix=finalized[entry['original']]
                    assert fix['beforeHash']==expectedHash and fix['staged']==entry['staged'],'Finalization does not match engine-verified file'
                    expectedHash=fix['afterHash']
                assert digest(stage)==expectedHash,'Staged file changed after engine verification or recorded metadata finalization'
                assert Path(str(original)+'.meta').read_bytes()==Path(str(stage)+'.meta').read_bytes(),'Asset GUID changed'
                before=original.read_text();after=stage.read_text()
                fields=['m_EulerCurves','m_PositionCurves','m_ScaleCurves','m_FloatCurves','m_PPtrCurves','m_SampleRate','m_ClipBindingConstant','m_AnimationClipSettings','m_EditorCurves']
                for a,b in zip(fields,fields[1:]):
                    assert before.split('  '+a+':',1)[1].split('  '+b+':',1)[0]==after.split('  '+a+':',1)[1].split('  '+b+':',1)[0],'Unrelated serialized section changed: '+a
                assert before.split('  m_RotationCurves:',1)[0]==after.split('  m_RotationCurves:',1)[0],'Clip identity or header changed'
                for marker in ['  m_Events:']:
                    if marker in before:assert before.split(marker,1)[1]==after.split(marker,1)[1],'Animation events changed'
                actual=parse(after);values=np.fromfile(row['data'],dtype='<f4').reshape(c['frames'],c['columns']).astype(float)
                samples=(np.arange(c['frames'],dtype=np.float32)*np.float32(np.float32(c['duration'])/max(1,c['frames']-1))).astype(float)
                peak=0;count=0;unresolved=[]
                for t in c['tracks']:
                    if t.get('typeId',0)!=0 or t['attribute']!=2:continue
                    target=t['path']
                    if target not in actual:
                        candidates=[k for k in actual if k.endswith('/'+target) or k.split('/')[-1]==target.split('/')[-1]]
                        if len(candidates)==1:target=candidates[0]
                    if target not in actual:unresolved.append(t['path']);continue
                    error=float(np.max(angle(values[:,t['offset']:t['offset']+4],evaluate(actual[target],samples))))
                    peak=max(peak,error);count+=1
                assert peak<=.0501,('Residual rotation deviation',peak)
                report['clips'].append(dict(original=str(original),staged=str(stage),comparedRotationTracks=count,maxErrorDegrees=peak,unresolved=unresolved,preservedOtherSections=True,preservedGuid=True,afterHash=expectedHash,beforeHash=entry['beforeHash']))
            except Exception as exc:report['errors'].append(dict(original=entry['original'],problem=str(exc)))
            if len(seen)%20==0:print('VERIFIED',len(report['clips']),'errors',len(report['errors']),flush=True)
            report['complete']=(W/'rotation-repair-complete.txt').exists() and len(seen)==len(json.loads((W/'rotation-repair-jobs.json').read_text())['jobs'])
            prior.write_text(json.dumps(report,indent=2))
        if report['complete'] or not watch:break
        status=ctypes.c_ulong();ok=api.GetExitCodeProcess(handle,ctypes.byref(status))
        if not ok or status.value!=259:
            report['engineExitCode']=status.value;prior.write_text(json.dumps(report,indent=2));break
        time.sleep(5)
finally:
    if handle:api.CloseHandle(handle)
print('DONE',len(report['clips']),'errors',len(report['errors']),'complete',report['complete'],flush=True)

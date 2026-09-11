from pathlib import Path
import sys,json,re,mmap,gc
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).resolve().parent
expected={}
for kind in ['legacy','native','latest']:
 ns={};exec(compile((w/f'audit_{kind}_loops.py').read_text(),str(w/f'audit_{kind}_loops.py'),'exec'),ns)
 for file,loop in ns['seen'].items():
  with open(file,'rb') as stream:
   with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as data:
    name=re.search(rb'^  m_Name: (.+)',data,re.M)[1].decode().strip().strip('"');length=float(re.search(rb'm_StopTime: ([-\d.eE+]+)',data)[1])
  row={'loop':loop,'length':length,'file':file};assert name not in expected or expected[name]==row,(name,expected.get(name),row);expected[name]=row
root=w/'FinalHarness/Library/DynamicCardsBundles/StandaloneWindows64';idx=json.loads((root/'cards.index.json').read_text());found=set();issues=[];checked=0
for part in idx['parts']:
 env=UnityPy.load(str(root/part['file']))
 for obj in env.objects:
  if obj.type.name!='AnimationClip':continue
  d=obj.read_typetree();name=d['m_Name']
  if name not in expected:continue
  found.add(name);checked+=1;e=expected[name];m=d['m_MuscleClip'];length=m['m_StopTime']-m['m_StartTime']
  if m['m_LoopTime']!=e['loop'] or abs(length-e['length'])>0.002:issues.append({'part':part['file'],'name':name,'actualLoop':m['m_LoopTime'],'actualLength':length,'expected':e})
 del env;gc.collect()
 print('PACKED_ANIMATION',part['file'],'matched',len(found),'issues',len(issues),flush=True)
report={'complete':True,'expected':len(expected),'matched':len(found),'checkedIncludingSharedCopies':checked,'missing':sorted(set(expected)-found),'issues':issues};(w/'final-packed-animation-contracts.json').write_text(json.dumps(report,indent=2));print(report,flush=True)

from pathlib import Path
import re,json
w=Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionRepair-20260910');cat=json.load(open('Assets/DynamicCards/Content/catalog.json'))['cards'];rows=[]
for id in ['13210201','16310101','16550101','19850101','16740101']:
 c=next(c for c in cat if c['id']==id);p=Path(c['prefab']);body=p.read_text();objs={i:(ty,b) for ty,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',body,re.S)}
 names={i:re.search(r'  m_Name: (.*)',b)[1] for i,(ty,b) in objs.items() if ty=='1'};trs={i:(re.search(r'm_GameObject: \{fileID: (-?\d+)',b)[1],re.search(r'm_Father: \{fileID: (-?\d+)',b)[1]) for i,(ty,b) in objs.items() if ty=='4'}
 def path(i):
  go,pa=trs[i];return (path(pa)+'/' if pa!='0' else '')+names[go]
 paths={go:path(i) for i,(go,pa) in trs.items()};metas={re.search(r'^guid: (\w+)',m.read_text(),re.M)[1]:Path(str(m)[:-5]) for m in p.parent.glob('*.meta') if re.search(r'^guid: (\w+)',m.read_text(),re.M)}
 for i,(ty,b) in objs.items():
  if ty!='95':continue
  go=re.search(r'm_GameObject: \{fileID: (-?\d+)',b)[1];anchor=paths[go];ref=re.search(r'm_Controller: \{fileID: \d+, guid: (\w+)',b)
  if not ref:print(id,'NO_CONTROLLER',anchor);continue
  ctrl=metas[ref[1]];bindingPaths={x[len(anchor)+1:] for x in paths.values() if x.startswith(anchor+'/')}|{''}
  for guid in set(re.findall(r'm_Motion: \{fileID: \d+, guid: (\w+)',ctrl.read_text())):
   clip=metas[guid];cp=set(re.findall(r'(?m)^    path: (.*)$',clip.read_text()));missing=sorted(cp-bindingPaths);row={'scene':id,'anchor':anchor,'controller':str(ctrl),'clip':str(clip),'pathCount':len(cp),'missing':missing};rows.append(row);print(id,anchor.split('/')[-1],clip.name,'paths',len(cp),'missing',len(missing),missing[:3])
(w/'target-binding-audit.json').write_text(json.dumps(rows,indent=2))

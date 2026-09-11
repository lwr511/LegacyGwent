from pathlib import Path
import re,json
w=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity');p=Path.cwd();cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());report=[]
for folder,source in [('LegacyAnimationData','Legacy2017'),('NativeAnimationData','Thronebreaker'),('LatestAnimationData','Latest')]:
 f=w.parent/folder/'animations.json'
 if not f.exists():continue
 data=json.loads(f.read_text()); records=data['animators']
 for c in cat['cards']:
  if ('/'+source+'/') not in c['prefab']:continue
  recs=[r for r in records if r['id']==(c.get('sourceId') or c['id'])]
  if not any(t.get('optional') for r in recs for cl in r['clips'] for t in cl['tracks']):continue
  body=(p/c['prefab']).read_text();objs={i:(ty,b) for ty,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',body,re.S)}
  names={i:re.search(r'  m_Name: (.*)',b)[1] for i,(ty,b) in objs.items() if ty=='1'}
  trs={i:(re.search(r'm_GameObject: \{fileID: (-?\d+)',b)[1],re.search(r'm_Father: \{fileID: (-?\d+)',b)[1]) for i,(ty,b) in objs.items() if ty=='4'}
  def path(i):
   go,pa=trs[i];return (path(pa)+'/' if pa!='0' else '')+names[go]
  paths=[path(i) for i in trs]
  for r in recs:
   anchors=[a for a in paths if a.endswith('/'+r['path']) or a==r['path']]
   if len(anchors)!=1:continue
   anchor=anchors[0];relative={a[len(anchor)+1:] for a in paths if a.startswith(anchor+'/')}
   matched=[]
   for t in {t['path']:t for cl in r['clips'] for t in cl['tracks'] if t.get('optional')}.values():
    if t['path'] in relative:continue
    candidates=[a for a in relative if a.endswith('/'+t['path'])]
    if len(candidates)==1:matched.append({'source':t['path'],'target':candidates[0]})
   if matched:report.append({'source':source,'scene':c['id'],'artIds':c['artIds'],'tracks':matched})
(w/'avatar-wrapper-audit.json').write_text(json.dumps(report,indent=2));print([(r['source'],r['scene'],len(r['tracks'])) for r in report],flush=True)

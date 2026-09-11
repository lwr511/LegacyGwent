exec(open(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/SourceFraming-20260910/find_latest_bundles.py').read().split('rows=[]')[0])
rows=[]
for p in [base/'dependencies/shared/global',base/'dependencies/shared/gameplay']:
 env=UnityPy.load(str(p))
 for o in env.objects:
  if o.type.name!='Transform':continue
  d=o.read();name=d.m_GameObject.deref().read().m_Name
  chain=[];t=d
  while t.m_Father.path_id:
   t=t.m_Father.deref().read();chain.append(t.m_GameObject.deref().read().m_Name)
  if not any('renderer' in n.lower() or 'cardrt' in n.lower() for n in [name]+chain):continue
  go=d.m_GameObject.deref().read();components=[]
  for c in go.m_Component:
   ob=c.component.deref()
   if ob.type.name=='Camera':components.append({'type':'Camera','data':ob.read_typetree()})
   elif ob.type.name=='MonoBehaviour':
    v=ob.read(check_read=False)
    try:typ=v.m_Script.deref().read().m_ClassName
    except:typ='?'
    try:components.append({'type':typ,'data':ob.read_typetree()})
    except:pass
  row={'bundle':str(p.relative_to(base)),'file':o.assets_file.name,'id':o.path_id,'name':name,'parents':chain,'transform':o.read_typetree(),'components':components}
  rows.append(row);print(name,row['transform']['m_LocalPosition'],chain,[c['type'] for c in components],flush=True)
Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/SourceFraming-20260910/latest-renderer-hierarchy.json').write_text(json.dumps(rows,indent=2))

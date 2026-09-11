exec(open(r'C:/UnityProjects/LegacyGwent/work/DynamicCards/SourceFraming-20260910/find_latest_bundles.py').read().split('rows=[]')[0])
for p in [base/'scenes/globalscene',base.parent.parent/'resources.assets']:
 env=UnityPy.load(str(p))
 for o in env.objects:
  if o.type.name!='GameObject':continue
  d=o.read()
  if not any(x in d.m_Name.lower() for x in ['renderer','camera','appearance','appereance']):continue
  print(p.name,o.path_id,d.m_Name)
  for c in d.m_Component:
   ob=c.component.deref()
   if ob.type.name in ['Transform','Camera']:print(ob.type.name,str(ob.read_typetree())[:2200])
   elif ob.type.name=='MonoBehaviour':
    v=ob.read(check_read=False)
    try:print('script',v.m_Script.deref().read().m_ClassName)
    except:pass
    try:print(str(ob.read_typetree())[:3000])
    except:pass

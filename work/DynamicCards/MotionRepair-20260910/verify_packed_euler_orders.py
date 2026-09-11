from pathlib import Path
import sys,json
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps');import UnityPy
w=Path(__file__).parent;cache=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Library/DynamicCardsBundles/StandaloneWindows64');rows=[]
for filename,controller in [('cards-legacy2017-000.bundle','Source_c64138d8'),('cards-legacy2017-002.bundle','Source_0d11c551'),('cards-latest-001.bundle','Source_5f2f4868')]:
 env=UnityPy.load(str(cache/filename));controllers=[o for o in env.objects if o.type.name=='AnimatorController' and o.read().m_Name==controller];assert len(controllers)==1,(filename,controller,len(controllers));c=controllers[0].read()
 for ref in c.m_AnimationClips:
  clip=ref.read_typetree();e=[b for b in clip['m_ClipBindingConstant']['genericBindings'] if b['typeID']==4 and b['attribute']==4];row={'part':filename,'controller':controller,'clip':clip['m_Name'],'eulerBindings':len(e),'orders':sorted(set(b['customType'] for b in e))};rows.append(row);print(row,flush=True)
(w/'packed-euler-orders.json').write_text(json.dumps(rows,indent=2),encoding='utf-8');assert all(r['orders']==[0] for r in rows),rows

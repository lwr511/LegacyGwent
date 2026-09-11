from pathlib import Path
import json, sys
sys.stdout.reconfigure(encoding='utf-8')
W = Path(__file__).resolve().parent
OLD = W.parent/'MotionIntegrity'
P = Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
catalog = json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards']
summaries = []
for source, data, subfolder in [('Legacy2017','LegacyAnimationData','Old/Legacy2017'),('Thronebreaker','NativeAnimationData','Old/Thronebreaker'),('Latest','LatestAnimationData','Latest')]:
    ids = {c['id'] for c in catalog if '/'+subfolder+'/' in c['prefab']}
    records = json.loads((W.parent/data/'animations.json').read_text(encoding='utf-8-sig'))['animators']
    for kind in ['controllers','loops']:
        template = (OLD/f'audit_legacy_{kind}.py').read_text(encoding='utf-8')
        lines = template.splitlines()
        lines[2] = "w=W;p=P;" + ('issues=[];checked=0' if kind=='controllers' else 'rows=[];missing=[];checked=0;seen={}')
        code = '\n'.join(lines).replace('Assets/DynamicCards/Content/Old/Legacy2017','Assets/DynamicCards/Content/'+subfolder).replace("w/'legacy-", "w/'"+source.lower()+"-").replace('.read_text()', ".read_text(encoding='utf-8-sig')")
        ns = dict(W=W,P=P,ids=ids,records=records)
        print(source,kind, 'catalog scenes',len(ids),flush=True)
        exec(compile(code,str(OLD/f'audit_legacy_{kind}.py'),'exec'),ns)
        summaries.append({'source':source,'kind':kind,'catalogScenes':len(ids),'checked':ns['checked'],'issues':len(ns.get('issues',ns.get('rows',[]))),'missing':len(ns.get('missing',[]))})
(W/'current-contract-summary.json').write_text(json.dumps(summaries,indent=2),encoding='utf-8')

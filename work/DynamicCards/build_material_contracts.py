from pathlib import Path
import json
root=Path(__file__).resolve().parent
for kind,filename in [('Native','source_effects.json'),('Legacy','second_source_effects.json'),('Latest','latest_source_effects.json')]:
 rows=[]
 for id,c in json.loads((root/filename).read_text(encoding='utf8')).items():
  scripts={s['path']:s for s in c['scripts'] if s['type']=='MatPropertyAnimate'}
  for item in c['scripts']:
   if item['type'] not in ['MaterialAnimator','CardAppearanceMaterialAnimator','UniversalMaterialAnimator']:continue
   refs=item['references'];targets=[refs[k] for k in refs if k in ['m_Renderer','m_SkinnedRenderer','m_MeshRenderer'] or k.startswith('AssignedRenderers.') or k.startswith('m_AdditionalRenderers.')];targets=list(dict.fromkeys(t for t in targets if t))
   for key,path in refs.items():
    if not key.startswith('Properties.') or path not in scripts:continue
    d=scripts[path]['data'];rows.append(dict(id=id,path=path,targets=targets,Slot=item['data'].get('MatSlot',0),**{k:d[k] for k in ['Type','Name','m_Color','m_Float','m_Vector','m_Offset','m_Scale'] if k in d}))
 folder=root/(kind+'AnimationData');folder.mkdir(exist_ok=True);(folder/'materials.json').write_text(json.dumps(dict(properties=rows),indent=2));print(kind,len(rows))

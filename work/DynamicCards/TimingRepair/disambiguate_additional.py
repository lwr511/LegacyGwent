from pathlib import Path
import json,re,sys,shutil
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.classes import PPtr
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');report=[]
def path(tr):
 d=tr.read();return (path(d.m_Father.deref())+'/' if d.m_Father.path_id else '')+d.m_GameObject.deref().read().m_Name
for kind,ident,src in [('Native','15660100',r'C:\SteamLibrary\steamapps\common\Thronebreaker The Witcher Tales\Thronebreaker_Data\StreamingAssets\bundledassets\cardassets\scenes')]:
 original=w/(kind+'AnimationData');records=[r for r in json.loads((original/'animations.json').read_text())['animators'] if r['id']==ident];by_path={r['path']:r for r in records};dest=w/'TimingRepair'/('Additional'+kind);dest.mkdir(exist_ok=True)
 for r in records:
  for c in r['clips']:
   c['name']=c['name']+'__'+Path(c['file']).stem.split('_')[-1];shutil.copy2(original/c['file'],dest/c['file'])
 env=UnityPy.load(src)
 for obj in env.objects:
  if obj.type.name!='Animator' or ident not in obj.assets_file.name:continue
  d=obj.read();go=d.m_GameObject.deref().read();tr=next(c.component.deref() for c in go.m_Component if c.component.deref().type.name=='Transform');record=by_path.get(path(tr))
  if record is None:continue
  co=d.m_Controller.deref();controller=co.read_typetree();clipnames=[]
  for ref in controller['m_AnimationClips']:
   c=PPtr(m_FileID=ref['m_FileID'],m_PathID=ref['m_PathID'],assetsfile=co.assets_file).deref() if ref['m_PathID'] else None
   match=next((x for x in record['clips'] if x['file']==ident+'_'+str(c.path_id)+'.bin'),None) if c else None
   clipnames.append(match['name'] if match else '')
  for li,wrapped in enumerate(controller['m_Controller']['m_LayerArray']):
   layer=wrapped['data'];sm=controller['m_Controller']['m_StateMachineArray'][layer['m_StateMachineIndex']]['data']
   for si,state in enumerate(sm['m_StateConstantArray']):
    nodes=[n['data'] for tree in state['data'].get('m_BlendTreeConstantArray',[]) for n in tree['data']['m_NodeArray']];ids=list(dict.fromkeys(n['m_ClipID'] for n in nodes if n.get('m_ClipID',4294967295)<len(clipnames)));names=[clipnames[i] for i in ids];assert all(names);record['layers'][li]['states'][si]['clips']=names
  report.append(dict(source=kind,path=record['path'],layers=record['layers']))
 (dest/'animations.json').write_text(json.dumps(dict(animators=records)))
(out:=w/'TimingRepair/additional-duplicate-clips.json').write_text(json.dumps(report,indent=2));print('RESTORED',[(r['path'],[(s['name'],s['clips']) for l in r['layers'] for s in l['states']]) for r in report])

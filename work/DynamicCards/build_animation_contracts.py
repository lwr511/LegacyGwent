from pathlib import Path
import sys,json,re
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from UnityPy.classes import PPtr
root=Path(__file__).resolve().parent
kind=sys.argv[1] if len(sys.argv)>1 else 'native'
source=r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes' if kind=='native' else r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'
folder=root/({'native':'NativeAnimationData','legacy':'LegacyAnimationData','latest':'LatestAnimationData'}[kind]);doc=json.loads((folder/'animations.json').read_text())
records={(r['id'],r['path']):r for r in doc['animators']}
for record in records.values():
 record['clips']=list({clip['file']:clip for clip in record['clips']}.values())
 counts={c['name']:sum(x['name']==c['name'] for x in record['clips']) for c in record['clips']}
 for clip in record['clips']:
  if counts[clip['name']]>1:clip['name']+='__'+Path(clip['file']).stem.split('_')[-1]
def path(tr):
 d=tr.read();return (path(d.m_Father.deref())+'/' if d.m_Father.path_id else '')+d.m_GameObject.deref().read().m_Name
stats={}
def objects():
 if kind!='latest':yield from UnityPy.load(source).objects;return
 shared=UnityPy.Environment();visited=set()
 base=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets')
 def deps(file):
  if file in visited:return
  visited.add(file);manifest=Path(str(file)+'.manifest')
  if not manifest.exists():return
  for line in manifest.read_text().split('Dependencies:')[-1].splitlines():
   if '/bundledassets/' not in line:continue
   dep=base/line.split('/bundledassets/')[-1].strip()
   if '/textures/' in str(dep):continue
   deps(dep);shared.load_file(str(dep))
 for key in sorted({r['id'] for r in records.values()}):
  file=base/'cardassets/scenes'/key;env=UnityPy.load(str(file));deps(file);env.cabs.update(shared.cabs);yield from env.objects
for obj in objects():
 if obj.type.name!='Animator':continue
 match=re.search(r'\d{8}',obj.assets_file.name)
 if not match:continue
 d=obj.read();go=d.m_GameObject.deref().read();tr=next(c.component.deref() for c in go.m_Component if c.component.deref().type.name=='Transform')
 r=records.get((match[0],path(tr)))
 if r is None or not d.m_Controller.path_id:continue
 co=d.m_Controller.deref();c=co.read_typetree();names=dict(c['m_TOS']);clips=[]
 for ref in c['m_AnimationClips']:
  sourceClip=PPtr(m_FileID=ref['m_FileID'],m_PathID=ref['m_PathID'],assetsfile=co.assets_file).deref() if ref['m_PathID'] else None
  name=sourceClip.read().m_Name if sourceClip else ''
  candidate=next((v for v in r['clips'] if sourceClip and v['file']==r['id']+'_'+str(sourceClip.path_id)+'.bin'),None)
  clips.append(candidate['name'] if candidate else '')
 r['layers']=[]
 for index,layer in enumerate(c['m_Controller']['m_LayerArray']):
  layer=layer['data'];sm=c['m_Controller']['m_StateMachineArray'][layer['m_StateMachineIndex']]['data'];states=[]
  for wrapped in sm['m_StateConstantArray']:
   s=wrapped['data'];nodes=[n['data'] for tree in s.get('m_BlendTreeConstantArray',[]) for n in tree['data']['m_NodeArray']]
   ids=list(dict.fromkeys(n['m_ClipID'] for n in nodes if n.get('m_ClipID',4294967295)<len(clips)))
   transitions=[]
   for wrappedTransition in s.get('m_TransitionConstantArray',[]):
    tr=wrappedTransition['data'];transitions.append(dict(destination=tr['m_DestinationState'],exitTime=tr.get('m_ExitTime',1),duration=tr.get('m_TransitionDuration',0),offset=tr.get('m_TransitionOffset',0),fixedDuration=tr.get('m_HasFixedDuration',True),hasExitTime=tr.get('m_HasExitTime',True),unconditional=not tr.get('m_ConditionConstantArray',[])))
   states.append(dict(name=names.get(s['m_NameID'],str(s['m_NameID'])),clips=[clips[i] for i in ids],speed=s.get('m_Speed',1),loop=s.get('m_Loop',False),writeDefaults=s.get('m_WriteDefaultValues',True),transitions=transitions))
  r['layers'].append(dict(name=names.get(layer['m_Binding'],'Layer'+str(index)),weight=1 if index==0 else layer['m_DefaultWeight'],additive=bool(layer.get('(int&)m_LayerBlendingMode',layer.get('m_LayerBlendingMode',0))),defaultState=sm.get('m_DefaultState',0),states=states))
 stats[len(r['layers'])]=stats.get(len(r['layers']),0)+1
(folder/'animations.json').write_text(json.dumps(doc,indent=2))
print('SOURCE_LAYERS',kind,stats,flush=True)
for r in doc['animators']:
 if r['id']=='10090100':print(json.dumps(r['layers'],indent=2),flush=True)

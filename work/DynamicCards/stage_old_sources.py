"""Stage old-source-only premium content without changing the active Unity content."""
from pathlib import Path
import json,re,shutil,collections,os,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parent
P=R.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
S=R/'Probe'; C=S/'Assets/DynamicCards/Content'
DEST=R/'OldSourcesStage'; DEST.mkdir(exist_ok=True)
old=json.loads((R/'BeforeCompletePremiumImport/Content/catalog.json').read_text())['cards']
current=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text())['cards']
effects=json.loads((R/'second_source_effects.json').read_text())
audio=json.loads((R/'legacy_audio.json').read_text())
matches=json.loads((R/'second_source_matches.json').read_text())
defined={d.get('ArtId')+'0' for t in ET.parse(R/'legacy_defs_Templates.xml').getroot() for d in t.findall('ArtDefinition') if d.get('ArtId')}
native=[c.copy() for c in old if '/Legacy2017/' not in c['prefab']]
legacy_saved={c['id']:c for c in old if '/Legacy2017/' in c['prefab']}
cards=native[:];owned={a for c in cards for a in c['artIds']};standard={c['id'][:-1] for c in cards}
for match in sorted(matches,key=lambda x:x['id']):
 ident=match['id'];folder=C/'Legacy2017'/ident
 aliases=list(dict.fromkeys(match.get('artIds',[])+match.get('newArtIds',[])))
 if match['standard'] in defined and match['standard'] not in aliases:aliases.append(match['standard'])
 if aliases and all(a in owned for a in aliases):continue
 conversion=json.loads((folder/'conversion.json').read_text())
 if ident in legacy_saved:c=json.loads(json.dumps(legacy_saved[ident]))
 else:
  c=dict(id=ident,artIds=[],prefab=f'Assets/DynamicCards/Content/Legacy2017/{ident}/Card.prefab',audio='',pivot='',fieldOfView=25,cameraDistance=-29.87103,nearClip=1,farClip=300,xStart=-6,xEnd=6,yStart=-2,yEnd=2,introDuration=0,loopDuration=1,cutTime=-1,particleEvents=[],uvMotions=[],transformPairs=[])
  if conversion.get('animations'):c['introDuration']=max(a.get('introDuration',0) for a in conversion['animations'])
  for f in folder.glob('*Loop*.anim'):
   m=re.search(r'm_StopTime: ([\d.Ee+-]+)',f.read_text());
   if m:c['loopDuration']=float(m[1]);break
  info=effects[ident];particle_paths=[]
  for script in info['scripts']:
   t=script['type'];d=script['data'];refs=script['references']
   if t=='CameraValuesChanger':c.update(fieldOfView=d['fov'],cameraDistance=d['camDistance'],nearClip=d['nearClippingPlane'],farClip=d['farClippingPlane'])
   elif t=='RotationObjectController':c.update(pivot=script['path'],xStart=d['XRotationStart'],xEnd=d['XRotationEnd'],yStart=d['YRotationStart'],yEnd=d['YRotationEnd'])
   elif t=='VFXAnimationEventListener':particle_paths += [p for k,p in refs.items() if k.startswith('particleSystems.') and p]
   elif t=='UpdateTextureOffset' and d.get('m_Enabled',1):c['uvMotions'].append(dict(path=script['path'],property=d['m_textureName'],materialIndex=d['m_matIndex'],speed=dict(x=d['m_xOffsetMulti'],y=d['m_yOffsetMulti']),start=d['m_ForcedOffset'],forceStart=bool(d['m_ForceStartOffset'])))
   elif t=='PairTransforms':
    for i in range(len(d['Pair'])):c['transformPairs'].append(dict(source=refs[f'Pair.{i}.Source'],target=refs[f'Pair.{i}.Target']))
   elif t in ('GeraltSwordmasterAnimationEvents','RocheAnimationEvents'):
    ev=next((e for e in info['events'] if e['functionName'] in ('GeraltRigSwitch','RocheGroupSwitch')),None)
    if ev:c.update(beforeCut=refs['firstGroup'],afterCut=refs['secoundGroup'],cutTime=ev['time'])
  for ev in info['events']:
   if ev['functionName']=='PlayEffect':
    c['particleEvents'] += [dict(path=p,time=ev['time'],loop='loop' in ev['clip'].lower()) for p in particle_paths if p.rsplit('/',1)[-1]==ev['data']]
  files=audio.get(ident,{}).get('files',[])
  if files:c['audio']='Assets/DynamicCards/Content/Legacy2017/Audio/'+max(files,key=lambda f:f['bytes'])['file']
 c['artIds']=[a for a in aliases if a not in owned];owned.update(c['artIds']);cards.append(c);standard.add(ident[:-1])
# Transfer verified project aliases only where a shared art ID or exact standard ID identifies the old scene.
for c in cards:
 for new in current:
  if set(c['artIds'])&set(new.get('artIds',[])):
   for a in new.get('artIds',[]):
    if a not in owned:c['artIds'].append(a);owned.add(a)
old_catalog=DEST/'catalog-input.json';old_catalog.write_text(json.dumps(dict(version=1,cards=cards),indent=2))
# Reuse the established extraction of source controller settings, but target the staged catalog.
code=(R/'prepare_controllers.py').read_text();code=code.replace("catalog=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/catalog.json'",'catalog=root/"OldSourcesStage/catalog-input.json"')
code=code.replace("effects=json.loads((root/'source_effects.json').read_text())","effects=json.loads((root/'source_effects.json').read_text());effects.update(json.loads((root/'second_source_effects.json').read_text()))")
exec(compile(code,str(R/'prepare_controllers.py'),'exec'),{'__file__':str(R/'prepare_controllers.py')})
cards=json.loads(old_catalog.read_text())['cards']
for c in cards:
 if '/Legacy2017/' in c['prefab']:c['initialTransforms']=[dict(path=s['path'],position=s['data']['localPos'],rotation=s['data']['localRot'],scale=s['data']['localScl']) for s in effects[c['id']]['scripts'] if s['type']=='OnStartTransformModification' and s['data'].get('m_Enabled',1)]
def mapped(path):
 path=path.replace('\\','/')
 if path.startswith('Assets/DynamicCards/Content/Legacy2017/'):return path.replace('Assets/DynamicCards/Content/Legacy2017/','Assets/DynamicCards/Content/Old/Legacy2017/',1)
 if path.startswith('Assets/DynamicCards/Content/'):return path.replace('Assets/DynamicCards/Content/','Assets/DynamicCards/Content/Old/Thronebreaker/',1)
 return path
print('INDEX_START',len(cards),flush=True)
index={}; main_scripts={p.stem:re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1] for p in (P/'Assets/DynamicCards/Runtime').glob('*.cs') if Path(str(p)+'.meta').exists()}; remap={}
for folder,dirs,files in os.walk(S/'Assets/DynamicCards'):
 dirs[:]=[d for d in dirs if d not in ('Latest','Fallback')]
 for name in files:
  if not name.endswith('.meta'):continue
  meta=Path(folder)/name;m=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)
  if not m:continue
  asset=Path(str(meta)[:-5]);index[m[1]]=asset
  if asset.suffix=='.cs' and asset.stem in main_scripts:remap[m[1]]=main_scripts[asset.stem]
copied=set();missing=set();extras=[]
def copy(asset):
 asset=Path(asset)
 if asset in copied:return
 copied.add(asset)
 if not asset.exists():missing.add(str(asset));return
 rel=asset.relative_to(S).as_posix()
 if asset.suffix=='.cs':
  if asset.stem not in main_scripts:missing.add('SCRIPT '+rel)
  return
 target=DEST/mapped(rel);target.parent.mkdir(parents=True,exist_ok=True)
 if target.exists():return
 if asset.suffix.lower() in ('.prefab','.anim','.controller','.mat','.asset','.json','.shader','.cginc'):
  try:body=asset.read_text(encoding='utf-8-sig')
  except UnicodeDecodeError:body=None
  if body is not None:
   for guid in set(re.findall(r'guid: ([a-fA-F0-9]{32})',body)):
    if guid in index:copy(index[guid])
    elif guid not in main_scripts.values() and not guid.startswith('0000000000000000'):missing.add('GUID '+guid+' in '+rel)
   body=body.replace('Assets/DynamicCards/Content/Legacy2017/','@@LEGACY@@').replace('Assets/DynamicCards/Content/','Assets/DynamicCards/Content/Old/Thronebreaker/').replace('@@LEGACY@@','Assets/DynamicCards/Content/Old/Legacy2017/')
   for old_guid,new_guid in remap.items():body=body.replace(old_guid,new_guid)
   target.write_text(body,encoding='utf-8')
  else:shutil.copy2(asset,target)
 else:shutil.copy2(asset,target)
 meta=Path(str(asset)+'.meta')
 if meta.exists():shutil.copy2(meta,Path(str(target)+'.meta'))
for i,c in enumerate(cards):
 folder=(S/c['prefab']).parent
 for asset in folder.iterdir():
  if asset.is_file() and not asset.name.endswith('.meta'):copy(asset)
 if c.get('audio'):copy(S/c['audio'])
 if i%50==0:print('STAGED',i,len(copied),flush=True)
for c in cards:
 c['sourceId']=c['id'];c['sourceVersion']='Legacy2017' if '/Legacy2017/' in c['prefab'] else 'Thronebreaker'
 c['prefab']=mapped(c['prefab']);c['audio']=mapped(c.get('audio',''))
out=DEST/'Assets/DynamicCards/Content/catalog.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(dict(version=1,cards=cards),indent=2))
report=dict(cards=len(cards),sources=dict(collections.Counter(c['sourceVersion'] for c in cards)),mappedArtIds=len(owned),files=len(copied),missing=sorted(missing),geralt=next(c for c in cards if '11210300' in c['artIds']))
(DEST/'report.json').write_text(json.dumps(report,indent=2));print('STAGE_COMPLETE',json.dumps({k:v for k,v in report.items() if k!='geralt'}),flush=True)

"""Copy only referenced portable resources. Never modify source-game or legacy-card assets."""
import json,re,shutil,hashlib,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parent
BRIDGE=ROOT/'Bridge2022'
CLIENT=ROOT.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
DEST=CLIENT/'Assets/DynamicCards/Content'
effects=json.loads((ROOT/'source_effects.json').read_text())
audio=json.loads((ROOT/'source_audio.json').read_text())
extra_assets=json.loads((ROOT/'extra_effect_assets.json').read_text())
matches=json.loads((ROOT/'art_matches.json').read_text())
old_cards=json.loads(Path(r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/outputs/ThronebreakerGodot/assets/cards.json').read_text(encoding='utf8'))
top_margins={c['id']:c.get('display_top_margin_px',0) for c in old_cards}
from concurrent.futures import ThreadPoolExecutor
def read_guid(p):
    m=re.search(r'^guid: (\w+)',p.read_text(),re.M)
    return (m[1],Path(str(p)[:-5])) if m else None
with ThreadPoolExecutor(max_workers=12) as workers:
    guid_paths=dict(item for item in workers.map(read_guid,(BRIDGE/'Assets/PortableCards').rglob('*.meta')) if item)
done=set()
for log in [ROOT/'portable_all2.log',ROOT/'portable_all3.log',ROOT/'portable_all4.log']:
    if log.exists():done.update(re.findall(r'PORTABLE_CARD_DONE (\d+)',log.read_text(errors='replace')))
if '--probe' in sys.argv: done={'15660100'}
copied=set()
def copy(p):
    if p in copied:return
    copied.add(p)
    if not p.exists():raise FileNotFoundError(p)
    relative=p.relative_to(BRIDGE/'Assets/PortableCards');out=DEST/relative
    out.parent.mkdir(parents=True,exist_ok=True)
    if p.suffix in ['.prefab','.asset','.anim','.controller','.mat']:
        text=p.read_text(encoding='utf8')
        # The target importer assigns a portable source shader. Do not drag compiled native shaders in.
        if p.suffix=='.mat':
            text=re.sub(r'm_Shader: \{[^\n]+\}', 'm_Shader: {fileID: 0}',text)
            text=re.sub(r'\{fileID: \d+, guid: 0{32}, type: 0\}', '{fileID: 0}',text)
            if p in globals().get('material_shader_overrides',{}):
                text=re.sub(r'm_Shader: \{[^\n]+\}', 'm_Shader: {fileID: 4800000, guid: '+material_shader_overrides[p]+', type: 3}',text)
        for guid in set(re.findall(r'guid: ([a-f0-9]{32})',text)):
            if guid in guid_paths:copy(guid_paths[guid])
        out.write_text(text,encoding='utf8')
    else:shutil.copy2(p,out)
    if Path(str(p)+'.meta').exists():shutil.copy2(str(p)+'.meta',str(out)+'.meta')
def asset(p):return 'Assets/DynamicCards/Content/'+p
def curve(value):
    if isinstance(value,dict):return value['m_Curve']
    return [dict(time=0,value=value,inSlope=0,outSlope=0),dict(time=1,value=value,inSlope=0,outSlope=0)]
catalog=[]
for ident in sorted(done):
    folder=BRIDGE/'Assets/PortableCards'/ident
    conversion=json.loads((folder/'conversion.json').read_text())
    copy(folder/'Card.prefab')
    for material in conversion['materials']:
        copy(BRIDGE/material['asset'])
    atlas=next((p for p in copied if p.suffix=='.png' and p.stem.endswith('_'+ident)),None)
    if atlas is None:
        candidates=list((BRIDGE/'Assets/PortableCards/Shared').glob('*_'+ident+'.png'))
        if candidates:atlas=candidates[-1];copy(atlas)
    if atlas:conversion['atlas']=asset(str(atlas.relative_to(BRIDGE/'Assets/PortableCards')).replace('\\','/'))
    for a in conversion['animations']:
        for name in ['intro','loop']:
            if a.get(name):
                # Controllers reference the correct clips; these names are timing metadata only.
                pass
    info=effects[ident]
    c=dict(id=ident,artIds=[],prefab=asset(ident+'/Card.prefab'),audio='',pivot='',fieldOfView=25,cameraDistance=-29.87103,nearClip=1,farClip=300,xStart=-6,xEnd=6,yStart=-2,yEnd=2,introDuration=0,loopDuration=1,cutTime=-1,particleEvents=[],uvMotions=[])
    for match in matches:
        if match['sourceId']==ident:
            for candidate in match['candidates']:
                if candidate['mse']<0.0005 and candidate['artId'] not in c['artIds']:c['artIds'].append(candidate['artId'])
    c['topMargin']=top_margins.get(ident,0)
    c['nonRenderingPaths']=info.get('nonRenderingPaths',[])
    if conversion['animations']:
        c['introDuration']=max(a['introDuration'] for a in conversion['animations'])
        for p in folder.glob('*Loop*.anim'):
            m=re.search(r'm_StopTime: ([\d.Ee+-]+)',p.read_text())
            if m:c['loopDuration']=float(m[1]);break
    particle_paths=[]
    c['transformPairs']=[]
    conversion['vertexAnimations']=[]
    conversion['candles']=[]
    conversion['lightning']=[]
    for script in info['scripts']:
        t=script['type'];d=script['data'];refs=script['references']
        if t=='CameraValuesChanger':
            c.update(fieldOfView=d['fov'],cameraDistance=d['camDistance'],nearClip=d['nearClippingPlane'],farClip=d['farClippingPlane'])
        elif t=='RotationObjectController':
            c.update(pivot=script['path'],xStart=d['XRotationStart'],xEnd=d['XRotationEnd'],yStart=d['YRotationStart'],yEnd=d['YRotationEnd'])
        elif t=='GeraltSwordmasterAnimationEvents':
            c.update(beforeCut=refs['firstGroup'],afterCut=refs['secoundGroup'],cutTime=next(ev['time'] for ev in info['events'] if ev['functionName']=='GeraltRigSwitch'))
        elif t=='VFXAnimationEventListener':particle_paths += [p for k,p in refs.items() if k.startswith('particleSystems.') and p]
        elif t=='UpdateTextureOffset' and d.get('m_Enabled',1):
            c['uvMotions'].append(dict(path=script['path'],property=d['m_textureName'],materialIndex=d['m_matIndex'],speed=dict(x=d['m_xOffsetMulti'],y=d['m_yOffsetMulti']),start=d['m_ForcedOffset'],forceStart=bool(d['m_ForceStartOffset'])))
        elif t=='PairTransforms':
            for i in range(len(d['Pair'])):
                c['transformPairs'].append(dict(source=refs['Pair.%d.Source'%i],target=refs['Pair.%d.Target'%i]))
        elif t in ['SetVertexAnimation','SetVertexAnimationParticle']:
            raw=refs.get('RawVertexOffsetData','')
            if raw in extra_assets:
                filename=extra_assets[raw];dest=DEST/'ExtraEffects'/filename;dest.parent.mkdir(exist_ok=True);shutil.copy2(ROOT/'ExtraEffects'/filename,dest)
                conversion['vertexAnimations'].append(dict(path=script['path'],samples=d['Samples'],vertices=d['Vertices'],data=asset('ExtraEffects/'+filename)))
        elif t=='CandleFireTool':
            texture=refs.get('CandleTexture','')
            if texture in extra_assets:
                filename=extra_assets[texture];dest=DEST/'ExtraEffects'/filename;dest.parent.mkdir(exist_ok=True);shutil.copy2(ROOT/'ExtraEffects'/filename,dest)
                for i,size in enumerate(d['Sizes']):
                    conversion['candles'].append(dict(path=refs.get('Positions.%d'%i,script['path']),size=size,texture=asset('ExtraEffects/'+filename)))
        elif t=='LightningTool':
            texture=refs.get('LightningTexture','');anim=next((s['data'] for s in info['scripts'] if s['type']=='LightningAnimator'),None)
            if texture in extra_assets and anim:
                filename=extra_assets[texture];dest=DEST/'ExtraEffects'/filename;dest.parent.mkdir(exist_ok=True);shutil.copy2(ROOT/'ExtraEffects'/filename,dest)
                sections=[]
                for section in anim['LightningAnimations']:
                    sections.append(dict(start=section['StartTime'],length=section['AnimationLength'],tint=section['TintColor'],opacity=curve(section['Opacity']),width=curve(section['Width']),buildUp=curve(section['BuildUp']),noise=curve(section['WiggleStrength']),speed=curve(section['WiggleSpeed'])))
                for i,bolt in enumerate(d['Lightnings']):
                    conversion['lightning'].append(dict(path=script['path'],start=refs['Lightnings.%d.StartObj'%i],end=refs['Lightnings.%d.EndObj'%i],points=bolt['Elements'],width=d['LightningWidth'],duration=anim['SequenceLength'],sections=sections,texture=asset('ExtraEffects/'+filename)))
    for event in info['events']:
        if event['functionName']=='PlayEffect':
            for p in particle_paths:
                if p.rsplit('/',1)[-1]==event['data']:
                    c['particleEvents'].append(dict(path=p,time=event['time'],loop='loop' in event['clip'].lower()))
        elif event['functionName']:
            for script in info['scripts']:
                if script['type']=='ArachasBehemothAnimationEvents':
                    key=event['functionName'][0].lower()+event['functionName'][1:]
                    if key in script['references']:c['particleEvents'].append(dict(path=script['references'][key],time=event['time'],loop='loop' in event['clip'].lower()))
    if ident in audio:
        files=audio[ident]['files']
        for item in files:
            out=DEST/'Audio'/item['file'];out.parent.mkdir(exist_ok=True);shutil.copy2(ROOT/'Audio'/item['file'],out)
        # The two multi-media banks contain alternatives. Select a stable original variation.
        if files:c['audio']=asset('Audio/'+max(files,key=lambda f:f['bytes'])['file'])
    for material in conversion['materials']:material['asset']=material['asset'].replace('Assets/PortableCards/','Assets/DynamicCards/Content/')
    conversion['textureAssignments']=info['textureAssignments']
    (DEST/ident/'conversion.json').write_text(json.dumps(conversion,indent=2),encoding='utf8')
    catalog.append(c)
DEST.mkdir(parents=True,exist_ok=True)
(DEST/'catalog.json').write_text(json.dumps(dict(version=1,cards=catalog),indent=2),encoding='utf8')
(ROOT/'content_inventory.json').write_text(json.dumps(dict(cards=len(catalog),matchedCards=sum(bool(c['artIds']) for c in catalog),matchedArtIds=sum(len(c['artIds']) for c in catalog),files=len(copied),particles=sum(json.loads((BRIDGE/'Assets/PortableCards'/c['id']/'conversion.json').read_text())['particles'] for c in catalog)),indent=2))
print((ROOT/'content_inventory.json').read_text())
if (ROOT/'shader_rendering.json').exists():
    import runpy
    runpy.run_path(str(ROOT/'apply_shader_states.py'))

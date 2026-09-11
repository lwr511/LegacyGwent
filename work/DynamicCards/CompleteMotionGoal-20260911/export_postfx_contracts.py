from pathlib import Path
import json,zlib,re,array,collections,sys
sys.stdout.reconfigure(encoding='utf-8')
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards'];identities=json.loads((W/'script-binding-identities.json').read_text());records=[];curves=[]
fields=['m_Enabled','blurSpread','contrast','blurIntensity','AngleIntensity','speed','intensity','staticIntensity','deltaTime'];names={zlib.crc32(s.encode()):s for s in fields}
for source,ef,af in [('Legacy2017','second_source_effects.json','LegacyAnimationData'),('Thronebreaker','source_effects.json','NativeAnimationData')]:
    effects=json.loads((W.parent/ef).read_text());animations=json.loads((W.parent/af/'animations.json').read_text());ars=collections.defaultdict(list)
    for r in {(r['id'],r['path']):r for r in animations['animators']}.values():ars[r['id']].append(r)
    for c in catalog:
        if '/'+source+'/' not in c['prefab']:continue
        scene=c.get('sourceId') or c['id'];scripts=effects[scene]['scripts'];post=[s for s in scripts if s['type'] in ['ForwardGlitchSettings','ForwardBloomSettings']]
        record={'source':source,'scene':scene,'prefab':c['prefab'],'components':[],'curves':[]}
        for s in post:
            kind='Glitch' if s['type']=='ForwardGlitchSettings' else 'Bloom';data={k:v for k,v in s['data'].items() if not k.startswith('m_') and k not in ['ObjCtrl','StaticNoise']};obj=s['references'].get('ObjCtrl','')
            if obj:
                controller=next(x for x in scripts if x['type']=='RotationObjectController' and x['path']==obj)
                for name in ['XRotationStart','XRotationEnd','YRotationStart','YRotationEnd']:data[name]=controller['data'][name]
            cohort='Legacy' if source=='Legacy2017' else 'Thronebreaker'
            record['components'].append({'path':s['path'],'kind':kind,'enabled':bool(s['data']['m_Enabled']),'dataJson':json.dumps(data,separators=(',',':')),'rotationObject':obj,'noise':'Assets/DynamicCards/Content/SourcePostFx/VHS_Static.png' if kind=='Glitch' else '', 'shader':f'Assets/DynamicCards/Shaders/SourcePostFx/{cohort}/Custom_CardPollen_{kind}.shader'})
        relevant=[i for i in identities if i['source']==source and i['scene']==scene and i['script'] in ['ForwardGlitchSettings','ForwardBloomSettings','Hayate']]
        for r in ars[scene]:
            for clip in {cl['file']:cl for cl in r['clips']}.values():
                file_id=int(Path(clip['file']).stem.rsplit('_',1)[1]);matching=[i for i in relevant if i['clipFileId']==file_id]
                if not matching:continue
                values=array.array('f');values.frombytes((W.parent/af/clip['file']).read_bytes())
                for identity in matching:
                    tracks=[t for t in clip['tracks'] if t.get('typeId')==114 and t['attribute']==identity['attribute'] and zlib.crc32(t['path'].encode())==identity['pathHash']]
                    assert len(tracks)==1,(scene,clip['name'],identity,tracks)
                    t=tracks[0];kind={'ForwardGlitchSettings':'Glitch','ForwardBloomSettings':'Bloom','Hayate':'Hayate'}[identity['script']];field=names[t['attribute']]
                    assert any(s['type']==identity['script'] and s['path']=='/'.join(x for x in [r['path'],t['path']] if x) for s in scripts)
                    wrong=[]
                    if field=='m_Enabled':
                        for ty in [95,199]:
                            if not any(q.get('typeId')==ty and q['path']==t['path'] and q['attribute']==t['attribute'] for q in clip['tracks']):wrong.append(ty)
                    record['curves'].append({'animator':r['path'],'clip':clip['name'],'path':t['path'],'kind':kind,'property':field,'duration':clip['duration'],'samples':list(values[t['offset']::clip['columns']]),'removeWrongTypes':wrong})
        if record['components'] or record['curves']:records.append(record)
(W/'postfx-contracts.json').write_text(json.dumps({'records':records},indent=2));print('POSTFX prefabs',len(records),'effects',sum(len(r['components']) for r in records),'script curves',sum(len(r['curves']) for r in records))

from pathlib import Path
import json,sys,zlib,array,collections
sys.stdout.reconfigure(encoding='utf-8')
W=Path(__file__).resolve().parent
P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
cat=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards']
contracts=[];unmapped=[]
for source,effects_file,animation_folder in [('Legacy2017','second_source_effects.json','LegacyAnimationData'),('Thronebreaker','source_effects.json','NativeAnimationData')]:
    entries={c['sourceId']:c for c in cat if c.get('sourceVersion')==source}
    effects=json.loads((W.parent/effects_file).read_text(encoding='utf-8-sig'))
    animations=json.loads((W.parent/animation_folder/'animations.json').read_text(encoding='utf-8-sig'))
    records={(r['id'],r['path']):r for r in animations['animators']}
    for scene,entry in entries.items():
        for script in effects[scene]['scripts']:
            if script['type']!='OGOAnimationIntermediary':continue
            used=script['references']['UsedAnimator'];r=records[(scene,used)]
            contract={'source':source,'scene':scene,'prefab':entry['prefab'],'animator':used,'proxies':[],'curves':[]}
            params=collections.defaultdict(list)
            for array_name,kind,fields in [('Parameters',2,['m_Float']),('ColorPrameters',1,['m_Color.r','m_Color.g','m_Color.b','m_Color.a']),('ScaleTexPrameters',4,['m_Scale.x','m_Scale.y']),('OffsetTexPrameters',3,['m_Offset.x','m_Offset.y'])]:
                for i,item in enumerate(script['data'][array_name]):
                    target=script['references'][array_name+'.'+str(i)+'.Rend'];slot=item['MatlId'];name=item['VariableName']
                    proxy='__SourceMaterial_'+format(zlib.crc32((target+'|'+str(slot)+'|'+name).encode()),'08x')
                    contract['proxies'].append({'path':proxy,'target':target,'slot':slot,'name':name,'kind':kind})
                    names=[item['VariableAnimatorName']] if kind==2 else item['VariableAnimatorNames']
                    for field,param in zip(fields,names):
                        params[zlib.crc32(param.encode())].append({'proxy':proxy,'field':field,'sourceParameter':param})
            found=set()
            for cl in {c['file']:c for c in r['clips']}.values():
                tracks=[t for t in cl['tracks'] if t.get('typeId')==95 and t['attribute'] in params]
                if not tracks:continue
                values=array.array('f');values.frombytes((W.parent/animation_folder/cl['file']).read_bytes())
                for track in tracks:
                    assert track['dimension']==1
                    for target in params[track['attribute']]:
                        contract['curves'].append(dict(target,clip=cl['name'],duration=cl['duration'],samples=list(values[track['offset']::cl['columns']]),sourceAttribute=track['attribute']))
                        found.add(target['sourceParameter'])
            for mappings in params.values():
                for item in mappings:
                    if item['sourceParameter'] not in found:unmapped.append({'scene':scene,**item})
            if contract['curves']:contracts.append(contract)
            print(source,scene,'proxies',len(contract['proxies']),'curves',len(contract['curves']),flush=True)
result={'records':contracts,'unmappedSourceParameters':unmapped}
(W/'ogo-animation-contracts.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print('TOTAL scenes',len(contracts),'curves',sum(len(r['curves']) for r in contracts),'unmapped',unmapped)

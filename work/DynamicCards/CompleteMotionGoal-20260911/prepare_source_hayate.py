from pathlib import Path
import json,re,hashlib
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');H=W/'OgoImportHarness'
out=H/'Assets/DynamicCards/Runtime/SourceParticles';out.mkdir(parents=True,exist_ok=True)
for name in ['Hayate','HayateEnums','HayateHelper','HayateTurbulence','Noise']:
    code=(W/'OriginalCode'/f'{name}.cs').read_text(encoding='utf-8-sig')
    code=code.replace('[ExecuteInEditMode]\n','')
    code=code.replace('using Simplex;','using Assets.Script.DynamicCards.SourceParticles.Simplex;')
    ns='Assets.Script.DynamicCards.SourceParticles'+('.Simplex' if name=='Noise' else '')
    code=code.replace('namespace Simplex;','')
    start=re.search(r'public (?:static )?class ',code).start()
    code=code[:start]+'namespace '+ns+'\n{\n'+code[start:]+'\n}\n'
    code='// Recovered from the supplied Unity 2017 card particle implementation.\n// Namespace isolated; editor-time simulation is intentionally omitted.\n'+code
    f=out/f'{name}.cs';f.write_text(code,encoding='utf-8')
    guid=hashlib.md5(('DynamicCards/SourceParticles/'+name).encode()).hexdigest()
    Path(str(f)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
booleans=set(re.findall(r'public bool (\w+)',(W/'OriginalCode/Hayate.cs').read_text()))
effects=json.loads((W.parent/'second_source_effects.json').read_text(encoding='utf-8-sig'));catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text(encoding='utf-8-sig'))['cards'];records=[]
object_fields={'Turbulence','transformParticle','followTransform','attractors','meshTarget','skinnedMeshTarget'}
routes_file=W/'hayate-source-object-routes.json'
routes={(r['scene'],r['objectId']):r for r in json.loads(routes_file.read_text())} if routes_file.exists() else {}
for c in catalog:
    if '/Legacy2017/' not in c['prefab']:continue
    scripts=[s for s in effects[c['sourceId']]['scripts'] if s['type']=='Hayate']
    if not scripts:continue
    grouped={c['prefab']:{'source':'Legacy2017','scene':c['id'],'prefab':c['prefab'],'components':[]}}
    for s in scripts:
        object_id=s['data']['m_GameObject']['m_PathID'];route=routes.get((c['id'],object_id),{})
        prefab=c['prefab']
        if not s['path'].startswith(c['sourceId']+'/'):
            tile=(P/prefab).parent/'Tiles'/s['path'].split('/')[0]/'Tile.prefab'
            assert tile.exists(), (c['id'],s['path'])
            prefab=tile.relative_to(P).as_posix()
            if prefab not in grouped:grouped[prefab]={'source':'Legacy2017','scene':c['id'],'prefab':prefab,'components':[]}
        fields={k:(bool(v) if k in booleans else v) for k,v in s['data'].items() if not k.startswith('m_') and k not in object_fields}
        refs=[]
        for k,v in s['references'].items():
            if k.split('.')[0] not in object_fields:continue
            refs.append({'field':k,'path':v,'ordinals':next((x['ordinals'] for x in route.get('references',[]) if x['field']==k),[])})
        grouped[prefab]['components'].append({'path':s['path'],'sourceObjectId':object_id,'ordinals':route.get('ordinals',[]),'enabled':bool(s['data']['m_Enabled']),'dataJson':json.dumps(fields,separators=(',',':')),'references':refs})
    records.extend(r for r in grouped.values() if r['components'])
(W/'hayate-contracts.json').write_text(json.dumps({'records':records},indent=2));print('HAYATE scenes',len(records),'components',sum(len(r['components']) for r in records))

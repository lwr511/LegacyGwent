import json
from pathlib import Path
root=Path(__file__).resolve().parent
catalog=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Content/catalog.json'
data=json.loads(catalog.read_text());effects=json.loads((root/'source_effects.json').read_text())
counts=dict(view=0,parents=0,jiggles=0,wiggles=0)
for card in data['cards']:
    card.update(viewMotions=[],cameraParents=[],jiggles=[],wiggles=[],materialValues=[])
    scripts=effects[card['id']]['scripts']
    for script in scripts:
        d=script['data'];refs=script['references'];kind=script['type']
        if not d.get('m_Enabled',1):continue
        if kind=='CameraPositionUVRemap':
            card['viewMotions'].append(dict(path=script['path'],property=d['MatPropertyName'],materialIndex=0,kind=0,xRange=d['TextureOffsetMinMaxX'],yRange=d['TextureOffsetMinMaxY']))
        elif kind=='CameraRotationToMaterialOffset':
            for axis,key in enumerate(['RotationSettingsToUseX','RotationSettingsToUseY']):
                for i,item in enumerate(d[key]):
                    card['viewMotions'].append(dict(path=refs[key+'.%d.Rend'%i],property=item['Name'],materialIndex=item['MatId'],kind=1,axis=axis,multiplier=item['MultiValue']))
        elif kind=='FindCameraAndParent':card['cameraParents'].append(dict(path=script['path'],delay=d['Delay']))
        elif kind=='JiggleBones':
            card['jiggles'].append(dict(bones=[refs['Bones.%d'%i] for i in range(len(d['Bones']))],speed=d['Speed'],damping=d['Damping'],maxDistance=d['MaxDistance'],maxVelocity=d['MaxVelocity'],delay=d['InitDelay'],falloff=d['FallOff']['m_Curve']))
        elif kind=='AddWiggle' and d['Animate']:
            card['wiggles'].append(dict(path=refs.get('wiggleTransform',script['path']),delay=d['InitialDelay'],length=d['AnimationLength'],loop=bool(d['Loop']),frequency=d['Frequency']['m_Curve'],strength=d['Strength']['m_Curve'],speed=d['Speed']['m_Curve']))
        elif kind=='MaterialAnimator':
            renderer=refs.get('m_SkinnedRenderer') or refs.get('m_MeshRenderer')
            for i in range(len(d['Properties'])):
                target=refs['Properties.%d'%i]
                prop=next((s for s in scripts if s['type']=='MatPropertyAnimate' and s['path']==target),None)
                if renderer and prop and prop['data']['Type']==2:
                    card['materialValues'].append(dict(path=renderer,property=prop['data']['Name'],materialIndex=d['MatSlot'],value=prop['data']['m_Float']))
    counts['view']+=len(card['viewMotions']);counts['parents']+=len(card['cameraParents']);counts['jiggles']+=len(card['jiggles']);counts['wiggles']+=len(card['wiggles'])
catalog.write_text(json.dumps(data,indent=2),encoding='utf8')
print(counts)

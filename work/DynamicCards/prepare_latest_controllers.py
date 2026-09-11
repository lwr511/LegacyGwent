from pathlib import Path
import json
root=Path(__file__).resolve().parent
catalog=root/'Probe/Assets/DynamicCards/Content/catalog.json'
data=json.loads(catalog.read_text());effects=json.loads((root/'latest_source_effects.json').read_text())
for card in data['cards']:
 if '/Latest/' not in card['prefab']:continue
 card.update(viewMotions=[],jiggles=[])
 for script in effects[card['id']]['scripts']:
  d=script['data'];refs=script['references'];kind=script['type']
  if not d.get('m_Enabled',1):continue
  if kind=='CameraPositionUVRemap':
   card['viewMotions'].append(dict(path=refs.get('m_Renderer') or script['path'],property=d['m_MatPropertyName'],materialIndex=d['m_MaterialIndex'],kind=0,xRange=d['m_TextureOffsetMinMaxX'],yRange=d['m_TextureOffsetMinMaxY']))
  elif kind=='CameraRotationToMaterialOffset':
   for axis,key in enumerate(['RotationSettingsToUseX','RotationSettingsToUseY']):
    for i,item in enumerate(d[key]):card['viewMotions'].append(dict(path=refs[key+'.%d.Rend'%i],property=item['Name'],materialIndex=item['MatId'],kind=1,axis=axis,multiplier=item['MultiValue']))
  elif kind=='JiggleBones':
   card['jiggles'].append(dict(bones=[refs['Bones.%d'%i] for i in range(len(d['Bones']))],speed=d['Speed'],damping=d['Damping'],maxDistance=d['MaxDistance'],maxVelocity=d['MaxVelocity'],delay=d['InitDelay'],falloff=d['FallOff']['m_Curve']))
catalog.write_text(json.dumps(data,indent=2))

from pathlib import Path
import json,re,sys,hashlib
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
from PIL import Image,ImageChops
W=Path(__file__).parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
cloud=P/'Assets/DynamicCards/Content/Latest/Shared/11150101_14977452_VFXC_Cloud_TiledWhite_01.png'
original=Image.open(W/'latest-10.png').convert('RGBA');existing=Image.open(cloud).convert('RGBA')
assert existing.size==original.size and max(v[1] for v in ImageChops.difference(existing,original).getextrema())<=4, 'Existing cloud must match source within compression rounding'
edits=[
 ('Assets/DynamicCards/Content/Latest/Shared/21870101_709854__21870300_AuberonMuircetachTierIIIConqueror-Sky.mat',r'(- _MainTex:\s*\n\s*m_Texture: \{fileID: 2800000, guid: )\w+',r'\g<1>03b5137c12040c045ac67156df70def7'),
 ('Assets/DynamicCards/Content/Old/Thronebreaker/13870100/Card.prefab',r'(  blurIntensity: )(?:1|0\.1)\r?\n',r'\g<1>0.1\n'),
 ('Assets/DynamicCards/Content/Old/Thronebreaker/Shared/14830100_149640__14830300_EyckOfDensele_mat_10_1.mat',r'(- _Cutoff: )(?:0|0\.5)\r?\n',r'\g<1>0.5\n')]
backup={}
for asset,pattern,replacement in edits:
 p=P/asset;data=p.read_bytes();backup[asset]=data.decode('utf8');text=data.decode('utf8').replace('\r\n','\n');fixed,n=re.subn(pattern,replacement,text);assert n==1,(asset,n)
 p.write_bytes(fixed.replace('\n','\r\n').encode('utf8') if b'\r\n' in data else fixed.encode('utf8'))
if not (W/'visual-originals.json').exists():(W/'visual-originals.json').write_text(json.dumps(backup,indent=2))
conversion=P/'Assets/DynamicCards/Content/Latest/21870101/conversion.json'
data=json.loads(conversion.read_text())
sky='[21870300]AuberonMuircetachTierIIIConqueror-Sky'
for assignment in data['textureAssignments']:
 if assignment['material']==sky and assignment.get('materialAsset')!=edits[0][0]:assignment['materialAsset']='Assets/DynamicCards/Content/Latest/Shared/21870101_709848__21870300_AuberonMuircetachTierIIIConqueror-Sky.mat'
data['textureAssignments']=[a for a in data['textureAssignments'] if a.get('materialAsset')!=edits[0][0]]
data['textureAssignments'].append({'material':sky,'materialAsset':edits[0][0],'texture':cloud.relative_to(P).as_posix(),'properties':['_MainTex']})
conversion.write_text(json.dumps(data,indent=2)+'\n')
print('Three targeted repairs applied; reused source cloud, at most 4/255 compression rounding')

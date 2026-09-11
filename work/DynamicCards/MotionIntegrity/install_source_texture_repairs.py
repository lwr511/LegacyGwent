from pathlib import Path
import sys,json,re,hashlib,shutil,xml.etree.ElementTree as ET
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
from PIL import Image
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');content=p/'Assets/DynamicCards/Content';backup=w/'BeforeSourceTextureRepair';repairs=[]
assert (w/'main-build-result.txt').read_text().startswith('OK')
def save_backup(path):
 dest=backup/path.relative_to(p);dest.parent.mkdir(parents=True,exist_ok=True)
 if not dest.exists():shutil.copy2(path,dest)
def guid(path):return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]
def install(image,kind,name):
 im=Image.open(image).convert('RGBA');signature=hashlib.sha256(im.tobytes()).hexdigest();folder=content/kind/'Shared';safe=re.sub(r'[^a-zA-Z0-9_-]','_',name)
 for f in folder.glob('*'+safe+'.png'):
  candidate=Image.open(f).convert('RGBA')
  if candidate.size==im.size and hashlib.sha256(candidate.tobytes()).hexdigest()==signature:return str(f.relative_to(p)).replace('\\','/')
 f=folder/('Restored_'+signature[:16]+'_'+safe+'.png')
 if not f.exists():shutil.copy2(image,f)
 meta=Path(str(f)+'.meta')
 if not meta.exists():
  template=next(folder.glob('*.png.meta')).read_text();template=re.sub(r'^guid: \w+', 'guid: '+hashlib.md5(str(f.relative_to(p)).encode()).hexdigest(),template,flags=re.M);template=re.sub(r'maxTextureSize: \d+','maxTextureSize: '+str(max(im.size)),template);meta.write_text(template)
 return str(f.relative_to(p)).replace('\\','/')
def bind(kind,scene,material,properties,texture,reason):
 conv=content/kind/scene/'conversion.json';c=json.loads(conv.read_text());found=False
 for m in c['materials']:
  target=p/m['asset']
  if m['originalName']!=material or not target.exists():continue
  body=target.read_text();new=body
  for prop in properties:new=re.sub(r'(- '+re.escape(prop)+r':\s*\n\s*m_Texture: )[^\n]+',lambda mt:mt[1]+'{fileID: 2800000, guid: '+guid(p/texture)+', type: 3}',new)
  if new!=body:save_backup(target);target.write_text(new)
  found=True
 if not found:
  print('Non-live conversion material',scene,material,flush=True);return
 save_backup(conv)
 c['textureAssignments']=[a for a in c['textureAssignments'] if not (a.get('sourceBinding') and a['material']==material and a['properties']==properties)]
 c['textureAssignments'].append({'material':material,'properties':properties,'texture':texture,'sourceBinding':reason})
 conv.write_text(json.dumps(c,indent=2));repairs.append({'kind':kind,'scene':scene,'material':material,'properties':properties,'texture':texture,'reason':reason})
for r in json.loads((w/'unassigned-atlas-audit.json').read_text())['issues']:
 assert 'unsupported' not in r,r
 tex=install(r['image'],r['kind'],r['expectedTexture']);bind(r['kind'],r['scene'],r['material'],['_MainTex'],tex,'original material texture')
defs=ET.parse(w.parent/'legacy_defs_Templates.xml').getroot();shared={t.find('ArtDefinition').get('ArtId')+'1':t.find('ArtDefinition').get('SharedArtId')+'2' for t in defs if t.find('ArtDefinition') is not None and t.find('ArtDefinition').get('SharedArtId')!='0'}
contracts=[r for r in json.loads((w/'shared-texture-contracts.json').read_text()) if r['source']=='second_source_effects.json' and r['scene'] in shared]
env=UnityPy.load('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/textures/premium/high');wanted={shared[r['scene']] for r in contracts};textures={o.read().m_Name:o for o in env.objects if o.type.name=='Texture2D'}
for texture in wanted:
 out=w/'SourceSharedTextures'/(texture+'.png');textures[texture].read().image.save(out)
for r in contracts:
 name=shared[r['scene']];tex=install(w/'SourceSharedTextures'/(name+'.png'),'Old/Legacy2017',name);bind('Old/Legacy2017',r['scene'],r['material'],r['properties'],tex,'SharedArtId '+name[:-1])
(w/'source-texture-repairs.json').write_text(json.dumps(repairs,indent=2));print('repaired',len(repairs),'bindings in',len({(r['kind'],r['scene']) for r in repairs}),'scenes')

from pathlib import Path
import sys,json,re,gc
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');content=p/'Assets/DynamicCards/Content'
sources={'Old/Legacy2017':'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes','Old/Thronebreaker':'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes','Latest':'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes'}
report=[];unresolved=[]
for kind,source in sources.items():
 candidates=[]
 for f in (content/kind).glob('*/conversion.json'):
  c=json.loads(f.read_text());atlas=c.get('atlas')
  if not atlas:continue
  guid=re.search(r'^guid: (\w+)',Path(str(p/atlas)+'.meta').read_text(),re.M)[1]
  assigned={a['material'] for a in c['textureAssignments'] if '_MainTex' in a['properties']}
  for m in c['materials']:
   if m['originalName'] in assigned or not (p/m['asset']).exists():continue
   body=(p/m['asset']).read_text();match=re.search(r'- _MainTex:\s*\n\s*m_Texture: \{fileID: 2800000, guid: (\w+)',body)
   if match and match[1]==guid:candidates.append((c,m))
 print(kind,'atlas on unassigned materials',len(candidates),flush=True)
 env=UnityPy.load(source);names={m['originalName'] for c,m in candidates};materials={}
 for o in env.objects:
  if o.type.name=='Material':
   d=o.read()
   if d.m_Name in names:materials.setdefault(d.m_Name,[]).append((o,d))
 for c,m in candidates:
  choices=materials.get(m['originalName'],[]);exact=[x for x in choices if c['id'] in x[0].assets_file.name];choices=exact or choices
  variants={}
  for o,d in choices:
   te=dict(d.m_SavedProperties.m_TexEnvs).get('_MainTex');t=te.m_Texture if te else None
   name=t.read().m_Name if t and t.path_id else None;variants.setdefault(name,(o,t))
  if len(variants)!=1:unresolved.append([kind,c['id'],m['originalName'],list(variants)]);continue
  name,(o,t)=next(iter(variants.items()))
  if name is None:continue # A null source slot can be populated at runtime by the client.
  row={'kind':kind,'scene':c['id'],'material':m['originalName'],'asset':m['asset'],'expectedTexture':name,'sourceFile':o.assets_file.name,'sourceId':o.path_id}
  td=t.deref()
  if td.type.name=='Texture2D':
   safe=re.sub(r'[^a-zA-Z0-9_-]','_',c['id']+'_'+m['originalName']);out=w/'SourceTextureRepair'/(safe+'.png');out.parent.mkdir(exist_ok=True);td.read().image.save(out);row['image']=str(out)
  else:row['unsupported']=td.type.name
  report.append(row)
 del env;gc.collect()
 (w/'unassigned-atlas-audit.json').write_text(json.dumps({'issues':report,'unresolved':unresolved},indent=2))
print('wrong atlas',len(report),'unresolved',len(unresolved),flush=True)

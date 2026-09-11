from pathlib import Path
import re,json,zlib,struct,collections
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());rows=[];trail_counts={};index=None
for card in cat['cards']:
 body=(p/card['prefab']).read_text();objects={i:(ty,b) for ty,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',body,re.S)}
 particles={re.search(r'm_GameObject: \{fileID: (-?\d+)',b)[1]:b for ty,b in objects.values() if ty=='198'};unused=0
 for i,(ty,b) in objects.items():
  if ty!='199':continue
  go=re.search(r'm_GameObject: \{fileID: (-?\d+)',b)[1];materials=re.search(r'  m_Materials:\n(.*?)(?=\n  \w)',b,re.S)
  slots=re.findall(r'fileID: (-?\d+)',materials[1]) if materials else []
  if len(slots)>1 and slots[1]=='0' and re.search(r'TrailModule:\s*\n\s*enabled: 0',particles.get(go,'')):unused+=1
 trail_counts[card['id']]=unused
 for ident,(ty,b) in objects.items():
  if ty!='137':continue
  mb=re.search(r'  m_Bones:([^\n]*)(.*?)(?=\n  m_BlendShapeWeights:)',b,re.S)
  if not mb:continue
  bone_ids=re.findall(r'fileID: (-?\d+)',mb[0]);mesh=re.search(r'm_Mesh: \{fileID: 4300000, guid: (\w+)',b)
  if bone_ids and '0' not in bone_ids:continue
  if not mesh:continue
  if index is None:
   index={}
   for meta in (p/'Assets/DynamicCards/Content').rglob('*.asset.meta'):
    guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M)
    if guid:index[guid[1]]=Path(str(meta)[:-5])
  asset=index.get(mesh[1]);text=asset.read_text() if asset else '';mh=re.search(r'  m_BoneNameHashes: ([0-9a-f]+)',text)
  if not mh:continue
  hashes=list(struct.unpack('<'+'I'*(len(mh[1])//8),bytes.fromhex(mh[1])));names={i:re.search(r'  m_Name: (.*)',ob)[1].strip('"') for i,(ot,ob) in objects.items() if ot=='1'};transforms={i:(re.search(r'm_GameObject: \{fileID: (-?\d+)',ob)[1],re.search(r'm_Father: \{fileID: (-?\d+)',ob)[1]) for i,(ot,ob) in objects.items() if ot=='4'}
  def path(i):
   go,parent=transforms[i];return (path(parent)+'/' if parent!='0' else '')+names[go]
  candidates=collections.defaultdict(set)
  for i in transforms:
   parts=path(i).split('/')
   for start in range(len(parts)):candidates[zlib.crc32('/'.join(parts[start:]).encode())].add(i)
  matched=[next(iter(candidates[h])) if len(candidates[h])==1 else None for h in hashes]
  rows.append({'scene':card['id'],'artIds':card['artIds'],'prefab':card['prefab'],'renderer':ident,'mesh':str(asset),'bones':bone_ids,'hashes':hashes,'matched':matched,'unresolved':sum(v is None for v in matched)})
(w/'empty-skin-audit.json').write_text(json.dumps(rows,indent=2));(w/'unused-trail-slot-counts.json').write_text(json.dumps(trail_counts));print([(r['scene'],len(r['bones']),len(r['hashes']),r['unresolved']) for r in rows],flush=True)

from pathlib import Path
import json,re
w=Path(__file__).resolve().parent;p=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');cat=json.loads((p/'Assets/DynamicCards/Content/catalog.json').read_text());rows=[]
for card in cat['cards']:
 f=p/card['prefab'];blocks={i:(ty,b) for ty,i,b in re.findall(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',f.read_text(),re.S)}
 def ref(b,key):
  m=re.search(r'\b'+key+r': \{fileID: (-?\d+)',b);return m[1] if m else '0'
 def path(t):
  b=blocks[t][1];go=blocks[ref(b,'m_GameObject')][1];name=re.search(r'\bm_Name: ([^\n]*)',go)[1];parent=ref(b,'m_Father');return (path(parent)+'/' if parent!='0' else '')+name
 for i,(ty,b) in blocks.items():
  if ty not in ['23','137','199'] or 'm_Enabled: 1' not in b:continue
  section=b.split('m_Materials:',1)[1].split('m_StaticBatchInfo:',1)[0];mats=re.findall(r'fileID: (-?\d+)',section)
  if any(x!='0' for x in mats):continue
  if ty=='199' and 'm_RenderMode: 5' in b:continue
  go=blocks[ref(b,'m_GameObject')][1];components=re.findall(r'component: \{fileID: (-?\d+)',go);tr=next(x for x in components if blocks[x][0]=='4')
  if ty=='23':
   mf=next((blocks[x][1] for x in components if blocks[x][0]=='33'),None)
   if mf is None or ref(mf,'m_Mesh')=='0':continue
  rows.append({'scene':card['id'],'prefab':card['prefab'],'rendererId':i,'type':ty,'path':path(tr),'materialCount':len(mats)})
(w/'unassigned-surfaces.json').write_text(json.dumps(rows,indent=2));print('surfaces',len(rows));print(rows)

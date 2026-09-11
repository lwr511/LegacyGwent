from pathlib import Path
import sys,json,zlib,re,collections
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
w=Path(__file__).parent;base=w.parent;root=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
rows=json.loads((w/'active-euler-clips.json').read_text(encoding='utf-8'))['clips'];docs={n:json.loads((base/n/'animations.json').read_text(encoding='utf-8')) for n in ['LegacyAnimationData','NativeAnimationData','LatestAnimationData']}
def safe(n):return ''.join(c if c.isalnum() or c in '_-' else '_' for c in n)
def reachable_motions(text):
 docs=re.split(r'^--- !u!',text,flags=re.M)[1:];blocks={int(re.match(r'\d+ &(-?\d+)',d)[1]):d for d in docs};todo=[i for i,d in blocks.items() if d.startswith('91 ')];visited=set();found=set()
 while todo:
  ident=todo.pop()
  if ident in visited:continue
  visited.add(ident);block=blocks[ident]
  found.update(re.findall(r'm_Motion: \{fileID: [^,]+, guid: ([0-9a-f]+)',block))
  for value in re.findall(r'\{fileID: (-?\d+)\}',block):
   value=int(value)
   if value in blocks and value not in visited:todo.append(value)
 return found
active=[]
for row in rows:
 folder=(root/row['asset']).parent;prefab=(folder/'Card.prefab').read_text(encoding='utf-8');guids=set(re.findall(r'm_Controller: \{fileID: [^,]+, guid: ([0-9a-f]+)',prefab));motions=set()
 for meta in folder.glob('*.controller.meta'):
  guid=re.search(r'guid: (\w+)',meta.read_text(encoding='utf-8'))[1]
  if guid in guids:motions.update(reachable_motions(meta.with_suffix('').read_text(encoding='utf-8')))
 guid=re.search(r'guid: (\w+)',Path(str(root/row['asset'])+'.meta').read_text(encoding='utf-8'))[1]
 if guid in motions:active.append(row)
rows=active
for row in rows:
 kind='LegacyAnimationData' if '/Legacy2017/' in row['asset'] else 'NativeAnimationData' if '/Thronebreaker/' in row['asset'] else 'LatestAnimationData';row['data']=kind;matches=[]
 for rec in docs[kind]['animators']:
  if rec['id']!=row['scene']:continue
  for cl in rec['clips']:
   expected=f'Source_{zlib.crc32(rec["path"].encode()):08x}_{safe(cl["name"])}.anim'
   if row['asset'].endswith('/'+expected):matches.append((rec,cl))
 if len(matches)!=1:raise Exception(('ambiguous',row['asset'],len(matches)))
 rec,cl=matches[0];row['sourceClip']=cl['file'];row['tracks']=[t for t in cl['tracks'] if t['attribute']==4 and t.get('typeId',0)==0]
legacy='E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes';native='C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes';latest=Path('C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')
for kind,source in [('LegacyAnimationData',legacy),('NativeAnimationData',native),('LatestAnimationData',latest)]:
 targets=[r for r in rows if r['data']==kind]
 if not targets:continue
 print('source',kind,len(targets),flush=True)
 if kind!='LatestAnimationData':env=UnityPy.load(source);objects={(zlib.crc32(o.assets_file.name.encode()),o.path_id):o for o in env.objects if o.type.name=='AnimationClip'}
 for row in targets:
  ids=row['sourceClip'].removesuffix('.bin').split('_');pid=int(ids[-1]);crc=int(ids[-2]) if len(ids)>2 else None
  if kind=='LatestAnimationData':
   env=UnityPy.load(str(latest/row['scene']));candidates=[o for o in env.objects if o.type.name=='AnimationClip' and o.path_id==pid]
   if len(candidates)!=1:raise Exception(('missing latest clip',row['sourceClip']))
   obj=candidates[0]
  else:
   candidates=[o for (c,i),o in objects.items() if i==pid and (c==crc if crc is not None else row['scene'] in o.assets_file.name)]
   if len(candidates)!=1:
    row['unresolved']='source clip mapping';print('UNRESOLVED_SOURCE',row['asset'],row['sourceClip'],len(candidates),flush=True);continue
   obj=candidates[0]
  data=obj.read_typetree();bindings={b['path']:b for b in data['m_ClipBindingConstant']['genericBindings'] if b['typeID']==4 and b['attribute']==4};orders=[]
  for path in row['eulerPaths']:
   actual=path.strip('"');matches=[t for t in row['tracks'] if actual==t['path'] or actual.endswith('/'+t['path'])]
   if not matches:matches=[t for t in row['tracks'] if re.sub(r' \d+$','',actual.split('/')[-1])==re.sub(r' \d+$','',t['path'].split('/')[-1])]
   if len(matches)!=1:raise Exception(('track mapping',row['asset'],actual,matches))
   track=matches[0];h=zlib.crc32(track['path'].encode());h=0 if track['path']=='' else h;b=bindings[h];order=b['customType'];order=order-10 if 10<=order<=15 else order
   if order not in range(6):print('UNKNOWN_ORDER',kind,row['asset'],path,order,flush=True)
   orders.append({'path':actual,'sourcePath':track['path'],'order':order,'hash':h})
  row['orders']=orders
  row.pop('tracks',None)
(w/'source-euler-orders.json').write_text(json.dumps({'clips':rows},indent=2),encoding='utf-8');bad=[r for r in rows if any(o['order']!=4 for o in r.get('orders',[]))];print('rotation counts',collections.Counter(o['order'] for r in rows for o in r.get('orders',[])));print('changes',len(bad),'clips',len({r['scene'] for r in bad}),'cards');print([(r['scene'],[(o['path'].split('/')[-1],o['order']) for o in r['orders'] if o['order']!=4]) for r in bad])

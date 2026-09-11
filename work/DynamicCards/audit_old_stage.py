from pathlib import Path
import json,re,collections,concurrent.futures
W=Path(__file__).resolve().parent; S=W/'OldSourcesStage'; P=W.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
cards=json.loads((S/'Assets/DynamicCards/Content/catalog.json').read_text())['cards']
files=list((S/'Assets').rglob('*')); index={};dups=[]
def readmeta(p):
 m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)
 return (m[1],str(p)) if m else None
metas=[p for p in files if p.suffix=='.meta']+list((P/'Assets/DynamicCards/Runtime').glob('*.meta'))+list((P/'Assets/DynamicCards/Shaders').rglob('*.meta'))
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
 for row in pool.map(readmeta,metas):
  if not row:continue
  guid,path=row
  if guid in index and path.replace(str(S),str(P))!=index[guid].replace(str(S),str(P)):dups.append((guid,index[guid],path))
  index[guid]=path
def check(p):
 try:body=p.read_text(encoding='utf-8-sig')
 except UnicodeDecodeError:return []
 return [(str(p),g) for g in set(re.findall(r'guid: (\w{32})',body)) if g not in index and not g.startswith('00000000')]
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
 missing=[x for rows in pool.map(check,[p for p in files if p.suffix in ('.prefab','.anim','.controller','.mat','.asset')]) for x in rows]
arts=set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"',(W.parents[1]/'src/Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf-8-sig')))
bound=collections.Counter(a for c in cards for a in c['artIds'])
result=dict(cards=len(cards),sources=dict(collections.Counter(c['sourceVersion'] for c in cards)),projectArts=len(arts),covered=len(arts&bound.keys()),missingArts=sorted(arts-bound.keys()),duplicateArtIds=[a for a,n in bound.items() if n>1],duplicateGuids=dups,missingGuids=missing,bytes=sum(p.stat().st_size for p in files if p.is_file()),extensions=dict(collections.Counter(p.suffix for p in files if p.is_file())))
(S/'audit.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2),flush=True)

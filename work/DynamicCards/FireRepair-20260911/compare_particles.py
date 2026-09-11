exec(open(__file__.replace('compare_particles.py','inspect_fire.py')).read().split("print('Loading source'")[0])
import yaml
from PIL import Image,ImageChops
def read_prefab(path):
 text=path.read_text(); blocks={}
 for typ,id,body in re.findall(r'--- !u!(\d+) &(\d+)\n(.*?)(?=--- !u!|\Z)',text,re.S):
  obj=yaml.safe_load(body);blocks[int(id)]=(int(typ),next(iter(obj.values())))
 return blocks
result=[]
for kind,cid,sourcepath in [('Legacy2017','12110301',source),('Thronebreaker','11870100',Path(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes'))]:
 print('Loading',kind,flush=True);e=UnityPy.load(str(sourcepath));src=[]
 for o in e.objects:
  if o.type.name not in ['ParticleSystem','ParticleSystemRenderer','Transform']:continue
  if cid not in o.assets_file.name:continue
  d=o.read_typetree();go=o.read().m_GameObject.deref().read();name=go.m_Name
  if 'fire' not in name.lower() and 'flame' not in name.lower():continue
  src.append(dict(type=o.type.name,name=name,data=d))
 blocks=read_prefab(P/f'Assets/DynamicCards/Content/Old/{kind}/{cid}/Card.prefab')
 dst=[]
 for typ,d in blocks.values():
  if typ not in [198,199,4]:continue
  name=str(blocks[d['m_GameObject']['fileID']][1]['m_Name'])
  if 'fire' in name.lower() or 'flame' in name.lower():dst.append(dict(type={198:'ParticleSystem',199:'ParticleSystemRenderer',4:'Transform'}[typ],name=name,data=d))
 (W/(kind+'-source-particles.json')).write_text(json.dumps(src,indent=2));(W/(kind+'-current-particles.json')).write_text(json.dumps(dst,indent=2))
 for row in src:
  current=next((x for x in dst if x['type']==row['type'] and x['name']==row['name']),None)
  if current is None:print('MISSING',row['name'],flush=True);continue
  a=row['data'];b=current['data']
  def walk(x,y,path):
   if isinstance(x,dict) and isinstance(y,dict):
    for k in x.keys()&y.keys():walk(x[k],y[k],path+'/'+k)
   elif x!=y and not isinstance(x,(dict,list)) and not isinstance(y,(dict,list)):
    if any(s in path for s in ['fileID','pathID','serializedVersion','ObjectHideFlags']):return
    if isinstance(x,(int,float)) and isinstance(y,(int,float)) and abs(x-y)<1e-4:return
    result.append(dict(kind=kind,name=row['name'],type=row['type'],path=path,source=x,current=y))
  walk(a,b,'')
(W/'particle-diffs.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2),flush=True)

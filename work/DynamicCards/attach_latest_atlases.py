from pathlib import Path
import json,shutil,re
root=Path(__file__).resolve().parent
client=root/'Probe';content=client/'Assets/DynamicCards/Content/Latest'
template=(content/'Shared/10130101_1818304__10130300_Villentretenmerth_FirTree.png.meta').read_text()
for metadata in content.glob('*/conversion.json'):
 data=json.loads(metadata.read_text());ident=data['id'][:-2]+'00'
 source=root/'LatestAtlases'/(ident+'.png')
 if not source.exists():
  print('MISSING_ATLAS',data['id']);continue
 target=content/'Atlases'/source.name;target.parent.mkdir(exist_ok=True)
 if not target.exists():
  shutil.copy2(source,target);shutil.copy2(str(source)+'.meta',str(target)+'.meta')
 meta=Path(str(target)+'.meta');guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1]
 meta.write_text(re.sub(r'^guid: \w+', 'guid: '+guid,template,flags=re.M))
 data['atlas']=target.relative_to(client).as_posix()
 metadata.write_text(json.dumps(data,indent=2))
 print('ATTACHED',data['id'])

import sys,json,re,shutil
from pathlib import Path
root=Path(__file__).resolve().parent
client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
dest=root/'Probe'
guids={}
for p in (client/'Assets/DynamicCards').rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',p.read_text(),re.M)
    if m:guids[m[1]]=Path(str(p)[:-5])
done=set()
def copy(p):
    if p in done:return
    done.add(p)
    out=dest/p.relative_to(client);out.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(p,out)
    if Path(str(p)+'.meta').exists():shutil.copy2(str(p)+'.meta',str(out)+'.meta')
    if p.suffix in ['.prefab','.asset','.mat','.controller','.anim']:
        for guid in set(re.findall(r'guid: ([a-f0-9]{32})',p.read_text())):
            if guid in guids:copy(guids[guid])
catalog=json.loads((client/'Assets/DynamicCards/Content/catalog.json').read_text())
selected=[c for c in catalog['cards'] if '--all' in sys.argv or c['id'] in set(sys.argv[1:]+['15660100'])]
for c in selected:
    copy(client/c['prefab'])
    conversion=(client/c['prefab']).parent/'conversion.json'
    if conversion.exists():
        copy(conversion)
        info=json.loads(conversion.read_text())
        for material in info['materials']:copy(client/material['asset'])
        for vat in info.get('vertexAnimations',[]):copy(client/vat['data'])
        for effect in info.get('candles',[])+info.get('lightning',[]):copy(client/effect['texture'])
    if c.get('audio'):copy(client/c['audio'])
(dest/'Assets/DynamicCards/Content/catalog.json').write_text(json.dumps({'version':1,'cards':selected},indent=2))
print('COPIED',len(done),len(selected))

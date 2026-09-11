import json,re,shutil
from pathlib import Path
root=Path(__file__).resolve().parent
source=root/'Bridge2022/Assets/PortableCards'
client=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
dest=client/'Assets/DynamicCards/Content'
guids={}
for meta in source.rglob('*.meta'):
    m=re.search(r'^guid: (\w+)',meta.read_text(),re.M)
    if m:guids[m[1]]=Path(str(meta)[:-5])
copied=set()
def copy(path):
    out=dest/path.relative_to(source)
    if out.exists() or path in copied:return
    copied.add(path);out.parent.mkdir(parents=True,exist_ok=True)
    if path.suffix in ['.mat','.asset','.prefab','.controller','.anim']:
        text=path.read_text()
        if path.suffix=='.mat':
            text=re.sub(r'm_Shader: \{[^\n]+\}','m_Shader: {fileID: 0}',text)
            text=re.sub(r'\{fileID: \d+, guid: 0{32}, type: 0\}','{fileID: 0}',text)
        for guid in set(re.findall(r'guid: ([a-f0-9]{32})',text)):
            if guid in guids:copy(guids[guid])
        out.write_text(text,encoding='utf8')
    else:shutil.copy2(path,out)
    if Path(str(path)+'.meta').exists():shutil.copy2(str(path)+'.meta',str(out)+'.meta')
for path in dest.glob('*/conversion.json'):
    for item in json.loads(path.read_text())['materials']:
        relative=item['asset'].split('Content/',1)[1]
        copy(source/relative)
fixed=0
for path in dest.rglob('*.mat'):
    text=path.read_text();new=re.sub(r'\{fileID: \d+, guid: 0{32}, type: 0\}','{fileID: 0}',text)
    if text!=new:path.write_text(new,encoding='utf8');fixed+=1
print('copied',len(copied),'cleared incompatible native builtin pointers',fixed)

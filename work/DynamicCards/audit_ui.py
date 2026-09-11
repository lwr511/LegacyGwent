from pathlib import Path
import re
p=Path('src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/Resources/Scenes/Game.unity')
s=p.read_text(encoding='utf-8-sig')
d={m[1]:m[2] for m in re.findall(r'--- !u!(\d+) &(\d+)\n(.*?)(?=--- !u!|\Z)',s,re.S)}
def ref(t,key):return re.search(key+r': \{fileID: (\d+)',t)[1]
go='770864342'
tr=re.search(r'component: \{fileID: (\d+)',d[go])[1]
for _ in range(3):
    print('NODE',d[ref(d[tr],'m_GameObject')][:450]);print(d[tr][-500:])
    tr=ref(d[tr],'m_Father')
print('CHILDREN')
for child in re.findall(r'- \{fileID: (\d+)\}',d[tr]):
    print(d[ref(d[child],'m_GameObject')][:400])

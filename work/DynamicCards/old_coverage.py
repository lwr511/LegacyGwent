import json,re,pathlib
w=pathlib.Path(__file__).resolve().parent
c=json.loads((w/'OldSourcesStage/catalog-input.json').read_text())['cards']
a={x for z in c for x in z['artIds']}
p=set(re.findall(r'CardArtsId\s*=\s*"([^"]+)"',(w.parents[1]/'src/Cynthia.Card/src/Cynthia.Card.Common/GwentGame/GwentMap.cs').read_text(encoding='utf-8-sig')))
print('SCENES',len(c),'ALIASES',len(a),'COVERED',len(a&p),'MISSING',len(p-a))
print('GERALT',next(z for z in c if z['id']=='10090100')['introDuration'])

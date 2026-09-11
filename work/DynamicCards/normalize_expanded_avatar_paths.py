from pathlib import Path
import re,json
base=Path(__file__).resolve().parent
primary=base.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
def decode_name(value):
    if value.startswith('"'):return json.loads(value)
    if value.startswith("'"):return value[1:-1].replace("''", "'")
    return value
changes=[];unresolved=[]
for project in [primary,base/'Probe']:
    content=project/'Assets/DynamicCards/Content';catalog=json.loads((content/'catalog.json').read_text())
    for card in catalog['cards']:
        if '/Legacy2017/' not in card['prefab']:continue
        prefab=project/card['prefab'];names={};transforms={}
        for block in re.split(r'(?m)^--- !u!',prefab.read_text())[1:]:
            m=re.match(r'(\d+) &(\d+)',block)
            if not m:continue
            typ,ident=map(int,m.groups())
            if typ==1:names[ident]=decode_name(re.search(r'  m_Name: (.*)',block)[1])
            if typ==4:transforms[ident]=(int(re.search(r'm_GameObject: \{fileID: (\d+)',block)[1]),int(re.search(r'm_Father: \{fileID: (\d+)',block)[1]))
        def path(t):
            g,parent=transforms[t]
            return path(parent)+[names[g]] if parent in transforms else []
        paths=[path(t) for t in transforms];strings={'/'.join(x) for x in paths}
        def subsequence(short,long):
            iterator=iter(long)
            return all(any(x==y for y in iterator) for x in short)
        def fix(value):
            if isinstance(value,dict):return {k:fix(v) for k,v in value.items()}
            if isinstance(value,list):return [fix(v) for v in value]
            if not isinstance(value,str) or '/' not in value or value.startswith('Assets/') or value in strings:return value
            parts=value.split('/')
            if parts[0]!=card['id']:return value
            candidates=['/'.join(p) for p in paths if p and p[-1]==parts[-1] and subsequence(parts,p)]
            if len(candidates)==1:
                changes.append([card['id'],value,candidates[0]]);return candidates[0]
            unresolved.append([card['id'],value,len(candidates)]);return value
        metadata=prefab.parent/'conversion.json'
        metadata.write_text(json.dumps(fix(json.loads(metadata.read_text())),indent=2),encoding='utf8')
        updated=fix(card);card.clear();card.update(updated)
    (content/'catalog.json').write_text(json.dumps(catalog,indent=2),encoding='utf8')
(base/'expanded_avatar_paths.json').write_text(json.dumps(dict(changes=changes,unresolved=unresolved),indent=2))
print('EXPANDED_AVATAR_PATHS',len(changes),'unresolved',len(unresolved))

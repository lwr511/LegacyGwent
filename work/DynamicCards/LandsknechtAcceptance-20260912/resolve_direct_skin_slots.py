from pathlib import Path
from collections import defaultdict,Counter
import sys,json,re,gc
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
first=json.loads((W/'skin-source-resolution.json').read_text());catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text())['cards'];cards={str(P/c['prefab']):c for c in catalog}
sources={'Thronebreaker':Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes'),'Legacy2017':Path('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),'Latest':Path('C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')}
def kind(row):return 'Thronebreaker' if '/Thronebreaker/' in row['file'].replace('\\','/') else 'Legacy2017' if '/Legacy2017/' in row['file'].replace('\\','/') else 'Latest'
contracts=[]
for source,file in sources.items():
    wanted=defaultdict(set)
    for row in first['unresolved']:
        if kind(row)!=source:continue
        c=cards[row['file']];wanted[c.get('sourceId') or c['id']].add(row['skin'])
    for f in ([file/i for i in wanted] if source=='Latest' else [file]):
        env=UnityPy.load(str(f));cache={}
        def path(ptr):
            if not ptr.path_id:return ''
            obj=ptr.deref();key=(obj.assets_file.name,obj.path_id)
            if key not in cache:
                tr=obj.read();cache[key]=(path(tr.m_Father)+'/' if tr.m_Father.path_id else '')+tr.m_GameObject.read().m_Name
            return cache[key]
        for o in env.objects:
            match=re.search(r'\d{8}',o.assets_file.name)
            if not match or match[0] not in wanted or o.type.name!='SkinnedMeshRenderer':continue
            d=o.read();name=d.m_GameObject.read().m_Name
            if name not in wanted[match[0]] or not d.m_Bones:continue
            mesh=d.m_Mesh.read_typetree()
            contracts.append(dict(source=source,id=match[0],skin=name,mesh=mesh['m_Name'],hashes=mesh.get('m_BoneNameHashes',[]),bones=[path(b) for b in d.m_Bones]))
        del env;gc.collect()
    print(source,'direct skin contracts',sum(c['source']==source for c in contracts),flush=True)
(W/'source-direct-skins.json').write_text(json.dumps(contracts,indent=2))
bykey=defaultdict(list)
for c in contracts:bykey[(c['source'],c['id'],c['skin'])].append(c)
resolved=[];remaining=[];stats=Counter()
for row in first['unresolved']:
    if row['problem']=='bone-count':remaining.append(row);continue
    c=cards[row['file']];source=kind(row);sid=c.get('sourceId') or c['id'];slot=row['slot']
    candidates=bykey[(source,sid,row['skin'])]
    candidates=[c for c in candidates if slot<len(c['bones']) and slot<len(c['hashes']) and c['hashes'][slot]==row['hash']]
    expected=sorted({c['bones'][slot] for c in candidates})
    if row['problem']=='null-bone' and expected==['']:
        resolved.append(dict(row,classification='null-in-original-source'));stats['originalNullSlots']+=1
    elif len(expected)==1 and expected[0] and row.get('path','').split('/')[-1]==expected[0].split('/')[-1]:
        resolved.append(dict(row,sourcePath=expected[0],classification='same-direct-source-bone-slot'));stats['directSourceBoneMatches']+=1
    else:remaining.append(dict(row,directSourcePaths=expected))
result=dict(complete=True,totals=dict(stats),resolved=resolved,remaining=remaining)
(W/'skin-direct-resolution.json').write_text(json.dumps(result,indent=2))
print('COMPLETE',dict(stats),'remaining',len(remaining),flush=True)

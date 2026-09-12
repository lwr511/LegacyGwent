from pathlib import Path
from collections import defaultdict,Counter
import sys,json,re,gc,time
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).resolve().parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
first=json.loads((W/'skin-path-hash-audit.json').read_text());catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text())['cards'];cards={str(P/c['prefab']):c for c in catalog}
sources={'Thronebreaker':Path('C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes'),'Legacy2017':Path('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes'),'Latest':Path('C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/bundledassets/cardassets/scenes')}
def kind(row):return 'Thronebreaker' if '/Thronebreaker/' in row['file'].replace('\\','/') else 'Legacy2017' if '/Legacy2017/' in row['file'].replace('\\','/') else 'Latest'
maps={};errors=[]
for source,file in sources.items():
    wanted={cards[r['file']].get('sourceId') or cards[r['file']]['id'] for r in first['issues'] if kind(r)==source}
    maps[source]=defaultdict(lambda:defaultdict(set))
    for f in ([file/i for i in wanted] if source=='Latest' else [file]):
        env=UnityPy.load(str(f))
        for o in env.objects:
            match=re.search(r'\d{8}',o.assets_file.name)
            if not match or match[0] not in wanted or o.type.name!='Animator':continue
            d=o.read()
            if not d.m_Avatar.path_id:continue
            for h,p in d.m_Avatar.read_typetree().get('m_TOS',[]):maps[source][match[0]][str(h)].add(p)
        del env;gc.collect()
    print(source,'source avatars',len(maps[source]),flush=True)
serialized={s:{i:{h:sorted(v) for h,v in hashes.items()} for i,hashes in ids.items()} for s,ids in maps.items()}
(W/'source-avatar-paths.json').write_text(json.dumps(serialized,indent=2))
resolved=[];unresolved=[];stats=Counter()
for row in first['issues']:
    if row['problem']!='path-hash-mismatch':unresolved.append(row);continue
    c=cards[row['file']];source=kind(row);sid=c.get('sourceId') or c['id'];paths=serialized[source].get(sid,{}).get(str(row['hash']),[])
    matches=[p for p in paths if p.split('/')[-1]==row['path'].split('/')[-1]]
    if len(paths)==1 and len(matches)==1:
        stats['sourceBoneNameMatches']+=1;resolved.append(dict(row,sourcePath=paths[0],classification='same-source-bone-with-restored-hierarchy'))
    else:unresolved.append(dict(row,sourcePaths=paths));stats['needsFurtherSourceCheck']+=1
result=dict(complete=True,totals=dict(stats),resolved=resolved,unresolved=unresolved,errors=errors)
(W/'skin-source-resolution.json').write_text(json.dumps(result,indent=2))
print('COMPLETE',dict(stats),'remaining',len(unresolved),flush=True)

from pathlib import Path
import json,shutil,re,hashlib,struct,datetime
W=Path(__file__).resolve().parent; P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
assert (W/'main-bundle-build.txt').read_text().startswith('COMPLETE'), 'Wait for current build to finish'
catalog=P/'Assets/DynamicCards/Content/catalog.json';raw=catalog.read_text(encoding='utf-8-sig');data=json.loads(raw)
assert not any('c10000100' in c['artIds'] for c in data['cards'])
entry=next(c for c in data['cards'] if c['id']=='16580100');assert entry['artIds']==[]
candidate=json.loads((W/'cropped-rematch/candidates.json').read_text())['c10000100'][0]
assert candidate['source']=='Native' and candidate['scene']=='16580100' and candidate['error']<.0005
pattern=r'("id": "16580100",\s*"artIds": )\[\]'
updated,n=re.subn(pattern,lambda m:m[1]+'[\n        "c10000100"\n      ]',raw);assert n==1
anim=P/'Assets/DynamicCards/Content/Latest/13860101/Source_9e8a2ac9_VFXLoop.anim';animraw=anim.read_text()
assert animraw.count('m_LoopTime: 1')==1
# Both original binary tracks are constant; restore the source loop flag.
buf=(W.parent/'LatestAnimationData/13860101_21.bin').read_bytes();values=struct.unpack('<'+'f'*(len(buf)//4),buf)
assert len(set(values[::2]))==1 and len(set(values[1::2]))==1
changes=[]
for path,text in [(catalog,updated),(anim,animraw.replace('m_LoopTime: 1','m_LoopTime: 0'))]:
    backup=W/'MainMergeBackup'/(path.name+'.before-final-findings')
    assert not backup.exists();shutil.copy2(path,backup)
    before=hashlib.sha256(path.read_bytes()).hexdigest();path.write_text(text,encoding='utf-8')
    changes.append(dict(path=str(path),backup=str(backup),before=before,after=hashlib.sha256(path.read_bytes()).hexdigest()))
(W/'final-source-findings-applied.json').write_text(json.dumps(dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),changes=changes,candidate=candidate,irisLoopSource=False,irisTracksConstant=True),indent=2))
request=json.loads((W/'postmerge-ui-request.json').read_text());game=json.loads((W/'reported-main-ui/game-card-map.json').read_text(encoding='utf-8-sig'))['cards']
newids=[c['card'] for c in game if c['art'] in ['c10000100','20008300']]
request['cards']=list(dict.fromkeys(request['cards']+newids));(W/'postmerge-ui-request.json').write_text(json.dumps(request,indent=2))
print('APPLIED final findings; UI cards',len(request['cards']), 'added',newids)

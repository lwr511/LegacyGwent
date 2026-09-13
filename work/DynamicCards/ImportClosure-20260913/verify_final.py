from pathlib import Path
import json,hashlib,sys,re
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
W=Path(__file__).parent;P=Path('C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');B=Path('C:/UnityProjects/LegacyGwent/Builds/Windows-20260913-FlashCardsFinal/DiyGwent_Data/StreamingAssets');C=P/'Library/DynamicCardsBundles/StandaloneWindows64'
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
 return h.hexdigest()
assert not (P/'Assets/StreamingAssets/CollectionDebug').exists() and not (B/'CollectionDebug').exists()
for file in ['Assets/Script/EditorMenu/EditorInfo.cs','Assets/DynamicCards/Runtime/DynamicCardLibrary.cs','Assets/DynamicCards/Runtime/DynamicCardSettings.cs']:
 assert not re.search('CollectionDebug|InspectionOverride|LoadDebugCatalog|DebugKey',(P/file).read_text(encoding='utf8'))
assert not list((P/'Assets').rglob('CollectionDebug*.cs'))
catalog=json.loads((P/'Assets/DynamicCards/Content/catalog.json').read_text());cards=catalog['cards'];aliases=[a for c in cards for a in c.get('artIds',[])];assert len(cards)==677 and len(aliases)==len(set(aliases))==670
retired={r['remove'].lower() for r in json.loads((W.parent/'CollectionCleanup-20260913/consolidation.json').read_text(encoding='utf8'))['duplicates']};assert len(retired)==63
index=json.loads((B/'DynamicCards/cards.index.json').read_text());prefabs=[f.lower() for part in index['parts'] for f in part['prefabs']];assert len(prefabs)==677 and set(prefabs)=={c['prefab'].lower() for c in cards} and not retired.intersection(prefabs)
expected={'cards.bundle','cards.index.json'}|{part['file'] for part in index['parts']};assert {f.name for f in (B/'DynamicCards').iterdir() if f.is_file()}==expected
checks=[]
for name in sorted(expected):
 file=B/'DynamicCards'/name;actual=sha(file);assert actual==sha(C/name),name;checks.append(dict(file=name,bytes=file.stat().st_size,sha256=actual))
env=UnityPy.load(str(B/'DynamicCards/cards.bundle'));texts=[o.read() for o in env.objects if o.type.name=='TextAsset'];assert [json.loads(t.m_Script) for t in texts if t.m_Name=='catalog']==[catalog]
code=json.loads((W/'player-code-check.json').read_text(encoding='utf-8-sig'));assert code['debugTypes']==0 and code['debugLibraryMethods']==0 and code['inspectionOverride']==False and code['normalCollectionCall']==True
result=dict(status='PASS',scenes=677,artMappings=670,retiredDuplicates=63,debugReferenceFiles=0,payloadFiles=len(checks),payloadBytes=sum(c['bytes'] for c in checks),playerAssemblySha256=code['sha256'],files=checks)
(W/'delivery-check.json').write_text(json.dumps(result,indent=2));print({k:v for k,v in result.items() if k!='files'})

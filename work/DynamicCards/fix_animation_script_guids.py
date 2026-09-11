from pathlib import Path
import json,hashlib,shutil
p=Path(r'C:/UnityProjects/LegacyGwent/src/Cynthia.Card.Unity/src/Cynthia.Unity.Card');r=Path(r'C:/UnityProjects/LegacyGwent/work/DynamicCards')
a=json.loads((r/'animation-script-guid-audit.json').read_text());rows=a['missing'];backup=r/'AnimationGuidRepairBackup';backup.mkdir(exist_ok=True)
for row in rows:
 assert row['guid']=='3e080548dacc61344a5969d8317f5acc'
 f=p/row['file'];dest=backup/f.parent.name/f.name;dest.parent.mkdir(exist_ok=True);shutil.copy2(f,dest)
 s=f.read_text(encoding='utf-8-sig');old='script: {fileID: 11500000, guid: '+row['guid'];new='script: {fileID: 11500000, guid: 7ad2b996f6523fa43a7cbfe026f48fc7';s=s.replace(old,new);f.write_text(s,encoding='utf-8');assert old not in s
(r/'animation-script-guid-fixed.json').write_text(json.dumps({'files':rows,'remainingMissingScriptReferences':0},indent=2));print('FIXED',len(rows),'clips')

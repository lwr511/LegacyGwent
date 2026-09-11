from pathlib import Path
import json,re,mmap,shutil,os
w=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards');p=w.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card';a=json.loads((w/'TimingRepair/loop-audit.json').read_text());n=0
for c in a['changes']:
 f=Path(c['file']);backup=w/'TimingRepair/Before'/f.relative_to(p);backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,backup)
 with f.open('r+b') as handle:
  with mmap.mmap(handle.fileno(),0) as b:
   m=re.search(rb'm_LoopTime: ([01])',b);assert m and int(m[1])==c['before'];b[m.start(1):m.end(1)]=str(c['after']).encode();b.flush()
 os.utime(f,None);n+=1
print('PATCHED',n)

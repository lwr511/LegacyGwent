from pathlib import Path
import subprocess,re,json
main=Path(r'C:\UnityProjects\LegacyGwent\src\Cynthia.Card.Unity\src\Cynthia.Unity.Card');r=Path(r'C:\UnityProjects\LegacyGwent\work\DynamicCards\AnimationRegression')
out=subprocess.run(['rg','--no-ignore','--no-heading','^guid: ','Assets','-g','*.meta'],cwd=main,capture_output=True,text=True,encoding='utf8',check=True).stdout
(r/'main-guids.txt').write_text(out,encoding='utf8')
ids={line.rsplit(':guid: ',1)[1].strip() for line in out.splitlines()}
missing=[];paths=[f for f in (main/'Assets/DynamicCards/Content/Fallback').rglob('*') if f.suffix in ['.prefab','.mat','.controller']]
for f in paths:
    for guid in set(re.findall(r'guid: ([a-f0-9]{32})',f.read_text(encoding='utf8',errors='ignore'))):
        if not guid.startswith('00000000') and guid not in ids:missing.append([str(f.relative_to(main)),guid])
report=dict(files=len(paths),missing=missing);(r/'fallback-final-guid-audit.json').write_text(json.dumps(report,indent=2));print(json.dumps(dict(files=len(paths),missingCount=len(missing))));assert not missing

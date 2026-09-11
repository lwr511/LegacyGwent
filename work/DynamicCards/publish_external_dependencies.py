from pathlib import Path
import json,shutil
r=Path(__file__).resolve().parent;probe=r/'Probe';client=r.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
plan=json.loads((r/'client_external_dependency_plan.json').read_text());rows=[]
for guid,relative in plan['found'].items():
 source=probe/relative
 target=client/'Assets/DynamicCards/Content/CompatibilityDependencies'/Path(relative).relative_to('Assets')
 target.parent.mkdir(parents=True,exist_ok=True)
 for suffix in ['', '.meta']:
  src=Path(str(source)+suffix);dst=Path(str(target)+suffix)
  if dst.exists():assert src.read_bytes()==dst.read_bytes(),f'Conflicting dependency {dst}'
  else:shutil.copy2(src,dst)
 rows.append(dict(guid=guid,source=str(source),target=str(target)))
(r/'published_external_dependencies.json').write_text(json.dumps(rows,indent=2));print('EXTERNAL_DEPENDENCIES_PUBLISHED',len(rows))

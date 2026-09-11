from pathlib import Path
import json
W=Path('C:/UnityProjects/LegacyGwent/work/DynamicCards/CompleteMotionGoal-20260911');done=json.loads((W/'all-main-ui-first-pass/results.json').read_text(encoding='utf-8-sig'));seen={r['art'] for r in done['cards']};mp=json.loads((W/'reported-main-ui/game-card-map.json').read_text(encoding='utf-8-sig'))['cards'];remaining=[]
for r in sorted(mp,key=lambda r:r['card']):
 if r['art'] not in seen:remaining.append(r['card']);seen.add(r['art'])
assert len(remaining)==6,remaining
(W/'stop.txt').unlink(missing_ok=True)
(W/'request.json').write_text(json.dumps(dict(run='all-main-ui-last-six',cards=remaining,waits=[.1,1,3,6],screenshots=True,reopen=False)))
print('Queued exact remaining cards',remaining)

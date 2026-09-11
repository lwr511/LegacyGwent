from pathlib import Path
import json,collections
r=Path(__file__).resolve().parent
audit=json.loads((r/'bundle-audit.json').read_text())
source=json.loads((r.parent/'LatestAnimationData/animations.json').read_text())
by_id=collections.defaultdict(list)
for a in source['animators']:by_id[a['id']].append(a)
suspects=[]
for row in audit['rows']:
    src=by_id[row['id']]
    n=sum(bool(c.get('tracks') or c.get('pointerTracks')) for a in src for c in a.get('clips',[]))
    if n and not row['nonemptyClips']:
        suspects.append(dict(row,sourceClips=n,sourcePaths=[a['path'] for a in src if a.get('clips')]))
summary=dict(cards=audit['cards'],withMovingTransforms=sum(x['transformsMoved']>0 for x in audit['rows']),withoutTransformMovement=sum(x['transformsMoved']==0 for x in audit['rows']),withSourceCurvesButNoBundleCurves=suspects)
(r/'audit-summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))

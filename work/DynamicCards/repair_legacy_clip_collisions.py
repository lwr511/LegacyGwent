from pathlib import Path
import json
root=Path(__file__).resolve().parent
before=json.loads((root/'LegacyAnimationData/animations.json').read_text())
text=(root/'decode_legacy_animations.py').read_text().replace("needed=set((root/'legacy_needed.txt').read_text().split())","needed={'16240101','16230901'}")
exec(compile(text,__file__,'exec'))
after=json.loads((root/'LegacyAnimationData/animations.json').read_text())
before['animators']=[a for a in before['animators'] if a['id'] not in needed]+after['animators']
(root/'LegacyAnimationData/animations.json').write_text(json.dumps(before,indent=2))

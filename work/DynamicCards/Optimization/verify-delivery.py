from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import json,hashlib
r=Path(__file__).resolve().parent
main=r.parents[2]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card'
source=r.parent/'Probe/Library/DynamicCardsBundles/StandaloneWindows64'
target=main/'Library/DynamicCardsBundles/StandaloneWindows64'
index=json.loads((source/'cards.index.json').read_text())
names=['cards.bundle','cards.index.json']+[p['file'] for p in index['parts']]
def digest(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').digest()
def verify(name):
    assert digest(source/name)==digest(target/name),'Copied bundle differs: '+name
with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(verify,names))
assert (target/'cards.bundle.editor-ready').is_file()
png_count=sum(1 for _ in (main/'Assets/DynamicCards/Content/Latest').rglob('*.png'))
assert png_count==5075
result=dict(passed=True,identicalPublishedFiles=len(names),pngCount=png_count,editorReady=True)
(r/'delivery-hash-test.json').write_text(json.dumps(result))
print(json.dumps(result),flush=True)

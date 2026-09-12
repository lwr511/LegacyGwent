"""Restore unchanged derived binding tables invalidated by Unity Euler-order writes."""
from pathlib import Path
import json,hashlib,shutil,sys,collections
sys.path.insert(0,'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import yaml
W=Path(__file__).resolve().parent
assert (W/'rotation-repair-complete.txt').exists(),'Wait for the active Unity repair process to finish'
entries={r['original']:r for r in map(json.loads,(W/'rotation-repair-engine.jsonl').read_text().splitlines())}
def digest(b):return hashlib.sha256(b).hexdigest()
def section(data):
    a=data.index(b'  m_ClipBindingConstant:');b=data.index(b'  m_AnimationClipSettings:',a);return a,b,data[a:b]
changes=[];planned=[]
for row in entries.values():
    src=Path(row['original']);dst=Path(row['staged']);before=src.read_bytes();after=dst.read_bytes()
    assert digest(before)==row['beforeHash'];assert digest(after)==row['afterHash']
    _,_,original=section(before);a,b,current=section(after)
    original=original.replace(b'\r\n',b'\n');normalized=current.replace(b'\r\n',b'\n')
    if original==normalized:continue
    if normalized.strip()==b'm_ClipBindingConstant:\n    genericBindings: []\n    pptrCurveMapping: []':
        row['metadataReason']='Restore unchanged binding identities after Unity invalidated the Euler-order cache'
    else:
        originalTable=yaml.safe_load(original)['m_ClipBindingConstant'];currentTable=yaml.safe_load(normalized)['m_ClipBindingConstant']
        identities=lambda table:collections.Counter(json.dumps(x,sort_keys=True) for x in table['genericBindings'])
        assert identities(originalTable)==identities(currentTable),('Binding identities changed',row['original'])
        assert {k:v for k,v in originalTable.items() if k!='genericBindings'}=={k:v for k,v in currentTable.items() if k!='genericBindings'},('Non-order binding metadata changed',row['original'])
        row['metadataReason']='Restore original serialization order of the identical binding multiset'
    # All key values/times and other curve bindings were asserted unchanged in Unity.
    # This table contains only their identities, so its original bytes remain valid.
    assert row['preservedOtherCurves'] and row['preservedKeys']
    planned.append(row)
print('PREFLIGHT PASSED',len(entries),'clips;',len(planned),'unchanged binding tables need restoration',flush=True)
for row in planned:
    src=Path(row['original']);dst=Path(row['staged']);before=src.read_bytes();after=dst.read_bytes()
    assert digest(before)==row['beforeHash'];assert digest(after)==row['afterHash']
    _,_,original=section(before);a,b,current=section(after)
    original=original.replace(b'\r\n',b'\n')
    backup=W/'BeforeBindingMetadataFinalization'/dst.relative_to(W/'RepairStaging')
    assert backup.resolve().is_relative_to(W.resolve()) and dst.resolve().is_relative_to((W/'RepairStaging').resolve())
    backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dst,backup)
    if b'\r\n' in current:original=original.replace(b'\n',b'\r\n')
    updated=after[:a]+original+after[b:];dst.write_bytes(updated)
    changes.append(dict(original=row['original'],staged=row['staged'],beforeHash=row['afterHash'],afterHash=digest(updated),reason=row['metadataReason']))
(W/'binding-metadata-finalization.json').write_text(json.dumps(dict(complete=True,changes=changes),indent=2))
print('RESTORED',len(changes),'unchanged binding tables; original curves and keys preserved')

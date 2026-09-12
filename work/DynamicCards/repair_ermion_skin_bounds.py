"""Restore Ermion's original fixed skin bounds; default is a read-only check.

The 2017 source uses updateWhenOffscreen=false for all three skins. Enabling it
recomputes their bounds and reverses transparent face/torso sorting during Loop.
--apply backs up the prefab before changing only those three source flags.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
RELATIVE = 'Assets/DynamicCards/Content/Old/Legacy2017/15210301/Card.prefab'
NAMES = {'_2_skinnedMeshes', '_3_skinnedMeshesTorso', '_4_skinnedMeshesNoseBowl'}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=ROOT/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    target = args.project/RELATIVE
    raw = target.read_bytes()
    text = raw.decode('utf-8')
    names = {}
    for m in re.finditer(r'(?ms)^--- !u!1 &(-?\d+)\r?\n(.*?)(?=^--- !u!|\Z)', text):
        names[m[1]] = re.search(r'^  m_Name: (.*)', m[2], re.M)[1].strip()
    found = set()
    def fix(m):
        block = m[0]
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', block)[1]
        name = names[go]
        if name not in NAMES:
            raise ValueError('Unexpected Ermion skin: '+name)
        found.add(name)
        flags = re.findall(r'^  m_UpdateWhenOffscreen: ([01])\s*$', block, re.M)
        if len(flags) != 1:
            raise ValueError('Unexpected flag layout: '+name)
        return block.replace('m_UpdateWhenOffscreen: 1', 'm_UpdateWhenOffscreen: 0')
    updated = re.sub(r'(?ms)^--- !u!137 &-?\d+\r?\n.*?(?=^--- !u!|\Z)', fix, text)
    if found != NAMES:
        raise ValueError('Missing source skin: '+str(NAMES-found))
    if text == updated:
        print('PASS: three Ermion skins retain original fixed bounds.')
        return
    if not args.apply:
        raise SystemExit('Repair pending; rerun with --apply and rebuild dynamic card packages.')
    backup = Path(__file__).resolve().parent/'ErmionTempest-20260912'/'Before'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')/'Card.prefab'
    backup.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(target, backup)
    target.write_bytes(updated.encode('utf-8'))
    print('Restored three source flags. Backup: '+str(backup))

if __name__ == '__main__':
    main()

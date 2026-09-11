"""Restore the source RGB of the shared Henselt/Brouver fire sheets.

These sheets are consumed by premultiplied-alpha particle shaders. Unity's
alphaIsTransparency colour dilation fills transparent texels with fire RGB,
which Blend One OneMinusSrcAlpha then displays as strips and rectangle edges.
The Triss bigFire sheet packs animation frames into all four RGBA channels;
colour dilation also corrupts this data. Only verified texture identities change; PNGs and all other
import/compression/particle settings are preserved. Rebuild dynamic-card bundles
after --apply. The default is a read-only audit.
"""
import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path

TEXTURES = {
    'Legacy2017/Shared/11210601_20908_bigFire_7x5.png': '518055c9098e3f1488eff4f8c8a034a9',
    'Legacy2017/Shared/11220701_29762_fire_13x5.png': 'f9bcbf611094c1e4a9b1c905ea3985a0',
    'Legacy2017/Shared/11210201_15682_small_fire_looped10x4.png': '04b5fce452eb6d443ae47489c6c25cfe',
    'Thronebreaker/Shared/41868_fire_13x5.png': '61dd5b8afb510b640b177cbc76f4c0e1',
    'Thronebreaker/Shared/27716_small_fire_looped10x4.png': 'a0b9cf6be84f80e4d87844ec324e2c02',
}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2] / 'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card')
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    pending = []
    for relative, guid in TEXTURES.items():
        image = args.project / 'Assets/DynamicCards/Content/Old' / relative
        meta = Path(str(image) + '.meta')
        original = meta.read_bytes()
        if not image.is_file() or f'guid: {guid}'.encode() not in original:
            raise ValueError(f'Unexpected texture identity: {image}')
        pattern = rb'(?m)^(  alphaIsTransparency: )[01](\r?)$'
        if len(re.findall(pattern, original)) != 1:
            raise ValueError(f'Unexpected alpha import setting: {meta}')
        updated = re.sub(pattern, rb'\g<1>0\2', original)
        pending.append((relative, meta, original, updated, hashlib.sha256(image.read_bytes()).hexdigest()))
    backup = Path(__file__).resolve().parent / 'FireRepair-20260911' / ('before-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    report = []
    for relative, meta, original, updated, image_hash in pending:
        changed = original != updated
        if args.apply and changed:
            saved = backup / (relative + '.meta')
            saved.parent.mkdir(parents=True, exist_ok=True)
            saved.write_bytes(original)
            meta.write_bytes(updated)
        report.append(dict(texture=relative, needsChange=changed, imageSHA256=image_hash))
    print(json.dumps(dict(applied=args.apply, textures=report), indent=2))

if __name__ == '__main__':
    main()

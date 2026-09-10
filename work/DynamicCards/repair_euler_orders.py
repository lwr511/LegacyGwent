"""Check or restore four verified Euler-order repairs in locally extracted clips.

Run from any directory; default is read-only. --apply backs up changed files.
Extracted game assets intentionally remain outside Git.
"""
import argparse
from datetime import datetime, timezone
from pathlib import Path
import re
import shutil

TARGETS = {
    "Old/Legacy2017/11210501/Source_c64138d8_Intro.anim": 6,
    "Old/Legacy2017/11210501/Source_c64138d8_Loop.anim": 6,
    "Old/Legacy2017/12230701/Source_0d11c551_Loop.anim": 2,
    "Latest/16740101/Source_5f2f4868_Loop.anim": 34,
}


def main():
    default = Path(__file__).resolve().parents[2] / "src/Cynthia.Card.Unity/src/Cynthia.Unity.Card"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=default)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    content = args.project.resolve() / "Assets/DynamicCards/Content"
    plans = []
    # Validate every target before writing any asset. Only Euler curve orders change.
    for relative, count in TARGETS.items():
        path = content / relative
        raw = path.read_bytes()
        match = re.search(rb"(?ms)^  m_EulerCurves:.*?(?=^  \w|\Z)", raw)
        if match is None:
            raise ValueError(f"{relative}: missing Euler curves")
        block = match.group()
        orders = re.findall(rb"m_RotationOrder: (\d+)", block)
        if len(orders) != count or any(order not in (b"0", b"4") for order in orders):
            raise ValueError(f"{relative}: unexpected source layout/orders: {orders}")
        fixed = re.sub(rb"m_RotationOrder: 4\b", b"m_RotationOrder: 0", block)
        updated = raw[:match.start()] + fixed + raw[match.end():]
        plans.append((path, relative, raw, updated))
        print(f"{relative}: curves={count}, pending={orders.count(b'4')}")
    pending = [plan for plan in plans if plan[2] != plan[3]]
    if pending and not args.apply:
        raise SystemExit("Repairs pending; rerun with --apply, then rebuild dynamic card bundles in Unity.")
    if pending:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        backup = Path(__file__).resolve().parent / "EulerOrderBackups" / stamp
        for path, relative, raw, updated in pending:
            target = backup / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
        for path, relative, raw, updated in pending:
            path.write_bytes(updated)
        print(f"Updated {len(pending)} clips; backups: {backup}")
        print("Refresh Unity assets and rebuild the dynamic card bundles.")
    else:
        print("PASS: all four clips already preserve source XYZ order (48 curves).")


if __name__ == "__main__":
    main()

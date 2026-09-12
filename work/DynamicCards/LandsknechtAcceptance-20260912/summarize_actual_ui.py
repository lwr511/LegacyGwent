"""Summarize an explicitly named actual-UI run; never promote sampling to visual approval."""
from pathlib import Path
import argparse, json, datetime
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument('run', type=Path)
parser.add_argument('--minimum-samples', type=int, default=4)
args = parser.parse_args()
root = args.run.resolve()
report = json.loads((root/'results.json').read_text(encoding='utf-8-sig'))
mapping = {r['card']:r for r in json.loads((root/'game-card-map.json').read_text(encoding='utf-8-sig'))['cards']}
rows = []
for card in report['cards']:
    samples = card.get('rows', [])
    concerns = []
    if card['status'] != 'sampled': concerns.append(card['status'])
    if samples and len(samples)<args.minimum_samples: concerns.append('incomplete-sample-sequence')
    if any(not all(r.get(k, False) for k in ['enabled', 'visible', 'surfaceEnabled', 'modelActive']) for r in samples):
        concerns.append('a-sample-was-not-visible-and-active')
    if any(r.get('unsupportedShaders') for r in samples): concerns.append('unsupported-shader')
    if any(not r.get('bundleRoot') for r in samples): concerns.append('bundle-root-not-recorded')
    if samples and not any(r.get('pixelChanges', 0) for r in samples[1:]): concerns.append('no-observed-surface-motion')
    skins = sorted({s['path'] for r in samples for s in r.get('skins', [])})
    stationary = [p for p in skins if not any(s.get('maxDelta', 0)>1e-6 for r in samples[1:] for s in r.get('skins', []) if s['path']==p)]
    frames = [r.get('artFrame') for r in samples if r.get('artFrame')]
    if samples and len(frames)!=len(samples): concerns.append('missing-art-frame-evidence')
    rows.append(dict(card=card['card'], art=card['art'], name=mapping.get(card['card'], {}).get('name', ''),
        status=card['status'], concerns=concerns, stationarySkinsForReview=stationary,
        frames=frames, sampleAges=[r.get('age',0) for r in samples], samples=len(samples), visualReview='pending'))
summary = dict(complete=report['complete'], generatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    run=str(root), sampledCards=len(rows), cardsWithConcerns=sum(bool(r['concerns']) for r in rows),
    cardsWithStationarySkins=sum(bool(r['stationarySkinsForReview']) for r in rows),
    actualVisualApproval=False, cards=rows)
(root/'inspection-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 13)
for page, start in enumerate(range(0, len(rows), 8), 1):
    batch=rows[start:start+8]
    sheet = Image.new('RGB', (1200, ((len(batch)+1)//2)*244), '#20242a')
    draw = ImageDraw.Draw(sheet)
    for slot, row in enumerate(batch):
        x, y = (slot%2)*600, (slot//2)*244
        draw.text((x+4,y+3), f"{row['card']} / {row['art']}", font=font, fill='white')
        draw.text((x+4,y+25), row['name'][:36], font=small, fill='#dddddd')
        selected = row['frames']
        step=592//max(1,len(selected))
        for sample, frame in enumerate(selected):
            path = (root/frame).resolve()
            if not path.is_relative_to(root): raise ValueError('Frame path outside run')
            with Image.open(path) as img:
                img = img.convert('RGB'); img.thumbnail((step-4,162))
                sheet.paste(img, (x+sample*step+3,y+49))
            draw.text((x+sample*step+3,y+210), f"age {row['sampleAges'][sample]:.2f}s", font=small, fill='#dddddd')
        label = 'REVIEW: '+','.join(row['concerns']) if row['concerns'] else 'all captured samples; visual review pending'
        draw.text((x+4,y+229), label[:77], font=small, fill='#ffbb77' if row['concerns'] else '#aaddcc')
    sheet.save(root/f'contact-{page:03}.jpg', quality=92)
print(json.dumps({k:v for k,v in summary.items() if k!='cards'}))

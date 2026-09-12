"""Produce viewable evidence and motion measurements from the real UI recorder only."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import argparse, json, math

p=argparse.ArgumentParser(); p.add_argument('run',type=Path); args=p.parse_args()
root=args.run.resolve(); report=json.loads((root/'poses.json').read_text(encoding='utf-8-sig'))
assert report['complete'] and report['frames'] and report['bundleRoot']
frames=report['frames']; maxima={}; dt=[]
for a,b in zip(frames,frames[1:]):
    elapsed=b['time']-a['time']; assert elapsed>0; dt.append(elapsed)
    prior={x['bone']:x['rotation'] for x in a['bones']}
    for bone in b['bones']:
        if bone['bone'] not in prior: continue
        u=prior[bone['bone']]; v=bone['rotation']
        dot=sum(u[k]*v[k] for k in ('x','y','z','w'))
        norm=math.sqrt(sum(u[k]**2 for k in ('x','y','z','w'))*sum(v[k]**2 for k in ('x','y','z','w')))
        angle=math.degrees(2*math.acos(min(1,abs(dot)/norm)))
        if angle>maxima.get(bone['bone'],{}).get('angleDegrees',-1):
            maxima[bone['bone']]=dict(angleDegrees=angle,elapsedSeconds=elapsed,degreesPerSecond=angle/elapsed,time=b['time'])
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
sheet=Image.new('RGB',(1024,1200),'#20242a'); draw=ImageDraw.Draw(sheet)
for n in range(12):
    i=round(n*(len(frames)-1)/11); x=(n%4)*256; y=(n//4)*400
    with Image.open(root/f'frame-{i+1:04}.jpg') as original:
        img=original.convert('RGB'); img.thumbnail((252,372)); sheet.paste(img,(x,y+25))
    draw.text((x+4,y+3),f"t={frames[i]['time']:.3f}s",font=font,fill='white')
sheet.save(root/'motion-contact.jpg',quality=94)
# Downsample the captured sequence for a portable animated preview, retaining its timing.
chosen=[]; next_time=0
for i,f in enumerate(frames):
    if f['time']>=next_time:
        chosen.append(i); next_time=f['time']+.095
images=[]; durations=[]
for n,i in enumerate(chosen):
    with Image.open(root/f'frame-{i+1:04}.jpg') as original:
        img=original.convert('RGB'); img.thumbnail((384,576)); images.append(img.copy())
    end=frames[chosen[n+1]]['time'] if n+1<len(chosen) else frames[-1]['time']+.1
    durations.append(max(10,round((end-frames[i]['time'])*1000/10)*10))
images[0].save(root/'actual-ui-motion.webp',save_all=True,append_images=images[1:],duration=durations,loop=0,quality=88,method=4)
summary=dict(complete=True,source='actual production ArtCard render surface and foreground _3_man mesh',
    sourceMesh=report['sourceMesh'],bundleRoot=report['bundleRoot'],frames=len(frames),seconds=frames[-1]['time']-frames[0]['time'],
    maximumSampleGapSeconds=max(dt),boneStepMeasurements=maxima,visualApproval='pending')
(root/'capture-summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps({k:v for k,v in summary.items() if k!='boneStepMeasurements'}))

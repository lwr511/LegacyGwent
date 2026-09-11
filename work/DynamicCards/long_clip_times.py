import sys,json,re,struct,math
from pathlib import Path
root=Path(__file__).resolve().parent
sys.path.insert(0,r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work/python_deps')
import UnityPy
env=UnityPy.load(r'C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
clips=[]
for o in env.objects:
    if o.type.name!='AnimationClip':continue
    card=re.search(r'\d{8}',o.assets_file.name)
    if not card:continue
    t=o.read_typetree();m=t['m_MuscleClip'];length=m['m_StopTime']-m['m_StartTime']
    if length<=30:continue
    data=m['m_Clip'].get('data',m['m_Clip'])['m_StreamedClip']['data'];raw=struct.pack('<%dI'%len(data),*data);p=0;times=set()
    while p+8<=len(raw):
        time,count=struct.unpack_from('<fi',raw,p);p+=8+count*20
        time-=m['m_StartTime']
        if math.isfinite(time) and 0<=time<=length:
            times.update([max(0,time-1/30),time,min(length,time+1/30)])
    clips.append(dict(card=card[0],clip=t['m_Name'],times=sorted(times),duration=length))
(root/'long_clip_times.json').write_text(json.dumps(dict(clips=clips),indent=2))
print('Long clips:',[(c['card'],c['clip'],c['duration'],len(c['times'])) for c in clips])

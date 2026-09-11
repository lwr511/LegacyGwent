from pathlib import Path
import json,shutil
root=Path(__file__).resolve().parent;reports=[]
for kind in ['Native','Legacy','Latest']:
 folder=root/(kind+'AnimationData');document=json.loads((folder/'animations.json').read_text())
 selected={a['id'] for a in document['animators'] if any(c['duration']>90 and c['frames']==2701 for c in a['clips'])}
 files={c['file'] for a in document['animators'] for c in a['clips'] if c['duration']>90 and c['frames']==2701}
 if not files:continue
 base_kind='Latest' if kind=='Latest' else 'Legacy'
 code=(root/('decode_'+base_kind.lower()+'_animations.py')).read_text()
 code=code.replace("out=root/'"+base_kind+"AnimationData'","out=root/'Long"+kind+"AnimationData'")
 original="needed=set(json.loads((root/'latest_source_effects.json').read_text(encoding='utf8')))" if kind=='Latest' else "needed=set((root/'legacy_needed.txt').read_text().split())"
 code=code.replace(original,'needed='+repr(selected))
 if kind=='Native':code=code.replace('E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets/AssetBundles/cardassets/scenes','C:/SteamLibrary/steamapps/common/Thronebreaker The Witcher Tales/Thronebreaker_Data/StreamingAssets/bundledassets/cardassets/scenes')
 code=code.replace('min(2701,max(2,round(dur*fps)+1))','max(2,round(dur*fps)+1)')
 namespace={'__file__':str(root/'long_sampling_decoder.py')};exec(compile(code,namespace['__file__'],'exec'),namespace)
 decoded=root/('Long'+kind+'AnimationData');fresh=json.loads((decoded/'animations.json').read_text());clips={(a['id'],a['path'],c['name']):c for a in fresh['animators'] for c in a['clips']}
 backup=root/'BeforeLongAnimationSampling'/kind;backup.mkdir(parents=True,exist_ok=True)
 if not (backup/'animations.json').exists():shutil.copy2(folder/'animations.json',backup/'animations.json')
 for animator in document['animators']:
  for clip in animator['clips']:
   if clip['file'] not in files:continue
   replacement=clips[animator['id'],animator['path'],clip['name']];assert replacement['columns']==clip['columns'];assert abs(replacement['duration']-clip['duration'])<.001
   if not (backup/clip['file']).exists():shutil.copy2(folder/clip['file'],backup/clip['file'])
   shutil.copy2(decoded/replacement['file'],folder/clip['file']);clip['frames']=replacement['frames']
 temporary=folder/'animations.updated.json';temporary.write_text(json.dumps(document,indent=2));temporary.replace(folder/'animations.json')
 reports.append(dict(source=kind,cards=sorted(selected),clips=len(files)));print('LONG_ANIMATION_SAMPLING_REPAIRED',kind,len(files),flush=True)
(root/'long_animation_sampling_repair.json').write_text(json.dumps(reports,indent=2))

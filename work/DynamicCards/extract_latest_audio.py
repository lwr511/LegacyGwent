import json,struct,subprocess,collections,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parent
OLD=Path(r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work')
BASE=Path(r'C:/SteamLibrary/steamapps/common/GWENT The Witcher Card Game/Gwent_Data/StreamingAssets/audio')
cards={c['id'] for c in json.loads((ROOT/'third_source_inventory.json').read_text())['cards']}
inclusion=json.loads((BASE/'event_inclusion.json').read_text())['InclusionMapping']
event_banks={str(e):b for b,events in inclusion.items() if b.lower().endswith('_pre') for e in events}
pack_banks=json.loads((BASE/'soundbank_inclusion.json').read_text())['InclusionMapping'];bank_pack={b:p for p,bs in pack_banks.items() for b in bs}
audio_ids={}
for item in ET.parse(ROOT/'latest_defs_CardAudio.xml').iter('CardAudio'):
 if item.get('id'):
  banks={event_banks[str(int(s.get('event'))&0xffffffff)] for s in item.iter('SoundEffect') if str(int(s.get('event'))&0xffffffff) in event_banks}
  if len(banks)==1:audio_ids[item.get('id')]=banks.pop()
card_banks=collections.defaultdict(set)
for item in ET.parse(ROOT/'latest_defs_Templates.xml').getroot():
 if item.get('AudioId') in audio_ids:card_banks[item.get('ArtId','')+'0101'].add(audio_ids[item.get('AudioId')])
def fnv(s):
 h=2166136261
 for c in s.lower().encode():h=((h*16777619)&0xffffffff)^c
 return h
indexes={};out=ROOT/'LatestAudio';out.mkdir(exist_ok=True);result={};missing=[]
for i,card in enumerate(sorted(cards)):
 bs=card_banks.get(card,set())
 if len(bs)!=1:missing.append(card);continue
 bank=next(iter(bs));pack=bank_pack.get(bank)
 if not pack:missing.append(card);continue
 with (BASE/pack).open('rb') as f:
  if pack not in indexes:
   header=f.read(28);f.seek(28+struct.unpack_from('<I',header,12)[0]);count=struct.unpack('<I',f.read(4))[0]
   rows=[struct.unpack('<5I',f.read(20)) for _ in range(count)];indexes[pack]={e[0]:e for e in rows}
  row=indexes[pack].get(fnv(bank))
  if row is None:missing.append(card);continue
  f.seek(row[1]*row[3]);b=f.read(row[2])
 chunks={};p=0
 while p+8<=len(b):
  tag=b[p:p+4];size=struct.unpack_from('<I',b,p+4)[0];chunks[tag]=b[p+8:p+8+size];p+=8+size
 idx=chunks.get(b'DIDX',b'');files=[]
 for offset in range(0,len(idx),12):
  ident,off,size=struct.unpack_from('<III',idx,offset);raw=out/(str(ident)+'.wem');dest=out/(str(ident)+'.wav')
  if not dest.exists():
   raw.write_bytes(chunks[b'DATA'][off:off+size]);proc=subprocess.run([str(OLD/'vgmstream/vgmstream-cli.exe'),'-i','-o',str(dest),str(raw)],capture_output=True)
   if proc.returncode:raise RuntimeError(card+': audio decode failed')
  files.append(dict(file=dest.name,bytes=size))
 result[card]=dict(bank=bank,files=files)
 if i%50==0:print('LATEST_AUDIO_PROGRESS',i,flush=True)
(ROOT/'latest_audio.json').write_text(json.dumps(result,indent=2));(ROOT/'latest_audio_missing.json').write_text(json.dumps(missing))
print('LATEST_AUDIO_DONE',len(result),'missing',len(missing),flush=True)

import sys,json,struct,subprocess,collections,xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parent
OLD=Path(r'C:/Users/11464/Documents/Codex/2026-09-05/c-steamlibrary-steamapps-common-thronebreaker-the/work')
BASE=Path(r'E:/Hbackup/FileRecv/Gwent/Gwent/Gwent_Data/StreamingAssets')
cards=json.loads((ROOT/'second_source_inventory.json').read_text())['scenes']
audio_ids={}
for item in ET.parse(ROOT/'legacy_defs_CardAudio.xml').iter('CardAudio'):
    if item.get('id'):
        banks={bank.get('name') for bank in item.iter('Soundbank') if bank.get('type')=='Premium'}
        if len(banks)==1:audio_ids[item.get('id')]=banks.pop()
card_banks=collections.defaultdict(set)
for item in ET.parse(ROOT/'legacy_defs_Templates.xml').iter('ArtDefinition'):
    if item.get('AudioId') in audio_ids:card_banks[item.get('ArtId','')+'1'].add(audio_ids[item.get('AudioId')])
text=(ROOT/'source_effects_audio.py').read_text();text='def fnv(s):'+text.split('def fnv(s):')[1]
text=text.replace("BASE/'audio/cards.pck'","BASE/'Audio/GeneratedSoundBanks/Windows/Cards.pck'").replace("ROOT/'Audio'","ROOT/'LegacyAudio'").replace('source_audio.json','legacy_audio.json')
exec(compile(text,__file__,'exec'))

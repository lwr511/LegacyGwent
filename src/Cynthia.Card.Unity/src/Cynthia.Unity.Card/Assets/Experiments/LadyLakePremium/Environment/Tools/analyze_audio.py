"""Read-only WAV feature scan used to pick Lady Lake 12 s loop sources.

Pure standard library (no numpy on this machine). It never writes to the
scanned project files; it only prints a report used to choose provenance.
"""
import json
import math
import os
import struct
import sys
import wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..', '..', '..'))
CONTENT = os.path.join(ROOT, 'Assets', 'DynamicCards', 'Content')


def read_wav(path):
    with wave.open(path, 'rb') as w:
        nch = w.getnchannels()
        sw = w.getsampwidth()
        fr = w.getframerate()
        n = w.getnframes()
        raw = w.readframes(n)
    if sw != 2:
        return None
    total = n * nch
    samples = struct.unpack('<%dh' % total, raw[:total * 2])
    return {'channels': nch, 'rate': fr, 'frames': n, 'samples': samples}


def features(info):
    nch = info['channels']
    rate = info['rate']
    s = info['samples']
    n = info['frames']
    # mono mix
    mono = s[::nch] if nch == 1 else [0.5 * (s[i * nch] + s[i * nch + 1]) for i in range(n)]
    peak = 0
    acc = 0
    zc = 0
    prev = 0
    for v in mono:
        a = v if v >= 0 else -v
        if a > peak:
            peak = a
        acc += v * v
        if (v >= 0) != (prev >= 0):
            zc += 1
        prev = v
    rms = math.sqrt(acc / max(1, len(mono)))
    # 50 ms block modulation
    blk = max(1, rate // 20)
    blocks = []
    for start in range(0, len(mono), blk):
        seg = mono[start:start + blk]
        if not seg:
            continue
        e = math.sqrt(sum(x * x for x in seg) / len(seg))
        blocks.append(e)
    if blocks:
        mean = sum(blocks) / len(blocks)
        var = sum((b - mean) ** 2 for b in blocks) / len(blocks)
        sd = math.sqrt(var)
        mod = sd / mean if mean > 1e-6 else 0.0
        quiet = sum(1 for b in blocks if b < 0.12 * mean) / len(blocks)
        loud = max(blocks) / (mean + 1e-9)
    else:
        mod = quiet = loud = 0.0
    return {
        'dur': n / float(rate),
        'channels': nch,
        'rate': rate,
        'peak_dbfs': 20 * math.log10((peak + 1e-9) / 32768.0),
        'rms_dbfs': 20 * math.log10((rms + 1e-9) / 32768.0),
        'zcr': zc / max(1.0, len(mono)) * rate / 2.0,
        'mod': mod,
        'quiet_frac': quiet,
        'crest': peak / (rms + 1e-9),
    }


def catalog_audio():
    path = os.path.join(CONTENT, 'catalog.json')
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    out = {}
    for card in data.get('cards', []):
        out[str(card.get('id'))] = card.get('audio')
    return out


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else 'scan'
    if mode == 'ids':
        cat = catalog_audio()
        rows = []
        for cid, audio in cat.items():
            if not audio:
                continue
            p = os.path.join(ROOT, audio.replace('/', os.sep))
            if os.path.isfile(p):
                rows.append((os.path.getsize(p), cid, audio))
        rows.sort(reverse=True)
        for size, cid, audio in rows:
            print('%9d %s %s' % (size, cid, audio))
        return
    if mode == 'some':
        cat = catalog_audio()
        for cid in sys.argv[2:]:
            print('%-16s %s' % (cid, cat.get(cid)))
        return
    if mode == 'files':
        for pattern in sys.argv[2:]:
            p = os.path.join(ROOT, pattern.replace('/', os.sep))
            if not os.path.isfile(p):
                continue
            try:
                info = read_wav(p)
            except Exception as exc:  # noqa: BLE001
                print('ERR %s %s' % (pattern, exc))
                continue
            if info is None:
                print('SKIP(non16) %s' % pattern)
                continue
            f = features(info)
            print('%-70s dur=%6.2f ch=%d rate=%5d peak=%7.2f rms=%7.2f crest=%5.2f mod=%5.2f quiet=%4.2f zcr=%6.0f'
                  % (os.path.basename(pattern), f['dur'], f['channels'], f['rate'],
                     f['peak_dbfs'], f['rms_dbfs'], f['crest'], f['mod'], f['quiet_frac'], f['zcr']))
        return
    if mode == 'dir':
        folder = os.path.join(ROOT, sys.argv[2].replace('/', os.sep))
        rows = []
        for name in os.listdir(folder):
            if not name.lower().endswith('.wav'):
                continue
            p = os.path.join(folder, name)
            try:
                info = read_wav(p)
            except Exception:  # noqa: BLE001
                continue
            if info is None:
                continue
            f = features(info)
            rows.append((f['dur'], name, f))
        rows.sort(reverse=True)
        for dur, name, f in rows:
            print('%-24s dur=%7.2f ch=%d rate=%5d peak=%7.2f rms=%7.2f crest=%5.2f mod=%5.2f quiet=%4.2f zcr=%6.0f'
                  % (name, f['dur'], f['channels'], f['rate'], f['peak_dbfs'], f['rms_dbfs'],
                     f['crest'], f['mod'], f['quiet_frac'], f['zcr']))
        return
    print('usage: analyze_audio.py ids|files <paths>|dir <folder>')


if __name__ == '__main__':
    main()

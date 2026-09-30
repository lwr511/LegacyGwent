"""Build Environment/Audio/LadyLakeLoop.wav: a 12.000 s seamless stereo loop.

Read-only against the project. Sources are original Gwent premium-card scene
audio loops (the `audio` field of Assets/DynamicCards/Content/catalog.json) and
are never modified. Processing is local (wave/struct/math only; numpy is not
installed on this machine), no network, no speech synthesis, no image edits.

Element map (honest, per the shared contract choreography):
  0.0-1.3   atmosphere bed only (fairy floats)
  1.0-3.4   gentle falling-blade water movement (Tempest water scene)
  3.2       catch glint (transient derived from a sword-flare premium scene)
  5.6-8.3   lift magical swell (Will of the Wisps magic scene)
  8.3-12.0  decay back to the bed, seam is crossfaded

Provenance for every element is written to Environment/audio-provenance.json
by the same run.
"""
import json
import math
import os
import struct
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
ENV = os.path.abspath(os.path.join(HERE, '..'))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..', '..'))

OUT_WAV = os.path.join(ENV, 'Audio', 'LadyLakeLoop.wav')
OUT_JSON = os.path.join(ENV, 'audio-provenance.json')

RATE = 48000
DURATION = 12.0
N = int(round(RATE * DURATION))
XFADE = int(RATE * 0.35)

SOURCES = {
    # key: (path, catalog scene id, role)
    'aeschna': ('Assets/DynamicCards/Content/Old/Thronebreaker/Audio/537737864.wav', '15010100'),
    'lake': ('Assets/DynamicCards/Content/Latest/Audio/22041709.wav', '56180101'),
    'tempest': ('Assets/DynamicCards/Content/Latest/Audio/6644576.wav', '16550101'),
    'swordflare': ('Assets/DynamicCards/Content/Latest/Audio/409841224.wav', '13680101'),
    'wisps': ('Assets/DynamicCards/Content/Latest/Audio/1035499252.wav', '22500101'),
}


def read_stereo(rel):
    path = os.path.join(ROOT, rel.replace('/', os.sep))
    with wave.open(path, 'rb') as w:
        nch = w.getnchannels()
        sw = w.getsampwidth()
        rate = w.getframerate()
        n = w.getnframes()
        raw = w.readframes(n)
    if sw != 2:
        raise RuntimeError('expected 16-bit PCM: ' + rel)
    total = n * nch
    flat = struct.unpack('<%dh' % total, raw[:total * 2])
    left = [0.0] * n
    right = [0.0] * n
    if nch == 1:
        for i in range(n):
            v = flat[i] / 32768.0
            left[i] = v
            right[i] = v
    else:
        for i in range(n):
            left[i] = flat[i * nch] / 32768.0
            right[i] = flat[i * nch + 1] / 32768.0
    return {'left': left, 'right': right, 'rate': rate, 'frames': n, 'path': rel}


def resample(channel, factor):
    """Linear resample; factor > 1 slows/pitch-downs."""
    n = len(channel)
    out_n = max(1, int(n / factor))
    out = [0.0] * out_n
    for i in range(out_n):
        pos = i * factor
        i0 = int(pos)
        i1 = min(i0 + 1, n - 1)
        f = pos - i0
        out[i] = channel[i0] * (1.0 - f) + channel[i1] * f
    return out


def window(src, start_sec, length_sec):
    r = src['rate']
    a = int(round(start_sec * r))
    b = min(src['frames'], a + int(round(length_sec * r)))
    if a >= src['frames']:
        a = max(0, src['frames'] - 1)
        b = src['frames']
    left = list(src['left'][a:b])
    right = list(src['right'][a:b])
    return left, right


def one_pole_lp(channel, cutoff):
    a = math.exp(-2.0 * math.pi * cutoff / RATE)
    out = [0.0] * len(channel)
    y = 0.0
    for i, x in enumerate(channel):
        y = (1.0 - a) * x + a * y
        out[i] = y
    return out


def one_pole_hp(channel, cutoff):
    lp = one_pole_lp(channel, cutoff)
    return [channel[i] - lp[i] for i in range(len(channel))]


def band_pass(channel, low, high):
    return one_pole_hp(one_pole_lp(channel, high), low)


def loop_to(channel, length):
    """Loop/trim a channel to exactly `length` samples with a short internal crossfade."""
    n = len(channel)
    if n == 0:
        return [0.0] * length
    if n >= length:
        return list(channel[:length])
    out = list(channel)
    pos = 0
    while len(out) < length:
        pos = (pos + n) % n
        remaining = length - len(out)
        take = min(n, remaining)
        for i in range(take):
            out.append(channel[(pos + i) % n])
    return out


def place(dst_l, dst_r, src_l, src_r, start_sec, gain_l, gain_r, env=None):
    off = int(round(start_sec * RATE))
    for i in range(len(src_l)):
        j = off + i
        if j < 0 or j >= N:
            continue
        e = 1.0 if env is None else env(i)
        dst_l[j] += src_l[i] * gain_l * e
        dst_r[j] += src_r[i] * gain_r * e


def at_least(channel, length):
    if len(channel) >= length:
        return list(channel[:length])
    return list(channel) + [0.0] * (length - len(channel))


def main():
    src = {}
    for key, (rel, scene) in SOURCES.items():
        src[key] = read_stereo(rel)
        src[key]['scene'] = scene
        print('read %-11s %6.2f s  %s (%s)' % (key, src[key]['frames'] / src[key]['rate'], rel, scene))

    # ---- atmosphere bed -------------------------------------------------
    # 56180101 (DIY art scene) is the calmest water loop: use it as the bed.
    bed_l, bed_r = window(src['lake'], 0.0, DURATION)
    bed_l = loop_to(bed_l, N)
    bed_r = loop_to(bed_r, N)
    # Aeschna 15010100 is the only original numbered underwater layer scene.
    aes_l, aes_r = window(src['aeschna'], 0.0, 10.0)
    aes_l = one_pole_lp(loop_to(aes_l, N), 2600.0)
    aes_r = one_pole_lp(loop_to(aes_r, N), 2600.0)
    # A third, quieter water layer for width.
    temp_l, temp_r = window(src['tempest'], 0.0, DURATION)
    temp_l = one_pole_lp(loop_to(temp_l, N), 1200.0)
    temp_r = one_pole_lp(loop_to(temp_r, N), 1200.0)

    mix_l = [0.0] * N
    mix_r = [0.0] * N
    for i in range(N):
        mix_l[i] = bed_l[i] * 0.62 + aes_l[i] * 0.34 + temp_l[i] * 0.16
        mix_r[i] = bed_r[i] * 0.62 + aes_r[i] * 0.34 + temp_r[i] * 0.20

    # ---- gentle falling blade water movement (1.0 -> 3.4 s) --------------
    fall_l, fall_r = window(src['tempest'], 3.0, 2.8)
    fall_l = resample(fall_l, 1.18)   # slow drag, underwater viscosity
    fall_r = resample(fall_r, 1.18)
    fall_l = band_pass(fall_l, 90.0, 5200.0)
    fall_r = band_pass(fall_r, 90.0, 4600.0)
    fall_len = len(fall_l)

    def fall_env(i):
        t = i / float(fall_len)
        if t < 0.18:
            return (t / 0.18) ** 1.4
        if t < 0.72:
            return 1.0
        return max(0.0, (1.0 - t) / 0.28) ** 1.1

    place(mix_l, mix_r, fall_l, fall_r, 1.0, 0.30, 0.26, fall_env)

    # ---- catch glint at 3.2 s -------------------------------------------
    # Derived transient from the Syanna sword-flare premium scene: take the
    # loudest transient, band-limit it and give it a metallic decay.
    sf = src['swordflare']
    peak_at = 0
    peak_v = 0.0
    chk = sf['left']
    for i in range(0, sf['frames']):
        a = abs(chk[i])
        if a > peak_v:
            peak_v = a
            peak_at = i
    gl_start = max(0, peak_at - int(0.020 * RATE))
    gl_l, gl_r = window(sf, gl_start / float(sf['rate']), 0.42)
    gl_l = band_pass(gl_l, 1800.0, 9000.0)
    gl_r = band_pass(gl_r, 2100.0, 11000.0)
    # add a doubled, brighter reflection 90 ms later
    gl_l2 = band_pass(list(gl_l), 3200.0, 12000.0)
    gl_r2 = band_pass(list(gl_r), 3600.0, 13000.0)

    def glint_env(i, delay_samp, total):
        d = i - delay_samp
        if d < 0 or d >= total:
            return 0.0
        t = d / float(RATE)
        attack = 1.0 - math.exp(-t / 0.0025)
        return attack * math.exp(-t / 0.085)

    gl_total = int(0.45 * RATE)
    gl_l = at_least(gl_l, gl_total)
    gl_r = at_least(gl_r, gl_total)
    gl_l2 = at_least(gl_l2, gl_total)
    gl_r2 = at_least(gl_r2, gl_total)
    for i in range(gl_total):
        l = gl_l[i] * glint_env(i, 0, gl_total) * 0.34 + gl_l2[i] * glint_env(i, int(0.085 * RATE), gl_total) * 0.16
        r = gl_r[i] * glint_env(i, 0, gl_total) * 0.30 + gl_r2[i] * glint_env(i, int(0.085 * RATE), gl_total) * 0.15
        j = int(3.2 * RATE) + i
        if 0 <= j < N:
            mix_l[j] += l
            mix_r[j] += r

    # ---- lift magical swell (5.6 -> 8.3 s) -------------------------------
    sw_l, sw_r = window(src['wisps'], 4.0, 3.1)
    sw_l = resample(sw_l, 1.06)
    sw_r = resample(sw_r, 1.06)
    sw_l = band_pass(sw_l, 320.0, 8000.0)
    sw_r = band_pass(sw_r, 300.0, 7600.0)
    sw_len = len(sw_l)

    def swell_env(i):
        t = i / float(sw_len)
        if t < 0.42:
            return (t / 0.42) ** 1.7
        if t < 0.60:
            return 1.0
        return max(0.0, (1.0 - t) / 0.40) ** 1.2

    place(mix_l, mix_r, sw_l, sw_r, 5.6, 0.26, 0.30, swell_env)

    # ---- tiny "release" shimmer right after 8.3 --------------------------
    rel_l, rel_r = window(src['wisps'], 8.4, 0.7)
    rel_l = band_pass(rel_l, 2600.0, 12000.0)
    rel_r = band_pass(rel_r, 3000.0, 13000.0)
    rel_len = len(rel_l)

    def rel_env(i):
        t = i / float(rel_len)
        return math.exp(-t * 4.0) * (1.0 - math.exp(-t * 60.0))

    place(mix_l, mix_r, rel_l, rel_r, 8.25, 0.14, 0.16, rel_env)

    # ---- seamless-loop crossfade ----------------------------------------
    for i in range(XFADE):
        t = i / float(XFADE)
        j = N - XFADE + i
        mix_l[j] = mix_l[j] * (1.0 - t) + mix_l[i] * t
        mix_r[j] = mix_r[j] * (1.0 - t) + mix_r[i] * t

    # ---- normalise to a safe, non-clipping peak -------------------------
    peak = 0.0
    dc_l = 0.0
    dc_r = 0.0
    for i in range(N):
        if abs(mix_l[i]) > peak:
            peak = abs(mix_l[i])
        if abs(mix_r[i]) > peak:
            peak = abs(mix_r[i])
        dc_l += mix_l[i]
        dc_r += mix_r[i]
    dc_l /= N
    dc_r /= N
    print('pre-normalise peak %.4f  dc %.5f / %.5f' % (peak, dc_l, dc_r))
    target = 10 ** (-1.5 / 20.0)     # -1.5 dBFS
    scale = target / peak if peak > 1e-9 else 1.0

    frames = bytearray()
    clipped = 0
    final_peak = 0
    for i in range(N):
        l = (mix_l[i] - dc_l) * scale
        r = (mix_r[i] - dc_r) * scale
        # The broad crossfade does not make the endpoints sample-continuous.
        # A short smooth window removes the measured 3.3% full-scale seam step.
        edge = min(i, N - 1 - i) / float(int(0.020 * RATE))
        seam_gain = math.sin(min(1.0, edge) * math.pi * 0.5) ** 2
        l *= seam_gain
        r *= seam_gain
        li = int(round(max(-1.0, min(1.0, l)) * 32767.0))
        ri = int(round(max(-1.0, min(1.0, r)) * 32767.0))
        if abs(li) >= 32767 or abs(ri) >= 32767:
            clipped += 1
        final_peak = max(final_peak, abs(li), abs(ri))
        frames += struct.pack('<hh', li, ri)

    os.makedirs(os.path.dirname(OUT_WAV), exist_ok=True)
    with wave.open(OUT_WAV, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(bytes(frames))

    info = {
        'output': 'Assets/Experiments/LadyLakePremium/Environment/Audio/LadyLakeLoop.wav',
        'durationSeconds': DURATION,
        'sampleRate': RATE,
        'channels': 2,
        'bitDepth': 16,
        'peak': final_peak,
        'peakDbfs': round(20 * math.log10(final_peak / 32768.0), 3),
        'clippedSamples': clipped,
        'seamCrossfadeSeconds': XFADE / float(RATE),
        'endpointWindowSeconds': 0.020,
        'dcRemoved': [round(dc_l, 6), round(dc_r, 6)],
        'elements': [
            {'range': '0.0-12.0', 'role': 'atmosphere bed',
             'sources': ['15010100 (Aeschna, Thronebreaker, the original numbered underwater layer scene)',
                         '56180101 (Latest, the DIY-art water scene)',
                         '16550101 (Tempest, Latest water scene, low-passed)'],
             'processing': 'one-pole low-pass 2600 Hz / 1200 Hz, fixed bed gains, 0.35 s loop crossfade'},
            {'range': '1.0-3.4', 'role': 'gentle falling-blade water movement',
             'sources': ['16550101 (Tempest, Latest water scene)'],
             'processing': 'excerpt 3.0 s slowed x1.18 (underwater drag), 90 Hz-5.2 kHz band-pass, swell envelope'},
            {'range': '3.2', 'role': 'catch glint',
             'sources': ['13680101 (Syanna, Latest, sword-flare premium scene)'],
             'processing': 'loudest transient excised, 1.8-9 kHz band-pass, 2.5 ms attack / 85 ms decay plus a 90 ms brighter reflection'},
            {'range': '5.6-8.3', 'role': 'lift magical swell',
             'sources': ['22500101 (Will of the Wisps, Latest magic scene)'],
             'processing': 'excerpt 4.0 s slowed x1.06, 320 Hz-8 kHz band-pass, asymmetric rise/hold/fall envelope'},
            {'range': '8.25-9.0', 'role': 'release shimmer',
             'sources': ['22500101 (Will of the Wisps, Latest magic scene)'],
             'processing': '2.6-13 kHz band-pass, exponential decay, stereo-offset gains'},
        ],
        'notVerified': [
            'No listening test was performed by this worker; the mix is described by its numeric analysis only.',
            'The selected catalog audio fields are the premium-card scene soundtracks. They are treated as non-speech ambience; the worker did not run a speech classifier, only modulation/zero-crossing statistics.',
        ],
    }
    with open(OUT_JSON, 'w', encoding='utf-8') as f:
        json.dump(info, f, indent=2, ensure_ascii=False)
    print('wrote %s (%d samples/ch) peak=%d (%.2f dBFS) clipped=%d'
          % (OUT_WAV, N, final_peak, info['peakDbfs'], clipped))


if __name__ == '__main__':
    main()

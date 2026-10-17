#!/usr/bin/env python3
"""Regenerate Resources/ZoneBossMusic.json, the zone boss tracks' beat maps, from the owner's MP3s.

Offline tool (not run by CI or any check): needs ffmpeg on PATH plus numpy and scipy; matplotlib only for --plot.
Deterministic for a given ffmpeg build: the same MP3 bytes give the same map.

    python3 tools/zone_boss_beatmap.py            # write the JSON
    python3 tools/zone_boss_beatmap.py --check    # recompute and compare with the committed JSON (1 ms tolerance)
    python3 tools/zone_boss_beatmap.py --plot DIR --clicks DIR   # beat-map plots and click-track mixes for review

What is measured (docs/ZONE-BOSS-MUSIC.md):
  * The beat grid. Both songs drift in tempo (Station Chime of Doom ~178 -> ~181 BPM, Hydraulic Hell ~138.5 -> ~143), so the
    grid is tracked, never derived from one BPM: the percussive onset envelope (median-filter HPSS, log spectral flux) is
    followed by dynamic programming (Ellis 2007) against a local tempo curve at the eighth-note level, smoothed by a local
    linear fit (keeps the drift, drops the jitter), then aligned to the waveform's attacks.
  * Downbeats: the song's drop is a downbeat (it is where every band changes at once); bars are four beats from it.
  * Accents: chime notes (harmonic onsets inside the chime statements), slams (the strongest percussive hits), cuts (the
    sub bass dropping out) and drops (it returning), each with a time, kind and 0..1 strength.
What is chosen by ear and checked here: the section table, the loop bars and the intro edit (SONGS below). The tool prints the
novelty at each section boundary so a boundary that is not a real change shows up.
"""
import argparse
import hashlib
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from scipy import ndimage, signal

ROOT = Path(__file__).resolve().parents[1]
MUSIC = ROOT / 'Assets/ChipArena/Resources/Audio/music'
OUT = ROOT / 'Assets/ChipArena/Resources/ZoneBossMusic.json'
SR, NFFT, HOP = 44100, 2048, 256
FR = SR / HOP
# The boss intro hold the simulation runs before the fight (Runtime/NativePve.cs BeginPveHold "intro"). Every zone track's
# drop lands on its end; Tests/ZoneBossMusicChecks.cs pins this value to the simulation's.
HOLD = 9.0678

# Per song: the eighth-note period to search around (s), beats per eighth grouping (2: a beat is two eighths), the drop time
# (s, the downbeat that anchors the bars), the sections (id, from bar, to bar), the loop (from bar, to bar), the intro edit
# (preroll s, the bar after which the opening cuts, the bar it resumes at), the chime statements (s windows) and accent limits.
SONGS = [
    dict(id='central-station-boss', title='Station Chime of Doom', map='expedition-transit', eighth=.168, group=2, drop=16.11,
         sections=[('chime', 1, 1), ('arrival', 2, 3), ('platform', 4, 10), ('doors', 11, 11), ('express', 12, 23), ('signal', 24, 35),
                   ('rush', 36, 53), ('stopstart', 54, 57), ('lastcall', 58, 61), ('terminus', 62, 65)],
         loop=(12, 61), intro=dict(cutAfter=3, resumeAt=10), chimes=[(0.0, 2.7), (13.85, 16.15)]),
    dict(id='city-ironworks-boss', title='Hydraulic Hell', map='expedition-foundry', eighth=.2148, group=2, drop=14.02,
         sections=[('ignition', 1, 4), ('stoke', 5, 8), ('forge', 9, 34), ('quench', 35, 42), ('press', 43, 66),
                   ('venting', 67, 68), ('meltdown', 69, 76), ('blowdown', 77, 78), ('slam', 79, 82), ('cooling', 83, 84)],
         loop=(9, 82), intro=dict(cutAfter=4, resumeAt=8), chimes=[]),
]


def decode(path):
    """The MP3 as mono float32 at SR through ffmpeg (its decoder honours the LAME gapless header)."""
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(path), '-map', '0:a', '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype='<f4').astype(np.float64)


def stft(y):
    pad = np.pad(y, NFFT // 2, mode='reflect')
    n = 1 + (len(pad) - NFFT) // HOP
    win = signal.windows.hann(NFFT, sym=False)
    idx = np.arange(NFFT)[None, :] + HOP * np.arange(n)[:, None]
    return np.abs(np.fft.rfft(pad[idx] * win, axis=1)).T.astype(np.float32)


def features(y):
    S = stft(y)
    freqs = np.fft.rfftfreq(NFFT, 1 / SR)
    harm = ndimage.median_filter(S, size=(1, 17), mode='reflect')
    perc = ndimage.median_filter(S, size=(17, 1), mode='reflect')
    mask = perc ** 2 / (perc ** 2 + harm ** 2 + 1e-12)
    P, H = S * mask, S * (1 - mask)
    band = (freqs >= 30) & (freqs < 16000)
    lp = np.log1p(100 * P[band])
    onset = np.maximum(0, np.diff(lp, axis=1, prepend=lp[:, :1])).mean(0)
    tonal = (freqs >= 400) & (freqs < 4000)
    lh = np.log1p(100 * H[tonal])
    chime = np.maximum(0, np.diff(lh, axis=1, prepend=lh[:, :1])).mean(0)
    sub = 10 * np.log10(np.mean(S[(freqs >= 25) & (freqs < 100)] ** 2, 0) + 1e-10)
    low = (freqs >= 30) & (freqs < 200)
    ll = np.log1p(100 * P[low])
    thump = np.maximum(0, np.diff(ll, axis=1, prepend=ll[:, :1])).mean(0)
    mel = np.log1p(100 * S[band][::8])
    return dict(onset=onset, chime=chime, sub=sub, thump=thump, mel=mel)


def local_period(onset, p0, win=8.0, step=1.0, spread=.03):
    n = len(onset)
    centres, periods = [], []
    t_end = n / FR
    for c in np.arange(win / 2, max(win / 2 + 1e-9, t_end - win / 2 + 1e-9), step):
        a, b = c - win / 2, c + win / 2
        best = (-1, p0)
        for p in np.linspace(p0 * (1 - spread), p0 * (1 + spread), 241):
            k = np.arange(int(a / p) + 1, int(b / p))
            for ph in np.linspace(0, p, 32, endpoint=False):
                idx = np.round((ph + k * p) * FR).astype(int)
                idx = idx[idx < n]
                s = onset[idx].mean()
                if s > best[0]:
                    best = (s, p)
        centres.append(c)
        periods.append(best[1])
    periods = np.array([np.median(periods[max(0, i - 5):i + 6]) for i in range(len(periods))])
    return np.interp(np.arange(n) / FR, centres, periods)


def track(onset, period, alpha=300.0):
    """Ellis dynamic-programming beat tracking with a time-varying target period."""
    o = onset / (onset.std() + 1e-12)
    n = len(o)
    score = o.copy()
    back = -np.ones(n, dtype=int)
    pf = period * FR
    for i in range(n):
        p = pf[i]
        lo, hi = max(0, int(i - 2 * p)), int(i - p / 2)
        if hi <= lo:
            continue
        j = np.arange(lo, hi)
        c = score[j] - alpha * np.log((i - j) / p) ** 2
        k = int(np.argmax(c))
        score[i] = o[i] + c[k]
        back[i] = j[k]
    last = int(n - pf[-1])
    i = last + int(np.argmax(score[last:]))
    path = [i]
    while back[i] >= 0:
        i = back[i]
        path.append(i)
    return np.array(path[::-1], dtype=float) / FR


def smooth(times, half=8):
    """A local linear fit over +-half eighths: keeps the tempo drift, drops per-onset jitter."""
    k = np.arange(len(times))
    out = np.empty_like(times)
    for i in range(len(times)):
        a, b = max(0, i - half), min(len(times), i + half + 1)
        A = np.vstack([k[a:b], np.ones(b - a)]).T
        m, c = np.linalg.lstsq(A, times[a:b], rcond=None)[0]
        out[i] = m * i + c
    return out


def attacks(y):
    """1 ms log-energy rise of the signal above 150 Hz: where a hit's attack is."""
    sos = signal.butter(4, 150, btype='high', fs=SR, output='sos')
    z = signal.sosfiltfilt(sos, y)
    w = 44
    e = np.convolve(z * z, np.ones(w) / w, 'same')[::w]
    d = np.diff(10 * np.log10(e + 1e-9), prepend=0)
    return np.convolve(d, np.ones(3) / 3, 'same'), SR / w


def offsets(grid, onset, d, fr):
    strength = np.array([onset[min(len(onset) - 1, int(round(t * FR)))] for t in grid])
    strong = strength > np.percentile(strength, 70)
    ts, offs = [], []
    for t in grid[strong]:
        i = int(t * fr)
        if i - 40 < 0 or i + 40 >= len(d):
            continue
        j = i - 40 + int(np.argmax(d[i - 40:i + 40]))
        ts.append(t)
        offs.append(j / fr - t)
    return np.array(ts), np.array(offs)


def align(grid, onset, y):
    """Shift the grid so its strong eighths sit on the attacks (the median offset; a per-passage correction was tried and
    made the residuals worse), and report the residuals."""
    d, fr = attacks(y)
    ts, offs = offsets(grid, onset, d, fr)
    shift = float(np.median(offs))
    return grid + shift, shift, offs - shift


def extend(beats, duration):
    """Beats before the first and after the last tracked one at the local period, kept inside the file."""
    b = list(beats)
    while b[0] - (b[1] - b[0]) >= 0:
        b.insert(0, b[0] - (b[1] - b[0]))
    while b[-1] + (b[-1] - b[-2]) < duration:
        b.append(b[-1] + (b[-1] - b[-2]))
    return np.array(b)


def peaks(env, times_from, times_to, min_gap, top, rel=.35):
    i0, i1 = int(times_from * FR), min(len(env), int(times_to * FR))
    seg = env[i0:i1]
    if len(seg) < 3:
        return []
    pk, _ = signal.find_peaks(seg, distance=max(1, int(min_gap * FR)), height=seg.max() * rel)
    pk = sorted(pk, key=lambda k: -seg[k])[:top]
    return sorted((i0 + k) / FR for k in pk)


def analyse(song, plot=None, clicks=None, verbose=True):
    path = MUSIC / (song['id'] + '.mp3')
    data = path.read_bytes()
    y = decode(path)
    duration = len(y) / SR
    f = features(y)
    period = local_period(f['onset'], song['eighth'])
    raw = track(f['onset'], period)
    grid, shift, res = align(smooth(raw), f['onset'], y)
    g = song['group']
    anchor = int(np.argmin(np.abs(grid - song['drop'])))
    beats = extend(grid[anchor % g::g], duration)
    beats = beats[beats >= 0]
    drop_beat = int(np.argmin(np.abs(beats - grid[anchor])))
    first = drop_beat % 4
    def bar(n):
        i = first + 4 * (n - 1)
        return float(beats[i]) if i < len(beats) else duration
    bars = (len(beats) - first + 3) // 4
    drop_bar = (drop_beat - first) // 4 + 1
    lf, lt = song['loop']
    loop = dict(start=round(bar(lf), 4), end=round(bar(lt + 1), 4), fromBar=lf, toBar=lt)
    # The intro edit: the opening plays from the top (after a preroll) to the downbeat after bar cutAfter, then resumes at
    # bar resumeAt so the drop lands on the end of the hold; the preroll makes the sum exact.
    ia, ib = song['intro']['cutAfter'], song['intro']['resumeAt']
    cut, resume, drop = bar(ia + 1), bar(ib), bar(drop_bar)
    preroll = HOLD - cut - (drop - resume)
    intro = dict(hold=HOLD, preroll=round(preroll, 4), cut=round(cut, 4), resume=round(resume, 4), drop=round(drop, 4))
    # Accents.
    acc = []
    # Accents sit on the sixteenth grid (the eighths and their midpoints) when they are within 30 ms of it.
    sixteenths = np.sort(np.concatenate([grid, (grid[1:] + grid[:-1]) / 2]))
    def snap(t):
        k = int(np.argmin(np.abs(sixteenths - t)))
        return float(sixteenths[k]) if abs(sixteenths[k] - t) <= .03 else float(t)
    def add(t, kind, s):
        acc.append(dict(t=round(snap(t), 4), kind=kind, s=round(float(max(0, min(1, s))), 3)))
    on, ch, sub, th = f['onset'], f['chime'], f['sub'], f['thump']
    for a, b in song['chimes']:
        for t in peaks(ch, max(a, .06), b, .3, 8, .4):
            add(t, 'chime', ch[int(t * FR)] / ch[int(a * FR):int(b * FR)].max())
    slam = on / np.percentile(on, 99) + th / np.percentile(th, 99)
    for t in peaks(slam, 0, duration, .9, 64, .62):
        add(t, 'slam', (slam[int(t * FR)] - 1) / 2)
    # Cuts and drops: the sub's mean over the next beat against the previous beat (12 dB either way, alternating), placed
    # on the steepest change of the 20 ms-smoothed sub level within half a beat.
    subs = np.convolve(sub, np.ones(4) / 4, 'same')
    slope = np.diff(subs, prepend=subs[0])
    last = None
    for i in range(1, len(beats) - 1):
        a, b, c = (int(beats[i - 1] * FR), int(beats[i] * FR), int(beats[i + 1] * FR))
        before, after = sub[a:b].mean(), sub[b:c].mean()
        lo, hi = (a + b) // 2, (b + c) // 2
        if after < before - 12 and last != 'cut':
            add((lo + int(np.argmin(slope[lo:hi]))) / FR, 'cut', (before - after) / 30)
            last = 'cut'
        elif after > before + 12 and last == 'cut':
            add((lo + int(np.argmax(slope[lo:hi]))) / FR, 'drop', (after - before) / 30)
            last = 'drop'
    acc.sort(key=lambda a: (a['t'], a['kind']))
    sections = [dict(id=s, fromBar=a, toBar=b) for s, a, b in song['sections']]
    bpm = 60 / np.diff(beats)
    track_json = dict(id=song['id'], file='music/' + song['id'] + '.mp3', duration=round(duration, 4), introEnd=round(drop, 4),
                      beats=[round(float(t), 4) for t in beats], firstDownbeat=int(first), loop=loop, sections=sections)
    out = dict(id=song['id'], title=song['title'], map=song['map'], sha256=hashlib.sha256(data).hexdigest(), bytes=len(data),
               bpm=[round(float(np.percentile(bpm, 5)), 1), round(float(np.median(bpm)), 1), round(float(np.percentile(bpm, 95)), 1)],
               bars=int(bars), dropBar=int(drop_bar), intro=intro, track=track_json, accents=acc)
    # Report: residuals, novelty at section boundaries, the seam.
    if verbose:
        print(f"{song['id']}: {duration:.3f} s, {len(beats)} beats, {bars} bars, BPM 5/50/95% {out['bpm']}, firstDownbeat {first}, drop bar {drop_bar} at {drop:.3f}")
        print(f"  grid aligned to attacks by {1000 * shift:+.1f} ms; strong-eighth residuals: median |r| {1000 * np.median(np.abs(res)):.1f} ms, "
              f"{100 * np.mean(np.abs(res) < .015):.0f}% within 15 ms, {100 * np.mean(np.abs(res) < .03):.0f}% within 30 ms")
        # The grid against the onset envelope: the share of the strongest onset peaks (top 10%, 100 ms apart) within 25 ms of
        # an eighth (chance is about 2x25/eighth), and the envelope's mean on the beats against the off-beat eighths.
        pk, _ = signal.find_peaks(on, distance=int(.1 * FR), height=np.percentile(on, 90))
        near = np.min(np.abs(grid[None, :] - (pk / FR)[:, None]), axis=1)
        near16 = np.min(np.abs(sixteenths[None, :] - (pk / FR)[:, None]), axis=1)
        on_beat = np.mean([on[int(round(t * FR))] for t in beats if t * FR < len(on)])
        off = (beats[1:] + beats[:-1]) / 2
        off_beat = np.mean([on[int(round(t * FR))] for t in off if t * FR < len(on)])
        print(f"  onset check: {100 * np.mean(near <= .025):.0f}% of the {len(pk)} strongest onset peaks lie within 25 ms of the eighth grid "
              f"(chance ~{100 * .05 / np.median(np.diff(grid)):.0f}%), {100 * np.mean(near16 <= .025):.0f}% within 25 ms of a sixteenth; onset on beats / off-beat eighths {on_beat / off_beat:.2f}")
        mel = f['mel']
        def mean(a, b):
            return mel[:, int(a * FR):max(int(a * FR) + 1, int(b * FR))].mean(1)
        nov = []
        for n in range(2, bars):
            nov.append(np.linalg.norm(mean(bar(n), bar(n + 1)) - mean(bar(n - 1), bar(n))))
        med = float(np.median(nov))
        for s in sections:
            n = s['fromBar']
            v = nov[n - 2] / med if 2 <= n < bars else float('nan')
            print(f"  section {s['id']:<10} bars {s['fromBar']:>3}-{s['toBar']:<3} {bar(s['fromBar']):8.3f}-{bar(s['toBar'] + 1):8.3f} s  novelty at start {v:5.2f}x median")
        print(f"  loop bars {lf}-{lt}: {loop['start']:.3f} -> {loop['end']:.3f} ({loop['end'] - loop['start']:.3f} s)")
        print(f"  intro edit: preroll {preroll:.4f} s, plays 0 -> {cut:.4f}, resumes at {resume:.4f}; drop {drop:.4f} lands at hold {HOLD}")
        kinds = {}
        for a in acc:
            kinds[a['kind']] = kinds.get(a['kind'], 0) + 1
        print('  accents', kinds)
    if plot:
        draw(plot, song, y, f, beats, first, out)
    if clicks:
        click_mix(clicks, song, path, y, beats, first, out)
    return out


def draw(folder, song, y, f, beats, first, out):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    dur = len(y) / SR
    t = np.arange(len(f['onset'])) / FR
    colors = {'chime': '#3fb8ff', 'slam': '#ff5a36', 'cut': '#9a9a9a', 'drop': '#ffd23f'}
    sec_colors = ['#20324a', '#2b2140', '#1f3b2e', '#40291f', '#3a3a1c', '#1c3a3a', '#3a1c33', '#2e2e2e', '#1c2a3a', '#3a2a1c']
    tr = out['track']
    def bar(n):
        i = first + 4 * (n - 1)
        return beats[i] if i < len(beats) else dur
    spans = [(0, dur)] + [(i * 30.0, min(dur, i * 30.0 + 30)) for i in range(int(math.ceil(dur / 30)))]
    fig, axes = plt.subplots(len(spans), 1, figsize=(22, 3.1 * len(spans)))
    for row, (a, b) in enumerate(spans):
        ax = axes[row]
        ax.set_facecolor('#0d0f14')
        for k, s in enumerate(tr['sections']):
            ax.axvspan(bar(s['fromBar']), bar(s['toBar'] + 1), color=sec_colors[k % len(sec_colors)], alpha=1, lw=0)
            mid = (bar(s['fromBar']) + bar(s['toBar'] + 1)) / 2
            if a <= mid < b:
                ax.text(mid, 1.02, s['id'], ha='center', va='bottom', color='w', fontsize=9 if row else 8, transform=ax.get_xaxis_transform())
        ax.axvspan(tr['loop']['start'], tr['loop']['end'], ymin=0, ymax=.04, color='#5cff8a')
        w = y[int(a * SR):int(b * SR)]
        step = max(1, len(w) // 6000)
        env = np.abs(w[:len(w) // step * step]).reshape(-1, step).max(1)
        tt = np.linspace(a, b, len(env))
        ax.fill_between(tt, -env, env, color='#7f8fa6', lw=0)
        m = (t >= a) & (t < b)
        on = f['onset'][m] / np.percentile(f['onset'], 99.5)
        ax.plot(t[m], -1 + np.clip(on, 0, 2) * .5, color='#c9a7ff', lw=.6)
        for i, bt in enumerate(beats):
            if a <= bt < b:
                down = (i - first) % 4 == 0 and i >= first
                ax.axvline(bt, color='#ffffff' if down else '#5a6070', lw=1.1 if down else .4, alpha=.9 if down else .6, ymin=.08)
        for acc in out['accents']:
            if a <= acc['t'] < b:
                ax.plot([acc['t']], [.92 - .07 * list(colors).index(acc['kind'])], marker='v', color=colors[acc['kind']], ms=4 + 5 * acc['s'])
        ax.axvline(tr['introEnd'], color='#ffd23f', lw=2.2)
        ax.set_xlim(a, b)
        ax.set_ylim(-1.05, 1.05)
        ax.set_yticks([])
        ax.tick_params(colors='#cccccc', labelsize=8)
    axes[0].set_title(f"{out['title']} ({song['id']}) - BPM {out['bpm'][1]} (5-95%: {out['bpm'][0]}-{out['bpm'][2]}), {out['bars']} bars; "
                      f"white: downbeats, grey: beats, yellow line: drop/introEnd, green strip: loop; markers: chime (blue), slam (red), cut (grey), drop (yellow); purple: onset strength",
                      color='#111', fontsize=10, loc='left')
    fig.patch.set_facecolor('#eeeeee')
    plt.tight_layout()
    fig.savefig(folder / (song['id'] + '-beatmap.png'), dpi=60)
    plt.close(fig)


def click_mix(folder, song, path, y, beats, first, out):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    full = decode(path)
    n = len(full)
    clicks = np.zeros(n)
    def blip(t, freq, dur, gain):
        i = int(t * SR)
        k = np.arange(int(dur * SR))
        s = np.sin(2 * np.pi * freq * k / SR) * np.exp(-k / (dur * SR / 5)) * gain
        e = min(n, i + len(s))
        if 0 <= i < n:
            clicks[i:e] += s[:e - i]
    for i, b in enumerate(beats):
        down = i >= first and (i - first) % 4 == 0
        blip(b, 1760 if down else 1100, .035, .35 if down else .2)
    for a in out['accents']:
        if a['kind'] in ('drop', 'cut'):
            blip(a['t'], 330 if a['kind'] == 'drop' else 220, .12, .4)
    mix = np.clip(full * .8 + clicks, -1, 1).astype('<f4')
    dest = folder / (song['id'] + '-clicks.mp3')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', '-c:a', 'libmp3lame', '-q:a', '3', str(dest)],
                   input=mix.tobytes(), check=True)
    # The boss intro as players hear it: the preroll, the opening up to the cut, the resume to the drop, 12 s of the fight.
    it = out['intro']
    parts = [np.zeros(int(it['preroll'] * SR)), full[:int(it['cut'] * SR)], full[int(it['resume'] * SR):int((it['drop'] + 12) * SR)]]
    edit = np.concatenate(parts)
    tick = np.zeros(len(edit))
    for at in (0.0, HOLD):
        i = int(at * SR)
        k = np.arange(int(.05 * SR))
        s = np.sin(2 * np.pi * 2637 * k / SR) * np.exp(-k / (.01 * SR)) * .3
        tick[i:i + len(s)] += s[:len(tick) - i]
    mix = np.clip(edit * .8 + tick, -1, 1).astype('<f4')
    dest = folder / (song['id'] + '-boss-intro.mp3')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '1', '-i', '-', '-c:a', 'libmp3lame', '-q:a', '3', str(dest)],
                   input=mix.tobytes(), check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--plot')
    ap.add_argument('--clicks')
    args = ap.parse_args()
    if shutil.which('ffmpeg') is None:
        sys.exit('ffmpeg is required')
    songs = [analyse(s, args.plot, args.clicks) for s in SONGS]
    doc = dict(note='Generated by tools/zone_boss_beatmap.py from the owner\'s MP3s; do not edit by hand. See docs/ZONE-BOSS-MUSIC.md.',
               hold=HOLD, songs=songs)
    text = json.dumps(doc, indent=1) + '\n'
    if args.check:
        old = json.loads(OUT.read_text())
        for a, b in zip(old['songs'], songs):
            ta, tb = a['track']['beats'], b['track']['beats']
            assert len(ta) == len(tb) and max(abs(p - q) for p, q in zip(ta, tb)) <= .001, a['id'] + ': beats differ'
            assert a['intro'] == b['intro'] and a['track']['loop'] == b['track']['loop'], a['id'] + ': intro or loop differ'
        print('ZoneBossMusic.json matches the MP3s')
        return
    OUT.write_text(text)
    print('wrote', OUT.relative_to(ROOT))


if __name__ == '__main__':
    main()

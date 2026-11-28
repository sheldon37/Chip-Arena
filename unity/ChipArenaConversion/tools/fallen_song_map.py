#!/usr/bin/env python3
"""Regenerate Resources/FallenBossMusic.json, what the final boss's floor and timing read from Lux Cecidit (docs/EXPEDITION-BOSS-FINAL.md section 6).

Offline tool (not run by CI or any check): needs ffmpeg on PATH plus numpy and scipy. Deterministic for a given ffmpeg build.

    python tools/fallen_song_map.py

Measures, at 20 Hz: the overall level and the bass (20-90 Hz), each 0..1 over the song's own range, and the slams: the strongest percussive hits, in order.
"""
import json
import subprocess
from pathlib import Path

import numpy as np
from scipy import ndimage, signal

ROOT = Path(__file__).resolve().parents[1]
SONG = ROOT / 'Assets/ChipArena/Resources/Audio/music/lux-cecidit.mp3'
OUT = ROOT / 'Assets/ChipArena/Resources/FallenBossMusic.json'
SR, NFFT, HOP, RATE = 22050, 2048, 256, 20


def main():
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(SONG), '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32).astype(np.float64)
    f, _, S = signal.stft(x, SR, nperseg=NFFT, noverlap=NFFT - HOP, boundary=None, padded=False)
    M = np.abs(S)
    fr = SR / HOP

    def band(a, b):
        i = (f >= a) & (f < b)
        return np.sqrt((M[i] ** 2).sum(0))

    def norm(v):
        db = 20 * np.log10(np.maximum(v, 1e-6))
        lo, hi = np.percentile(db, 5), np.percentile(db, 99)
        return np.clip((db - lo) / max(hi - lo, 1e-6), 0, 1)

    def at_rate(v):
        v = ndimage.uniform_filter1d(v, int(fr / RATE))
        n = int(len(v) / fr * RATE)
        return [round(float(v[min(len(v) - 1, int(k * fr / RATE))]), 2) for k in range(n)]

    level = at_rate(norm(np.sqrt((M ** 2).sum(0))))
    bass = at_rate(norm(band(20, 90)))
    # Slams: the percussive onset envelope's strongest peaks, at least a beat apart, kept above a high share of its range.
    lg = np.log1p(M * 50)
    flux = np.maximum(0, np.diff(lg, axis=1)).sum(0)
    flux = np.r_[0, flux]
    flux = flux - ndimage.uniform_filter1d(flux, int(fr * .5))
    flux = np.maximum(flux, 0)
    peaks, _ = signal.find_peaks(ndimage.uniform_filter1d(flux, 3), distance=int(fr * 60 / 152), height=np.percentile(flux, 99.3))
    slams = [round(float(p / fr), 3) for p in peaks]
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f_out:
        f_out.write(json.dumps(dict(title='Lux Cecidit', rate=RATE, level=level, bass=bass, slams=slams), separators=(',', ':')) + '\n')
    print('duration %.2f s, %d samples, %d slams' % (len(x) / SR, len(level), len(slams)))


if __name__ == '__main__':
    main()

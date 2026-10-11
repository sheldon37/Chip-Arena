"""Deterministically synthesize the destruction cue clips (docs/arenas/ARENA_PROPOSAL.md section 3.11).

Writes 22050 Hz mono 16-bit WAVs (the source bank's format) to Assets/ChipArena/Resources/StructureAudio, outside the
83-clip source bank under Assets/Conversion/Resources/Audio. Only the standard library is used; every clip comes from a
fixed seed, so a rerun reproduces the committed bytes (tools/asset-lock.json holds their receipts and
Tests/test_structure_audio.py regenerates and compares them). Core/NativeStructureSounds routes them; they are original
procedural placeholders the owner can replace with recorded sounds under the same names.

usage: python3 tools/synth_structure_audio.py [--out DIR] [--check]
"""
import argparse
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/ChipArena/Resources/StructureAudio'
RATE = 22050
NAMES = ('thud', 'crack', 'gravel', 'rebar', 'groan', 'creak', 'snap', 'boom', 'rumble', 'glass', 'debris')


def buffer(seconds):
    return [0.0] * int(round(seconds * RATE))


class Biquad:
    """RBJ cookbook biquad (lowpass, highpass or bandpass, constant skirt gain)."""

    def __init__(self, kind, freq, q=0.707):
        self.tune(kind, freq, q)
        self.x1 = self.x2 = self.y1 = self.y2 = 0.0

    def tune(self, kind, freq, q=0.707):
        """New coefficients, keeping the filter's state (a sweep without clicks)."""
        w = 2 * math.pi * freq / RATE
        alpha = math.sin(w) / (2 * q)
        cos = math.cos(w)
        if kind == 'low':
            b = ((1 - cos) / 2, 1 - cos, (1 - cos) / 2)
        elif kind == 'high':
            b = ((1 + cos) / 2, -(1 + cos), (1 + cos) / 2)
        else:
            b = (alpha, 0.0, -alpha)
        a0 = 1 + alpha
        self.b = tuple(x / a0 for x in b)
        self.a = (-2 * cos / a0, (1 - alpha) / a0)

    def __call__(self, x):
        y = self.b[0] * x + self.b[1] * self.x1 + self.b[2] * self.x2 - self.a[0] * self.y1 - self.a[1] * self.y2
        self.x2, self.x1, self.y2, self.y1 = self.x1, x, self.y1, y
        return y


def noise(rng):
    return rng.random() * 2 - 1


def add_grain(out, start, length, freq, q, gain, decay, rng):
    """A band-passed noise grain with an exponential decay."""
    bp = Biquad('band', freq, q)
    i0 = int(start * RATE)
    for k in range(int(length * RATE)):
        i = i0 + k
        if i >= len(out):
            break
        out[i] += bp(noise(rng)) * gain * math.exp(-k / RATE / decay)


def add_ping(out, start, freq, gain, decay):
    i0 = int(start * RATE)
    for k in range(int(min(decay * 6, 2.0) * RATE)):
        i = i0 + k
        if i >= len(out):
            break
        t = k / RATE
        out[i] += math.sin(2 * math.pi * freq * t) * gain * math.exp(-t / decay)


def sweep(out, f0, f1, tau, gain, decay, attack=0.004, start=0.0):
    """A sine falling exponentially from f0 toward f1 (time constant tau)."""
    phase = 0.0
    i0 = int(start * RATE)
    for k in range(len(out) - i0):
        t = k / RATE
        f = f1 + (f0 - f1) * math.exp(-t / tau)
        phase += 2 * math.pi * f / RATE
        out[i0 + k] += math.sin(phase) * gain * min(1.0, t / attack) * math.exp(-t / decay)


def thud(rng):
    out = buffer(0.55)
    sweep(out, 95, 42, 0.06, 1.0, 0.11)
    low = Biquad('low', 180)
    click = Biquad('low', 1200)
    for i in range(len(out)):
        t = i / RATE
        out[i] += low(noise(rng)) * 0.9 * math.exp(-t / 0.08)
        if t < 0.015:
            out[i] += click(noise(rng)) * 0.5 * (1 - t / 0.015)
    return [math.tanh(1.5 * x) for x in out]


def crack(rng):
    out = buffer(0.4)
    hp = Biquad('high', 1500)
    body = Biquad('band', 600, 4)
    for i in range(len(out)):
        t = i / RATE
        n = noise(rng)
        out[i] += hp(n) * math.exp(-t / 0.012) + body(n) * 0.9 * math.exp(-t / 0.05)
    for _ in range(3):
        add_grain(out, 0.02 + rng.random() * 0.05, 0.03, 2000 + rng.random() * 2500, 1.5, 0.6, 0.006, rng)
    return out


def gravel(rng):
    out = buffer(0.8)
    for _ in range(120):
        t = min(0.7, rng.expovariate(1 / 0.18))
        add_grain(out, t, 0.004 + rng.random() * 0.011, 1500 + rng.random() * 3500, 2, (0.2 + 0.8 * rng.random()) * math.exp(-t / 0.35), 0.004, rng)
    return out


def rebar(rng):
    out = buffer(1.3)
    for ratio, gain, decay in ((1, 1, 0.6), (1.004, 0.7, 0.55), (2.76, 0.6, 0.35), (5.4, 0.35, 0.2), (8.93, 0.2, 0.12)):
        add_ping(out, 0.0, 820 * ratio, gain, decay)
    hp = Biquad('high', 2500)
    for i in range(int(0.003 * RATE)):
        out[i] += hp(noise(rng)) * 0.8
    return out


def groan(rng):
    out = buffer(1.8)
    bp = Biquad('band', 160, 3)
    low = Biquad('low', 90)
    phase = 0.0
    wobble = 0.0
    for i in range(len(out)):
        t = i / RATE
        wobble += (noise(rng) * 0.02 - wobble * 0.001)
        f = 58 + 10 * math.sin(2 * math.pi * 0.7 * t) + wobble * 40
        phase += 2 * math.pi * f / RATE
        saw = sum(math.sin(phase * h) / h for h in range(1, 9))
        env = min(1.0, t / 0.35) * min(1.0, (1.8 - t) / 0.5) * (1 - 0.25 * (0.5 + 0.5 * math.sin(2 * math.pi * 11 * t)))
        if i % 256 == 0:
            bp.tune('band', 180 - 60 * t / 1.8, 3)
        out[i] = bp(saw) * env * 2.5 + low(noise(rng)) * 0.3 * env
    return out


def creak(rng):
    out = buffer(1.5)
    res = (Biquad('band', 520, 8), Biquad('band', 1250, 6))
    i = 0
    while i < len(out):
        t = i / RATE
        gap = 0.006 + 0.008 * (0.5 + 0.5 * math.sin(2 * math.pi * 1.3 * t + 1)) + rng.random() * 0.002
        out[i] += (0.6 + 0.4 * rng.random()) * min(1.0, t / 0.2) * min(1.0, (1.5 - t) / 0.3)
        i += max(1, int(gap * RATE))
    return [res[0](x) * 3 + res[1](x) * 2 for x in out]


def snap(rng):
    out = buffer(0.35)
    hp = Biquad('high', 800)
    for i in range(len(out)):
        t = i / RATE
        out[i] += hp(noise(rng)) * math.exp(-t / 0.02) * (1.6 if t < 0.002 else 1)
    sweep(out, 140, 70, 0.05, 0.6, 0.06)
    for k in range(2):
        add_grain(out, 0.05 + 0.06 * k + rng.random() * 0.02, 0.04, 2400, 1.2, 0.5 / (k + 1), 0.01, rng)
    return out


def boom(rng):
    out = buffer(1.5)
    sweep(out, 60, 40, 0.3, 1.0, 0.45, attack=0.008)
    low = Biquad('low', 120)
    for i in range(len(out)):
        t = i / RATE
        out[i] += low(noise(rng)) * 1.2 * math.exp(-t / 0.25)
        if t < 0.01:
            out[i] += noise(rng) * 0.3 * (1 - t / 0.01)
    return [math.tanh(1.3 * x) for x in out]


def rumble(rng):
    out = buffer(2.8)
    low = Biquad('low', 110)
    res = Biquad('band', 60, 2)
    brown = 0.0
    mod = 0.0
    for i in range(len(out)):
        t = i / RATE
        brown = brown * 0.995 + noise(rng) * 0.1
        mod += (rng.random() - 0.5) * 0.002 - mod * 0.0005
        env = min(1.0, t / 0.15) * math.exp(-t / 1.6) * min(1.0, (2.8 - t) / 0.8) * (0.8 + 0.4 * max(-0.5, min(0.5, mod * 20)))
        x = low(brown)
        out[i] = (x + res(x) * 0.8) * env
    return out


def glass(rng):
    out = buffer(1.4)
    hp = Biquad('high', 3000)
    for i in range(int(0.08 * RATE)):
        t = i / RATE
        out[i] += hp(noise(rng)) * math.exp(-t / 0.03)
    for _ in range(40):
        t = min(1.2, rng.expovariate(1 / 0.25))
        gain = (0.15 + 0.35 * rng.random()) * math.exp(-t / 0.6)
        for _p in range(2 + int(rng.random() * 2)):
            add_ping(out, t, 2500 + rng.random() * 4500, gain, 0.03 + rng.random() * 0.09)
    return out


def debris(rng):
    out = buffer(3.8)
    for _ in range(90):
        t = rng.expovariate(1 / 1.0)
        if t > 3.6:
            continue
        gain = (0.3 + 0.7 * rng.random()) * math.exp(-t / 1.4)
        add_ping(out, t, 110 + rng.random() * 190, gain * 0.8, 0.025 + rng.random() * 0.035)
        add_grain(out, t, 0.02, 800 + rng.random() * 2200, 1.5, gain * 0.6, 0.006, rng)
    low = Biquad('low', 900)
    for i in range(len(out)):
        t = i / RATE
        out[i] += low(noise(rng)) * 0.15 * math.exp(-t / 1.2) * (0.6 + 0.4 * math.sin(2 * math.pi * 3.1 * t))
    return out


SYNTH = {'thud': thud, 'crack': crack, 'gravel': gravel, 'rebar': rebar, 'groan': groan, 'creak': creak, 'snap': snap, 'boom': boom, 'rumble': rumble, 'glass': glass, 'debris': debris}


def render(name):
    """The clip's 16-bit PCM bytes: synthesized from its own seed, peak-normalized to -1 dBFS, 4 ms fade-out."""
    rng = random.Random('chip-arena-structure-' + name)
    samples = SYNTH[name](rng)
    fade = int(0.004 * RATE)
    for k in range(fade):
        samples[-1 - k] *= k / fade
    peak = max(abs(x) for x in samples) or 1.0
    scale = 0.891 / peak
    return b''.join(struct.pack('<h', max(-32767, min(32767, int(round(x * scale * 32767))))) for x in samples)


def wav_bytes(name):
    import io
    data = io.BytesIO()
    with wave.open(data, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(render(name))
    return data.getvalue()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, default=OUT)
    parser.add_argument('--check', action='store_true', help='compare with the committed clips instead of writing')
    args = parser.parse_args()
    changed = []
    for name in NAMES:
        data = wav_bytes(name)
        path = args.out / (name + '.wav')
        if args.check:
            if not path.is_file() or path.read_bytes() != data:
                changed.append(name)
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        print(f'{path} {len(data)} bytes')
    if changed:
        raise SystemExit('Structure clips differ from the generator: ' + ', '.join(changed))


if __name__ == '__main__':
    main()

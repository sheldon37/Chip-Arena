"""Layer supplied voice and electric SFX at sample zero; preserve pitch and full tails."""
from array import array
from pathlib import Path
import subprocess
import sys
import wave

root = Path(__file__).resolve().parent.parent
rate = 22050

def decode(name):
    data = subprocess.check_output([
        'ffmpeg', '-v', 'error', '-i', str(root / 'scripts/source-audio' / name),
        '-f', 'f32le', '-ac', '1', '-ar', str(rate), '-'
    ])
    samples = array('f', data)
    if sys.byteorder != 'little':
        samples.byteswap()
    peak = max(map(abs, samples), default=1) or 1
    return [x * .70 / peak for x in samples]

impact = decode('electric-hook-impact.wav')
voice = decode('electric-hook-voice.wav')
mix = [(impact[i] if i < len(impact) else 0) +
       (voice[i] if i < len(voice) else 0)
       for i in range(max(len(impact), len(voice)))]
gain = .70 / (max(map(abs, mix), default=1) or 1)
output = array('h', (round(x * gain * 32767) for x in mix))
if sys.byteorder != 'little':
    output.byteswap()
with wave.open(str(root / 'public/audio/electric-hook.wav'), 'wb') as f:
    f.setnchannels(1)
    f.setsampwidth(2)
    f.setframerate(rate)
    f.writeframes(output.tobytes())
print(f'Electric Hook: {len(impact)/rate:.3f}s SFX + {len(voice)/rate:.3f}s voice; both start at sample 0; peak 0.70')

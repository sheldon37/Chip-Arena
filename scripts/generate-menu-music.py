"""Original 128-second synth lounge loop: warm extended chords, sparse keys, soft beat."""
from pathlib import Path
import numpy as np
import wave, subprocess
rate=32000
beat=60/90
length=48*4*beat
n=round(length*rate)
mix=np.zeros((n,2),dtype=np.float64)
rng=np.random.default_rng(407)
def hz(m): return 440*2**((m-69)/12)
def add(at,x,pan=0):
    idx=(round(at*rate)+np.arange(len(x)))%n
    mix[idx,0]+=x*np.sqrt((1-pan)/2)
    mix[idx,1]+=x*np.sqrt((1+pan)/2)
def tone(note,duration,amp,pan=0,at=0,pad=False):
    t=np.arange(round(duration*rate))/rate
    f=hz(note)
    if pad:
        env=np.sin(np.pi*t/duration)**1.7
        x=(np.sin(2*np.pi*f*t)+.3*np.sin(2*np.pi*f*1.002*t)+.13*np.sin(2*np.pi*f*2*t))*env*amp
    else:
        env=np.minimum(1,t/.015)*np.exp(-t/1.1)*np.minimum(1,(duration-t)/.18)
        x=(np.sin(2*np.pi*f*t)+.18*np.sin(2*np.pi*f*2*t))*env*amp
    add(at,x,pan)
    if not pad:
        add(at+beat*.75,x*.23,-pan)
        add(at+beat*1.5,x*.10,pan)
# Dm9, Bbmaj9, Fmaj9, Cadd9; inversions keep the pads in a narrow, calm register.
chords=[[50,57,60,64,69],[46,53,57,60,65],[41,53,57,60,67],[48,55,60,62,67]]
for bar in range(48):
    chord=chords[(bar//2)%4];at=bar*4*beat
    if bar%2==0:
        for j,note in enumerate(chord[1:]):tone(note,beat*9,.065,(j-1.5)*.42,at-.5*beat,True)
    for b in [0,2]:
        tone(chord[0]-12,beat*1.8,.13,-.05,at+b*beat)
        t=np.arange(round(.3*rate))/rate
        add(at+b*beat,np.sin(2*np.pi*(43*t+2*(1-np.exp(-t*28))))*np.exp(-t*20)*.16)
    for b in [1,3]:
        t=np.arange(round(.13*rate))/rate
        noise=rng.normal(0,1,len(t));noise=np.convolve(noise,np.ones(7)/7,mode='same')
        add(at+b*beat,noise*np.exp(-t*42)*.055,.2)
    for b in [.5,1.5,2.5,3.5]:
        t=np.arange(round(.08*rate))/rate
        add(at+b*beat,rng.normal(0,1,len(t))*np.exp(-t*80)*.012,-.35)
    # Space between motifs; three different eight-bar phrases over the longer loop.
    if bar%4 in [0,2]:
        for step in range(3 if bar%4==0 else 2):
            note=chord[1+(step+bar//8)%4]+12
            tone(note,beat*3,.065 if bar<32 else .05,np.sin(bar+step)*.55,at+(step*.75+.5)*beat)
peak=np.max(np.abs(mix));mix*=.78/max(peak,.78)
path=Path('/tmp/chip-menu-synth.wav')
with wave.open(str(path),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(rate);w.writeframes((mix*32767).astype('<i2').tobytes())
subprocess.run(['ffmpeg','-v','error','-y','-i',str(path),'-c:a','libmp3lame','-q:a','3','public/music/menu-afterglow.mp3'],check=True)
print(f'Afterglow: {length:.0f}s, 90 BPM, stereo, peak {np.max(np.abs(mix)):.3f}')

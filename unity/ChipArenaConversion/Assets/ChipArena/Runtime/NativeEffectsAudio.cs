using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The effects sound bank on NativeAudio's existing 24 voices (no new AudioSource; ArenaPresentation's 25 and the 83-clip
    // source bank are untouched). Core NativeSoundBank reads the manifest that the owner's builder copies to
    // Assets/LocalAssets/Resources/EffectsAudio (private assets repository, tools/install_local_assets.py); Unity merges every
    // Resources folder, so Resources.Load("EffectsAudio/...") finds it. Without it the bank is empty: Cue plays today's clip,
    // EventSounds plays the route sound for sound (NativeSoundPlan), and bank-only hooks stay silent. The fixed menu/chip
    // hover ticks below are built in and play identically with or without LocalAssets.
    // Clips load a few per frame after startup (and on first use), so no frame loads the whole bank. Each bank play keeps
    // the limiter gate and the 8 gated-voice cap, takes a voice by priority (NativeVoicePool) and stops at its entry's hard
    // length cap. Presentation only: gameplay, timing and networking are unchanged.
    public sealed partial class NativeAudio
    {
        private NativeSoundBank bank=new NativeSoundBank();private bool bankLoaded;private int bankGeneration=-2;
        private readonly Dictionary<string,AudioClip> bankClips=new Dictionary<string,AudioClip>(StringComparer.Ordinal);
        private readonly Queue<string> bankPreload=new Queue<string>();
        private AudioClip[] bankVoiceClip;private NativeSoundPriority[] bankVoicePriority;private double[] bankVoiceEnd;private bool[] busyScratch;private NativeSoundPriority[] heldScratch;
        private readonly NativeSoundPlay[] planned=new NativeSoundPlay[NativeSoundPlan.MaxPlays];
        // Variation bags are seeded in NativeSoundBank; rate jitter has its own stream so UnityEngine.Random's sequence (today's
        // .97-1.03 rates) is the same with or without the bank.
        private readonly System.Random bankRandom=new System.Random(0x5eed);
        /// <summary>The loaded effects bank (empty without LocalAssets).</summary>
        public NativeSoundBank Bank=>bank;

        private void LoadBank()
        {
            bankLoaded=true;var text=Resources.Load<TextAsset>(NativeSoundKeys.Manifest);
            bank=NativeSoundBank.Parse(text!=null?text.text:null,(uint)Environment.TickCount);
            foreach(var error in bank.Errors)Debug.LogWarning("Effects audio manifest: "+error);
            foreach(var e in bank.Entries)for(int i=0;i<e.Count;i++)bankPreload.Enqueue(e.Path(i));
            bankVoiceClip=new AudioClip[voices.Length];bankVoicePriority=new NativeSoundPriority[voices.Length];bankVoiceEnd=new double[voices.Length];busyScratch=new bool[voices.Length];heldScratch=new NativeSoundPriority[voices.Length];
        }
        private AudioClip BankClip(string path){if(!bankClips.TryGetValue(path,out var c)){c=Resources.Load<AudioClip>(path);bankClips[path]=c;}return c;}

        /// <summary>
        /// A cue with a bank key and today's fallback: the bank's variation while <paramref name="key"/> has clips, nothing while
        /// <paramref name="cover"/> has clips (a recorded cue already holds this sound), else exactly <see cref="Play"/> of
        /// <paramref name="clip"/> (null: silent, as today). mine: the local pilot's own sound (never dropped for a distant one).
        /// </summary>
        public void Cue(string key,string clip,Vector3? position=null,float gain=1,float rate=0,string gate=null,string cover=null,bool mine=false)
        {
            if(key!=null&&BankPlay(key,position,gain,0,gate,mine))return;
            if(cover!=null&&bank.Has(cover))return;
            if(clip!=null)Play(clip,position,gain,rate,gate);
        }
        // The shared UI helpers' click/hover/back/confirm/error (NativeUi.Sound) wait a frame and yield to a screen's own cue
        // played in the same frame (UiCue: the loadout select, the draft pick and hover, the home menu click).
        private string pendingUi;private int pendingUiFrame=-1,explicitUiFrame=-1;
        private void UiHeard(string key){int f=Time.frameCount;if(pendingUi!=null&&pendingUiFrame==f&&pendingUi!=NativeUiSounds.Hover)return;pendingUi=key;pendingUiFrame=f;}
        /// <summary>A screen's own UI cue (key with today's fallback clip); it replaces the generic helper sound of this frame.</summary>
        public void UiCue(string key,string clip,float gain)
        {
            explicitUiFrame=Time.frameCount;
            if(key=="ui.draft.hover"||key==NativeUiSounds.Hover){PlayHover(key=="ui.draft.hover",gain);return;}
            Cue(key,clip,null,gain,0,null,null,true);
        }
        private void FlushUi()
        {
            if(pendingUi==null||Time.frameCount<=pendingUiFrame)return;string key=pendingUi;pendingUi=null;if(pendingUiFrame==explicitUiFrame)return;
            if(key==NativeUiSounds.Hover){PlayHover(false,.35f);return;}
            BankPlay(key,null,key==NativeUiSounds.Confirm?.6f:.5f,0,null,true);
        }

        private AudioClip menuHoverClip,chipHoverClip;private double nextHoverAt;
        private void PlayHover(bool chip,float gain)
        {
            double now=Time.unscaledTimeAsDouble;if(Muted||Effects<=0||now<nextHoverAt)return;
            var voice=Voice();if(voice==null)return;
            var clip=chip?chipHoverClip:menuHoverClip;
            if(clip==null)
            {
                var data=NativeHoverSound.Samples(chip);clip=AudioClip.Create(chip?"Chip highlight":"Menu highlight",data.Length,1,NativeHoverSound.SampleRate,false);clip.SetData(data,0);
                if(chip)chipHoverClip=clip;else menuHoverClip=clip;
            }
            nextHoverAt=now+NativeHoverSound.Cooldown;
            voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=0;voice.pitch=1;voice.loop=false;voice.Play();
        }
        private void ClearHoverClips(){CapturedAnimation.Release(menuHoverClip);CapturedAnimation.Release(chipHoverClip);}

        /// <summary>
        /// One bank play of <paramref name="key"/>. False when the key has no clips or its clip failed to load (the caller falls
        /// back); true when it played or was skipped on purpose (muted, its dedup window, the limiter gate, no voice by priority).
        /// </summary>
        private bool BankPlay(string key,Vector3? position,float gain,float delay=0,string gate=null,bool mine=false,NativeSoundPriority? priority=null,float rateScale=1,float startAt=0,int variation=-1)
        {
            var e=bank.Entry(key);if(e==null||e.Count==0||e.Loop||voices==null)return false;
            var clip=BankClip(e.Path(variation>=0?variation:bank.Next(key)));if(clip==null||clip.loadState==AudioDataLoadState.Failed)return false;
            if(Muted||Effects<=0)return true;if(!bank.Admit(key,Time.unscaledTimeAsDouble))return true;
            if(gate!=null){gain=(float)limiter.Cue(gate,Time.unscaledTime,gain);if(gain<=0)return true;}
            int v=BankVoice(gate!=null,priority??NativeSoundPlan.Priority(e.Priority,mine));if(v<0)return true;
            var voice=voices[v];keyedVoices[v]=gate!=null;gain*=e.Gain;float pan=0;
            if(position.HasValue&&e.Positional){var d=position.Value-listener;gain*=NativeEventSounds.Falloff(d.magnitude);pan=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);}
            float rate=NativeSoundBank.Rate(e,(float)bankRandom.NextDouble(),rateScale);startAt=Mathf.Clamp(startAt,0,Math.Max(0,clip.length-.02f));
            voice.loop=false;voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=rate;
            if(delay>.005f)voice.PlayDelayed(delay);else{voice.Play();if(startAt>0)voice.time=startAt;}
            bankVoiceClip[v]=clip;bankVoicePriority[v]=priority??NativeSoundPlan.Priority(e.Priority,mine);bankVoiceEnd[v]=AudioSettings.dspTime+Math.Max(0,delay)+Math.Min(clip.length-startAt,e.MaxLength)/rate+.02;
            return true;
        }
        private int BankVoice(bool gated,NativeSoundPriority priority)
        {
            for(int i=0;i<voices.Length;i++){busyScratch[i]=voices[i].isPlaying;heldScratch[i]=bankVoiceClip[i]!=null&&voices[i].clip==bankVoiceClip[i]?bankVoicePriority[i]:NativeSoundPriority.Normal;}
            int v=NativeVoicePool.Pick(busyScratch,keyedVoices,heldScratch,gated,priority,out bool steal);
            if(v>=0&&steal){voices[v].Stop();voices[v].loop=false;ReleaseLoopVoice(v);}
            return v;
        }
        // Hard length caps: a bank voice past its entry's cap stops (the clips are cut and faded offline; this is the backstop).
        private void BankCaps()
        {
            double now=AudioSettings.dspTime;
            for(int v=0;v<voices.Length;v++)
            {
                var clip=bankVoiceClip[v];if(clip==null)continue;
                if(voices[v].clip!=clip){bankVoiceClip[v]=null;continue;}
                if(voices[v].loop)continue;
                if(now>=bankVoiceEnd[v]){if(voices[v].isPlaying)voices[v].Stop();bankVoiceClip[v]=null;}
            }
        }
        /// <summary>One fresh event through the bank plan (NativeSoundPlan; with an empty bank, exactly the routed sounds).</summary>
        private void Perform(in NativeSoundPlay s,Vector3 at,Vector3? source,float age,bool mine)
        {
            if(s.Key==null){if(s.Tone)ArcadeTone();else Play(s.Clip,s.Positional?at:(Vector3?)null,s.Gain,s.Rate,s.Gate);return;}
            var where=s.AtSource&&source.HasValue?source.Value:at;
            if(!BankPlay(s.Key,where,s.Gain,Math.Max(0,s.Delay-age),s.Gate,mine,s.Priority)&&s.Clip!=null)Play(s.Clip,s.Positional?at:(Vector3?)null,s.Gain,s.Rate,s.Gate);
        }
        private static bool WeaponDetail(NativeArena arena,string id)
        {var weapons=arena.Content?.weapons;if(weapons==null||string.IsNullOrEmpty(id))return false;for(int i=0;i<weapons.Length;i++)if(weapons[i].id==id)return true;return false;}
        /// <summary>A destruction layer's override clip from the bank (NativeSoundCues.Structure), else null (the synthesized one).</summary>
        private AudioClip BankStructureClip(string id,out NativeSoundEntry entry)
        {
            string key=NativeSoundCues.Structure(id);entry=bank.Entry(key);if(entry==null||entry.Count==0||entry.Loop){entry=null;return null;}
            var clip=BankClip(entry.Path(bank.Next(key)));if(clip==null)entry=null;return clip;
        }

        // Once a frame from Render: load, the UI flush, the caps, then the presentation-timed hooks (fields, fuses, movement).
        private void EffectsFrame(NativeArena arena,bool menu)
        {
            if(!bankLoaded)LoadBank();for(int i=0;i<8&&bankPreload.Count>0;i++)BankClip(bankPreload.Dequeue());
            FlushUi();BankCaps();
            int generation=arena?.Generation??-1;if(generation!=bankGeneration){bankGeneration=generation;StopLoops();strides.Clear();bank.ResetClock();}
            if(arena==null||menu||bank.Count==0){FadeLoops(false,null);return;}
            FieldCues(arena);FadeLoops(true,arena);MoveCues(arena);
        }

        // ---------- field hooks: born cues, mine arming, fuse beeps and loops tied to the field's life ----------
        private struct LoopSlot{public bool Live;public long Field;public int Voice;public string Key;public float Level,Target;public Vector3 At;public AudioClip Clip;}
        private readonly LoopSlot[] loops=new LoopSlot[NativeVoicePool.MaxLoops];
        private readonly long[] loopWant=new long[NativeVoicePool.MaxLoops];private readonly string[] loopWantKey=new string[NativeVoicePool.MaxLoops];
        private readonly float[] loopWantGain=new float[NativeVoicePool.MaxLoops];private readonly Vector3[] loopWantAt=new Vector3[NativeVoicePool.MaxLoops];private int loopWants;
        private void FieldCues(NativeArena arena)
        {
            float t=arena.Time;loopWants=0;
            var fields=arena.ChipFields;
            for(int i=0;i<fields.Count;i++)
            {
                var f=fields[i];string fx=string.IsNullOrEmpty(f.Effect)?null:f.Effect;Vector3 at=f.Attached!=null&&f.Attached.Body!=null?f.Attached.Body.position:f.Position;
                FieldBorn(NativeFieldSounds.Born(f.Kind,fx),(long)f.Id*2,at,f.Born,f.Until-f.Born,t);
                if(NativeFieldSounds.Mine(f.Kind)){float arm=f.Arm>0?f.Arm:f.Born+NativeFieldSounds.MineArm;if(t>=arm&&t-arm<.3f&&t<f.Until&&Once("field-arm:"+f.Id,arm))BankPlay("chip.mine.arm",at,.6f);}
                bool fused=NativeFieldSounds.Fused(f.Kind,fx);
                if(fused&&bank.Has("chip.bomb.fuse-beep"))FuseBeeps(f.Id,at,f.Born,f.Until,t);
                string loop=fused&&!bank.Has("chip.bomb.fuse-beep")?"chip.bomb.fuse-hiss":NativeFieldSounds.Loop(f.Kind,fx);
                if(loop!=null&&t>=f.Born&&t<f.Until)WantLoop((long)f.Id*2,loop,at);
            }
            var zones=arena.AbilityZones;
            for(int i=0;i<zones.Count;i++)
            {
                var z=zones[i];var d=z.Definition;if(d==null)continue;string fx=string.IsNullOrEmpty(d.fx)?null:d.fx;float born=z.Born>0?z.Born:z.Until-(d.fuse>0?(float)d.fuse:0);
                FieldBorn(NativeFieldSounds.Born(z.Kind,fx),(long)z.Id*2+1,z.Position,born,d.fuse>0?(float)d.fuse:z.Until-born,t);
                string loop=NativeFieldSounds.Loop(z.Kind,fx);if(loop!=null&&t>=born&&t<z.Until)WantLoop((long)z.Id*2+1,loop,z.Position);
            }
        }
        // A field's birth cue, once: the warning sweep at the rate that peaks on detonation (the variation whose length is
        // closest to the fuse), the meteor whistle started so that it ends on the impact, else a plain one-shot.
        private void FieldBorn(string key,long field,Vector3 at,float born,float fuse,float t)
        {
            if(key==null||t<born||t-born>.3f||!bank.Has(key)||!Once("field-born:"+field,born))return;
            float left=Math.Max(0,born+fuse-t);
            if(key=="chip.delayed.charge")
            {
                var e=bank.Entry(key);int best=0;float miss=float.MaxValue;
                for(int v=0;v<e.Count;v++){var c=BankClip(e.Path(v));if(c==null)continue;float m=Math.Abs(c.length-fuse);if(m<miss){miss=m;best=v;}}
                var chosen=BankClip(e.Path(best));if(chosen!=null)BankPlay(key,at,.7f,0,null,false,null,NativeFieldSounds.SweepRate(chosen.length,Math.Max(.05f,left)),0,best);return;
            }
            if(key=="magic.meteor.incoming")
            {
                var e=bank.Entry(key);int v=bank.Next(key);var c=BankClip(e.Path(v));if(c!=null)BankPlay(key,at,.8f,0,null,false,null,1,NativeFieldSounds.StartOffset(Math.Min(c.length,e.MaxLength),left),v);return;
            }
            BankPlay(key,at,.7f);
        }
        // MarkBeeps' scheduler for bomb fuses: each beep once, scheduled ahead with PlayDelayed.
        private void FuseBeeps(int id,Vector3 at,float born,float until,float t)
        {
            for(int i=0;NativeFieldSounds.Beep(born,until,i,out double when,out float rate);i++)
            {if(when>t+.08)break;if(when<t-.08||!Once("fuse:"+id+":"+i,(float)when))continue;BankPlay("chip.bomb.fuse-beep",at,.6f,(float)Math.Max(0,when-t),null,false,null,rate);}
        }
        // The loudest MaxLoops wanted loops (by distance falloff) keep a looping voice; the rest fade out.
        private void WantLoop(long field,string key,Vector3 at)
        {
            if(!bank.Has(key))return;float gain=NativeEventSounds.Falloff((at-listener).magnitude);if(gain<.05f)return;
            int slot=loopWants<loopWant.Length?loopWants++:-1;
            if(slot<0){int worst=0;for(int i=1;i<loopWant.Length;i++)if(loopWantGain[i]<loopWantGain[worst])worst=i;if(loopWantGain[worst]>=gain)return;slot=worst;}
            loopWant[slot]=field;loopWantKey[slot]=key;loopWantGain[slot]=gain;loopWantAt[slot]=at;
        }
        private void FadeLoops(bool live,NativeArena arena)
        {
            if(!live||Muted||Effects<=0)loopWants=0;
            for(int s=0;s<loops.Length;s++)
            {
                if(!loops[s].Live)continue;var voice=voices[loops[s].Voice];
                if(voice.clip!=loops[s].Clip||!voice.isPlaying){loops[s].Live=false;continue;}
                loops[s].Target=0;for(int w=0;w<loopWants;w++)if(loopWant[w]==loops[s].Field&&loopWantKey[w]==loops[s].Key){loops[s].Target=1;loops[s].At=loopWantAt[w];loopWant[w]=long.MinValue;}
            }
            for(int w=0;w<loopWants;w++)
            {
                if(loopWant[w]==long.MinValue)continue;int s=Array.FindIndex(loops,l=>!l.Live);if(s<0)break;
                var e=bank.Entry(loopWantKey[w]);var clip=BankClip(e.Path(bank.Next(e.Key)));if(clip==null)continue;
                int v=BankVoice(false,NativeSoundPriority.Low);if(v<0)break;var voice=voices[v];keyedVoices[v]=false;
                voice.clip=clip;voice.loop=true;voice.volume=0;voice.pitch=NativeSoundBank.Rate(e,(float)bankRandom.NextDouble());voice.Play();voice.time=(float)(bankRandom.NextDouble()*Math.Max(0,clip.length-.05f));
                bankVoiceClip[v]=clip;bankVoicePriority[v]=NativeSoundPriority.Low;bankVoiceEnd[v]=double.MaxValue;
                loops[s]=new LoopSlot{Live=true,Field=loopWant[w],Voice=v,Key=e.Key,Level=0,Target=1,At=loopWantAt[w],Clip=clip};
            }
            float step=Time.unscaledDeltaTime/.25f;
            for(int s=0;s<loops.Length;s++)
            {
                if(!loops[s].Live)continue;var l=loops[s];var voice=voices[l.Voice];var e=bank.Entry(l.Key);
                l.Level=Mathf.MoveTowards(l.Level,l.Target,step);
                if(l.Level<=0&&l.Target<=0){voice.Stop();voice.loop=false;bankVoiceClip[l.Voice]=null;l.Live=false;loops[s]=l;continue;}
                var d=l.At-listener;voice.volume=Mathf.Clamp01(l.Level*(e?.Gain??1)*.5f*NativeEventSounds.Falloff(d.magnitude)*Effects);voice.panStereo=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);loops[s]=l;
            }
        }
        private void ReleaseLoopVoice(int v){for(int s=0;s<loops.Length;s++)if(loops[s].Live&&loops[s].Voice==v)loops[s].Live=false;}
        private void StopLoops()
        {
            for(int s=0;s<loops.Length;s++){if(!loops[s].Live)continue;var voice=voices[loops[s].Voice];if(voice.clip==loops[s].Clip){voice.Stop();voice.loop=false;}bankVoiceClip[loops[s].Voice]=null;loops[s].Live=false;}
        }

        // ---------- movement hooks: jump, landing, footsteps by map surface, the local scope in/out ----------
        private struct Stride{public Vector3 Last;public float Distance,AirAt;public bool Grounded,Aiming,Seen;}
        private readonly Dictionary<int,Stride> strides=new Dictionary<int,Stride>();
        private void MoveCues(NativeArena arena)
        {
            float t=arena.Time;var me=arena.Local;string step=NativeFootsteps.Surface(arena.MapId);bool steps=bank.Has(step);
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;int id=p.Seat.id;strides.TryGetValue(id,out var s);var pos=p.Body.position;bool grounded=p.Pose.Grounded,mine=p==me;
                if(p.Hp<=0||p.Death!=null||!s.Seen){s=new Stride{Last=pos,Grounded=grounded,Aiming=p.Pose.Aiming,Seen=p.Hp>0&&p.Death==null,AirAt=t};strides[id]=s;continue;}
                if(s.Grounded&&!grounded){s.AirAt=t;if(p.Pose.Velocity.y>2.5f)BankPlay("move.jump",pos,mine?.4f:.5f,0,null,mine);}
                else if(!s.Grounded&&grounded&&t-s.AirAt>=NativeFootsteps.MinAir)BankPlay("move.land",pos,mine?.45f:.55f,0,null,mine);
                var delta=pos-s.Last;delta.y=0;float moved=delta.magnitude;
                if(moved>NativeFootsteps.MaxStep||!grounded||t<p.RollEnd)s.Distance=0;
                else if(steps&&moved/Math.Max(1e-3f,Time.unscaledDeltaTime)>=NativeFootsteps.MinSpeed){s.Distance+=moved;if(s.Distance>=NativeFootsteps.Stride){s.Distance-=NativeFootsteps.Stride;BankPlay(step,pos,mine?.3f:.45f);}}
                if(mine&&p.Pose.Aiming!=s.Aiming&&p.Weapon?.@class=="ranged")BankPlay("gun.ads",null,.5f,0,null,true);
                s.Last=pos;s.Grounded=grounded;s.Aiming=p.Pose.Aiming;strides[id]=s;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The footsteps and landings (owner 2026-10-15: "can we add sounds for footsteps and zombies?") on NativeAudio's existing voices
    // (no new AudioSource): the presentation's footfalls (Runtime/NativeFootfallPresentation.cs, from the drawn gaits' plants),
    // chosen by Core NativeStepMix (my own always; the others loudest first, at most four a frame, within each family's rate and
    // range), each on its surface (the block underfoot, else the map's floor: NativeFootsteps.Surface). Every play takes its key's
    // clips from the LocalAssets bank, else an existing stand-in key, else its built-in synthesized takes (Core NativeCreatureSynth),
    // so a build without the bank still steps. Mine is normal priority, the rest low (never one of the four reserved voices).
    // Presentation only: gameplay, timing and networking are unchanged.
    public sealed partial class NativeAudio
    {
        /// <summary>The presentation's footfall queue (ConversionGame wires ArenaPresentation.Footfalls; null: no footsteps).</summary>
        [NonSerialized] public NativeFootfallQueue Footfalls;
        private readonly NativeStepMix stepMix=new NativeStepMix();
        private readonly Dictionary<string,AudioClip[]> creatureSynth=new Dictionary<string,AudioClip[]>(StringComparer.Ordinal);
        private readonly Dictionary<string,int> creatureLast=new Dictionary<string,int>(StringComparer.Ordinal);
        private int creatureWarm;

        private void StepCues(NativeArena arena)
        {
            WarmCreatureSynth(1);var q=Footfalls;if(q==null)return;
            if(Muted||Effects<=0){q.Clear();return;}
            int n=stepMix.Choose(q,listener.x,listener.y,listener.z,arena.Local?.Seat.id??0,Time.unscaledDeltaTime);
            for(int i=0;i<n;i++)
            {
                var pick=stepMix[i];var f=q[pick.Index];var at=new Vector3(f.X,f.Y,f.Z);
                if(f.Zombie)
                {
                    string zk=NativeZombieSteps.Key(f.Kind,f.Crawl,f.Run);
                    CreatureCue(zk,NativeZombieSteps.StandIn(zk,NativeFootsteps.Surface(arena.MapId)),NativeZombieSteps.StandInRate(zk,f.Kind),at,pick.Gain,NativeZombieSteps.Rate(f.Kind,NativeHordeKinds.Seed(f.Id)),NativeSoundPriority.Low);
                    continue;
                }
                var priority=pick.Mine?NativeSoundPriority.Normal:NativeSoundPriority.Low;
                if(f.Land){CreatureCue("move.land",null,1,at,pick.Gain,NativeFootsteps.LandRate(f.Air),priority);continue;}
                CreatureCue(NativeFootsteps.Surface(arena.MapId,SurfaceUnder(at)),null,1,at,pick.Gain,NativeFootsteps.StepRate(NativeFootsteps.Gait(f.Speed,f.Sprint)),priority);
            }
            q.Clear();
        }
        // The material of the block under a foot (null: none within reach, the map's floor).
        private static string SurfaceUnder(Vector3 at)
        {
            if(!Physics.Raycast(at+Vector3.up*.4f,Vector3.down,out var hit,1.2f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)||hit.collider==null)return null;
            return hit.collider.TryGetComponent<NativePropBody>(out var prop)?prop.Definition?.material:null;
        }
        /// <summary>
        /// One play of `key` at `at`: its bank clips, else `standIn`'s (at `standInRate`), else its synthesized takes. False only when
        /// none exists (a key the synth does not own and no clip).
        /// </summary>
        private bool CreatureCue(string key,string standIn,float standInRate,Vector3? at,float gain,float rate,NativeSoundPriority priority)
        {
            if(BankPlay(key,at,gain,0,null,false,priority,rate))return true;
            if(standIn!=null&&BankPlay(standIn,at,gain*.85f,0,null,false,priority,standInRate))return true;
            return SynthPlay(key,at,gain,rate,priority);
        }
        /// <summary>A synthesized take of `key` (never the one before it) on a voice by priority, falloff and pan as a bank play.</summary>
        private bool SynthPlay(string key,Vector3? at,float gain,float rate,NativeSoundPriority priority)
        {
            if(voices==null||!NativeCreatureSynth.Has(key))return false;if(Muted||Effects<=0)return true;
            creatureLast.TryGetValue(key,out int last);int n=NativeCreatureSynth.Variants,take=(last+1+(int)(bankRandom.NextDouble()*(n-1)))%n;creatureLast[key]=take;
            var clip=CreatureSynthClip(key,take);if(clip==null)return false;
            int v=BankVoice(false,priority);if(v<0)return true;var voice=voices[v];keyedVoices[v]=false;float pan=0;
            if(at.HasValue){var d=at.Value-listener;gain*=NativeEventSounds.Falloff(d.magnitude);pan=NativeEventSounds.Pan(NativeArena.Vec(d),yaw);}
            float pitch=Mathf.Clamp(rate*(1+((float)bankRandom.NextDouble()*2-1)*.04f),.4f,2.5f);
            voice.loop=false;voice.clip=clip;voice.volume=Mathf.Clamp01(gain*Effects);voice.panStereo=pan;voice.pitch=pitch;voice.Play();
            bankVoiceClip[v]=clip;bankVoicePriority[v]=priority;bankVoiceEnd[v]=AudioSettings.dspTime+clip.length/pitch+.02;
            return true;
        }
        private AudioClip CreatureSynthClip(string key,int take)
        {
            if(!creatureSynth.TryGetValue(key,out var takes)){takes=new AudioClip[NativeCreatureSynth.Variants];creatureSynth[key]=takes;}
            if(takes[take]==null){var data=NativeCreatureSynth.Samples(key,take);if(data==null)return null;var c=AudioClip.Create("Built-in "+key+" "+take,data.Length,1,NativeCreatureSynth.Rate,false);c.SetData(data,0);takes[take]=c;}
            return takes[take];
        }
        // The synthesized takes of the keys the bank has no clips for are made a few at a time (one a frame in a match, and the
        // stage loading screen's Preload), so no play makes one.
        private int CreatureSynthPending{get{int n=0;for(int k=creatureWarm/NativeCreatureSynth.Variants;k<NativeCreatureSynth.Keys.Length;k++)if(!bank.Has(NativeCreatureSynth.Keys[k]))n+=NativeCreatureSynth.Variants;return n;}}
        private int WarmCreatureSynth(int count)
        {
            int made=0,all=NativeCreatureSynth.Keys.Length*NativeCreatureSynth.Variants;
            while(made<count&&creatureWarm<all)
            {
                string key=NativeCreatureSynth.Keys[creatureWarm/NativeCreatureSynth.Variants];int take=creatureWarm%NativeCreatureSynth.Variants;creatureWarm++;
                if(bank.Has(key))continue;if(!creatureSynth.TryGetValue(key,out var takes)||takes[take]==null){CreatureSynthClip(key,take);made++;}
            }
            return made;
        }
        private void ClearCreatureSynth(){foreach(var takes in creatureSynth.Values)if(takes!=null)foreach(var c in takes)CapturedAnimation.Release(c);creatureSynth.Clear();creatureWarm=0;}
    }
}

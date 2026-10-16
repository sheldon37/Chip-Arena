using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The footsteps and landings (owner 2026-10-15: "can we add sounds for footsteps and zombies?") on NativeAudio's existing voices
    // (no new AudioSource): the presentation's footfalls (Runtime/NativeFootfallPresentation.cs, from the drawn gaits' plants),
    // chosen by Core NativeStepMix (my own always; the others loudest first, at most four a frame, within each family's rate and
    // range), each on its surface: one probe down at the planted foot (Core NativeFootSurface: the block's or the stair wedge's
    // material, else the map's floor) through the walker's surface memory (Core NativeSurfaceMemory: a new surface takes over on its
    // second plant in a row, so a seam or a rail never flips one step). Pilots step in their surface's pool; zombies keep their
    // gait's takes on the hard floors and step in the surface's pool on metal, wood, gravel and snow (a crawl always drags). Every
    // play takes its key's own clips from the LocalAssets bank only; a key without clips plays an existing stand-in key, else its
    // built-in synthesized takes (Core NativeCreatureSynth), so a build without the bank still steps, and no key ever mixes in
    // another's takes. Mine is normal priority, the rest low (never one of the four reserved voices).
    // Presentation only: gameplay, timing and networking are unchanged.
    public sealed partial class NativeAudio
    {
        /// <summary>The presentation's footfall queue (ConversionGame wires ArenaPresentation.Footfalls; null: no footsteps).</summary>
        [NonSerialized] public NativeFootfallQueue Footfalls;
        private readonly NativeStepMix stepMix=new NativeStepMix();
        private readonly Dictionary<string,AudioClip[]> creatureSynth=new Dictionary<string,AudioClip[]>(StringComparer.Ordinal);
        private readonly Dictionary<string,int> creatureLast=new Dictionary<string,int>(StringComparer.Ordinal);
        private int creatureWarm;
        // Each walker's surface (pilots by seat, adds by id) and the physics materials' names (read once: Object.name allocates).
        private readonly Dictionary<int,NativeSurfaceMemory> pilotSurfaces=new Dictionary<int,NativeSurfaceMemory>(),zombieSurfaces=new Dictionary<int,NativeSurfaceMemory>();
        private readonly Dictionary<PhysicsMaterial,string> surfaceNames=new Dictionary<PhysicsMaterial,string>();

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
                    ZombieStep(arena,f.Id,zk,f.Kind,f.FootX,f.Y,f.FootZ,f.Scale,at,pick.Gain,NativeZombieSteps.Rate(f.Kind,NativeHordeKinds.Seed(f.Id)));
                    continue;
                }
                var priority=pick.Mine?NativeSoundPriority.Normal:NativeSoundPriority.Low;
                if(f.Land){pilotSurfaces.Remove(f.Id);CreatureCue("move.land",null,1,at,pick.Gain,NativeFootsteps.LandRate(f.Air),priority);continue;}
                CreatureCue(StepSurface(pilotSurfaces,f.Id,arena.MapId,f.FootX,f.Y,f.FootZ,f.Scale),null,1,at,pick.Gain,NativeFootsteps.StepRate(NativeFootsteps.Gait(f.Speed,f.Sprint)),priority);
            }
            q.Clear();
        }
        /// <summary>A zombie footfall of gait key <paramref name="gait"/>: its gait's takes on a hard floor and for a crawl, else its
        /// surface's own step pool at the add's pitch (NativeFootSurface.Zombie). Also the Hulk's charge warning stomps.</summary>
        private void ZombieStep(NativeArena arena,int id,string gait,int kind,float x,float groundY,float z,float scale,Vector3 at,float gain,float rate)
        {
            string step=gait==NativeZombieSteps.Crawling?null:StepSurface(zombieSurfaces,id,arena.MapId,x,groundY,z,scale),key=NativeFootSurface.Zombie(gait,step);
            if(key==gait)CreatureCue(gait,NativeZombieSteps.StandIn(gait,step??NativeFootsteps.Surface(arena.MapId)),NativeZombieSteps.StandInRate(gait,kind),at,gain,rate,NativeSoundPriority.Low);
            else CreatureCue(key,null,1,at,gain,rate,NativeSoundPriority.Low);
        }
        // The step key under a walker's planted foot through its surface memory (a new surface counts from its second plant in a row).
        private string StepSurface(Dictionary<int,NativeSurfaceMemory> surfaces,int id,string map,float x,float groundY,float z,float scale)
        {
            if(surfaces.Count>512)surfaces.Clear();
            surfaces.TryGetValue(id,out var memory);
            string step=memory.Plant(NativeFootSurface.Key(map,SurfaceUnder(x,groundY,z,scale,out string material),material),Time.unscaledTime);
            surfaces[id]=memory;return step;
        }
        // One probe down at a foot (Core NativeFootSurface's start and reach): whether it hit the world, and the material its collider
        // names (a block's own; a stair or ramp wedge's physics material, its treads'; null for an unnamed collider).
        private bool SurfaceUnder(float x,float groundY,float z,float scale,out string material)
        {
            material=null;
            if(!Physics.Raycast(new Vector3(x,NativeFootSurface.StartY(groundY,scale),z),Vector3.down,out var hit,NativeFootSurface.Reach(scale),NativeArena.WorldMask,QueryTriggerInteraction.Ignore)||hit.collider==null)return false;
            if(hit.collider.TryGetComponent<NativePropBody>(out var prop)&&prop.Definition!=null){material=prop.Definition.material;return true;}
            var physics=hit.collider.sharedMaterial;
            if(physics!=null){if(!surfaceNames.TryGetValue(physics,out material)){material=physics.name;surfaceNames[physics]=material;}}
            return true;
        }
        /// <summary>
        /// One play of `key` at `at`: its bank clips, else `standIn`'s (at `standInRate`), else its synthesized takes. A key with clips
        /// plays only its own (a clip that fails to load is a silent step, never another key's or a built-in take). False only when
        /// none exists (a key the synth does not own and no clip).
        /// </summary>
        private bool CreatureCue(string key,string standIn,float standInRate,Vector3? at,float gain,float rate,NativeSoundPriority priority)
        {
            if(bank.Has(key)){BankPlay(key,at,gain,0,null,false,priority,rate);return true;}
            if(standIn!=null&&bank.Has(standIn)){BankPlay(standIn,at,gain*.85f,0,null,false,priority,standInRate);return true;}
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

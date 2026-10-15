using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The horde's roaming as every peer reads it (owner 2026-10-15: "Monsters outside of the hub should walk around, every now
    /// and then stand idle for moments. Only start chasing the player when they detect them (Either through vision or audio)";
    /// docs/EXPEDITION-HUB.md "Town zombie roaming and perception"). Presentation only: reads the add, never writes it.
    /// <list type="bullet">
    /// <item><see cref="Read"/> is the one place the host's roaming record is read: Astra's HubZombieView (NativePveAdd.Hub,
    /// replicated as NativeMinionFrame hubState / hubAlertAt / hubLookYaw; Core/NativeHubPerception). The town's roaming
    /// population carries it; the state's start time is not replicated, so each peer starts a state on the frame it sees it
    /// (blends only; the alert's own moments run from hubAlertAt).</item>
    /// <item>Adds without the record (instance hordes, DEV monsters, the town's final encounter, an add still coming in) keep
    /// today's look, with the motion-inferred fallback limited to standing apart from hunting (<see cref="Roams"/> false): an
    /// add that stands with no pilot within reach and no attack for a while shows the idle variants; nothing else changes.</item>
    /// <item>The Hulk keeps its own heavy presentation.</item>
    /// </list>
    /// </summary>
    public static class NativeHordeRoamRecord
    {
        public const float Reach=14;
        public static bool On(NativeMinion m)=>m?.Pve?.Stats!=null&&m.Pve.Stats.id!="hulk";
        /// <summary>The host's roaming record for the add (State None: not carried; infer from motion).</summary>
        public static HordeRoamWire Read(NativeMinion m)=>NativeHordeRoam.FromHub(m?.Pve?.Hub);
        /// <summary>Whether the add's inference may read a wander and an alert: only a roaming population (none ships without the
        /// record today, so every add without it infers standing and hunting only).</summary>
        public static bool Roams(NativeMinion m)=>false;
        /// <summary>The add's horizontal ground speed as the authority moves it (the host's body, a guest's replicated velocity).</summary>
        public static float Speed(NativeArena arena,NativeMinion m)
        {
            var v=arena.Replica?m.NetworkVelocity:m.Body.isKinematic?Vector3.zero:m.Body.linearVelocity;return new Vector2(v.x,v.z).magnitude;
        }
        /// <summary>Whether the add is engaged for the inference: it attacked lately (it stands to strike), or, with no record, a
        /// living pilot is within <see cref="Reach"/> (an instance horde always hunts).</summary>
        public static bool Engaged(NativeArena arena,NativeMinion m,float now,in HordeRoamWire w)
        {
            if(now-m.LastAttack>=0&&now-m.LastAttack<NativeHordeRoam.EngagedFor)return true;if(w.State!=HordeRoamState.None)return false;
            var at=m.Body.position;var list=arena.Pilots;
            for(int i=0;i<list.Count;i++){var p=list[i];if(p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss&&p.Body!=null&&(p.Body.position-at).sqrMagnitude<Reach*Reach)return true;}
            return false;
        }
    }

    public sealed partial class ArenaPresentation
    {
        /// <summary>
        /// The add's roaming read this frame (off for the Hulk and while it comes in), and the pilot its head and alert stance
        /// follow: none while calm (a wandering, idling or searching zombie has not noticed anyone); while alerted or chasing, the
        /// nearest within 14 m (today's) when it lies within 75 degrees of where the host has it looking, else none (it noticed
        /// something else: the head follows the record's look).
        /// </summary>
        private HordeRoamPose HordeRoam(NativeArena arena,NativeMinion m,HordeKindState s,float now,bool entering,ref NativePilot target)
        {
            bool on=!entering&&NativeHordeRoamRecord.On(m);var wire=on?NativeHordeRoamRecord.Read(m):default;
            var r=s.Roam.Read(on,wire,m.Id,now,on?NativeHordeRoamRecord.Speed(arena,m):0,on&&NativeHordeRoamRecord.Engaged(arena,m,now,wire),NativeHordeRoamRecord.Roams(m));
            if(!r.On)return r;
            if(NativeHordeRoam.Calm(r.State))target=null;
            else if(target!=null&&r.HasLookYaw)
            {
                var d=target.Body.position-m.Body.position;if(d.x*d.x+d.z*d.z>.01f&&Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,r.LookYaw))>75)target=null;
            }
            return r;
        }
    }

    public sealed partial class NativeAudio
    {
        // ---------- the town zombies' voices (Core NativeHordeRoam): sparse low moans while calm, a shriek on noticing ----------
        // The town's roaming population only (adds with the host's record: the instance hordes sound as today). Bank keys
        // pve.zombie.moan / pve.zombie.shriek when the owner's LocalAssets bank has them, else existing bank stand-ins (the add
        // death's groan pitched down, the Siren's rush); silent without the bank, as every horde cue. The moments are a pure
        // function of the add's seed, the clock and the record (the same on every peer), positional, attenuated with distance
        // (NativeEventSounds.Falloff), never more than one moan a MoanGap, and low priority.
        private readonly Dictionary<int,NativeHordeRoamTrack> hordeRoamTracks=new Dictionary<int,NativeHordeRoamTrack>();private readonly Stack<NativeHordeRoamTrack> hordeRoamSpare=new Stack<NativeHordeRoamTrack>();
        private readonly Dictionary<int,float> hordeRoamSeen=new Dictionary<int,float>();private readonly List<int> hordeRoamStale=new List<int>();private float hordeMoanNext=-1,hordeRoamSweep;
        private void HordeRoamCue(NativeArena arena,NativeMinion m,int kind,Vector3 pos,float t)
        {
            if(!NativeHordeRoamRecord.On(m))return;var wire=NativeHordeRoamRecord.Read(m);if(wire.State==HordeRoamState.None)return;
            if(!hordeRoamTracks.TryGetValue(m.Id,out var track)){track=hordeRoamSpare.Count>0?hordeRoamSpare.Pop():new NativeHordeRoamTrack();track.Reset();hordeRoamTracks[m.Id]=track;}hordeRoamSeen[m.Id]=t;
            var r=track.Read(true,wire,m.Id,t,0,false);
            if(t<hordeMoanNext-5)hordeMoanNext=-1;uint seed=NativeHordeKinds.Seed(m.Id);
            // The shriek with the scream's opening (once per alert).
            if(!float.IsNaN(r.AlertAge))
            {
                float at=NativeHordeRoam.ShriekAt(t-r.AlertAge);
                if(t>=at&&t-at<.12f&&Once("add"+m.Id+"y",Mathf.Round(at*20)/20)&&!BankPlay("pve.zombie.shriek",pos,NativeHordeRoam.ShriekGain,0,null,false,null,NativeHordeRoam.MoanRate(seed,kind)))
                    BankPlay("siren.rush",pos,NativeHordeRoam.ShriekGain*.75f,0,null,false,null,1.08f*NativeHordeRoam.MoanRate(seed,kind));
            }
            // A moan now and then while it wanders, idles or searches, near the listener only.
            if(NativeHordeRoam.Calm(r.State)&&(pos-listener).sqrMagnitude<NativeHordeRoam.MoanRange*NativeHordeRoam.MoanRange)
            {
                float at=NativeHordeRoam.MoanAt(seed,t);
                if(!float.IsNaN(at)&&t>=at&&t-at<.12f&&t>=hordeMoanNext&&Once("add"+m.Id+"n",at))
                {
                    hordeMoanNext=t+NativeHordeRoam.MoanGap;float rate=NativeHordeRoam.MoanRate(seed,kind),gain=NativeHordeRoam.MoanGain*(r.State==HordeRoamState.Idle?.8f:1);
                    if(!BankPlay("pve.zombie.moan",pos,gain,0,null,false,NativeSoundPriority.Low,rate))BankPlay("pve.add.death",pos,gain*.85f,0,null,false,NativeSoundPriority.Low,.74f*rate);
                }
            }
        }
        // A new arena forgets every track.
        private void HordeRoamClear(){foreach(var pair in hordeRoamTracks)hordeRoamSpare.Push(pair.Value);hordeRoamTracks.Clear();hordeRoamSeen.Clear();hordeMoanNext=-1;hordeRoamSweep=0;}
        // Tracks of adds gone for 2 s go back to the spares (swept every 2 s; no allocation once warm).
        private void HordeRoamSweep(float t)
        {
            if(t<hordeRoamSweep&&t>hordeRoamSweep-10)return;hordeRoamSweep=t+2;hordeRoamStale.Clear();
            foreach(var pair in hordeRoamSeen)if(t-pair.Value>2||t<pair.Value-1)hordeRoamStale.Add(pair.Key);
            for(int i=0;i<hordeRoamStale.Count;i++){int id=hordeRoamStale[i];hordeRoamSeen.Remove(id);if(hordeRoamTracks.TryGetValue(id,out var track)){hordeRoamTracks.Remove(id);hordeRoamSpare.Push(track);}}
        }
    }
}

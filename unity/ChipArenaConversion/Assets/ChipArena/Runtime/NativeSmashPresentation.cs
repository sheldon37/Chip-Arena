using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // A smashed body (Core NativeSmashGib): a pinned body's ragdoll bodies are kinematic and posed by the plan's sprawl, then
    // drawn flattened onto the floor (ArenaPresentation.SmashOverlay); a thrown body holds for the hit-stop, then flies
    // along the smash's travel with its plan's parts crushed to fragments (SetupKill). Presentation only: no collider here
    // reaches the authority.
    internal sealed partial class NativeCorpse
    {
        /// <summary>The smash this body shows (null: none).</summary>
        internal readonly SmashPlan Smash;
        /// <summary>A thrown smash's crushed parts past the first (<see cref="CrushPart"/>).</summary>
        internal readonly List<int> ExtraCrush=new List<int>();
        internal bool IsCrushed(int part)=>part>=0&&(part==CrushPart||ExtraCrush.Contains(part));
        private bool Pinned=>Smash!=null&&Smash.Pinned;
        private Vector3[] smashAt;private Quaternion[] smashTurn;private NVector3[] smashJoints;private NativeSmashGib.Move[] smashMoves;private Vector3[] smashVelocity,smashSpin;
        private Vector3 smashWeaponAt,smashWeaponVelocity;private Quaternion smashWeaponTurn;private int smashWeaponAnchor=-1;private bool smashReleased,smashSettled;
        /// <summary>The smash's blood the presentation has sprayed and laid (NativeSmashGib.Gush / Pool), in order.</summary>
        internal int SmashFired,SmashPooled;
        private static Quaternion Q(NQuaternion q)=>new Quaternion(q.X,q.Y,q.Z,q.W);
        /// <summary>At the end of the build: a pinned body goes kinematic and collisionless (the sprawl poses it), its weapon with
        /// it; a thrown body holds still for the hit-stop with each body's flight kept for the release.</summary>
        private void SmashSetup()
        {
            if(Smash==null)return;smashAt=new Vector3[22];smashTurn=new Quaternion[22];
            foreach(var pair in Parts){smashAt[pair.Key]=pair.Value.position;smashTurn[pair.Key]=pair.Value.rotation;}
            smashWeaponAnchor=Anchor[WeaponHand(Death)];if(weapon!=null){smashWeaponAt=weapon.position;smashWeaponTurn=weapon.rotation;}
            if(Smash.Pinned)
            {
                smashJoints=new NVector3[22];for(int b=0;b<22;b++)smashJoints[b]=N(Bind[b].GetColumn(3));smashMoves=new NativeSmashGib.Move[22];
                foreach(var body in Parts.Values){body.linearVelocity=body.angularVelocity=Vector3.zero;body.isKinematic=true;body.GetComponent<Collider>().enabled=false;}
                if(weapon!=null){weapon.linearVelocity=weapon.angularVelocity=Vector3.zero;weapon.isKinematic=true;weapon.GetComponent<Collider>().enabled=false;}
                return;
            }
            smashVelocity=new Vector3[22];smashSpin=new Vector3[22];var centre=Parts.TryGetValue(CapturedRig.Pelvis,out var pelvis)?pelvis.position:Death.position;
            foreach(var pair in Parts)
            {
                var body=pair.Value;if(Still(pair.Key))continue;NativeSmashGib.Throw(Smash,pair.Key,N(body.position),N(centre),out var v,out var w);
                smashVelocity[pair.Key]=Vector3.ClampMagnitude(U(v),(float)NativeKillEffectRules.MaxBodySpeed);smashSpin[pair.Key]=U(w);body.linearVelocity=body.angularVelocity=Vector3.zero;body.isKinematic=true;
            }
            if(weapon!=null){smashWeaponVelocity=smashWeaponAnchor>=0?smashVelocity[smashWeaponAnchor]:Vector3.zero;weapon.linearVelocity=weapon.angularVelocity=Vector3.zero;weapon.isKinematic=true;}
        }
        /// <summary>Each fixed step: a pinned body takes its sprawl at the corpse clock until it is down; a thrown body leaves
        /// the hit-stop with its flight.</summary>
        private void DriveSmash(float at)
        {
            if(Smash==null)return;
            if(Smash.Pinned)
            {
                if(smashSettled)return;float progress=NativeSmashGib.Progress(Smash,at);NativeSmashGib.Sprawl(Smash,progress,smashJoints,smashMoves);
                foreach(var pair in Parts){var m=smashMoves[pair.Key];pair.Value.transform.SetPositionAndRotation(U(NativeSmashGib.Apply(m,N(smashAt[pair.Key]))),Q(m.Turn)*smashTurn[pair.Key]);}
                if(weapon!=null&&smashWeaponAnchor>=0){var m=smashMoves[smashWeaponAnchor];weapon.transform.SetPositionAndRotation(U(NativeSmashGib.Apply(m,N(smashWeaponAt))),Q(m.Turn)*smashWeaponTurn);}
                if(progress>=1)smashSettled=true;return;
            }
            if(smashReleased||at+1e-6f<Smash.Contact)return;smashReleased=true;
            foreach(var pair in Parts){if(Still(pair.Key))continue;var body=pair.Value;body.isKinematic=false;body.linearVelocity=smashVelocity[pair.Key];body.angularVelocity=smashSpin[pair.Key];}
            if(weapon!=null){weapon.isKinematic=false;weapon.linearVelocity=smashWeaponVelocity;weapon.angularVelocity=new Vector3(2,4,1);}
        }
    }

    /// <summary>
    /// The hazard gibs (owner 2026-10-14: "Need to add a "smash" gib, something that can be used when getting hit by the train on
    /// cyber city, or smashed from things on factory"; "Keep the smash even with kill effects"; "Always show gib in saws too").
    /// Presentation only, from replicated data only (the death record and its hazard id, the map's hazards, the round,
    /// FightAt): every peer builds the same plan (Core NativeSmashGib), so host, guests and the killcam show the same smash; a
    /// late joiner sees the body as it lies. A Factory saw splits the body (NativeBisectPresentation); Cyber City's train, a
    /// press ram and the crane's load smash it:
    /// <list type="bullet">
    /// <item>Pinned (a press ram or the crane's load coming down over the body): it buckles and sprawls as the crusher comes
    /// down and is drawn squashed flat onto the floor in the crusher's own timing (the overlay below), blood squirting out
    /// from under the crusher's edges, a pool and its spill laid on the floor round it, a low blood mist at the edges. The
    /// flattened remains stay for the round.</item>
    /// <item>Thrown (the train; a crusher's edge): a hit-stop, then the body flies along the travel with several parts crushed
    /// to fragments thrown at the train's pace, a heavy blood burst and mist along the travel, streaks on the track bed, the
    /// stumps' sprays trailing it, and (the train) an impact flash.</item>
    /// </list>
    /// The gate (NativeSmashGib.Applies) is gore on, a pilot's own body, not a revivable PvE body, not a Siren or vent
    /// ending; any kill effect style is drawn as the hazard gib instead (the replicated style, feed and credit unchanged).
    /// Reduced effects and the low graphics tier thin the blood, the splats and the mist.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private sealed class HazardGib{public SmashPlan Smash;public int Saw=-1;}
        private static readonly HazardGib NoGib=new HazardGib();
        private readonly Dictionary<long,HazardGib> hazardGibs=new Dictionary<long,HazardGib>();
        private static NVector3 SmashN(Vector3 v)=>new NVector3(v.x,v.y,v.z);
        private static Vector3 SmashU(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);
        /// <summary>The hazard gib a pilot's death shows (its smash plan, or the saw that splits it), or null: not a stage-hazard
        /// death that gibs, or the gate refuses. Planned once per death (id and seed) and kept for the round.</summary>
        private HazardGib HazardGibOf(NativeArena arena,NativeDeath death)
        {
            if(arena==null||death==null||!NativeSmashGib.StageDeath(death.damageKind)||!NativeSmashGib.Applies(Gore,death.style,death.kept,death.model))return null;
            long key=((long)death.id<<32)^(uint)death.seed;
            if(!hazardGibs.TryGetValue(key,out var gib))
            {
                gib=NoGib;var hazards=arena.Map?.hazards;int round=NativeStructureFx.RoundIndex(arena.Match.Set,arena.Match.Round);
                var smash=NativeSmashGib.Plan(Gore,death.style,death.kept,death.model,death.damageKind,arena.MapId,hazards,death.hazardId,round,arena.Match.FightAt,death.at,SmashN(death.position),SmashN(death.direction),death.yaw,death.seed);
                int saw=smash==null&&death.damageKind=="hazard"&&NativeFactoryHazards.Active(arena.MapId)&&hazards!=null&&hazards.Length>0?NativeSmashGib.Saw(hazards,death.hazardId,round,arena.Match.FightAt,death.at,NativeArena.Vec(death.position)):-1;
                if(smash!=null||saw>=0)gib=new HazardGib{Smash=smash,Saw=saw};
                if(hazardGibs.Count>=64)hazardGibs.Clear();hazardGibs[key]=gib;
            }
            return ReferenceEquals(gib,NoGib)?null:gib;
        }
        /// <summary>Whether a death shows a hazard gib (the victim's cinematic screens leave it to the gib).</summary>
        internal bool HazardGibbed(NativeArena arena,NativeDeath death)=>HazardGibOf(arena,death)!=null;
        /// <summary>A pinned smash's squash about the floor at `age` (identity when not pinned): the whole body, its hat, weapon,
        /// bands and caps flattened under the crusher and spread across the floor.</summary>
        private static Matrix4x4 SmashOverlay(SmashPlan p,float age)
        {
            if(!NativeSmashGib.Overlay(p,age,out var pivot,out float spread,out float height,out float lift))return Matrix4x4.identity;
            var c=SmashU(pivot);return Matrix4x4.Translate(c+Vector3.up*lift)*Matrix4x4.Scale(new Vector3(spread,height,spread))*Matrix4x4.Translate(-c);
        }
        /// <summary>The smash's blood as its moments pass: the gushes (NativeSmashGib.Gush, each a kill spray, skipped when first
        /// seen long after) and the splats it lays on the floor (NativeSmashGib.Pool, cast down onto the surface; once each for
        /// the round, however often the body is rebuilt).</summary>
        private void SmashBlood(NativeCorpse corpse,NativeDeath death,float now)
        {
            var p=corpse?.Smash;if(p==null)return;float age=now-death.at;
            while(corpse.SmashFired<NativeSmashGib.Gushes)
            {
                int k=corpse.SmashFired;if(!NativeSmashGib.Gush(p,k,out float at,out var origin,out var normal,out var along,out var carry,out string kind,out float severity)){corpse.SmashFired++;continue;}
                if(age<at)break;corpse.SmashFired++;if(age-at>.6f)continue;
                long key=((long)(death.id&0xffff)<<40)^((long)(death.seed&0xffffff)<<12)^(k+1)^(1L<<59);bool stain=stainedWounds.Add(key);
                blood.Spray(kind,severity,unchecked((uint)death.seed*2246822519u^(uint)(k+1)*0x9e3779b1u),key,origin,normal,along,death.at+at,BloodQuality,LowGraphics,stain,0,death.id,true,carry:carry);
            }
            int pools=NativeSmashGib.PoolCount(BloodQuality,LowGraphics);
            while(corpse.SmashPooled<pools&&bloodArena!=null)
            {
                int k=corpse.SmashPooled;if(!NativeSmashGib.Pool(p,k,out float at,out var point,out var along,out float length,out float width)){corpse.SmashPooled++;continue;}
                if(age<at)break;corpse.SmashPooled++;
                if(!BloodCast(new NVector3(point.X,point.Y+.35f,point.Z),new NVector3(point.X,point.Y-.35f,point.Z),out var hit,out var up,out int surface)||surface==0)continue;
                long key=((long)(death.id&0xffff)<<40)^((long)(death.seed&0xffffff)<<12)^(k+1)^(1L<<58);
                blood.Splats.Add(key,hit+up*NativeBloodRules.Lift,up,along,length,width,unchecked((uint)death.seed*31u+(uint)k*7919u),surface,now);
            }
        }
        /// <summary>The smashes' blood mist and the train's impact flash this frame (the shared effects renderer).</summary>
        private void SmashMist(float now)
        {
            if(vfx==null)return;float q=Mathf.Clamp01(EffectsQuality)*(LowGraphics?.5f:1);if(q<=.01f)return;var mist=NativeVfxDraw.Linear(0x5c0a12);
            foreach(var corpse in corpses.Values)
            {
                var p=corpse.Smash;if(p==null)continue;float age=now-corpse.Death.at;if(!(age>=0)||age>2.5f)continue;
                for(int k=0;k<NativeSmashGib.Mists;k++)
                {
                    if(q<.5f&&(k&1)==1)continue;
                    if(NativeSmashGib.Mist(p,k,age,out var at,out float size,out float opacity,out float u))vfx.Smoke(SmashU(at),mist,opacity,size,u,(corpse.Death.seed&0xffff)*16+k,p.Floor-.05f);
                }
                if(NativeSmashGib.Flash(p,age,out var flash,out float extent,out float level))
                {
                    vfx.Glow(SmashU(flash),new Color(4.2f,2.4f,1.9f,0)*(level*q),extent,NativeVfxDraw.CoreGlow);
                    vfx.Glow(SmashU(flash),new Color(2.6f,.22f,.16f,0)*(level*q),extent*1.7f,NativeVfxDraw.Ring,1-level);
                }
            }
        }
    }
}

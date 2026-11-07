using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Arrow gibbing (owner 2026-10-14: "Add gibbing from arrows (Can pierce through enemies if player has pierce modifiers, can
    /// go in body/head and stop halfway) Blood coming out of arrow gibbing points"), presentation only (Core NativeArrowGib):
    /// <list type="bullet">
    /// <item>A bow hit's arrow lodges or passes by the hit's own replicated flag (ArenaEvent.Pierced with its Projectile id;
    /// an older frame's hit falls back to the live arrows' paths and their pierce chips, lodging in doubt).</item>
    /// <item>A lodged arrow buries about halfway into the struck part (a head shot halfway into the head), riding the joint
    /// (the live rig, then the corpse, the half of a split body it sat on); its entry drips a short, decaying trickle.</item>
    /// <item>A pass-through leaves no arrow: its entry and exit bleed (the exit sprays along the flight, then both drip) and the
    /// flying arrow goes on.</item>
    /// </list>
    /// The wound's own blood is the existing wound path's (the bow's puncture or cavity wound, LivingWounds); this adds only the
    /// arrow points' trickle and a pass-through's exit spray, under the per-target blood budget and at most
    /// NativeArrowGib.BleedCap points at once. Gore off: no arrow blood (arrows still lodge or pass). No hit, damage, hitbox,
    /// timing or network rule reads any of it.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>A pass-through's exit hole's record id (its entry keeps the hit event's).</summary>
        private const int ArrowExitHole=0x40000000;
        private ArrowShot[] arrowShots=new ArrowShot[64];
        private readonly Dictionary<int,int> arrowPasses=new Dictionary<int,int>();
        private readonly Dictionary<long,NativeShotModules> arrowModules=new Dictionary<long,NativeShotModules>();
        private void ArrowClear(){arrowPasses.Clear();}
        /// <summary>The live bow arrows for an older frame's hit (no projectile id): each with the pierces its chips allow less
        /// the ones this presentation saw it spend.</summary>
        private int ArrowShots(NativeArena arena)
        {
            int n=0;
            foreach(var b in arena.Bullets)
            {
                if(n>=arrowShots.Length)break;if(!NativeBowWire.Is(b.Spec))continue;int pierces=(int)Math.Round(NativeShotViews.Modules(arena,b.Mods,arrowModules).Pierces);arrowPasses.TryGetValue(b.Id,out int spent);
                arrowShots[n++]=new ArrowShot{Id=b.Id,Owner=b.Owner,Pierces=Math.Max(0,pierces-spent),At=arena.Time,Gravity=b.Gravity,Position=BowN(b.Position),Velocity=BowN(b.Velocity)};
            }
            return n;
        }
        /// <summary>A bow arrow striking pilot `e.Target` at `at` along `dir` (world), its joint `bone` of the posed `rig`: lodged
        /// halfway into the part, or (passed through) its entry and exit holes and the exit's spray.</summary>
        private void ArrowStrike(NativeArena arena,in ArenaEvent e,CapturedAnimation.Player rig,int bone,Vector3 at,Vector3 dir,float draw,bool std)
        {
            var joint=rig.Bones[bone].localToWorldMatrix;var inv=rig.Bones[bone].worldToLocalMatrix;var local=BowN(inv.MultiplyPoint3x4(at));var along=inv.MultiplyVector(dir);
            float scale=along.sqrMagnitude>1e-8f?1/along.magnitude:1;var localDir=CapturedRig.Unit(BowN(along),NVector.UnitZ);
            int count=e.Projectile>0?0:ArrowShots(arena);
            var fate=NativeArrowGib.Fate(e.Projectile,e.Pierced,e.Source,BowN(at),e.Time,arrowShots,count,out int shot);
            if(fate==ArrowFate.Pass&&e.Projectile<=0&&shot>0){arrowPasses.TryGetValue(shot,out int spent);if(arrowPasses.Count>256)arrowPasses.Clear();arrowPasses[shot]=spent+1;}
            var victim=PilotOf(arena,e.Target);bool lethal=victim!=null&&victim.Hp<=0&&victim.Death!=null&&Mathf.Abs(victim.Death.at-e.Time)<.02f;
            bool gore=Gore&&!(FirstPerson&&e.Target==LocalId);
            if(fate==ArrowFate.Pass)
            {
                NativeArrowGib.Through(bone,local,localDir,out var entry,out var exit);
                bowStuck.AddHole(e.Id,e.Target,bone,entry,localDir,e.Time,NativeArrowGib.Strength(gore,draw,lethal,false),LowGraphics);
                bowStuck.AddHole(e.Id^ArrowExitHole,e.Target,bone,exit,localDir,e.Time,NativeArrowGib.Strength(gore,draw,lethal,true),LowGraphics);
                if(!gore)return;
                // The exit sprays along the flight (the wound's own spray leaves its entry, LivingWounds).
                NativeArrowGib.Exit(draw,lethal,out float severity,out int extra);var from=joint.MultiplyPoint3x4(BowU(exit));
                long key=((long)(e.Target&0xffff)<<40)^((long)(e.Id&0xffffff)<<12)^(1L<<57);bool stain=stainedWounds.Add(key);
                blood.Spray("cavity",severity,unchecked((uint)e.Id*2654435761u^0xa7f0u),key,BowN(from),BowN(dir),BowN(dir),e.Time,BloodQuality,LowGraphics,stain,extra,e.Target,lethal);
                return;
            }
            NativeArrowGib.Lodge(bone,local,localDir,unchecked((uint)e.Id*2246822519u^0xa770u),out var point,out float depth);
            bowStuck.AddPilot(e.Id,e.Target,bone,point,localDir,e.Time,draw,std,LowGraphics,depth*scale,NativeArrowGib.Strength(gore,draw,lethal,false));
        }
        /// <summary>The arrow points' trickle this frame: each bleeding point (the newest NativeArrowGib.BleedCap) drips the drops
        /// its schedule has reached, from where it rides now, out of the wound along the shaft and falling.</summary>
        private void ArrowBleeds(float time)
        {
            if(bowBone==null)return;int cap=NativeArrowGib.BleedCap(BloodQuality,LowGraphics);
            for(int i=0;i<bowStuck.Count;i++)
            {
                var r=bowStuck[i];if(r.Kind!=NativeBowStuck.Kind.Pilot||!(r.Blood>0))continue;
                int due=NativeArrowGib.Drips(time-r.Born,r.Blood,BloodQuality,LowGraphics);if(due<=r.Bled)continue;
                if(!Gore||FirstPerson&&r.Target==LocalId||!NativeArrowGib.Bleeds(bowStuck,i,time,cap)||!bowBone(r.Target,r.Bone,r.Point,out NMatrix m)){bowStuck.Bleed(i,due);continue;}
                var at=NVector.Transform(r.Point,m);var dir=CapturedRig.Unit(NVector.TransformNormal(r.Dir,m),NVector.UnitZ);var outward=r.Hole&&(r.Event&ArrowExitHole)!=0?dir:-dir;
                for(int k=r.Bled;k<due&&k<r.Bled+2;k++)
                {
                    long key=((long)(r.Target&0xffff)<<40)^((long)(r.Event&0x7fffffff)<<8)^(k+1)^(1L<<56);bool stain=stainedWounds.Add(key);
                    blood.Spray("drip",1,unchecked((uint)r.Event*2654435761u^(uint)(k+1)*0x9e3779b1u),key,at+outward*.012f,outward,-NVector.UnitY,time,BloodQuality,LowGraphics,stain);
                }
                bowStuck.Bleed(i,due);
            }
        }
        /// <summary>The half of a split corpse an arrow rides: the half its joint went to, or (a joint the cut crossed) the side
        /// its point was on as the body died.</summary>
        private static NativeCorpse ArrowHalf(NativeCorpse corpse,int bone,NVector local)
        {
            if(corpse.Twin==null||corpse.Request==null)return corpse;var p=corpse.Bind[bone].MultiplyPoint3x4(BowU(local));
            return NativeArrowGib.Half(corpse.Request,bone,BowN(p))==1?corpse.Twin:corpse;
        }
    }
}

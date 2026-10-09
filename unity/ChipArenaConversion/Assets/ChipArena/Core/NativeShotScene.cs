using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pure port of the source's per-frame projectile visuals (client/renderer.ts shots loop and abilityShot, and
    /// client/chip-effects.ts ChipEffects.update): one model per projectile with its colour, orientation, spin and fuse
    /// blink, its chip module parts, and the spark and beam emitters, drawn into a reused list of parts plus the shared
    /// NativeBurstPool. Halos, spill lights and trails stay in NativeCombatGlow and read NativeShotRules.
    /// Sizes freeze at the radius first seen for an id, as the source builds each mesh once; the rest of a shot's look is
    /// frozen upstream (NativeShotTracker). Cosmetic only.
    /// </summary>
    public sealed class NativeShotScene
    {
        private sealed class State
        {
            public int Frame;public double Radius,QX,QY,QZ,QW=1,NextPuff;public readonly List<NativeShotRules.Part> Parts=new List<NativeShotRules.Part>();
            public void Reset(){Radius=0;QX=QY=QZ=0;QW=1;NextPuff=0;Parts.Clear();}
        }
        public readonly List<NativeShotDraw> Draws=new List<NativeShotDraw>();
        public readonly NativeBurstPool Bursts;
        private readonly Dictionary<long,State> states=new Dictionary<long,State>();private readonly Stack<State> spare=new Stack<State>();private readonly List<long> stale=new List<long>();
        private Func<double> decide;private int frame;private double emitClock,time,simTime,quality;private bool emit;
        public int Live=>states.Count;
        /// <summary>decide: the emitter coin flips (fire spark colour, water and darkness chances); sparks: the spark spawn values.</summary>
        public NativeShotScene(Func<double> decide=null,Func<double> sparks=null){this.decide=decide??new NativeShotRandom(0x5a17).Next;Bursts=new NativeBurstPool(sparks);}
        public void Reseed(Func<double> decide,Func<double> sparks){this.decide=decide??new NativeShotRandom(0x5a17).Next;Bursts.Reseed(sparks);}
        public static long Key(NativeShotKind kind,int id)=>((long)kind<<32)|(uint)id;
        /// <summary>True once an id has been drawn: its module inputs are no longer read.</summary>
        public bool Knows(NativeShotKind kind,int id)=>states.ContainsKey(Key(kind,id));
        public void Clear(){foreach(var s in states.Values){s.Reset();spare.Push(s);}states.Clear();Draws.Clear();Bursts.Clear();emitClock=0;}
        /// <summary>
        /// Starts a frame. time is the presentation clock (spin, blink, element cycling, puffs, module animation),
        /// simTime the source's state.time (darkness pulse and tail wobble: the match clock on the host, the interpolated
        /// snapshot time on a replica), dt the frame step, quality the effects quality.
        /// The source emits its per-frame sparks and beams once per rendered frame; here once per 1/60 s of frame time
        /// at most once a frame, which equals the source at 60 Hz and below and keeps the density on faster displays.
        /// </summary>
        public void Begin(double time,double simTime,double dt,double quality)
        {
            frame++;Draws.Clear();this.time=time;this.simTime=simTime;this.quality=quality;emit=NativeShotRules.Emit(ref emitClock,dt);
        }
        private State Get(in NativeShotView v,out bool fresh)
        {
            long key=Key(v.Kind,v.Id);fresh=!states.TryGetValue(key,out var s);
            if(fresh){s=spare.Count>0?spare.Pop():new State();s.Reset();s.Radius=v.Radius;bool fire=v.Fire,poison=v.Poison;NativeShotRules.Modules(v.Modules,fire,poison,s.Parts);states.Add(key,s);}
            s.Frame=frame;return s;
        }
        private void Add(NativeShotShape shape,in NativeShotAffine m,int hex,bool lerpWhite,float alpha=1)
        {NativeShotRules.Linear(hex,lerpWhite,out float r,out float g,out float b);Draws.Add(new NativeShotDraw{Shape=shape,M=m,R=r,G=g,B=b,A=alpha});}
        private void Burst(double x,double y,double z,int hex,int count,double power)=>Bursts.Spawn(x,y,z,hex,count,power,quality);
        /// <summary>One live projectile. Siren shots are skipped: the source draws them with siren/fx.ts (C12), not here.</summary>
        public void Shot(in NativeShotView v)
        {
            if(v.Siren)return;var s=Get(v,out _);
            if(v.Kind==NativeShotKind.Bullet)Primary(v,s);else if(v.Kind==NativeShotKind.Ability)Ability(v,s);
            Modules(v,s);
        }
        private void Primary(in NativeShotView v,State s)
        {
            var shape=NativeShotRules.PrimaryShape(v);double r=s.Radius,k=shape==NativeShotShape.Box?1:r,sx=1,sy=1,sz=1;
            if(v.Class=="ranged"){sx=sy=Math.Max(.075,r*2);sz=v.Bowling?r*2:.65;}
            if(v.SwordBeam){sx=1;sy=.08;sz=.4;}
            int n=v.Elements;
            if(n>0&&time>s.NextPuff)
            {
                s.NextPuff=time+NativeShotRules.PuffSeconds;double px=v.X-v.VX*.012,py=v.Y-v.VY*.012,pz=v.Z-v.VZ*.012;
                if(v.Fire)Burst(px,py,pz,NativeShotRules.Fire,3,1.5);if(v.Poison)Burst(px,py,pz,NativeShotRules.Poison,3,.65);
            }
            NativeShotMath.Flight(v.VX,v.VY,v.VZ,out double qx,out double qy,out double qz,out double qw);
            if(v.Spec=="fire"){NativeShotMath.RotateX(ref qx,ref qy,ref qz,ref qw,Math.PI/2);if(emit)Burst(v.X,v.Y,v.Z,decide()<.5?0xffbb45:0xff6839,2,1.5);}
            else if(v.Spec=="water"){sx=1;sy=1;sz=1.7;if(emit&&decide()<.3)Burst(v.X,v.Y,v.Z,0xa8eeef,1,.6);}
            else if(v.Spec=="darkness")
            {
                sx=sy=sz=1+Math.Sin(simTime*12)*.13;
                if(emit&&decide()<.35)Bursts.Beam(v.X,v.Y,v.Z,v.X-v.VX*.06+Math.Sin(simTime*24)*.2,v.Y-v.VY*.06+.1,v.Z-v.VZ*.06,0x6c458e,quality);
            }
            if(v.Spec=="lightning"&&emit)Bursts.Beam(v.X,v.Y,v.Z,v.X-v.VX*.025,v.Y-v.VY*.025,v.Z-v.VZ*.025,v.WeaponColor,quality);
            Add(shape,NativeShotAffine.Compose(v.X,v.Y,v.Z,qx,qy,qz,qw,sx*k,sy*k,sz*k),NativeShotRules.MeshColor(v,time),true);
        }
        private void Ability(in NativeShotView v,State s)
        {
            var shape=NativeShotRules.AbilityShape(v.Ability,out int color);string id=v.Ability;
            // The mesh keeps its last rotation while the shot is (nearly) still: a resting frag grenade keeps tumbling.
            if(Math.Sqrt(v.VX*v.VX+v.VY*v.VY+v.VZ*v.VZ)>.05)NativeShotMath.Flight(v.VX,v.VY,v.VZ,out s.QX,out s.QY,out s.QZ,out s.QW);
            bool frag=id=="frag-grenade";
            if(frag||id=="ball-lightning"){NativeShotMath.RotateX(ref s.QX,ref s.QY,ref s.QZ,ref s.QW,time*(frag?11:4));NativeShotMath.RotateY(ref s.QX,ref s.QY,ref s.QZ,ref s.QW,time*3);}
            if((id=="rocket"||id=="shadow-shackles")&&time>s.NextPuff){s.NextPuff=time+NativeShotRules.PuffSeconds;Burst(v.X,v.Y,v.Z,id=="rocket"?NativeShotRules.RocketSmoke:NativeShotRules.ShackleSmoke,2,.5);}
            Add(shape,NativeShotAffine.Compose(v.X,v.Y,v.Z,s.QX,s.QY,s.QZ,s.QW,1,1,1),frag&&NativeShotRules.Blinks(v.FuseLeft,time)?NativeShotRules.Blink:color,false,id=="tidal-wave"?.5f:1);
        }
        // ChipEffects.update: the group follows the shot, turned +Z to the velocity every frame.
        private void Modules(in NativeShotView v,State s)
        {
            if(s.Parts.Count==0)return;NativeShotMath.Flight(v.VX,v.VY,v.VZ,out double gx,out double gy,out double gz,out double gw);
            for(int i=0;i<s.Parts.Count;i++)
            {
                var p=NativeShotRules.Animate(s.Parts[i],time);NativeShotMath.Euler(p.EX,p.EY,p.EZ,out double qx,out double qy,out double qz,out double qw);
                Add(p.Shape,NativeShotAffine.Grouped(v.X,v.Y,v.Z,gx,gy,gz,gw,p.X,p.Y,p.Z,qx,qy,qz,qw,p.SX,p.SY,p.SZ),p.Color,false);
            }
        }
        /// <summary>Ends a frame: forgets ids not seen this frame and steps the sparks and beams by dt (after this frame's spawns).</summary>
        public void End(double dt)
        {
            stale.Clear();foreach(var pair in states)if(pair.Value.Frame!=frame)stale.Add(pair.Key);
            foreach(long key in stale){var s=states[key];states.Remove(key);s.Reset();spare.Push(s);}
            Bursts.Update(Math.Max(0,dt));
        }
    }
}

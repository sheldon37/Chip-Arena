using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// How an air jump reads for each weapon and each charge. The movement chips (Air Step, Acrobat, Skybound) and the Airborne
    /// modifier give every weapon up to NativeJumpSimulation.MaxAirJumps air jumps, refilled on landing (Astra, PR #282); the
    /// presented signal is the one the dagger flip already used (NativeArena.PresentedJump: Actor.AirJumps / AirJumpAt, the
    /// guest's prediction latching one cue per spent charge), watched by a NativeJumpTrack per pilot.
    ///  - Flip: the daggers and the Bow keep their flip, burst and whoosh exactly (every charge the same).
    ///  - Own: the Cyber Lord's hardlight boost has its own effects (Core/NativeCyberLordAirBoostFx); no burst here.
    ///  - Kick: every other weapon steps off the air (NativeJumpMotion's air step: a knee drive and a kick-back, the weapon held
    ///    as the route holds it) with the same burst, a little smaller, brighter and whiter for each chained charge so a chain
    ///    reads as separate pops, and the jump sound a semitone higher per charge.
    /// Presentation only: nothing here is read by the authority, prediction or the wire.
    /// </summary>
    public static class NativeAirJumpLook
    {
        public const int Own=0,Flip=1,Kick=2;
        public static int KindOf(string spec)=>CyberLordAirBoost.Handles(spec)?Own:NativeJumpRules.Flips(spec)?Flip:Kick;
        /// <summary>Whether `spec`'s air jump draws the burst (all but the Cyber Lord) and steps off the air (not flips, not the boost).</summary>
        public static bool Bursts(string spec)=>KindOf(spec)!=Own;
        public static bool Kicks(string spec)=>KindOf(spec)==Kick;
        /// <summary>The charge (1 = the first air jump since the takeoff), clamped to the simulation's budget.</summary>
        public static int Charge(int airJumps)=>Math.Max(1,Math.Min(NativeJumpSimulation.MaxAirJumps,airJumps));
        /// <summary>Per chained charge of a Kick weapon: the burst's size, its HDR gain and its mix toward white (the first charge, and every
        /// Flip charge, is the dagger's burst exactly: 1, 1, 0).</summary>
        public const float ScaleStep=.06f,GainStep=.08f,WhiteStep=.06f;
        public static float Scale(int kind,int charge)=>kind==Kick?1-ScaleStep*(Charge(charge)-1):1;
        public static float Gain(int kind,int charge)=>kind==Kick?1+GainStep*(Charge(charge)-1):1;
        public static float White(int kind,int charge)=>kind==Kick?WhiteStep*(Charge(charge)-1):0;
        /// <summary>The Kick air jump's sound: the jump's own bank key, softer than the takeoff (.4/.5), a little higher and a semitone up
        /// per charge (the dagger's flip whoosh and the Cyber Lord's boost keep theirs).</summary>
        public const string KickSound="move.jump";
        public const float KickRate=1.1f,KickGain=.38f,KickOwnGain=.3f;
        public static float KickPitch(int charge)=>KickRate*(float)Math.Pow(2,(Charge(charge)-1)/12.0);

        // ---- the air step (NativeJumpMotion, Kick weapons): a lead knee drives up to the set's tuck, the trailing leg kicks down and back
        // from the push-off, a small forward crunch of the spine; the lead leg alternates with the charge (odd: right). Joint rotations
        // only, over the jump layer's own air pose: no root motion, hitbox, attack lock, timing or launch change. ----
        /// <summary>Seconds: the whole step, its blend in and the end of its hold (it blends back into the jump layer's air pose after).</summary>
        public const float KickSeconds=.34f,KickIn=.07f,KickHold=.12f;
        /// <summary>The lead thigh's extra lift past the set's tuck, its least and most lift and its least knee bend; the trailing thigh's
        /// kick-back and its knee (radians); the spine's forward crunch at full weight.</summary>
        public const float LeadLift=.2f,MinLift=1.3f,MaxLift=2.1f,LeadKnee=2f,KickBack=.35f,TrailKnee=.35f,Crunch=.1f;
        public static float KickWeight(float age)
        {
            if(!(age>=0&&age<KickSeconds))return 0;
            return MeleeMath.Ease(age/KickIn)*(1-MeleeMath.Ease((age-KickHold)/(KickSeconds-KickHold)));
        }
        public static bool LeadRight(int charge)=>(Charge(charge)&1)==1;
    }

    /// <summary>Where the air-jump burst draws (Runtime: NativeVfxDraw through NativeJumpFxPresentation; the checks: a counter).</summary>
    public interface IAirJumpFxSink
    {
        /// <summary>A camera-facing ring glow (NativeVfxDraw.Glow, Ring profile); `color` HDR, linear.</summary>
        void Ring(Vector3 at,Vector3 color,float size,float u,float seed);
        void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed);
        /// <summary>A soft puff (NativeVfxDraw.SoftSmoke) and its mote glow (NativeVfxDraw.Glow, Mote profile).</summary>
        void Puff(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor);
        void Mote(Vector3 at,Vector3 color,float size,float u,float seed);
    }

    /// <summary>
    /// The live air-jump bursts of the presented pilots (Core/NativeJumpFx shapes, NativeAirJumpLook per weapon and charge): a
    /// NativeJumpTrack per pilot on its drawn feet with the explicit air-jump signal, one burst per spent charge, at most Capacity
    /// live. The view pilot's afterimage puffs are skipped in first person; Reduced effects and Low graphics thin the counts.
    /// No allocation per frame once each pilot has been seen.
    /// </summary>
    public sealed class NativeAirJumpBursts
    {
        public const int Capacity=16;
        public struct Burst{public float At,Floor,Scale;public Vector3 Feet,Accent;public int Seed,Owner,Charge,Kind;}
        private readonly Burst[] bursts=new Burst[Capacity];private int count,seeds;
        private readonly Dictionary<int,NativeJumpTrack> tracks=new Dictionary<int,NativeJumpTrack>();
        public int Live=>count;
        public Burst Get(int i)=>bursts[i];
        /// <summary>Bursts started so far (the checks count them per charge).</summary>
        public int Started{get;private set;}
        public NativeJumpTrack Track(int id)=>tracks.TryGetValue(id,out var t)?t:null;
        public void Clear(){count=0;tracks.Clear();}
        /// <summary>
        /// One presented pilot this frame: `feet` its presented root less the hips height (Unity world), `blocked` an overlay owning it
        /// (stun, juggle, knockdown, roll, special), `airJumpAt` the presented air-jump signal (-100 none), `accent` its accent colour
        /// (read only when a burst starts). Returns the charge (1..6) of an air jump that started a burst this frame, else 0.
        /// </summary>
        public int Observe(int id,string spec,float now,bool grounded,Vector3 feet,bool blocked,float airJumpAt,bool alive,string accent)
        {
            if(!tracks.TryGetValue(id,out var t))tracks[id]=t=new NativeJumpTrack();
            int jumps=t.AirJumps;t.Explicit=true;t.Observe(now,grounded,feet.Y,blocked);t.AirJumpSignal(airJumpAt);
            int kind=NativeAirJumpLook.KindOf(spec);
            if(!(t.AirJumps>jumps&&alive&&kind!=NativeAirJumpLook.Own))return 0;
            int charge=NativeAirJumpLook.Charge(t.AirJumps);var tint=NativeGlowRules.ArmorTint(accent);float white=NativeAirJumpLook.White(kind,charge),gain=NativeAirJumpLook.Gain(kind,charge);
            if(white>0||gain!=1)tint=(tint+(Vector3.One-tint)*white)*gain;
            if(count>=Capacity){Array.Copy(bursts,1,bursts,0,Capacity-1);count--;}
            bursts[count++]=new Burst{At=now,Feet=feet,Floor=t.Floor,Seed=++seeds,Owner=id,Charge=charge,Kind=kind,Scale=NativeAirJumpLook.Scale(kind,charge),Accent=tint};
            Started++;return charge;
        }
        /// <summary>Drops the finished bursts (and any from a later time: a rewind) and draws the rest at `now`.</summary>
        public void Draw(IAirJumpFxSink s,float now,float quality,bool low,int viewId,bool firstPerson)
        {
            for(int i=count-1;i>=0;i--)if(now-bursts[i].At>=NativeJumpFx.Life||now<bursts[i].At-.25f){Array.Copy(bursts,i+1,bursts,i,count-i-1);count--;}
            if(s==null)return;
            int streaks=NativeJumpFx.Count(NativeJumpFx.Streaks,NativeJumpFx.ReducedStreaks,quality,low),puffs=NativeJumpFx.Count(NativeJumpFx.Puffs,NativeJumpFx.ReducedPuffs,quality,low);
            for(int i=0;i<count;i++)
            {
                var b=bursts[i];float age=now-b.At;var centre=NativeJumpFx.Centre(b.Feet,b.Floor);
                if(NativeJumpFx.Ring(age,out float size,out float alpha,b.Scale))s.Ring(centre,b.Accent*(NativeJumpFx.RingGain*alpha),size,age/NativeJumpFx.RingLife,b.Seed);
                for(int k=0;k<streaks;k++)
                    if(NativeJumpFx.Streak(k,streaks,b.Seed,age,centre,b.Floor,out var head,out var dir,out float length,out float fade,b.Scale))
                        s.Streak(head,dir*length,b.Accent*(NativeJumpFx.StreakGain*fade),NativeJumpFx.StreakWidth,length,1,.8f,age/NativeJumpFx.StreakLife,b.Seed*31+k);
                if(firstPerson&&b.Owner==viewId)continue;
                for(int k=0;k<puffs;k++)
                    if(NativeJumpFx.Puff(k,puffs,b.Seed,age,b.Feet,out var at,out float ps,out float opacity,b.Scale))
                    {
                        float life=age/NativeJumpFx.PuffLife;
                        s.Puff(at,b.Accent,opacity,ps,life,b.Seed*17+k,b.Floor);s.Mote(at,b.Accent*(NativeJumpFx.PuffGain*opacity),ps*.8f,life,b.Seed*13+k);
                    }
            }
        }
    }
}

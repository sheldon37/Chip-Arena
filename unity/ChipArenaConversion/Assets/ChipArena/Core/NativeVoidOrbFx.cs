using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.NativeMagicMath;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The darkness primary (GameContent darkness: 34-damage heavy orbs, speed 26, gravity 3, range 36, interval .66, shot radius
    /// .2, drain .2 of direct damage; content.ts castWindup .22 s) drawn as void magic instead of the source's pale icosahedron,
    /// entirely from what every peer holds:
    ///  - in flight (the replicated bullet's presented position and velocity, its spawn time): a black-hole core (an opaque black
    ///    orb with a violet-magenta event-horizon rim), a gravitational-lens fake (a dark ring and a light ring round it), an
    ///    accretion swirl, orbiting shadow wisps, ink tendrils trailing and swaying, motes spiralling inward, and a smoky tail of
    ///    dark puffs with violet embers along the path it flew, sinking a little (its weight) along the gravity-3 arc. The core's
    ///    pulse and the tendrils' wobble keep NativeShotScene's darkness clock (state.time: the match clock on the host, the
    ///    interpolated snapshot clock on a replica);
    ///  - the cast (the caster's replicated AttackAt and CastRate): ink and violet motes swirling into the right-hand socket over
    ///    the windup with a dark ring closing on a forming black core, then a release pulse (in the caster's own first-person
    ///    view on the view-model layer);
    ///  - impacts: an implosion then an outward dark burst (smoke, embers, ink, a magenta ring and a dark shock ring) where the
    ///    replicated direct Hit lands, or where the shot left the replicated list against a surface (then a void scorch with
    ///    glowing cracks on it), or a smaller fizzle at range end in the air;
    ///  - the lifesteal: on a replicated direct Hit by a darkness caster, a dark-red to violet stream from the victim back to
    ///    the caster, and when the paired replicated Heal event (the 20% drain) exists, a heal glint on the caster.
    /// Particles are pure functions of time slots, seeds and the recorded path, so host, guests and the killcam draw the same
    /// frame. Effects quality (Reduced .3) and the low graphics tier thin them. Cosmetic only.
    /// </summary>
    public sealed class NativeVoidOrbFx
    {
        // ---- the simulation's numbers (GameContent darkness; NativeSpellCharge.CastWindup) ----
        public const float Speed=26,ShotRadius=.2f,Drain=.2f,CastSeconds=.22f;
        // ---- the look (sRGB hex; linear on use) ----
        public const int RimHex=0xc04dff,LensHex=0xd9a6ff,SwirlHex=0xff4fd8,VioletHex=0x9a5cff,WispHex=0x1a0e2a,MoteHex=0xf0b0ff,SmokeHex=0x2a1440,EmberHex=0xc65cff,BloodHex=0xff2a5a,HealHex=0xff6fa8;
        public const int MaxOrbs=24,MaxSamples=48,MaxImpacts=24,MaxDrains=16,MaxHeals=16;
        public const float PathStep=.28f,TailLifeMin=.28f,TailLifeMax=.42f,ImpactSeconds=.85f,ScorchSeconds=2.6f,DrainSeconds=1.3f,GlintSeconds=.55f;
        /// <summary>An orb this presentation remembers: live (replicated this frame) or its tail fading after it ended.</summary>
        public sealed class Orb
        {
            public long Key;public int Id,Owner;public bool Used,Live,Seen,Own;public float Born,FirstSeen,LastSeen,GoneAt,Radius;public Vector3 Last,Velocity;
            public int Samples;public readonly Vector3[] Path=new Vector3[MaxSamples];public readonly float[] PathTime=new float[MaxSamples],PathDistance=new float[MaxSamples];
            public float Travel=>Samples>0?PathDistance[Samples-1]:0;
        }
        public struct Impact{public int Id,Source;public float Time;public Vector3 At,Normal;public bool Surface,Direct,Air;}
        public struct DrainRecord{public int Id,Source,Target;public float Time,Heal;public Vector3 From;public bool Healed;}
        private struct HealRecord{public int Id,Source;public float Time,Amount;}
        private readonly Orb[] orbs=new Orb[MaxOrbs];private readonly Impact[] impacts=new Impact[MaxImpacts];private readonly DrainRecord[] drains=new DrainRecord[MaxDrains];private readonly HealRecord[] heals=new HealRecord[MaxHeals];
        private int impactNext,drainNext,healNext,impactSerial;
        public IReadOnlyList<Orb> Orbs=>orbs;public IReadOnlyList<Impact> Impacts=>impacts;public IReadOnlyList<DrainRecord> Drains=>drains;
        /// <summary>Particles and parts queued this frame (checks).</summary>
        public int Particles;
        public NativeVoidOrbFx(){for(int i=0;i<orbs.Length;i++)orbs[i]=new Orb();}
        public void Clear()
        {
            foreach(var o in orbs){o.Used=false;o.Live=false;o.Samples=0;}
            for(int i=0;i<impacts.Length;i++)impacts[i]=default;for(int i=0;i<drains.Length;i++)drains[i]=default;for(int i=0;i<heals.Length;i++)heals[i]=default;impactNext=drainNext=healNext=0;
        }
        private static Vector3 V(V3 v)=>new Vector3(v.x,v.y,v.z);

        // ---- event intake (NativeMagicFx.Observe) ----
        /// <summary>A replicated Hit or Headshot: a darkness caster's direct hit is an impact at the victim and a drain stream.</summary>
        public void Hit(in ArenaEvent e,string sourceSpec)
        {
            if(sourceSpec!="darkness"||e.Detail!="direct"||e.Amount<=0||e.Target==0)return;
            impacts[impactNext]=new Impact{Id=++impactSerial,Source=e.Source,Time=e.Time,At=V(e.Position),Direct=true};impactNext=(impactNext+1)%impacts.Length;
            var d=new DrainRecord{Id=e.Id,Source=e.Source,Target=e.Target,Time=e.Time,From=V(e.Position)};
            // The paired Heal (NativeArena.Damage / MinionWeaponHit emit it in the same tick as the hit).
            for(int i=0;i<heals.Length;i++)if(heals[i].Id!=0&&heals[i].Source==e.Source&&Math.Abs(heals[i].Time-e.Time)<1e-3f){d.Healed=true;d.Heal=heals[i].Amount;heals[i]=default;break;}
            drains[drainNext]=d;drainNext=(drainNext+1)%drains.Length;
        }
        /// <summary>A replicated Heal on its own source (lifesteal, the drain): pairs with that pilot's direct hit of the same tick.</summary>
        public void Heal(in ArenaEvent e)
        {
            if(e.Amount<=0||e.Source==0||e.Target!=e.Source)return;
            for(int i=0;i<drains.Length;i++)if(drains[i].Id!=0&&!drains[i].Healed&&drains[i].Source==e.Source&&Math.Abs(drains[i].Time-e.Time)<1e-3f){drains[i].Healed=true;drains[i].Heal=e.Amount;return;}
            heals[healNext]=new HealRecord{Id=e.Id,Source=e.Source,Time=e.Time,Amount=e.Amount};healNext=(healNext+1)%heals.Length;
        }

        // ---- the frame ----
        private Orb Find(long key){foreach(var o in orbs)if(o.Used&&o.Key==key)return o;return null;}
        private Orb Take()
        {
            foreach(var o in orbs)if(!o.Used)return o;
            Orb oldest=null;foreach(var o in orbs)if(!o.Live&&(oldest==null||o.GoneAt<oldest.GoneAt))oldest=o;if(oldest!=null)return oldest;
            foreach(var o in orbs)if(oldest==null||o.FirstSeen<oldest.FirstSeen)oldest=o;return oldest;
        }
        private static void Sample(Orb o,Vector3 at,float time)
        {
            if(o.Samples>0&&Vector3.Distance(o.Path[o.Samples-1],at)<PathStep)return;
            if(o.Samples==MaxSamples){Array.Copy(o.Path,1,o.Path,0,MaxSamples-1);Array.Copy(o.PathTime,1,o.PathTime,0,MaxSamples-1);Array.Copy(o.PathDistance,1,o.PathDistance,0,MaxSamples-1);o.Samples--;}
            float d=o.Samples>0?o.PathDistance[o.Samples-1]+Vector3.Distance(o.Path[o.Samples-1],at):0;o.Path[o.Samples]=at;o.PathTime[o.Samples]=time;o.PathDistance[o.Samples]=d;o.Samples++;
        }
        private static bool AtDistance(Orb o,float distance,out Vector3 at,out float time)
        {
            at=default;time=0;if(o.Samples<2||distance<o.PathDistance[0]||distance>o.PathDistance[o.Samples-1])return false;int i=1;while(i<o.Samples-1&&o.PathDistance[i]<distance)i++;
            float span=o.PathDistance[i]-o.PathDistance[i-1],f=span>1e-5f?Clamp01((distance-o.PathDistance[i-1])/span):1;at=Vector3.Lerp(o.Path[i-1],o.Path[i],f);time=o.PathTime[i-1]+(o.PathTime[i]-o.PathTime[i-1])*f;return true;
        }
        private static float DistanceAt(Orb o,float time)
        {
            if(o.Samples==0||time<=o.PathTime[0])return o.Samples>0?o.PathDistance[0]:0;
            for(int i=1;i<o.Samples;i++)if(time<=o.PathTime[i]){float span=o.PathTime[i]-o.PathTime[i-1],f=span>1e-5f?(time-o.PathTime[i-1])/span:1;return o.PathDistance[i-1]+(o.PathDistance[i]-o.PathDistance[i-1])*f;}
            return o.Travel;
        }
        /// <summary>Steps the tracked orbs from the replicated darkness shots and draws every live part of the frame. simTime: the
        /// darkness pulse and wobble clock (NativeShotScene: the match clock on the host, the snapshot clock on a replica).</summary>
        public void Draw(IMagicFxSink sink,IMagicFxScene scene,NativeMagicDecals decals,float time,float simTime,float quality,bool low)
        {
            Particles=0;int viewer=scene.Viewer;
            foreach(var o in orbs)o.Seen=false;
            for(int i=0;i<scene.Orbs;i++)
            {
                if(!scene.Orb(i,out var s))continue;var o=Find(s.Key);
                if(o==null){o=Take();o.Used=true;o.Key=s.Key;o.Id=s.Id;o.Samples=0;o.GoneAt=-1;o.FirstSeen=time;o.Born=s.Born>0&&s.Born<=time&&time-s.Born<1?s.Born:time;}
                o.Seen=true;o.Live=true;o.Owner=s.Owner;o.Last=s.Position;o.Velocity=s.Velocity;o.Radius=s.Radius>0?s.Radius:ShotRadius;o.LastSeen=time;o.Own=viewer!=0&&s.Owner==viewer;Sample(o,s.Position,time);
            }
            foreach(var o in orbs)
            {
                if(!o.Used||!o.Live||o.Seen)continue;o.Live=false;o.GoneAt=time;
                // Gone: a direct hit near its last stretch is the hit's own impact; else a surface it reached, else range end in the air.
                bool hit=false;for(int i=0;i<impacts.Length;i++){var m=impacts[i];if(m.Id!=0&&m.Direct&&m.Source==o.Owner&&m.Time>=o.LastSeen-.2f&&Vector3.Distance(m.At,o.Last)<1.6f+o.Velocity.Length()*.08f){hit=true;break;}}
                if(hit)continue;var dir=o.Velocity.LengthSquared()>1e-6f?Vector3.Normalize(o.Velocity):Vector3.UnitZ;float reach=o.Radius+.45f+o.Velocity.Length()*Math.Min(.06f,Math.Max(.02f,time-o.LastSeen)+.02f);
                var m2=new Impact{Id=++impactSerial,Source=o.Owner,Time=time};
                if(scene.Surface(o.Last-dir*o.Radius,dir,reach+o.Radius,out var point,out var normal)){m2.At=point+normal*.04f;m2.Normal=normal;m2.Surface=true;}else{m2.At=o.Last+dir*.15f;m2.Air=true;}
                impacts[impactNext]=m2;impactNext=(impactNext+1)%impacts.Length;
            }
            foreach(var o in orbs)
            {
                if(!o.Used)continue;if(!o.Live&&time-o.GoneAt>TailLifeMax+.05f){o.Used=false;continue;}
                if(o.Live)Body(sink,scene,o,time,simTime,quality,low);Tail(sink,o,time,quality,low);
            }
            Casts(sink,scene,time,quality,low);
            for(int i=0;i<impacts.Length;i++)if(impacts[i].Id!=0)Burst(sink,decals,impacts[i],time,quality,low);
            for(int i=0;i<drains.Length;i++)if(drains[i].Id!=0)Stream(sink,scene,drains[i],time,quality,low);
        }

        // ---- the orb in flight ----
        /// <summary>The core pulse on the darkness clock (NativeShotScene: 1 + sin(12 t) .13).</summary>
        public static float Pulse(float simTime)=>1+(float)Math.Sin(simTime*12)*.13f;
        /// <summary>The tendril sway on the darkness clock (NativeShotScene's tail wobble, sin(24 t) .2, shared out along each thread).</summary>
        public static float Wobble(float simTime,int tendril,int point)=>(float)Math.Sin(simTime*24+point*.7f+tendril*2.1f)*.2f;
        private void Body(IMagicFxSink sink,IMagicFxScene scene,Orb o,float time,float simTime,float quality,bool low)
        {
            var at=o.Last;var f=o.Velocity.LengthSquared()>1e-6f?Vector3.Normalize(o.Velocity):Vector3.UnitZ;Basis(f,out var e1,out var b1);
            float spawn=.45f+.55f*Smooth((time-o.Born)/.08f),pulse=Pulse(simTime),rc=o.Radius*.95f*spawn*pulse,seed=o.Id*31;
            // The caster's own orb in first person: its lens and halo open out as it leaves the eye (the core stays).
            float near=o.Own?Smooth((Vector3.Distance(at,scene.Eye)-.6f)/1.9f):1;
            // The black core and its event horizon; the lens: a dark ring and a light ring round it; a faint violet aura.
            sink.Sphere(at,rc,new Vector3(.004f,.002f,.008f),Lin(RimHex,3.4f),Vector3.Zero);
            sink.Glow(at,Lin(RimHex,2.3f*(.8f+.2f*pulse)),0,rc*3.75f,2,0,seed);
            sink.Glow(at,Vector3.Zero,.82f*near,rc*5.8f,2,0,seed+1);
            sink.Glow(at,Lin(LensHex,(.85f+.25f*(float)Math.Sin(simTime*7))*near),0,rc*7.6f,2,0,seed+2);
            sink.Glow(at,Lin(VioletHex,.17f*near),0,rc*10f,0,0,seed+3);Particles+=5;
            // The accretion swirl: streaks orbiting in a disc tilted off the flight.
            const float tilt=.6f;var d1=e1;var d2=Vector3.Normalize(b1*(float)Math.Cos(tilt)+f*(float)Math.Sin(tilt));
            int swirl=Count(6,quality,low);
            for(int k=0;k<swirl;k++)
            {
                float th=simTime*9+k*6.2832f/swirl,rd=rc*1.55f*(1+.08f*(float)Math.Sin(simTime*7+k));var radial=d1*(float)Math.Cos(th)+d2*(float)Math.Sin(th);var tangent=-d1*(float)Math.Sin(th)+d2*(float)Math.Cos(th);
                sink.Streak(at+radial*rd,tangent*(rd*9),Lin(k%2==0?SwirlHex:VioletHex,k%2==0?2.6f:2.2f),.034f*spawn,.06f,.05f,.85f,0,seed+10+k);Particles++;
            }
            // Shadow wisps orbiting and lagging, sagging a little (the orb's weight).
            int wisps=Count(4,quality,low);
            for(int k=0;k<wisps;k++)
            {
                float ph=simTime*4.5f+k*1.2566f+.3f*(float)Math.Sin(simTime*3+k);var radial=e1*(float)Math.Cos(ph)+b1*(float)Math.Sin(ph);
                var p=at+radial*(rc*1.9f)-f*(.08f+.06f*k)-Vector3.UnitY*(.025f*k);
                sink.Smoke(p,Lin(WispHex),.9f,(.12f+.03f*(float)Math.Sin(simTime*5+k))*spawn,.18f,seed+20+k,NoFloor);Particles++;
            }
            // Ink tendrils trailing behind, swaying on the darkness clock and hanging lower toward their tips.
            int threads=low?2:3,points=quality<.5f?4:6;
            for(int k=0;k<threads;k++)
            {
                float alpha=k*2.094f+simTime*2.2f;var off=e1*(float)Math.Cos(alpha)+b1*(float)Math.Sin(alpha);Vector3 prev=default;
                for(int j=0;j<=points;j++)
                {
                    float d=rc*.8f+.085f*j;var p=at-f*d+off*(rc*.5f+.022f*j)+e1*(Wobble(simTime,k,j)*.18f*j/points)-Vector3.UnitY*(.012f*j*j);
                    if(j>0){sink.Drop(prev,prev-p,new Vector3(.02f,0,.035f),.85f*(1-.12f*j),.05f*(1-j/(points+2f))*spawn,0,seed+30+k*8+j);Particles++;}
                    prev=p;
                }
            }
            // Motes spiralling inward from about three core radii, brightening as they fall in.
            int motes=Count(10,quality,low);
            for(int k=0;k<motes;k++)
            {
                float ph=simTime*1.7f+k*.1f+H(k,(uint)o.Id+5);ph-=(float)Math.Floor(ph);float r=rc*(1+2.6f*(float)Math.Pow(1-ph,1.5));float ang=k*2.4f+ph*4.5f;
                float roll=k*.62f;var a1=e1*(float)Math.Cos(roll)+b1*(float)Math.Sin(roll);var a2=Vector3.Cross(f,a1);
                var p=at+(a1*(float)Math.Cos(ang)+a2*(float)Math.Sin(ang))*r+f*((H(k,(uint)o.Id+6)-.5f)*.2f);float bright=Smooth(ph/.3f)*(1-Smooth((ph-.85f)/.15f));
                sink.Glow(p,Lin(MoteHex,2.4f*bright),0,.07f*spawn,0,ph,seed+50+k);Particles++;
            }
        }
        // ---- the smoky tail: dark puffs every TailStep metres of the path it flew, violet embers among them ----
        public const float TailStep=.14f;
        private void Tail(IMagicFxSink sink,Orb o,float time,float quality,bool low)
        {
            if(o.Samples<2)return;float step=TailStep/Math.Max(.3f,Math.Min(1,quality))*(low?1.6f:1);
            int first=(int)Math.Ceiling(DistanceAt(o,time-TailLifeMax)/step),last=(int)Math.Floor(o.Travel/step);
            for(int j=Math.Max(0,first);j<=last;j++)
            {
                if(!AtDistance(o,j*step,out var at,out float born))continue;float a=time-born;long k=j+(long)o.Id*4096;float life=TailLifeMin+(TailLifeMax-TailLifeMin)*H(k,71);if(a<0||a>=life)continue;float u=a/life;
                // Fresh puffs right behind the core are small, so the core stays round.
                float behind=o.Live?Vector3.Distance(at,o.Last):9;if(behind<o.Radius*1.4f)continue;
                var jitter=new Vector3(H(k,72)-.5f,H(k,73)-.5f,H(k,74)-.5f)*.14f;var p=at+jitter*(1+2*u)-Vector3.UnitY*(.35f*a);
                sink.Smoke(p,Lin(SmokeHex),.9f,(.13f+.08f*H(k,75))*(1+1.8f*u)*Smooth(behind/.6f+.35f),u,k,NoFloor);Particles++;
                if(j%4==1)sink.Glow(p,Lin(VioletHex,.55f*(1-u)),0,(.22f+.2f*u),0,u,k+7);
                if(j%2==0){var drift=Vector3.Normalize(jitter+new Vector3(0,.001f,0))*(.8f+.6f*H(k,76));float eu=Clamp01(a/(life*.8f));sink.Streak(p+drift*a,drift,Lin(EmberHex,2.6f*(1-eu)*(1-eu)),.026f,.04f,.05f,.85f,eu,k);Particles++;}
            }
        }
        // ---- the cast: a dark gather at the right-hand socket over the windup, then the release pulse ----
        private void Casts(IMagicFxSink sink,IMagicFxScene scene,float time,float quality,bool low)
        {
            var actors=scene.Actors;if(actors==null)return;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null||!a.Alive||a.SourceSpec!="darkness")continue;float rate=a.CastRate>0?a.CastRate:1,windup=CastSeconds/rate,age=time-a.AttackAt;
                if(age<0||age>windup+.24f||!scene.Hand(a.Id,out var hand,out bool view))continue;
                var f=Forward(a.Yaw);var up=Vector3.UnitY;var side=Right(f);uint seed=(uint)(a.Id*977)+(uint)Math.Round(a.AttackAt*64);
                if(age<windup)
                {
                    float p=age/windup;int n=Count(14,quality,low);float close=Smooth(p);
                    for(int k=0;k<n;k++)
                    {
                        float ph=p*1.4f+k/(float)n;ph-=(float)Math.Floor(ph);float r=(view?.2f:.55f)*(float)Math.Pow(1-ph,1.25)+.03f,ang=k*2.5f+ph*5.5f+H(k,seed)*6.283f;
                        var radial=side*(float)Math.Cos(ang)+up*(float)Math.Sin(ang)*.85f+f*((float)Math.Sin(ang*.5f)*.35f);var pos=hand+radial*r;var inward=-radial*(r*10)+Vector3.Cross(radial,f)*(r*6);
                        float fade=Smooth(ph/.2f);
                        if(view)sink.View(pos,k%2==0?Vector3.Zero:Lin(k%4==1?SwirlHex:VioletHex,2.4f*fade),k%2==0?.8f*fade:0,(.025f+.02f*(1-ph)),0);
                        else if(k%2==0)sink.Drop(pos,radial*(.08f+.12f*(1-ph)),new Vector3(.02f,0,.035f),.88f*fade,.045f,ph,seed+k);
                        else sink.Streak(pos,inward,Lin(k%4==1?SwirlHex:VioletHex,2.5f*fade),.035f,.07f,.025f,.85f,ph,seed+k);
                        Particles++;
                    }
                    float core=.03f+.07f*close;
                    if(view){sink.View(hand,Vector3.Zero,.7f*close,.34f-.2f*close,2);sink.View(hand,Lin(RimHex,2.2f*close),0,core*2.2f,2);sink.View(hand,Vector3.Zero,.9f*close,core*1.1f,1);}
                    else{sink.Glow(hand,Vector3.Zero,.65f*close,.9f-.55f*close,2,p,seed);sink.Sphere(hand,core,new Vector3(.004f,.002f,.008f),Lin(RimHex,3f),Vector3.Zero);sink.Glow(hand,Lin(RimHex,2.4f*close),0,core*3.9f,2,p,seed+1);sink.Glow(hand,Lin(VioletHex,.5f*close),0,core*9,0,p,seed+2);}
                    Particles+=3;
                }
                else
                {
                    float q=(age-windup)/.24f;
                    if(view){sink.View(hand,Lin(RimHex,2.6f*(1-q)),0,.1f+.35f*q,2);sink.View(hand,Vector3.Zero,.5f*(1-q),.14f+.45f*q,2);}
                    else{sink.Glow(hand,Lin(RimHex,2.6f*(1-q)),0,.2f+.9f*q,2,q,seed);sink.Glow(hand,Vector3.Zero,.5f*(1-q),.3f+1.1f*q,2,q,seed+1);}
                    int n=Count(6,quality,low);
                    for(int k=0;k<n;k++)
                    {
                        float aa=age-windup;var dir=Vector3.Normalize(f*(2+H(k,seed+3))+side*(H(k,seed+4)-.5f)*1.6f+up*(H(k,seed+5)-.4f)*1.2f);var p=hand+dir*(5.5f*aa*(1-.6f*q));
                        if(view)sink.View(hand+(p-hand)*.35f,Lin(SwirlHex,2.2f*(1-q)),0,.025f,0);else sink.Streak(p,dir*(6*(1-q)),Lin(k%2==0?SwirlHex:VioletHex,2.4f*(1-q)),.026f,.05f,.03f,.85f,q,seed+10+k);
                        Particles++;
                    }
                }
            }
        }
        // ---- impacts: the implosion, then the dark burst; on a surface, the scorch and its cracks ----
        public const float ImplodeSeconds=.09f;
        private void Burst(IMagicFxSink sink,NativeMagicDecals decals,in Impact m,float time,float quality,bool low)
        {
            float a=time-m.Time;if(a<0||a>Math.Max(ImpactSeconds,m.Surface?ScorchSeconds:0))return;uint seed=(uint)m.Id*101u+7u;var c=m.At;float scale=m.Air?.7f:1;
            var n=m.Surface?m.Normal:Vector3.UnitY;Basis(n,out var t1,out var t2);
            if(a<ImpactSeconds)
            {
                if(a<ImplodeSeconds)
                {
                    float p=a/ImplodeSeconds;int k=Count(12,quality,low);
                    for(int j=0;j<k;j++){var dir=Dir(j,seed,n,m.Surface);float r=(.95f*(1-p)+.05f)*scale;sink.Streak(c+dir*r,-dir*10,Lin(j%2==0?VioletHex:SwirlHex,2.4f),.035f,.08f,.03f,.8f,p,seed+j);Particles++;}
                    float core=(.08f+.17f*Smooth(p))*scale;var cs=m.Surface?c+n*core:c;sink.Sphere(cs,core,new Vector3(.004f,.002f,.008f),Lin(RimHex,3f),Vector3.Zero);sink.Glow(cs,Vector3.Zero,.7f*p,core*6,2,p,seed);Particles+=2;
                }
                else
                {
                    float b=(a-ImplodeSeconds)/(ImpactSeconds-ImplodeSeconds),ring=Clamp01((a-ImplodeSeconds)/.28f),flash=Clamp01((a-ImplodeSeconds)/.22f);
                    float core=.25f*scale*(1-Smooth((a-ImplodeSeconds)/.12f));if(core>.01f){sink.Sphere(m.Surface?c+n*core:c,core,new Vector3(.004f,.002f,.008f),Lin(RimHex,3f),Vector3.Zero);Particles++;}
                    if(flash<1){sink.Glow(c,Lin(SwirlHex,3.4f*(1-flash)*(1-flash)),0,(.6f+1.4f*flash)*scale,1,flash,seed);sink.Glow(c,Lin(VioletHex,1.2f*(1-flash)),0,(1.4f+1.6f*flash)*scale,0,flash,seed+3);Particles+=2;}
                    if(ring<1){float fade=(1-ring)*(1-ring);sink.Glow(c,Lin(RimHex,3f*fade),0,(.4f+2.3f*ring)*scale,2,ring,seed+1);sink.Glow(c,Vector3.Zero,.7f*fade,(.5f+2.6f*ring)*scale,2,ring,seed+2);Particles+=2;}
                    int smoke=Count(10,quality,low),embers=Count(18,quality,low),ink=Count(12,quality,low);float tau=a-ImplodeSeconds;
                    for(int j=0;j<smoke;j++)
                    {
                        float life=.5f+.25f*H(j,seed+3);if(tau>=life)continue;float u=tau/life;var dir=Dir(j+40,seed,n,m.Surface);
                        var p=c+dir*((.12f+1.25f*(1-(float)Math.Exp(-4*tau)))*scale)+Vector3.UnitY*(.25f*tau);
                        sink.Smoke(p,Lin(SmokeHex,1.1f),.95f,(.28f+.16f*H(j,seed+4))*(1+.9f*u)*scale,u,seed+100+j,NoFloor);if(j%3==0)sink.Glow(p,Lin(VioletHex,.7f*(1-u)),0,(.5f+.4f*u)*scale,0,u,seed+150+j);Particles++;
                    }
                    for(int j=0;j<embers;j++)
                    {
                        float life=.35f+.25f*H(j,seed+5);if(tau>=life)continue;float u=tau/life;var dir=Dir(j+80,seed,n,m.Surface);float sp=(4+5*H(j,seed+6))*scale;
                        var p=c+dir*(sp*.35f*(1-(float)Math.Exp(-tau/.35f*3))/1)+Vector3.UnitY*(-.5f*tau*tau);
                        sink.Streak(p,dir*(sp*(1-u)),Lin(j%3==0?SwirlHex:EmberHex,2.6f*(1-u)),.03f,.05f,.04f,.85f,u,seed+200+j);Particles++;
                    }
                    for(int j=0;j<ink;j++)
                    {
                        float life=.4f+.3f*H(j,seed+7);if(tau>=life)continue;float u=tau/life;var dir=Dir(j+120,seed,n,m.Surface);var v0=dir*((2.5f+3f*H(j,seed+8))*scale)+Vector3.UnitY*1.2f;
                        var p=c+v0*tau+new Vector3(0,-4.9f*tau*tau,0);var v=v0+new Vector3(0,-9.8f*tau,0);sink.Drop(p,v*.035f,new Vector3(.03f,0,.05f),.85f*(1-u*u),.05f*scale,u,seed+300+j);Particles++;
                    }
                }
            }
            if(m.Surface)Cracks(decals,m,a,seed,t1,t2);
        }
        // A unit direction for burst particle j: over the hemisphere about a surface's normal, else the sphere.
        private static Vector3 Dir(int j,uint seed,Vector3 n,bool hemisphere)
        {
            float z=H(j,seed+400)*2-1,ang=H(j,seed+401)*6.2832f,s=(float)Math.Sqrt(Math.Max(0,1-z*z));var d=new Vector3(s*(float)Math.Cos(ang),z,s*(float)Math.Sin(ang));
            if(hemisphere&&Vector3.Dot(d,n)<0)d-=2*Vector3.Dot(d,n)*n;return hemisphere?Vector3.Normalize(d+n*.35f):d;
        }
        /// <summary>The void scorch and its glowing cracks on the surface an orb burst against: bright first, dimming, gone by ScorchSeconds.</summary>
        public static float CrackGlow(float age)=>age<0?0:age<.25f?1:Math.Max(0,1-Smooth((age-.25f)/1.6f)*.8f-Smooth((age-1.6f)/(ScorchSeconds-1.6f))*.2f);
        private static void Cracks(NativeMagicDecals decals,in Impact m,float a,uint seed,Vector3 t1,Vector3 t2)
        {
            float scorch=1-Smooth((a-.4f)/(ScorchSeconds-.4f)),glow=CrackGlow(a);if(scorch<=0)return;var n=m.Normal;var c=m.At-n*.025f;
            decals.Disc(c+n*.012f,n,.62f,H(1,seed)*6.283f,scorch*Smooth(a/.06f),a,seed,3);
            int cracks=7;
            for(int k=0;k<cracks;k++)
            {
                float ang=(k+.5f*H(k,seed+500))*6.2832f/cracks,len=.32f+.42f*H(k,seed+501);var dir=t1*(float)Math.Cos(ang)+t2*(float)Math.Sin(ang);
                float kink=(H(k,seed+502)-.5f)*.9f;var dir2=t1*(float)Math.Cos(ang+kink)+t2*(float)Math.Sin(ang+kink);
                var p0=c+dir*.08f;var p1=p0+dir*(len*.5f);var p2=p1+dir2*(len*.5f);float grow=Smooth((a-ImplodeSeconds)/.08f);
                Segment(decals,p0,Vector3.Lerp(p0,p1,Clamp01(grow*2)),n,.05f,glow,0,.5f,seed+k);if(grow>.5f)Segment(decals,p1,Vector3.Lerp(p1,p2,Clamp01(grow*2-1)),n,.04f,glow,.5f,1,seed+k);
                if(H(k,seed+503)<.55f&&grow>.5f){float b=ang+(H(k,seed+504)<.5f?.7f:-.7f);var dir3=t1*(float)Math.Cos(b)+t2*(float)Math.Sin(b);Segment(decals,p1,p1+dir3*(len*.35f*Clamp01(grow*2-1)),n,.03f,glow*.8f,.45f,1,seed+k+50);}
            }
        }
        // One crack segment: a quad on the surface along a to b, half width w; Data x across (-1..1), y along (from..to of the crack).
        private static void Segment(NativeMagicDecals decals,Vector3 a,Vector3 b,Vector3 n,float w,float glow,float from,float to,float seed)
        {
            var d=b-a;if(d.LengthSquared()<1e-6f)return;var side=Vector3.Normalize(Vector3.Cross(n,d))*w;var lift=n*.016f;
            decals.Quad(a-side+lift,a+side+lift,b+side+lift,b-side+lift,new Vector4(-1,from,glow,0),new Vector4(1,from,glow,0),new Vector4(1,to,glow,0),new Vector4(-1,to,glow,0),n,seed,4);
        }
        // ---- the lifesteal stream: dark red to violet from the victim back to the caster; the heal glint on the caster ----
        public const float StreamDelay=.06f,StreamSpread=.018f;public const int StreamParticles=32;
        /// <summary>How long the stream takes to reach the caster over a distance (m): a little longer from further away.</summary>
        public static float StreamTravel(float distance)=>.3f+.024f*Math.Min(distance,36);
        private void Stream(IMagicFxSink sink,IMagicFxScene scene,in DrainRecord d,float time,float quality,bool low)
        {
            float a=time-d.Time;if(a<0||a>DrainSeconds+1.2f)return;if(!scene.Chest(d.Source,out var to))return;
            var from=scene.Chest(d.Target,out var victim)?Vector3.Lerp(d.From,victim,.5f):d.From;var span=to-from;float dist=span.Length();if(dist<.3f)return;
            var dir=span/dist;var side=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,dir).LengthSquared()>1e-6f?Vector3.Cross(Vector3.UnitY,dir):Vector3.UnitX);uint seed=(uint)d.Id*53u+11u;
            float travel=StreamTravel(dist);var mid=(from+to)*.5f+Vector3.UnitY*(.45f+.12f*Math.Min(dist,12))+side*((H(1,seed)-.5f)*.6f);
            Vector3 B(float s){float r=1-s;return from*(r*r)+mid*(2*r*s)+to*(s*s);}Vector3 T(float s)=>2*(1-s)*(mid-from)+2*s*(to-mid);
            int n=Count(StreamParticles,quality,low);
            for(int k=0;k<n;k++)
            {
                // Two strands twisting round each other along a bowed path, beaded with glows and dark drops of ink.
                float s=(a-StreamDelay-(k>>1)*2*StreamSpread*(StreamParticles/(float)n))/travel;if(s<0||s>1)continue;var tan=T(s);var tn=Vector3.Normalize(tan);Basis(tn,out var q1,out var q2);
                float spin=s*(8+dist*.9f)+(k&1)*3.1416f+(float)Math.Sin(k*.7f)*.3f,swell=(float)Math.Sin(Math.PI*s);
                var p=B(s)+(q1*(float)Math.Cos(spin)+q2*(float)Math.Sin(spin))*(.03f+.13f*swell);
                var col=Vector3.Lerp(Lin(BloodHex,2.5f),Lin(VioletHex,2.4f),s*s);float fade=Smooth(s/.08f)*(1-Smooth((s-.92f)/.08f));
                if(k%5==4)sink.Drop(p,tn*.12f,new Vector3(.08f,0,.02f),.8f*fade,.05f,s,seed+k);
                else{sink.Streak(p,tan*(.55f/travel),col*fade,.055f,.07f,.025f,.85f,s,seed+k);if(k%2==0)sink.Glow(p,col*(.5f*fade),0,.17f,0,s,seed+100+k);}
                Particles++;
            }
            // Drawn out of the wound: a small dark-red burst on the victim.
            if(a<.18f){float u=a/.18f;int m=Count(6,quality,low);for(int k=0;k<m;k++){var dd=Dir(k,seed,Vector3.UnitY,false);sink.Streak(from+dd*(.1f+.4f*u),-dd*3,Lin(BloodHex,2.4f*(1-u)),.03f,.05f,.04f,.85f,u,seed+40+k);Particles++;}}
            // Arriving: the caster's heal glint when the replicated Heal paired with the hit (the 20% drain), else a faint absorb.
            float g=(a-StreamDelay-travel*.85f)/GlintSeconds;if(g<0||g>=1)return;float fall=(1-g)*(1-g);
            if(scene.Hand(d.Source,out var hand,out bool view)&&view){sink.View(hand,Lin(d.Healed?HealHex:VioletHex,(d.Healed?2.6f:1.3f)*(1-g)),0,.14f+.3f*g,2);if(d.Healed)sink.View(hand,Lin(HealHex,2.4f*fall),0,.22f,3);Particles++;}
            if(!d.Healed){sink.Glow(to,Lin(VioletHex,1.4f*(1-g)),0,.3f+.4f*g,0,g,seed+60);Particles++;return;}
            sink.Glow(to,Lin(HealHex,4f*(1-g)),0,.45f+1.2f*g,2,g,seed+61);sink.Glow(to,Lin(HealHex,4f*fall),0,.9f,3,g,seed+62);sink.Glow(to,Lin(BloodHex,1.3f*fall),0,1.4f,0,g,seed+63);Particles+=3;
            if(g<.45f){float u=g/.45f;int sparks=Count(8,quality,low);for(int k=0;k<sparks;k++){var dd=Dir(k,seed+90,Vector3.UnitY,false);sink.Streak(to+dd*(.15f+.6f*u),dd*4,Lin(HealHex,3f*(1-u)),.035f,.06f,.03f,.85f,u,seed+80+k);Particles++;}}
            int rise=Count(8,quality,low);for(int k=0;k<rise;k++){float ang=k*6.283f/rise+H(k,seed+70);var p=to+new Vector3((float)Math.Cos(ang)*(.3f-.1f*g),-.35f+1.1f*g*(.8f+.4f*H(k,seed+71)),(float)Math.Sin(ang)*(.3f-.1f*g));sink.Glow(p,Lin(HealHex,2.6f*(1-g)),0,.09f,0,g,seed+70+k);Particles++;}
        }
    }
}

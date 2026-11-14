using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The primitive a <see cref="BossVfxItem"/> is drawn as by the shared effects renderer (Runtime NativeVfxDraw).</summary>
    public enum BossVfxKind:byte{Glow,Spark,Smoke,Fire,Rubble,Drop}

    /// <summary>
    /// One particle of a zone boss's effects for the shared effects renderer (Runtime/NativeBossVfxSubmit.cs hands it to
    /// NativeVfxDraw): a soft HDR glow, a motion-stretched spark (a capsule along its velocity with a hot core), a soft lit smoke
    /// puff (the atlas puff, soft against the scene's depth, lit by the key light and the ambient, glowing a little in its own
    /// colour when it is lit from below by molten metal), a soft fire puff (emissive in its own hue, cooling into soot), a lit
    /// rubble chunk or a blended teardrop. Colours are linear; glows, sparks and fire may exceed 1.
    /// </summary>
    public struct BossVfxItem
    {
        public BossVfxKind Kind;
        /// <summary>Glow, smoke, fire, rubble: the centre; spark, drop: the head.</summary>
        public Vector3 At;
        /// <summary>Spark: the velocity (m/s); drop: the segment behind the head (m); rubble: the rotation (axis times angle, rad).</summary>
        public Vector3 Axis;
        /// <summary>Linear colour (glow, spark, fire: HDR; smoke, rubble: albedo).</summary>
        public Vector3 Rgb;
        /// <summary>Glow, spark, drop: coverage (0 adds); smoke, fire: opacity 0..1; rubble: its emission gain.</summary>
        public float A;
        /// <summary>Glow, smoke, fire: width (m); spark, drop: width (m); rubble: size (m).</summary>
        public float Size;
        /// <summary>Normalized age 0..1 (the atlas erosion), the seed (the puff's shape, the chunk's variant) and the floor a puff
        /// fades out below (<see cref="NativeBossVfx.NoFloor"/> none).</summary>
        public float U,Seed,Floor;
        /// <summary>Glow: its profile (0 mote, 1 core, 2 ring); spark: its minimum length (m); smoke: its own-colour glow 0..1.</summary>
        public float P0;
        /// <summary>Spark: the motion blur (s: the streak is velocity times this, at least the minimum length) and the tail's taper.</summary>
        public float P1,P2;
    }

    /// <summary>
    /// A zone boss's particles for one frame (THE CONVERTER's and THE 6:47's sparks, spatter, embers, smoke, steam, dust, fire and
    /// debris; docs/EXPEDITION-BOSS-CITY-IRONWORKS.md and docs/EXPEDITION-BOSS-CENTRAL-STATION.md "Effects realism"): a fixed array
    /// of <see cref="BossVfxItem"/>s and deterministic emitters on closed-form physics (gravity, linear air drag, one floor
    /// bounce, buoyant smoke that slows as it entrains air and spreads). Every particle is a pure function of its emitter's seed,
    /// its birth time and the clock, so host, guests, the killcam and the DEV preview draw the same frame from the same replicated
    /// times, at any frame rate. Counts scale with the effects quality and the low tier (<see cref="Scale"/>); telegraphs never
    /// come through here. Allocation free: <see cref="Add"/> refuses past the capacity.
    /// </summary>
    public sealed class NativeBossVfx
    {
        public const float NoFloor=-1e9f,Gravity=9.81f;
        /// <summary>Glow profiles (NativeVfxDraw's Mote, CoreGlow, Ring).</summary>
        public const float Mote=0,Core=1,RingGlow=2;
        public readonly BossVfxItem[] Items;public int Count;
        /// <summary>This frame's counts by family (budget lines, checks).</summary>
        public int Sparks,Puffs,Glows,Debris,Refused;
        private double quality=1;private bool low;
        public NativeBossVfx(int capacity){Items=new BossVfxItem[capacity];}
        /// <summary>Starts a frame at the effects quality (0..1) and tier (low: half the particles).</summary>
        public void Begin(double effects,bool lowTier){Count=0;Sparks=Puffs=Glows=Debris=Refused=0;quality=effects;low=lowTier;}
        public bool Low=>low;
        /// <summary>How many of `full` particles this quality and tier draw (the Brickfall atmosphere's rule: 30% at the least,
        /// halved on the low tier).</summary>
        public int Scale(int full)=>NativeBrickfallAtmosphere.Count(full,quality,low);
        /// <summary>The share of a continuous emitter's rate this quality and tier keep.</summary>
        public float RateScale=>(float)(Math.Max(.3,Math.Min(1,double.IsNaN(quality)?1:quality))*(low?.5:1));

        private bool Add(in BossVfxItem item){if(Count>=Items.Length){Refused++;return false;}Items[Count++]=item;return true;}
        public void Glow(Vector3 at,Vector3 rgb,float size,float profile=Core,float cover=0)
        {if(!(size>1e-4f)||rgb.X+rgb.Y+rgb.Z<.004f)return;if(Add(new BossVfxItem{Kind=BossVfxKind.Glow,At=at,Rgb=rgb,A=cover,Size=size,P0=profile}))Glows++;}
        /// <summary>A spark (or a glowing droplet) at `head` moving at `velocity`: a streak velocity x blur long (at least `min`),
        /// `width` wide, its tail `taper` dimmer.</summary>
        public void Spark(Vector3 head,Vector3 velocity,Vector3 rgb,float width,float min=.04f,float blur=.04f,float taper=.85f,float u=0,float seed=0)
        {if(!(width>1e-4f)||rgb.X+rgb.Y+rgb.Z<.004f)return;if(Add(new BossVfxItem{Kind=BossVfxKind.Spark,At=head,Axis=velocity,Rgb=rgb,Size=width,P0=min,P1=blur,P2=taper,U=u,Seed=seed}))Sparks++;}
        /// <summary>A soft lit puff: albedo (linear), opacity, width, normalized age, seed, the floor it fades below, its own glow.</summary>
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor=NoFloor,float glow=0)
        {if(!(opacity>.004f)||!(size>1e-3f))return;if(Add(new BossVfxItem{Kind=BossVfxKind.Smoke,At=at,Rgb=albedo,A=Math.Min(1,opacity),Size=size,U=u,Seed=seed,Floor=floor,P0=glow}))Puffs++;}
        /// <summary>A soft fire puff: emissive in `tint` (linear HDR), cooling over u into lit soot.</summary>
        public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor=NoFloor)
        {if(!(opacity>.004f)||!(size>1e-3f))return;if(Add(new BossVfxItem{Kind=BossVfxKind.Fire,At=at,Rgb=tint,A=Math.Min(1,opacity),Size=size,U=u,Seed=seed,Floor=floor}))Puffs++;}
        /// <summary>A lit rubble chunk of about `size` m, turned by `turn` (axis times angle), its albedo, a little emission.</summary>
        public void Rubble(Vector3 at,Vector3 turn,float size,int seed,Vector3 albedo,float emissive=0)
        {if(!(size>1e-3f))return;if(Add(new BossVfxItem{Kind=BossVfxKind.Rubble,At=at,Axis=turn,Rgb=albedo,A=emissive,Size=size,Seed=seed}))Debris++;}
        /// <summary>A blended teardrop whose round head is at `head`, `segment` the tail behind it.</summary>
        public void Drop(Vector3 head,Vector3 segment,Vector3 rgb,float width,float cover,float u,float seed)
        {if(!(width>1e-4f))return;if(Add(new BossVfxItem{Kind=BossVfxKind.Drop,At=head,Axis=segment,Rgb=rgb,A=cover,Size=width,U=u,Seed=seed}))Sparks++;}

        // ============================================================================================================
        // physics (closed form; every particle a pure function of its birth and the clock)
        // ============================================================================================================
        /// <summary>A particle's position `t` s after leaving `p0` at `v0`, under gravity `g` (m/s^2, down) and linear air drag
        /// `k` (1/s: the velocity relaxes toward the terminal g/k), and its velocity then.</summary>
        public static Vector3 Fly(Vector3 p0,Vector3 v0,float k,float g,float t,out Vector3 v)
        {
            if(t<=0){v=v0;return p0;}
            if(k<1e-4f){v=new Vector3(v0.X,v0.Y-g*t,v0.Z);return new Vector3(p0.X+v0.X*t,p0.Y+v0.Y*t-.5f*g*t*t,p0.Z+v0.Z*t);}
            float e=(float)Math.Exp(-k*t),w=(1-e)/k,term=-g/k;
            v=new Vector3(v0.X*e,term+(v0.Y-term)*e,v0.Z*e);return new Vector3(p0.X+v0.X*w,p0.Y+term*t+(v0.Y-term)*w,p0.Z+v0.Z*w);
        }
        /// <summary>
        /// A particle that falls onto the floor at `floor` and bounces once (vertical restitution `rest`, `keep` of its sliding
        /// speed), then slides to rest on it. Drag-free ballistic arcs with the velocity scaled by `damp` per second of flight
        /// (so the crossing is exact). `landed` reports the first contact (and the time of it, `at`).
        /// </summary>
        public static Vector3 Bounce(Vector3 p0,Vector3 v0,float g,float floor,float rest,float keep,float t,out Vector3 v,out bool landed,out float at)
        {
            landed=false;at=float.NaN;
            float h=p0.Y-floor;float disc=v0.Y*v0.Y+2*g*Math.Max(0,h);float t1=(v0.Y+(float)Math.Sqrt(disc))/g;
            if(t<=t1||!(t1>0)){v=new Vector3(v0.X,v0.Y-g*t,v0.Z);return new Vector3(p0.X+v0.X*t,p0.Y+v0.Y*t-.5f*g*t*t,p0.Z+v0.Z*t);}
            landed=true;at=t1;var hit=new Vector3(p0.X+v0.X*t1,floor,p0.Z+v0.Z*t1);float vy=-(v0.Y-g*t1)*rest;var vh=new Vector3(v0.X*keep,0,v0.Z*keep);
            float t2=2*vy/g,s=t-t1;
            if(s<t2){v=new Vector3(vh.X,vy-g*s,vh.Z);return new Vector3(hit.X+vh.X*s,floor+vy*s-.5f*g*s*s,hit.Z+vh.Z*s);}
            // Sliding to rest (friction decel 6 m/s^2).
            var p2=new Vector3(hit.X+vh.X*t2,floor,hit.Z+vh.Z*t2);float sp=(float)Math.Sqrt(vh.X*vh.X+vh.Z*vh.Z);if(sp<1e-4f){v=Vector3.Zero;return p2;}
            float slide=Math.Min(s-t2,sp/6f),d=sp*slide-3f*slide*slide;var dir=new Vector3(vh.X/sp,0,vh.Z/sp);float left=Math.Max(0,sp-6f*slide);v=dir*left;return p2+dir*d;
        }
        /// <summary>A spark's light as it cools over its life u (0..1): white-yellow, orange, a dull red, out (linear HDR).</summary>
        public static Vector3 SparkHeat(float u,float bright=1)
        {
            u=BossMath.Clamp01(u);Vector3 c;
            if(u<.3f)c=Vector3.Lerp(new Vector3(3.8f,3.0f,1.9f),new Vector3(3.4f,1.6f,.42f),u/.3f);
            else if(u<.75f)c=Vector3.Lerp(new Vector3(3.4f,1.6f,.42f),new Vector3(1.5f,.36f,.06f),(u-.3f)/.45f);
            else c=Vector3.Lerp(new Vector3(1.5f,.36f,.06f),new Vector3(.25f,.03f,.005f),(u-.75f)/.25f);
            return c*bright;
        }
        /// <summary>A unit direction spread round `dir` (spread 0 straight, 1 a wide cone, 2 nearly all round), from three hashes.</summary>
        public static Vector3 Cone(Vector3 dir,float spread,float h0,float h1,float h2)
        {
            float l=dir.Length();var d=l>1e-5f?dir/l:Vector3.UnitY;var j=new Vector3(h0-.5f,h1-.5f,h2-.5f)*(2*spread);var r=d+j;float rl=r.Length();return rl>1e-5f?r/rl:d;
        }
        private static float H(uint s,int i)=>BossMath.Hash(s,i);

        // ============================================================================================================
        // emitters
        // ============================================================================================================
        /// <summary>
        /// A burst of `count` sparks (scaled by the quality) from `p` round `dir` (cone `spread`), `speed` m/s (60% to 115% of it),
        /// born over [t0, t0 + span), each living `life` (60% to 100%): drag `drag`, gravity, one bounce on `floor` when `bounce`,
        /// cooling white to red, motion-stretched. `bright` scales the light.
        /// </summary>
        public void SparkBurst(float t,Vector3 p,Vector3 dir,float spread,float speed,int count,float life,float t0,float span,uint seed,int key,float bright=1,float width=.028f,float drag=.9f,bool bounce=true,float floor=0)
        {
            if(bright<=.01f||t<t0||t>t0+span+life)return;int n=Scale(count);
            for(int i=0;i<n;i++)
            {
                int k=key*977+i*7;float born=t0+span*H(seed,k),age=t-born,lf=life*(.6f+.4f*H(seed,k+1));if(age<0||age>lf)continue;
                var v0=Cone(dir,spread,H(seed,k+2),H(seed,k+3),H(seed,k+4))*(speed*(.6f+.55f*H(seed,k+5)));
                Vector3 at,vel;
                if(bounce){at=Bounce(p,v0*(1/(1+.3f*drag)),Gravity,floor,.32f,.55f,age,out vel,out _,out _);}
                else{at=Fly(p,v0,drag,Gravity,age,out vel);if(at.Y<floor)continue;}
                float u=age/lf;Spark(at,vel,SparkHeat(u,bright),width*(1-.4f*u),.03f,.035f,.85f,u,i);
            }
        }
        /// <summary>A continuous spark fountain over [start, end] at `rate` per second (scaled by the quality): as
        /// <see cref="SparkBurst"/>, each spark born at its own slot time. The rate is fixed (so no spark changes slot); `amount`
        /// 0..1 thins the flow smoothly (each spark fades out as the amount drops past its own hash), `trail` the emitter's own
        /// velocity (older sparks start where it was).</summary>
        public void SparkFlow(float t,Vector3 p,Vector3 dir,float spread,float speed,float rate,float life,float start,float end,uint seed,int key,float bright=1,float width=.024f,float drag=1.1f,bool bounce=true,float floor=0,Vector3 trail=default,float amount=1)
        {
            float r=rate*RateScale;if(r<=0||bright<=.01f||amount<=.01f||t<start||t>end+life)return;
            int last=(int)Math.Floor((Math.Min(t,end)-start)*r),first=Math.Max(0,(int)Math.Floor((t-life-start)*r));if(last-first>400)first=last-400;
            for(int i=first;i<=last;i++)
            {
                int k=key*1543+i*5;float born=start+(i+H(seed,k)*.9f)/r,age=t-born,lf=life*(.6f+.4f*H(seed,k+1));if(age<0||age>lf||born>end)continue;
                float gate=BossMath.Smooth((amount*1.12f-H(seed,k+6))/.12f);if(gate<=0)continue;
                var v0=Cone(dir,spread,H(seed,k+2),H(seed,k+3),H(seed,k+4))*(speed*(.6f+.55f*H(seed,k+4)));
                Vector3 at,vel;
                var from=p-trail*age;
                if(bounce)at=Bounce(from,v0*(1/(1+.3f*drag)),Gravity,floor,.32f,.55f,age,out vel,out _,out _);
                else{at=Fly(from,v0,drag,Gravity,age,out vel);if(at.Y<floor)continue;}
                float u=age/lf;Spark(at,vel,SparkHeat(u,bright*gate),width*(1-.4f*u),.03f,.035f,.85f,u,i);
            }
        }
        /// <summary>
        /// Buoyant smoke or steam from `p`: puffs leaving at `vel` (scattered by `scatter`), slowed by drag 1.6/s while rising at
        /// `rise` m/s, spreading from `size0` to `size1` as they entrain air (an exponential approach), turning in a slow drift;
        /// opacity fading in over the first 12% of a puff's life and out over its end. Continuous over [start, end] at `rate` per
        /// second (scaled by the quality; fixed, `amount` 0..1 thins it smoothly as for sparks; `trail` the emitter's velocity).
        /// `glow`: its own-colour light at birth (smoke lit from below by molten metal), fading as it rises. `heat`: a warm tint
        /// at birth fading to the albedo. `lit`: a steady own-colour light (dust and steam catching the station's lamps, which the
        /// effects renderer's puffs do not take).
        /// </summary>
        public void SmokeFlow(float t,Vector3 p,Vector3 vel,float scatter,float rise,float rate,float life,float size0,float size1,Vector3 albedo,float opacity,float start,float end,uint seed,int key,float glow=0,float floor=NoFloor,Vector3 heat=default,Vector3 trail=default,float amount=1,float lit=0)
        {
            float r=rate*RateScale;if(r<=0||opacity<=.004f||amount<=.01f||t<start||t>end+life)return;
            int last=(int)Math.Floor((Math.Min(t,end)-start)*r),first=Math.Max(0,(int)Math.Floor((t-life-start)*r));if(last-first>160)first=last-160;
            for(int i=first;i<=last;i++)
            {
                int k=key*2131+i*9;float born=start+(i+H(seed,k)*.8f)/r,age=t-born,lf=life*(.75f+.25f*H(seed,k+1));if(age<0||age>lf||born>end)continue;
                float gate=BossMath.Smooth((amount*1.15f-H(seed,k+8))/.15f);if(gate<=0)continue;
                Puff(p-trail*age,vel,scatter,rise,age,lf,size0,size1,albedo,opacity*gate,seed,k,glow,floor,heat,lit);
            }
        }
        /// <summary>A burst of `count` puffs (scaled) born over [t0, t0 + span): as <see cref="SmokeFlow"/>.</summary>
        public void SmokeBurst(float t,Vector3 p,Vector3 vel,float scatter,float rise,int count,float life,float size0,float size1,Vector3 albedo,float opacity,float t0,float span,uint seed,int key,float glow=0,float floor=NoFloor,Vector3 heat=default,float lit=0)
        {
            if(opacity<=.004f||t<t0||t>t0+span+life)return;int n=Scale(count);
            for(int i=0;i<n;i++)
            {
                int k=key*2131+i*9;float born=t0+span*H(seed,k),age=t-born,lf=life*(.75f+.25f*H(seed,k+1));if(age<0||age>lf)continue;
                Puff(p,vel,scatter,rise,age,lf,size0,size1,albedo,opacity,seed,k,glow,floor,heat,lit);
            }
        }
        private void Puff(Vector3 p,Vector3 vel,float scatter,float rise,float age,float lf,float size0,float size1,Vector3 albedo,float opacity,uint seed,int k,float glow,float floor,Vector3 heat,float lit)
        {
            var v0=vel+new Vector3(H(seed,k+2)-.5f,(H(seed,k+3)-.5f)*.5f,H(seed,k+4)-.5f)*(2*scatter);
            // Drag 1.6/s on the launch, the buoyant rise (its own slow climb) and a lazy turning drift.
            const float drag=1.6f;float e=(float)Math.Exp(-drag*age);var at=p+v0*((1-e)/drag)+new Vector3(0,rise*age,0);
            float ph=H(seed,k+5)*6.28f,w=.35f+.3f*H(seed,k+6);at+=new Vector3((float)Math.Sin(ph+age*w)-(float)Math.Sin(ph),0,(float)Math.Cos(ph*1.3f+age*w)-(float)Math.Cos(ph*1.3f))*(.45f*age/(1+.4f*age));
            float u=age/lf,grow=1-(float)Math.Exp(-3.2f*u);float size=size0+(size1-size0)*grow;
            float a=opacity*BossMath.Smooth(u/.12f)*(1-u)*(1-u*.35f);
            var col=heat==default?albedo:Vector3.Lerp(heat,albedo,BossMath.Smooth(u*2.2f));
            Smoke(at,col,a,size,u,H(seed,k+7)*8191,floor,glow*(1-BossMath.Smooth(u*1.8f))+lit);
        }
        /// <summary>
        /// A burst of `count` rubble chunks (scaled; at least one) from `p` round `dir`: flying with gravity, one bounce, sliding to
        /// rest and fading after `life`; tumbling while airborne. `hot` &gt; 0 starts them glowing (slag, sparks of the iron).
        /// </summary>
        public void RubbleBurst(float t,Vector3 p,Vector3 dir,float spread,float speed,int count,float life,float t0,float size,Vector3 albedo,uint seed,int key,float hot=0,float floor=0)
        {
            float age=t-t0;if(age<0||age>life)return;int n=Math.Max(1,Scale(count));
            for(int i=0;i<n;i++)
            {
                int k=key*3301+i*11;var v0=Cone(dir,spread,H(seed,k),H(seed,k+1),H(seed,k+2))*(speed*(.5f+.6f*H(seed,k+3)));
                var at=Bounce(p,v0,Gravity,floor,.28f,.45f,age,out var vel,out bool landed,out float when);
                float tumble=landed?when:age;var axis=Vector3.Normalize(new Vector3(H(seed,k+4)-.5f,H(seed,k+5)-.5f,H(seed,k+6)-.5f)+new Vector3(0,0,.01f));
                float s=size*(.55f+.7f*H(seed,k+7));at.Y=Math.Max(at.Y,floor+s*.35f);float fade=1-BossMath.Smooth((age-life*.7f)/(life*.3f));
                Rubble(at,axis*(tumble*(6+8*H(seed,k+8))),s*fade,(int)(H(seed,k+9)*997),albedo,hot*Math.Max(0,1-age/1.2f));
            }
        }
    }
}

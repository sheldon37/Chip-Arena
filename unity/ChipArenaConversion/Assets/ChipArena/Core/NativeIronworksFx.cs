using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One effect primitive (Unity world, linear HDR colour): a camera-facing glow, a camera-facing streak a to b, a flat
    /// decal quad a-b-c-d (additive or alpha-blended), a lit chunk (rubble or a slag shard: albedo, emission, turn) or a toon
    /// smoke puff.</summary>
    public enum IronFxKind:byte{Glow,Streak,Decal,Chunk,Smoke}
    public struct IronFx{public IronFxKind Kind;public Vector3 A,B,C,D,Color,Emit;public float Alpha,Size;public bool Additive;public Quaternion Turn;public float Seed;}
    /// <summary>The frame's Ironworks effects: fixed arrays (no allocation), filled by NativeIronworksFx and drawn by the
    /// presentation (Runtime/NativeIronworksKindsPresentation.cs) or the render harness; anything past the capacity is dropped.</summary>
    public sealed class IronFxBuffer
    {
        public readonly IronFx[] Items;public int Count;public IronFxBuffer(int capacity){Items=new IronFx[capacity];}
        public void Clear()=>Count=0;
        public bool Full=>Count>=Items.Length;
        public void Glow(Vector3 at,float size,Vector3 color,float alpha){if(alpha<=.002f||Full)return;Items[Count++]=new IronFx{Kind=IronFxKind.Glow,A=at,Size=size,Color=color,Alpha=Math.Min(1,alpha),Additive=true};}
        public void Streak(Vector3 a,Vector3 b,float width,Vector3 color,float alpha){if(alpha<=.002f||Full)return;Items[Count++]=new IronFx{Kind=IronFxKind.Streak,A=a,B=b,Size=width,Color=color,Alpha=Math.Min(1,alpha),Additive=true};}
        public void Decal(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 color,float alpha,bool additive){if(alpha<=.002f||Full)return;Items[Count++]=new IronFx{Kind=IronFxKind.Decal,A=a,B=b,C=c,D=d,Color=color,Alpha=Math.Min(1,alpha),Additive=additive};}
        public void Chunk(Vector3 at,Quaternion turn,float size,Vector3 albedo,Vector3 emit,float seed){if(Full)return;Items[Count++]=new IronFx{Kind=IronFxKind.Chunk,A=at,Turn=turn,Size=size,Color=albedo,Emit=emit,Alpha=1,Seed=seed};}
        public void Smoke(Vector3 at,float size,Vector3 color,float alpha,float seed){if(alpha<=.002f||Full)return;Items[Count++]=new IronFx{Kind=IronFxKind.Smoke,A=at,Size=size,Color=color,Alpha=Math.Min(1,alpha),Seed=seed};}
    }

    /// <summary>What one Ironworks add's effects read this frame (Unity world): its kind (the zone's local index), seed, clock,
    /// floor point, yaw (deg) and scale, its attack (LastAttack's age, strike, recovery), its view, where its chest, head, hands,
    /// hook end and ladle lip are, the pilot it hunts (or its hook holds), its speed and whether it is alive.</summary>
    public struct IronFxInput
    {
        public int Kind;public uint Seed;public float Time,Yaw,Scale,Speed;public Vector3 Floor,Chest,Head,HandL,HandR,Hook,Lip,Target;public bool HasTarget,Hanging;
        public float AttackAge,Strike,Recover;public IronworksView View;
        /// <summary>The Rigger's chain anchor on the girder and its hang point's feet (world), while it hangs or drops.</summary>
        public Vector3 Anchor,Landing;
    }

    /// <summary>
    /// The City Ironworks kinds' effects (docs/EXPEDITION-CITY-IRONWORKS.md 6, the readability rules of 3.2): pure functions of
    /// the add's replicated times (LastAttack, the view's) and the presentation clock, so every peer draws the same frame.
    /// Telegraphs are never the environment's plain orange: area attacks cream-white decals with a hard edge (the slam ring, the
    /// pour arc, the drop ring, the swing ring), lines cyan dashes (the Stoker's pull), fast attacks a white flare on the body
    /// (the Cinderling's core). Molten surfaces stay below a telegraph's brightness (the pool at most <see cref="PoolMost"/>).
    /// </summary>
    public static class NativeIronworksFx
    {
        public static readonly Vector3 Cream=Lin(NativeIronworksKinds.Cream),Cyan=Lin(NativeIronworksKinds.Cyan),White=new Vector3(1,1,1),ColdWhite=Lin(NativeIronworksKinds.ColdWhite),
            Ember=new Vector3(3.2f,2.5f,1.6f),Hot=new Vector3(2.4f,1.25f,.42f),Spark=new Vector3(3.4f,2.6f,1.5f),SmokeDark=new Vector3(.035f,.03f,.028f),Ash=new Vector3(.09f,.08f,.075f),
            Slag=Lin(0x141110),SlagGlow=new Vector3(.5f,.12f,.03f),Steel=Lin(0x5a5751);
        /// <summary>The molten pool's brightest (HDR): under the cream decals' full white (readability rule 4).</summary>
        public const float PoolMost=1.3f;
        public const int MaxPerAdd=160;
        private static Vector3 Lin(int hex)=>WeaponPaint.Linear(hex);
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float H(uint seed,int i,int k)=>NativeHordeFx.Hash(seed,i,k);
        private static Vector3 Fwd(float yaw){float r=yaw*(float)Math.PI/180;return new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));}
        private static Vector3 Side(Vector3 f)=>new Vector3(f.Z,0,-f.X);
        private const float Lift=.022f,Tau=(float)(Math.PI*2);

        // ---- shared shapes ----
        /// <summary>A dashed ring on the floor (centre c, radius r, width w): `dashes` dashes of share `fill`, turning by `phase`.</summary>
        public static void DashRing(IronFxBuffer b,Vector3 c,float r,float w,int dashes,float fill,float phase,Vector3 color,float alpha,bool additive=true)
        {
            for(int i=0;i<dashes;i++)
            {
                float a0=Tau*(i+phase)/dashes,a1=a0+Tau*fill/dashes;Vector3 P(float a,float rr)=>c+new Vector3((float)Math.Sin(a)*rr,Lift,(float)Math.Cos(a)*rr);
                b.Decal(P(a0,r-w*.5f),P(a0,r+w*.5f),P(a1,r+w*.5f),P(a1,r-w*.5f),color,alpha,additive);
            }
        }
        /// <summary>A solid ring (or a disc: inner 0) of `segments` quads.</summary>
        public static void Ring(IronFxBuffer b,Vector3 c,float inner,float outer,int segments,Vector3 color,float alpha,bool additive=true,float from=0,float to=Tau)
        {
            for(int i=0;i<segments;i++)
            {
                float a0=from+(to-from)*i/segments,a1=from+(to-from)*(i+1)/segments;Vector3 P(float a,float rr)=>c+new Vector3((float)Math.Sin(a)*rr,Lift,(float)Math.Cos(a)*rr);
                b.Decal(P(a0,inner),P(a0,outer),P(a1,outer),P(a1,inner),color,alpha,additive);
            }
        }
        private static float Ang(Vector3 f)=>(float)Math.Atan2(f.X,f.Z);
        /// <summary>A molten pool's band: a ring whose edges wander (seeded per segment), so a pool reads as poured, not as a disc.</summary>
        private static float Lobe(uint seed,float a,int edge){float p1=Tau*H(seed,edge,142),p2=Tau*H(seed,edge,143);return .5f+.32f*(float)Math.Sin(a*5+p1)+.18f*(float)Math.Sin(a*11+p2);}
        public static void Blob(IronFxBuffer b,Vector3 c,float inner,float outer,int segments,Vector3 color,float alpha,bool additive,float from,float to,uint seed)
        {
            float span=outer-inner;
            for(int i=0;i<segments;i++)
            {
                // a slow, seeded wobble along the edge (a few lobes, not a saw): whole-number frequencies close a full circle
                float a0=from+(to-from)*i/segments,a1=from+(to-from)*(i+1)/segments;float j0=Lobe(seed,a0,0),j1=Lobe(seed,a1,0),k0=Lobe(seed,a0,1),k1=Lobe(seed,a1,1);
                Vector3 P(float a,float rr)=>c+new Vector3((float)Math.Sin(a)*rr,Lift,(float)Math.Cos(a)*rr);
                b.Decal(P(a0,inner+span*.12f*k0),P(a0,outer-span*.18f*j0),P(a1,outer-span*.18f*j1),P(a1,inner+span*.12f*k1),color,alpha,additive);
            }
        }

        /// <summary>Every effect of one add this frame (at most <see cref="MaxPerAdd"/>; `full` false thins the streams).</summary>
        public static void Emit(IronFxBuffer b,in IronFxInput f,bool full)
        {
            int start=b.Count;
            switch(f.Kind)
            {
                case NativeIronworksKinds.Cinderling:Cinderling(b,f,full);break;case NativeIronworksKinds.Stoker:Stoker(b,f,full);break;
                case NativeIronworksKinds.Slagback:Slagback(b,f,full);break;case NativeIronworksKinds.Ladleman:Ladleman(b,f,full);break;case NativeIronworksKinds.Rigger:Rigger(b,f,full);break;
            }
            if(b.Count-start>MaxPerAdd)b.Count=start+MaxPerAdd;
        }

        // ================================================================================================================
        // Cinderling: the coal's halo (flaring white through the lunge's wind-up), the smoke plume off its head, embers off it
        // when it runs, the lunge's white streaks
        // ================================================================================================================
        public static float CoreFlare(float age,float strike){if(float.IsNaN(age)||age<0)return 0;if(age<strike)return Sm(age/Math.Max(.05f,strike*.85f));return 1-Sm((age-strike)/.25f);}
        private static void Cinderling(IronFxBuffer b,in IronFxInput f,bool full)
        {
            var fwd=Fwd(f.Yaw);var side=Side(fwd);float flare=CoreFlare(f.AttackAge,f.Strike),t=f.Time;
            float pulse=.5f+.5f*(float)Math.Sin(t*5.3f+H(f.Seed,1,10)*6);var core=f.Chest+fwd*.12f*f.Scale+new Vector3(0,.02f,0);
            b.Glow(core,(.22f+.06f*pulse+.42f*flare)*f.Scale,Vector3.Lerp(new Vector3(1.6f,1.1f,.6f),White*2.6f,flare),.4f+.5f*flare);
            if(flare>.6f)b.Glow(core,.14f*f.Scale,White*4,flare);
            // The plume: dark smoke streaming up and back off the skull (rising, spreading, thinning).
            int puffs=full?6:3;for(int i=0;i<puffs;i++)
            {
                float u=((t*.9f+i/(float)puffs+H(f.Seed,i,11))%1+1)%1;var at=f.Head+new Vector3(0,.2f+u*1.4f,0)-fwd*(u*(.4f+f.Speed*.18f))+side*((H(f.Seed,i,12)-.5f)*.3f*u);
                b.Smoke(at,(.22f+.5f*u)*f.Scale,SmokeDark,.55f*(1-u)*Sm(u/.15f),f.Seed+i);
            }
            // Embers shed when it runs (and in the lunge): small hot streaks lifting off its back.
            int embers=(int)(Math.Min(1,f.Speed/3.5f+flare)*(full?8:4));for(int i=0;i<embers;i++)
            {
                float u=((t*1.7f+i*.137f+H(f.Seed,i,13))%1+1)%1;var at=f.Chest+new Vector3((H(f.Seed,i,14)-.5f)*.35f,(H(f.Seed,i,15)-.3f)*.6f+u*.7f,(H(f.Seed,i,16)-.5f)*.25f)-fwd*u*(.3f+f.Speed*.25f);
                b.Streak(at,at+new Vector3(0,.07f,0)-fwd*.05f,.018f,Ember,(1-u)*.9f);
            }
            // The lunge: white streaks off the body at the strike.
            if(!float.IsNaN(f.AttackAge)&&f.AttackAge>=f.Strike-.03f&&f.AttackAge<f.Strike+.14f)
            {
                float k=1-Math.Abs(f.AttackAge-f.Strike)/.14f;for(int i=0;i<(full?6:3);i++){var at=f.Chest+side*((H(f.Seed,i,17)-.5f)*.5f)+new Vector3(0,(H(f.Seed,i,18)-.5f)*.5f,0);b.Streak(at,at-fwd*(.5f+.4f*H(f.Seed,i,19)),.025f,White*2.2f,.8f*k);}
            }
        }
        /// <summary>The cinderling's death `since` s after it (the fuse's strobe, the pop's flash, embers, hot cinders and the
        /// scorch on a melee kill; a crumble of ash otherwise).</summary>
        public static void CinderlingDeath(IronFxBuffer b,Vector3 chest,Vector3 floor,float since,bool pops,uint seed,bool full)
        {
            if(since<0)return;
            if(!pops){for(int i=0;i<(full?8:4);i++){float u=Clamp01(since/1.4f);var at=chest+new Vector3((H(seed,i,30)-.5f)*.4f,-u*.6f+.2f*H(seed,i,31),(H(seed,i,32)-.5f)*.4f);b.Smoke(at,.25f+.3f*u,Ash,.5f*(1-u),seed+(uint)i);}return;}
            float fuse=NativeIronworksKinds.PopFuse;
            // The fuse is the pop's telegraph: the core strobes white and the 1.4 m it will burst over rings the floor in cream.
            if(since<fuse){float s=NativeIronworksKinds.Strobe(since),k=Sm(since/.08f);b.Glow(chest,.3f+.5f*s+.3f*since/fuse,White*(1.5f+2.5f*s),.5f+.5f*s);Ring(b,floor,NativeIronworksKinds.PopRadius-.08f,NativeIronworksKinds.PopRadius,full?32:16,Cream*(1.4f+.8f*s),.95f*k);Ring(b,floor,0,NativeIronworksKinds.PopRadius-.08f,full?16:8,Cream,.05f+.07f*s,true);return;}
            float a=since-fuse,R=NativeIronworksKinds.PopRadius;
            // The flash and the fireball's core, then embers to the pop's radius, hot cinders bouncing, a scorch ring.
            if(a<.18f){float u=a/.18f;b.Glow(chest,.6f+2.4f*u,White*4,1-u);b.Glow(chest,1.2f+2f*u,new Vector3(2.6f,1.6f,.6f),.8f*(1-u));}
            int n=full?22:11;for(int i=0;i<n;i++)
            {
                float life=.55f+.4f*H(seed,i,33);if(a>life)continue;float u=a/life;double az=Tau*H(seed,i,34),el=.15+.9*H(seed,i,35);
                var dir=new Vector3((float)(Math.Cos(az)*Math.Cos(el)),(float)Math.Sin(el),(float)(Math.Sin(az)*Math.Cos(el)));float r=R*(.4f+.6f*H(seed,i,36))*(1-(1-u)*(1-u));
                var at=chest+dir*r+new Vector3(0,-2.2f*a*a,0);if(at.Y<floor.Y+.02f)at.Y=floor.Y+.02f;b.Streak(at,at-dir*.12f*(1-u),.022f,Ember*(1.2f-u),1-u);
            }
            for(int i=0;i<(full?6:3);i++){float u=Clamp01(a/1.6f);double az=Tau*H(seed,i,37);float r=R*.8f*(1-(1-u)*(1-u))*(.5f+.5f*H(seed,i,38));var at=floor+new Vector3((float)Math.Cos(az)*r,.04f+.4f*(float)Math.Sin(Math.PI*Math.Min(1,u*2))*(1-u),(float)Math.Sin(az)*r);
                b.Chunk(at,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,H(seed,i,39),.3f)),a*9),.05f+.04f*H(seed,i,40),Slag,Hot*(1-u)*1.4f,seed+i);}
            if(a<3){float k=a<.12f?a/.12f:1-Sm((a-1.6f)/1.4f);Blob(b,floor,0,R,full?20:12,new Vector3(.03f,.025f,.02f),.55f*k,false,0,Tau,seed);}
        }

        // ================================================================================================================
        // Stoker: the visor's cyan slit, the sweep's cream arc and pale trail, the pull's cyan dashed line, the hook's glint and
        // spark trail
        // ================================================================================================================
        private static void Stoker(IronFxBuffer b,in IronFxInput f,bool full)
        {
            var fwd=Fwd(f.Yaw);var side=Side(fwd);float t=f.Time;var v=f.View;
            b.Glow(f.Head+fwd*.17f*f.Scale+new Vector3(0,.2f*f.Scale,0),.12f*f.Scale,Cyan*2.2f,.45f);
            // The sweep (LastAttack + .5): a cream arc on the floor in front through the wind-up, a pale trail at the strike.
            if(!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Recover)
            {
                float a=f.AttackAge,H0=f.Strike;var c=f.Floor;float ang=Ang(fwd),arc=1.9f,R=NativeIronworksKinds.SweepArc;
                if(a<H0){float k=Sm(a/(H0*.6f));Ring(b,c,R-.06f,R,full?20:10,Cream*1.5f,.85f*k,true,ang-arc*.5f,ang+arc*.5f);Ring(b,c,.6f,R-.06f,full?14:7,Cream,.03f+.06f*k*k,true,ang-arc*.5f,ang+arc*.5f);}
                if(a>=H0-.05f&&a<H0+.2f){float k=1-Math.Abs(a-H0)/.2f;for(int i=0;i<(full?10:5);i++){float u0=i/10f,u1=(i+.8f)/10f;float a0=ang+arc*.5f-arc*u0,a1=ang+arc*.5f-arc*u1;
                    b.Streak(c+new Vector3((float)Math.Sin(a0)*R*.92f,.35f,(float)Math.Cos(a0)*R*.92f),c+new Vector3((float)Math.Sin(a1)*R*.92f,.32f,(float)Math.Cos(a1)*R*.92f),.05f*(1-u0*.6f),White*1.2f,.55f*k*(1-u0*.5f));}}
            }
            // The pull: the cyan dashed line to the target through the wind-up (the dashes crawl toward the Stoker), the hook's
            // white glint at the rake's end; at the throw the hook's spark trail along the line.
            var ph=NativeIronworksKinds.Pull(v.pullAt,t,v.pullTarget>=0,out float into);
            if(ph==NativeIronworksKinds.PullPhase.Windup||ph==NativeIronworksKinds.PullPhase.Throw)
            {
                float k=ph==NativeIronworksKinds.PullPhase.Windup?Sm(into/.2f):1-Sm(into/NativeIronworksKinds.HookFlight);var o=f.Floor+fwd*.45f;float L=NativeIronworksKinds.PullRange,W=NativeIronworksKinds.PullWidth;
                int dashes=9;float crawl=(t*2.2f)%1;for(int i=0;i<dashes;i++){float u0=(i+crawl)/dashes,u1=u0+.55f/dashes;if(u1>1)continue;var p0=o+fwd*(L*u0)+new Vector3(0,Lift,0);var p1=o+fwd*(L*u1)+new Vector3(0,Lift,0);
                    b.Decal(p0-side*W*.5f,p0+side*W*.5f,p1+side*W*.5f,p1-side*W*.5f,Cyan*1.4f,.85f*k,true);}
                b.Decal(o-side*.03f+new Vector3(0,Lift,0),o+side*.03f+new Vector3(0,Lift,0),o+fwd*L+side*.03f+new Vector3(0,Lift,0),o+fwd*L-side*.03f+new Vector3(0,Lift,0),Cyan,.35f*k,true);
                float glint=ph==NativeIronworksKinds.PullPhase.Windup?Sm((into-.2f)/.3f)*(.7f+.3f*(float)Math.Sin(into*40)):1;b.Glow(f.Hook,.5f*glint,White*3,.9f*glint);
            }
            if(ph==NativeIronworksKinds.PullPhase.Throw||ph==NativeIronworksKinds.PullPhase.Drag)
            {
                for(int i=0;i<(full?7:4);i++){var at=f.Hook-fwd*(.15f+.25f*i)+new Vector3((H(f.Seed,i,50)-.5f)*.1f,(H(f.Seed,i,51)-.5f)*.1f,0);b.Streak(at,at-fwd*.2f,.02f,Spark,.8f*(1-i/8f));}
                if(ph==NativeIronworksKinds.PullPhase.Drag&&f.HasTarget)for(int i=0;i<(full?6:3);i++){var at=new Vector3(f.Target.X,f.Floor.Y+.05f,f.Target.Z)+side*((H(f.Seed,i,52)-.5f)*.4f);b.Streak(at,at+fwd*.25f+new Vector3(0,.12f,0),.02f,Spark,.7f);}
            }
        }

        // ================================================================================================================
        // Slagback: the fissure's glow on its back, the slam's cream dashed ring (1.6 m disc 1.2 m ahead; the strike's impact is
        // the zombie impact's), the crust breaking off, the deflect's sparks (DeflectSparks)
        // ================================================================================================================
        private static void Slagback(IronFxBuffer b,in IronFxInput f,bool full)
        {
            var fwd=Fwd(f.Yaw);float t=f.Time;var v=f.View;
            b.Glow(f.Chest-fwd*.24f*f.Scale+new Vector3(0,.12f,0),.45f*f.Scale,Lin(NativeIronworksKinds.Fissure)*1.6f,.32f+.08f*(float)Math.Sin(t*3.1f));
            if(!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Strike)
            {
                float a=f.AttackAge,H0=f.Strike,R=NativeHordeKinds.SmashRadius;var c=f.Floor+fwd*NativeHordeKinds.SmashAhead;float k=Sm(a/(H0*.4f));
                DashRing(b,c,R-.06f,.12f,(int)NativeIronworksKinds.SlamRingDashes,.55f,t*.3f,Cream*1.3f,.95f*k);
                Ring(b,c,0,R*Sm(a/H0),full?24:12,Cream,.04f+.08f*Sm(a/H0),true);
                // grit off its fists as it heaves them up (the crust grinding)
                for(int i=0;i<(full?5:2);i++){float u=((a*2.5f+i*.21f)%1);var at=Vector3.Lerp(f.HandL,f.HandR,H(f.Seed,i,60))+new Vector3(0,-u*.9f,0);b.Chunk(at,Quaternion.CreateFromAxisAngle(Vector3.UnitY,u*6),.025f,Slag,SlagGlow*.6f,f.Seed+i);}
            }
            // the slam (over the zombie impact's cracks and dust, which the runtime adds in fissure ink): slag chips knocked off its
            // fists, white-gold sparks out of the fissure's jolt, a low dust ring
            if(!float.IsNaN(f.AttackAge)&&f.AttackAge>=f.Strike&&f.AttackAge<f.Strike+1.2f)SlamBurst(b,f.Floor+fwd*NativeHordeKinds.SmashAhead,f.AttackAge-f.Strike,f.Seed,full);
            float shed=float.IsNaN(v.shedAt)?-1:t-v.shedAt;if(shed>=0&&shed<1.8f)CrustShed(b,f.Chest,f.Floor,fwd,shed,f.Seed,full);
        }
        /// <summary>The slam's strike `since` s ago at `c` on the floor: slag chips thrown out and bouncing to rest, sparks, dust.</summary>
        public static void SlamBurst(IronFxBuffer b,Vector3 c,float since,uint seed,bool full)
        {
            int n=full?10:5;for(int i=0;i<n;i++)
            {
                double az=Tau*(i+.6f*H(seed,i,90))/n;float sp=1.4f+1.6f*H(seed,i,91),up=1.6f+1.8f*H(seed,i,92);var dir=new Vector3((float)Math.Cos(az),0,(float)Math.Sin(az));
                float land=up/4.9f,k=Math.Min(since,land);var at=c+dir*(.2f+sp*k);at.Y=since<land?c.Y+.05f+up*since-4.9f*since*since:c.Y+.03f;
                b.Chunk(at,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(seed,i,93)-.5f,1,H(seed,i,94)-.5f)),k*9),.035f+.04f*H(seed,i,95),Slag,SlagGlow*(1-Clamp01(since/.9f)),seed+(uint)(i+40));
            }
            if(since<.12f)b.Glow(c+new Vector3(0,.2f,0),.9f*(1-since/.12f),Lin(NativeIronworksKinds.Fissure)*2.2f,1-since/.12f);
            if(since<.35f)for(int i=0;i<(full?10:5);i++){double az=Tau*H(seed,i,96);var dir=Vector3.Normalize(new Vector3((float)Math.Cos(az),.35f+.5f*H(seed,i,97),(float)Math.Sin(az)));var at=c+new Vector3(0,.1f,0)+dir*(.2f+since*(4+2*H(seed,i,98)));b.Streak(at,at-dir*.16f,.016f,Spark,1-since/.35f);}
            if(since<.9f)for(int i=0;i<(full?8:4);i++){float u=since/.9f;double az=Tau*(i+.5f)/(full?8:4);var at=c+new Vector3((float)Math.Cos(az)*(.5f+1.1f*u),.12f+.25f*u,(float)Math.Sin(az)*(.5f+1.1f*u));b.Smoke(at,.35f+.55f*u,Ash,.45f*(1-u),seed+(uint)(i+60));}
        }
        /// <summary>The crust breaking off `since` s after the shed: glassy black shards thrown off the body, glowing dim at their
        /// broken edges, bouncing to the floor; grit and a puff of dust.</summary>
        public static void CrustShed(IronFxBuffer b,Vector3 chest,Vector3 floor,Vector3 fwd,float since,uint seed,bool full)
        {
            int n=full?14:7;for(int i=0;i<n;i++)
            {
                float d=.05f*H(seed,i,70),a=since-d;if(a<0)continue;double az=Tau*H(seed,i,71);float sp=1.6f+1.8f*H(seed,i,72),up=1.8f+1.6f*H(seed,i,73);
                var dir=new Vector3((float)Math.Cos(az),0,(float)Math.Sin(az));var start=chest+dir*.25f+new Vector3(0,(H(seed,i,74)-.4f)*.6f,0);
                float y=start.Y+up*a-4.9f*a*a;float land=(up+(float)Math.Sqrt(up*up+19.6f*Math.Max(0,start.Y-floor.Y)))/9.8f;float k=Math.Min(a,land);var at=start+dir*sp*k;at.Y=a<land?y:floor.Y+.03f;
                float glow=1-Clamp01(a/1.2f);b.Chunk(at,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(H(seed,i,75)-.5f,1,H(seed,i,76)-.5f)),k*11),.06f+.06f*H(seed,i,77),Slag,SlagGlow*glow,seed+(uint)i);
            }
            if(since<.6f)for(int i=0;i<(full?6:3);i++){float u=since/.6f;double az=Tau*H(seed,i,78);var at=floor+new Vector3((float)Math.Cos(az)*(.4f+u),.15f+.3f*u,(float)Math.Sin(az)*(.4f+u));b.Smoke(at,.4f+.6f*u,Ash,.5f*(1-u),seed+(uint)i);}
            if(since<.3f)for(int i=0;i<(full?8:4);i++){double az=Tau*H(seed,i,79);var dir=new Vector3((float)Math.Cos(az),.5f,(float)Math.Sin(az));var at=chest+dir*(.3f+since*3);b.Streak(at,at-dir*.18f,.02f,Spark,1-since/.3f);}
        }
        /// <summary>A shot glancing off the crust (a frontal hit while crusted): a spray of sparks off the hit point.</summary>
        public static void DeflectSparks(IronFxBuffer b,Vector3 at,Vector3 normal,float since,uint seed,bool full)
        {
            if(since<0||since>.22f)return;float u=since/.22f;if(u<.4f)b.Glow(at,.25f*(1-u),White*3,1-u*2);
            for(int i=0;i<(full?7:4);i++){var dir=Vector3.Normalize(normal+new Vector3(H(seed,i,80)-.5f,H(seed,i,81)-.2f,H(seed,i,82)-.5f)*1.4f);var p=at+dir*(.1f+u*.7f*(.6f+.4f*H(seed,i,83)))+new Vector3(0,-u*u*.4f,0);b.Streak(p,p-dir*.1f,.014f,Spark,1-u);}
        }

        // ================================================================================================================
        // Ladleman: the bowl's glow (the brightest thing on any enemy), the pour's cream arc and bubbling, the stream, its splash
        // and the burning pool; the swing's crescent; the spill
        // ================================================================================================================
        private static void Ladleman(IronFxBuffer b,in IronFxInput f,bool full)
        {
            var fwd=Fwd(f.Yaw);var side=Side(fwd);float t=f.Time;var v=f.View;bool full_=NativeIronworksKinds.Carrying(v.spilledAt,t);
            if(full_){b.Glow(f.Lip+new Vector3(0,.22f,0)-fwd*.3f*f.Scale,1.1f*f.Scale,new Vector3(1.5f,.75f,.25f),.42f);for(int i=0;i<(full?3:1);i++){float u=((t*.6f+i/3f+H(f.Seed,i,90))%1);var at=f.Lip-fwd*.3f*f.Scale+new Vector3((H(f.Seed,i,91)-.5f)*.3f,.25f+u*1.2f,(H(f.Seed,i,92)-.5f)*.3f);b.Smoke(at,.3f+.5f*u,new Vector3(.12f,.1f,.09f),.25f*(1-u),f.Seed+(uint)i);}}
            b.Glow(f.Head+fwd*.15f*f.Scale+new Vector3(0,.13f*f.Scale,0),.1f*f.Scale,Cyan*2,.4f);
            // The pour: the arc (60 degrees, 4.6 m) in cream through the wind-up, the metal bubbling white; then the stream and pool.
            float yaw=float.IsNaN(v.pourAt)?f.Yaw:v.pourYaw;var pf=Fwd(yaw);var ph=NativeIronworksKinds.Pour(v.pourAt,t,out float into);float ang=Ang(pf),arc=NativeIronworksKinds.PourArc*(float)Math.PI/180,R=NativeIronworksKinds.PourRange,R0=NativeIronworksKinds.PourInner;
            if(ph==NativeIronworksKinds.PourPhase.Raise||ph==NativeIronworksKinds.PourPhase.Pour&&into<.15f)
            {
                float k=ph==NativeIronworksKinds.PourPhase.Raise?Sm(into/.35f):1-into/.15f,fill=ph==NativeIronworksKinds.PourPhase.Raise?Sm(into/NativeIronworksKinds.PourWindup):1;
                Ring(b,f.Floor,R-.08f,R,full?22:11,Cream*1.4f,.95f*k,true,ang-arc*.5f,ang+arc*.5f);Ring(b,f.Floor,R0,R0+.06f,full?10:5,Cream*1.4f,.8f*k,true,ang-arc*.5f,ang+arc*.5f);
                for(int ei=0;ei<2;ei++){float e=ei==0?-1:1;float a=ang+e*arc*.5f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));var s2=new Vector3(d.Z,0,-d.X)*.04f;b.Decal(f.Floor+d*R0+s2+new Vector3(0,Lift,0),f.Floor+d*R+s2+new Vector3(0,Lift,0),f.Floor+d*R-s2+new Vector3(0,Lift,0),f.Floor+d*R0-s2+new Vector3(0,Lift,0),Cream*1.4f,.9f*k,true);}
                Ring(b,f.Floor,R0,R0+(R-R0)*fill,full?22:11,Cream,.03f+.08f*fill,true,ang-arc*.5f,ang+arc*.5f);
                for(int i=0;i<(full?5:2);i++){float u=((into*3+i*.29f+H(f.Seed,i,93))%1);b.Glow(f.Lip-fwd*.3f*f.Scale+new Vector3((H(f.Seed,i,94)-.5f)*.4f,.22f+.1f*u,(H(f.Seed,i,95)-.5f)*.3f),.12f+.1f*u,White*2.6f,(1-u));}
            }
            float sweep=NativeIronworksKinds.Sweep(v.pourAt,t);
            if(!float.IsNaN(sweep))
            {
                // The stream: from the lip, arcing down to where it lands across the arc; white-hot at the lip, orange where it falls.
                float a=ang+sweep*arc*.5f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));var land=f.Floor+d*(R0+(R-R0)*(.45f+.25f*(float)Math.Sin(t*7)))+new Vector3(0,.02f,0);
                var lip=f.Lip;int seg=full?9:5;var prev=lip;for(int i=1;i<=seg;i++){float u=i/(float)seg;var p=Vector3.Lerp(lip,land,u)+new Vector3(0,(float)Math.Sin(u*Math.PI)*.35f-u*u*.1f,0);b.Streak(prev,p,.1f-.03f*u,Vector3.Lerp(new Vector3(3.4f,2.6f,1.3f),new Vector3(2.6f,1.1f,.25f),u),.95f);prev=p;}
                b.Glow(land+new Vector3(0,.15f,0),.9f,new Vector3(2.2f,1.1f,.3f),.7f);
                for(int i=0;i<(full?10:5);i++){float u=((t*3.1f+i*.137f)%1);double az=Tau*H(f.Seed,(int)(t*3.1f)+i,96);var dir=new Vector3((float)Math.Cos(az),1.2f,(float)Math.Sin(az));var p=land+dir*u*.8f+new Vector3(0,-u*u*.9f,0);b.Streak(p,p-dir*.08f,.016f,Spark,1-u);}
            }
            // The pool: the arc's band laid as the stream sweeps, burning 3 s, then a dark crust that fades.
            LingeringPool(b,f.Floor,v.pourAt,yaw,t,f.Seed,full);
            // The swing (LastAttack + .6): a cream crescent on the floor where the ladle will land in the wind-up, a trail through it.
            if(!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Strike+.2f)
            {
                float a=f.AttackAge,H0=f.Strike,k=a<H0?Sm(a/(H0*.6f)):1-(a-H0)/.2f;float r=2.4f;
                Ring(b,f.Floor,r-.08f,r,full?14:7,Cream*1.4f,.85f*k,true,Ang(fwd)-.6f,Ang(fwd)+.6f);
            }
            float spill=float.IsNaN(v.spilledAt)?-1:t-v.spilledAt;if(spill>=0&&spill<NativeIronworksKinds.SpillStun+1)Spill(b,f.Chest,f.Floor,fwd,spill,f.Seed,full);
        }
        /// <summary>The ladle spilled over him `since` s ago: a splash over the body, metal running off it, a short-lived pool round
        /// his knees, steam.</summary>
        // Same laid metal after its owner dies; the authority keeps its original expiry.
        public static void LingeringPool(IronFxBuffer b,Vector3 floor,float pourAt,float yaw,float t,uint seed,bool full)
        {
            float ang=Ang(Fwd(yaw)),arc=NativeIronworksKinds.PourArc*(float)Math.PI/180,R=NativeIronworksKinds.PourRange,R0=NativeIronworksKinds.PourInner;
            if(NativeIronworksKinds.Pool(pourAt,t,out float laid,out float heat,out float alpha))
            {
                int segs=full?16:8;float to=ang-arc*.5f+arc*laid;var c=floor+new Vector3(0,.004f,0);
                // a dark skin, the glowing band (redder at its edges), a hotter core inside it: the two add up to PoolMost at most
                var hot=Vector3.Lerp(new Vector3(.25f,.06f,.02f),new Vector3(PoolMost*.6f,PoolMost*.6f*.38f,PoolMost*.6f*.08f),heat);var core=new Vector3(PoolMost*.4f,PoolMost*.4f*.62f,PoolMost*.4f*.2f)*heat;
                Blob(b,c,R0+.15f,R-.12f,segs,new Vector3(.05f,.03f,.02f),.85f*alpha,false,ang-arc*.5f,to,seed);
                Blob(b,c+new Vector3(0,.002f,0),R0+.3f,R-.25f,segs,hot,alpha,true,ang-arc*.5f,to,seed+7);
                Blob(b,c+new Vector3(0,.003f,0),R0+.65f,R-.7f,segs,core,alpha,true,ang-arc*.5f,to,seed+11);
                if(full&&heat>.5f)for(int i=0;i<4;i++){float u=((t*.8f+i*.25f)%1);float aa=ang-arc*.5f+arc*laid*H(seed,i+(int)(t*.8f)*4,97);float rr=R0+.3f+(R-R0-.6f)*H(seed,i,98);var p=floor+new Vector3((float)Math.Sin(aa)*rr,.05f+u*.8f,(float)Math.Cos(aa)*rr);b.Smoke(p,.4f+.6f*u,new Vector3(.12f,.1f,.09f),.3f*(1-u),seed+(uint)i);}
            }
        }
        public static void Spill(IronFxBuffer b,Vector3 chest,Vector3 floor,Vector3 fwd,float since,uint seed,bool full)
        {
            if(since<.25f){float u=since/.25f;b.Glow(chest+fwd*.3f,1.4f+1.2f*u,new Vector3(3,1.6f,.5f),1-u);}
            for(int i=0;i<(full?14:7);i++){float life=.5f+.4f*H(seed,i,100);if(since>life)continue;float u=since/life;double az=Tau*H(seed,i,101);var dir=new Vector3((float)Math.Cos(az),.8f+H(seed,i,102),(float)Math.Sin(az));var p=chest+fwd*.2f+dir*(.2f+u*1.1f)+new Vector3(0,-2.5f*since*since,0);if(p.Y<floor.Y)p.Y=floor.Y+.02f;b.Streak(p,p-dir*.12f,.03f,Hot*1.4f,1-u);}
            float k=since<.3f?since/.3f:1-Sm((since-1.6f)/1.2f);Blob(b,floor+fwd*.25f,0,1.1f,full?16:8,new Vector3(PoolMost,PoolMost*.45f,PoolMost*.11f),.7f*k*(1-Clamp01(since/2.4f)),true,0,Tau,seed);
            for(int i=0;i<(full?5:2);i++){float u=((since*.7f+i*.2f)%1);var p=chest+new Vector3((H(seed,i,103)-.5f)*.5f,.2f+u*1.5f,(H(seed,i,104)-.5f)*.5f);b.Smoke(p,.5f+.7f*u,new Vector3(.5f,.5f,.5f),.35f*(1-u)*(1-Clamp01(since/2.5f)),seed+(uint)i);}
        }

        // ================================================================================================================
        // Rigger: the lamp's halo and cone, the drop's ring, the hook's swaying shadow, girder dust, glove sparks, the landing dust,
        // the swing's rings and trail, the hook's drag sparks
        // ================================================================================================================
        private static void Rigger(IronFxBuffer b,in IronFxInput f,bool full)
        {
            var fwd=Fwd(f.Yaw);var side=Side(fwd);float t=f.Time;var v=f.View;
            var lamp=f.Head+fwd*.17f*f.Scale+new Vector3(0,.34f*f.Scale,0);bool on=NativeIronworksKinds.Lamp(v.lockAt,t,f.Hanging);
            if(on)b.Glow(lamp,f.Hanging?.34f:.2f,ColdWhite*(f.Hanging?2.4f:1.7f),.85f);
            // The hook drop (lockAt): girder dust at the rattle, the ring and the lamp's cone on it, the hook's shadow swinging
            // across it, the white flash at the slide, sparks at the gloves.
            var ph=NativeIronworksKinds.Drop(v.lockAt,t,out float into);var spot=new Vector3(v.lockX,f.Landing.Y,v.lockZ);
            if(ph==NativeIronworksKinds.DropPhase.Rattle||ph==NativeIronworksKinds.DropPhase.Lowered&&into<.6f)
            {
                float since=ph==NativeIronworksKinds.DropPhase.Rattle?into:into+NativeIronworksKinds.DropRattle;
                for(int i=0;i<(full?10:5);i++){float u=((since*.8f+i*.1f)%1);var p=f.Anchor+new Vector3((H(f.Seed,i,110)-.5f)*1.6f,-u*2.6f,(H(f.Seed,i,111)-.5f)*.6f);b.Smoke(p,.15f+.25f*u,new Vector3(.32f,.3f,.28f),.3f*(1-u),f.Seed+(uint)i);}
            }
            if(ph==NativeIronworksKinds.DropPhase.Lowered||ph==NativeIronworksKinds.DropPhase.Slide||ph==NativeIronworksKinds.DropPhase.Land)
            {
                float ring=NativeIronworksKinds.Ring(v.lockAt,t,out float flash);float R=NativeIronworksKinds.DropRadius;
                Ring(b,spot,R-.09f,R,full?40:20,Vector3.Lerp(Cream*1.4f,White*3,flash),ring,true);Ring(b,spot,0,R-.09f,full?24:12,Cream,.04f*ring+.25f*flash,true);
                if(ph!=NativeIronworksKinds.DropPhase.Land)
                {
                    // the lamp's cold pool of light on the ring, the hook's shadow swinging across it, the lamp catching the hook
                    Ring(b,spot-new Vector3(0,Lift*.5f,0),0,R*1.12f,full?24:12,ColdWhite*.35f,.5f*ring,true);
                    float sway=NativeIronworksKinds.Sway(Math.Max(0,t-v.lockAt),f.Seed);Blob(b,spot+side*sway+new Vector3(0,Lift*.5f,0),0,.3f,full?12:6,new Vector3(0,0,0),.6f*ring,false,0,Tau,f.Seed+3);
                    b.Glow(f.Hook,.16f,ColdWhite*2.2f,.8f*ring);
                }
                if(ph==NativeIronworksKinds.DropPhase.Slide)for(int i=0;i<(full?8:4);i++){var p=Vector3.Lerp(f.HandL,f.HandR,H(f.Seed,i,112))+new Vector3((H(f.Seed,i,113)-.5f)*.12f,0,(H(f.Seed,i,114)-.5f)*.12f);b.Streak(p,p+new Vector3(0,.35f+.3f*H(f.Seed,i,115),0),.016f,Spark,.9f);}
            }
            // The swing (LastAttack + .6): a cream dashed ring at 3.2 m and a cold inner ring at 1 m through the wind-up, a pale
            // trail along the hook's arc in the sweep, sparks where the hook passes.
            if(!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Recover)
            {
                float a=f.AttackAge;var c=f.Floor;
                if(a<NativeIronworksKinds.SwingWindup+.1f){float k=a<NativeIronworksKinds.SwingWindup?Sm(a/.2f):1-(a-NativeIronworksKinds.SwingWindup)/.1f;DashRing(b,c,NativeIronworksKinds.SwingRadius,.14f,36,.55f,-a*.8f,Cream*1.4f,.95f*k);Ring(b,c,NativeIronworksKinds.SwingInner-.04f,NativeIronworksKinds.SwingInner+.04f,full?24:12,ColdWhite*1.6f,.7f*k);}
                NativeIronworksKinds.Swing(a,out float ang,out float rad,out bool sweeping);
                if(sweeping){float base_=Ang(fwd);for(int i=0;i<(full?14:7);i++){float back=i*.11f;float a0=base_+ang+back,a1=a0+.11f;var p0=c+new Vector3((float)Math.Sin(a0)*rad*.97f,1.05f,(float)Math.Cos(a0)*rad*.97f);var p1=c+new Vector3((float)Math.Sin(a1)*rad*.97f,1.05f,(float)Math.Cos(a1)*rad*.97f);b.Streak(p0,p1,.09f*(1-i/14f)+.01f,ColdWhite*1.1f,.5f*(1-i/14f));}
                    for(int i=0;i<(full?4:2);i++){var p=f.Hook+new Vector3((H(f.Seed,i+(int)(a*30),116)-.5f)*.3f,(H(f.Seed,i,117)-.5f)*.3f,(H(f.Seed,i,118)-.5f)*.3f);b.Streak(p,p+new Vector3(0,-.12f,0),.014f,Spark,.8f);}}
            }
            // The hook dragged behind it: sparks off its point.
            else if(!f.Hanging&&f.Speed>.5f)for(int i=0;i<(full?4:2);i++){float u=((t*4+i*.25f)%1);var p=new Vector3(f.Hook.X,f.Floor.Y+.03f,f.Hook.Z)-fwd*u*.4f+side*(H(f.Seed,i,119)-.5f)*.1f;b.Streak(p,p+new Vector3(0,.08f+.1f*u,0)-fwd*.06f,.012f,Spark,(1-u)*.8f);}
        }
        /// <summary>The Rigger's landing `since` s after it (a light dust ring and sparks off the hook: the zombie landing's impact at
        /// .6 of the brute's, drawn by the presentation) and the chain cut's burst (sparks and hot steel links).</summary>
        public static void ChainSnap(IronFxBuffer b,Vector3 at,float since,uint seed,bool full)
        {
            if(since<0||since>1.2f)return;if(since<.1f)b.Glow(at,.6f*(1-since/.1f),White*3,1);
            for(int i=0;i<(full?10:5);i++){float u=Clamp01(since/.5f);if(u>=1)break;double az=Tau*H(seed,i,120);var dir=new Vector3((float)Math.Cos(az),H(seed,i,121)-.3f,(float)Math.Sin(az));var p=at+dir*u*1.2f+new Vector3(0,-u*u*.6f,0);b.Streak(p,p-dir*.12f,.014f,Spark,1-u);}
            for(int i=0;i<(full?5:3);i++){float u=Clamp01(since/1.2f);double az=Tau*H(seed,i,122);var p=at+new Vector3((float)Math.Cos(az)*u*.8f,-4.9f*since*since,(float)Math.Sin(az)*u*.8f);b.Chunk(p,Quaternion.CreateFromAxisAngle(Vector3.UnitX,since*14),.04f,Steel,Hot*(1-u),seed+(uint)i);}
        }
    }
}

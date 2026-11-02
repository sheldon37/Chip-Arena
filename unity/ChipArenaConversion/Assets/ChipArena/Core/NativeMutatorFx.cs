using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition mutators' world looks (Claude, 2026-11-02; docs/EXPEDITION-DEPLOYMENT.md "Mutator looks"), from Astra's
    /// replicated Impact events (Detail mutator-*, authoritative Position and Time, Radius, Amount):
    ///  - STORM's warning: an amber floor disc over the strike's radius that pulses ever faster while a red-orange fill grows
    ///    from its centre to its edge, and a thin targeting column from the sky, brightening; the last .2 s strobe red. It is
    ///    never thinned by the effects quality (the danger language of NativeStationBossFx: a warning is amber and pulses, the
    ///    strike flashes red).
    ///  - STORM's strike: a jagged bolt from <see cref="BoltHeight"/> m to the floor (a white core in a violet glow, re-striking
    ///    once; one steady fade with reduced effects), a floor flash, and a scorch that fades over <see cref="ScorchSeconds"/> s.
    ///    The shared blast sim adds the Shock blast (<see cref="Blast"/>).
    ///  - BLOATED's burst: the bloater's own gas (NativeHordeFx.Burst), spread to the burst's radius; HIGH EXPLOSIVE's and
    ///    MARTYR's blasts are the shared blast sim's (<see cref="Blast"/>), MARTYR with a floor flash.
    ///  - ROULETTE's weapon change: Chaos-violet card glints that spiral up round the pilot.
    /// Every record keeps the event's own position and time, so host, guests and late joiners agree; a record past its life is
    /// dropped. Pure and allocation free: the same records, clock and camera give the same quads.
    /// </summary>
    public sealed class NativeMutatorFx
    {
        public const string StormWarning="mutator-storm-warning",Storm="mutator-storm",Explosive="mutator-explosive",Martyr="mutator-martyr",Bloated="mutator-bloated",Roulette="mutator-roulette";
        /// <summary>An event that is a mutator's (its look is here, not event-vfx.ts's generic spark burst).</summary>
        public static bool Event(string detail)=>detail!=null&&detail.StartsWith("mutator-",StringComparison.Ordinal);
        public const int WarnHex=0xFFB020,FillHex=0xFF6A1A,DangerHex=0xFF2A1A,BoltHex=0xFFFFFF,GlowHex=0xA070FF,FlashHex=0xF4ECFF,ScorchHex=0x0E0B09,MartyrHex=0xFFD36A;
        /// <summary>STORM's warning when the event carries no duration; how long a warning stays past its strike time when no
        /// strike event takes it (a strike the area left, or a lost event).</summary>
        public const double WarnSeconds=2,WarnLinger=.3;
        public const double StrikeSeconds=.5,ScorchSeconds=4,FlashSeconds=.32,BurstSeconds=1.4,SpinSeconds=.8;
        public const double BoltHeight=38,ColumnHeight=30,FloorLift=.04;
        public const int BoltSegments=14,Branches=2,BranchSegments=4,DiscWedges=28,Glints=8;
        public const int MaxWarnings=32,MaxStrikes=16,MaxScorches=24,MaxBursts=24,MaxSpins=8,MaxFlashes=12,Capacity=3200;
        /// <summary>STORM's strike: a white core, warm hot and violet cool (Lightning keeps its sky bolt: no blue or cyan).</summary>
        public static readonly NativeBlastTint StormTint=new NativeBlastTint(0xffffff,0xfff3c4,0xa070ff);
        /// <summary>MARTYR: a gold-white nova that burns out red.</summary>
        public static readonly NativeBlastTint MartyrTint=new NativeBlastTint(0xffffff,0xffd36a,0xff4a2a);
        /// <summary>
        /// The shared blast sim's blast for an event's detail (NativeBlastPresentation), at the event's radius: the storm's Shock,
        /// HIGH EXPLOSIVE's small fire (at a third of the effects quality: a machine gun lands one a shot) and MARTYR's nova.
        /// BLOATED has none: its burst is the bloater's gas. False for none.
        /// </summary>
        public static bool Blast(string detail,out NativeBlastStyle style,out NativeBlastTint? tint,out double share)
        {
            style=NativeBlastStyle.Fire;tint=null;share=1;
            switch(detail)
            {
                case Storm:style=NativeBlastStyle.Shock;tint=StormTint;return true;
                case Explosive:style=NativeBlastStyle.Fire;share=.35;return true;
                case Martyr:style=NativeBlastStyle.Nova;tint=MartyrTint;return true;
                default:return false;
            }
        }

        private struct Mark{public Vector3 At;public double Born,Due,Radius;public int Seed;}
        private readonly Mark[] warnings=new Mark[MaxWarnings],strikes=new Mark[MaxStrikes],scorches=new Mark[MaxScorches],bursts=new Mark[MaxBursts],spins=new Mark[MaxSpins],flashes=new Mark[MaxFlashes];
        private int warningCount,strikeCount,scorchCount,burstCount,spinCount,flashCount;
        public readonly NativeFxQuads Quads=new NativeFxQuads(Capacity);
        /// <summary>What the last Build drew (checks and budget lines).</summary>
        public int Telegraphs,Bolts,Scorches,BurstsDrawn,Spins;
        /// <summary>FNV-1a over the telegraph quads (the checks pin them to the warnings drawn).</summary>
        public uint TelegraphHash;
        public int Warnings=>warningCount;public int Strikes=>strikeCount;

        public void Clear(){warningCount=strikeCount=scorchCount=burstCount=spinCount=flashCount=0;Quads.Clear();Telegraphs=Bolts=Scorches=BurstsDrawn=Spins=0;}
        /// <summary>
        /// Records one fresh mutator event: `at` its position with y on the floor under it (the presentation casts it), `time` its
        /// authoritative time, `amount` and `radius` its fields, `seed` its id. Events this look has nothing for are ignored.
        /// </summary>
        public void Add(string detail,Vector3 at,double time,double amount,double radius,int seed)
        {
            switch(detail)
            {
                case StormWarning:Push(warnings,ref warningCount,new Mark{At=at,Born=time,Due=time+(amount>0?amount:WarnSeconds),Radius=radius>0?radius:3,Seed=seed});break;
                case Storm:
                {
                    // The strike takes its warning (the same spot): its fill has reached the edge.
                    for(int i=warningCount-1;i>=0;i--)if(Vector2.DistanceSquared(new Vector2(warnings[i].At.X,warnings[i].At.Z),new Vector2(at.X,at.Z))<.25f*.25f){warnings[i]=warnings[--warningCount];break;}
                    var m=new Mark{At=at,Born=time,Due=time+StrikeSeconds,Radius=radius>0?radius:3,Seed=seed};Push(strikes,ref strikeCount,m);
                    m.Due=time+ScorchSeconds;Push(scorches,ref scorchCount,m);m.Due=time+FlashSeconds;Push(flashes,ref flashCount,m);break;
                }
                case Bloated:Push(bursts,ref burstCount,new Mark{At=at,Born=time,Due=time+BurstSeconds,Radius=radius>0?radius:3,Seed=seed});break;
                case Martyr:Push(flashes,ref flashCount,new Mark{At=at,Born=time,Due=time+FlashSeconds*1.5,Radius=radius>0?radius:4,Seed=seed});break;
                case Roulette:Push(spins,ref spinCount,new Mark{At=at,Born=time,Due=time+SpinSeconds,Radius=.9,Seed=seed});break;
            }
        }
        // A full list drops its oldest.
        private static void Push(Mark[] list,ref int count,Mark m)
        {
            if(count==list.Length){int old=0;for(int i=1;i<count;i++)if(list[i].Born<list[old].Born)old=i;list[old]=m;return;}
            list[count++]=m;
        }
        private static void Prune(Mark[] list,ref int count,double now,double linger){for(int i=count-1;i>=0;i--)if(now>list[i].Due+linger||now<list[i].Born-1)list[i]=list[--count];}
        /// <summary>How long a fresh event of this detail is worth recording: one older than this has already played out.</summary>
        public static double Life(string detail,double amount)
        =>detail==StormWarning?(amount>0?amount:WarnSeconds)+WarnLinger:detail==Storm?ScorchSeconds:detail==Bloated?BurstSeconds:detail==Martyr?FlashSeconds*1.5:detail==Roulette?SpinSeconds:0;

        /// <summary>
        /// Fills <see cref="Quads"/> for `now` (the presentation clock the events' times are on) from the camera's position, right
        /// and up. `reduced` (the reduced-effects setting): the bolt fades once instead of re-striking and the warning's last strobe
        /// is a steady red. `quality` thins only the scorches and glints, never a warning.
        /// </summary>
        public void Build(double now,Vector3 eye,Vector3 right,Vector3 up,bool reduced,double quality)
        {
            Quads.Clear();Telegraphs=Bolts=Scorches=BurstsDrawn=Spins=0;TelegraphHash=2166136261u;
            Prune(warnings,ref warningCount,now,WarnLinger);Prune(strikes,ref strikeCount,now,0);Prune(scorches,ref scorchCount,now,0);
            Prune(bursts,ref burstCount,now,0);Prune(spins,ref spinCount,now,0);Prune(flashes,ref flashCount,now,0);
            // Premultiplied first (scorches, then the warnings' discs), additive after.
            for(int i=0;i<scorchCount;i++)Scorch(scorches[i],now,eye);
            int from=Quads.Count;for(int i=0;i<warningCount;i++)Telegraph(warnings[i],now,eye,right,reduced);TelegraphHash=Hash(from,Quads.Count,TelegraphHash);
            for(int i=0;i<flashCount;i++)Flash(flashes[i],now);
            for(int i=0;i<strikeCount;i++)Bolt(strikes[i],now,eye,reduced);
            for(int i=0;i<burstCount;i++)Burst(bursts[i],now,right,up,quality);
            for(int i=0;i<spinCount;i++)Spin(spins[i],now,right,up,quality);
        }

        // ---- STORM's warning ----
        /// <summary>The warning's progress at `now`: 0 at its event, 1 at its strike.</summary>
        public static double Progress(double born,double due,double now)=>due>born?Math.Max(0,Math.Min(1,(now-born)/(due-born))):1;
        /// <summary>Its pulse (0..1): 3 Hz rising to 9 Hz toward the strike.</summary>
        public static double Pulse(double age,double p)=>.5+.5*Math.Sin(Math.PI*2*age*(3+6*p*p));
        private void Telegraph(in Mark m,double now,Vector3 eye,Vector3 right,bool reduced)
        {
            double age=now-m.Born,p=Progress(m.Born,m.Due,now),pulse=Pulse(age,p);bool late=now>=m.Due-.2;
            bool strobe=late&&(reduced||((int)Math.Floor((now-m.Due)*15)&1)==0);float R=(float)m.Radius;Vector3 c=m.At+new Vector3(0,(float)FloorLift,0);
            // The area: a flat amber disc, pulsing; the fill: red-orange from the centre to the edge as the strike nears.
            Disc(c,R,strobe?DangerHex:WarnHex,1,.2+.12*pulse);
            if(p>0)Disc(c+new Vector3(0,.01f,0),R*(float)p,strobe?DangerHex:FillHex,1,.28+.1*pulse);
            // A soft glow over it and the targeting column from the sky, brightening.
            Flat(c+new Vector3(0,.02f,0),R*1.25f,strobe?DangerHex:WarnHex,1.2,.35+.3*pulse,true);
            float w=.14f+.1f*(float)p;Column(c,eye,w*3,(float)ColumnHeight,strobe?DangerHex:WarnHex,1,.12+.25*p,true);Column(c,eye,w,(float)ColumnHeight,0xFFF4D8,1,.3+.5*p,true);
            Telegraphs++;
        }
        // A flat disc of DiscWedges wedges (each a quad folded to a triangle).
        private void Disc(Vector3 c,float r,int hex,double gain,double alpha)
        {
            for(int i=0;i<DiscWedges;i++)
            {
                double a0=i*Math.PI*2/DiscWedges,a1=(i+1)*Math.PI*2/DiscWedges;
                double x0=c.X+Math.Cos(a0)*r,z0=c.Z+Math.Sin(a0)*r,x1=c.X+Math.Cos(a1)*r,z1=c.Z+Math.Sin(a1)*r;
                Put(c.X,c.Y,c.Z,x0,c.Y,z0,x1,c.Y,z1,c.X,c.Y,c.Z,hex,gain,alpha,0,false);
            }
        }
        // A flat soft radial sprite on the floor.
        private void Flat(Vector3 c,float r,int hex,double gain,double alpha,bool additive)
        =>Put(c.X-r,c.Y,c.Z-r,c.X+r,c.Y,c.Z-r,c.X+r,c.Y,c.Z+r,c.X-r,c.Y,c.Z+r,hex,gain,alpha,1,additive);
        // A vertical ribbon up from c, turned about its axis to face the eye.
        private void Column(Vector3 c,Vector3 eye,float width,float height,int hex,double gain,double alpha,bool additive)
        {
            var to=new Vector3(eye.X-c.X,0,eye.Z-c.Z);var side=to.LengthSquared()>1e-6f?Vector3.Normalize(new Vector3(to.Z,0,-to.X))*width*.5f:new Vector3(width*.5f,0,0);
            Put(c.X-side.X,c.Y,c.Z-side.Z,c.X+side.X,c.Y,c.Z+side.Z,c.X+side.X,c.Y+height,c.Z+side.Z,c.X-side.X,c.Y+height,c.Z-side.Z,hex,gain,alpha,0,additive);
        }

        // ---- STORM's strike ----
        /// <summary>The bolt's brightness `age` s after the strike: a first stroke, a dark gap, the re-strike, then the fade
        /// (reduced: one steady fade).</summary>
        public static double Stroke(double age,bool reduced)
        {
            if(age<0||age>StrikeSeconds)return 0;if(reduced)return 1-age/StrikeSeconds;
            return age<.07?1:age<.11?.15:age<.2?.95:Math.Max(0,1-(age-.2)/(StrikeSeconds-.2));
        }
        /// <summary>Point k (0 top .. BoltSegments ground) of a strike's bolt: it wanders less as it nears the floor.</summary>
        public static Vector3 BoltPoint(Vector3 floor,int seed,int k)
        {
            if(k>=BoltSegments)return floor;float t=(float)k/BoltSegments,y=(float)BoltHeight*(1-t),reach=k==0?2.5f:1.5f*(1-t*t)+.12f;
            return floor+new Vector3((H(seed,k,1)-.5f)*2*reach,y,(H(seed,k,2)-.5f)*2*reach);
        }
        private void Bolt(in Mark m,double now,Vector3 eye,bool reduced)
        {
            double age=now-m.Born,s=Stroke(age,reduced);if(s<=.01)return;var floor=m.At;
            for(int k=0;k<BoltSegments;k++){var a=BoltPoint(floor,m.Seed,k);var b=BoltPoint(floor,m.Seed,k+1);Ribbon(a,b,eye,.9f,GlowHex,1.4,.55*s,true);Ribbon(a,b,eye,.16f,BoltHex,2,s,true);}
            // Two forks off the upper half.
            for(int f=0;f<Branches;f++)
            {
                int k0=2+(int)(H(m.Seed,f,3)*5);var a=BoltPoint(floor,m.Seed,k0);double ang=H(m.Seed,f,4)*Math.PI*2;
                for(int j=0;j<BranchSegments;j++)
                {
                    float d=1.1f+H(m.Seed,f*8+j,5)*.8f;var b=a+new Vector3((float)Math.Cos(ang)*d,-1.6f-H(m.Seed,f*8+j,6)*1.4f,(float)Math.Sin(ang)*d);
                    double fade=s*(1-j/(double)BranchSegments);Ribbon(a,b,eye,.5f,GlowHex,1.2,.4*fade,true);Ribbon(a,b,eye,.08f,BoltHex,1.6,.8*fade,true);a=b;ang+=(H(m.Seed,f*8+j,7)-.5)*1.2;
                }
            }
            Bolts++;
        }
        private void Flash(in Mark m,double now)
        {
            double life=m.Due-m.Born,u=(now-m.Born)/life;if(u<0||u>1)return;float R=(float)m.Radius;
            Flat(m.At+new Vector3(0,.03f,0),R*(1.4f+.8f*(float)u),m.Radius>3.5?MartyrHex:FlashHex,1.8,1-u,true);
        }
        private void Scorch(in Mark m,double now,Vector3 eye)
        {
            double age=now-m.Born;if(age<0)return;double a=.6*(1-Math.Max(0,Math.Min(1,(age-(ScorchSeconds-1.5))/1.5)));if(a<=.01)return;
            Flat(m.At+new Vector3(0,.015f,0),(float)m.Radius*.42f,ScorchHex,1,a,false);Scorches++;
        }
        // A camera-facing ribbon from a to b.
        private void Ribbon(Vector3 a,Vector3 b,Vector3 eye,float width,int hex,double gain,double alpha,bool additive)
        {
            var d=b-a;var mid=(a+b)*.5f;var side=Vector3.Cross(d,eye-mid);float l=side.Length();if(l<1e-5f)return;side*=width*.5f/l;
            Put(a.X-side.X,a.Y-side.Y,a.Z-side.Z,a.X+side.X,a.Y+side.Y,a.Z+side.Z,b.X+side.X,b.Y+side.Y,b.Z+side.Z,b.X-side.X,b.Y-side.Y,b.Z-side.Z,hex,gain,alpha,0,additive);
        }

        // ---- BLOATED's burst: the bloater's gas, spread to the burst's radius ----
        /// <summary>How far the bloater's own gas reaches (NativeHordeFx.Burst), the scale a BLOATED burst grows it by.</summary>
        public const float BloaterReach=1.6f;
        private void Burst(in Mark m,double now,Vector3 right,Vector3 up,double quality)
        {
            float since=(float)(now-m.Born);if(since<0)return;float k=(float)m.Radius/BloaterReach;uint seed=unchecked((uint)m.Seed*2654435761u);int gas=NativeHordeKinds.Seam[NativeHordeKinds.Bloater];
            int step=quality<.5?2:1;
            for(int i=0;i<NativeHordeFx.BurstPieces;i+=step)if(NativeHordeFx.Burst(seed,i,since,out var o,out float size,out float alpha,out bool isGas)&&isGas)
                Sprite(m.At+new Vector3(o.X*k,o.Y*k*.6f+.4f,o.Z*k),right,up,size*2.2f*k,gas,1,alpha*.8,true);
            if(since<.25f)Sprite(m.At+new Vector3(0,.5f,0),right,up,2.4f*k*(1-since/.25f),gas,1,.8*(1-since/.25f),true);
            BurstsDrawn++;
        }
        // ---- ROULETTE: card glints spiralling up round the pilot ----
        private void Spin(in Mark m,double now,Vector3 right,Vector3 up,double quality)
        {
            double u=(now-m.Born)/SpinSeconds;if(u<0||u>1)return;int color=NativeDeployPresentation.PolarityColor(NativeDeployPresentation.Polarity.Chaos);
            double a=Math.Min(1,u*5)*(1-u*u);int step=quality<.5?2:1;
            for(int i=0;i<Glints;i+=step)
            {
                double ang=i*Math.PI*2/Glints+u*9,r=m.Radius*(1-.45*u);float y=(float)(-.7+u*2.1+(i%2)*.15);
                Sprite(m.At+new Vector3((float)(Math.Cos(ang)*r),y,(float)(Math.Sin(ang)*r)),right,up,.26f,color,1.5,a,true);
            }
            Spins++;
        }
        private void Sprite(Vector3 c,Vector3 right,Vector3 up,float size,int hex,double gain,double alpha,bool additive)
        {
            var r=right*size*.5f;var u=up*size*.5f;
            Put(c.X-r.X-u.X,c.Y-r.Y-u.Y,c.Z-r.Z-u.Z,c.X+r.X-u.X,c.Y+r.Y-u.Y,c.Z+r.Z-u.Z,c.X+r.X+u.X,c.Y+r.Y+u.Y,c.Z+r.Z+u.Z,c.X-r.X+u.X,c.Y-r.Y+u.Y,c.Z-r.Z+u.Z,hex,gain,alpha,1,additive);
        }

        // One quad: the colour linear, premultiplied when not additive (as NativeExpeditionHubGates), on the effects layer.
        private void Put(double ax,double ay,double az,double bx,double by,double bz,double cx,double cy,double cz,double dx,double dy,double dz,int hex,double gain,double alpha,int mode,bool additive)
        {
            if(alpha<=.002)return;alpha=Math.Min(1,alpha);NativeBlastSim.Linear(hex,out double r,out double g,out double b);double k=gain*(additive?1:alpha);
            Quads.Add(ax,ay,az,bx,by,bz,cx,cy,cz,dx,dy,dz,r*k,g*k,b*k,alpha,mode,additive,1);
        }
        private uint Hash(int from,int to,uint h)
        {
            unchecked{for(int i=from;i<to;i++){for(int j=0;j<12;j++){h^=(uint)BitConverter.SingleToInt32Bits(Quads.Corners[i*12+j]);h*=16777619u;}for(int j=0;j<4;j++){h^=(uint)BitConverter.SingleToInt32Bits(Quads.Colors[i*4+j]);h*=16777619u;}}}
            return h;
        }
        private static float H(int seed,int i,int k){unchecked{uint h=(uint)seed*2654435761u^(uint)i*2246822519u^(uint)k*3266489917u;h^=h>>15;h*=2246822519u;h^=h>>13;h*=3266489917u;h^=h>>16;return (h&0xffffff)/16777216f;}}
    }
}

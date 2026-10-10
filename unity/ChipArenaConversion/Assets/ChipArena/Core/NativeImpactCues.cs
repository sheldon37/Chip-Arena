using System;

namespace ChipArena.UnityConversion
{
    public enum NativeImpactOp{Burst,Beam,Impact,Line,Paddle}
    /// <summary>
    /// One source draw call: BurstPool.spawn (Burst: A, Color, Count, Power), EventVfx.beam (Beam: A to B, Color),
    /// ChipBlasts.impact (Impact: A, Radius, Color, Ground), ChipBlasts.line (Line: A to B, Color to Color1, Jitter, Life,
    /// Sweep) or ChipBlasts.paddle (Paddle: at A facing B, Color).
    /// </summary>
    public struct NativeImpactCommand{public NativeImpactOp Op;public V3 A,B;public int Color,Color1;public double Count,Power,Radius,Jitter,Life,Sweep;public bool Ground;}
    /// <summary>What the presentation resolves about one fresh Unity event before planning its source draws.</summary>
    public struct NativeImpactEvent
    {
        public EventKind Kind;public string Detail;public V3 Position,From;public float Amount,Radius;
        /// <summary>The colour the source event carries where Unity's event has none: a Break's material colour (none for the
        /// Siren's seeker breaks, siren.ts:109), an Impact's weapon colour (simulation.ts:411/420). Otherwise the event's own.</summary>
        public int Color;
        /// <summary>The target is a pilot: the source's `hit` and `debrisHit` exist for pilots only (adds and summons take
        /// minionHit and impact).</summary>
        public bool TargetPilot;
        /// <summary>The projectile trails setting (the source draws an impact's streak only with trails on).</summary>
        public bool Trails;
        /// <summary>A Pong rally's target pilot and its body position (the source's state.players pos).</summary>
        public bool HasTarget;public V3 Target;
    }

    /// <summary>
    /// Pure presentation rules for the authoritative event stream, ported from client/event-vfx.ts EventVfx.event (at
    /// 05950ea; also its rally()) and client/chaos-visuals.ts summons' minionShot: which voxel spark bursts, lit streaks,
    /// chip-blast impacts, lines and paddles each Unity event draws. They replace the early 12-particle ParticleSystem burst
    /// on every hit and the cube bars on ability beams. Nothing here changes the match.
    /// </summary>
    public static class NativeImpactCues
    {
        public const int MaxCommands=16;
        // event-vfx.ts:29: impact, break, hit (not burn/poison), debrisHit, parry, blockHit, reflect, blockAbsorb and collapse
        // burst event.color||0xfbc98b, 18 for a break and 6 otherwise, at power 3; :28 an impact with a from point (trails on)
        // streaks event.color||0xffedb8 from it.
        public const int BurstColor=0xfbc98b,StreakColor=0xffedb8,BreakCount=18,HitCount=6;public const double HitPower=3;
        // simulation.ts:241 the hit's colour by damage kind (burn/poison never burst), :316 blockHit 0x91f7eb.
        public const int HitColor=0xffffff,DebrisColor=0xffbe71,BlockColor=0x91f7eb;
        // :31 procs: critical, catch, split (meleeRebound 0x91f7eb at event.radius||1.5 and slowed 0xb45cff shackles / 0x8de9ee
        // at .8 wait for Unity events); :32 a non-rally ricochet (0xffcc49 .8, the pong mod's 0xff5269 1.2: Unity's Pong).
        public const int CriticalColor=0xffd66c,CatchColor=0x7fffe0,SplitColor=0x96ffb7,RicochetColor=0xffcc49,PongColor=0xff5269,SummonColor=0x93db67;
        public const double CriticalRadius=1.5,CatchRadius=.9,SplitRadius=1.1,RicochetRadius=.8,PongRadius=1.2,SummonRadius=1.2;
        public const int ReboundColor=0x91f7eb,SlowedColor=0x8de9ee,ShacklesColor=0xb45cff;public const double ReboundRadius=1.5,SlowedRadius=.8;
        // :27 every leet-* event bursts 0x55ff88 (24 at power 5, leet-nuke 70 at 22) and streaks to event.from.
        public const int LeetColor=0x55ff88,LeetCount=24,NukeCount=70;public const double LeetPower=5,NukePower=22;
        // :27 stage hazards: saw 32 x 0xffcb62 at 6 + 14 x 0xe9f6ff at 4 + seven 1.1 m 0xffe5ad spokes; spikes rising 18 x
        // 0xb2a89b at 3; lava 8 x 0xff9c22 at 2.
        public const int SawColor=0xffcb62,SawSparkColor=0xe9f6ff,SawSpokeColor=0xffe5ad,SpikeDustColor=0xb2a89b,LavaColor=0xff9c22;
        public const int SawCount=32,SawSparkCount=14,SawSpokes=7,SpikeDustCount=18,LavaCount=8;public const double SawPower=6,SawSparkPower=4,SawSpoke=1.1,SawSpokeDepth=.12,SpikeDustPower=3,LavaPower=2;
        // :33 an arc with a from point is a white-to-colour line (event.color||0xc4b4ff); without one or a radius, four short
        // radial lines. Ball Lightning's arcs carry LIT (magic-abilities.ts:12, WEAPONS.lightning.color).
        public const int ArcColor=0xc4b4ff,Lit=0xc2a3ff;
        // :36 rally(): a paddle at the bounce point and a white-to-red .15 s streak (jitter .05, sweep .12) up to 6 m toward
        // the target's body + .3 m.
        public const double RallyReach=6,RallyLift=.3,RallyJitter=.05,RallyLife=.15,RallySweep=.12;
        // gun-abilities.ts:23 dud(): an unarmed rocket is an `impact` coloured 0x9a9a9a, drawn as the 6-voxel burst.
        public const int DudColor=0x9a9a9a;
        // chaos-visuals.ts:64 minionShot with renderer.ts summonFx: a 0xfff2b8 -> 0xffa040 line (jitter .02, life .09), a
        // 4-voxel 0xffd27a muzzle puff at 1.2 and a 3-voxel 0xd8c8a0 puff at 1.4 where it lands.
        public const int ToyLine=0xfff2b8,ToyLineEnd=0xffa040,ToyMuzzle=0xffd27a,ToyHit=0xd8c8a0;public const double ToyJitter=.02,ToyLife=.09;

        /// <summary>A from point the event really carries: Emit stores the position when there is none, and an event added
        /// without Emit (surface impacts) leaves it zero.</summary>
        public static bool HasFrom(V3 position,V3 from)=>!(from.x==0&&from.y==0&&from.z==0)&&(from-position).Length>1e-4f;
        /// <summary>The source event's own colour for kinds whose colour the simulation fixes (simulation.ts:241/:316,
        /// magic-abilities.ts:69); otherwise what the presentation resolved. 0: none (the draw's default applies).</summary>
        public static int SourceColor(in NativeImpactEvent e)
        {
            switch(e.Kind)
            {
                case EventKind.Hit:case EventKind.Headshot:return e.Detail=="debris"?DebrisColor:e.Detail=="burn"?0xff822f:e.Detail=="poison"?0x9de34d:HitColor;
                case EventKind.Block:return BlockColor;
                case EventKind.Arc:return e.Color!=0?e.Color:e.Detail=="ball-lightning"?Lit:0;
                default:return e.Color;
            }
        }
        private static int Put(NativeImpactCommand[] o,int n,in NativeImpactCommand c){if(n<o.Length)o[n]=c;return n+1;}
        public static int Burst(NativeImpactCommand[] o,int n,V3 at,int color,double count,double power)=>Put(o,n,new NativeImpactCommand{Op=NativeImpactOp.Burst,A=at,Color=color,Count=count,Power=power});
        public static int Beam(NativeImpactCommand[] o,int n,V3 a,V3 b,int color)=>Put(o,n,new NativeImpactCommand{Op=NativeImpactOp.Beam,A=a,B=b,Color=color});
        public static int Impact(NativeImpactCommand[] o,int n,V3 at,double radius,int color,bool ground=false)=>Put(o,n,new NativeImpactCommand{Op=NativeImpactOp.Impact,A=at,Radius=radius,Color=color,Ground=ground});
        public static int Line(NativeImpactCommand[] o,int n,V3 a,V3 b,int c0,int c1,double jitter=.25,double life=.14,double sweep=0)=>Put(o,n,new NativeImpactCommand{Op=NativeImpactOp.Line,A=a,B=b,Color=c0,Color1=c1,Jitter=jitter,Life=life,Sweep=sweep});
        public static int Paddle(NativeImpactCommand[] o,int n,V3 at,V3 dir,int color)=>Put(o,n,new NativeImpactCommand{Op=NativeImpactOp.Paddle,A=at,B=dir,Color=color});
        private static int Leet(NativeImpactCommand[] o,int n,V3 at,bool nuke)=>Burst(o,n,at,LeetColor,nuke?NukeCount:LeetCount,nuke?NukePower:LeetPower);
        /// <summary>The draws event-vfx.ts EventVfx.event makes for the source event a Unity event stands for. Returns the count.</summary>
        public static int Plan(in NativeImpactEvent e,NativeImpactCommand[] o)
        {
            int n=0,color=SourceColor(e);V3 at=e.Position;bool from=HasFrom(e.Position,e.From);
            switch(e.Kind)
            {
                case EventKind.Hit:case EventKind.Headshot:
                    // simulation.ts:241 a pilot's `hit` above .05; :473 debris on a pilot is also a debrisHit (no colour).
                    if(!e.TargetPilot)break;
                    if(NativeEventFeedback.SourceHit(e.Kind,e.Amount)&&e.Detail!="burn"&&e.Detail!="poison")n=Burst(o,n,at,color,HitCount,HitPower);
                    if(e.Detail=="debris")n=Burst(o,n,at,BurstColor,HitCount,HitPower);
                    break;
                case EventKind.Block:case EventKind.Collapse:n=Burst(o,n,at,color!=0?color:BurstColor,HitCount,HitPower);break;
                case EventKind.Break:n=Burst(o,n,at,color!=0?color:BurstColor,BreakCount,HitPower);break;
                case EventKind.Impact:
                    if(e.Trails&&from)n=Beam(o,n,e.From,at,color!=0?color:StreakColor);
                    n=Burst(o,n,at,color!=0?color:BurstColor,HitCount,HitPower);break;
                case EventKind.Critical:n=Impact(o,n,at,CriticalRadius,CriticalColor);break;
                case EventKind.Catch:n=Impact(o,n,at,CatchRadius,CatchColor);break;
                case EventKind.Split:n=Impact(o,n,at,SplitRadius,SplitColor);break;
                // Unity's Ricochet is a bullet's prop bounce; a pong ball's bounces and rallies are its Pong event (PR #88:
                // Detail 'rally', Target = rally seat, From = bounce point, Position = the ball).
                case EventKind.Ricochet:n=e.Detail=="rally"?Rally(e,o,n):Impact(o,n,at,RicochetRadius,RicochetColor);break;
                case EventKind.Pong:n=e.Detail=="rally"?Rally(e,o,n):Impact(o,n,at,PongRadius,PongColor);break;
                case EventKind.Summon:n=Impact(o,n,at,SummonRadius,SummonColor,true);break;
                case EventKind.HazardHit:
                    if(e.Detail=="saw")
                    {
                        n=Burst(o,n,at,SawColor,SawCount,SawPower);n=Burst(o,n,at,SawSparkColor,SawSparkCount,SawSparkPower);
                        for(int k=0;k<SawSpokes;k++){double a=k*Math.PI*2/SawSpokes;n=Beam(o,n,at,new V3((float)(at.x+Math.Cos(a)*SawSpoke),(float)(at.y+Math.Sin(a)*SawSpoke),(float)(at.z+SawSpokeDepth)),SawSpokeColor);}
                    }
                    else if(e.Detail=="lava")n=Burst(o,n,at,LavaColor,LavaCount,LavaPower);
                    break;
                // Every leet-* event (Unity: Leet with the leet-* Detail; PR #88 adds leet-freeze at the caster). Unity's Leet on
                // a bullet impact (Detail = the orb kind: bolt, copy, brain) stands for no source event (leet.ts impact() emits
                // none; the surface or pilot hit draws its own impact or hit).
                case EventKind.Leet:if(e.Detail==null||!e.Detail.StartsWith("leet-",StringComparison.Ordinal))break;n=Leet(o,n,at,e.Detail=="leet-nuke");if(from)n=Beam(o,n,at,e.From,LeetColor);break;
                // Unity's nuke is its Explosion (Detail leet-nuke); the source's own leet-nuke event bursts 70 at 22 beside it.
                case EventKind.Explosion:if(e.Detail=="leet-nuke")n=Leet(o,n,at,true);break;
                case EventKind.Arc:
                    // Unity tags the L33t orb lock as an Arc; the source's is the leet-lock event (leet.ts:34).
                    if(e.Detail=="leet-lock"){n=Leet(o,n,at,false);if(from)n=Beam(o,n,at,e.From,LeetColor);break;}
                    if(from)n=Line(o,n,at,e.From,0xffffff,color!=0?color:ArcColor);
                    else if(!(e.Radius>0))for(int i=0;i<4;i++){double a=i*1.57+.4;n=Line(o,n,at,new V3((float)(at.x+Math.Cos(a)),at.y+.5f,(float)(at.z+Math.Sin(a))),0xffffff,ArcColor);}
                    break;
            }
            return Math.Min(n,o.Length);
        }
        // rally(): from = e.from||e.pos; d toward the target (+.3 m) or, without one, along the bounce; streak min(6, |d|).
        private static int Rally(in NativeImpactEvent e,NativeImpactCommand[] o,int n)
        {
            V3 at=e.Position,from=HasFrom(e.Position,e.From)?e.From:e.Position;
            double dx,dy,dz;if(e.HasTarget){dx=e.Target.x-at.x;dy=e.Target.y+RallyLift-at.y;dz=e.Target.z-at.z;}else{dx=at.x-from.x;dy=at.y-from.y;dz=at.z-from.z;}
            double l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l==0)l=1;double k=Math.Min(RallyReach,l)/l;
            n=Paddle(o,n,from,new V3((float)dx,(float)dy,(float)dz),PongColor);
            return Line(o,n,at,new V3((float)(at.x+dx*k),(float)(at.y+dy*k),(float)(at.z+dz*k)),0xffffff,PongColor,RallyJitter,RallyLife,RallySweep);
        }
        /// <summary>
        /// A fresh ability-effect beam record (NativeAbilityFx with Beam, element = school, radius) that stands for a source
        /// event with no Unity event of its own: the L33t possession (leet-control, leet.ts:24: a burst at the target, no from)
        /// and Slenderman's hop (leet-slender-hop, :83: burst at the landing, streak from the feet; hop: the record's from is
        /// a live tree's From), both element 2 radius 1, and the toy soldier's shot (minionShot, element 0 radius .1).
        /// Blinks and wormholes (element 3, radius 1) are their teleport blast, Ball Lightning arcs and the Leet orb lock their
        /// Arc events: nothing here.
        /// </summary>
        public static int Record(int element,double radius,V3 at,V3 from,bool hop,NativeImpactCommand[] o)
        {
            int n=0;
            if(element==2&&Math.Abs(radius-1)<1e-3){n=Leet(o,n,at,false);if(hop)n=Beam(o,n,at,from,LeetColor);}
            else if(element==0&&Math.Abs(radius-.1)<1e-3){n=Line(o,n,from,at,ToyLine,ToyLineEnd,ToyJitter,ToyLife);n=Burst(o,n,from,ToyMuzzle,4,1.2);n=Burst(o,n,at,ToyHit,3,1.4);}
            return Math.Min(n,o.Length);
        }
        /// <summary>stage-spikes-rise (stage-hazards.ts:21, derived by NativeSpikeRises): the dust burst at the bed.</summary>
        public static int SpikeRise(V3 at,NativeImpactCommand[] o)=>Burst(o,0,at,SpikeDustColor,SpikeDustCount,SpikeDustPower);
        /// <summary>An unarmed rocket (NativeBlastCues.Cue.Dud): the 6-voxel grey burst of its `impact`.</summary>
        public static int Dud(V3 at,NativeImpactCommand[] o)=>Burst(o,0,at,DudColor,HitCount,HitPower);
        /// <summary>config.ts MATERIALS colour of a Break's material (the content's material table), 0 when unknown.</summary>
        public static int MaterialColor(ContentMaterial[] materials,string id)
        {if(materials==null||string.IsNullOrEmpty(id))return 0;for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&materials[i].id==id)return materials[i].color;return 0;}
        /// <summary>Executes planned draws: sparks and streaks into the source BurstPool port, impacts, lines and paddles into
        /// the chip-blast sim, all at the renderer's effects quality (event-vfx.ts options().quality).</summary>
        public static void Run(NativeImpactCommand[] o,int n,NativeBlastSim sim,NativeBurstPool sparks,double quality)
        {
            for(int i=0;i<n&&i<o.Length;i++)
            {
                var c=o[i];
                switch(c.Op)
                {
                    case NativeImpactOp.Burst:sparks?.Spawn(c.A.x,c.A.y,c.A.z,c.Color,c.Count,c.Power,quality);break;
                    case NativeImpactOp.Beam:sparks?.Beam(c.A.x,c.A.y,c.A.z,c.B.x,c.B.y,c.B.z,c.Color,quality);break;
                    case NativeImpactOp.Impact:sim?.Impact(c.A.x,c.A.y,c.A.z,c.Radius,c.Color,quality,false,c.Ground);break;
                    case NativeImpactOp.Line:sim?.Line(c.A.x,c.A.y,c.A.z,c.B.x,c.B.y,c.B.z,c.Color,c.Color1,quality,c.Jitter,c.Life,c.Sweep);break;
                    case NativeImpactOp.Paddle:sim?.Paddle(c.A.x,c.A.y,c.A.z,c.B.x,c.B.y,c.B.z,c.Color,quality);break;
                }
            }
        }
    }

    /// <summary>
    /// client/standard-melee-effects.ts flash(): each landed `hit` from a standard melee pilot throws 3 + 4 x weight thin
    /// 0xffd9a0 sparks (at least 2, scaled by the effects quality) out of the contact point over .28 s, the weight by the
    /// attacker's spec (HIT_WEIGHT); burn and poison ticks never flash; at most 8 live flashes (the oldest goes). The flat
    /// shock ring is hidden in the source (owner 2026-10-04: no outlined circles) and is not drawn. Ages run on the snapshot
    /// clock (the source's s.time). Difference: the source drops a flash whose age is negative, which on a guest's
    /// interpolated clock is every fresh one; here it waits until its time comes.
    /// </summary>
    public sealed class NativeMeleeFlash
    {
        public const double Life=.28,SparkWidth=.035,SparkLength=.32;public const int Cap=8,Color=0xffd9a0;
        public struct Flash{public double At,Weight;public V3 Position;public int Sparks;}
        private readonly Flash[] flashes=new Flash[Cap];
        public int Count{get;private set;}
        public Flash this[int i]=>flashes[i];
        /// <summary>HIT_WEIGHT: greatsword 1, shield .8, sword .6, daggers .35; 0 for every other spec (no flash).</summary>
        public static double Weight(string spec){switch(spec){case "greatsword":return 1;case "shield":return .8;case "sword":return .6;case "daggers":return .35;default:return 0;}}
        public static bool Flashes(string spec,string detail)=>Weight(spec)>0&&detail!="burn"&&detail!="poison";
        public static int Sparks(double weight,double quality)=>Math.Max(2,(int)NativeBlastSim.Round((3+4*weight)*Math.Min(1,Math.Max(.1,quality))));
        public void Clear()=>Count=0;
        public void Add(double at,double weight,V3 position,double quality)
        {
            if(Count>=Cap){for(int i=1;i<Count;i++)flashes[i-1]=flashes[i];Count--;}
            flashes[Count++]=new Flash{At=at,Weight=weight,Position=position,Sparks=Sparks(weight,quality)};
        }
        /// <summary>Forgets flashes older than Life at `now`.</summary>
        public void Prune(double now){int kept=0;for(int i=0;i<Count;i++)if(now-flashes[i].At<=Life)flashes[kept++]=flashes[i];Count=kept;}
        /// <summary>
        /// Spark i of a flash `age` seconds old (0..Life): offset from the contact point, rotation (three.js Euler XYZ
        /// (-atan2(y, hypot(x, z)), atan2(x, z), 0) as a quaternion), length along local z (.32 x (.3 + 1.2 (1 - u))) and opacity.
        /// </summary>
        public static void Spark(double at,double weight,int i,double age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double length,out double alpha)
        {
            double u=Math.Max(0,Math.Min(1,age/Life)),o=1-Math.Pow(1-u,3),a=i*2.39996+at*7,d=(.15+o*(.6+.7*weight))*(.8+.1*(i%3));
            x=Math.Cos(a)*d;y=Math.Sin(a*1.7)*.25*d+.1*o;z=Math.Sin(a)*d;
            NativeImpactMath.EulerXYZ(-Math.Atan2(y,Math.Sqrt(x*x+z*z)),Math.Atan2(x,z),0,out qx,out qy,out qz,out qw);
            length=SparkLength*(.3+1.2*(1-u));alpha=Math.Pow(1-u,1.5);
        }
    }

    /// <summary>
    /// client/standard-melee-effects.ts event('melee-slam') and update(): a wolf-leap or melee slam landing (Unity
    /// MeleeImpacts) raises round(40 q) 0xb6a895 dust points (size .3, size-attenuated, opacity .46 - .4 age) that spread
    /// (scale .4 + 1.6 age) and rise (.35 m/s), and throws max(3, round(10 q)) 0x8b8880 Lambert icosahedron stones on arcs,
    /// shrinking from .075 m to nothing over 1.1 s; q = the effects quality clamped to .1..1. The outlined ring is hidden in
    /// the source. Offsets are from the landing point lifted .03 m.
    /// </summary>
    public static class NativeSlamDust
    {
        public const double Life=1.1,Lift=.03,DustSize=.3,StoneRadius=.075;public const int DustColor=0xb6a895,StoneColor=0x8b8880;
        public static double Q(double quality)=>Math.Min(1,Math.Max(.1,quality));
        public static int Dust(double quality)=>(int)NativeBlastSim.Round(40*Q(quality));
        public static int Stones(double quality)=>Math.Max(3,(int)NativeBlastSim.Round(10*Q(quality)));
        public static void DustPoint(int i,double age,out double x,out double y,out double z)
        {double a=i*2.39996,r=.7+i%5*.25,s=.4+age*1.6;x=Math.Cos(a)*r*s;y=(i%4)*.05*s+age*.35;z=Math.Sin(a)*r*s;}
        public static double DustOpacity(double age)=>Math.Max(0,.46-age*.4);
        /// <summary>PointsMaterial sizeAttenuation: a point of `size` covers size x tan(fovY / 2) world units at any depth.</summary>
        public static double PointWorldSize(double size,double fovYDegrees)=>size*Math.Tan(fovYDegrees*Math.PI/360);
        public static void Stone(int i,double age,out double x,out double y,out double z,out double scale,out double qx,out double qy,out double qz,out double qw)
        {
            double a=i*2.39996,speed=1.7+(i%3)*.6;x=Math.Cos(a)*speed*age;y=Math.Max(0,(1.8+i%3*.45)*age-4*age*age);z=Math.Sin(a)*speed*age;
            scale=StoneRadius*(1-age/Life);NativeImpactMath.EulerXYZ(age*(i+2),0,age*3,out qx,out qy,out qz,out qw);
        }
    }

    public static class NativeImpactMath
    {
        /// <summary>three.js Quaternion.setFromEuler for order XYZ.</summary>
        public static void EulerXYZ(double x,double y,double z,out double qx,out double qy,out double qz,out double qw)
        {
            double c1=Math.Cos(x/2),c2=Math.Cos(y/2),c3=Math.Cos(z/2),s1=Math.Sin(x/2),s2=Math.Sin(y/2),s3=Math.Sin(z/2);
            qx=s1*c2*c3+c1*s2*s3;qy=c1*s2*c3-s1*c2*s3;qz=c1*c2*s3+s1*s2*c3;qw=c1*c2*c3-s1*s2*s3;
        }
        /// <summary>Rotates (x, y, z) by the unit quaternion q.</summary>
        public static void Rotate(double qx,double qy,double qz,double qw,double x,double y,double z,out double rx,out double ry,out double rz)
        {
            double tx=2*(qy*z-qz*y),ty=2*(qz*x-qx*z),tz=2*(qx*y-qy*x);
            rx=x+qw*tx+(qy*tz-qz*ty);ry=y+qw*ty+(qz*tx-qx*tz);rz=z+qw*tz+(qx*ty-qy*tx);
        }
    }
}

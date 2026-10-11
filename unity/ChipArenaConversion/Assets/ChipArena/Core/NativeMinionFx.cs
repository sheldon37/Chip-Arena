using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Siren minion (PvE horde add) effects of client/siren/minions at 05950ea, as pure functions of what host, guests and the
    /// killcam replay all hold (the add's id, its vent entry and its death) and the presentation clock, so every client draws
    /// the same frame at any frame rate and after a rewind (Runtime/NativeMinionFxPresentation.cs draws them):
    ///  - the dying glow (materials.ts MinionMaterial): crack, ember and strip cells flicker per cell, and every few frames the
    ///    whole add's glow may drop out like failing power. The bank flags those cells (tools/export_models.mjs minionGlow);
    ///    NativeSourceLit runs <see cref="Flicker"/> per pixel on the clock, with the drop hashed here once per add and frame
    ///    (<see cref="GlowW"/>), and keeps a charred joint's embers glowing (materials.ts charred glow and flicker);
    ///  - the black-flame tongues (model.ts, motion.ts animateSirenMinion): each one swells and sways on its anchor, and
    ///    flares while the add climbs out of a vent and while it burns away (<see cref="TongueMatrix"/>);
    ///  - shed licks (a Unity addition after the Siren's own tongues, motion.ts emitFlames): each tongue sheds small black
    ///    flames from its tip that rise, trail behind a walking add and fade to violet smoke (<see cref="Lick"/>);
    ///  - the crack light's haze (Unity addition): a faint violet glow over a cracked head, breathing and dropping out with the
    ///    crack cells on the same drop bits (<see cref="Haze"/>);
    ///  - the death (motion.ts dyingLook): each joint's parts char at a hashed time, then the body burns away by rest height,
    ///    feet first and the head last (<see cref="Charred"/>, <see cref="Vanish"/>);
    ///  - fx.ts MinionFx: the black-flame pool, rim flames and violet sparks of a vent climb, and the flames, ash and scorch of
    ///    the burn-away (<see cref="Effects"/>), given out by priority and distance as horde-view.ts gives out full rigs
    ///    (<see cref="Rank"/>, <see cref="Band"/>);
    ///  - hints.ts possession hint: a violet glow pulsing out of the possessed add's crown every ~4 s (<see cref="HintPulse"/>,
    ///    <see cref="Hint"/>, brightened at level 3). The levels, the grin within 25 m and the black-flame trail are
    ///    NativeMinionHint.cs, timed by the replicated possession start (NativePveAdd.PossessedAt, NativeMinionFrame.possessedAt).
    /// Everything is in the source (three.js) frame; the runtime mirrors x. Allocation free.
    /// </summary>
    public static partial class NativeMinionFx
    {
        /// <summary>fx.ts EMERGE_SECONDS, DEATH_SECONDS and DEATH_FX_SECONDS (how long the death effects outlast the body).</summary>
        public const double EmergeSeconds=1.95,DeathSeconds=2.3,DeathFxSeconds=3.1;
        /// <summary>materials.ts CATEGORY flicker and the accent/strip phase lift, per bank flicker class (0 shell, edge and
        /// flex; 1 accent plates; 2 glow strips).</summary>
        public static readonly double[] ClassFlicker={.35,.85,.9},ClassPhase={0,.37,.37};
        /// <summary>The bank's glow code base (emission.w = 3 + class + .98 x cell hash) and the cell hash's span.</summary>
        public const double GlowCode=3,CellSpan=.98;
        private static double Sm(double x)=>NativeFxMath.Smooth(x);
        private static double Clamp(double x)=>NativeFxMath.Clamp01(x);
        /// <summary>motion.ts win: in over [a, b], out over [c, d] (0 for an absent age, +inf).</summary>
        public static double Win(double x,double a,double b,double c,double d)=>double.IsInfinity(x)||double.IsNaN(x)?0:Sm((x-a)/(b-a))*(1-Sm((x-c)/(d-c)));
        private static double Fract(double x)=>x-Math.Floor(x);

        /// <summary>An add's look seed (gore.ts and horde-view.ts hash an add by its id's digits).</summary>
        public static uint Seed(int id)=>NativeGoreRules.FnvDigits(id);
        /// <summary>The bank variant an add draws (Runtime/NativeSourcePresentation.cs: id % 6).</summary>
        public static int Variant(int id)=>((id%Variants)+Variants)%Variants;
        /// <summary>
        /// The add's glow phase. The source spreads minions over four material pools (pool·1.618); the bank shares one look per
        /// variant, so the phase comes from the add's id instead and adds of one variant never flicker in step. It sits on a
        /// 1/4096 grid, so it comes back exactly out of <see cref="GlowW"/>'s drop bits in a float.
        /// </summary>
        public static double Phase(int id)=>Math.Round(4*1.618*NativeGoreRules.Hash01(Seed(id),0x6d,1)*4096)/4096;

        /// <summary>The bank's glow code for a flicker class and a cell hash in [0, 1).</summary>
        public static double Code(int cls,double cell)=>GlowCode+cls+CellSpan*cell;
        /// <summary>The flicker class and the cell hash a glow code holds (NativeSourceLit MinionFlicker).</summary>
        public static void Decode(double code,out int cls,out double cell)
        {double c=Math.Floor(code-GlowCode+1e-3);cls=(int)Math.Max(0,Math.Min(2,c));cell=Clamp((code-GlowCode-cls)/CellSpan);}
        /// <summary>materials.ts minionMaterial(charred): glow 3.2 over each class's live glow (shell 1.7, accent 1.5, strip 1.6;
        /// edge, flex and scrap share class 0) and flicker .6, so a charred joint's embers glow on through its dark tint.</summary>
        public static readonly double[] CharGlow={3.2/1.7,3.2/1.5,3.2/1.6};public const double CharFlicker=.6;
        /// <summary>
        /// materials.ts MinionMaterial's emission flicker for one glow cell at clock t: a per-cell wave (4 to 13 Hz) mixed in by
        /// the class's flicker, times the add-wide drop (about one slot in seven of 1/7 s loses .8 of the flicker share). The
        /// source hashes the cell centre with the pool phase; the bank holds the centre's hash, re-hashed here with the phase.
        /// charred (the joint's char, 0..1) moves the flicker to .6 and scales the emission by <see cref="CharGlow"/>.
        /// NativeSourceLit.shader MinionFlicker is the same expression, except that it reads the drop from <see cref="GlowW"/>
        /// (re-hashing the large slot argument in float rounds to other slots, and differs between GPUs); its per-cell wave keeps
        /// a float hash, so the cells' wave phases (not the drop) may differ from this reference and between GPUs.
        /// </summary>
        public static double Flicker(double code,double phase,double t,double charred=0)
        {
            Decode(code,out int cls,out double cell);double f=ClassFlicker[cls]+(CharFlicker-ClassFlicker[cls])*charred,drop=Dropped(phase,t,cls)?1:0;phase+=ClassPhase[cls];
            double h=Fract(Math.Sin(cell*289.37+phase*7.13)*43758.5453),wave=.5+.5*Math.Sin(t*(4+9*h)+h*40+phase*3);
            return (1+(wave-1)*f)*(1-drop*f*.8)*(1+(CharGlow[cls]-1)*charred);
        }
        /// <summary>Whether the add-wide power drop is on for a flicker class at clock t (one 1/7 s slot in about seven).</summary>
        public static bool Dropped(double phase,double t,int cls=0)
        {phase+=ClassPhase[cls];double slot=Math.Floor(t*7+phase*11);return Fract(Math.Sin(slot*12.9898+phase*78.233)*43758.5453)>=.86;}
        /// <summary>The add-wide power drop alone (1 or 1 - .8·flicker), for effects that follow the glow (the haze).</summary>
        public static double Drop(double phase,double t,int cls=0)=>Dropped(phase,t,cls)?1-ClassFlicker[cls]*.8:1;
        /// <summary>
        /// The add's _GlowTint.w at clock t: its phase (under 6.5) plus 8 while class 0's drop is on and 16 while the accent and
        /// strip classes' is (they share one phase lift). NativeSourceLit takes the drop from these bits, so every machine dims
        /// the crack cells in exactly the slots the haze dims (<see cref="Drop"/>).
        /// </summary>
        public static double GlowW(double phase,double t)=>phase+(Dropped(phase,t,0)?8:0)+(Dropped(phase,t,1)?16:0);
        /// <summary>NativeSourceLit MinionFlicker's reading of <see cref="GlowW"/>.</summary>
        public static void SplitGlow(double w,out double phase,out bool drop0,out bool drop12){drop12=w>=15.5;if(drop12)w-=16;drop0=w>=7.5;if(drop0)w-=8;phase=w;}

        // ---- tongues (motion.ts animateSirenMinion) ------------------------------------------------------------------
        /// <summary>The tongues' flare: up to 1.5 while emerging (e seconds into a vent climb, +inf without one) and up to
        /// 1.7 while burning away (d seconds since death, +inf alive).</summary>
        public static double Flare(double e,double d)=>1+.5*Win(e,.2,.6,1.6,EmergeSeconds)+(double.IsInfinity(d)||double.IsNaN(d)?0:.7*Win(d,.3,.8,1.6,2.1));
        /// <summary>Tongue i of a variant: its range in the generated table.</summary>
        public static int TongueCount(int variant)=>variant<0||variant>=Variants?0:TongueStart[variant+1]-TongueStart[variant];
        /// <summary>
        /// One tongue's scale and sway at clock t (phase: the tongue's own; extra: the add's offset so adds of one variant do
        /// not lick in step): x and z widen with the flare, y flickers between .65 and 1.2 of its height times the flare, and
        /// it whips by Euler (rx, 0, rz) on its base turn.
        /// </summary>
        public static void Tongue(double t,double phase,double height,double flare,out double wide,out double tall,out double rx,out double rz)
        {
            double n=.5+.5*Math.Sin(t*7.3+phase)*Math.Sin(t*3.1+phase*1.9);wide=1.25*(1+.45*(flare-1));tall=1.25*height*(.65+.55*n)*flare;
            rx=.18*Math.Sin(t*2.6+phase);rz=.26*Math.Sin(t*3.4+phase*1.3);
        }
        /// <summary>A tongue's per-add phase offset (added to its table phase).</summary>
        public static double TonguePhase(int id)=>Phase(id)*2.4;
        /// <summary>
        /// Tongue k (0-based, table order) of a variant at clock t: its joint and its matrix in that joint's frame (row-major
        /// 4x4, source axes, the unit tongue piece to the joint): T(anchor)·R(base·Euler(rx, 0, rz))·S(wide, tall, wide).
        /// False past the variant's tongues.
        /// </summary>
        public static bool TongueMatrix(int variant,int k,double t,double flare,double extraPhase,double[] m,out int joint,out int layout)
        {
            joint=-1;layout=0;if(k<0||k>=TongueCount(variant))return false;int i=TongueStart[variant]+k,o=i*9;joint=TongueJoint[i];layout=TongueLayout[i];
            Tongue(t,TongueFrame[o+8]+extraPhase,TongueFrame[o+7],flare,out double wide,out double tall,out double rx,out double rz);
            NativeFxMath.EulerQuaternion(rx,0,rz,out double sx,out double sy,out double sz,out double sw);
            double bx=TongueFrame[o+3],by=TongueFrame[o+4],bz=TongueFrame[o+5],bw=TongueFrame[o+6];
            // q = base·sway (Hamilton product), then the rotation matrix scaled per column.
            double qx=bw*sx+bx*sw+by*sz-bz*sy,qy=bw*sy-bx*sz+by*sw+bz*sx,qz=bw*sz+bx*sy-by*sx+bz*sw,qw=bw*sw-bx*sx-by*sy-bz*sz;
            Rotation(m,qx,qy,qz,qw,wide,tall,wide,TongueFrame[o],TongueFrame[o+1],TongueFrame[o+2]);return true;
        }
        /// <summary>m = T(p)·R(q)·S(s) (Matrix4.compose), row-major.</summary>
        public static void Rotation(double[] m,double qx,double qy,double qz,double qw,double sx,double sy,double sz,double px,double py,double pz)
        {
            double x2=qx+qx,y2=qy+qy,z2=qz+qz,xx=qx*x2,xy=qx*y2,xz=qx*z2,yy=qy*y2,yz=qy*z2,zz=qz*z2,wx=qw*x2,wy=qw*y2,wz=qw*z2;
            m[0]=(1-(yy+zz))*sx;m[1]=(xy-wz)*sy;m[2]=(xz+wy)*sz;m[3]=px;
            m[4]=(xy+wz)*sx;m[5]=(1-(xx+zz))*sy;m[6]=(yz-wx)*sz;m[7]=py;
            m[8]=(xz-wy)*sx;m[9]=(yz+wx)*sy;m[10]=(1-(xx+yy))*sz;m[11]=pz;m[12]=m[13]=m[14]=0;m[15]=1;
        }

        // ---- shed licks (Unity addition) ----------------------------------------------------------------------------
        /// <summary>Licks per tongue (half with reduced effects) and the tip in the tongue piece's frame (the tall flame's top).</summary>
        public const int Licks=4;public const double TipY=.24;
        /// <summary>
        /// Lick j of tongue k at clock t: each slot repeats with its own hashed period (.3 to .45 s); a lick is born at the
        /// tongue's tip, rises (.9u + 1.3u², over .14 m), drifts back against the add's ground velocity (vx, vz) as if it stayed
        /// where it was shed, swells then shrinks as a flames.ts tongue does, and ages from the Siren's black toward violet smoke
        /// (now and then an ember), so each tongue keeps a short flickering column above it. Writes the offset from the tip
        /// (source axes), the Euler turn, the edge and the linear colour; false while the slot rests.
        /// </summary>
        public static bool Lick(uint seed,int k,int j,double t,double vx,double vz,out double dx,out double dy,out double dz,out double ex,out double ey,out double ez,out double edge,out double r,out double g,out double b)
        {
            dx=dy=dz=ex=ey=ez=edge=r=g=b=0;int slot=k*Licks+j;
            double period=.3+.15*NativeGoreRules.Hash01(seed,slot,71),off=NativeGoreRules.Hash01(seed,slot,72)*period,cycle=Math.Floor((t-off)/period),born=off+cycle*period;
            int c=(int)(((long)cycle%100000+100000)%100000);double life=.22+.14*NativeGoreRules.Hash01(seed,slot,c,73),age=t-born,u=age/life;if(u<0||u>=1)return false;
            double h1=NativeGoreRules.Hash01(seed,slot,c,74),h2=NativeGoreRules.Hash01(seed,slot,c,75),h3=NativeGoreRules.Hash01(seed,slot,c,76);
            double rise=.14*(.9*u+1.3*u*u)/2.2;dx=(h1-.5)*.03+(h2-.5)*.04*u-vx*age;dz=(h2-.5)*.03+(h3-.5)*.04*u-vz*age;dy=rise;
            ex=(h1-.5)*.5;ey=h2*6.3+(h3-.5)*4*age;ez=(h3-.5)*.5;
            double grow=u<.2?u/.2:1-Math.Pow((u-.2)/.8,1.35);edge=(.02+.014*h3)*Math.Max(0,grow);
            bool ember=h1>.95;double f=u*u;
            if(ember){double e=.75+.4*h2;NativeBlastSim.Linear(NativeFlameField.EmberTint,out r,out g,out b);r*=e;g*=e;b*=e;NativeBlastSim.Linear(NativeFlameField.Fade(NativeFlameTone.Ember),out double fr,out double fg,out double fb);r+=(fr-r)*f;g+=(fg-g)*f;b+=(fb-b)*f;}
            else{NativeBlastSim.Linear(NativeFlameField.Ink,out r,out g,out b);NativeBlastSim.Linear(NativeFlameField.SmokeTint,out double sr,out double sg,out double sb);double k0=h3*.7;r+=(sr-r)*k0;g+=(sg-g)*k0;b+=(sb-b)*k0;
                NativeBlastSim.Linear(NativeFlameField.Fade(NativeFlameTone.Black),out double fr,out double fg,out double fb);r+=(fr-r)*f;g+=(fg-g)*f;b+=(fb-b)*f;}
            return edge>0;
        }
        /// <summary>Licks drawn per tongue at an effects quality (fx.ts halves its cubes below .5).</summary>
        public static int LickCount(double quality)=>quality<.5?Licks/2:Licks;

        // ---- crack haze (Unity addition) and possession hint (hints.ts) -----------------------------------------------
        public const int Violet=0x9d84ff;
        /// <summary>The cracked head's haze at clock t: size (m) and opacity, breathing slowly and dropping with the glow.</summary>
        public static void Haze(double phase,double t,out double size,out double opacity)
        {double breath=.5+.5*Math.Sin(t*1.7+phase*2.3);size=.28+.1*breath;opacity=(.12+.14*breath)*Drop(phase,t);}
        /// <summary>hints.ts pulse: a quick swell and slow fade every 4 s (the first 1.4 s of each period), offset per add.</summary>
        public static double HintPulse(double phase,double t){double k=((t+phase*4)%4+4)%4;return k<1.4?Math.Pow(Math.Sin(Math.PI*k/1.4),2):0;}
        /// <summary>
        /// hints.ts possessHint on the head joint (source axes): the seam box (centre, size; a short split on a cracked crown, a
        /// thin seam across a blank one) at opacity .55+.45·pulse, and the halo sprite at (0, .38, 0) of size .6+1.1·pulse at
        /// opacity .18+.82·pulse; both opacities times `boost` (level 3's 1.35, NativeMinionHint.Brightness), capped at 1.
        /// </summary>
        public static void Hint(bool cracked,double pulse,out double cx,out double cy,out double cz,out double sx,out double sy,out double sz,out double seam,out double halo,out double haloOpacity,double boost=1)
        {
            cx=cracked?-.03:0;cy=.365;cz=cracked?.03:0;sx=cracked?.09:.27;sy=.026;sz=cracked?.22:.045;
            seam=Math.Min(1,(.55+.45*pulse)*boost);halo=.6+1.1*pulse;haloOpacity=Math.Min(1,(.18+.82*pulse)*boost);
        }

        // ---- death (motion.ts dyingLook) -----------------------------------------------------------------------------
        /// <summary>When joint j's parts char (seconds after death; the source hashes each part).</summary>
        public static double CharAt(uint seed,int joint)=>.42+.7*NativeGoreRules.Hash01(seed,joint,61);
        /// <summary>How far joint j's parts have charred at d seconds after death (0..1, a .1 s turn).</summary>
        public static double Charred(uint seed,int joint,double d)=>double.IsNaN(d)||d<0?0:Clamp((d-CharAt(seed,joint))/.1);
        /// <summary>The charred colour scale (materials.ts minionMaterial charred: the colour 86 % toward #0d0b0e).</summary>
        public const double CharScale=.17;
        /// <summary>The burn-away of joint j at d seconds after death: 0 whole, 1 gone (feet first from 1.25 s, the head
        /// last by 2.35 s, by the rest height of the joint's parts). Joints without parts never vanish.</summary>
        public static double Vanish(int joint,double d)
        {
            if(double.IsNaN(d)||d<0||joint<0||joint*4>=JointRest.Length)return 0;double y=JointRest[joint*4];if(double.IsNaN(y))return 0;
            return Sm((d-(1.25+.8*(y-RestLow)/Math.Max(1e-6,RestHigh-RestLow)))/.3);
        }
        /// <summary>The body is gone (motion.ts: at DEATH_SECONDS the rig body hides).</summary>
        public static bool Gone(double d)=>!double.IsNaN(d)&&d>=DeathSeconds;

        // ---- fx.ts MinionFx ------------------------------------------------------------------------------------------
        public const int Pool=8,Flames=36,Sparks=20,EffectCubes=Pool+Flames+Sparks;
        /// <summary>horde-view.ts HORDE_RENDER (quality): at most 24 full rigs carry minion effects, out to fullRadius 20 m plus
        /// the 4 m hysteresis. Unity draws every add as a full rig, so its effects go out by <see cref="Rank"/> within budgets of
        /// FullRigs adds' worth; past FullBand they are halved (fx.ts's reduced count) and past FarBand left out.</summary>
        public const int FullRigs=24;public const double FullBand=24,FarBand=40;
        /// <summary>The emergence or the burn-away is showing (e seconds into a vent climb, d since death; NaN without).</summary>
        public static bool Showing(double e,double d)=>!double.IsNaN(e)&&e>=0&&e<EmergeSeconds+.4||!double.IsNaN(d)&&d>=0&&d<DeathFxSeconds;
        /// <summary>
        /// An add's claim on the frame's effect budgets, lowest first: the possessed and the dying adds, then the ones climbing out
        /// of a vent, then the rest, each nearest first (distance in m, as horde-view.ts orders its full rigs). Without e and d
        /// (NaN) it is horde-view.ts's own order, the possessed add then the nearest (the licks' order).
        /// </summary>
        public static double Rank(bool possessed,double e,double d,double distance)=>(possessed||!double.IsNaN(d)?0:Showing(e,double.NaN)?1:2)*1e5+Math.Max(0,Math.Min(distance,9e4));
        /// <summary>The effects quality an add's cubes draw at, distance m from the camera: full to FullBand, halved (.3) to
        /// FarBand, none (-1) beyond.</summary>
        public static double Band(double distance,double quality)=>distance<=FullBand?quality:distance<=FarBand?Math.Min(quality,.3):-1;
        /// <summary>The most cubes <see cref="Effects"/> shows at a quality: what an add reserves, so it is drawn whole or not at all.</summary>
        public static int MostCubes(double quality)=>quality<0?0:quality<.5?Pool+Flames/2+Sparks/2:EffectCubes;
        /// <summary>The most tongues a horde variant grows (the lick budget's unit).</summary>
        public static int MostTongues{get{int most=0;for(int v=0;v<Variants;v++)most=Math.Max(most,TongueCount(v));return most;}}
        public const int Ink=0x0d0b0e,Smoke=0x3b3150,Rim=0xcfc6ea,Ember=0xe8dcff,Hot=0xff6a2c,Ash=0x8e8a86;
        /// <summary>One effect cube (rig-group frame, source axes): centre, Euler XYZ turn, edges, linear colour.</summary>
        public struct Cube{public double X,Y,Z,Ex,Ey,Ez,Sx,Sy,Sz,R,G,B;}
        private static void Mix(int a,int b,double t,out double r,out double g,out double bl)
        {NativeBlastSim.Linear(a,out r,out g,out bl);NativeBlastSim.Linear(b,out double r2,out double g2,out double b2);r+=(r2-r)*t;g+=(g2-g)*t;bl+=(b2-bl)*t;}
        /// <summary>
        /// fx.ts MinionFx.update: e seconds into the emergence (NaN when not emerging), d seconds since death (NaN alive),
        /// floor the rig-group height of the floor, points the rig-group positions of the head, chest, pelvis, knees and left
        /// elbow (x, y, z each; dying only). Writes the shown cubes in the source's slot order and returns how many; quality
        /// below .5 halves the flames and sparks.
        /// </summary>
        public static int Effects(uint seed,double e,double d,double floor,double[] points,int pointCount,double quality,Cube[] into)
        {
            if(!Showing(e,d))return 0;bool em=!double.IsNaN(e)&&e>=0&&e<EmergeSeconds+.4;
            bool low=quality<.5;int n=0;
            double R=em?.78*Sm(e/.3)*(1-Sm((e-EmergeSeconds+.2)/.5)):.62*Sm((d-.6)/.8)*(1-Sm((d-2.5)/.6));
            // The pool: overlapping rotated slabs make a ragged black disc; under a dying minion it is the scorch it leaves.
            for(int i=0;i<Pool&&R>=.01;i++)
            {
                if(n>=into.Length)return n;double a=i*Math.PI/Pool+.3*NativeGoreRules.Hash01(seed,i,1),r=R*(.82+.3*NativeGoreRules.Hash01(seed,i,2));
                Mix(Ink,Smoke,i==0?.25:.05,out double cr,out double cg,out double cb);into[n++]=new Cube{X=0,Y=floor+.004+i*.0004,Z=0,Ey=a,Sx=2*r,Sy=.006,Sz=.62*r,R=cr,G=cg,B=cb};
            }
            // Flames: tongues that swell then shrink as they rise. Emerging: around the rim; dying: off the body.
            int flames=low?Flames/2:Flames;
            for(int i=0;i<flames;i++)
            {
                double life=.45+.4*NativeGoreRules.Hash01(seed,i,3),span=em?EmergeSeconds-.2:1.9,start=em?0:.45,age=em?e:d;
                double period=life+.15*NativeGoreRules.Hash01(seed,i,4),off=NativeGoreRules.Hash01(seed,i,5)*period,cycle=Math.Floor((age-start-off)/period),born=start+off+cycle*period,u=(age-born)/life;
                if(cycle<0||born>start+span||u<0||u>1)continue;int c=(int)cycle;
                double h1=NativeGoreRules.Hash01(seed,i,c,6),h2=NativeGoreRules.Hash01(seed,i,c,7),h3=NativeGoreRules.Hash01(seed,i,c,8),x,y,z,size=.045+.045*h3;
                if(em){double a=h1*Math.PI*2,r=R*(.75+.25*h2);x=Math.Cos(a)*r;z=Math.Sin(a)*r;y=floor;size*=.9+.4*Sm((e-.3)/.6);}
                else{if(pointCount<1)continue;int p=(int)Math.Floor(h1*pointCount)%pointCount;x=points[p*3]+(h2-.5)*.28;y=points[p*3+1]+(h3-.5)*.2;z=points[p*3+2]+(NativeGoreRules.Hash01(seed,i,c,9)-.5)*.28;size*=.9+.4*Sm((d-.5)/.6);}
                double rise=.9*u+1.3*u*u,grow=u<.22?u/.22:1-Math.Pow((u-.22)/.78,1.4),edge=size*Math.Max(0,grow);if(n>=into.Length)return n;
                bool rim=h1<.12;Mix(Ink,rim?Rim:Smoke,u*u*u*(rim?.6:.8),out double cr,out double cg,out double cb);
                into[n++]=new Cube{X=x+(h2-.5)*.12*u,Y=y+rise*.55,Z=z+(h1-.5)*.12*u,Ex=(h2-.5)*.4,Ey=h1*6.3+u*2*(h3-.5),Ez=(h3-.5)*.4,Sx=edge*.8,Sy=edge*1.8,Sz=edge*.8,R=cr,G=cg,B=cb};
            }
            // Sparks: pale violet embers rising (emerging) and ash flakes drifting off the burning body (dying).
            for(int i=0;i<Sparks;i++)
            {
                if(low&&i%2==1)continue;double age=em?e:d,born=(em?.2:.6)+NativeGoreRules.Hash01(seed,i,10)*(em?1.4:1.6),life=.7+.6*NativeGoreRules.Hash01(seed,i,11),u=(age-born)/life;
                if(u<0||u>1)continue;double h1=NativeGoreRules.Hash01(seed,i,12),h2=NativeGoreRules.Hash01(seed,i,13),x,y,z;
                if(em){double a=h1*Math.PI*2,r=R*(.3+.6*h2);x=Math.Cos(a)*r;z=Math.Sin(a)*r;y=floor+.05+u*(.6+.8*h2);}
                else{if(pointCount<1)continue;int p=i%Math.Max(1,pointCount);x=points[p*3]+(h1-.5)*.5*u+(h1-.5)*.2;z=points[p*3+2]+(h2-.5)*.5*u;y=points[p*3+1]+u*(.5+.6*h1)-u*u*.3;}
                double size=em?.018+.014*h2:.022+.02*h2,fade=1-Sm((u-.6)/.4);if(n>=into.Length)return n;double cr,cg,cb;
                if(em)Mix(Ember,Hot,h1<.25?.7:0,out cr,out cg,out cb);else Mix(Ash,h2<.2?Ember:Ink,h2<.2?.8:.45*u,out cr,out cg,out cb);
                into[n++]=new Cube{X=x,Y=y,Z=z,Ex=u*7*h1,Ey=u*5*h2,Ez=u*3,Sx=size*fade,Sy=(em?size:size*.25)*fade,Sz=size*fade,R=cr,G=cg,B=cb};
            }
            return n;
        }
        /// <summary>
        /// The emergence age of a vent climb: the source counts from the first add's timeline (followers .35 s in), which
        /// puts every add's ActiveAt at EMERGE_SECONDS; NaN for doors, cells and before the climb starts.
        /// </summary>
        public static double VentAge(bool vent,double activeAt,double time){if(!vent)return double.NaN;double e=EmergeSeconds-(activeAt-time);return e<0?double.NaN:e;}
    }
}

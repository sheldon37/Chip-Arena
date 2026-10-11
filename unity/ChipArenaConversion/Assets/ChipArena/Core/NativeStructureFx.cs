using System;

namespace ChipArena.UnityConversion
{
    public enum NativeStructureMaterial{Masonry,Concrete,Timber,Metal,Glass}
    /// <summary>The structure events of docs/arenas/ARENA_PROPOSAL.md §5.4 (Astra adds the EventKinds; this is their look).</summary>
    public enum NativeStructureCue{Stress,Fall,Land}
    public enum NativeStructureMark{Scorch,Chip,Crack,Stain}
    /// <summary>An opaque debris cube: centre, full edges, rotation, albedo (sRGB 0..1).</summary>
    public struct NativeStructureCube{public V3 Center,Size;public float Qx,Qy,Qz,Qw,R,G,B;}
    /// <summary>A soft camera-facing dust billboard (premultiplied over): width and height in metres, sRGB albedo, opacity,
    /// and the light multipliers of its top and bottom edge.</summary>
    public struct NativeStructurePuff{public V3 Center;public float Width,Height,R,G,B,Alpha,Top,Bottom;}
    /// <summary>A world-space quad (decals, the crush footprint): centre and half axes U, V; Soft is a radial falloff, Lit
    /// takes the scene's up-facing light (pale marks), unlit marks darken what is under them.</summary>
    public struct NativeStructureFlat{public V3 Center,U,V;public float R,G,B,Alpha;public bool Soft,Lit;}
    /// <summary>A camera-facing ribbon from From to To: snapping wires (over) or spark streaks (additive).</summary>
    public struct NativeStructureLine{public V3 From,To;public float Width,R,G,B,Alpha;public bool Additive;}
    /// <summary>An additive soft sprite: the metal clank flash, glass glints, the wire snap spark.</summary>
    public struct NativeStructureGlow{public V3 Center;public float Size,R,G,B,Alpha;}

    /// <summary>
    /// Brickfall's destruction VFX kit, presentation only (docs/arenas/ARENA_PROPOSAL.md §2.2 Destruction VFX and decals, §2.3,
    /// §3.9, §3.10 Destruction look, §5.4, budgets §2.4). Pure, allocation-free rules behind one record store:
    /// <list type="bullet">
    /// <item>Material break bursts for today's Break events (Detail = material, SurfaceId = block id): masonry throws brick
    /// halves, mortar grit and a mortar puff; concrete chunks, grit and grey dust; timber splinters, chips and sawdust; metal
    /// spark streaks, bent plates and a clank flash; glass shards that glint. They replace the generic 18-spark burst on every
    /// map.</item>
    /// <item>The coming structure events (a presentation API until Astra emits them): the stress telegraph (red crush footprint,
    /// grit columns at the four corners of the crush box, dust trickling from cracks, window and lamp strobe values, a visual
    /// sag of at most 1° / 8 cm; never reduced), fall streamers and snapping wires, and the landing: the dust wall (24-40
    /// billboards expanding from 4 to 14 m radius in 1.6 s, up to 8 m tall, opacity .5 for 1.5 s, .2 by 3 s, gone by 7 s,
    /// thinned within 3 m of the camera), a ground shock skirt, 40-120 debris voxels that fade from fresh breaks to the dust
    /// coat over 10 s, and smoke wisps over the rubble for 15 s.</item>
    /// <item>A 256-decal ring buffer of scorch, chip, crack and stain marks; camera shake by distance (within 25 m, scaled by
    /// mass or energy; <see cref="NativeStructureShake"/>); the destruction sound routing (<see cref="NativeStructureSounds"/>).</item>
    /// </list>
    /// Every piece is a pure function of its record (seed, place, birth time) and the presentation clock, never integrated, so
    /// host, guests and the killcam draw the same frame; seeds come from hash(mapId, segment or block id, round index), never
    /// sent. A record may be born in the future (the dev preview schedules a whole sequence); it draws from its birth.
    /// Caps: at most 120 dust billboards map-wide (telegraphs claim theirs first, then the newest records), and fixed output
    /// arrays. Reduced effects halve debris and atmosphere, never telegraphs. Runtime/NativeStructureFxPresentation.cs turns
    /// the lists into one opaque and one premultiplied mesh (NativeFxDraw on NativeSirenFx.shader).
    /// </summary>
    public sealed class NativeStructureFx
    {
        public const int BurstSlots=24,EventSlots=6,DecalSlots=256,ShakeSlots=8,MockSlots=2;
        public const int CubeCap=1024,PuffCap=120,FlatCap=1536,LineCap=512,GlowCap=256;
        public const float BurstLife=2.6f,StressTail=.9f,FallLife=3.4f,LandLife=15,ThinNear=3,Gravity=14;
        public const float WallGrow=1.6f,WallFull=1.5f,WallThin=3,WallGone=7,WallInner=4,WallOuter=14,WallHeight=8,FreshFade=10,WispFrom=1.2f;
        public const int WallMin=24,WallMax=40,WallCore=6,DebrisMin=40,DebrisMax=120,SkirtCount=14,Wisps=8,Streamers=5,Wires=3;
        public const float SagDegrees=1,SagMetres=.08f,WindowHz=8,LampHz=4;
        // §3.10 palette (sRGB): brick, dark course, mortar and fresh breaks; concrete; timber; elevated green steel and rust;
        // demolition marks; windows; dust and soot. The rest are the kit's own (sparks, glass, ink).
        public const int Brick=0x9c4a30,BrickDark=0x7e3a26,BrickFresh=0xd2bfa3,Mortar=0xcdbb9e,Concrete=0x8f8a80,ConcreteFresh=0xc9c2b4,ConcreteDust=0xa9a49b,
            Timber=0xb4834f,TimberFresh=0xe2bc86,Sawdust=0xc9a57a,Steel=0x3e6b5a,SteelFresh=0x9aa3a6,Rust=0x7a3e22,SparkHot=0xffe3a0,SparkCool=0xff7a2a,Flash=0xfff1d6,FlashCore=0xffc87a,
            Glass=0x9ec4cc,Glint=0xf4fbff,Dust=0xb8a68f,DustCoat=0xb3a48e,Grit=0x8e7f6c,Wisp=0xa0968a,Soot=0x4a403a,Danger=0xd6332a,Ink=0x2a2420,Yellow=0xf4b321,Black=0x1c1a18,
            WindowLit=0xffb45e,WindowDark=0x1f2a30;
        public const uint StressSalt=0x51e55,FallSalt=0xfa11,LandSalt=0x1a4d,MarkSalt=0x3a2c,MockSalt=0x90c4;

        public readonly NativeStructureCube[] Cubes=new NativeStructureCube[CubeCap];public readonly NativeStructurePuff[] Puffs=new NativeStructurePuff[PuffCap];
        public readonly NativeStructureFlat[] Flats=new NativeStructureFlat[FlatCap];public readonly NativeStructureLine[] Lines=new NativeStructureLine[LineCap];
        public readonly NativeStructureGlow[] Glows=new NativeStructureGlow[GlowCap];
        public int CubeCount,PuffCount,FlatCount,LineCount,GlowCount;
        /// <summary>Collapse dust drift (m/s; Brickfall: east at .7, §3.10 atmosphere). Set per map by the presentation.</summary>
        public V3 Wind;

        private struct Burst{public bool Live;public NativeStructureMaterial Material;public V3 Center,Size;public int Color;public float Floor,Born;public uint Seed;}
        private struct Record{public bool Live;public NativeStructureCue Cue;public NativeStructureMaterial Material;public V3 Center,Normal,BoxCenter,BoxHalf;public float Radius,Amount,Floor,Born;public uint Seed,Base;}
        private struct Mark{public bool Live;public NativeStructureMark Kind;public V3 Center,Normal;public float Size,Born;public uint Seed;}
        private struct Shake{public bool Live,Rise;public V3 At;public float Peak,Born,Duration;public uint Seed;}
        private struct Mock{public bool Live;public NativeStructureMaterial Material;public V3 Spot;public float Floor,Born;public uint Seed;}
        private readonly Burst[] bursts=new Burst[BurstSlots];private readonly Record[] records=new Record[EventSlots];private readonly Mark[] marks=new Mark[DecalSlots];
        private readonly Shake[] shakes=new Shake[ShakeSlots];private readonly Mock[] mocks=new Mock[MockSlots];
        private readonly int[] burstOrder=new int[BurstSlots],recordOrder=new int[EventSlots];private int markCursor;

        public void Clear()
        {
            for(int i=0;i<BurstSlots;i++)bursts[i].Live=false;for(int i=0;i<EventSlots;i++)records[i].Live=false;for(int i=0;i<DecalSlots;i++)marks[i].Live=false;
            for(int i=0;i<ShakeSlots;i++)shakes[i].Live=false;for(int i=0;i<MockSlots;i++)mocks[i].Live=false;markCursor=0;CubeCount=PuffCount=FlatCount=LineCount=GlowCount=0;
        }
        public int LiveBursts{get{int n=0;for(int i=0;i<BurstSlots;i++)if(bursts[i].Live)n++;return n;}}
        public int LiveRecords{get{int n=0;for(int i=0;i<EventSlots;i++)if(records[i].Live)n++;return n;}}
        public int LiveMarks{get{int n=0;for(int i=0;i<DecalSlots;i++)if(marks[i].Live)n++;return n;}}

        // ---------- pure rules ----------
        /// <summary>Content material id (today's wood / stone / metal / foundation; Brickfall's brick and friends) to a burst kind.</summary>
        public static NativeStructureMaterial Classify(string id)
        {
            if(string.IsNullOrEmpty(id))return NativeStructureMaterial.Concrete;
            switch(id)
            {
                case "brick":case "masonry":case "terracotta":return NativeStructureMaterial.Masonry;
                case "stone":case "concrete":case "foundation":return NativeStructureMaterial.Concrete;
                case "wood":case "timber":return NativeStructureMaterial.Timber;
                case "metal":case "steel":case "iron":return NativeStructureMaterial.Metal;
                case "glass":return NativeStructureMaterial.Glass;
            }
            if(Has(id,"brick")||Has(id,"mason")||Has(id,"terracotta"))return NativeStructureMaterial.Masonry;if(Has(id,"wood")||Has(id,"timber"))return NativeStructureMaterial.Timber;
            if(Has(id,"metal")||Has(id,"steel")||Has(id,"iron")||Has(id,"rebar"))return NativeStructureMaterial.Metal;if(Has(id,"glass")||Has(id,"window"))return NativeStructureMaterial.Glass;
            return NativeStructureMaterial.Concrete;
        }
        private static bool Has(string s,string part)=>s.IndexOf(part,StringComparison.Ordinal)>=0;
        /// <summary>§5.2 seed rule: FNV-1a of mapId | id | round index. Identical on every client and in the killcam.</summary>
        public static uint Seed(string map,string id,int round){unchecked{uint h=Fnv(2166136261,map);h=(h^'|')*16777619;h=Fnv(h,id);h=(h^'|')*16777619;h=(h^(uint)round)*16777619;return h==0?1:h;}}
        private static uint Fnv(uint h,string s){unchecked{if(s!=null)for(int i=0;i<s.Length;i++)h=(h^s[i])*16777619;return h;}}
        /// <summary>The round index of the seed rule: set and round both replicate (NativeMatch.ApplyNetwork), so the killcam agrees.</summary>
        public static int RoundIndex(int set,int round)=>set*64+round;
        /// <summary>Brickfall-only extras stay dormant on every other map: decals from breaks and explosions, collapse dust drift.</summary>
        public static bool MapMarks(string map)=>map=="brickfall";
        public static V3 WindFor(string map)=>map=="brickfall"?new V3(.7f,0,0):new V3();
        /// <summary>Debris and atmosphere count factor: half at reduced effects (quality below .5). Telegraphs never use it.</summary>
        public static float Reduced(float quality)=>quality<.5f?.5f:1;
        public static float WallOpacity(float t)
        {
            if(t<0||t>=WallGone)return 0;float fade=Math.Min(1,t/.12f);
            if(t<WallFull)return .5f*fade;if(t<WallThin)return .5f-.3f*(t-WallFull)/(WallThin-WallFull);return .2f*(WallGone-t)/(WallGone-WallThin);
        }
        public static float WallRadius(float t)=>WallInner+(WallOuter-WallInner)*EaseOut(t/WallGrow);
        public static float WallTop(float t,float energy)=>WallHeight*(.6f+.4f*Clamp01(energy))*(.35f+.65f*EaseOut(t/WallGrow));
        public static int WallCount(float energy,float quality)=>(int)Math.Round((WallMin+(WallMax-WallMin)*Clamp01(energy))*Reduced(quality));
        public static int DebrisCount(float energy,float quality)=>(int)Math.Round((DebrisMin+(DebrisMax-DebrisMin)*Clamp01(energy))*Reduced(quality));
        /// <summary>Within 3 m of the camera a billboard thins to a fifth (at .8 m and closer).</summary>
        public static float NearThin(float distance)=>.2f+.8f*Smooth((distance-.8f)/(ThinNear-.8f));
        /// <summary>Rubble freshness: 1 at the landing, 0 (dust coat) 10 s later.</summary>
        public static float Freshness(float age)=>age<0?1:1-Smooth(age/FreshFade);
        /// <summary>§3.9 telegraph: the segment's lit windows strobe at 8 Hz, then go black once the telegraph ends (1 = as lit).</summary>
        public static float WindowStrobe(float age,float telegraph)=>age<0?1:age<telegraph?((int)Math.Floor(age*WindowHz*2)%2==0?1:.12f):0;
        /// <summary>§2.3 band lamp: red strobe at the last damage stage (on/off at 4 Hz on the presentation clock).</summary>
        public static float LampStrobe(float time)=>(int)Math.Floor(time*LampHz*2)%2==0?1:0;
        /// <summary>Visual-only sag over the telegraph, 0..1 of <see cref="SagDegrees"/> / <see cref="SagMetres"/>.</summary>
        public static float Sag(float age,float telegraph)=>age<=0?0:Smooth(age/Math.Max(.05f,telegraph));
        public static float FallSeconds(float radius)=>Math.Max(.6f,Math.Min(1.2f,.55f+.05f*radius));
        /// <summary>The crush box when the event names none: a square footprint around the centroid, floor to above the top.</summary>
        public static void DefaultBox(V3 center,float radius,float floor,out V3 boxCenter,out V3 boxHalf)
        {float r=Math.Max(.5f,radius),top=Math.Max(floor+1,center.y+r*.9f),h=r*.55f;boxHalf=new V3(h,(top-floor)*.5f,h);boxCenter=new V3(center.x,(top+floor)*.5f,center.z);}
        public static int BaseColor(NativeStructureMaterial m)=>m==NativeStructureMaterial.Masonry?Brick:m==NativeStructureMaterial.Timber?Timber:m==NativeStructureMaterial.Metal?Steel:m==NativeStructureMaterial.Glass?Glass:Concrete;
        public static int FreshColor(NativeStructureMaterial m)=>m==NativeStructureMaterial.Masonry?BrickFresh:m==NativeStructureMaterial.Timber?TimberFresh:m==NativeStructureMaterial.Metal?SteelFresh:m==NativeStructureMaterial.Glass?Glint:ConcreteFresh;
        public static int DustColor(NativeStructureMaterial m)=>m==NativeStructureMaterial.Masonry?Mortar:m==NativeStructureMaterial.Timber?Sawdust:m==NativeStructureMaterial.Concrete?ConcreteDust:Dust;
        public static void Rgb(int hex,out float r,out float g,out float b){r=((hex>>16)&255)/255f;g=((hex>>8)&255)/255f;b=(hex&255)/255f;}
        private static void Lerp(int a,int b,float t,out float r,out float g,out float bl){Rgb(a,out float ar,out float ag,out float ab);Rgb(b,out float br,out float bg,out float bb);r=ar+(br-ar)*t;g=ag+(bg-ag)*t;bl=ab+(bb-ab)*t;}
        private static float H(uint s,int a,int b=0,int c=0)=>(float)NativeWoundCarve.Hash(s,a,b,c);
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float EaseOut(float x){x=Clamp01(x);float k=1-x;return 1-k*k*k;}
        private static float Len(V3 v)=>(float)Math.Sqrt(v.x*v.x+v.y*v.y+v.z*v.z);
        private static V3 Flat(V3 v){float l=(float)Math.Sqrt(v.x*v.x+v.z*v.z);return l<1e-4f?new V3(1,0,0):new V3(v.x/l,0,v.z/l);}
        private static void AxisAngle(V3 axis,float angle,out float qx,out float qy,out float qz,out float qw)
        {float l=Len(axis);if(l<1e-6f){qx=qy=qz=0;qw=1;return;}float s=(float)Math.Sin(angle*.5f)/l;qx=axis.x*s;qy=axis.y*s;qz=axis.z*s;qw=(float)Math.Cos(angle*.5f);}
        private static void Mul(float ax,float ay,float az,float aw,float bx,float by,float bz,float bw,out float x,out float y,out float z,out float w)
        {x=aw*bx+ax*bw+ay*bz-az*by;y=aw*by-ax*bz+ay*bw+az*bx;z=aw*bz+ax*by-ay*bx+az*bw;w=aw*bw-ax*bx-ay*by-az*bz;}
        /// <summary>The rotation taking +Z onto <paramref name="d"/>.</summary>
        private static void FromZ(V3 d,out float qx,out float qy,out float qz,out float qw)
        {
            float l=Len(d);if(l<1e-6f){qx=qy=qz=0;qw=1;return;}float x=d.x/l,y=d.y/l,z=d.z/l,r=z+1;
            if(r<1e-6f){qx=0;qy=1;qz=0;qw=0;return;}qx=-y;qy=x;qz=0;qw=r;float n=(float)Math.Sqrt(qx*qx+qy*qy+qw*qw);qx/=n;qy/=n;qw/=n;
        }
        private static V3 Axis(uint s,int i,int k){float u=H(s,i,k)*2-1,a=H(s,i,k+1)*6.2831855f,r=(float)Math.Sqrt(Math.Max(0,1-u*u));return new V3(r*(float)Math.Cos(a),u,r*(float)Math.Sin(a));}
        /// <summary>
        /// A thrown piece: ballistic under <paramref name="g"/> to <paramref name="floor"/>, one bounce of a quarter of the impact
        /// speed and a short decaying slide. Pure in t. Returns the landing time.
        /// </summary>
        public static float Throw(V3 p0,V3 v0,float g,float floor,float t,out V3 p)
        {
            float drop=Math.Max(0,p0.y-floor),hit=(v0.y+(float)Math.Sqrt(Math.Max(0,v0.y*v0.y+2*g*drop)))/g;if(p0.y<floor)hit=0;
            if(t<hit){p=new V3(p0.x+v0.x*t,p0.y+v0.y*t-.5f*g*t*t,p0.z+v0.z*t);return hit;}
            float vy=Math.Abs(v0.y-g*hit)*.24f,tt=t-hit,bounce=2*vy/g,y=tt<bounce?vy*tt-.5f*g*tt*tt:0,slide=(1-(float)Math.Exp(-tt*5))*.09f;
            p=new V3(p0.x+v0.x*(hit+slide),floor+y,p0.z+v0.z*(hit+slide));return hit;
        }
        private static float SpinTime(float t,float hit)=>t<hit?t:hit+.28f*(1-(float)Math.Exp(-(t-hit)*5));

        // ---------- output ----------
        private bool Cube(V3 c,V3 s,float qx,float qy,float qz,float qw,float r,float g,float b)
        {if(CubeCount>=CubeCap||s.x<=.002f||s.y<=.002f||s.z<=.002f)return false;Cubes[CubeCount++]=new NativeStructureCube{Center=c,Size=s,Qx=qx,Qy=qy,Qz=qz,Qw=qw,R=r,G=g,B=b};return true;}
        private bool Box(V3 c,V3 s,int hex){Rgb(hex,out float r,out float g,out float b);return Cube(c,s,0,0,0,1,r,g,b);}
        private bool Puff(V3 c,float w,float h,float r,float g,float b,float a,float top,float bottom,V3 eye)
        {if(PuffCount>=PuffCap)return false;a*=NearThin(Len(c-eye));if(a<=.004f||w<=.01f)return true;Puffs[PuffCount++]=new NativeStructurePuff{Center=c,Width=w,Height=h,R=r,G=g,B=b,Alpha=a,Top=top,Bottom=bottom};return true;}
        private void Quad(V3 c,V3 u,V3 v,int hex,float a,bool soft,bool lit){if(FlatCount>=FlatCap||a<=.004f)return;Rgb(hex,out float r,out float g,out float b);Flats[FlatCount++]=new NativeStructureFlat{Center=c,U=u,V=v,R=r,G=g,B=b,Alpha=a,Soft=soft,Lit=lit};}
        private void Line(V3 a,V3 b,float w,float r,float g,float bl,float alpha,bool additive){if(LineCount>=LineCap||alpha<=.004f)return;Lines[LineCount++]=new NativeStructureLine{From=a,To=b,Width=w,R=r,G=g,B=bl,Alpha=alpha,Additive=additive};}
        private void Glow(V3 c,float size,int hex,float a){if(GlowCount>=GlowCap||a<=.004f)return;Rgb(hex,out float r,out float g,out float b);Glows[GlowCount++]=new NativeStructureGlow{Center=c,Size=size,R=r,G=g,B=b,Alpha=a};}

        // ---------- intake ----------
        private int Oldest<T>(T[] list,Func<T,bool> live,Func<T,float> born)
        {int best=0;float at=float.MaxValue;for(int i=0;i<list.Length;i++){if(!live(list[i]))return i;float b=born(list[i]);if(b<at){at=b;best=i;}}return best;}
        private static readonly Func<Burst,bool> BurstLive=b=>b.Live;private static readonly Func<Burst,float> BurstBorn=b=>b.Born;
        private static readonly Func<Record,bool> RecordLive=r=>r.Live;private static readonly Func<Record,float> RecordBorn=r=>r.Born;
        private static readonly Func<Shake,bool> ShakeLive=s=>s.Live;private static readonly Func<Shake,float> ShakeBorn=s=>s.Born;
        private static readonly Func<Mock,bool> MockLive=m=>m.Live;private static readonly Func<Mock,float> MockBorn=m=>m.Born;
        /// <summary>A Break event: the block's material kind and content colour (0: the kind's own), centre, full size and the floor
        /// under it. <paramref name="decals"/>: also leave chip and crack marks below (Brickfall, <see cref="MapMarks"/>).</summary>
        public void Break(NativeStructureMaterial material,int color,V3 center,V3 size,float floor,uint seed,float time,bool decals)
        {
            bursts[Oldest(bursts,BurstLive,BurstBorn)]=new Burst{Live=true,Material=material,Center=center,Size=new V3(Math.Max(.2f,size.x),Math.Max(.2f,size.y),Math.Max(.2f,size.z)),Color=color,Floor=Math.Min(floor,center.y),Born=time,Seed=seed};
            if(!decals)return;float s=BurstScale(size);V3 at=new V3(center.x,floor,center.z),up=new V3(0,1,0);
            AddMark(NativeStructureMark.Chip,at,up,(.7f+.4f*H(seed,1,90))*s,seed^MarkSalt,time);AddMark(NativeStructureMark.Crack,at,up,(1f+.5f*H(seed,2,90))*s,seed^MarkSalt^1,time);
        }
        /// <summary>StructureStress (§5.4: Position = centroid, Radius = extent, Amount = telegraph seconds). The crush box is the
        /// sim's crush volume when the hook has it (half extents zero: <see cref="DefaultBox"/>); floor is the ground under it.</summary>
        public void Stress(V3 centroid,float radius,float telegraph,V3 boxCenter,V3 boxHalf,float floor,uint seed,float time)
        {
            if(!(boxHalf.x>0&&boxHalf.y>0&&boxHalf.z>0))DefaultBox(centroid,radius,floor,out boxCenter,out boxHalf);telegraph=Math.Max(.4f,Math.Min(4,telegraph>0?telegraph:1.2f));
            records[Oldest(records,RecordLive,RecordBorn)]=new Record{Live=true,Cue=NativeStructureCue.Stress,Center=centroid,BoxCenter=boxCenter,BoxHalf=boxHalf,Radius=Math.Max(.5f,radius),Amount=telegraph,Floor=floor,Born=time,Seed=seed^StressSalt,Base=seed};
            AddShake(NativeStructureCue.Stress,centroid,.5f,telegraph,seed^StressSalt,time);
        }
        /// <summary>StructureFall (§5.4: Normal = fall direction, Amount = mass in tonnes, SurfaceId = dominant material).</summary>
        public void Fall(V3 position,float radius,V3 direction,float tonnes,NativeStructureMaterial material,float floor,uint seed,float time)
        {
            records[Oldest(records,RecordLive,RecordBorn)]=new Record{Live=true,Cue=NativeStructureCue.Fall,Material=material,Center=position,Normal=Flat(direction),Radius=Math.Max(.5f,radius),Amount=Math.Max(0,tonnes),Floor=floor,Born=time,Seed=seed^FallSalt,Base=seed};
            AddShake(NativeStructureCue.Fall,position,tonnes,0,seed^FallSalt,time);
        }
        /// <summary>StructureLand (§5.4: settle and rubble swap; Amount = energy 0..1). Land carries no material: pass the one its
        /// fall named (<see cref="FallMaterial"/>).</summary>
        public void Land(V3 position,float radius,float energy,NativeStructureMaterial material,float floor,uint seed,float time,bool decals=true)
        {
            records[Oldest(records,RecordLive,RecordBorn)]=new Record{Live=true,Cue=NativeStructureCue.Land,Material=material,Center=position,Radius=Math.Max(.5f,radius),Amount=Clamp01(energy),Floor=floor,Born=time,Seed=seed^LandSalt,Base=seed};
            AddShake(NativeStructureCue.Land,position,energy,0,seed^LandSalt,time);if(!decals)return;
            float r=Math.Max(.5f,radius),k=Math.Min(1,r/5);V3 up=new V3(0,1,0);uint s=seed^LandSalt^MarkSalt;
            AddMark(NativeStructureMark.Stain,new V3(position.x,floor,position.z),up,r*2.2f,s,time);
            for(int i=0;i<8;i++){float a=i*.785398f+H(s,i,1)*.5f,d=r*(.9f+.4f*H(s,i,2));AddMark(NativeStructureMark.Crack,new V3(position.x+(float)Math.Cos(a)*d,floor,position.z+(float)Math.Sin(a)*d),up,(1.4f+H(s,i,3))*k,s+(uint)i*7919u,time);}
        }
        /// <summary>The material the newest fall of the same segment (same base seed) named, else <paramref name="fallback"/>.</summary>
        public NativeStructureMaterial FallMaterial(uint seed,NativeStructureMaterial fallback)
        {float at=float.MinValue;var m=fallback;for(int i=0;i<EventSlots;i++){var r=records[i];if(r.Live&&r.Cue==NativeStructureCue.Fall&&r.Base==seed&&r.Born>at){at=r.Born;m=r.Material;}}return m;}
        /// <summary>A decal into the 256-slot ring buffer (the oldest is overwritten); normal is the surface it lies on.</summary>
        public void AddMark(NativeStructureMark kind,V3 center,V3 normal,float size,uint seed,float time)
        {var n=Len(normal)<1e-4f?new V3(0,1,0):normal.Normalized;marks[markCursor]=new Mark{Live=true,Kind=kind,Center=center,Normal=n,Size=Math.Max(.05f,size),Born=time,Seed=seed};markCursor=(markCursor+1)%DecalSlots;}
        /// <summary>Dev preview stand-in (<see cref="NativeStructurePreview"/>): a three-storey quadrant at the spot that sags,
        /// pancakes and swaps to rubble on the preview's stress / fall / land schedule.</summary>
        public void Preview(V3 spot,NativeStructureMaterial material,float floor,uint seed,float time)
        {mocks[Oldest(mocks,MockLive,MockBorn)]=new Mock{Live=true,Material=material,Spot=spot,Floor=floor,Born=time,Seed=seed^MockSalt};}
        private void AddShake(NativeStructureCue cue,V3 at,float amount,float telegraph,uint seed,float time)
        {shakes[Oldest(shakes,ShakeLive,ShakeBorn)]=new Shake{Live=true,Rise=cue==NativeStructureCue.Stress,At=at,Peak=NativeStructureShake.Peak(cue,amount),Born=time,Duration=NativeStructureShake.Duration(cue,telegraph),Seed=seed};}

        // ---------- queries (hooks for the block shader, dressing and camera) ----------
        /// <summary>Whether <paramref name="p"/> lies in a live stress telegraph's crush box (grown .5 m): its age and length.</summary>
        public bool StressAt(V3 p,float now,out float age,out float telegraph)
        {
            age=telegraph=0;float best=float.MaxValue;bool found=false;
            for(int i=0;i<EventSlots;i++)
            {
                var r=records[i];if(!r.Live||r.Cue!=NativeStructureCue.Stress)continue;float a=now-r.Born;if(a<0||a>r.Amount+StressTail)continue;
                V3 d=p-r.BoxCenter;if(Math.Abs(d.x)>r.BoxHalf.x+.5f||Math.Abs(d.y)>r.BoxHalf.y+.5f||Math.Abs(d.z)>r.BoxHalf.z+.5f)continue;if(a<best){best=a;age=a;telegraph=r.Amount;found=true;}
            }
            return found;
        }
        /// <summary>The window light multiplier at p (1 outside every telegraph).</summary>
        public float WindowAt(V3 p,float now)=>StressAt(p,now,out float age,out float telegraph)?WindowStrobe(age,telegraph):1;
        public float SagAt(V3 p,float now)=>StressAt(p,now,out float age,out float telegraph)?Sag(Math.Min(age,telegraph),telegraph):0;
        /// <summary>Rubble freshness at p: 1 under a fresh landing, fading to 0 over 10 s (0 where nothing landed).</summary>
        public float FreshnessAt(V3 p,float now)
        {
            float best=0;for(int i=0;i<EventSlots;i++){var r=records[i];if(!r.Live||r.Cue!=NativeStructureCue.Land)continue;float a=now-r.Born;if(a<0||a>=LandLife)continue;V3 d=p-r.Center;d.y=0;if(Len(d)>r.Radius*1.3f+1)continue;best=Math.Max(best,Freshness(a));}
            return best;
        }
        /// <summary>The camera offset of every live structure shake heard at <paramref name="eye"/> (at most .3 m).</summary>
        public V3 ShakeOffset(float now,V3 eye)
        {
            float x=0,y=0,z=0;
            for(int i=0;i<ShakeSlots;i++)
            {
                var s=shakes[i];if(!s.Live)continue;float t=now-s.Born;if(t<0)continue;if(t>s.Duration){shakes[i].Live=false;continue;}
                float amp=s.Peak*NativeStructureShake.Falloff(Len(eye-s.At))*NativeStructureShake.Envelope(s.Rise,t,s.Duration);if(amp<=0)continue;
                x+=amp*Noise(s.Seed,0,t*21);y+=amp*.75f*Noise(s.Seed,1,t*27);z+=amp*.5f*Noise(s.Seed,2,t*17);
            }
            float l=(float)Math.Sqrt(x*x+y*y+z*z);if(l>NativeStructureShake.MaxOffset){float k=NativeStructureShake.MaxOffset/l;x*=k;y*=k;z*=k;}return new V3(x,y,z);
        }
        private static float Noise(uint seed,int ch,float x){int i=(int)Math.Floor(x);float f=x-i,u=f*f*(3-2*f),a=H(seed,ch,i)*2-1,b=H(seed,ch,i+1)*2-1;return a+(b-a)*u;}

        // ---------- frame ----------
        /// <summary>Fills the draw lists for <paramref name="now"/> seen from <paramref name="eye"/> at effects quality
        /// <paramref name="quality"/> (1 full, .3 reduced). Expired records are freed; nothing allocates.</summary>
        public void Frame(float now,V3 eye,float quality)
        {
            CubeCount=PuffCount=FlatCount=LineCount=GlowCount=0;float q=Clamp01(quality);
            for(int i=0;i<BurstSlots;i++)if(bursts[i].Live&&now-bursts[i].Born>=BurstLife)bursts[i].Live=false;
            for(int i=0;i<EventSlots;i++){var r=records[i];if(r.Live&&now-r.Born>=(r.Cue==NativeStructureCue.Land?LandLife:r.Cue==NativeStructureCue.Fall?FallLife:r.Amount+StressTail))records[i].Live=false;}
            for(int i=0;i<MockSlots;i++)if(mocks[i].Live&&now-mocks[i].Born>=NativeStructurePreview.LandAt+LandLife)mocks[i].Live=false;
            for(int i=0;i<DecalSlots;i++)if(marks[i].Live)MarkFrame(marks[i],now);
            int n=0;for(int i=0;i<EventSlots;i++)if(records[i].Live)recordOrder[n++]=i;
            for(int i=1;i<n;i++){int k=recordOrder[i],j=i-1;while(j>=0&&records[recordOrder[j]].Born<records[k].Born){recordOrder[j+1]=recordOrder[j];j--;}recordOrder[j+1]=k;}
            // Telegraphs claim their billboards first and are never reduced; then the preview stand-in, the newest landings, falls.
            for(int i=0;i<n;i++)if(records[recordOrder[i]].Cue==NativeStructureCue.Stress)StressFrame(records[recordOrder[i]],now,eye);
            for(int i=0;i<MockSlots;i++)if(mocks[i].Live)MockFrame(mocks[i],now);
            for(int i=0;i<n;i++)if(records[recordOrder[i]].Cue==NativeStructureCue.Land)LandFrame(records[recordOrder[i]],now,eye,q);
            for(int i=0;i<n;i++)if(records[recordOrder[i]].Cue==NativeStructureCue.Fall)FallFrame(records[recordOrder[i]],now,eye,q);
            int m=0;for(int i=0;i<BurstSlots;i++)if(bursts[i].Live)burstOrder[m++]=i;
            for(int i=1;i<m;i++){int k=burstOrder[i],j=i-1;while(j>=0&&bursts[burstOrder[j]].Born<bursts[k].Born){burstOrder[j+1]=burstOrder[j];j--;}burstOrder[j+1]=k;}
            for(int i=0;i<m;i++)BurstFrame(bursts[burstOrder[i]],now,eye,q);
        }

        // ---------- material break bursts ----------
        public static float BurstScale(V3 size)=>Math.Max(.55f,Math.Min(1.6f,(float)Math.Pow(Math.Max(.001,size.x*size.y*size.z),1/3.0)/1.1f));
        /// <summary>Piece counts of one break at full quality (before <see cref="Reduced"/>): solid pieces, grit, puffs, sparks, glints.</summary>
        public static void BurstCounts(NativeStructureMaterial m,float scale,out int pieces,out int grit,out int puffs,out int sparks)
        {
            float s=scale;pieces=grit=puffs=sparks=0;
            switch(m)
            {
                case NativeStructureMaterial.Masonry:pieces=(int)Math.Round(22*s);grit=(int)Math.Round(12*s);puffs=s>1?7:5;break;
                case NativeStructureMaterial.Concrete:pieces=(int)Math.Round(14*s);grit=(int)Math.Round(16*s);puffs=s>1?8:6;break;
                case NativeStructureMaterial.Timber:pieces=(int)Math.Round(16*s);grit=(int)Math.Round(8*s);puffs=4;break;
                case NativeStructureMaterial.Metal:pieces=(int)Math.Round(5*s);sparks=(int)Math.Round(20*s);break;
                case NativeStructureMaterial.Glass:pieces=(int)Math.Round(20*s);sparks=pieces;break;
            }
        }
        private static void Launch(uint seed,int i,int ch,V3 c,V3 half,float lo,float hi,float upLo,float upHi,out V3 p0,out V3 v0)
        {
            float hx=H(seed,i,ch)-.5f,hy=H(seed,i,ch+1)-.5f,hz=H(seed,i,ch+2)-.5f;p0=new V3(c.x+hx*half.x*1.6f,c.y+hy*half.y*1.6f,c.z+hz*half.z*1.6f);
            float a=(float)Math.Atan2(hz,hx)+(H(seed,i,ch+3)-.5f)*1.1f,sp=lo+(hi-lo)*H(seed,i,ch+4),up=upLo+(upHi-upLo)*H(seed,i,ch+5);
            v0=new V3((float)Math.Cos(a)*sp,up+hy*2.5f,(float)Math.Sin(a)*sp);
        }
        private void BurstFrame(in Burst b,float now,V3 eye,float q)
        {
            float t=now-b.Born;if(t<0||t>=BurstLife)return;float s=BurstScale(b.Size),k=Reduced(q),root=(float)Math.Sqrt(s);V3 c=b.Center,half=b.Size*.5f;uint seed=b.Seed;
            int baseHex=b.Color!=0?b.Color:BaseColor(b.Material);BurstCounts(b.Material,s,out int pieces,out int grit,out int puffs,out int sparks);
            pieces=(int)Math.Round(pieces*k);grit=(int)Math.Round(grit*k);puffs=Math.Max(1,(int)Math.Round(puffs*k));sparks=(int)Math.Round(sparks*k);
            var m=b.Material;
            for(int i=0;i<pieces;i++)
            {
                float life=1.8f+.8f*H(seed,i,20);if(t>=life)continue;V3 dim;float lo,hi,upLo,upHi,spin;int hex;float pick=H(seed,i,21);
                switch(m)
                {
                    case NativeStructureMaterial.Masonry:dim=new V3(.21f,.075f,.105f)*(.85f+.3f*H(seed,i,22));lo=1.2f;hi=4.2f;upLo=1;upHi=3.5f;spin=7;hex=pick<.6f?baseHex:pick<.85f?BrickDark:BrickFresh;break;
                    case NativeStructureMaterial.Timber:dim=new V3(.04f,.035f,.22f+.33f*H(seed,i,22))*root;lo=1.8f;hi=5;upLo=1.2f;upHi=4;spin=9;hex=pick<.5f?baseHex:TimberFresh;break;
                    case NativeStructureMaterial.Metal:dim=new V3(.18f+.16f*H(seed,i,22),.025f,.12f+.14f*H(seed,i,23));lo=1.5f;hi=4;upLo=1.5f;upHi=3.5f;spin=6;hex=baseHex;break;
                    case NativeStructureMaterial.Glass:dim=new V3(.08f+.1f*H(seed,i,22),.012f,.06f+.08f*H(seed,i,23));lo=1.5f;hi=4.5f;upLo=.5f;upHi=3;spin=12;hex=pick<.5f?0xb9dbe2:0xe2f2f5;break;
                    default:dim=new V3((.12f+.18f*H(seed,i,22))*(.7f+.6f*H(seed,i,23)),(.12f+.18f*H(seed,i,22))*(.7f+.6f*H(seed,i,24)),(.12f+.18f*H(seed,i,22))*(.7f+.6f*H(seed,i,25)))*root;lo=1.2f;hi=4;upLo=1;upHi=3.2f;spin=6;hex=pick<.55f?baseHex:ConcreteFresh;break;
                }
                Launch(seed,i,30,c,half,lo,hi,upLo,upHi,out V3 p0,out V3 v0);float floor=b.Floor+Math.Min(dim.x,Math.Min(dim.y,dim.z))*.5f;float hit=Throw(p0,v0,Gravity,floor,t,out V3 p);
                AxisAngle(Axis(seed,i,40),spin*(.4f+.6f*H(seed,i,42))*SpinTime(t,hit),out float qx,out float qy,out float qz,out float qw);
                if(m==NativeStructureMaterial.Timber){FromZ(v0,out float ax,out float ay,out float az,out float aw);Mul(qx,qy,qz,qw,ax,ay,az,aw,out qx,out qy,out qz,out qw);}
                float shrink=Math.Min(1,(life-t)/.4f),tint=.88f+.24f*H(seed,i,26);Rgb(hex,out float r,out float g,out float bl);
                Cube(p,dim*shrink,qx,qy,qz,qw,Math.Min(1,r*tint),Math.Min(1,g*tint),Math.Min(1,bl*tint));
                if(m==NativeStructureMaterial.Glass&&i<sparks)
                {
                    // A glint: the shard's face turns through the light about twice a second; full quality glints every shard,
                    // reduced every other one.
                    if(q<.5f&&i%2==1)continue;float glint=(float)Math.Pow(Math.Max(0,Math.Sin(t*spin*.9f+H(seed,i,43)*6.2831855f)),24)*shrink;if(glint<.02f)continue;
                    Glow(p,.12f,Glint,glint);Line(p+new V3(-.14f,0,0),p+new V3(.14f,0,0),.012f,.96f,.98f,1,glint,true);Line(p+new V3(0,-.14f,0),p+new V3(0,.14f,0),.012f,.96f,.98f,1,glint,true);
                }
            }
            int gritHex=m==NativeStructureMaterial.Masonry?Mortar:m==NativeStructureMaterial.Timber?TimberFresh:m==NativeStructureMaterial.Concrete?ConcreteDust:Grit;
            for(int i=0;i<grit;i++)
            {
                float life=1.1f+.5f*H(seed,i,50);if(t>=life)continue;float e=.035f+.03f*H(seed,i,51);Launch(seed,i,52,c,half,2,5.5f,1.2f,4,out V3 p0,out V3 v0);Throw(p0,v0,Gravity,b.Floor+e*.5f,t,out V3 p);
                float shrink=Math.Min(1,(life-t)/.3f);Rgb(gritHex,out float r,out float g,out float bl);Cube(p,new V3(e,e,e)*shrink,0,0,0,1,r,g,bl);
            }
            if(m!=NativeStructureMaterial.Metal&&m!=NativeStructureMaterial.Glass)
            {
                // The puff: the first is the pop at the block's centre, the rest roll out from its faces and settle.
                int dust=DustColor(m);Rgb(dust,out float r,out float g,out float bl);float big=m==NativeStructureMaterial.Concrete?1.3f:m==NativeStructureMaterial.Timber?.8f:1,peak=m==NativeStructureMaterial.Timber?.38f:m==NativeStructureMaterial.Concrete?.5f:.52f;
                for(int i=0;i<puffs;i++)
                {
                    float born=i*.03f,a=t-born,life=(m==NativeStructureMaterial.Concrete?1.35f:1f)+.4f*H(seed,i,60);if(a<0||a>=life)continue;float u=a/life;
                    V3 off=i==0?new V3():new V3((H(seed,i,61)-.5f)*half.x*1.4f,(H(seed,i,62)-.45f)*half.y,(H(seed,i,63)-.5f)*half.z*1.4f),dir=Flat(off);
                    float w=(i==0?1.2f:.65f+.5f*H(seed,i,64))*s*big*(.7f+1.3f*EaseOut(u));V3 at=c+off+dir*(.6f*EaseOut(u))+new V3(0,.3f*u-.25f*u*u,0)+Wind*a;
                    Puff(at,w,w*.85f,r,g,bl,peak*(float)Math.Pow(1-u,1.5f)*Math.Min(1,a/.05f),1.1f,.85f,eye);
                }
            }
            if(m==NativeStructureMaterial.Metal)
            {
                for(int i=0;i<sparks;i++)
                {
                    float life=.3f+.35f*H(seed,i,70);if(t>=life)continue;float u=t/life;Launch(seed,i,71,c,half,4,10,1,5,out V3 p0,out V3 v0);
                    Throw(p0,v0,9.8f,b.Floor,t,out V3 head);Throw(p0,v0,9.8f,b.Floor,Math.Max(0,t-.035f),out V3 tail);Lerp(SparkHot,SparkCool,u,out float r,out float g,out float bl);Line(tail,head,.022f,r,g,bl,1-u,true);
                }
                // The clank: a white-hot flash and a short orange core (additive sprites, no Unity light).
                if(t<.14f)Glow(c,1.5f*s,Flash,(1-t/.14f)*(1-t/.14f));if(t<.3f)Glow(c,.7f*s,FlashCore,1-t/.3f);
            }
        }

        // ---------- stress telegraph (never reduced) ----------
        private void StressFrame(in Record r,float now,V3 eye)
        {
            float t=now-r.Born,T=r.Amount;if(t<0||t>T+StressTail)return;float on=Clamp01(t/.2f),off=t<T?1:1-(t-T)/StressTail,ramp=.4f+.6f*Clamp01(t/T);uint seed=r.Seed;
            V3 bc=r.BoxCenter,bh=r.BoxHalf;float floor=r.Floor,top=bc.y+bh.y,y=floor+.03f;
            // The red crush footprint from the same box the sim crushes in: a faint fill and pulsing edges.
            float pulse=on*off*(.75f+.25f*(float)Math.Sin(t*6.2831855f*4));
            Quad(new V3(bc.x,y,bc.z),new V3(bh.x,0,0),new V3(0,0,bh.z),Danger,.16f*pulse,false,false);
            Quad(new V3(bc.x,y+.002f,bc.z+bh.z),new V3(bh.x,0,0),new V3(0,0,.07f),Danger,.75f*pulse,false,false);Quad(new V3(bc.x,y+.002f,bc.z-bh.z),new V3(bh.x,0,0),new V3(0,0,.07f),Danger,.75f*pulse,false,false);
            Quad(new V3(bc.x+bh.x,y+.002f,bc.z),new V3(.07f,0,0),new V3(0,0,bh.z),Danger,.75f*pulse,false,false);Quad(new V3(bc.x-bh.x,y+.002f,bc.z),new V3(.07f,0,0),new V3(0,0,bh.z),Danger,.75f*pulse,false,false);
            float h=top-floor;Rgb(Grit,out float gr,out float gg,out float gb);Rgb(Dust,out float dr,out float dg,out float db);
            if(h>.5f)
            {
                // Grit columns at the four corners: a periodic emitter in closed form (spawn m at m*period until the telegraph
                // ends; each grain falls freely to the floor), with dust streaming down beside it.
                float fallT=(float)Math.Sqrt(2*h/9.8f);const int K=16;float period=fallT/K;
                for(int corner=0;corner<4;corner++)
                {
                    float cx=bc.x+((corner&1)==0?-bh.x:bh.x),cz=bc.z+((corner&2)==0?-bh.z:bh.z);
                    int last=(int)Math.Floor(Math.Min(t,T)/period),first=Math.Max(0,(int)Math.Ceiling((t-fallT)/period));
                    for(int j=first;j<=last;j++)
                    {
                        float born=j*period,tau=t-born;if(tau<0||tau>=fallT)continue;if(H(seed,corner,j)>.4f+.6f*Clamp01(born/T))continue;
                        float gy=top-4.9f*tau*tau-H(seed,corner,j+500)*tau,e=.05f+.05f*H(seed,corner,j+1000);if(gy<floor+e*.5f)continue;
                        Cube(new V3(cx+(H(seed,corner,j+1500)-.5f)*(.24f+.3f*tau),gy,cz+(H(seed,corner,j+2000)-.5f)*(.24f+.3f*tau)),new V3(e,e,e),0,0,0,1,gr,gg,gb);
                    }
                    float seg=h/3;for(int j=0;j<3;j++){float fall=(t*1.1f+j*seg)%h;Puff(new V3(cx,top-fall,cz),.8f+.4f*H(seed,corner,j+2500),seg*1.2f,dr,dg,db,.3f*ramp*on*off,1.15f,.9f,eye);}
                }
            }
            // Dust trickles from six cracks on the box faces: a puff every .45 s slides down and fades, with two grains.
            for(int k=0;k<6;k++)
            {
                int face=k%4;float fu=(H(seed,k,3000)-.5f)*1.6f,fy=bc.y+(H(seed,k,3001)-.4f)*bh.y*1.4f;
                V3 p=face==0?new V3(bc.x+bh.x,fy,bc.z+fu*bh.z):face==1?new V3(bc.x-bh.x,fy,bc.z+fu*bh.z):face==2?new V3(bc.x+fu*bh.x,fy,bc.z+bh.z):new V3(bc.x+fu*bh.x,fy,bc.z-bh.z);
                V3 n=face==0?new V3(1,0,0):face==1?new V3(-1,0,0):face==2?new V3(0,0,1):new V3(0,0,-1);float phase=H(seed,k,3002)*.45f;
                for(int j=0;j<3;j++)
                {
                    int slot=(int)Math.Floor((t-phase)/.45f)-j;float born=phase+slot*.45f,tau=t-born;if(slot<0||born>T||tau<0||tau>=.9f)continue;float u=tau/.9f;
                    Puff(p+n*.15f+new V3(0,-1.1f*tau,0),.4f+.7f*u,(.4f+.7f*u)*1.3f,dr,dg,db,.45f*(1-u)*on,1.15f,.9f,eye);
                    for(int g=0;g<2;g++){float gy=p.y-4.9f*tau*tau;if(gy>floor)Cube(p+n*(.08f+.1f*g)+new V3(0,gy-p.y,0),new V3(.05f,.05f,.05f),0,0,0,1,gr,gg,gb);}
                }
            }
        }

        // ---------- fall: streamers and snapping wires ----------
        private V3 FallPath(V3 p0,V3 dir,float radius,float duration,float floor,float t)
        {float u=Math.Min(1,t/Math.Max(.05f,duration));V3 p=p0+dir*(radius*.35f*u*u)+new V3(0,-9f*t*t,0);if(p.y<floor)p.y=floor;return p;}
        private void FallFrame(in Record r,float now,V3 eye,float q)
        {
            float t=now-r.Born;if(t<0||t>=FallLife)return;float D=FallSeconds(r.Radius),R=r.Radius;uint seed=r.Seed;V3 c=r.Center,dir=r.Normal;float floor=r.Floor;
            Lerp(Dust,FreshColor(r.Material),.2f,out float dr,out float dg,out float db);int baseHex=BaseColor(r.Material),fresh=FreshColor(r.Material);
            int streamers=q<.5f?3:Streamers;
            for(int k=0;k<streamers;k++)
            {
                V3 p0=c+new V3((H(seed,k,1)-.5f)*R,R*.35f+(H(seed,k,2)-.5f)*R*.3f,(H(seed,k,3)-.5f)*R);
                // Puffs left behind every .11 s while the mass is still above the floor; each lingers 1.8 s.
                for(int m=0;m*.11f<=Math.Min(t,D);m++)
                {
                    float born=m*.11f,a=t-born;if(a>=1.8f)continue;V3 at=FallPath(p0,dir,R,D,floor,born);if(at.y<floor+.3f)break;float u=a/1.8f;
                    float w=.6f+1.6f*EaseOut(u);Puff(at+Wind*a+new V3(0,.25f*a,0),w,w*1.2f,dr,dg,db,.45f*(float)Math.Pow(1-u,1.4f),1.2f,.88f,eye);
                }
                for(int j=0;j<3;j++)
                {
                    float lag=t-.05f*j;if(lag<0)continue;V3 at=FallPath(p0,dir,R,D,floor,lag)+new V3((H(seed,k,10+j)-.5f)*.8f,0,(H(seed,k,20+j)-.5f)*.8f);if(at.y<=floor+.05f)continue;
                    float e=.14f+.18f*H(seed,k,30+j);AxisAngle(Axis(seed,k*3+j,40),8*lag,out float qx,out float qy,out float qz,out float qw);Rgb(H(seed,k,50+j)<.5f?baseHex:fresh,out float cr,out float cg,out float cb);Cube(at,new V3(e,e*.8f,e*1.2f),qx,qy,qz,qw,cr,cg,cb);
                }
            }
            Rgb(Black,out float wr,out float wg,out float wb);
            for(int k=0;k<Wires;k++)
            {
                float ang=H(seed,k,60)*6.2831855f;V3 out1=new V3((float)Math.Cos(ang),0,(float)Math.Sin(ang));
                // Cables from the top of the mass to stakes on the ground: they snap; the staked half drops flat, the other half
                // trails from the falling mass.
                V3 anchor=new V3(c.x+out1.x*R*(1.3f+.4f*H(seed,k,61)),floor+.05f,c.z+out1.z*R*(1.3f+.4f*H(seed,k,61))),start=c+out1*(R*.45f)+new V3(0,R*.45f,0);float snap=.06f+.09f*k+.05f*H(seed,k,62);
                V3 end=FallPath(start,dir,R,D,floor,t);
                if(t<snap){Line(anchor,end,.03f,wr,wg,wb,1,false);continue;}
                float tau=t-snap;V3 brk=FallPath(start,dir,R,D,floor,snap),mid=(anchor+brk)*.5f;float L=Len(brk-anchor)*.5f;
                Whip(anchor,(mid-anchor).Normalized,Flat(mid-anchor),L,tau,floor,wr,wg,wb);Whip(end,(mid-brk).Normalized,new V3(0,-1,0),L,tau,floor,wr,wg,wb);
                if(tau<.12f){float f=1-tau/.12f;Glow(mid,.6f,Flash,f*f);for(int s2=0;s2<5;s2++){V3 d=Axis(seed,k*5+s2,70);Line(mid+d*(tau*6),mid+d*(tau*6+.12f),.018f,1,.85f,.55f,f,true);}}
            }
        }
        // A snapped half: it swings from its fixed end toward its rest direction (hanging down, or lying flat from a stake) with a
        // damped overshoot, two segments sagging, never below the floor.
        private void Whip(V3 fixedEnd,V3 from,V3 rest,float length,float tau,float floor,float r,float g,float b)
        {
            float w=1-(float)Math.Exp(-4*tau)*(float)Math.Cos(10*tau);V3 d=(from+(rest-from)*w).Normalized;
            V3 m=fixedEnd+d*(length*.5f)+new V3(0,-.12f*length*(1-Clamp01(w)),0),e=fixedEnd+d*length;m.y=Math.Max(m.y,floor+.03f);e.y=Math.Max(e.y,floor+.03f);Line(fixedEnd,m,.03f,r,g,b,1,false);Line(m,e,.03f,r,g,b,1,false);
        }

        // ---------- landing: dust wall, shock skirt, debris voxels, wisps ----------
        private void LandFrame(in Record r,float now,V3 eye,float q)
        {
            float t=now-r.Born;if(t<0||t>=LandLife)return;float e=r.Amount,size=Math.Max(.5f,Math.Min(1,r.Radius/5)),R=r.Radius,floor=r.Floor;uint seed=r.Seed;V3 c=r.Center;
            V3 drift=Wind*Math.Max(0,t-.8f);Lerp(Dust,FreshColor(r.Material),.3f,out float dr,out float dg,out float db);
            // The wall first (it is what the cap protects): golden-angle order, so a capped wall still rings the landing evenly.
            float o=WallOpacity(t)*(.8f+.2f*e);
            if(o>0)
            {
                int n=WallCount(e,q);
                for(int i=0;i<n;i++)
                {
                    bool core=i<WallCore;float a=i*2.3999632f+H(seed,i,1)*.5f,rad=core?(1.5f+3f*EaseOut(t/WallGrow))*size:WallRadius(t)*(.9f+.2f*H(seed,i,2))*size;
                    // The soft disk fades toward its rim: centred low (the ground hides its lower part, so the wall is densest at
                    // the base) and 1.45x the wall height, its visible top reaches the wall top.
                    float height=WallTop(t,e)*size*(.75f+.25f*H(seed,i,3))*(core?1.1f:1)*1.45f,width=height*(.7f+.3f*H(seed,i,4))+1.5f*size;
                    V3 p=new V3(c.x+(float)Math.Cos(a)*rad,floor+height*.3f,c.z+(float)Math.Sin(a)*rad)+drift;
                    if(!Puff(p,width,height,dr,dg,db,o*(core?.9f:1),1.28f,.9f,eye))break;
                }
            }
            if(t<.8f)
            {
                // The ground shock skirt: a low ring racing out in .45 s.
                Lerp(Dust,Grit,.5f,out float sr,out float sg,out float sb);int n=(int)Math.Round(SkirtCount*Reduced(q));float a0=.45f*(1-t/.8f)*(1-t/.8f);
                for(int i=0;i<n;i++){float a=i*6.2831855f/n+H(seed,i,10)*.3f,rad=R*.35f*size+9*size*EaseOut(t/.45f),hgt=.9f+.5f*H(seed,i,11);if(!Puff(new V3(c.x+(float)Math.Cos(a)*rad,floor+hgt*.45f,c.z+(float)Math.Sin(a)*rad),2.4f+.8f*H(seed,i,12),hgt,sr,sg,sb,a0,1.05f,.85f,eye))break;}
                float fs=(2+R*2.4f*EaseOut(t/.5f))*.5f;Quad(new V3(c.x,floor+.02f,c.z),new V3(fs,0,0),new V3(0,0,fs),Dust,.35f*(1-t/.8f),true,true);
            }
            // Debris voxels: thrown from the footprint, then rubble faces fading from fresh breaks to the dust coat over 10 s;
            // they sink away over the last 2 s of the 15 s.
            int count=DebrisCount(e,q),baseHex=BaseColor(r.Material),fresh=FreshColor(r.Material);float coat=Smooth(t/FreshFade)*.85f,sink=t>13?(LandLife-t)/2:1;
            Rgb(DustCoat,out float cr,out float cg,out float cb);
            for(int j=0;j<count;j++)
            {
                float rr=R*.55f*size*(float)Math.Sqrt(H(seed,j,20)),aa=H(seed,j,21)*6.2831855f;V3 p0=new V3(c.x+(float)Math.Cos(aa)*rr,floor+.3f+1.2f*H(seed,j,22),c.z+(float)Math.Sin(aa)*rr);
                V3 dir=rr>.2f?Flat(p0-c):new V3((float)Math.Cos(aa),0,(float)Math.Sin(aa));float sp=(1.5f+5*(float)Math.Pow(H(seed,j,23),1.5f))*(.6f+.4f*e),up=2+4.5f*H(seed,j,24);
                // At most .32 m: loose voxels on a walkway must not read as cover (§2.1 rule 1); the authored rubble is the cover.
                float edge=.08f+.24f*(float)Math.Pow(H(seed,j,25),2.6f);V3 dim=new V3(edge*(.7f+.6f*H(seed,j,26)),edge*(.7f+.6f*H(seed,j,27)),edge*(.7f+.6f*H(seed,j,28)))*sink;
                float hit=Throw(p0,dir*sp+new V3(0,up,0),Gravity,floor+dim.y*.4f,t,out V3 p);AxisAngle(Axis(seed,j,30),(1+7*H(seed,j,32))*SpinTime(t,hit),out float qx,out float qy,out float qz,out float qw);
                Rgb(H(seed,j,33)<.6f?fresh:baseHex,out float pr,out float pg,out float pb);float tint=.85f+.25f*H(seed,j,34);pr*=tint;pg*=tint;pb*=tint;
                if(!Cube(p,dim,qx,qy,qz,qw,pr+(cr-pr)*coat,pg+(cg-pg)*coat,pb+(cb-pb)*coat))break;
            }
            if(t>=WispFrom)
            {
                // Smoke wisps over the rubble for 15 s: each vent puffs every 1.1 s; a puff rises for 3.2 s.
                Rgb(Wisp,out float wr,out float wg,out float wb);int vents=q<.5f?Wisps/2:Wisps;
                for(int v=0;v<vents;v++)
                {
                    float rr=R*.6f*size*(float)Math.Sqrt(H(seed,v,40)),aa=H(seed,v,41)*6.2831855f,phase=H(seed,v,42)*1.1f;V3 at=new V3(c.x+(float)Math.Cos(aa)*rr,floor+.6f+R*.2f*size*(1-rr/(R*.6f*size+.01f))+.6f*H(seed,v,43),c.z+(float)Math.Sin(aa)*rr);
                    int last=(int)Math.Floor((t-WispFrom-phase)/1.1f);
                    for(int m=last;m>=0&&m>last-3;m--)
                    {
                        float born=WispFrom+phase+m*1.1f,a=t-born;if(a<0||a>=3.2f||born>=LandLife)continue;float u=a/3.2f,strength=1-born/LandLife;
                        if(!Puff(at+new V3(0,.6f*a,0)+Wind*(a*.5f),.8f+2.2f*u,(.8f+2.2f*u)*1.4f,wr,wg,wb,.42f*(float)Math.Sin(Math.PI*u)*strength,1.15f,.9f,eye))return;
                    }
                }
            }
        }

        // ---------- decals ----------
        private void MarkFrame(in Mark k,float now)
        {
            float a=now-k.Born;if(a<0)return;float on=Math.Min(1,a/.15f),s=k.Size;V3 n=k.Normal;
            V3 t1=Math.Abs(n.y)>.9f?new V3(1,0,0):new V3(0,1,0);t1=new V3(n.y*t1.z-n.z*t1.y,n.z*t1.x-n.x*t1.z,n.x*t1.y-n.y*t1.x).Normalized;V3 t2=new V3(n.y*t1.z-n.z*t1.y,n.z*t1.x-n.x*t1.z,n.x*t1.y-n.y*t1.x);
            float ang=H(k.Seed,0,1)*6.2831855f,ca=(float)Math.Cos(ang),sa=(float)Math.Sin(ang);V3 u=t1*ca+t2*sa,v=t2*ca-t1*sa,at=k.Center+n*(.012f+(int)k.Kind*.002f);
            switch(k.Kind)
            {
                case NativeStructureMark.Scorch:Quad(at,u*(s*.8f),v*(s*.8f),Soot,.25f*on,true,false);Quad(at+n*.001f,u*(s*.5f),v*(s*.45f),Ink,.62f*on,true,false);break;
                case NativeStructureMark.Chip:Quad(at,u*(s*.55f),v*(s*.45f),Mortar,.18f*on,true,true);Quad(at+n*.001f,u*(s*.18f),v*(s*.14f),Ink,.35f*on,true,false);break;
                case NativeStructureMark.Stain:Quad(at,u*(s*.5f),v*(s*.5f),Dust,.32f*on,true,true);break;
                default:
                    // A crack: four branches of two kinked segments each.
                    for(int b=0;b<4;b++)
                    {
                        float ba=b*1.5707964f+(H(k.Seed,b,2)-.5f)*1.2f,l1=s*(.18f+.2f*H(k.Seed,b,3)),kink=(H(k.Seed,b,4)-.5f)*.9f,l2=s*(.1f+.18f*H(k.Seed,b,5)),w=.018f+.012f*s;
                        V3 d1=u*(float)Math.Cos(ba)+v*(float)Math.Sin(ba),d2=u*(float)Math.Cos(ba+kink)+v*(float)Math.Sin(ba+kink),side1=new V3(n.y*d1.z-n.z*d1.y,n.z*d1.x-n.x*d1.z,n.x*d1.y-n.y*d1.x),side2=new V3(n.y*d2.z-n.z*d2.y,n.z*d2.x-n.x*d2.z,n.x*d2.y-n.y*d2.x);
                        V3 p1=at+d1*l1;Quad(at+d1*(l1*.5f),d1*(l1*.5f),side1*w,Ink,.7f*on,false,false);Quad(p1+d2*(l2*.5f),d2*(l2*.5f),side2*(w*.7f),Ink,.6f*on,false,false);
                    }
                    break;
            }
        }

        // ---------- dev preview stand-in ----------
        private void MockFrame(in Mock m,float now)
        {
            float t=now-m.Born;if(t<0)return;V3 s=m.Spot;float floor=m.Floor,T=NativeStructurePreview.Telegraph,land=NativeStructurePreview.LandAt;
            int wall=BaseColor(m.Material),fresh=FreshColor(m.Material);float hw=NativeStructurePreview.Width*.5f,hd=NativeStructurePreview.Depth*.5f,storey=NativeStructurePreview.Storey,pier=NativeStructurePreview.Pier;
            if(t<land)
            {
                float sag=Sag(Math.Min(t,T),T)*SagMetres,lowest=float.MaxValue;
                for(int k=0;k<NativeStructurePreview.Storeys;k++)
                {
                    float y=NativeStructurePreview.SlabY(k,t,floor)-sag,rel=t-T-NativeStructurePreview.Stagger[k];lowest=Math.Min(lowest,y);
                    Box(new V3(s.x,y,s.z),new V3(hw*2,.35f,hd*2),k==NativeStructurePreview.Storeys-1?0x2b2a2e:Concrete);
                    // The storey's front panel with two windows rides its slab down: they strobe through the telegraph, then go dark.
                    float py=y-storey*.5f+.18f;if(py-storey*.4f<floor)continue;Box(new V3(s.x,py,s.z-hd+.12f),new V3(hw*2-pier*2,storey-.35f,.24f),wall);
                    float lit=WindowStrobe(t,T),glass=rel>0?0:lit;for(int w=0;w<2;w++){Lerp(WindowDark,WindowLit,(k+w)%3==1?0:glass,out float wr,out float wg,out float wb);Cube(new V3(s.x+(w==0?-1.3f:1.3f),py+.2f,s.z-hd-.01f),new V3(1.1f,1.4f,.06f),0,0,0,1,wr,wg,wb);}
                }
                float pierTop=Math.Max(floor+.8f,Math.Min(floor+storey*NativeStructurePreview.Storeys,lowest+.2f));
                for(int k=0;k<4;k++)
                {
                    float px=s.x+((k&1)==0?-hw+pier*.5f:hw-pier*.5f),pz=s.z+((k&2)==0?-hd+pier*.5f:hd-pier*.5f);
                    if(k==0)
                    {
                        // The lost member: a banded stub with a rebar tangle (§3.9 stage S3).
                        Box(new V3(px,floor+.5f,pz),new V3(pier,1,pier),wall);Box(new V3(px,floor+.85f,pz),new V3(pier+.04f,.2f,pier+.04f),Yellow);Box(new V3(px,floor+.65f,pz),new V3(pier+.04f,.2f,pier+.04f),Black);
                        Rgb(Rust,out float rr,out float rg,out float rb);for(int b=0;b<6;b++){V3 base0=new V3(px+(H(m.Seed,b,1)-.5f)*pier*.7f,floor+1,pz+(H(m.Seed,b,2)-.5f)*pier*.7f);Line(base0,base0+new V3((H(m.Seed,b,3)-.5f)*.5f,.35f+.3f*H(m.Seed,b,4),(H(m.Seed,b,5)-.5f)*.5f),.025f,rr,rg,rb,1,false);}
                        continue;
                    }
                    float hgt=pierTop-floor;Box(new V3(px,floor+hgt*.5f,pz),new V3(pier,hgt,pier),wall);Box(new V3(px,floor+1.3f,pz),new V3(pier+.04f,.6f,pier+.04f),Yellow);Box(new V3(px,floor+1.3f,pz),new V3(pier+.05f,.18f,pier+.05f),Black);
                }
                return;
            }
            // Rubble swap under the dust: a mound over the footprint plus an apron, fresh breaks fading to the dust coat.
            float coat=(1-Freshness(t-land))*.8f;Rgb(DustCoat,out float cr,out float cg,out float cb);
            for(int i=0;i<18;i++)
            {
                float rx=(H(m.Seed,i,20)-.5f)*(hw*2+1.5f),rz=(H(m.Seed,i,21)-.5f)*(hd*2+1.5f),rd=Math.Max(Math.Abs(rx)/(hw+.75f),Math.Abs(rz)/(hd+.75f)),heap=.6f+2.8f*(1-rd);
                float e=1.1f+1.3f*H(m.Seed,i,22);AxisAngle(Axis(m.Seed,i,23),.5f*H(m.Seed,i,25),out float qx,out float qy,out float qz,out float qw);Rgb(H(m.Seed,i,26)<.45f?fresh:wall,out float pr,out float pg,out float pb);
                Cube(new V3(s.x+rx,floor+heap*.5f,s.z+rz),new V3(e,heap,e*(.8f+.4f*H(m.Seed,i,27))),qx,qy,qz,qw,pr+(cr-pr)*coat,pg+(cg-pg)*coat,pb+(cb-pb)*coat);
            }
            for(int i=0;i<2;i++){AxisAngle(new V3(i==0?1:-1,0,.3f),.55f,out float qx,out float qy,out float qz,out float qw);Rgb(Concrete,out float sr,out float sg,out float sb);Cube(new V3(s.x+(i==0?-1.2f:1.4f),floor+2.2f,s.z+(i==0?.8f:-.6f)),new V3(3.4f,.3f,2.6f),qx,qy,qz,qw,sr+(cr-sr)*coat,sg+(cg-sg)*coat,sb+(cb-sb)*coat);}
        }
    }

    /// <summary>
    /// Camera shake for the structure events (§3.10, §5.4), decided on the presentation side from the event's position: no
    /// change to what the sim emits (Target stays 0). Within 25 m, quadratic falloff, scaled by mass (fall) or energy (land);
    /// the stress tremor rises over the telegraph. Applied only while Settings · camera shake is on, in a live local view.
    /// </summary>
    public static class NativeStructureShake
    {
        public const float Range=25,MaxOffset=.3f,StressPeak=.035f,FallPeak=.09f,LandPeak=.2f,FallSeconds=.7f,LandSeconds=1.2f;
        public static float Falloff(float distance)=>distance>=Range?0:(1-distance/Range)*(1-distance/Range);
        /// <summary>Peak offset at the source: a fall scales by tonnes / 40 (clamped .35..1.4), a landing by .4 + .6 energy.</summary>
        public static float Peak(NativeStructureCue cue,float amount)=>cue==NativeStructureCue.Stress?StressPeak:cue==NativeStructureCue.Fall?FallPeak*Math.Max(.35f,Math.Min(1.4f,amount/40)):LandPeak*(.4f+.6f*Math.Max(0,Math.Min(1,amount)));
        public static float Duration(NativeStructureCue cue,float telegraph)=>cue==NativeStructureCue.Stress?Math.Max(.4f,telegraph):cue==NativeStructureCue.Fall?FallSeconds:LandSeconds;
        public static float Envelope(bool rise,float t,float duration)
        {if(t<0||t>duration)return 0;float u=t/duration;if(rise)return (float)Math.Pow(u,1.5);return (1-u)*(1-u)*Math.Min(1,t/.03f);}
    }

    public enum NativeStructureSound{PierHit,StageCrack,RebarPing,Groan,Collapse,Land,Chimney}
    /// <summary>One layer of a destruction cue: clip id ("structure/..." = synthesized, Resources/StructureAudio; else the
    /// source bank), gain, rate (0 = the bank's .97-1.03 jitter), delay in seconds, and whether it is a high layer that
    /// distance dulls first.</summary>
    public struct NativeStructureLayer{public string Clip;public float Gain,Rate,Delay;public bool High;}
    /// <summary>
    /// §3.11 destruction cue routing (at most 6 rubble voices): pier hit thud + crack; stage changes (crack and gravel at S1,
    /// rebar ping at S2); the stress groan rising with level; the collapse (snap, sub boom, rumble, glass, debris tail); landing
    /// thuds by material; the chimney's long creak, whoosh and four thuds; one structure-level cue per segment event, replacing
    /// today's explosion per block. Distance "low-pass" is approximated by dulling the high layers faster than the low ones
    /// (one AudioSource per voice, no per-voice filter). Clips: tools/synth_structure_audio.py (deterministic, asset-locked)
    /// plus the bank's wood, metal and dash.
    /// </summary>
    public static class NativeStructureSounds
    {
        public const int Voices=6,MaxLayers=6;public const string Prefix="structure/",Folder="StructureAudio/";
        public static readonly string[] Synthesized={"thud","crack","gravel","rebar","groan","creak","snap","boom","rumble","glass","debris"};
        private static int Add(NativeStructureLayer[] o,int n,string clip,float gain,float rate,float delay,bool high){if(n<o.Length)o[n]=new NativeStructureLayer{Clip=clip,Gain=gain,Rate=rate,Delay=delay,High=high};return n+1;}
        public static int Plan(NativeStructureSound cue,NativeStructureMaterial m,float level,NativeStructureLayer[] o)
        {
            level=Math.Max(0,Math.Min(1,level));int n=0;const string P=Prefix;
            switch(cue)
            {
                case NativeStructureSound.PierHit:n=Add(o,n,P+"thud",.8f,0,0,false);n=Add(o,n,P+"crack",.55f,0,.02f,true);break;
                case NativeStructureSound.StageCrack:n=Add(o,n,P+"crack",.6f,0,0,true);n=Add(o,n,P+"gravel",.5f,0,.05f,true);break;
                case NativeStructureSound.RebarPing:n=Add(o,n,P+"rebar",.55f,0,0,true);break;
                case NativeStructureSound.Groan:n=Add(o,n,P+"groan",.45f+.45f*level,.92f+.08f*level,0,false);n=Add(o,n,P+"creak",.3f,0,.25f,true);break;
                case NativeStructureSound.Collapse:n=Add(o,n,P+"snap",.9f,0,0,true);n=Add(o,n,P+"boom",1,1,0,false);n=Add(o,n,P+"rumble",.8f,1,.05f,false);n=Add(o,n,P+"glass",.5f,0,.12f,true);n=Add(o,n,P+"debris",.65f,0,.35f,true);break;
                case NativeStructureSound.Land:
                    switch(m)
                    {
                        case NativeStructureMaterial.Timber:n=Add(o,n,P+"thud",.6f,1.15f,0,false);n=Add(o,n,"wood",.7f,.7f,.02f,true);break;
                        case NativeStructureMaterial.Metal:n=Add(o,n,P+"thud",.6f,0,0,false);n=Add(o,n,"metal",.7f,.6f,.02f,true);break;
                        case NativeStructureMaterial.Glass:n=Add(o,n,P+"glass",.8f,0,0,true);n=Add(o,n,P+"thud",.4f,1.2f,0,false);break;
                        default:n=Add(o,n,P+"thud",.9f*(.6f+.4f*level),.95f,0,false);n=Add(o,n,P+"gravel",.6f,0,.04f,true);if(level>.5f)n=Add(o,n,P+"debris",.45f,1.05f,.1f,true);break;
                    }
                    break;
                case NativeStructureSound.Chimney:
                    n=Add(o,n,P+"creak",1,.9f,0,true);n=Add(o,n,"dash",.7f,.6f,1,true);n=Add(o,n,P+"thud",.9f,.9f,1.6f,false);n=Add(o,n,P+"thud",.75f,1,1.85f,false);n=Add(o,n,P+"thud",.7f,1.05f,2.05f,false);n=Add(o,n,P+"thud",.6f,1.1f,2.25f,false);break;
            }
            return Math.Min(n,o.Length);
        }
        /// <summary>The kiln chimneys (stack-n, stack-s) topple with their own cue.</summary>
        public static bool Chimney(string segment)=>segment!=null&&segment.StartsWith("stack",StringComparison.Ordinal);
        /// <summary>The cue a structure event plays: stress groans (a chimney's lean plays the whole chimney cue), a fall collapses
        /// (a chimney's fall is already in its cue), a landing thuds by material. False: none.</summary>
        public static bool ForEvent(NativeStructureCue cue,string segment,out NativeStructureSound sound)
        {
            bool stack=Chimney(segment);sound=cue==NativeStructureCue.Stress?(stack?NativeStructureSound.Chimney:NativeStructureSound.Groan):cue==NativeStructureCue.Fall?NativeStructureSound.Collapse:NativeStructureSound.Land;
            return !(stack&&cue==NativeStructureCue.Fall);
        }
        /// <summary>A layer's gain heard at <paramref name="distance"/>: the bank's falloff, and high layers dulled further.</summary>
        public static float Gain(in NativeStructureLayer l,float distance)=>l.Gain*NativeEventSounds.Falloff(distance)*(l.High?1/(1+Math.Max(0,distance-4)*.07f):1);
    }

    /// <summary>
    /// The dev preview's stand-in segment (Sandbox, dev accounts): a 5.6 x 6 m three-storey hotel quadrant of §3.9 whose banded
    /// corner pier is already gone. Telegraph 1.2 s, then a pancake (roof, floor 2 and floor 1 released at 0, .25 and .45 s,
    /// each accelerating at 19-22 m/s²) that lands together 1 s after the telegraph, when the rubble swaps in. Pure in time.
    /// </summary>
    public static class NativeStructurePreview
    {
        public const float Telegraph=1.2f,Drop=1,Width=5.6f,Depth=6,Storey=3.6f,Pier=.9f,Mass=60,Energy=.85f;public const int Storeys=3;
        public const float LandAt=Telegraph+Drop;
        /// <summary>Release offsets after the telegraph, by storey (floor 1, floor 2, roof).</summary>
        public static readonly float[] Stagger={.45f,.25f,0};
        public static float SlabY(int k,float t,float floor)
        {float y0=floor+Storey*(k+1),rest=floor+.6f+.45f*k,rel=t-Telegraph-Stagger[k],fall=Drop-Stagger[k];if(rel<=0)return y0;return Math.Max(rest,y0-(y0-rest)*rel*rel/(fall*fall));}
        public static float Height=>Storey*Storeys;
        public static float Radius=>(float)Math.Sqrt(Width*Width*.25f+Depth*Depth*.25f+Height*Height*.25f);
        public static V3 Centroid(V3 spot,float floor)=>new V3(spot.x,floor+Height*.5f,spot.z);
        public static void Box(V3 spot,float floor,out V3 center,out V3 half){center=new V3(spot.x,floor+Height*.5f+.5f,spot.z);half=new V3(Width*.5f,Height*.5f+.5f,Depth*.5f);}
    }
}

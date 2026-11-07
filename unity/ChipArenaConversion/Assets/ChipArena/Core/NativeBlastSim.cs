using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Pure port of the source's volumetric chip blasts (client/chip-blasts.ts at 05950ea; owner request 2026-10-03:
    // explosions must read as real explosions, not flat decals). Cosmetic only: callers key every blast to an
    // authoritative effect (position and gameplay radius) or a live field, and nothing here feeds back into the match.
    // Numbers, spawn order, the Float32 particle storage and the seeded generator follow the source, so one run
    // reproduces the source's voxels. Coordinates are taken as given: the converted maps keep the source's numbers,
    // so Unity world positions go in and Unity world positions come out. Owner exceptions: no blue or cyan lightning on
    // area effects (2026-10-10, NativeBlastSim.Lightning), and no flat area disc under blasts and impacts (2026-10-11: on a
    // wall it read as "a floating pancake"; its voxels go to the shell, NativeBlastSim.Blast). tools/export_blast_fixtures.mjs
    // applies the same cuts to the source before it records the parity frames. The 2026-10-11 effects overhaul adds more
    // there: light styles' smoke (Quake's dust among them) no longer fades to near-black (SmokeEnd), a voxel's tumble follows
    // its stable seed, not its pool slot (NativeBlastPool.Update), and a chip bomb's fuse sparks start at the modelled bomb's
    // fuse tip on its floor (NativeFieldRules.BombTipY), not .85 m over the source's ground. It also tags every particle with
    // a draw role (NativeFxRole), writes role, age, seed, velocity, raw size, floor, strand and start size into the draw list,
    // and records one NativeBlastShock per shock front, all without touching a random draw, count, lifetime, velocity or
    // size. Also beyond the source: callers may pass the
    // real floor under a blast, impact or field (NativeBlastPresentation and NativeFieldPresentation raycast it). The source
    // assumes every blast is at a pilot's chest and puts its floor .95 m lower, which is still the default.
    // Effects toon pass (owner 2026-10-11, after the Asset Store pack study): Blast is Unity-owned from then on. Each style's
    // composition, counts and timing are retuned to read as stylized toon effects (Blast; the Geyser Wall and Tidal Wave water
    // looks, NativeBlastLook), a water globe's floor splash is new (Splash, NativeWaterSplash), and the pool keeps each
    // particle's spawn colours for the renderer's own ramps. Tests/blast-pinned.json pins the blasts (Unity-owned frames,
    // budgets and timing to the radius); Impact, Field, Line and Paddle still match the source frames in Tests/blast-parity.json.
    public enum NativeBlastStyle{Fire,Shock,Implosion,Frost,Toxic,Void,Nova,Kinetic,Quake,Meteor,Mine}
    /// <summary>A tint's composition beyond its colours (toon pass, owner 2026-10-11): the Geyser Wall's Frost blast erupts as a
    /// water column and the Tidal Wave's Kinetic blast pops as water instead of dust and rubble.</summary>
    public enum NativeBlastLook{Default,Geyser,Tide}
    public struct NativeBlastTint{public int Core,Hot,Cool;public NativeBlastLook Look;public NativeBlastTint(int core,int hot,int cool,NativeBlastLook look=NativeBlastLook.Default){Core=core;Hot=hot;Cool=cool;Look=look;}}
    /// <summary>A live authoritative field as the source's FieldView presents it (chip fields and ability zones). Floor, beyond
    /// the source: the floor height under the field (NativeFieldPresentation raycasts it; NativeBlastSim.NoFloor for none), which
    /// its arrival and Orbital Mark lock impacts sit on (NativeBlastSim.Field) and which hides its glow disc when the field is
    /// high above it (NativeFieldRules.GlowShown). Null keeps the source's floor .95 m under the field.</summary>
    public struct NativeBlastField{public long Id;public string Kind,Fx;public double X,Y,Z,Radius,Born,Until;public double? LockAt,Floor;}
    /// <summary>One pooled point light. Intensity and Distance are three.js PointLight values (decay 2).</summary>
    public sealed class NativeBlastFlash{public double X,Y,Z,Distance=8,At=-1e9,Peak,Life=.3,Intensity;public int Color=0xffffff;}
    /// <summary>Which spawn drew a shock front: a blast's shell, an impact's front, Nova's sphere front, or Quake's slam wave
    /// (toon pass: a short upright open cylinder standing on the floor, never a flat ring).</summary>
    public enum NativeShockKind{Blast,Impact,Nova,Slam}
    /// <summary>
    /// One expanding shock front (owner 2026-10-11 effects overhaul), recorded once where the sim spawns a front's
    /// <see cref="NativeFxRole.Shell"/> voxels, so a renderer draws one expanding fresnel shell per front instead of dozens
    /// of cubes. It grows at the voxels' constant speed from <see cref="Start"/> at <see cref="Born"/> to exactly
    /// <see cref="Radius"/> (the gameplay radius the front ends on) at Born + FrontSeconds, then holds there and fades out
    /// by Born + <see cref="Life"/>. Ages on the sim clock (NativeBlastSim.Time, the presentation clock); cosmetic only.
    /// </summary>
    public struct NativeBlastShock
    {
        /// <summary>Origin: centre x/z; Y is the floor point for a dome, the centre for a sphere (exactly where the voxels start).</summary>
        public double X,Y,Z;
        /// <summary>Radius at birth (the voxels' start offset) and the gameplay radius the front ends on.</summary>
        public double Start,Radius;
        /// <summary>A dome rising from Y (an upper hemisphere); otherwise a full sphere around the point.</summary>
        public bool Dome;
        /// <summary>The floor height under the front (NativeBlastSim.NoFloor for none): a dome stands on it; an airborne blast or
        /// impact sphere's voxels stop on it (Nova's sphere, from the source, passes through it; the opaque floor hides that half).</summary>
        public double Floor;
        /// <summary>Sim time at spawn and total seconds alive (NativeBlastSim.ShockLife: the front's travel plus its fade).</summary>
        public double Born,Life;
        /// <summary>Linear rgb: the colour the front's voxels start in (Hot*) and fade to (Cool*), and the look's core colour.</summary>
        public double HotR,HotG,HotB,CoolR,CoolG,CoolB,CoreR,CoreG,CoreB;
        /// <summary>A stable per-front seed (counted from 0 after each Clear) and how many Shell voxels the front carries.</summary>
        public int Seed,Voxels;
        public NativeShockKind Kind;
        /// <summary>The blast style for Blast, Nova and Slam fronts; null for an impact.</summary>
        public NativeBlastStyle? Style;
        /// <summary>Seconds the front takes from Start to Radius; 0 means <see cref="NativeBlastSim.FrontSeconds"/> (every
        /// front but the slam wave).</summary>
        public double Travel;
        /// <summary>A slam wave's wall height (m); 0 for the other fronts.</summary>
        public double Height;
        public double Age(double time)=>time-Born;
        /// <summary>Seconds from Start to Radius (Travel, or FrontSeconds).</summary>
        public double FrontTime=>Travel>0?Travel:NativeBlastSim.FrontSeconds;
        /// <summary>The front has been born (a record may be born late: Implosion's front waits for its pop).</summary>
        public bool Started(double time)=>time>=Born;
        /// <summary>Front progress 0..1: exactly 1 from Born + FrontTime on.</summary>
        public double Progress(double time)=>Math.Max(0,Math.Min(1,(time-Born)/FrontTime));
        /// <summary>The front's radius: linear (constant voxel speed) from Start, and exactly Radius once Progress is 1.</summary>
        public double RadiusAt(double time){double u=Progress(time);return u>=1?Radius:Start+(Radius-Start)*u;}
        /// <summary>Normalized age over the whole life, 0..1.</summary>
        public double U(double time)=>Math.Max(0,Math.Min(1,(time-Born)/Life));
        /// <summary>Opacity envelope: 1 while the front travels, then a smooth fall to exactly 0 at Born + Life.</summary>
        public double Fade(double time)
        {double t=(time-Born-FrontTime)/Math.Max(1e-6,Life-FrontTime);if(t<=0)return 1;if(t>=1)return 0;return 1-t*t*(3-2*t);}
    }

    /// <summary>The source's generator (chip-blasts.ts rng): a 32-bit LCG, so runs are repeatable.</summary>
    public sealed class NativeBlastRandom
    {
        public const uint SourceSeed=0x51a57;
        private uint state;
        public NativeBlastRandom(uint seed=SourceSeed){state=seed==0?1u:seed;}
        public double Next(){state=unchecked(state*1664525u+1013904223u);return state/4294967296.0;}
    }

    /// <summary>
    /// What a pooled blast particle is, so a renderer draws the right primitive for it instead of one flat cube for every
    /// particle (owner 2026-10-11 effects overhaul: "the effects seem a bit outdated and there still seems to be cube
    /// placeholders for certain impact effects"). Cosmetic only: a role changes no motion, count, lifetime, size, colour or
    /// random draw, so the source parity frames stay intact. Every spawn site in NativeBlastSim names its role (VfxChecks
    /// scans for it). The values are stable: they are written as floats into <see cref="NativeBlastPool.Output"/>. Each
    /// summary names the pool the role occurs in and the primitive it is meant to be drawn as.
    /// </summary>
    public enum NativeFxRole
    {
        /// <summary>Glow. A small soft mote (field telegraph motes, impact motes, Geyser Wall droplets, time and heal motes,
        /// the rally paddle): a soft round HDR glow sprite.</summary>
        Mote=0,
        /// <summary>Glow. The white-hot core of a blast or impact, and the falling meteor's head: a large soft HDR glow puff.</summary>
        Core=1,
        /// <summary>Solid. A fireball puff (Fire, Mine, Meteor): a toon fire puff (toon pass) that runs white-hot, its spawn colour
        /// (the flame), orange, then its end colour (the ash, slate), growing from its start size and eaten into blobs late.</summary>
        Fireball=2,
        /// <summary>Glow. A shock-front voxel (the blast shell, the impact front, Nova's front). Not drawn one by one: each
        /// front is one <see cref="NativeBlastShock"/> record (<see cref="NativeBlastSim.Shocks"/>), drawn as an expanding
        /// fresnel shell. The voxels stay in the pool so the source parity and the gameplay-radius reads hold.</summary>
        Shell=3,
        /// <summary>Solid. Blast smoke, dust and ink, and the meteor's smoke trail: a toon puff (toon pass: hard lobes, a two-tone
        /// shadow) in its colour, growing from its start size and eaten into blobs late in its life instead of fading.</summary>
        Smoke=4,
        /// <summary>Solid. Small rubble flung out and bouncing (blast and impact debris): a lit irregular rubble chunk.</summary>
        Debris=5,
        /// <summary>Glow. A ballistic spark (blast sparks, the sky bolt's side sparks, Thunder Step crackle, bomb fuse sparks):
        /// a thin HDR streak stretched along its velocity.</summary>
        Spark=6,
        /// <summary>Solid. An ice crystal (Frost blast shards, frost field crystals): a lit crystal shard along its long axis.</summary>
        Shard=7,
        /// <summary>Solid. A slow drifting cloud (toxic clouds, Void's and Implosion's rings, storm clouds, acid fumes): a large
        /// toon puff that glows a little in a saturated colour, slow erosion.</summary>
        Cloud=8,
        /// <summary>Solid. A flame tongue (fire field flames, the Comet Dash trail): an emissive flame puff licking upward.</summary>
        Flame=9,
        /// <summary>Glow. A rising ember (fire field embers, the Meteor blast's plume): a tiny HDR mote with a short streak.</summary>
        Ember=10,
        /// <summary>Solid. A light-eating core (Void's dark cubes): a dark absorbing puff with a faint rim.</summary>
        Dark=11,
        /// <summary>Glow. A lightning segment (the sky bolt and the ground arcs): a bright HDR beam joining its neighbours.</summary>
        Bolt=12,
        /// <summary>Glow. A point on a straight streak (Line: teleport, arc and rally streaks): a glow segment to its neighbours.</summary>
        Line=13,
        /// <summary>Glow. A mote rushing along a path (implosion and vacuum inrush, black-hole and Reverse Grenade infall, the
        /// repulsion push, the Orbital Mark column): a soft HDR streak stretched along its velocity.</summary>
        Streak=14,
        /// <summary>Solid. A slab heaved out of the ground (Quake chunks): a lit irregular rubble chunk, larger and rockier.</summary>
        Rock=15,
        /// <summary>Glow. An acid bubble rising and swelling (acid field): a soft ring-like glow bubble.</summary>
        Bubble=16,
        /// <summary>Glow. A falling snowflake (frost field): a small soft pale mote, not HDR-hot.</summary>
        Snow=17,
        /// <summary>Solid. Rubble thrown by a fire-family blast (Fire, Mine and Meteor debris): a lit rubble chunk that leaves
        /// the fireball glowing hot and cools over its first third of life (review 2026-10-11: every debris colour is dark, so
        /// the colour-keyed heat never fired and fire debris read as black specks). Draw-only: spawned exactly as Debris.</summary>
        HotDebris=18,
        // ---- toon pass (owner 2026-10-11, after the Asset Store pack study): drawn from their spawn colours
        // (NativeBlastPool.HexFromAt / HexToAt) on the toon sheet (NativeVfxTextures.ToonSheet). ----
        /// <summary>Glow. A toon flash: a camera-facing sprite playing the round flash flipbook (a disc eaten into a thin
        /// crescent) over its life, white-hot to its spawn colours (blast flashes, Shock's nova).</summary>
        Flash=19,
        /// <summary>Glow. A crescent flame lick flying outward (fire-family blasts, Nova's radial licks): an HDR crescent
        /// sprite along its velocity.</summary>
        Lick=20,
        /// <summary>Glow. A sharp spike spark (Frost's ice spikes): an HDR arrowhead sprite stretched along its velocity.</summary>
        Spike=21,
        /// <summary>Solid. A toon water puff (the Geyser Wall column and spray, the Tidal Wave pop, a water globe's floor
        /// splash): a pale two-tone puff with blue shadows, stretched along its velocity.</summary>
        Water=22,
        /// <summary>Glow. A streak blended over the scene in its colour, not added (water droplets, Void's ink threads).</summary>
        Droplet=23,
        /// <summary>Solid. A faint toon puff (frost and water mist): the smoke look at low opacity.</summary>
        Mist=24,
        /// <summary>Solid. A glossy toon ball flung on an arc (Toxic's acid blobs; the puffs it trails are Smoke).</summary>
        Blob=25,
        /// <summary>Glow. A big soft glow round a blast's centre that sits behind what is in front of it (a Core glow is drawn
        /// in front of the puffs round it; Void's black shapes must stay black over this violet one).</summary>
        Halo=26,
    }

    /// <summary>Growable voxel particle pool (structure of arrays, swap-remove on expiry) and its per-frame draw list.</summary>
    public sealed class NativeBlastPool
    {
        public const int A=10;// age,life,size0,size1,gravity,drag,delay,floor,stretch,spin
        /// <summary>
        /// Floats per drawn voxel in <see cref="Output"/>. 0-12 are the source's voxel: position xyz (0), full edge lengths xyz
        /// (3), rotation quaternion xyzw (6), linear rgb (10). Beyond the source (owner 2026-10-11 effects overhaul):
        /// <see cref="NativeFxRole"/> as a float (<see cref="RoleAt"/>), normalized age u in 0..1 (<see cref="AgeAt"/>), the
        /// stable per-particle seed, an integer below 2^24 (<see cref="SeedAt"/>), velocity xyz in m/s (<see cref="VelocityAt"/>),
        /// the raw edge before the opaque pool's end shrink (<see cref="RawSizeAt"/>; the glow pool's edge is already raw), the
        /// particle's floor height (<see cref="FloorAt"/>; <see cref="NativeBlastSim.NoFloor"/> for none), its strand
        /// (<see cref="StrandAt"/>: the seed of the first point of the line, arc or bolt it belongs to, so a renderer joins Bolt
        /// and Line points only within one strand; a lone particle's own seed) and its edge at birth (<see cref="StartSizeAt"/>,
        /// size0: a puff drawn from its start size can billow instead of shrinking with the source's size1). Toon pass: the two
        /// spawn colours as sRGB hex integers below 2^24 (<see cref="HexFromAt"/>, <see cref="HexToAt"/>), so a renderer can run
        /// its own colour ramp (fire to slate, water's lit and shadow tones) instead of the pool's linear lerp.
        /// </summary>
        public const int Stride=25;
        public const int RoleAt=13,AgeAt=14,SeedAt=15,VelocityAt=16,RawSizeAt=19,FloorAt=20,StrandAt=21,StartSizeAt=22,HexFromAt=23,HexToAt=24;
        /// <summary>Seeds count spawns from 0 after each Clear and wrap below 2^24, so a seed is exact as a float.</summary>
        public const int SeedMask=0xffffff;
        public readonly bool Glow;public int Count,Spawned,Capacity,Drawn;
        /// <summary>Multiplies voxel sizes at spawn (the source's close-up stages).</summary>
        public double SizeScale=1;
        /// <summary>Drawn voxels after the last Update. Glow colours are already faded (additive: final colour = rgb).</summary>
        public float[] Output;
        private readonly int initial;private float[] p,v,c0,c1,a;private int[] seed,strand,hex0,hex1;private byte[] role;private int nextSeed;
        public NativeBlastPool(int capacity,bool glow){Glow=glow;initial=capacity;Capacity=capacity;p=new float[capacity*3];v=new float[capacity*3];c0=new float[capacity*3];c1=new float[capacity*3];a=new float[capacity*A];seed=new int[capacity];strand=new int[capacity];hex0=new int[capacity];hex1=new int[capacity];role=new byte[capacity];Output=new float[capacity*Stride];}
        /// <summary>The seed the next spawn gets: a line, arc or bolt reads it before its first point and passes it to every
        /// point as their shared strand.</summary>
        public int NextSeed=>nextSeed;
        /// <summary>The source's spawn. role (beyond the source): what the particle is drawn as; null is
        /// <see cref="NativeFxRole.Mote"/> in the glow pool and <see cref="NativeFxRole.Debris"/> in the opaque pool. strand
        /// (beyond the source): the strand the particle belongs to (<see cref="StrandAt"/>); negative is its own seed. Neither
        /// touches a random draw.</summary>
        public void Spawn(double x,double y,double z,double vx,double vy,double vz,double life,double size0,double size1,int color0,int color1,double gravity=0,double drag=0,double delay=0,double floor=-1e9,double stretch=1,double spin=0,NativeFxRole? role=null,int strand=-1)
        {
            if(Count==Capacity)Resize(Capacity*2);
            int i=Count++;Spawned++;int j=i*3,k=i*A;
            p[j]=(float)x;p[j+1]=(float)y;p[j+2]=(float)z;v[j]=(float)vx;v[j+1]=(float)vy;v[j+2]=(float)vz;
            NativeBlastSim.Linear(color0,out double r,out double g,out double b);c0[j]=(float)r;c0[j+1]=(float)g;c0[j+2]=(float)b;
            NativeBlastSim.Linear(color1,out r,out g,out b);c1[j]=(float)r;c1[j+1]=(float)g;c1[j+2]=(float)b;
            a[k]=(float)-delay;a[k+1]=(float)Math.Max(.01,life);a[k+2]=(float)(size0*SizeScale);a[k+3]=(float)(size1*SizeScale);a[k+4]=(float)gravity;a[k+5]=(float)drag;a[k+6]=(float)delay;a[k+7]=(float)floor;a[k+8]=(float)stretch;a[k+9]=(float)spin;
            seed[i]=nextSeed;this.strand[i]=strand<0?nextSeed:strand&SeedMask;nextSeed=(nextSeed+1)&SeedMask;this.role[i]=(byte)(role??(Glow?NativeFxRole.Mote:NativeFxRole.Debris));hex0[i]=color0&0xffffff;hex1[i]=color1&0xffffff;
        }
        // Arrays are replaced only at a capacity boundary (doubling, as the source's InstancedMesh pools).
        private void Resize(int capacity)
        {
            float[] Grow(float[] old,int stride){var next=new float[capacity*stride];Array.Copy(old,next,Math.Min(old.Length,Math.Min(Count,capacity)*stride));return next;}
            p=Grow(p,3);v=Grow(v,3);c0=Grow(c0,3);c1=Grow(c1,3);a=Grow(a,A);Output=new float[capacity*Stride];Capacity=capacity;if(Drawn>capacity)Drawn=capacity;
            int keep=Math.Min(Count,capacity);var seeds=new int[capacity];Array.Copy(seed,seeds,keep);seed=seeds;var strands=new int[capacity];Array.Copy(strand,strands,keep);strand=strands;var roles=new byte[capacity];Array.Copy(role,roles,keep);role=roles;
            var from=new int[capacity];Array.Copy(hex0,from,keep);hex0=from;var to=new int[capacity];Array.Copy(hex1,to,keep);hex1=to;
        }
        /// <summary>Age and position of particle i (checks).</summary>
        public (double x,double y,double z,double age,double life) At(int i){int j=i*3;return (p[j],p[j+1],p[j+2],a[i*A],a[i*A+1]);}
        /// <summary>Velocity, gravity and floor of particle i (checks).</summary>
        public (double vx,double vy,double vz,double gravity,double floor) Motion(int i){int j=i*3,k=i*A;return (v[j],v[j+1],v[j+2],a[k+4],a[k+7]);}
        /// <summary>Role and stable seed of live particle i (checks).</summary>
        public (NativeFxRole role,int seed) Identity(int i)=>((NativeFxRole)role[i],seed[i]);
        /// <summary>The strand of live particle i (checks).</summary>
        public int Strand(int i)=>strand[i];
        /// <summary>The spawn colours (sRGB hex) of live particle i (checks).</summary>
        public (int from,int to) Colors(int i)=>(hex0[i],hex1[i]);
        private void Kill(int i)
        {
            int last=--Count;if(i==last)return;int j=i*3,l=last*3;
            for(int n=0;n<3;n++){p[j+n]=p[l+n];v[j+n]=v[l+n];c0[j+n]=c0[l+n];c1[j+n]=c1[l+n];}
            int k=i*A,m=last*A;for(int n=0;n<A;n++)a[k+n]=a[m+n];
            // Beyond the source: the moved particle keeps its seed and role, so nothing about it snaps when another expires.
            seed[i]=seed[last];strand[i]=strand[last];role[i]=role[last];hex0[i]=hex0[last];hex1[i]=hex1[last];
        }
        public void Update(double dt)
        {
            for(int i=Count-1;i>=0;i--)
            {
                int k=i*A;a[k]=(float)(a[k]+dt);if(a[k]>=a[k+1]){Kill(i);continue;}if(a[k]<0)continue;
                int j=i*3;double drag=Math.Exp(-a[k+5]*dt);
                v[j]=(float)(v[j]*drag);v[j+2]=(float)(v[j+2]*drag);v[j+1]=(float)(v[j+1]*drag-a[k+4]*dt);
                p[j]=(float)(p[j]+v[j]*dt);p[j+1]=(float)(p[j+1]+v[j+1]*dt);p[j+2]=(float)(p[j+2]+v[j+2]*dt);
                if(p[j+1]<a[k+7]){p[j+1]=a[k+7];if(v[j+1]<0){v[j+1]=(float)(v[j+1]*-.28);v[j]=(float)(v[j]*.6);v[j+2]=(float)(v[j+2]*.6);}}
            }
            int n=0;var o=Output;
            for(int i=0;i<Count;i++)
            {
                int k=i*A,j=i*3;double age=a[k];if(age<0)continue;
                double life=a[k+1],size0=a[k+2],size1=a[k+3],u=Math.Min(1,age/life),raw=size0+(size1-size0)*u,size=raw*(Glow?1:Math.Min(1,(1-u)*4));
                // three.js Euler(age*spin*1.3+s, age*spin+s*.7, 0) in XYZ order. The source's s is the pool index i, so a voxel's
                // tumble jumped whenever an expiring voxel swapped it to another slot (a resting shard visibly snapped). Owner-approved
                // fix (2026-10-11 effects overhaul): s is the particle's stable seed, which equals i until the first expiry
                // (tools/export_blast_fixtures.mjs applies the same change to the source).
                double s=seed[i],spin=a[k+9],ex=age*spin*1.3+s,ey=age*spin+s*.7,c1x=Math.Cos(ex/2),c2y=Math.Cos(ey/2),s1x=Math.Sin(ex/2),s2y=Math.Sin(ey/2);
                double qx=s1x*c2y,qy=c1x*s2y,qz=s1x*s2y,qw=c1x*c2y,sx=size,sy=size,sz=size,stretch=a[k+8];
                if(stretch!=1)
                {
                    double vx=v[j],vy=v[j+1],vz=v[j+2],l=Math.Sqrt(vx*vx+vy*vy+vz*vz);
                    if(l>.05)
                    {
                        // Quaternion.setFromUnitVectors(+Y, velocity) then normalize(), as three.js r180.
                        double tx=vx/l,ty=vy/l,tz=vz/l,r=ty+1;
                        if(r<1e-8){qx=0;qy=0;qz=1;qw=0;}else{qx=tz;qy=0;qz=-tx;qw=r;}
                        double len=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);if(len==0){qx=qy=qz=0;qw=1;}else{len=1/len;qx*=len;qy*=len;qz*=len;qw*=len;}
                    }
                    sy=size*stretch;
                }
                double t=Math.Min(1,u*1.4),r0=c0[j],g0=c0[j+1],b0=c0[j+2];
                r0+=(c1[j]-r0)*t;g0+=(c1[j+1]-g0)*t;b0+=(c1[j+2]-b0)*t;
                // Additive voxels fade by dimming; opaque voxels fade by shrinking (size1 carries that).
                if(Glow){double fade=(1-u)*(1-u)*1.15;r0*=fade;g0*=fade;b0*=fade;}
                int w=n*Stride;
                o[w]=p[j];o[w+1]=p[j+1];o[w+2]=p[j+2];o[w+3]=(float)sx;o[w+4]=(float)sy;o[w+5]=(float)sz;
                o[w+6]=(float)qx;o[w+7]=(float)qy;o[w+8]=(float)qz;o[w+9]=(float)qw;o[w+10]=(float)r0;o[w+11]=(float)g0;o[w+12]=(float)b0;
                o[w+RoleAt]=role[i];o[w+AgeAt]=(float)u;o[w+SeedAt]=seed[i];o[w+VelocityAt]=v[j];o[w+VelocityAt+1]=v[j+1];o[w+VelocityAt+2]=v[j+2];o[w+RawSizeAt]=(float)raw;o[w+FloorAt]=a[k+7];o[w+StrandAt]=strand[i];o[w+StartSizeAt]=(float)size0;o[w+HexFromAt]=hex0[i];o[w+HexToAt]=hex1[i];n++;
            }
            Drawn=n;
        }
        public void Clear(){Count=0;Drawn=0;nextSeed=0;if(Capacity!=initial){Count=0;Resize(initial);}}
        /// <summary>Longest gap (m) a Bolt or a Line segment bridges between two drawn neighbours.</summary>
        public const float BoltJoin=1.6f,LineJoin=1.5f;
        /// <summary>
        /// Whether a renderer joins two drawn points of <see cref="Output"/> (offsets a and b, a's seed below b's) with a
        /// segment (owner 2026-10-11 effects overhaul: a beam or streak joining its neighbours). Both Bolt or both Line, in
        /// the same strand (<see cref="StrandAt"/>; review 2026-10-11: by seed alone, back-to-back arcs, spokes and lines were
        /// joined end to start), seed neighbours (a Bolt may skip one, the sky bolt's side spark between its points) and at
        /// most BoltJoin / LineJoin apart. Cosmetic only.
        /// </summary>
        public static bool Linked(float[] o,int a,int b)
        {
            int role=(int)(o[a+RoleAt]+.5f);if(role!=(int)(o[b+RoleAt]+.5f)||role!=(int)NativeFxRole.Bolt&&role!=(int)NativeFxRole.Line||o[a+StrandAt]!=o[b+StrandAt])return false;
            bool bolt=role==(int)NativeFxRole.Bolt;int gap=(int)(o[b+SeedAt]-o[a+SeedAt]);if(gap<1||gap>(bolt?2:1))return false;
            float dx=o[b]-o[a],dy=o[b+1]-o[a+1],dz=o[b+2]-o[a+2],len=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
            return len>=1e-4f&&len<=(bolt?BoltJoin:LineJoin);
        }
    }

    public sealed class NativeBlastSim
    {
        /// <summary>Seconds the shock front takes to reach the gameplay radius. The front ends exactly at that radius.</summary>
        public const double FrontSeconds=.3;
        public const int SolidCapacity=900,GlowCapacity=700,LightCapacity=3;
        /// <summary>Voxels every blast puts on its gameplay radius however full the pools are (source §13.3).</summary>
        public const int MinVoxels=16;
        /// <summary>A blast or impact at most this high above its floor is on the ground: its shell is a dome rising from the floor.
        /// Higher up (a wall or ceiling hit, an airburst) the shell is a full sphere around it (owner 2026-10-11).</summary>
        public const double NearGround=1.2;
        /// <summary>The floor to pass for a point with nothing under it (a pit, the sky): nothing lands, the shell is a sphere.</summary>
        public const double NoFloor=-1e9;
        /// <summary>three.js PointLight intensity to Unity built-in intensity; the gain NativeCombatGlow already uses.</summary>
        public const float UnityLightGain=.35f;
        public readonly NativeBlastPool Solid=new NativeBlastPool(SolidCapacity,false),Glow=new NativeBlastPool(GlowCapacity,true);
        public readonly NativeBlastFlash[] Flashes=new NativeBlastFlash[LightCapacity];
        public double Time{get;private set;}
        public int FieldClocks=>fieldClock.Count;
        /// <summary>Seconds a shock front record lives: its FrontSeconds of travel, then a .15 s fade at the gameplay radius.</summary>
        public const double ShockLife=.45;
        /// <summary>
        /// The live shock fronts, oldest first (owner 2026-10-11 effects overhaul): one record per blast shell, impact front and
        /// Nova front, aged and expired in Update and emptied by Clear and Reset. Read only; the list is reused, so copy a
        /// record rather than hold the list across frames. The fronts' Shell voxels stay in the glow pool for parity; a
        /// renderer draws these records instead of those voxels.
        /// </summary>
        public IReadOnlyList<NativeBlastShock> Shocks=>shocks;
        private readonly List<NativeBlastShock> shocks=new List<NativeBlastShock>(32);private int nextShock;
        private Func<double> random;private int nextLight;
        private readonly Dictionary<long,double> fieldClock=new Dictionary<long,double>();
        private readonly HashSet<long> locked=new HashSet<long>();private readonly List<long> stale=new List<long>();
        public NativeBlastSim(Func<double> random=null){this.random=random??new NativeBlastRandom().Next;for(int i=0;i<LightCapacity;i++)Flashes[i]=new NativeBlastFlash();}
        // Records a front (no random draw). delay: born that much later (Implosion's pop); travel, height and life: the slam wave's.
        private void Shock(NativeShockKind kind,NativeBlastStyle? style,double x,double y,double z,double start,double radius,bool dome,double floor,int color0,int color1,int core,int voxels,double delay=0,double travel=0,double height=0,double life=ShockLife)
        {
            var s=new NativeBlastShock{Kind=kind,Style=style,X=x,Y=y,Z=z,Start=start,Radius=radius,Dome=dome,Floor=floor,Born=Time+delay,Life=life,Seed=nextShock,Voxels=voxels,Travel=travel,Height=height};
            nextShock=(nextShock+1)&NativeBlastPool.SeedMask;
            Linear(color0,out s.HotR,out s.HotG,out s.HotB);Linear(color1,out s.CoolR,out s.CoolG,out s.CoolB);Linear(core,out s.CoreR,out s.CoreG,out s.CoreB);
            shocks.Add(s);
        }

        private static readonly int[][] palette={
            /*fire*/new[]{0xfff6d2,0xffb340,0xd8401c,0x57504c,0x5b4636},/*shock (toon pass: violet only)*/new[]{0xffffff,0xc9a2ff,0x8a4cff,0x2a2048,0xb98cff},
            /*implosion*/new[]{0xffffff,0xd59bff,0x7a32d8,0x231834,0x6b45a8},/*frost*/new[]{0xffffff,0xc9f4ff,0x5fb8ef,0xb9dceb,0xe6fbff},
            /*toxic*/new[]{0xf2ffd0,0xb8f53c,0x4f9a25,0x3c5a2a,0x4a5a30},/*void*/new[]{0xf3e2ff,0xbc72ff,0x4a1d86,0x1c1028,0x7a4ac2},
            /*nova*/new[]{0xffffff,0xfff0a8,0xffa24a,0x4a3b30,0xffd27a},/*kinetic*/new[]{0xffffff,0xbff7ff,0x52c7ff,0x3b4650,0x8a99a6},
            /*quake*/new[]{0xfff1d8,0xffc27a,0x9a6a3a,0x6b5a48,0x5d4b3a},/*meteor*/new[]{0xfff2c4,0xff9a2e,0xc8321a,0x332b28,0x4a3a30},
            /*mine*/new[]{0xffe9e0,0xff6a5a,0xb8202c,0x352c2e,0x4b3d3a}};
        // The source's Shock palette (blue), which the field emitters keep: the toon pass retints only the Shock blasts.
        private static readonly int[] sourceShock={0xffffff,0xbfe6ff,0x6f8dff,0x2a3048,0x8fd6ff};
        private struct Palette{public int Core,Hot,Cool,Smoke,Debris;}
        private static Palette Colors(NativeBlastStyle style,NativeBlastTint? tint,bool field=false)
        {var c=field&&style==NativeBlastStyle.Shock?sourceShock:palette[(int)style];var p=new Palette{Core=c[0],Hot=c[1],Cool=c[2],Smoke=c[3],Debris=c[4]};if(tint.HasValue){p.Core=tint.Value.Core;p.Hot=tint.Value.Hot;p.Cool=tint.Value.Cool;}return p;}
        /// <summary>Hue band (degrees) that reads as cyan through blue.</summary>
        public const double BlueCyanFrom=165,BlueCyanTo=250;
        /// <summary>An sRGB colour with a visible tint (max - min channel at least 24) whose hue lies in the cyan-to-blue band.
        /// Whites, greys, violets and reds are not.</summary>
        public static bool BlueCyan(int hex)
        {
            int r=(hex>>16)&255,g=(hex>>8)&255,b=hex&255,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b));double d=max-min;if(d<24)return false;
            double hue=max==r?60*(((g-b)/d+6)%6):max==g?60*((b-r)/d+2):60*((r-g)/d+4);
            return hue>=BlueCyanFrom&&hue<=BlueCyanTo;
        }
        /// <summary>
        /// Owner direction 2026-10-10 (Windows test build): "Remove that blue/cyan lightning that shows when an area of effect
        /// is happening." A Shock blast draws the source's lightning (the jagged bolt from the sky with its side sparks, and the
        /// skittering ground arcs) only for a tinted look that is not blue or cyan: Ball Lightning's violet and the Orbital Mark's
        /// red keep it; Thunder Step, Static, Storm In A Jar, Lightning Rod and thunder (the plain palette) and Blink Strike (the
        /// cyan tint) draw none. Toon pass (2026-10-11): the plain Shock palette is violet now, and those looks still draw no
        /// sky bolt or ground arcs (the cut the owner approved stays); every Shock look that is not blue or cyan gets only the
        /// very short violet crackle near its core (Crackle). The storm field draws no strikes at all (Field).
        /// </summary>
        public static bool Lightning(NativeBlastStyle style,NativeBlastTint? tint)=>style==NativeBlastStyle.Shock&&tint.HasValue&&!BlueCyan(tint.Value.Hot);
        /// <summary>The source's end colour for every blast's smoke, a near-black.</summary>
        public const int DarkSmoke=0x16181c;
        /// <summary>A light style's smoke ends at SmokeDim of its own luminance, SmokeGrey of the way to grey; never below SmokeFloor.</summary>
        public const double SmokeDim=.5,SmokeGrey=.4,SmokeFloor=.35;
        /// <summary>Styles whose smoke is steam, frost or dust: Frost (and the Geyser Wall tint), Kinetic (Tidal Wave, frag, dash
        /// and block pulses), Shock, Nova, Implosion and Quake (its smoke is the ground's dust; review 2026-10-11: it turned to
        /// soot). Fire, Mine, Meteor, Toxic and Void smoke stays sooty.</summary>
        public static bool LightSmoke(NativeBlastStyle style)=>style==NativeBlastStyle.Frost||style==NativeBlastStyle.Kinetic||style==NativeBlastStyle.Shock||style==NativeBlastStyle.Nova||style==NativeBlastStyle.Implosion||style==NativeBlastStyle.Quake;
        /// <summary>Relative luminance (Rec. 709 weights) of an sRGB hex in the linear working space.</summary>
        public static double Luminance(int hex){Linear(hex,out double r,out double g,out double b);return .2126*r+.7152*g+.0722*b;}
        /// <summary>The style's smoke colour (the source palette; tints do not change it).</summary>
        public static int SmokeColor(NativeBlastStyle style)=>palette[(int)style][3];
        /// <summary>
        /// The colour a blast's smoke fades to. Owner-visible bug fixed 2026-10-11 (effects overhaul): the source fades every
        /// style's smoke to <see cref="DarkSmoke"/>, so frost steam and geyser spray turned black. A light style
        /// (<see cref="LightSmoke"/>) now ends at a darker, partly desaturated version of its own smoke colour, at SmokeDim of
        /// its luminance (never below SmokeFloor of it, since the colour lerp is linear); a dark style keeps the near-black.
        /// Toon pass (2026-10-11): a dark style's smoke ends slate (Fire, Meteor: <see cref="Slate"/>; Mine a cooler slate;
        /// Toxic blue-black; Void deep violet), never the near-black DarkSmoke.
        /// </summary>
        public static int SmokeEnd(NativeBlastStyle style)
        {
            if(!LightSmoke(style))return style==NativeBlastStyle.Toxic?ToxicDark:style==NativeBlastStyle.Void?0x120a24:style==NativeBlastStyle.Mine?0x3b3f50:Slate;
            Linear(SmokeColor(style),out double r,out double g,out double b);double l=.2126*r+.7152*g+.0722*b;
            return Hex((r+(l-r)*SmokeGrey)*SmokeDim,(g+(l-g)*SmokeGrey)*SmokeDim,(b+(l-b)*SmokeGrey)*SmokeDim);
        }
        /// <summary>Maps an authoritative explosion message (the engine's blast kind, chip or ability fx tag) to a style.</summary>
        public static NativeBlastStyle StyleOf(string message)
        {
            switch(message)
            {
                case "thunder":case "thunderstep":case "static":case "storm":case "lightning-rod":case "orbital-mark":case "blink-strike":case "ball-lightning":return NativeBlastStyle.Shock;
                case "implosion":case "implosion-bomb":case "singularity":return NativeBlastStyle.Implosion;
                case "frost":case "blizzard":case "geyser-wall":return NativeBlastStyle.Frost;
                case "corpseParty":case "toxic":case "acid-rain":return NativeBlastStyle.Toxic;
                case "meteor":case "artillery":case "meteor-strike":return NativeBlastStyle.Meteor;
                case "mine":case "cluster-mine":case "mirror-mine":case "bread-crumbs":case "proximity-mail":return NativeBlastStyle.Mine;
                case "dashBlast":case "kinetic":case "bubble-mine":case "frag":case "tidal-wave":return NativeBlastStyle.Kinetic;
                case "quake":case "aftershock":case "trampoline":return NativeBlastStyle.Quake;
                case "vacuum":case "void":case "black-hole":case "wormhole":case "shackles":return NativeBlastStyle.Void;
                case "blockPulse":case "nova":return NativeBlastStyle.Nova;
                default:return NativeBlastStyle.Fire;
            }
        }
        /// <summary>Distinct looks for ability tags that share a style (source §13.1).</summary>
        public static NativeBlastTint? TintOf(string tag)
        {
            switch(tag)
            {
                case "frag":return new NativeBlastTint(0xfff3e2,0xcfc8bc,0x676b72);case "tidal-wave":return new NativeBlastTint(0xeaffff,0x8de9ee,0x2b8bd6,NativeBlastLook.Tide);
                case "geyser-wall":return new NativeBlastTint(0xf2ffff,0x9cefff,0x2a9ae6,NativeBlastLook.Geyser);case "orbital-mark":return new NativeBlastTint(0xffffff,0xff9a8a,0xe8323a);
                case "blink-strike":return new NativeBlastTint(0xf2ffff,0x9ffcff,0x1fc4e6);case "ball-lightning":return new NativeBlastTint(0xfbf0ff,0xd7a6ff,0x8a4cff);
                case "flame-lance":return new NativeBlastTint(0xffffff,0xfff0b0,0xffc23a);default:return null;
            }
        }
        /// <summary>three.js Color.setHex: sRGB hex to the linear working space.</summary>
        public static void Linear(int hex,out double r,out double g,out double b){r=ToLinear(((hex>>16)&255)/255.0);g=ToLinear(((hex>>8)&255)/255.0);b=ToLinear((hex&255)/255.0);}
        private static double ToLinear(double c)=>c<0.04045?c*0.0773993808:Math.Pow(c*0.9478672986+0.0521327014,2.4);
        private static double ToSrgb(double c)=>c<0.0031308?c*12.92:1.055*Math.Pow(c,0.41666)-0.055;
        /// <summary>three.js Color.getHex from linear channels.</summary>
        public static int Hex(double r,double g,double b){int H(double c)=>(int)Round(Math.Max(0,Math.Min(255,ToSrgb(c)*255)));return H(r)*65536+H(g)*256+H(b);}
        /// <summary>JavaScript Math.round (halves round up), not banker's rounding.</summary>
        public static double Round(double v)=>Math.Floor(v+.5);
        private static double Clamp01(double v)=>Math.Max(0,Math.Min(1,v));
        public static float UnityLightIntensity(double three)=>(float)three*UnityLightGain;
        public static double ThreeLightIntensity(float unity)=>unity/UnityLightGain;

        private void Flash(double x,double y,double z,int color,double radius,double peak,double life,bool compat,double quality,bool minor=false)
        {
            if(compat||quality<.5)return;NativeBlastFlash f=null;
            if(minor){foreach(var c in Flashes)if(Time-c.At>=c.Life){f=c;break;}}else f=Flashes[nextLight++%Flashes.Length];
            if(f==null)return;f.X=x;f.Y=y+.4;f.Z=z;f.Color=color;f.Distance=Math.Max(4,radius*3);f.At=Time;f.Peak=peak;f.Life=life;
        }
        // Sphere of directions, roughly even (golden spiral) with jitter.
        private void Dir(int i,int n,bool hemi,out double x,out double y,out double z)
        {y=hemi?1-(i+.5)/n:1-2*(i+.5)/n;double r=Math.Sqrt(Math.Max(0,1-y*y)),phi=i*2.39996+random()*.4;x=Math.Cos(phi)*r;z=Math.Sin(phi)*r;}
        // The shock shell: the volumetric read of the blast radius (ends exactly on it). On the ground (near) the source's dome
        // rising from the floor point under the blast; in the air (a wall or ceiling hit, an airburst) a full sphere centred on
        // the blast whose voxels stop on the floor. It also carries the voxels of the source's flat ground disc (the sweep that
        // filled 92% of the radius at floor height), which is cut: owner 2026-10-11, on a wall it read as "a floating pancake".
        // delay (toon pass): the shell starts that much later (Implosion's pop), voxels and record alike.
        private void Shell(double px,double py,double pz,double floor,bool near,double radius,int color0,int color1,int count,int core,NativeBlastStyle style,double delay=0)
        {
            double r0=Math.Min(.25,radius*.2),speed=(radius-r0)/FrontSeconds,y0=near?floor:py,rest=near?NoFloor:floor;
            Shock(NativeShockKind.Blast,style,px,y0,pz,r0,radius,near,floor,color0,color1,core,count,delay);
            // A delayed shell's voxels wait a millisecond longer, so a fixed-step clock never moves them on the frame the delay ends
            // (their age reaches 0 within float rounding): they then reach the radius exactly FrontSeconds after the record's birth.
            double wait=delay>0?delay+.001:0;
            for(int i=0;i<count;i++){Dir(i,count,near,out double x,out double y,out double z);Glow.Spawn(px+x*r0,y0+y*r0,pz+z*r0,x*speed,y*speed,z*speed,FrontSeconds+.001,.2,.1,color0,color1,0,0,wait,rest,role:NativeFxRole.Shell);}
        }
        // Voxels that start on the radius and rush to the centre in t0 (implosions, vacuum); they never drop.
        private void Inward(double cx,double cy,double cz,double R,int count,int c0,int c1,double t0,double swirl=0,double lift=.7)
        {
            for(int i=0;i<count;i++)
            {
                Dir(i,count,true,out double x,out double y,out double z);double h=Math.Sqrt(x*x+z*z);if(h==0)h=1;double X=x/h*R,Z=z/h*R,Y=(y-.3)*R*lift;
                Glow.Spawn(cx+X,cy+Y,cz+Z,-X/t0-Z*swirl,-Y/t0,-Z/t0+X*swirl,t0,.16,.24,c0,c1,0,0,0,-1e9,1,swirl!=0?3:0,role:NativeFxRole.Streak);
            }
        }
        /// <summary>A line of glow voxels from a to b (arcs, teleport and rally streaks); sweep delays the far end so it travels.</summary>
        public void Line(double ax,double ay,double az,double bx,double by,double bz,int color0,int color1,double quality,double jitter=.25,double life=.14,double sweep=0)
        {
            double d=Math.Sqrt((bx-ax)*(bx-ax)+(by-ay)*(by-ay)+(bz-az)*(bz-az));int c=(int)Math.Min(40,Math.Max(3,Round(d*5*quality))),strand=Glow.NextSeed;
            for(int i=0;i<=c;i++){double u=(double)i/c,j=i>0&&i<c?jitter:0;Glow.Spawn(ax+(bx-ax)*u+(random()-.5)*j,ay+(by-ay)*u+(random()-.5)*j,az+(bz-az)*u+(random()-.5)*j,0,0,0,life,.11,.05,color0,color1,0,0,u*sweep,strand:strand,role:NativeFxRole.Line);}
        }
        /// <summary>Pong rally paddle flash: a 4x4 voxel square at the bounce point, facing and drifting along the rally.</summary>
        public void Paddle(double x,double y,double z,double dx,double dy,double dz,int color,double quality)
        {
            if(quality<=0)return;double l=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(l==0)l=1;double nx=dx/l,ny=dy/l,nz=dz/l,ux=-nz,uz=nx,h=Math.Sqrt(ux*ux+uz*uz);
            if(h<.01){ux=1;uz=0;}else{ux/=h;uz/=h;}double vx=-ny*uz,vy=nz*ux-nx*uz,vz=ny*ux;
            for(int i=0;i<4;i++)for(int j=0;j<4;j++){double a=(i-1.5)*.16,b=(j-1.5)*.16;Glow.Spawn(x+ux*a+vx*b,y+vy*b,z+uz*a+vz*b,nx*1.5,ny*1.5,nz*1.5,.15,.15,.06,0xffffff,color,role:NativeFxRole.Mote);}
        }
        /// <summary>
        /// Small volumetric impact (chip procs, splashes, field arrivals): a voxel front ending on radius, core, debris, motes, an
        /// idle light. ground: the impact lies on the floor, so its front is a dome rising from it; otherwise, or more than
        /// NearGround above its floor, the front is a sphere around the point (stopping on the floor when it is that high). The
        /// source also slid a flat disc of voxels over the floor under a ground impact; that disc is cut (owner 2026-10-11) and its
        /// voxels go to the front. floor: the floor height under the point (the presentation raycasts it, NoFloor for none); null
        /// keeps the source's py - .95. Debris land on it.
        /// </summary>
        public void Impact(double px,double py,double pz,double radius,int hot,double quality,bool compat=false,bool ground=false,int? cool=null,double? floor=null)
        {
            int cold;if(cool.HasValue)cold=cool.Value;else{Linear(hot,out double lr,out double lg,out double lb);cold=Hex(lr*.45,lg*.45,lb*.45);}
            double q=Clamp01(quality)*(compat?.5:1);if(q<=.02)return;
            double R=Math.Max(.3,radius),t0=FrontSeconds,g=floor??py-.95,scale=Math.Min(1.5,.6+R*.2);bool near=py-g<=NearGround,dome=ground&&near;
            double y0=dome?g+.12:py,rest=near?NoFloor:g;int N(double b)=>(int)Math.Max(2,Round(b*q*scale));
            for(int i=0,c=ground?N(14)+N(26):N(20);i<c;i++){if(i==0)Shock(NativeShockKind.Impact,null,px,y0,pz,.1,R,dome,g,0xffffff,hot,0xffffff,c);Dir(i,c,dome,out double x,out double y,out double z);double s=(R-.1)/t0;Glow.Spawn(px+x*.1,y0+y*.1,pz+z*.1,x*s,y*s,z*s,t0+.001,.13,.06,0xffffff,hot,0,0,0,rest,role:NativeFxRole.Shell);}
            for(int i=0,c=N(5);i<c;i++)Glow.Spawn(px,y0+.05,pz,(random()-.5)*1.2,(random()-.2)*1.2,(random()-.5)*1.2,.16,.26,.08,0xffffff,hot,role:NativeFxRole.Core);
            for(int i=0,c=N(7);i<c;i++){Dir(i,c,true,out double x,out double y,out double z);double s=R*(1.5+random()*2);Solid.Spawn(px,y0,pz,x*s,Math.Abs(y)*s+1.5,z*s,.5+random()*.3,.07+random()*.06,.04,hot,cold,12,.6,0,g+.04,1,6,role:NativeFxRole.Debris);}
            for(int i=0,c=N(6);i<c;i++){double a=random()*Math.PI*2,r=R*Math.Sqrt(random())*.9;Glow.Spawn(px+Math.Cos(a)*r,y0,pz+Math.Sin(a)*r,0,1.4+random(),0,.5+random()*.3,.07,.03,hot,cold,0,.4,.05,role:NativeFxRole.Mote);}
            Flash(px,py,pz,hot,R,2.5+R,.22,compat,q*2,true);
        }
        /// <summary>Seconds Implosion's inrush runs before its pop (toon pass): the pop's flash, puffs, sparks and its shock
        /// front are born this much after the blast, so the front reaches the gameplay radius at ImplosionPop + FrontSeconds.</summary>
        public const double ImplosionPop=.25;
        /// <summary>Quake's slam wave (toon pass): an upright open cylinder that grows from SlamStart to SlamReach of the
        /// radius in SlamTravel seconds, SlamHeight of the radius tall (at least SlamMinHeight m), and is gone by SlamLife.</summary>
        public const double SlamStart=.25,SlamReach=.85,SlamTravel=.22,SlamLife=.42,SlamHeight=.33,SlamMinHeight=.5;
        /// <summary>The toon colours of the 2026-10-11 toon pass (after the owner's Asset Store packs; colours are our own picks
        /// from the study's ramps): fire puffs start yellow and end slate (never black), Toxic dark green to blue-black, Void
        /// lilac to navy, Kinetic white to blue-grey, Quake sand to umber, frost and water mist pale ice.</summary>
        public const int FireYellow=0xffce00,Slate=0x3e4556,SlateLight=0x5a6580,ToxicGreen=0x3cae20,ToxicDark=0x2d2e3c,ToxicLime=0xb8f53c,
            VoidInk=0x0a0614,VoidLilac=0xd5a5ff,VoidNavy=0x1a0d3a,KineticWhite=0xeef2f8,KineticShade=0x7a88a6,QuakeSand=0xb39a78,QuakeUmber=0x5f5040,
            MistIce=0xe6fbff,MistShade=0xa9d0e4,WaterLit=0xe8fbff,WaterShade=0x2a9ae6;
        // Per-style toon fire: the flame a fire puff starts in and the ash it ends in (Fire, Mine, Meteor).
        private static int Flame(NativeBlastStyle style)=>style==NativeBlastStyle.Mine?0xff7a3d:style==NativeBlastStyle.Meteor?0xffb02e:FireYellow;
        private static int Ash(NativeBlastStyle style)=>style==NativeBlastStyle.Mine?0x3b3f50:Slate;
        /// <summary>
        /// One authoritative blast. radius is the gameplay radius from the effect; the shock front and shell end exactly on it
        /// (Implosion's after its pop), the puffs fill about it, debris and sparks may travel past it (cosmetic). floor: the floor
        /// height under the blast (the presentation raycasts it, NoFloor for none); null keeps the source's py - .95. Pieces
        /// land on it. Within NearGround of it the shell is a dome on the floor and ground rings start on it; higher up the
        /// shell is a sphere around the blast and the rings blow out at its height. No flat ground disc or ring (owner
        /// 2026-10-11). Toon pass (owner 2026-10-11, after the Asset Store pack study; Unity-owned since, see
        /// Tests/blast-pinned.json): each style's composition, counts and timing follow the study's toon references, scaled to
        /// the radius (size 1 at 3 m): Fire, Mine and Meteor pop in under a second (a flash, toon fire puffs blown out from a
        /// ground circle and up a cone that turn from flame to slate, crescent licks, hot debris, sparks); Kinetic a white poof
        /// (the Tidal Wave's a water pop); Frost ice spikes, crystals, faint mist and lingering snow (the Geyser Wall's a water
        /// column); Toxic dark green puffs, acid blobs leaving puff trails and a lingering cloud; Void black shapes over a violet
        /// glow with inward streaks; Shock violet only, a strobing glow and very short arcs; Quake a dust ring of ground-hugging
        /// puffs and a short upright slam wave; Nova a gold flash and radial licks; Implosion an inrush, then a violet pop.
        /// Returns the particles spawned.
        /// </summary>
        public int Blast(NativeBlastStyle style,double px,double py,double pz,double radius,double quality=1,bool compat=false,NativeBlastTint? tint=null,double? floor=null)
        {
            double q=Clamp01(quality);if(q<=0)return 0;int spawned=Solid.Spawned+Glow.Spawned;var pal=Colors(style,tint);var look=tint?.Look??NativeBlastLook.Default;
            double R=Math.Max(.4,radius),k=q*(compat?.4:1)*Math.Min(1.6,.55+R*.18),ground=floor??py-.95,cx=px,cy=Math.Max(ground+.35,py),cz=pz,sc=Math.Max(.45,Math.Min(2.2,R/3));
            // On the ground the shell, the rings, the quake's chunks and the lightning rise from the floor. In the air they start at
            // the blast, and its airborne glow (core, shell, Nova's front) stops on the floor.
            bool near=py-ground<=NearGround;double y0=near?ground:py,rest=near?NoFloor:ground;
            int N(double b)=>(int)Math.Max(1,Round(b*k));int M(double b)=>(int)Math.Max(MinVoxels,Round(b*k));
            double J(double a,double b)=>a+(b-a)*random();
            // Where puff i of c starts and how it flies. shape 0: a ring blown outward from r0 round the centre, on the floor when
            // near it (ground-hugging, a little lift), else at the blast's height; 1: a burst from a small sphere round the centre;
            // 2: up a cone of half-angle cone (radians). Speeds between vmin and vmax; up adds to the vertical speed.
            void Emit(int shape,int i,int c,double r0,double vmin,double vmax,double up,double cone,out double x,out double y,out double z,out double vx,out double vy,out double vz)
            {
                double s=J(vmin,vmax);
                if(shape==0){double a=(i+random()*.6)/c*Math.PI*2,ca=Math.Cos(a),sa=Math.Sin(a);x=cx+ca*r0;z=cz+sa*r0;y=near?ground+.18*sc:cy;vx=ca*s;vz=sa*s;vy=up*J(.5,1.5);}
                else if(shape==1){Dir(i,c,false,out double dx,out double dy,out double dz);x=cx+dx*r0;y=Math.Max(cy+dy*r0,ground+.1);z=cz+dz*r0;vx=dx*s;vy=dy*s*.8+up;vz=dz*s;}
                else{double a=random()*Math.PI*2,t=cone*Math.Sqrt(random()),st=Math.Sin(t);x=cx;y=cy;z=cz;vx=Math.Cos(a)*st*s;vy=Math.Cos(t)*s;vz=Math.Sin(a)*st*s;}
            }
            // Toon puffs of each role: Fireball (flame to ash), Smoke (lit, its colours), Water (lit tone, shadow tone), Cloud (a
            // little glow), Mist (faint). Every puff lands on the floor; gravity -rise lifts it, drag brakes it.
            void FirePuffs(int c,int shape,double r0,double vmin,double vmax,double up,double cone,double life,double size,int c0,int c1,double drag,double rise,double delay=0)
            {for(int i=0;i<c;i++){Emit(shape,i,c,r0,vmin,vmax,up,cone,out double x,out double y,out double z,out double vx,out double vy,out double vz);Solid.Spawn(x,y,z,vx,vy,vz,life*J(.65,1),size*J(.85,1.15),size*.5,c0,c1,-rise,drag,delay,ground,1,2,role:NativeFxRole.Fireball);}}
            void SmokePuffs(int c,int shape,double r0,double vmin,double vmax,double up,double cone,double life,double size,int c0,int c1,double drag,double rise,double delay=0)
            {for(int i=0;i<c;i++){Emit(shape,i,c,r0,vmin,vmax,up,cone,out double x,out double y,out double z,out double vx,out double vy,out double vz);Solid.Spawn(x,y,z,vx,vy,vz,life*J(.7,1),size*J(.85,1.15),size*.5,c0,c1,-rise,drag,delay,ground,1,1,role:NativeFxRole.Smoke);}}
            void WaterPuffs(int c,int shape,double r0,double vmin,double vmax,double up,double cone,double life,double size,double gravity,double drag,double delay=0)
            {for(int i=0;i<c;i++){Emit(shape,i,c,r0,vmin,vmax,up,cone,out double x,out double y,out double z,out double vx,out double vy,out double vz);Solid.Spawn(x,y,z,vx,vy,vz,life*J(.75,1),size*J(.8,1.2),size*.5,WaterLit,WaterShade,gravity,drag,delay,ground,1,0,role:NativeFxRole.Water);}}
            void CloudPuffs(int c,int shape,double r0,double vmin,double vmax,double up,double life,double size,int c0,int c1,double drag,double rise,double delay=0,double spread=0)
            {for(int i=0;i<c;i++){Emit(shape,i,c,r0,vmin,vmax,up,0,out double x,out double y,out double z,out double vx,out double vy,out double vz);Solid.Spawn(x,y,z,vx,vy,vz,life*J(.7,1),size*J(.85,1.2),size*.5,c0,c1,-rise,drag,delay+spread*i/Math.Max(1,c),ground,1,.5,role:NativeFxRole.Cloud);}}
            void MistPuffs(int c,int shape,double r0,double vmin,double vmax,double up,double life,double size,int c0,int c1,double delay=0,double lift=0)
            {for(int i=0;i<c;i++){Emit(shape,i,c,r0,vmin,vmax,up,0,out double x,out double y,out double z,out double vx,out double vy,out double vz);Solid.Spawn(x,y+lift,z,vx,vy,vz,life*J(.75,1),size*J(.85,1.2),size*.5,c0,c1,-.15,1.4,delay,ground,1,.4,role:NativeFxRole.Mist);}}
            // The white-hot centre (Core glows) and one big soft flash glow of size m that shrinks to a third.
            void Core(double count,double spread,double life,double size,double rise=0,double delay=0)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double s=spread*(.6+random()*.6);Glow.Spawn(cx,cy,cz,x*s,y*s*.8+rise,z*s,life*(.7+random()*.5),size*(.8+random()*.5),size*.3,pal.Core,pal.Hot,-rise*.5,3.5,delay,rest,role:NativeFxRole.Core);}}
            void Halo(double size,double life,int c0,int c1,double delay=0)=>Glow.Spawn(cx,cy,cz,0,0,0,life,size*sc,size*sc*.35,c0,c1,0,0,delay,rest,role:NativeFxRole.Halo);
            // Round flash flipbooks at the centre, a little apart, staggered by 20 ms.
            void Flashes(int c,double size,double life,int c0,int c1,double delay=0)
            {for(int i=0;i<c;i++){double a=random()*Math.PI*2,r=(i==0?0:.25)*sc;Glow.Spawn(cx+Math.Cos(a)*r,cy+.1*sc+(i==0?0:.2*sc),cz+Math.Sin(a)*r,0,.4,0,life*J(.85,1.15),size*sc*J(.85,1.15),size*sc,c0,c1,0,2,delay+i*.02,rest,role:NativeFxRole.Flash);}}
            // Crescent licks flung out from a small sphere (shape 1) or radially round the centre at its height (horizontal).
            void Licks(int c,bool radial,double vmin,double vmax,double life,double size,int c0,int c1,double delay=0)
            {
                for(int i=0;i<c;i++)
                {
                    double x,y,z,s=J(vmin,vmax)*sc;
                    if(radial){double a=(i+random()*.5)/c*Math.PI*2;x=Math.Cos(a);z=Math.Sin(a);y=(random()-.5)*.3;}else{Dir(i,c,false,out x,out y,out z);y=Math.Abs(y)*.8+.1;}
                    Glow.Spawn(cx+x*.3*sc,cy+y*.3*sc,cz+z*.3*sc,x*s,y*s,z*s,life*J(.8,1.1),size*sc*J(.85,1.15),size*sc*.25,c0,c1,0,2.5,delay,rest,role:NativeFxRole.Lick);
                }
            }
            // Fire, Mine and Meteor debris is HotDebris (drawn glowing out of the fireball, cooling); every other style's is Debris.
            bool hot=style==NativeBlastStyle.Fire||style==NativeBlastStyle.Mine||style==NativeBlastStyle.Meteor;
            void Debris(double count,double power,int color,double delay=0)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,true,out double x,out double y,out double z);double s=power*(.5+random()*.8);Solid.Spawn(cx,cy,cz,x*s,Math.Abs(y)*s+2+random()*3,z*s,.9+random()*.6,.1+random()*.12,.06,color,color,14,.4,delay,ground+.04,1,6,role:hot?NativeFxRole.HotDebris:NativeFxRole.Debris);}}
            // Stretched sparks: a quick burst braking and falling (the toon references' sparks), streak length from the speed.
            void Sparks(double count,double vmin,double vmax,double life,int c0,int c1,double gravity=9.8,double delay=0)
            {for(int i=0,c=N(count);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double s=J(vmin,vmax)*sc;Glow.Spawn(cx,cy,cz,x*s,Math.Abs(y)*s*.9+1,z*s,life*J(.6,1),.06,.03,c0,c1,gravity,1.6,delay,ground+.03,4,role:NativeFxRole.Spark);}}
            switch(style)
            {
                case NativeBlastStyle.Fire:case NativeBlastStyle.Mine:case NativeBlastStyle.Meteor:
                {
                    bool mine=style==NativeBlastStyle.Mine,meteor=style==NativeBlastStyle.Meteor;int flame=Flame(style),ash=Ash(style);
                    Halo(meteor?3.1:2.5,.2,pal.Core,pal.Hot);Core(4,R*1.2,.22,.5);Flashes(meteor?3:2,1.9,.26,pal.Core,flame);
                    FirePuffs(N(mine?12:meteor?18:14),0,.55*sc,8*sc,(meteor?13:11)*sc,1.1,0,.8,.95*sc,flame,ash,6.5,1);
                    FirePuffs(N(mine?6:10),2,0,2*sc,12*sc,0,.24,.8,1.1*sc,flame,ash,4.5,.5);
                    Licks(N(7),false,8,15,.28,.5,pal.Core,flame);
                    Shell(px,py,pz,ground,near,R,pal.Hot,pal.Cool,M(36),pal.Core,style);
                    SmokePuffs(N(5),1,.6*sc,.6,1.2,1.2,0,1.3,1.2*sc,ash,SlateLight,1.5,.35,.16);
                    Debris(meteor?14:10,R*2.2,pal.Debris);Sparks(mine?30:22,7,24,.45,pal.Core,pal.Hot);
                    if(meteor)
                    {
                        for(int i=0,c=N(10);i<c;i++)Glow.Spawn(cx+(random()-.5)*.6,cy+.2,cz+(random()-.5)*.6,(random()-.5)*1.4,6+random()*5,(random()-.5)*1.4,.55,.22,.06,pal.Core,pal.Cool,8,.5,role:NativeFxRole.Ember);
                        // The heavy spark fountain: shot up, slammed down by high gravity.
                        for(int i=0,c=N(14);i<c;i++){double a=random()*Math.PI*2,t=.45*Math.Sqrt(random()),s=J(12,18)*Math.Sqrt(sc);Glow.Spawn(cx,cy,cz,Math.Cos(a)*Math.Sin(t)*s,Math.Cos(t)*s,Math.Sin(a)*Math.Sin(t)*s,.75,.07,.03,pal.Core,pal.Hot,25,.3,0,ground+.03,4,role:NativeFxRole.Spark);}
                    }
                    Flash(cx,cy,cz,pal.Hot,R,9+R*2,.38,compat,q);break;
                }
                case NativeBlastStyle.Kinetic:
                    if(look==NativeBlastLook.Tide)
                    {
                        // Tidal Wave: a water pop, droplets and a little mist instead of dust and rubble.
                        Core(5,R*1.4,.16,.3);WaterPuffs(N(14),1,.25*sc,3*sc,7*sc,1.5,0,.85,.95*sc,4,3.5);
                        for(int i=0,c=N(18);i<c;i++){Dir(i,c,true,out double x,out double y,out double z);double s=J(4,9)*sc;Glow.Spawn(cx,cy,cz,x*s,Math.Abs(y)*s+3,z*s,J(.5,.9),.05,.03,WaterLit,pal.Hot,9.8,.4,0,ground+.02,1,role:NativeFxRole.Droplet);}
                        MistPuffs(N(3),1,.3*sc,.4,1,.3,1.2,1.5*sc,MistIce,MistShade,.05);
                    }
                    else
                    {
                        // A white poof (frag: its warm greys), snap streaks, metal debris.
                        bool tinted=tint.HasValue;Core(6,R*1.6,.16,.32);
                        SmokePuffs(N(14),1,.2*sc,3*sc,7*sc,.5,0,.95,1.05*sc,tinted?pal.Core:KineticWhite,tinted?pal.Cool:KineticShade,4.5,.3);
                        Sparks(14,9,16,.3,pal.Core,pal.Hot,4);Debris(12,R*2.2,pal.Debris);Sparks(8,6,14,.45,pal.Core,pal.Cool);
                    }
                    Shell(px,py,pz,ground,near,R,pal.Hot,pal.Cool,M(48),pal.Core,style);Flash(cx,cy,cz,pal.Hot,R,6+R,.28,compat,q);break;
                case NativeBlastStyle.Quake:
                {
                    // The radius read: the shell's voxels (not drawn) and the slam wave, an upright open cylinder on the floor.
                    Shell(px,py,pz,ground,near,R,pal.Hot,pal.Cool,M(30),pal.Core,style);
                    Shock(NativeShockKind.Slam,style,px,y0,pz,SlamStart*R,SlamReach*R,false,ground,0xfff0d0,0xc8a070,0xffffff,0,0,SlamTravel,Math.Max(SlamMinHeight,SlamHeight*R),SlamLife);
                    for(int i=0,c=N(14);i<c;i++){double a=(double)i/c*Math.PI*2,r=R*(.25+random()*.6),x=Math.Cos(a)*r,z=Math.Sin(a)*r;Solid.Spawn(px+x,y0,pz+z,x*.6,5+random()*4,z*.6,1.1,.26+random()*.24,.1,pal.Debris,0x3a3028,16,.3,random()*.12,ground+.05,1,4,role:NativeFxRole.Rock);}
                    // The dust ring: ground-hugging puffs blown outward, then a few slow ones that linger.
                    SmokePuffs(N(16),0,.45*sc,6.5*sc,9.5*sc,1.8,0,.9,1.05*sc,QuakeSand,QuakeUmber,5.5,.5);
                    SmokePuffs(N(6),2,0,1.5,4,0,.6,1.1,1.15*sc,QuakeSand,QuakeUmber,2.5,.3,.03);
                    SmokePuffs(N(5),0,.6*R,.6,1,.4,0,1.5,1.4*sc,0xc9b394,0x7d6a55,1.2,.25,.12);
                    Debris(6,R*1.6,pal.Debris);Flash(cx,cy,cz,pal.Hot,R,4,.25,compat,q);break;
                }
                case NativeBlastStyle.Shock:
                {
                    // Violet only: a strobing glow, a nova flash, short arcs. The jagged bolt from the sky and its skittering ground
                    // arcs stay where Lightning allows them (the violet Ball Lightning and red Orbital Mark tints).
                    bool lightning=Lightning(style,tint);
                    if(lightning)SkyBolt(cx,cz,y0,N(22),pal);
                    Strobe(cx,cy,cz,sc,pal,rest);Flashes(2,1.6,.22,pal.Hot,pal.Cool);Core(5,R*1,.14,.28);
                    Shell(px,py,pz,ground,near,R,pal.Hot,pal.Cool,M(48),pal.Core,style);Sparks(10,5,12,.4,pal.Core,pal.Cool,19.6);
                    if(!BlueCyan(pal.Hot))Crackle(cx,cy,cz,ground,R,N(6),N(3),pal);
                    if(lightning)GroundArcs(cx,cz,y0,R,N(5),pal);
                    Flash(cx,cy,cz,pal.Hot,R,12+R*2,.3,compat,q);break;
                }
                case NativeBlastStyle.Implosion:
                    // Voxels rush in from the radius, then the centre pops (ImplosionPop later): flash, core, puffs, sparks, front.
                    Inward(cx,cy,cz,R,M(36),pal.Cool,pal.Hot,.28);
                    Core(8,R*2.2,.3,.38,0,ImplosionPop);Flashes(2,2,.24,pal.Core,pal.Hot,ImplosionPop);
                    Shell(px,py,pz,ground,near,R,pal.Hot,pal.Cool,M(30),pal.Core,style,ImplosionPop);
                    CloudPuffs(N(10),1,.3*sc,3*sc,6*sc,.3,.95,1.1*sc,0xe0b8ff,0x3a3060,3.5,.2,ImplosionPop);
                    Sparks(12,6,14,.45,pal.Core,pal.Hot,9.8,ImplosionPop);Flash(cx,cy,cz,pal.Hot,R,10,.4,compat,q);break;
                case NativeBlastStyle.Void:
                    // Black shapes over a violet glow: the glow behind, inward streaks, swelling black orbs and ink puffs, ink
                    // threads flung out, then a lilac cloud ring that sinks into navy.
                    Halo(3.1,.5,0xb27cff,0x6800ff);
                    Inward(cx,cy,cz,R,M(32),pal.Cool,pal.Hot,.4,.56,.5);
                    for(int i=0,c=N(4);i<c;i++)Solid.Spawn(cx+(random()-.5)*.5*sc,cy+(random()-.5)*.3*sc,cz+(random()-.5)*.5*sc,0,0,0,.45,.08,(.45+random()*.2)*sc,0x07030f,0x1c0c33,0,0,.04*i,-1e9,1,5,role:NativeFxRole.Dark);
                    SmokePuffs(N(8),1,.3*sc,.6,1.6*sc,0,0,.6,1.3*sc,VoidInk,0x120a24,3,0);
                    for(int i=0,c=N(12);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double s=J(10,16)*sc;Glow.Spawn(cx,cy,cz,x*s,y*s*.7,z*s,J(.2,.32),.06,.03,0x05020c,0x1a0d33,0,2,0,rest,1,role:NativeFxRole.Droplet);}
                    CloudPuffs(N(8),0,.5*sc,4*sc,6*sc,.3,1.2,1.2*sc,VoidLilac,VoidNavy,3.5,.2,.12);
                    Flash(cx,cy,cz,pal.Hot,R,5,.3,compat,q);break;
                case NativeBlastStyle.Nova:
                    Core(10,R*2,.25,.42);Flashes(1,2.4,.28,pal.Hot,pal.Cool);
                    for(int i=0,c=M(60);i<c;i++){if(i==0)Shock(NativeShockKind.Nova,style,cx,cy,cz,.25,R,false,ground,pal.Core,pal.Cool,pal.Core,c);Dir(i,c,false,out double x,out double y,out double z);double r0=.25,s=(R-r0)/FrontSeconds;Glow.Spawn(cx+x*r0,cy+y*r0,cz+z*r0,x*s,y*s,z*s,FrontSeconds+.001,.24,.12,pal.Core,pal.Cool,0,0,0,rest,role:NativeFxRole.Shell);}
                    Licks(N(16),true,12,15,.28,.55,pal.Hot,pal.Cool);Sparks(14,8,18,.45,pal.Core,pal.Hot);Flash(cx,cy,cz,pal.Hot,R,12,.4,compat,q);break;
                case NativeBlastStyle.Frost:
                    if(look==NativeBlastLook.Geyser)Geyser(cx,cz,y0,ground,near,sc,k,pal);
                    else
                    {
                        // Ice spikes and crystals, falling ice dots, faint mist and lingering snow under a cold glow.
                        Halo(2.5,.25,pal.Core,pal.Hot);Core(6,R*1.2,.2,.3);
                        for(int i=0,c=N(16);i<c;i++){Dir(i,c,false,out double x,out double y,out double z);double s=J(6,12)*sc;Glow.Spawn(cx,cy,cz,x*s,y*s*.8+1,z*s,J(.18,.42),.6*sc,.3*sc,pal.Core,pal.Hot,0,3,0,rest,1,role:NativeFxRole.Spike);}
                        for(int i=0,c=N(14);i<c;i++){Dir(i,c,true,out double x,out double y,out double z);double s=R*(1.4+random()*1.4);Solid.Spawn(cx,cy,cz,x*s,Math.Abs(y)*s+1.5,z*s,.9+random()*.4,.19,.06,pal.Debris,pal.Cool,9,1.5,0,ground+.04,3.5,role:NativeFxRole.Shard);}
                        Sparks(12,3,6,.8,0xffffff,pal.Hot,19.6);
                        MistPuffs(N(6),1,.5*sc,1*sc,3*sc,.3,1.5,1.3*sc,MistIce,MistShade);
                        for(int i=0,c=N(12);i<c;i++){double a=random()*Math.PI*2,r=R*.6*Math.Sqrt(random());Glow.Spawn(cx+Math.Cos(a)*r,ground+.5+random()*1.5,cz+Math.Sin(a)*r,(random()-.5)*.3,.2+random()*.3,(random()-.5)*.3,J(1.4,2.2),.08,.05,0xffffff,0xbfeaff,0,0,J(.05,.3),rest,role:NativeFxRole.Snow);}
                    }
                    Shell(px,py,pz,ground,near,R,pal.Core,pal.Cool,M(32),pal.Core,style);Flash(cx,cy,cz,pal.Hot,R,6,.3,compat,q);break;
                case NativeBlastStyle.Toxic:
                {
                    // Green flashes and glow, dark green puffs, acid blobs on arcs trailing puffs, then a lingering cloud.
                    Halo(2.4,.3,pal.Core,pal.Hot);Flashes(3,2,.25,pal.Core,ToxicLime);Core(5,R*1.2,.2,.3);
                    SmokePuffs(N(14),1,.25*sc,2.5*sc,7*sc,.6,0,1,1.1*sc,ToxicGreen,ToxicDark,4,.2);
                    Blobs(cx,cy,cz,ground,R,sc,N(6));
                    Sparks(8,6,14,.45,pal.Core,ToxicLime);
                    for(int i=0,c=N(10);i<c;i++){double a=random()*Math.PI*2,r=.55*R*Math.Sqrt(random());Solid.Spawn(cx+Math.Cos(a)*r,(near?ground:cy)+.35*sc,cz+Math.Sin(a)*r,Math.Cos(a)*.3,.25,Math.Sin(a)*.3,J(1,1.8),1.4*sc*J(.9,1.3),.7*sc,0x58b33a,0x2d3a2c,-.15,.8,.15+1.3*i/c,ground,1,.4,role:NativeFxRole.Cloud);}
                    Shell(px,py,pz,ground,near,R,pal.Hot,pal.Cool,M(40),pal.Core,style);Debris(6,R*2,pal.Debris);Flash(cx,cy,cz,pal.Hot,R,7,.35,compat,q);break;
                }
            }
            return Solid.Spawned+Glow.Spawned-spawned;
        }
        /// <summary>
        /// A water globe landing on the floor (toon pass, owner 2026-10-11): a crown of toon water puffs thrown up and out, droplets
        /// up a cone falling back, a little mist. No ground ring. The match emits no event for a shot that hits the floor, so the
        /// presentation calls this where a water shot vanished at its floor (NativeWaterSplash). size scales it (1 for the water
        /// staff's globe), floor is the floor height at the landing point. Cosmetic only. Returns the particles spawned.
        /// </summary>
        public int Splash(double x,double floor,double z,double size,double quality)
        {
            double q=Clamp01(quality);if(q<=0||floor<NoFloor/2)return 0;int spawned=Solid.Spawned+Glow.Spawned;double s=Math.Max(.4,Math.Min(2.5,size)),v=Math.Sqrt(s);
            int N(double b)=>(int)Math.Max(1,Round(b*q));
            for(int i=0,c=N(9);i<c;i++){double a=(i+random()*.6)/c*Math.PI*2,ca=Math.Cos(a),sa=Math.Sin(a),out_=(1.1+random())*v;Solid.Spawn(x+ca*.14*s,floor+.06,z+sa*.14*s,ca*out_,(3.6+random()*1.8)*v,sa*out_,.4+random()*.2,.42*s*(.85+random()*.3),.2*s,WaterLit,WaterShade,9.8,1.5,0,floor,1,0,role:NativeFxRole.Water);}
            Glow.Spawn(x,floor+.2,z,0,.3,0,.14,.7*s,.7*s,0xffffff,0x8de9ee,0,0,0,floor,role:NativeFxRole.Flash);
            for(int i=0,c=N(16);i<c;i++){double a=random()*Math.PI*2,t=.6*Math.Sqrt(random()),sp=(2.5+random()*2)*v;Glow.Spawn(x,floor+.08,z,Math.Cos(a)*Math.Sin(t)*sp,Math.Cos(t)*sp,Math.Sin(a)*Math.Sin(t)*sp,.4+random()*.35,.045,.03,WaterLit,0x8de9ee,9.8,.3,0,floor+.02,1,role:NativeFxRole.Droplet);}
            Solid.Spawn(x,floor+.15,z,0,.3,0,.6,.7*s,.4*s,MistIce,MistShade,-.1,1,.04,floor,1,.3,role:NativeFxRole.Mist);
            Glow.Spawn(x,floor+.12,z,0,0,0,.12,.18*s,.06*s,0xffffff,0x8de9ee,0,0,0,floor,role:NativeFxRole.Mote);
            return Solid.Spawned+Glow.Spawned-spawned;
        }
        // The Geyser Wall eruption (Frost with the geyser look): a column of stretched toon water puffs and droplet streaks shot
        // up fast and braked hard (it tops out at about 3.5 m in half a second, then falls back), spray shed from its top falling
        // outward with gravity, a crown at its base, mist at the base and the top, a small cold glow.
        private void Geyser(double cx,double cz,double y0,double ground,bool near,double sc,double k,Palette pal)
        {
            int N(double b)=>(int)Math.Max(1,Round(b*k));double J(double a,double b)=>a+(b-a)*random();double w=Math.Max(.8,Math.Min(1.4,sc)),rest=near?NoFloor:ground;
            Glow.Spawn(cx,y0+.4,cz,0,0,0,.22,1.2*w,.4*w,pal.Core,pal.Hot,0,0,0,rest,role:NativeFxRole.Core);
            for(int i=0,c=N(24);i<c;i++){double a=random()*Math.PI*2,r=.16*w*Math.Sqrt(random());Solid.Spawn(cx+Math.Cos(a)*r,y0+.1,cz+Math.Sin(a)*r,Math.Cos(a)*.2,10+8*(i+random())/c,Math.Sin(a)*.2,J(1,1.35),J(.45,.66)*w,.25*w,WaterLit,WaterShade,9.8,3,.008*i,ground,1,0,role:NativeFxRole.Water);}
            for(int i=0,c=N(14);i<c;i++){double a=random()*Math.PI*2,s=J(.4,1.4);Glow.Spawn(cx,y0+.15,cz,Math.Cos(a)*s,J(14,19),Math.Sin(a)*s,J(1,1.4),.05,.03,WaterLit,pal.Hot,9.8,2.6,.01*i,ground+.02,1,role:NativeFxRole.Droplet);}
            for(int i=0,c=N(16);i<c;i++){double a=random()*Math.PI*2,s=J(1.5,3.5);Glow.Spawn(cx+Math.Cos(a)*.2,y0+J(2.4,3.4),cz+Math.Sin(a)*.2,Math.Cos(a)*s,J(1,3),Math.Sin(a)*s,J(.8,1.1),.05,.03,WaterLit,pal.Hot,9.8,.3,J(.25,.4),ground+.02,1,role:NativeFxRole.Droplet);}
            for(int i=0,c=N(10);i<c;i++){double a=random()*Math.PI*2,s=J(2,3.5);Solid.Spawn(cx+Math.Cos(a)*.2,y0+J(2.2,3.2),cz+Math.Sin(a)*.2,Math.Cos(a)*s,J(.5,2),Math.Sin(a)*s,J(.7,1),.45*w,.2*w,WaterLit,WaterShade,9.8,.5,J(.25,.4),ground,1,0,role:NativeFxRole.Water);}
            for(int i=0,c=N(8);i<c;i++){double a=(i+random()*.6)/c*Math.PI*2,s=J(2,3.5);Solid.Spawn(cx+Math.Cos(a)*.3*w,y0+.1,cz+Math.Sin(a)*.3*w,Math.Cos(a)*s,J(2,3.5),Math.Sin(a)*s,J(.4,.6),.55*w,.25*w,WaterLit,WaterShade,9.8,1.2,0,ground,1,0,role:NativeFxRole.Water);}
            for(int i=0,c=N(3);i<c;i++){double a=random()*Math.PI*2;Solid.Spawn(cx+Math.Cos(a)*.3,y0+.3,cz+Math.Sin(a)*.3,Math.Cos(a)*.5,.2,Math.Sin(a)*.5,J(1.1,1.5),1.6*w,.8*w,MistIce,MistShade,-.1,1,.05,ground,1,.3,role:NativeFxRole.Mist);}
            for(int i=0,c=N(2);i<c;i++){double a=random()*Math.PI*2;Solid.Spawn(cx+Math.Cos(a)*.3,y0+2.6,cz+Math.Sin(a)*.3,Math.Cos(a)*.4,.1,Math.Sin(a)*.4,.9,1.4*w,.7*w,MistIce,MistShade,.5,1,.35,ground,1,.3,role:NativeFxRole.Mist);}
        }
        // Toxic's acid blobs: glossy balls on ballistic arcs from the centre, each trailing small puffs (spawned late where the
        // arc will be: no sub-emitters) and bursting into a few puffs where it lands on the floor.
        private void Blobs(double cx,double cy,double cz,double ground,double R,double sc,int count)
        {
            const double G=9.8;
            for(int i=0;i<count;i++)
            {
                double a=(i+random()*.7)/count*Math.PI*2,e=.7+random()*.4,s=R*(1.1+random()*.4),vx=Math.Cos(a)*Math.Cos(e)*s,vy=Math.Sin(e)*s,vz=Math.Sin(a)*Math.Cos(e)*s;
                double h=cy-(ground+.1),land=ground<NoFloor/2?1.2:(vy+Math.Sqrt(vy*vy+2*G*Math.Max(0,h)))/G;land=Math.Min(1.2,land);
                Solid.Spawn(cx,cy,cz,vx,vy,vz,land+.02,.32*sc,.26*sc,ToxicLime,0x3f8f1c,G,0,0,ground,1,0,role:NativeFxRole.Blob);
                for(double t=.06;t<land-.04;t+=.07)Solid.Spawn(cx+vx*t,cy+vy*t-.5*G*t*t,cz+vz*t,vx*.08,.2,vz*.08,.32+random()*.15,.24*sc,.1*sc,0x3fae22,ToxicDark,-.2,2,t,ground,1,.8,role:NativeFxRole.Smoke);
                if(ground>NoFloor/2&&land<1.2)for(int j=0;j<3;j++){double b=random()*Math.PI*2;Solid.Spawn(cx+vx*land,ground+.1,cz+vz*land,Math.Cos(b)*1.2,.8+random(),Math.Sin(b)*1.2,.4+random()*.15,.32*sc,.15*sc,0x3fae22,ToxicDark,-.2,3,land,ground,1,.8,role:NativeFxRole.Smoke);}
            }
        }
        // Shock's strobing glow: four big glows in quick succession, bright, dim, bright, fading (the study's alpha strobe).
        private void Strobe(double cx,double cy,double cz,double sc,Palette pal,double rest)
        {
            Glow.Spawn(cx,cy,cz,0,0,0,.09,2*sc,1.6*sc,pal.Hot,pal.Cool,0,0,0,rest,role:NativeFxRole.Core);
            Glow.Spawn(cx,cy,cz,0,0,0,.07,1*sc,.8*sc,pal.Cool,pal.Cool,0,0,.07,rest,role:NativeFxRole.Core);
            Glow.Spawn(cx,cy,cz,0,0,0,.09,1.8*sc,1.4*sc,pal.Hot,pal.Cool,0,0,.15,rest,role:NativeFxRole.Core);
            Glow.Spawn(cx,cy,cz,0,0,0,.12,.8*sc,.35*sc,pal.Cool,pal.Cool,0,0,.24,rest,role:NativeFxRole.Core);
        }
        /// <summary>The longest any of Crackle's arcs lives (s): the study's 30-150 ms crackle.</summary>
        public const double CrackleLife=.15;
        // Shock's crackle: two waves of very short violet arcs (at 0 and .1 s) from near the centre out to half the radius. Only
        // for a look that is not blue or cyan (owner 2026-10-10).
        private void Crackle(double cx,double cy,double cz,double ground,double R,int first,int second,Palette pal)
        {
            for(int arc=0;arc<first+second;arc++)
            {
                double a=random()*Math.PI*2,len=R*(.3+random()*.3),y=Math.Max(ground+.15,cy+(random()-.5)*R*.3),delay=arc<first?random()*.04:.1+random()*.03,life=.04+random()*(CrackleLife-.04);int strand=Glow.NextSeed;
                for(int s=0;s<5;s++){double u=.15+s/4.0*(len/R);Glow.Spawn(cx+Math.Cos(a)*u*R+(random()-.5)*.18,y+random()*.09,cz+Math.Sin(a)*u*R+(random()-.5)*.18,0,0,0,life,.1,.05,pal.Hot,pal.Cool,0,0,delay,strand:strand,role:NativeFxRole.Bolt);}
            }
        }
        // The Shock lightning (chip-blasts.ts), drawn only where Lightning allows it: a jagged bolt from 9 m into the centre
        // with side sparks, then skittering arcs along the ground. ground: where they land, the floor or, for a blast in the
        // air, the blast's own height.
        private void SkyBolt(double cx,double cz,double ground,int steps,Palette pal)
        {
            double top=ground+9,x=cx,z=cz;int strand=Glow.NextSeed;
            for(int i=0;i<steps;i++)
            {
                double y=top-(top-ground)*((double)i/steps);x+=(random()-.5)*.45;z+=(random()-.5)*.45;if(i==steps-1){x=cx;z=cz;}
                Glow.Spawn(x,y,z,0,0,0,.22+(double)i/steps*.08,.22,.12,pal.Core,pal.Hot,0,0,i*.004,strand:strand,role:NativeFxRole.Bolt);
                if(i%4==2)Glow.Spawn(x,y,z,(random()-.5)*8,(random()-.5)*3,(random()-.5)*8,.2,.1,.04,pal.Core,pal.Cool,0,2,i*.004,strand:strand,role:NativeFxRole.Spark);
            }
        }
        private void GroundArcs(double cx,double cz,double ground,double R,int arcs,Palette pal)
        {for(int arc=0;arc<arcs;arc++){double a=random()*Math.PI*2,len=R*(.5+random()*.5);int strand=Glow.NextSeed;for(int s=1;s<=6;s++){double u=s/6.0;Glow.Spawn(cx+Math.Cos(a)*len*u+(random()-.5)*.3,ground+.15+random()*.5,cz+Math.Sin(a)*len*u+(random()-.5)*.3,0,0,0,.16,.12,.05,pal.Core,pal.Cool,0,0,.03+u*.04,strand:strand,role:NativeFxRole.Bolt);}}}
        private static double KindRate(string kind)
        {
            switch(kind){case "storm":return 9;case "acid":return 36;case "fire":return 64;case "frost":return 26;case "time":return 14;case "black-hole":return 50;case "repulsion":return 18;case "heal":return 20;case "implosion":return 40;case "bomb":return 10;case "mine":return 6;default:return 0;}
        }
        /// <summary>
        /// Ambient emitters for a live authoritative field: telegraph motes inside the gameplay radius, an arrival impact
        /// sized to it (source §19.2) and the §13.1 ability emitters. Rate-limited per field and by quality. The arrival and the
        /// Orbital Mark lock impacts take f.Floor (Impact: a dome on the floor, or a sphere when the field sits on a wall or in
        /// the air, its debris landing on that floor); without one they keep the source's floor. The emitters stay on the
        /// source's floor under the field.
        /// </summary>
        public void Field(NativeBlastField f,double time,double quality,bool compat=false)
        {
            string fx=f.Fx??"",kind=f.Kind??"";double q=Clamp01(quality)*(compat?.4:1);bool meteor=fx=="artillery"||fx=="meteor-strike",fused=kind=="bomb"&&(meteor||fx=="thunderstep"||fx=="comet"||fx=="orbital-mark"||fx=="geyser-wall");if(q<=0)return;
            double rate=fused?40:fx=="singularity"?70:KindRate(kind);if(rate==0)return;
            double R=f.Radius,ground=Math.Max(.03,f.Y-.95),ax=f.X,ay=ground+.95,az=f.Z,lockAt=f.LockAt??1e9;
            var style=kind=="bomb"||kind=="mine"?StyleOf(fx.Length>0?fx:kind):kind=="storm"?NativeBlastStyle.Shock:kind=="acid"||kind=="heal"?NativeBlastStyle.Toxic:kind=="frost"||kind=="time"?NativeBlastStyle.Frost:kind=="implosion"||fx=="singularity"?NativeBlastStyle.Implosion:kind=="black-hole"?NativeBlastStyle.Void:kind=="repulsion"?NativeBlastStyle.Mine:NativeBlastStyle.Fire;
            // Field emitters keep the source's colours (the Shock blasts' toon retint is not theirs: Field is drawn as the source draws it).
            var pal=Colors(style,TintOf(fx),true);
            if(fx=="orbital-mark"&&time>=lockAt&&!locked.Contains(f.Id)){locked.Add(f.Id);Impact(ax,ay,az,R,pal.Core,q,false,true,pal.Cool,f.Floor);Flash(ax,ay,az,pal.Hot,R,8,.3,compat,q);}
            bool known=fieldClock.TryGetValue(f.Id,out double last);
            if(!known||time<last)
            {
                fieldClock[f.Id]=time;
                if(!known&&!fused&&time-f.Born<.3){if(kind=="implosion")Inward(ax,ay,az,R,4+(int)Round(24*q),pal.Hot,pal.Core,.5,0,.3);else Impact(ax,ay,az,R,pal.Hot,q*.7,compat,true,pal.Cool,f.Floor);}
                return;
            }
            int due=(int)Math.Min(6,Math.Floor((time-last)*rate*q));if(due<=0)return;fieldClock[f.Id]=last+due/(rate*q);
            double left=f.Until-time,u=Clamp01((time-f.Born)/Math.Max(.01,f.Until-f.Born));
            void Spot(out double sx,out double sz,out double sr){double a=random()*Math.PI*2,r=Math.Sqrt(random())*R*.95;sx=f.X+Math.Cos(a)*r;sz=f.Z+Math.Sin(a)*r;sr=r;}
            for(int n=0;n<due;n++)
            {
                // Telegraph: motes rise from anywhere inside the area (owner request 2026-10-04: no motes tracing the outline).
                if(random()<8/rate){Spot(out double mx,out double mz,out _);Glow.Spawn(mx,ground+.06,mz,0,.9+random()*.6,0,.6,.1,.03,pal.Hot,pal.Cool,role:NativeFxRole.Mote);}
                if(fused)
                {
                    // Meteor Shower / Meteor Strike: the meteor is visible falling onto its warning circle for the last 0.45 s.
                    if(meteor){if(left>.45)continue;double w=Math.Max(0,left/.45),m=Math.Max(1,R/2.8),y=ground+.6+w*6*m,x=f.X-w*2.4*m,z=f.Z-w*1.1*m;Glow.Spawn(x,y,z,(random()-.5)*.6,1.2,(random()-.5)*.6,.28,.42*m,.08,0xfff2c4,0xff7a2a,role:NativeFxRole.Core);Solid.Spawn(x+(random()-.5)*.25*m,y+.2,z+(random()-.5)*.25*m,0,.4,0,.5,.2*m,.35*m,0x4a3a30,0x262020,-.2,1,role:NativeFxRole.Smoke);}
                    // Thunder Step: the target spot crackles before the bolt.
                    else if(fx=="thunderstep"){double a=random()*Math.PI*2,r=R*(.5+random()*.5);Glow.Spawn(f.X+Math.Cos(a)*r,ground+.08,f.Z+Math.Sin(a)*r,-Math.Cos(a)*r*2,2+random()*2,-Math.Sin(a)*r*2,.22,.08,.03,0xffffff,0x8fb8ff,role:NativeFxRole.Spark);}
                    // Orbital Mark: a targeting column from the sky (the shrinking outline ring was removed, owner request 2026-10-04).
                    else if(fx=="orbital-mark"){for(int i=0;i<3;i++)Glow.Spawn(f.X+(random()-.5)*.2,ground+random()*9,f.Z+(random()-.5)*.2,0,-7,0,.3,time>=lockAt?.15:.1,.05,pal.Core,pal.Cool,0,0,0,-1e9,6,role:NativeFxRole.Streak);}
                    // Geyser Wall: water fills the ring from the centre as the geyser arms, so the three erupt in order.
                    else if(fx=="geyser-wall"){double a=random()*Math.PI*2,r=R*u*Math.Sqrt(random());Glow.Spawn(f.X+Math.Cos(a)*r,ground+.05,f.Z+Math.Sin(a)*r,(random()-.5)*.4,1+u*3+random(),(random()-.5)*.4,.35+u*.25,.12,.05,pal.Core,pal.Hot,3,role:NativeFxRole.Mote);}
                    // Comet Dash: the rolling pilot trails fire until the landing blast.
                    else Solid.Spawn(f.X+(random()-.5)*.4,f.Y-.3+random()*.6,f.Z+(random()-.5)*.4,0,.8,0,.45,.3,.05,0xffc04a,0xd8401c,-.4,.6,0,-1e9,1,4,role:NativeFxRole.Flame);
                    continue;
                }
                // Storm In A Jar: storm clouds drifting over the area. The source's lightning strikes (a white-to-blue bolt from
                // 3.6 m, its ground flash and sparks) are not drawn (owner 2026-10-10: no blue/cyan lightning, see Lightning).
                if(kind=="storm")
                {
                    for(int i=0;i<2;i++){Spot(out double cxs,out double czs,out _);Solid.Spawn(cxs,ground+3.5+random()*.5,czs,(random()-.5)*.3,0,(random()-.5)*.3,1.4,.45,.75,0x4a5378,0x2a3048,0,.5,role:NativeFxRole.Cloud);}
                }
                else if(kind=="acid"){Spot(out double sx,out double sz,out _);Glow.Spawn(sx,ground+.05,sz,0,.9+random()*.8,0,.7+random()*.4,.14,.26,pal.Hot,pal.Cool,0,0,role:NativeFxRole.Bubble);if(random()<.45)Solid.Spawn(sx,ground+.15,sz,(random()-.5)*.3,.45,(random()-.5)*.3,1.5,.18,.55,0x6f9a3a,0x2c3a22,-.1,1,role:NativeFxRole.Cloud);}
                else if(kind=="fire"){Spot(out double sx,out double sz,out double sr);double hot=1-sr/R;Solid.Spawn(sx,ground+.05,sz,(random()-.5)*.4,1.4+hot*2+random()*1.2,(random()-.5)*.4,.55+hot*.4+random()*.3,.3+hot*.15,.04,random()<.3?0xffe28a:pal.Hot,pal.Cool,-.4,.5,0,-1e9,1,3,role:NativeFxRole.Flame);if(random()<.4)Glow.Spawn(sx,ground+.2,sz,random()-.5,2.5+random()*2,random()-.5,.6,.05,.02,pal.Core,pal.Hot,role:NativeFxRole.Ember);}
                else if(kind=="frost"){Spot(out double sx,out double sz,out _);Solid.Spawn(sx,ground+.02,sz,0,.35,0,1.6,.08,.22,0xe8fbff,0x8fd8ff,0,.8,0,-1e9,3.5,0,role:NativeFxRole.Shard);Glow.Spawn(f.X+(random()-.5)*R*1.6,ground+1.6+random(),f.Z+(random()-.5)*R*1.6,(random()-.5)*.3,-.9,(random()-.5)*.3,1.4,.05,.03,0xffffff,0xbfeaff,role:NativeFxRole.Snow);}
                else if(kind=="time"){double a=random()*Math.PI*2,y=random();Glow.Spawn(f.X+Math.Cos(a)*R*Math.Sqrt(1-y*y),ground+y*R*.8,f.Z+Math.Sin(a)*R*Math.Sqrt(1-y*y),0,0,0,.9,.1,.04,0xdbe8ff,0x83aeff,role:NativeFxRole.Mote);}
                else if(kind=="black-hole"){double a=random()*Math.PI*2,r=R*(.7+random()*.3),t0=.6,x=Math.Cos(a)*r,z=Math.Sin(a)*r;Glow.Spawn(f.X+x,ground+.7+(random()-.5),f.Z+z,-x/t0-z*.8,(random()-.5)*.4,-z/t0+x*.8,t0,.12,.04,pal.Hot,pal.Core,0,0,0,-1e9,2.5,role:NativeFxRole.Streak);}
                // Reverse Grenade: motes stream inward from the radius onto the spinning gravity bomb during the mark.
                else if(kind=="implosion"){double a=random()*Math.PI*2,x=Math.Cos(a)*R,z=Math.Sin(a)*R,t0=.5;Glow.Spawn(f.X+x,ground+.15+random()*.6,f.Z+z,-x/t0,0,-z/t0,t0,.13,.05,pal.Hot,pal.Core,0,0,0,-1e9,2.5,role:NativeFxRole.Streak);}
                // The fuse sparks leave the modelled bomb's fuse tip (owner cut 2026-10-11: the source's ground + .85 sat 15 cm above
                // it once the bomb stands on its floor; NativeFieldRules.BombTipY). Same draws, only the start height moved.
                else if(kind=="bomb")Glow.Spawn(f.X,NativeFieldRules.BombTipY(f),f.Z,(random()-.5)*1.4,1.4+random(),(random()-.5)*1.4,.3,.06,.02,0xffffff,pal.Hot,6,role:NativeFxRole.Spark);
                else if(kind=="repulsion"){double a=random()*Math.PI*2;Glow.Spawn(f.X+Math.Cos(a)*.4,ground+.3+random()*.8,f.Z+Math.Sin(a)*.4,Math.Cos(a)*R/.5,0,Math.Sin(a)*R/.5,.45,.12,.04,0xffd0da,0xff728f,0,.9,0,-1e9,1.2,role:NativeFxRole.Streak);}
                else if(kind=="heal"){Spot(out double sx,out double sz,out _);Glow.Spawn(sx,ground+.1,sz,0,1.1,0,.9,.12,.05,0xd6ffe4,0x79ffa9,role:NativeFxRole.Mote);}
            }
        }
        /// <summary>Forget field clocks for fields that no longer exist. live is read only.</summary>
        public void Prune(HashSet<long> live)
        {
            stale.Clear();foreach(var id in fieldClock.Keys)if(!live.Contains(id))stale.Add(id);foreach(var id in stale)fieldClock.Remove(id);
            stale.Clear();foreach(var id in locked)if(!live.Contains(id))stale.Add(id);foreach(var id in stale)locked.Remove(id);
        }
        public void Update(double dt)
        {
            dt=Math.Max(0,Math.Min(.1,dt));Time+=dt;Solid.Update(dt);Glow.Update(dt);
            foreach(var f in Flashes){double u=(Time-f.At)/f.Life;f.Intensity=u>=0&&u<1?f.Peak*(1-u)*(1-u):0;}
            // Shock fronts expire like voxels (age >= life), keeping their order; no allocation.
            int keep=0;for(int i=0;i<shocks.Count;i++){var s=shocks[i];if(Time-s.Born>=s.Life)continue;if(keep!=i)shocks[keep]=s;keep++;}
            if(keep<shocks.Count)shocks.RemoveRange(keep,shocks.Count-keep);
        }
        public void Clear(){Solid.Clear();Glow.Clear();fieldClock.Clear();locked.Clear();shocks.Clear();nextShock=0;foreach(var f in Flashes){f.At=-1e9;f.Intensity=0;}}
        /// <summary>Clear() plus a rewound clock and generator: replaying the same spawns afterwards gives identical voxels.</summary>
        public void Reset(Func<double> random=null){Clear();Time=0;nextLight=0;this.random=random??new NativeBlastRandom().Next;}

        // Unit cube topology for renderers: 8 corners (signs) and 12 triangles wound clockwise seen from outside
        // (Unity front faces), so a back-face-culled draw shows each voxel's three near faces exactly once.
        public static readonly sbyte[] CubeCorners={-1,-1,-1, 1,-1,-1, 1,1,-1, -1,1,-1, -1,-1,1, 1,-1,1, 1,1,1, -1,1,1};
        public static readonly int[] CubeTriangles={0,3,2,0,2,1, 4,5,6,4,6,7, 0,4,7,0,7,3, 1,2,6,1,6,5, 3,7,6,3,6,2, 0,1,5,0,5,4};
    }

    /// <summary>
    /// How Unity's existing effect records map onto the source's blast calls (event-vfx.ts blastEvent and event) until the
    /// records carry the source's event type and message (A21). An ability/chip effect keeps only its position, radius, the
    /// owner's magic school (0 none, 1 fire, 2 water, 3 lightning, 4 darkness) and a beam origin. Its source look comes from
    /// the authoritative Explosion event the match emits with it (same spot, same tick: ExplosionTag), or else from a
    /// replicated field, zone or ability shot that ends exactly there (the caller's job). The school never picks the look.
    /// </summary>
    public static class NativeBlastCues
    {
        public enum Cue{None,Blast,Teleport,Vacuum,Dud}
        public const double TeleportRadius=1.2,TeleportQuality=.5,TeleportImpactRadius=.9,TeleportLineJitter=.3,TeleportLineLife=.22,TeleportLineSweep=.08;
        public const int TeleportColor=0xb66cff;
        /// <summary>event-vfx.ts blastEvent: an explosion without a message (a bare weapon 'blast') draws fire at .55 quality.</summary>
        public const double UntaggedQuality=.55;
        /// <summary>event-vfx.ts blastEvent: the static discharge is an `arc` with a radius, a shock blast at .6 quality.</summary>
        public const double ArcQuality=.6;
        /// <summary>
        /// explosion: the match emitted an Explosion or Vacuum event at this record's spot in its tick (ChipBlast, AbilityBlast,
        /// Vacuum). tag: the source message recovered for it (ExplosionTag, a teleport landing, or a field, zone or ability shot).
        /// </summary>
        public static Cue Classify(bool beam,double radius,int element,string tag,bool siren=false,bool explosion=false)
        {
            // The Siren's own attacks (NativeBlastFrame.AtSiren). The source sends them as siren-* events that client/siren/fx.ts
            // draws as black fire, shock rings and cracks, never as chip blasts; NativeDevEffects draws those from the events, so
            // the record itself draws nothing (the first port's two flat darkness halos are gone).
            if(siren&&!beam)return Cue.None;
            // Blink and the wormhole blink record a radius-1 lightning beam; every other beam is an arc and draws no blast.
            if(beam)return radius>=.99&&element==3?Cue.Teleport:Cue.None;
            // Vacuum() always records (position, 5, darkness).
            if(element==4&&Math.Abs(radius-5)<1e-3)return Cue.Vacuum;
            // An unarmed rocket: the source's dud `impact` (gun-abilities.ts:23), a grey spark burst, not a blast.
            if(tag=="rocket"&&radius<.5)return Cue.Dud;
            // Owner playtest 2026-10-10 ("explosion effects seem to play randomly", Siren adds killed by guns): a record draws a
            // blast only where the source draws one, an authoritative explosion at its spot or an ability's own visual hit blast
            // (flame-lance, tidal-wave, shackles: a tag from its shot). Anything else is a first-port stand-in record (the old
            // gib, summon, revive and pong bounce records drew the owner's school, a fireball for school 0 or 1) and draws none.
            return explosion||tag!=null?Cue.Blast:Cue.None;
        }
        /// <summary>The blast recorded where a teleport beam lands: Blink Strike's (always lightning) or the wormhole chip's.</summary>
        public static string Colocated(int element)=>element==3?"blink-strike":"wormhole";
        /// <summary>chip-blasts.ts blastStyle for a source message; an explosion without one is fire (event-vfx.ts blastEvent).</summary>
        public static NativeBlastStyle Style(string tag)=>tag!=null?NativeBlastSim.StyleOf(tag):NativeBlastStyle.Fire;
        public static double Quality(string tag)=>tag=="flame-lance"?.5:1;
        /// <summary>
        /// The static discharge. Unity's untagged ChipBlast ("chip") is either the discharge on a summon or PvE add
        /// (NativeSummons.MinionWeaponHit, 3 m) or Corpse Party (3.5 m); the source emits the first as `arc`, the second as
        /// `corpseParty` (simulation.ts:264/275, chaos.ts:111).
        /// </summary>
        public static bool Static(string detail,double radius)=>detail=="static"||detail=="chip"&&Math.Abs(radius-3)<1e-3;
        /// <summary>
        /// The source message (tag) and quality of the blast for a Unity Explosion event (the ChipBlast/AbilityBlast effect in
        /// Detail), as simulation.ts explode() emits it and event-vfx.ts blastEvent draws it: kind 'static' is an `arc` (shock,
        /// .6 quality), 'blockPulse' its pulse (nova with Supernova, otherwise kinetic), a bare weapon 'blast' an explosion with
        /// no message (fire, .55), and any other kind an explosion whose message is its fx tag or kind (.5 for flame-lance).
        /// weapon: Detail names a weapon spec (the fire weapon's blast, NativeProjectiles). Unity's own names map to the source
        /// kinds (dash-blast: dashBlast, leet-block: root).
        /// </summary>
        public static string ExplosionTag(string detail,double radius,bool weapon,out double quality)
        {
            quality=1;
            if(string.IsNullOrEmpty(detail)||weapon){quality=UntaggedQuality;return null;}
            if(Static(detail,radius)){quality=ArcQuality;return "static";}
            switch(detail)
            {
                case "chip":return "corpseParty";
                case "block-pulse":return "kinetic";
                case "supernova":return "nova";
                case "dash-blast":return "dashBlast";
                case "leet-block":return "root";
                default:quality=Quality(detail);return detail;
            }
        }
    }

    /// <summary>
    /// Where a water globe that vanished came down (toon pass, owner 2026-10-11: a splash when water-magic globes hit the floor).
    /// The match emits no event when a shot hits the floor (only a prop hit is an Impact), so the presentation watches the
    /// replicated shot list instead: a water shot that is gone this frame is tested from the last state it was seen in. It
    /// landed if it was already touching its floor (Touch), or if its ballistic path from there (the weapon's gravity) reaches
    /// the floor within Lookahead seconds, which covers a guest's 20 Hz snapshot gap and some jitter. A shot that ended on a pilot,
    /// a minion or a prop leaves a hit or impact event near its path (NearPath) and draws no splash. Cosmetic only, and a pure
    /// function of what host and guests both hold (the shot list and the raycast floor), so the same disappearance gives the
    /// same splash; a guest's splash can sit up to one snapshot of flight from the host's.
    /// </summary>
    public static class NativeWaterSplash
    {
        /// <summary>Seconds of flight extrapolated past the last seen state, and how far above its floor (m, below the shot's
        /// bottom) a shot already counts as landed; a shot more than Below under its floor is not landing on it.</summary>
        public const double Lookahead=.15,Touch=.3,Below=.25;
        /// <summary>A hit, impact or block event within this distance (m) of the path means the shot hit something else.</summary>
        public const double Clear=1.2;
        /// <summary>The water staff's globe radius (GameContent shotRadius), the splash's size 1.</summary>
        public const double GlobeRadius=.16;
        /// <summary>
        /// Whether a shot last seen at p with velocity v (m/s), falling at gravity (m/s², down) with this radius, came down on the
        /// floor height floor: the landing point x, z and the seconds t after the last state. False without a floor, or when the
        /// floor is not reached within lookahead.
        /// </summary>
        public static bool Landing(double px,double py,double pz,double vx,double vy,double vz,double gravity,double radius,double floor,out double x,out double z,out double t,double lookahead=Lookahead)
        {
            x=px;z=pz;t=0;if(floor<NativeBlastSim.NoFloor/2||double.IsNaN(py)||double.IsNaN(floor))return false;
            double h=py-radius-floor;if(h<-Below)return false;if(h<=Touch)return true;
            // h + vy t - g t^2 / 2 = 0, the first t > 0.
            if(gravity>1e-6)t=(vy+Math.Sqrt(vy*vy+2*gravity*h))/gravity;else if(vy<-1e-6)t=-h/vy;else return false;
            if(!(t>=0&&t<=lookahead))return false;x=px+vx*t;z=pz+vz*t;return true;
        }
        /// <summary>The splash size for a shot radius (1 for the water staff's globe), .7 to 1.6.</summary>
        public static double Size(double radius)=>Math.Max(.7,Math.Min(1.6,radius/GlobeRadius));
        /// <summary>Whether point e lies within Clear of the segment from a to b (the shot's last stretch of path).</summary>
        public static bool NearPath(double ax,double ay,double az,double bx,double by,double bz,double ex,double ey,double ez)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,l=dx*dx+dy*dy+dz*dz,u=l>1e-12?((ex-ax)*dx+(ey-ay)*dy+(ez-az)*dz)/l:0;u=Math.Max(0,Math.Min(1,u));
            double qx=ax+dx*u-ex,qy=ay+dy*u-ey,qz=az+dz*u-ez;return qx*qx+qy*qy+qz*qz<=Clear*Clear;
        }
    }

    /// <summary>
    /// Per-frame rules the blast presentation applies to Unity's effect records before they reach the sim: how far the
    /// blasts advance, duplicate records of one source event, and which records belong to the Siren.
    /// </summary>
    public static class NativeBlastFrame
    {
        /// <summary>Records closer than this (metres) are at the same spot: the match writes both from one Vector3.</summary>
        public const double SameSpot=.05;
        /// <summary>The source main loop caps a frame at 0.05 s before the renderer steps the blasts with it (main.ts frame()).</summary>
        public const double MaxFrame=.05;
        /// <summary>
        /// Seconds the blasts advance this frame. The source steps ChipBlasts with the render frame time, not the match clock;
        /// a guest's match clock only moves when a 20 Hz snapshot arrives. They stop only while the match itself is paused
        /// (the offline menu). An online match keeps running behind the menu, and so do its blasts.
        /// </summary>
        public static double Step(double frameSeconds,bool paused)=>paused||double.IsNaN(frameSeconds)?0:Math.Max(0,Math.Min(MaxFrame,frameSeconds));
        /// <summary>A record first seen this frame, with the cue, source tag and quality factor the presentation resolved for it.</summary>
        public struct Candidate{public double X,Y,Z,Radius,Quality;public string Tag;public NativeBlastCues.Cue Cue;}
        private static bool Same(Candidate a,Candidate b){double x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return x*x+y*y+z*z<SameSpot*SameSpot;}
        private static bool Explosive(NativeBlastCues.Cue cue)=>cue==NativeBlastCues.Cue.Blast||cue==NativeBlastCues.Cue.Dud;
        /// <summary>
        /// The match records some single source events more than once in a tick, and the source draws one cue for each:
        /// Blink Strike's teleport (ChipBlink, then Blink), a ball-lightning wall hit (its blast, then a 0.6 m hit) and an
        /// armed rocket on a summon (its blast, then the dud's 0.3 m hit). Sets Cue.None on a teleport that lands where an
        /// earlier one landed, and on a blast or dud at the spot of a larger one with the same source tag (the earlier one
        /// wins a tie). Untagged records are kept, because distinct chip blasts can share a spot.
        /// </summary>
        public static void Collapse(List<Candidate> fresh)
        {
            for(int i=0;i<fresh.Count;i++)
            {
                var a=fresh[i];
                if(a.Cue==NativeBlastCues.Cue.Teleport){for(int j=0;j<i;j++)if(fresh[j].Cue==NativeBlastCues.Cue.Teleport&&Same(a,fresh[j])){a.Cue=NativeBlastCues.Cue.None;break;}}
                else if(Explosive(a.Cue)&&a.Tag!=null)for(int j=0;j<fresh.Count;j++)
                {
                    var b=fresh[j];if(j==i||!Explosive(b.Cue)||b.Tag!=a.Tag||!Same(a,b))continue;
                    if(b.Radius>a.Radius||b.Radius==a.Radius&&j<i){a.Cue=NativeBlastCues.Cue.None;break;}
                }
                fresh[i]=a;
            }
        }
        /// <summary>
        /// Where the Siren's own attacks land: under a Siren pilot (slam), on its shots (seekers, rain drops, damnation), on
        /// its fields (eruption) and on the Siren stage's vents. A Below site matches on the ground plane under its centre.
        /// </summary>
        public struct SirenSite{public double X,Y,Z,Reach;public bool Below;}
        /// <summary>How far under a Below site's centre a record must lie (the slam lands on the ground under the body).</summary>
        public const double BelowDepth=.3;
        public static bool AtSiren(List<SirenSite> sites,double x,double y,double z)
        {
            for(int i=0;i<sites.Count;i++)
            {
                var s=sites[i];double dx=x-s.X,dy=y-s.Y,dz=z-s.Z,r=s.Reach*s.Reach;
                if(s.Below?dx*dx+dz*dz<=r&&dy<=-BelowDepth:dx*dx+dy*dy+dz*dz<=r)return true;
            }
            return false;
        }
    }
}

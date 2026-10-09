using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Weapon presentation from client/weapon-visuals.ts (weaponModifiers chip modules, enchantWeapon and the muzzle flash) and
    // client/melee-trails.ts at 05950ea, in the source weapon frame (+x the rig's left, +y up the sights, +z along the blade or
    // barrel). The Unity boundary mirrors x once (CapturedRig.Mirror), as tools/export_models.mjs does for the weapon meshes.
    // Presentation only: the source states that weapon meshes never define reach or hit volumes (weapon-visuals.ts:20), and
    // nothing here is read by the authority, the network frame or the melee rules.
    public enum WeaponShape : byte { Cube, Sphere, Cone, Ring }
    public struct WeaponPiece
    {
        public WeaponShape Shape;public bool Glow;public int Color;public Vector3 Position,Scale;public Quaternion Rotation;
        // Per-frame animation (weapon-visuals.ts:110,134): an orbiting orb index, the rate coil, or an enchant element.
        public int Orbit;public bool Coil;public byte Enchant;public float BaseY,Phase;
    }
    public static class NativeWeaponDressing
    {
        public const int MaxPieces=64,FireEnchant=1,PoisonEnchant=2;
        public const float FlashLife=.075f,FirstPersonModuleScale=.5f;
        // weapon-visuals.ts:21-22: the visual blade lengths that the reach strip and the enchant tracks share.
        public static float BladeLength(string spec,bool shield=true)=>spec=="daggers"?.5f:spec=="greatsword"?1.35f:spec=="shield"&&shield?.7f:.94f;
        // The 'muzzle' socket z (weapon-visuals.ts:59,68; matrix-effects.ts:10), or .8 without one (weapon-visuals.ts:89).
        public static float MuzzleZ(string spec)=>spec=="pistol"?.51f:spec=="rifle"?1.22f:spec=="machinegun"?.91f:Staff(spec)?.88f:spec=="leet"?1.52f:.8f;
        public static bool Staff(string spec)=>spec=="fire"||spec=="water"||spec=="lightning"||spec=="darkness";
        public static bool Gun(string spec)=>spec=="pistol"||spec=="rifle"||spec=="machinegun";
        // WEAPONS class 'melee' (shared/content.ts): the sword family, the fighters and the weaponless siren.
        public static bool Melee(string spec)=>spec=="sword"||spec=="daggers"||spec=="greatsword"||spec=="shield"||spec=="emperor"||spec=="ansatsuken"||spec=="siren";
        // Chip modules and blade enchants are skipped for the fighters' fists (weapon-visuals.ts:88,128).
        public static bool Fist(string spec)=>spec=="emperor"||spec=="ansatsuken";
        // character-animation.ts:57: a melee-class weapon (and its off-hand copy) is scaled along +z by reach / range, i.e.
        // stats.reach, before any layer runs. The fighters' own layers reset their fists to scale 1 (emperor-animation.ts:45,
        // ansatsuken-animation.ts:44), so a fist keeps the stretch only where sampleCharacter returns before them (stunned,
        // knocked down or rolling: `fighterLayersSkipped`). The siren returns before sampleCharacter and is never scaled.
        public static float ReachScale(string spec,float reach,bool fighterLayersSkipped)
            =>!Melee(spec)||spec=="siren"||Fist(spec)&&!fighterLayersSkipped||!(reach>0)||float.IsInfinity(reach)?1:reach;
        // The 'blade-tip' sockets the sweep samples (weapon-visuals.ts:28,29,73,82).
        public static bool BladeTip(string spec,out Vector3 tip)
        {
            tip=spec=="sword"?new Vector3(0,0,1.28f):spec=="daggers"?new Vector3(0,0,.84f):spec=="greatsword"?new Vector3(0,0,1.69f):spec=="shield"?new Vector3(0,.4f,.29f):
                spec=="emperor"?new Vector3(0,-.045f,.14f):spec=="ansatsuken"?new Vector3(0,-.045f,.115f):Vector3.Zero;
            return tip!=Vector3.Zero;
        }
        private static int Count(IReadOnlyDictionary<string,int> chips,string id)=>chips!=null&&chips.TryGetValue(id,out int n)&&n>0?n:0;
        private static bool Add(WeaponPiece[] o,ref int n,WeaponShape shape,bool glow,int color,float x,float y,float z,float sx,float sy,float sz,float rx=0,float ry=0,float rz=0,int orbit=-1,bool coil=false)
        {
            if(n>=o.Length)return false;
            o[n++]=new WeaponPiece{Shape=shape,Glow=glow,Color=color,Position=new Vector3(x,y,z),Scale=new Vector3(sx,sy,sz),Rotation=CapturedRig.Euler(rx,ry,rz),Orbit=orbit,Coil=coil};return true;
        }
        // weaponModifiers' 'chip-modules' group (weapon-visuals.ts:89-106) in build order. `ranged` is WEAPONS[spec].class, and
        // the orbs take the weapon colour. Conditions read the chip counts for ricochet, velocity, magazine, the rate ids and
        // demolition, and the stats for the rest, exactly as the source does. Returns the piece count (no allocation).
        public static int Modules(string spec,bool ranged,int weaponColor,IReadOnlyDictionary<string,int> chips,ChipStats stats,WeaponPiece[] o)
        {
            int n=0;if(Fist(spec)||stats==null)return 0;float tip=MuzzleZ(spec);
            double extra=Math.Min(6,stats["extraProjectiles"]);
            for(int i=0;i<extra;i++)
            {
                float x=(i%2!=0?1:-1)*(.18f+i/2*.14f);
                if(ranged){Add(o,ref n,WeaponShape.Cube,false,0x647e89,x,0,tip-.23f,.11f,.13f,.48f);Add(o,ref n,WeaponShape.Cube,true,0xffd88d,x,0,tip+.015f,.12f,.14f,.075f);}
                else Add(o,ref n,WeaponShape.Sphere,true,weaponColor,x,.08f,tip-.06f,.085f,.085f,.085f,orbit:i);
            }
            for(int i=0;i<Count(chips,"ricochet");i++)Add(o,ref n,WeaponShape.Ring,true,0xffcc49,0,0,tip-.08f-i*.11f,.17f,.17f,.17f);
            for(int i=0;i<Count(chips,"velocity");i++)Add(o,ref n,WeaponShape.Ring,true,0x79eaff,0,0,tip-.35f-i*.09f,.13f,.13f,.13f);
            if(stats["homing"]!=0)for(int side=-1;side<=1;side+=2)Add(o,ref n,WeaponShape.Cone,true,0xf482d4,side*.19f,0,tip-.15f,.10f,.25f,.06f,rz:side*-.9f);
            if(stats["split"]!=0)for(int side=-1;side<=1;side+=2)Add(o,ref n,WeaponShape.Cube,true,0x96ffb7,side*.16f,-.13f,tip-.05f,.06f,.06f,.30f,ry:side*.3f);
            if(stats["pierces"]!=0)Add(o,ref n,WeaponShape.Cube,true,0xffffff,0,.12f,tip+.1f,.035f,.035f,.35f);
            int magazine=Count(chips,"magazine");if(magazine>0)Add(o,ref n,WeaponShape.Cube,false,0xe3b779,0,-.30f,.1f,.15f+Math.Min(magazine,8)*.025f,.26f,.19f);
            if(stats["pong"]!=0)for(int i=0;i<3;i++)Add(o,ref n,WeaponShape.Ring,true,0xff5269,0,0,tip-.10f+i*.09f,.27f,.27f,.27f,ry:i*(float)Math.PI/3);
            if(stats["boomerang"]!=0)for(int side=-1;side<=1;side+=2)Add(o,ref n,WeaponShape.Cube,true,0x7fffe0,side*.2f,0,tip-.1f,.08f,.04f,.4f,ry:side*.6f);
            if(stats["vacuum"]!=0)Add(o,ref n,WeaponShape.Ring,true,0xbc72ff,0,0,tip,.3f,.3f,.3f,rx:(float)Math.PI/2);
            int rate=Count(chips,"fire-rate");if(rate==0)rate=Count(chips,"cast-rate");if(rate==0)rate=Count(chips,"swing-rate");rate+=Count(chips,"overclock");
            if(rate>0)Add(o,ref n,WeaponShape.Ring,true,0xffae64,0,0,.12f,.21f,.21f,.21f,coil:true);
            if(stats["impulse"]>1.3)for(int i=0;i<2;i++)Add(o,ref n,WeaponShape.Cube,true,0xffad68,(i!=0?1:-1)*.12f,.02f,.35f,.085f,.085f,.45f);
            if(Count(chips,"demolition")>0)for(int i=0;i<3;i++)Add(o,ref n,WeaponShape.Cube,true,0xffd45f,(i-1)*.09f,.12f,.35f,.045f,.04f,.20f);
            if(stats["shrapnel"]!=0)Add(o,ref n,WeaponShape.Sphere,true,0xff855c,0,-.12f,.35f,.13f,.13f,.13f);
            if(stats["staticEvery"]!=0)for(int i=0;i<3;i++)Add(o,ref n,WeaponShape.Cube,true,0xc4a0ff,(i-1)*.08f,-.12f,.08f,.04f,.12f,.04f);
            if(stats["reach"]>1){float length=BladeLength(spec,false);Add(o,ref n,WeaponShape.Cube,true,0x92f5e5,.12f,0,.26f+length/2,.025f,.06f,length);}
            if(stats["extraStrikes"]!=0)for(int side=-1;side<=1;side+=2)Add(o,ref n,WeaponShape.Cube,true,0xc1a1ff,side*.16f,0,.6f,.025f,.04f,.6f);
            return n;
        }
        // enchantWeapon (weapon-visuals.ts:126-133): flame cones along both blade edges (both shield rims) for fire, bobbing motes
        // for poison, 10+min(6, ember or poison chips, at least 1) of each; fire first. Appends at `start`, returns the new count.
        public static int Enchant(string spec,bool fire,bool poison,int ember,int poisonStacks,WeaponPiece[] o,int start)
        {
            int n=start;if(Fist(spec))return n;float length=BladeLength(spec);bool shield=spec=="shield";
            for(int e=0;e<2;e++)
            {
                bool flame=e==0;if(flame?!fire:!poison)continue;int count=10+Math.Min(6,Math.Max(1,flame?ember:poisonStacks));
                for(int i=0;i<count&&n<o.Length;i++)
                {
                    float z=shield?.34f:.28f+(float)i/(count-1)*length,y=shield?-.30f+(float)i/(count-1)*.7f:0,side=i%2!=0?1:-1;
                    o[n++]=new WeaponPiece{Shape=flame?WeaponShape.Cone:WeaponShape.Sphere,Glow=true,Color=flame?(i%3!=0?0xff6c27:0xffd06a):0x88f37f,Position=new Vector3(side*(shield?.27f:.10f),y,z),
                        Scale=flame?new Vector3(.065f,.24f,.08f):new Vector3(.045f),Rotation=Quaternion.Identity,Orbit=-1,Enchant=(byte)(flame?FireEnchant:PoisonEnchant),BaseY=y,Phase=i*.81f};
                }
            }
            return n;
        }
        // The per-frame motion (weapon-visuals.ts:110,134): orbs bob, the coil spins about the barrel, flames flicker and sway,
        // motes bob and swell. Everything else is static.
        public static void Animate(ref WeaponPiece p,float time)
        {
            if(p.Orbit>=0)p.Position.Y=(float)Math.Sin((double)time*2+p.Orbit*2.1)*.15f;
            if(p.Coil)p.Rotation=CapturedRig.Euler(0,0,time*8);
            if(p.Enchant!=0)
            {
                bool fire=p.Enchant==FireEnchant;double phase=time*(fire?7:1.2)+p.Phase;float k=(float)(Math.Sin(phase)+1)/2;
                p.Position.Y=p.BaseY+(fire?k*.06f:k*.20f);
                if(fire){p.Scale.Y=.16f+k*.25f;p.Rotation=CapturedRig.Euler(0,0,(float)Math.Sin(phase)*.3f);}else p.Scale=new Vector3(.025f+k*.045f);
            }
        }
        // Deterministic 0..1 hash of an authoritative timestamp (weapon-visuals.ts:24), in double as the source computes it.
        public static double Hash(double t){double x=Math.Sin(t*127.1+311.7)*43758.5453;return x-Math.Floor(x);}
        // Two flash frames per shot (weapon-visuals.ts:59,109): a glow cone .14 in front of the muzzle socket, rolled and sized
        // from the hash of the shot time. Guns only (only their builds carry 'muzzle-flash').
        public static bool MuzzleFlash(string spec,double lastShot,double time,out WeaponPiece flash)
        {
            flash=default;double age=time-lastShot;if(!Gun(spec)||!(age>=0&&age<FlashLife))return false;
            float h=(float)Hash(lastShot);bool late=age>=FlashLife/2;float size=(spec=="rifle"?.16f:.10f)*(late?.72f+.2f*h:1+.12f*h);
            flash=new WeaponPiece{Shape=WeaponShape.Cone,Glow=true,Color=0xffdb90,Position=new Vector3(0,0,MuzzleZ(spec)+.14f),Scale=new Vector3(size,late?.24f:.30f+.06f*h,size),
                Rotation=CapturedRig.Euler((float)Math.PI/2,h*(float)Math.PI*2+(late?.9f:0),0),Orbit=-1};
            return true;
        }
        // The swept ribbon's colour from the chip ids (melee-trails.ts:45): ember, then poison, then echo strike.
        public static int SweepColor(IReadOnlyDictionary<string,int> chips)=>Count(chips,"ember")>0?0xff953e:Count(chips,"poison")>0?0x88ed80:Count(chips,"echo-strike")>0?0xc1a1ff:0xc9fff3;
        // melee-trails.ts:20-22: fade life per weapon (x.7 below full effects) and where the inner edge sits along the blade.
        public static float SweepLife(string spec,float quality)=>(spec=="daggers"?.12f:spec=="greatsword"?.22f:.16f)*(quality<1?.7f:1);
        public static float SweepBase(string spec)=>spec=="daggers"?.4f:spec=="sword"?.32f:spec=="greatsword"?.25f:.35f;
        // MeleeTrails.sweeping over attackPose (melee-trails.ts:41-43; character-animation.ts:28-32) for the replicated actor:
        // from 88% of the windup until 70 ms after the hit window, and through a released sword launch or wolf spin.
        // AttackAt is the source's lastAttack; SwingAt/SwingUntil are its swing window while it lasts (else -100, the source's
        // fallback to windup and .16/rate); CastRate is attackRate. Unity cancels a strike by zeroing AttackUntil (block, roll,
        // stun), where the source resets lastAttack, so a cancelled strike stops sweeping as in the source.
        public static bool Sweeping(Actor a,float time)
        {
            if(a==null||!a.Alive||!Melee(a.SourceSpec))return false;
            if(a.SpecialReleased&&(a.SpecialKind=="sword-launch"||a.SpecialKind=="wolf-spin")&&time<a.SpecialUntil)return true;
            float rate=a.CastRate>0?a.CastRate:1,age=Math.Max(0,time-a.AttackAt);bool swing=a.SwingAt>-90;
            float windup=swing?Math.Max(.025f,a.SwingAt-a.AttackAt):Math.Max(.025f,a.VisualWindup),window=swing?a.SwingUntil-a.SwingAt:a.VisualActive;
            bool rolled=a.RollAt>a.AttackAt&&a.RollAt<=time;
            bool active=a.AttackUntil>a.AttackAt&&age<windup+window+NativeMeleeRules.Recovery(a.SourceSpec)/rate&&!rolled;
            return active&&age>=windup*.88f&&age<=windup+window+.07f;
        }
        // three r180 primitives as weapon-visuals.ts:9 builds them: BoxGeometry(1,1,1), SphereGeometry(1,8,6), ConeGeometry(1,1,5)
        // and TorusGeometry(1,.055,4,16), in three's convention (convert like the weapon exports: x=-x, nx=-nx, swap winding).
        public static void Primitive(WeaponShape shape,List<Vector3> positions,List<Vector3> normals,List<int> indices)
        {
            positions.Clear();normals.Clear();indices.Clear();
            if(shape==WeaponShape.Cube)
            {
                // BoxGeometry.buildPlane for px, nx, py, ny, pz, nz (axes 0=x, 1=y, 2=z).
                void Plane(int u,int v,int w,int udir,int vdir,float width,float height,float depth)
                {
                    int start=positions.Count;
                    for(int iy=0;iy<2;iy++)for(int ix=0;ix<2;ix++)
                    {
                        var p=new float[3];var nrm=new float[3];p[u]=(ix-.5f)*width*udir;p[v]=(iy-.5f)*height*vdir;p[w]=depth/2;nrm[w]=depth>0?1:-1;
                        positions.Add(new Vector3(p[0],p[1],p[2]));normals.Add(new Vector3(nrm[0],nrm[1],nrm[2]));
                    }
                    int a=start,b=start+2,c=start+3,d=start+1;indices.Add(a);indices.Add(b);indices.Add(d);indices.Add(b);indices.Add(c);indices.Add(d);
                }
                Plane(2,1,0,-1,-1,1,1,1);Plane(2,1,0,1,-1,1,1,-1);Plane(0,2,1,1,1,1,1,1);Plane(0,2,1,1,-1,1,1,-1);Plane(0,1,2,1,-1,1,1,1);Plane(0,1,2,-1,-1,1,1,-1);
                return;
            }
            if(shape==WeaponShape.Sphere)
            {
                const int W=8,H=6;
                for(int iy=0;iy<=H;iy++)for(int ix=0;ix<=W;ix++)
                {
                    double u=(double)ix/W,v=(double)iy/H;var p=new Vector3((float)(-Math.Cos(u*Math.PI*2)*Math.Sin(v*Math.PI)),(float)Math.Cos(v*Math.PI),(float)(Math.Sin(u*Math.PI*2)*Math.Sin(v*Math.PI)));
                    positions.Add(p);normals.Add(p.LengthSquared()>0?Vector3.Normalize(p):p);
                }
                for(int iy=0;iy<H;iy++)for(int ix=0;ix<W;ix++)
                {
                    int a=iy*(W+1)+ix+1,b=iy*(W+1)+ix,c=(iy+1)*(W+1)+ix,d=(iy+1)*(W+1)+ix+1;
                    if(iy!=0){indices.Add(a);indices.Add(b);indices.Add(d);}if(iy!=H-1){indices.Add(b);indices.Add(c);indices.Add(d);}
                }
                return;
            }
            if(shape==WeaponShape.Cone)
            {
                // CylinderGeometry(0, 1, 1, 5, 1, closed): the torso rows (apex at +.5), then the bottom cap.
                const int R=5;float slope=1;
                for(int y=0;y<=1;y++)for(int x=0;x<=R;x++)
                {
                    double theta=(double)x/R*Math.PI*2;float radius=y;float s=(float)Math.Sin(theta),c=(float)Math.Cos(theta);
                    positions.Add(new Vector3(radius*s,-y+.5f,radius*c));normals.Add(Vector3.Normalize(new Vector3(s,slope,c)));
                }
                for(int x=0;x<R;x++){int b=R+1+x,c=R+1+x+1,d=x+1;indices.Add(b);indices.Add(c);indices.Add(d);}
                int center=positions.Count;for(int x=1;x<=R;x++){positions.Add(new Vector3(0,-.5f,0));normals.Add(new Vector3(0,-1,0));}
                int ring=positions.Count;for(int x=0;x<=R;x++){double theta=(double)x/R*Math.PI*2;positions.Add(new Vector3((float)Math.Sin(theta),-.5f,(float)Math.Cos(theta)));normals.Add(new Vector3(0,-1,0));}
                for(int x=0;x<R;x++){indices.Add(ring+x+1);indices.Add(ring+x);indices.Add(center+x);}
                return;
            }
            const int Radial=4,Tubular=16;const double Tube=.055;
            for(int j=0;j<=Radial;j++)for(int i=0;i<=Tubular;i++)
            {
                double u=(double)i/Tubular*Math.PI*2,v=(double)j/Radial*Math.PI*2;
                var p=new Vector3((float)((1+Tube*Math.Cos(v))*Math.Cos(u)),(float)((1+Tube*Math.Cos(v))*Math.Sin(u)),(float)(Tube*Math.Sin(v)));
                positions.Add(p);normals.Add(Vector3.Normalize(p-new Vector3((float)Math.Cos(u),(float)Math.Sin(u),0)));
            }
            for(int j=1;j<=Radial;j++)for(int i=1;i<=Tubular;i++)
            {
                int a=(Tubular+1)*j+i-1,b=(Tubular+1)*(j-1)+i-1,c=(Tubular+1)*(j-1)+i,d=(Tubular+1)*j+i;
                indices.Add(a);indices.Add(b);indices.Add(d);indices.Add(b);indices.Add(c);indices.Add(d);
            }
        }
    }
    // The identity of one swept ribbon (melee-trails.ts:53): every strike or special gets its own ribbon, and older ribbons
    // keep fading on their own. A released action keys on its kind and start; a strike on its start and attack index.
    public struct SweepKey : IEquatable<SweepKey>
    {
        public int Id,Index;public float At;public string Action;
        public static SweepKey Of(Actor a)=>a.SpecialReleased&&a.SpecialKind!=null?new SweepKey{Id=a.Id,Action=a.SpecialKind,At=a.SpecialAt}:new SweepKey{Id=a.Id,At=a.AttackAt,Index=a.Combo+1};
        public bool Equals(SweepKey o)=>Id==o.Id&&Index==o.Index&&At.Equals(o.At)&&string.Equals(Action,o.Action,StringComparison.Ordinal);
        public override bool Equals(object o)=>o is SweepKey k&&Equals(k);
        public override int GetHashCode()=>Id*397^Index*31^At.GetHashCode();
    }
    // MeleeTrails (melee-trails.ts:37-102): at most 16 live sweeps of at most 48 samples, three sub-frame samples per frame on
    // full effects (one below), the blade turned about the grip between frames so fast arcs stay curved. Sampling uses the
    // enabled flag and quality of the previous Update, as the source's sample() reads the values its update() stored.
    // Buffers are pooled: a sweeping frame allocates nothing once the pool is warm.
    public sealed class NativeMeleeSweeps
    {
        public const int MaxSweeps=16,MaxSamples=48;
        public struct Sample {public Vector3 Grip,Dir;public float Length,At;}
        public sealed class Sweep
        {
            public SweepKey Key;public int Color,Count;public float Seen,Life,Base;public readonly Sample[] Samples=new Sample[MaxSamples+4];
            internal void Push(Sample s)=>Samples[Count++]=s;
            internal void Drop(int k){if(k<=0)return;Array.Copy(Samples,k,Samples,0,Count-k);Count-=k;}
        }
        private readonly List<Sweep> live=new List<Sweep>(MaxSweeps);private readonly Stack<Sweep> spare=new Stack<Sweep>(MaxSweeps);
        private bool enabled;private float quality=1;
        public int Count=>live.Count;public Sweep this[int i]=>live[i];public bool Enabled=>enabled;
        private Sweep Find(SweepKey key){for(int i=0;i<live.Count;i++)if(live[i].Key.Equals(key))return live[i];return null;}
        // One blade pose at `time` (melee-trails.ts:44-65): call only while Sweeping and the blade is visible.
        public bool Add(SweepKey key,string spec,int color,Vector3 grip,Vector3 tip,float time)
        {
            if(!enabled)return false;var s=Find(key);
            if(s==null){if(live.Count>=MaxSweeps)return false;s=spare.Count>0?spare.Pop():new Sweep();s.Key=key;s.Count=0;s.Seen=time;s.Life=NativeWeaponDressing.SweepLife(spec,quality);s.Base=NativeWeaponDressing.SweepBase(spec);live.Add(s);}
            s.Color=color;float length=Vector3.Distance(tip,grip);Vector3 dir=(tip-grip)/Math.Max(1e-6f,length);
            if(s.Count>0)
            {
                var last=s.Samples[s.Count-1];
                if(time>last.At&&time-last.At<.1f)
                {
                    int steps=quality>=1?3:1;var turn=FromUnitVectors(last.Dir,dir);
                    for(int i=1;i<=steps;i++){float f=(float)i/(steps+1);s.Push(new Sample{Grip=last.Grip+(grip-last.Grip)*f,Dir=Vector3.Transform(last.Dir,Slerp(Quaternion.Identity,turn,f)),Length=last.Length+(length-last.Length)*f,At=last.At+(time-last.At)*f});}
                }
                if(time>last.At)s.Push(new Sample{Grip=grip,Dir=dir,Length=length,At=time});
            }
            else s.Push(new Sample{Grip=grip,Dir=dir,Length=length,At=time});
            if(s.Count>MaxSamples)s.Drop(s.Count-MaxSamples);s.Seen=time;return true;
        }
        // melee-trails.ts:67-74: drop samples older than the life, and release a sweep that has fewer than two samples once it
        // is older than its life or time ran backwards (a replay). Disabled trails clear every sweep at once.
        public void Update(float time,bool on,float effects)
        {
            enabled=on;quality=effects;if(!on){Clear();return;}
            for(int i=0;i<live.Count;)
            {
                var s=live[i];int old=0;while(old<s.Count&&time-s.Samples[old].At>s.Life)old++;s.Drop(old);
                if(s.Count<2&&(time-s.Seen>s.Life||time<s.Seen-.1f)){live.RemoveAt(i);spare.Push(s);continue;}
                i++;
            }
        }
        public void Clear(){for(int i=0;i<live.Count;i++)spare.Push(live[i]);live.Clear();}
        private static float TipSpeed(in Sample a,in Sample b)
        {float dt=b.At-a.At;if(dt<=1e-6f)return 0;return Vector3.Distance(b.Grip+b.Dir*b.Length,a.Grip+a.Dir*a.Length)/dt;}
        // melee-trails.ts:92-101: per sample an inner-edge vertex (Base along the blade) and a blade-edge vertex, with colour
        // scalars glow*.04 and glow*.62, glow = fade^2 * (.2+.8*(i+1)/n) * (.7+.45*speed/fastest). Writes 2n vertices at
        // `offset` and returns n, or 0 when the sweep draws nothing this frame.
        public static int Build(Sweep s,float time,Vector3[] positions,float[] scalars,int offset)
        {
            int n=s.Count;if(n<2||offset+n*2>positions.Length||offset+n*2>scalars.Length)return 0;
            float top=1e-6f;for(int i=1;i<n;i++)top=Math.Max(top,TipSpeed(s.Samples[i-1],s.Samples[i]));
            for(int i=0;i<n;i++)
            {
                var k=s.Samples[i];float fade=Math.Max(0,1-(time-k.At)/s.Life),speed=TipSpeed(s.Samples[Math.Max(0,i-1)],s.Samples[Math.Min(n-1,Math.Max(1,i))])/top;
                float glow=fade*fade*(.2f+.8f*(i+1)/n)*(.7f+.45f*speed);
                positions[offset+i*2]=k.Grip+k.Dir*(k.Length*s.Base);positions[offset+i*2+1]=k.Grip+k.Dir*k.Length;
                scalars[offset+i*2]=glow*.04f;scalars[offset+i*2+1]=glow*.62f;
            }
            return n;
        }
        // three r180 Quaternion.setFromUnitVectors and slerp, so the sub-frame arcs match the source's turn exactly.
        private static Quaternion FromUnitVectors(Vector3 from,Vector3 to)
        {
            double r=Vector3.Dot(from,to)+1.0,x,y,z;
            if(r<1e-8){r=0;if(Math.Abs(from.X)>Math.Abs(from.Z)){x=-from.Y;y=from.X;z=0;}else{x=0;y=-from.Z;z=from.Y;}}
            else{x=from.Y*to.Z-from.Z*to.Y;y=from.Z*to.X-from.X*to.Z;z=from.X*to.Y-from.Y*to.X;}
            double l=Math.Sqrt(x*x+y*y+z*z+r*r);return l==0?Quaternion.Identity:new Quaternion((float)(x/l),(float)(y/l),(float)(z/l),(float)(r/l));
        }
        private static Quaternion Slerp(Quaternion a,Quaternion b,float t)
        {
            if(t==0)return a;if(t==1)return b;
            double cos=a.W*b.W+a.X*b.X+a.Y*b.Y+a.Z*b.Z,bx=b.X,by=b.Y,bz=b.Z,bw=b.W;if(cos<0){bx=-bx;by=-by;bz=-bz;bw=-bw;cos=-cos;}
            if(cos>=1)return a;double sin2=1-cos*cos;
            if(sin2<=2.220446049250313e-16){double s=1-t;var q=new Quaternion((float)(s*a.X+t*bx),(float)(s*a.Y+t*by),(float)(s*a.Z+t*bz),(float)(s*a.W+t*bw));return Quaternion.Normalize(q);}
            double sin=Math.Sqrt(sin2),half=Math.Atan2(sin,cos),ra=Math.Sin((1-t)*half)/sin,rb=Math.Sin(t*half)/sin;
            return new Quaternion((float)(a.X*ra+bx*rb),(float)(a.Y*ra+by*rb),(float)(a.Z*ra+bz*rb),(float)(a.W*ra+bw*rb));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One built minion model: one section per rig joint (bank frame: Unity axes, +x the add's right, +z forward, each
    /// joint's own frame at rest), the triangles and whether it is the impostor tier.</summary>
    public sealed class HordeModel{public int Kind,Look,Tier;public WeaponMesh Mesh;public int Triangles=>Mesh.Triangles;}

    /// <summary>
    /// The Siren's minions, sculpted in code (presentation only; the exported horde-0..5 bank bodies stay for the classic look
    /// and the old death records). Since 2026-10-12 every kind is a zombie built from the pilots (Core/NativeZombieModels.cs,
    /// the builders and the zombie palette): the Standard pilot's blocks rotted and torn, the armour cracked and half gone, the
    /// jaw hanging open, the eyes glowing in the kind's colour and the Siren's ink running through the wounds as veins that
    /// flicker cell by cell and drop out with the add's failing power (NativeMinionFx glow codes); the brute a big muscle
    /// version. Each kind keeps its silhouette at 30 m and is built inside its hit volume (NativeHordeKinds.Radius/Height):
    /// Tests/HordeKindChecks poses them and holds every vertex to the capsule. This file keeps the shared kit.
    /// <list type="bullet">
    /// <item>Three tiers, as the horde's crowd tiers draw them: Full (the nearest 24 adds, 16 on the low graphics tier),
    /// Reduced (the rest within 40 m) and Impostor (a dozen boxes per add, the far crowd), with the same joints, so lost limbs,
    /// the char and the burn-away work on all three.</item>
    /// <item>Looks: whole variants per kind (NativeHordeKinds.Looks), chosen by the add's id.</item>
    /// <item>The black-flame tongues of the old bank (NativeMinionFx) grow from each kind's own anchors here
    /// (<see cref="Tongues"/>), and a cracked look keeps the crack haze (<see cref="Crack"/>).</item>
    /// </list>
    /// Built once when the source models load (Runtime/NativeHordeKindModels.cs); nothing here runs per frame.
    /// </summary>
    public static partial class NativeHordeModels
    {
        public const int Full=0,Reduced=1,Impostor=2,Tiers=3;
        public static readonly string[] TierNames={"full","reduced","impostor"};
        /// <summary>The bank id of a kind's look at a tier.</summary>
        public static string Id(int kind,int look,int tier)=>"minion-"+NativeHordeKinds.Ids[kind]+"-"+look+"-"+TierNames[tier];
        /// <summary>Triangle budgets per tier (every kind and look stays under them; the checks hold them).</summary>
        public static readonly int[] Budget={7200,3000,600};

        // ---- palette (sRGB): the Siren's inks; the seams are the kind's (NativeHordeKinds.Seam) ----
        private const int InkHex=0x15111c,SheenHex=0x2a2238,DeepHex=0x09070c,ClothHex=0x1d1628,LiningHex=0x2e1838,BoneHex=0x8f87a6;
        private static WeaponPaint Ink=>WeaponPaint.Solid(InkHex);private static WeaponPaint Sheen=>WeaponPaint.Solid(SheenHex);private static WeaponPaint Deep=>WeaponPaint.Solid(DeepHex);
        private static WeaponPaint Cloth=>WeaponPaint.Solid(ClothHex);private static WeaponPaint Lining=>WeaponPaint.Solid(LiningHex);private static WeaponPaint Bone=>WeaponPaint.Solid(BoneHex,.08f);
        /// <summary>The grin's teeth and the eyes: the Siren's white, steady (no glow code: they never flicker or drop out).</summary>
        private static WeaponPaint Teeth=>WeaponPaint.Glow(0xf4efff,2.2f,.9f);private static WeaponPaint Eye=>WeaponPaint.Glow(0xffffff,3f,1);private static WeaponPaint Mouth=>WeaponPaint.Solid(0x050307);

        public static HordeModel Build(int kind,int look,int tier)
        {
            kind=Math.Max(0,Math.Min(NativeHordeKinds.Count-1,kind));look=Math.Max(0,Math.Min(NativeHordeKinds.Looks[kind]-1,look));tier=Math.Max(Full,Math.Min(Impostor,tier));
            var k=new K(kind,look,tier);
            switch(kind)
            {
                case NativeHordeKinds.Runner:Runner(k);break;case NativeHordeKinds.Brute:Brute(k);break;case NativeHordeKinds.Spitter:Spitter(k);break;
                case NativeHordeKinds.Crawler:Crawler(k);break;case NativeHordeKinds.Bloater:Bloater(k);break;default:Shambler(k);break;
            }
            return new HordeModel{Kind=kind,Look=look,Tier=tier,Mesh=k.Mesh};
        }

        // ================================================================================================================
        // The kit: one WeaponSculpt over a mesh with a section per joint; limbs authored on the left (bank -x) and mirrored.
        // ================================================================================================================
        private sealed class K
        {
            public readonly WeaponMesh Mesh;public readonly WeaponSculpt S;public readonly int Tier,Kind,Look;private float side=1;private int cell;private readonly uint seed;
            public K(int kind,int look,int tier){Kind=kind;Look=look;Tier=tier;Mesh=new WeaponMesh{Spec="minion-"+kind,Tier=tier};S=new WeaponSculpt(Mesh,tier);seed=(uint)(kind*977+look*131+17);}
            public bool Fine=>Tier==Full;public bool Mid=>Tier<=Reduced;public bool Far=>Tier==Impostor;
            /// <summary>Starts drawing on `bone`; `mirror` draws a left-authored (bank -x) part on the right.</summary>
            public void On(int bone,bool mirror=false){if(mirror)bone=Right(bone);S.Into(bone);side=mirror?-1:1;}
            /// <summary>The right side's joint for a left one (clavicle to wrist, hip to toe).</summary>
            public static int Right(int bone)=>bone>=CapturedRig.LClav&&bone<=CapturedRig.LHand||bone>=CapturedRig.LHip&&bone<=CapturedRig.LToe?bone+4:bone;
            public float Side=>side;
            /// <summary>The kind's seam glow: a new flicker cell per piece (NativeMinionFx glow code), `k` its strength.</summary>
            public WeaponPaint Seam(float k=1,int hex=-1){var c=WeaponPaint.Linear(hex<0?NativeHordeKinds.Seam[Kind]:hex);double h=NativeGoreRules.Hash01(seed,cell++,0x5e);return new WeaponPaint(c*.32f,c*(1.9f*k),(float)NativeMinionFx.Code(NativeHordeKinds.SeamClass[Kind],h));}
            /// <summary>A glow piece on another flicker class (0 shell embers, 1 accent plates, 2 strips).</summary>
            public WeaponPaint Glow(int cls,float k,int hex){var c=WeaponPaint.Linear(hex);double h=NativeGoreRules.Hash01(seed,cell++,0x5f);return new WeaponPaint(c*.3f,c*k,(float)NativeMinionFx.Code(cls,h));}
            private void Mirror(Action draw){if(side<0){S.Push(WeaponSculpt.S(-1,1,1));draw();S.Pop();}else draw();}
            /// <summary>A box by centre and full size (left-authored x), turned by Euler XYZ (radians) about its centre.</summary>
            public void Box(in WeaponPaint p,float x,float y,float z,float sx,float sy,float sz,float bevel=0,float rx=0,float ry=0,float rz=0)
            {
                if(Tier==Reduced&&bevel<.012f)bevel=0;var c=new Vector3(x,y,z);var s=new Vector3(sx,sy,sz);var paint=p;
                if(side<0){S.Push(WeaponSculpt.S(-1,1,1));S.Box(paint,c,s,bevel,rx,ry,rz);S.Pop();}else S.Box(paint,c,s,bevel,rx,ry,rz);
            }
            /// <summary>A box spanning bounds (left-authored x).</summary>
            public void Span(in WeaponPaint p,float x0,float x1,float y0,float y1,float z0,float z1,float bevel=0)=>Box(p,(x0+x1)*.5f,(y0+y1)*.5f,(z0+z1)*.5f,Math.Abs(x1-x0),Math.Abs(y1-y0),Math.Abs(z1-z0),bevel);
            /// <summary>A tapered limb segment hanging down the joint's -y from `top` to `bottom`: a lofted octagon (box on the far tier).</summary>
            public void Limb(in WeaponPaint p,float top,float bottom,float r0,float r1,float x=0,float z=0,float squash=1,float lean=0)
            {
                if(Far){Box(p,x,(top+bottom)*.5f,z+lean*.5f,(r0+r1)*1.6f,Math.Abs(top-bottom),(r0+r1)*1.6f*squash);return;}
                int sides=S.Sides(8);var rings=new List<Vector3[]>(4);float[] at={0,.18f,.62f,1},rad={.88f,1,.92f,.8f};
                for(int i=0;i<4;i++){float u=at[i],r=(r0+(r1-r0)*u)*rad[i];float y=top+(bottom-top)*u;var ring=WeaponSculpt.Circle(sides,r,r*squash,0,(float)Math.PI/sides);for(int q=0;q<sides;q++)ring[q]=new Vector3(ring[q].X+x,y,ring[q].Y+z+lean*u);rings.Add(ring);}
                // Circle rings lie in xy about +z; laid flat in xz they run counter-clockwise looking down -y.
                var paint=p;Mirror(()=>S.Loft(rings,(i,q)=>paint,Mid?1:0));
            }
            /// <summary>An ellipsoid (faceted below Full).</summary>
            public void Blob(in WeaponPaint p,float x,float y,float z,float rx,float ry,float rz)
            {
                if(Far){Box(p,x,y,z,rx*1.7f,ry*1.7f,rz*1.7f);return;}var paint=p;int rings=Fine?6:4,sides=S.Sides(10);
                Mirror(()=>{S.Push(WeaponSculpt.Rx(-(float)Math.PI/2));S.Sphere(paint,new Vector3(x,-z,y),new Vector3(rx,rz,ry),rings,sides,!Fine);S.Pop();});
            }
            /// <summary>A point (spike, claw, fin): a thin pyramid from a base box (x, y, z, w, d) along a direction.</summary>
            public void Spike(in WeaponPaint p,float x,float y,float z,float w,float d,Vector3 tip)
            {
                var b0=new Vector3(x-w*.5f,y,z-d*.5f);var b1=new Vector3(x+w*.5f,y,z-d*.5f);var b2=new Vector3(x+w*.5f,y,z+d*.5f);var b3=new Vector3(x-w*.5f,y,z+d*.5f);var t=new Vector3(x,y,z)+tip;var paint=p;
                var mid=(b0+b1+b2+b3)*.25f;
                Mirror(()=>{Out(paint,b0,b1,t,mid);Out(paint,b1,b2,t,mid);Out(paint,b2,b3,t,mid);Out(paint,b3,b0,t,mid);Out(paint,b0,b1,b2,t,true);Out(paint,b0,b2,b3,t,true);});
            }
            // A triangle facing away from `inside` (`under`: the base, facing away from the tip).
            private void Out(in WeaponPaint p,Vector3 a,Vector3 b,Vector3 c,Vector3 inside,bool under=false)
            {
                var n=Vector3.Cross(b-a,c-a);var mid=(a+b+c)/3;
                if(Vector3.Dot(n,mid-inside)<0^under){var t=b;b=c;c=t;}S.Tri(p,a,b,c);
            }
            /// <summary>A flat polygon slab facing +z (outline counter-clockwise in xy) from z0 to z1.</summary>
            public void Plate(in WeaponPaint side,in WeaponPaint front,Vector3[] outline,float z0,float z1)
            {
                var s=side;var f=front;Mirror(()=>S.Slab(_=>s,f,s,outline,z0,z1,0));
            }
            /// <summary>The Siren's grin on a face at depth z: a crescent of jagged white teeth over a black mouth, `w` half wide,
            /// `drop` how far its middle sags (a smile), `h` its height; a slab on the far tier.</summary>
            public void Grin(float cx,float cy,float z,float w,float drop,float h,int teeth,float tilt=0)
            {
                if(Far){Box(Teeth,cx,cy-drop*.5f,z,w*2,h*.8f,.012f,0,0,0,tilt);return;}
                if(!Mid)teeth=Math.Max(5,teeth*2/3);
                float Curve(float u)=>cy-drop*(1-u*u)+tilt*u*w;
                // The mouth's black back plate, a little behind the teeth.
                var back=new Vector3[18];for(int i=0;i<9;i++){float u=-1+2*i/8f;back[i]=new Vector3(cx+u*w*1.04f,Curve(u)-h*.62f,0);back[17-i]=new Vector3(cx+u*w*1.04f,Curve(u)+h*.5f*(1-.35f*u*u),0);}
                Plate(Mouth,Mouth,back,z-.012f,z-.002f);
                // Upper teeth hang from the top lip, lower ones rise from the bottom lip, offset by half a tooth (a zig-zag bite).
                for(int i=0;i<teeth;i++)
                {
                    float u0=-1+2f*i/teeth,u1=-1+2f*(i+1)/teeth,um=(u0+u1)*.5f;float top=h*.5f*(1-.35f*um*um),len=h*(.72f+.18f*(float)Math.Sin(i*2.3f));
                    Tooth(new Vector3(cx+u0*w,Curve(u0)+top,z),new Vector3(cx+u1*w,Curve(u1)+top,z),new Vector3(cx+um*w,Curve(um)+top-len,z+.004f));
                    if(i<teeth-1){float v0=um,v1=-1+2f*(i+1.5f)/teeth,vm=u1;float bot=-h*.62f;Tooth(new Vector3(cx+v1*w,Curve(v1)+bot,z),new Vector3(cx+v0*w,Curve(v0)+bot,z),new Vector3(cx+vm*w,Curve(vm)+bot+len*.92f,z+.004f));}
                }
            }
            private void Tooth(Vector3 a,Vector3 b,Vector3 tip){var t=Teeth;var back=tip-new Vector3(0,0,.012f);if(Vector3.Cross(b-a,tip-a).Z<0){var s=a;a=b;b=s;}Mirror(()=>{S.Tri(t,a,b,tip);S.Tri(t,b,a,back);});}
            /// <summary>The Siren's teardrop eyes on a face at depth z: spaced `gap` apart, `w` x `h`, the points slanting in by `slant`.</summary>
            public void Eyes(float cx,float cy,float z,float gap,float w,float h,float slant)
            {
                foreach(int s in new[]{-1,1})
                {
                    float ex=cx+s*gap*.5f;
                    if(Far){Box(Eye,ex,cy,z,w,h*.6f,.012f,0,0,-s*slant);continue;}
                    // A teardrop: round on the outside, pointed toward the nose and down (the Siren's angry tilt).
                    int n=Fine?9:6;var o=new Vector3[n+1];
                    for(int i=0;i<n;i++){double a=Math.PI*.5+Math.PI*i/(n-1);float x=(float)Math.Cos(a)*w*.5f,y=(float)Math.Sin(a)*h*.5f;o[i]=new Vector3(ex-s*x,cy+y,0);}
                    o[n]=new Vector3(ex-s*w*.62f,cy-h*.28f-slant*.05f,0);if(s<0)Array.Reverse(o);
                    var eye=Eye;Plate(Deep,eye,Orient(o),z-.006f,z+.004f);
                }
            }
            private static Vector3[] Orient(Vector3[] o){float a=0;for(int i=0;i<o.Length;i++){var p=o[i];var q=o[(i+1)%o.Length];a+=p.X*q.Y-q.X*p.Y;}if(a<0)Array.Reverse(o);return o;}
            /// <summary>A stitched seam: a glowing strip from a to b (joint frame) with cross stitches, lifted off the surface by its normal.</summary>
            public void Stitch(Vector3 a,Vector3 b,Vector3 n,float w,int crosses,float k=1)
            {
                if(Far)return;var d=b-a;float l=d.Length();if(l<1e-4f)return;d/=l;n=Vector3.Normalize(n);var u=Vector3.Normalize(Vector3.Cross(n,d));var seam=Seam(k);
                Quad(seam,a+n*.003f-u*w*.5f,b+n*.003f-u*w*.5f,b+n*.003f+u*w*.5f,a+n*.003f+u*w*.5f);
                if(!Mid)return;for(int i=0;i<crosses;i++){var c=a+d*(l*(i+.5f)/crosses)+n*.004f;var e=u*w*2.2f;var f=d*w*.9f;Quad(seam,c-e-f,c+e-f,c+e+f,c-e+f);}
            }
            public void Quad(in WeaponPaint p,Vector3 a,Vector3 b,Vector3 c,Vector3 d){var paint=p;Mirror(()=>{S.Quad(paint,a,b,c,d);S.Quad(paint,d,c,b,a);});}
            /// <summary>A collar of `count` torn, slanted plates round a ring (radius r at height y, the Siren's high couture collar),
            /// open at the front by `open` radians, each plate leaning out by `flare` and `tall` high.</summary>
            public void Collar(float y,float r,float tall,int count,float open,float flare,float depth=.022f,float zc=0)
            {
                if(Far){Box(Cloth,0,y+tall*.4f,zc-r*.3f,r*2,tall*.8f,r*1.2f);return;}if(!Mid)count=Math.Max(4,count*2/3);
                for(int i=0;i<count;i++)
                {
                    float u=(i+.5f)/count;double a=open*.5f+(Math.PI*2-open)*u;float x=(float)Math.Sin(a)*r,z=(float)Math.Cos(a)*r+zc;float rot=(float)a;
                    float h=tall*(.75f+.4f*(float)NativeGoreRules.Hash01(seed,i,0x77));var paint=i%3==1?Lining:Cloth;
                    S.Push(WeaponSculpt.Rx(flare)*WeaponSculpt.Ry(rot)*WeaponSculpt.T(x,y,z));
                    S.Box(paint,new Vector3(0,h*.5f,0),new Vector3(r*1.05f*(float)Math.Sin(Math.PI/count)*2.1f,h,depth),Fine?.006f:0);
                    if(Fine)S.Box(Seam(.7f),new Vector3(0,h*.98f,depth*.5f),new Vector3(r*(float)Math.Sin(Math.PI/count)*1.6f,.008f,.006f));
                    S.Pop();
                }
            }
            /// <summary>A tattered hem: `count` hanging strips round an ellipse (rx, rz at height y), each `len` long, splayed by `flare`;
            /// `back` keeps only the rear half (coat tails).</summary>
            public void Hem(float y,float rx,float rz,int count,float len,float flare,bool back=false,float zc=0)
            {
                if(Far){Box(Cloth,0,y-len*.45f,zc-(back?rz*.6f:0),rx*2,len*.9f,back?rz*.8f:rz*2);return;}if(!Mid)count=Math.Max(4,count/2);
                for(int i=0;i<count;i++)
                {
                    float u=(i+.5f)/count;double a=back?Math.PI*.6+Math.PI*.8*u:Math.PI*2*u;float x=(float)Math.Sin(a)*rx,z=(float)Math.Cos(a)*rz+zc;
                    float l=len*(.6f+.55f*(float)NativeGoreRules.Hash01(seed,i,0x78)),w=(float)(Math.PI*2*Math.Sqrt((rx*rx+rz*rz)*.5)/(back?count*2.4:count))*1.15f;var paint=i%4==2?Lining:Cloth;
                    S.Push(WeaponSculpt.Rx(-flare)*WeaponSculpt.Ry((float)a)*WeaponSculpt.T(x,y,z));
                    // A strip that narrows to a torn point.
                    var top0=new Vector3(-w*.5f,0,0);var top1=new Vector3(w*.5f,0,0);var mid0=new Vector3(-w*.42f,-l*.7f,.004f);var mid1=new Vector3(w*.45f,-l*.62f,.004f);var tip=new Vector3(w*.08f*(i%2==0?1:-1),-l,.008f);
                    S.Quad(paint,top0,top1,mid1,mid0);S.Quad(paint,mid0,mid1,top1,top0);S.Tri(paint,mid0,mid1,tip);S.Tri(paint,mid1,mid0,tip);
                    S.Pop();
                }
            }
        }

        // ================================================================================================================
        // Black-flame tongues (NativeMinionFx.Tongue swells and sways them; the bank's horde-tongue pieces draw them) and the
        // crack's light, per kind and look. Anchors are in the joint frame in source axes (+x the add's left), as the old bank's.
        // ================================================================================================================
        /// <summary>One tongue: joint, the bank piece (0/1), anchor (source axes), base turn (quaternion xyzw), height, phase.</summary>
        public struct Tongue{public int Joint,Layout;public double X,Y,Z,Qx,Qy,Qz,Qw,Height,Phase;}
        private static Tongue T(int joint,int layout,double x,double y,double z,double lean,double height,double phase)
        {double h=lean*.5;return new Tongue{Joint=joint,Layout=layout,X=x,Y=y,Z=z,Qx=Math.Sin(h),Qy=0,Qz=0,Qw=Math.Cos(h),Height=height,Phase=phase};}
        private static Tongue TZ(int joint,int layout,double x,double y,double z,double roll,double height,double phase)
        {double h=roll*.5;return new Tongue{Joint=joint,Layout=layout,X=x,Y=y,Z=z,Qx=0,Qy=0,Qz=Math.Sin(h),Qw=Math.Cos(h),Height=height,Phase=phase};}
        private static readonly Tongue[][][] tongues=MakeTongues();
        /// <summary>The tongues a kind's look grows (the same on every tier).</summary>
        public static Tongue[] Tongues(int kind,int look){var k=tongues[Math.Max(0,Math.Min(NativeHordeKinds.Count-1,kind))];return k[Math.Max(0,Math.Min(k.Length-1,look))];}
        public static int MostTongues{get{int n=0;foreach(var k in tongues)foreach(var l in k)n=Math.Max(n,l.Length);return n;}}
        private static Tongue[][][] MakeTongues()
        {
            const int H=CapturedRig.Head,C=CapturedRig.Chest,S=CapturedRig.Spine,LC=CapturedRig.LClav,RC=CapturedRig.RClav,LE=CapturedRig.LElbow,RA=CapturedRig.RArm;
            return new[]{
                // shambler: collar, hood, crack
                new[]{new[]{T(H,0,.02,.31,-.02,-.2,.9,.3),TZ(C,1,.12,.3,-.05,.5,.8,2.1),TZ(C,0,-.13,.3,-.04,-.5,.75,4.2),T(RA,1,.0,-.1,-.09,-.25,.7,5.3)},
                      new[]{T(H,0,0,.33,-.1,-.6,1.0,1.1),T(S,1,.1,.12,-.13,-.4,.7,3.3),T(LE,0,.06,-.12,-.05,-.2,.6,5.9)},
                      new[]{T(H,0,.04,.32,.03,-.1,1.2,.7),T(H,1,-.06,.31,-.05,-.3,.9,2.9),TZ(C,0,.14,.27,-.08,.6,.8,4.6),T(S,1,-.08,.12,-.13,-.4,.7,6.0)}},
                // runner: crown fin flame and the shoulder blades
                new[]{new[]{T(H,0,0,.26,-.1,-.9,1.0,.9),TZ(LC,1,.09,.06,-.04,.5,.7,2.4),TZ(RC,1,-.09,.06,-.04,-.5,.7,4.8)},
                      new[]{T(H,1,.05,.25,-.06,-.8,.9,1.6),T(H,0,-.05,.25,-.06,-.8,.9,3.1),T(C,1,0,.24,-.12,-.6,.8,5.2)}},
                // brute: the hump and the pauldrons
                new[]{new[]{T(C,0,0,.36,-.12,-.3,1.2,.4),TZ(LC,1,.12,.1,0,.8,.95,2.2),TZ(RC,1,-.12,.1,0,-.8,.95,4.4),T(H,0,0,.22,0,-.2,.8,5.6)},
                      new[]{T(C,0,.08,.36,-.12,-.3,1.1,1.2),T(C,1,-.08,.36,-.12,-.3,1.1,3.0),TZ(LC,0,.12,.1,0,.8,.9,4.1),TZ(RC,0,-.12,.1,0,-.8,.9,5.5)}},
                // spitter: the back blisters and the crown
                new[]{new[]{T(H,0,0,.3,-.04,-.3,.9,.2),T(C,1,.06,.2,-.16,-.8,.8,2.6),T(C,0,-.05,.12,-.16,-.9,.7,4.9)},
                      new[]{T(H,1,.04,.3,-.02,-.2,.9,1.4),T(C,0,0,.24,-.15,-.7,.85,3.7)}},
                // crawler: along the spine (it faces up in the pose)
                new[]{new[]{T(C,0,0,.18,-.15,-1.4,.9,.5),T(S,1,0,.1,-.12,-1.4,.8,2.7),T(H,0,0,.22,-.06,-.9,.7,4.4)},
                      new[]{T(C,1,.05,.2,-.14,-1.3,.85,1.9),T(C,0,-.05,.08,-.14,-1.3,.8,3.4),T(S,0,0,.14,-.12,-1.4,.75,5.8)}},
                // bloater: the gut's vents and the crown
                new[]{new[]{T(S,0,.12,.2,.32,.6,.8,.8),T(S,1,-.14,.12,.33,.5,.75,3.2),T(H,0,0,.21,0,-.2,.8,5.1)},
                      new[]{T(S,1,0,.27,.3,.4,.85,1.7),T(H,0,0,.28,0,-.1,.9,4.3)}}};
        }
        /// <summary>
        /// Tongue k of a kind's look at clock t (NativeMinionFx.TongueMatrix with this kind's anchors): its joint, bank piece and
        /// matrix in the joint's frame (row-major, source axes): T(anchor)·R(base·sway)·S(wide, tall, wide). False past its tongues.
        /// </summary>
        public static bool TongueMatrix(int kind,int look,int k,double t,double flare,double extraPhase,double[] m,out int joint,out int layout)
        {
            var list=Tongues(kind,look);joint=-1;layout=0;if(k<0||k>=list.Length)return false;var g=list[k];joint=g.Joint;layout=g.Layout;
            NativeMinionFx.Tongue(t,g.Phase+extraPhase,g.Height,flare,out double wide,out double tall,out double rx,out double rz);
            NativeFxMath.EulerQuaternion(rx,0,rz,out double sx,out double sy,out double sz,out double sw);double bx=g.Qx,by=g.Qy,bz=g.Qz,bw=g.Qw;
            double qx=bw*sx+bx*sw+by*sz-bz*sy,qy=bw*sy-bx*sz+by*sw+bz*sx,qz=bw*sz+bx*sy-by*sx+bz*sw,qw=bw*sw-bx*sx-by*sy-bz*sz;
            NativeMinionFx.Rotation(m,qx,qy,qz,qw,wide,tall,wide,g.X,g.Y,g.Z);return true;
        }
        /// <summary>Whether a look is cracked (the possession hint draws a split on its crown, NativeMinionFx.Hint) and the crack's
        /// light centre on the head (source axes), NaN without one.</summary>
        public static bool Cracked(int kind,int look)=>kind==NativeHordeKinds.Shambler&&look==2;
        public static void Crack(int kind,int look,out double x,out double y,out double z){if(Cracked(kind,look)){x=.03;y=.27;z=.13;}else x=y=z=double.NaN;}
    }
}

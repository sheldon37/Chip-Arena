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
    /// and the old death records). Every kind reads as the Siren's creature: an ink-black body with a violet ink rim (the
    /// Siren's own ink shading), seams stitched in the kind's glow colour that flicker cell by cell and drop out with the
    /// add's failing power (NativeMinionFx glow codes), her couture (high torn collars, corsets laced in glow, tattered hems,
    /// pauldrons) and her white grin and teardrop eyes. Each kind keeps its silhouette at 30 m: the shambler upright and
    /// slumped under its collar, the runner thin and pitched forward with blade claws and streaming tails, the brute a
    /// shoulder-hunched mass with plated fists, the spitter hunched over a glowing throat sac, the crawler low on four
    /// limbs, the bloater a swollen glowing belly. Each is built inside its hit volume (NativeHordeKinds.Radius/Height):
    /// Tests/HordeKindChecks poses them and holds every vertex to the capsule.
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
    public static class NativeHordeModels
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
        // Shared anatomy
        // ================================================================================================================
        // A plain arm on the left (bank -x): upper arm on LArm, forearm on LElbow, a clawed hand on LHand (mirrored right).
        private static void Arm(K k,bool mirror,float upper,float fore,float handW,int claws,float clawLen,float clawCurl,in WeaponPaint shell,bool plates=false)
        {
            k.On(CapturedRig.LArm,mirror);k.Limb(Ink,-.01f,-.27f,upper,upper*.82f);
            if(plates&&k.Mid)k.Box(shell,-.01f,-.07f,0,upper*2.3f,.09f,upper*2.2f,.012f);
            k.On(CapturedRig.LElbow,mirror);k.Limb(Ink,.02f,-.24f,fore,fore*.78f);
            if(k.Mid)k.Box(shell,0,-.13f,.0f,fore*2.05f,.12f,fore*2.05f,.01f);
            if(k.Fine)k.Stitch(new Vector3(-fore*.95f,-.04f,.02f),new Vector3(-fore*.8f,-.22f,.03f),new Vector3(-1,0,.2f),.009f,3,.8f);
            Hand(k,mirror,handW,claws,clawLen,clawCurl);
        }
        private static void Hand(K k,bool mirror,float w,int claws,float len,float curl)
        {
            k.On(CapturedRig.LHand,mirror);k.Box(Ink,0,-.035f,.01f,w,.085f,w*1.05f,.014f);
            if(k.Far){k.Box(Bone,0,-.1f-len*.3f,.03f,w*.9f,len*.7f,.02f);return;}
            for(int i=0;i<claws;i++)
            {
                float u=claws==1?0:i/(claws-1f)-.5f;float x=u*w*.8f,z=.03f+(1-Math.Abs(u))*.012f;
                k.Box(Ink,x,-.09f,z,.024f,.04f,.026f,.004f);
                k.Spike(Bone,x,-.105f,z,.02f,.022f,new Vector3(0,-len,len*curl));
            }
            if(k.Fine)k.Spike(Bone,-w*.5f,-.04f,.03f,.018f,.02f,new Vector3(-.03f,-len*.55f,len*.4f));
        }
        private static void Leg(K k,bool mirror,float thigh,float shin,float footL,float footW,bool toeClaws,in WeaponPaint shin2)
        {
            k.On(CapturedRig.LHip,mirror);k.Limb(Ink,.02f,-.25f,thigh,thigh*.8f);
            k.On(CapturedRig.LKnee,mirror);k.Limb(Ink,.02f,-.24f,shin,shin*.72f);if(k.Mid)k.Box(shin2,0,-.08f,shin*.55f,shin*1.6f,.1f,.04f,.008f);
            k.On(CapturedRig.LFoot,mirror);k.Box(Ink,0,-.035f,-.01f,footW,.06f,footL*.6f,.012f);
            k.On(CapturedRig.LToe,mirror);k.Box(Ink,0,-.012f,.035f,footW*.92f,.045f,footL*.55f,.01f);
            if(toeClaws&&k.Mid)for(int i=-1;i<=1;i++)k.Spike(Bone,i*footW*.3f,-.02f,.08f+.01f*(1-Math.Abs(i)),.018f,.02f,new Vector3(0,-.02f,.045f));
        }
        private static void Neck(K k,float r,float h){k.On(CapturedRig.Neck);k.Limb(Ink,-.04f,h,r,r*.9f);}

        // ================================================================================================================
        // Shambler: the rank and file. Upright but slumped, a torn couture bodice stitched up the front in violet, a high torn
        // collar (look 0), a hood draped over its head (look 1) or a bare cracked crown spilling light (look 2); a ragged hem.
        // ================================================================================================================
        private static void Shambler(K k)
        {
            var plate=Sheen;
            k.On(CapturedRig.Pelvis);k.Span(Ink,-.19f,.19f,-.1f,.1f,-.13f,.14f,.03f);k.Span(Cloth,-.21f,.21f,-.035f,.06f,-.145f,.155f,.012f);
            // A ragged skirt, longer behind (a torn coat's tails).
            k.Hem(-.02f,.21f,.16f,k.Look==1?12:10,.26f,.12f);k.Hem(-.03f,.19f,.15f,k.Look==1?7:5,.36f,.06f,true);
            // A corseted waist: narrower than the chest above it, laced in violet.
            k.On(CapturedRig.Spine);k.Span(Ink,-.15f,.15f,-.01f,.17f,-.12f,.125f,.03f);k.Span(Cloth,-.165f,.165f,.02f,.15f,-.13f,.135f,.012f);
            if(k.Mid)for(int i=0;i<2;i++){float y=.04f+i*.06f;k.Stitch(new Vector3(-.06f,y,.136f),new Vector3(.06f,y+.045f,.136f),Vector3.UnitZ,.008f,0,1f);k.Stitch(new Vector3(.06f,y,.136f),new Vector3(-.06f,y+.045f,.136f),Vector3.UnitZ,.008f,0,1f);}
            // A bodice wider at the shoulders, torn at its hem, stitched up the front.
            k.On(CapturedRig.Chest);k.Span(Ink,-.22f,.22f,-.04f,.27f,-.14f,.15f,.04f);k.Box(Ink,0,.21f,-.01f,.5f,.12f,.27f,.035f);
            k.Span(Cloth,-.235f,.235f,.0f,.22f,.1f,.166f,.02f);k.Span(plate,-.2f,.2f,.03f,.25f,-.16f,-.12f,.015f);
            k.Stitch(new Vector3(0,-.02f,.167f),new Vector3(0,.22f,.167f),Vector3.UnitZ,.012f,5,1.1f);
            if(k.Mid)for(int s=-1;s<=1;s+=2){k.Stitch(new Vector3(s*.05f,.17f,.167f),new Vector3(s*.19f,.07f,.167f),Vector3.UnitZ,.008f,3,.8f);k.Stitch(new Vector3(s*.06f,.07f,.167f),new Vector3(s*.17f,-.01f,.167f),Vector3.UnitZ,.007f,2,.7f);}
            if(k.Look==0)
            {
                // The Siren's high collar: torn plates rising behind the head and flaring out, lined in violet.
                k.Collar(.23f,.155f,.28f,12,1.35f,.24f);
                if(k.Mid)k.Box(k.Seam(.9f),0,.26f,-.17f,.26f,.012f,.012f);
            }
            else if(k.Look==1)
            {
                // A shawl: a torn cape from the shoulders down the back.
                k.Span(Cloth,-.25f,.25f,.16f,.3f,-.18f,.04f,.03f);k.Hem(.18f,.23f,.16f,9,.36f,.12f,true,-.02f);
            }
            else{k.Span(plate,-.23f,.23f,.21f,.29f,-.12f,.12f,.02f);for(int s=-1;s<=1;s+=2)k.Spike(Ink,s*.13f,.24f,-.13f,.06f,.05f,new Vector3(s*.07f,.12f,-.08f));}
            // Shoulders: torn epaulettes.
            foreach(bool m in new[]{false,true}){k.On(CapturedRig.LClav,m);k.Box(Ink,-.05f,0,0,.13f,.11f,.17f,.025f);if(k.Mid){k.Box(Cloth,-.085f,.04f,0,.14f,.04f,.2f,.01f,0,0,.35f);k.Spike(Cloth,-.14f,.0f,.0f,.05f,.16f,new Vector3(-.05f,-.08f,0));}}
            // Long thin arms; the forearm runs past the wrist into the claws.
            Arm(k,false,.058f,.05f,.1f,3,.085f,.35f,plate);Arm(k,true,.058f,.05f,.1f,3,.085f,.35f,plate);
            Leg(k,false,.068f,.058f,.2f,.11f,false,plate);Leg(k,true,.068f,.058f,.2f,.11f,false,plate);
            Neck(k,.058f,.1f);
            k.On(CapturedRig.Head);
            if(k.Look==1)
            {
                // A hood falling over the brow and down the back of the neck; the face sits deep inside it.
                k.Span(Ink,-.13f,.13f,.0f,.27f,-.12f,.12f,.05f);
                k.Span(Cloth,-.155f,.155f,-.04f,.33f,-.16f,.06f,.04f);k.Span(Cloth,-.158f,-.12f,-.06f,.3f,.05f,.155f,.02f);k.Span(Cloth,.12f,.158f,-.06f,.3f,.05f,.155f,.02f);k.Span(Cloth,-.145f,.145f,.27f,.33f,.05f,.165f,.02f);
                if(k.Mid)k.Spike(Cloth,0,.31f,-.1f,.12f,.08f,new Vector3(0,.04f,-.14f));
                k.Grin(0,.07f,.125f,.085f,.025f,.05f,9);k.Eyes(0,.165f,.125f,.105f,.06f,.04f,.25f);
            }
            else
            {
                k.Span(Ink,-.145f,.145f,.0f,.3f,-.14f,.14f,.07f);k.Span(plate,-.13f,.13f,.2f,.31f,-.12f,.12f,.04f);
                k.Span(Ink,-.11f,.11f,-.04f,.04f,-.06f,.13f,.025f);
                k.Grin(0,.075f,.143f,.1f,.03f,.055f,11);k.Eyes(0,.175f,.143f,.12f,.065f,.045f,.25f);
                if(k.Look==2)
                {
                    // The cracked crown: a jagged split of light over the brow (NativeHordeModels.Crack is its centre).
                    var g=k.Seam(1.4f);k.Box(g,-.03f,.245f,.142f,.016f,.07f,.01f,0,0,0,.4f);k.Box(g,-.015f,.29f,.12f,.014f,.05f,.04f,0,.6f,0,-.3f);k.Box(g,-.035f,.31f,.04f,.016f,.012f,.12f,0,0,.3f,0);
                    if(k.Mid)k.Box(k.Seam(1.1f),.05f,.27f,.14f,.012f,.05f,.01f,0,0,0,-.5f);
                }
            }
        }

        // ================================================================================================================
        // Runner: thin and pitched forward (the pose leans it), a narrow skull thrust out on a long neck, a ridge of hot pink
        // fins down its back, long blade claws and two couture tails that stream behind it as it sprints.
        // ================================================================================================================
        private static void Runner(K k)
        {
            var plate=Sheen;
            k.On(CapturedRig.Pelvis);k.Span(Ink,-.16f,.16f,-.08f,.09f,-.11f,.12f,.025f);
            // The coat tails: two long torn ribbons off the back of the hips (rigid on the pelvis; the gait's lean streams them).
            foreach(int s in new[]{-1,1})
            {
                if(k.Far){k.Box(Cloth,s*.07f,-.15f,-.15f,.07f,.26f,.03f,0,-.35f);continue;}
                k.Box(Cloth,s*.07f,-.05f,-.13f,.075f,.14f,.02f,.006f,-.25f);k.Box(Cloth,s*.075f,-.17f,-.165f,.065f,.13f,.018f,.004f,-.4f);
                if(k.Mid)k.Spike(Cloth,s*.078f,-.23f,-.2f,.06f,.016f,new Vector3(0,-.1f,-.05f));
                if(k.Fine)k.Stitch(new Vector3(s*.07f,.02f,-.142f),new Vector3(s*.072f,-.22f,-.19f),new Vector3(0,.3f,-1),.008f,3,.9f);
            }
            k.On(CapturedRig.Spine);k.Span(Ink,-.13f,.13f,-.01f,.17f,-.1f,.11f,.03f);
            k.On(CapturedRig.Chest);k.Span(Ink,-.19f,.19f,-.03f,.25f,-.13f,.13f,.04f);k.Span(plate,-.15f,.15f,.03f,.22f,.09f,.142f,.015f);
            // Ribs stitched in pink and the back's fin ridge.
            if(k.Mid)for(int i=0;i<3;i++){float y=.05f+i*.06f;k.Stitch(new Vector3(-.13f,y,.142f),new Vector3(.13f,y-.015f,.142f),Vector3.UnitZ,.007f,4,.8f);}
            for(int i=0;i<(k.Far?2:4);i++){float y=.22f-i*.075f;k.Spike(k.Seam(1.2f),0,y,-.13f,.03f,.04f,new Vector3(0,.035f,-.11f+.01f*i));}
            k.On(CapturedRig.Spine);for(int i=0;i<(k.Far?1:2);i++)k.Spike(k.Seam(1.1f),0,.13f-i*.07f,-.1f,.026f,.035f,new Vector3(0,.03f,-.09f));
            foreach(bool m in new[]{false,true}){k.On(CapturedRig.LClav,m);k.Box(Ink,-.05f,0,0,.12f,.09f,.14f,.02f);if(k.Mid)k.Spike(plate,-.08f,.04f,-.02f,.06f,.06f,new Vector3(-.04f,.07f,-.04f));}
            // Long arms ending in three blade claws.
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LArm,m);k.Limb(Ink,-.01f,-.27f,.05f,.042f);
                k.On(CapturedRig.LElbow,m);k.Limb(Ink,.02f,-.24f,.045f,.035f);if(k.Mid)k.Box(plate,0,-.1f,.03f,.07f,.14f,.03f,.008f);
                k.On(CapturedRig.LHand,m);k.Box(Ink,0,-.03f,.01f,.08f,.07f,.08f,.012f);
                for(int i=0;i<(k.Far?1:3);i++){float x=(i-1)*.026f;k.Spike(i==1?k.Seam(.9f):Bone,x,-.06f,.03f,.018f,.012f,new Vector3(x*.4f,-.2f,.06f));}
            }
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LHip,m);k.Limb(Ink,.02f,-.25f,.058f,.045f);
                // A shin swept back (a hound's hock read on a biped) and a hooked foot.
                k.On(CapturedRig.LKnee,m);k.Limb(Ink,.02f,-.24f,.05f,.034f,0,0,1,-.05f);if(k.Mid)k.Spike(plate,0,-.03f,-.04f,.05f,.04f,new Vector3(0,-.02f,-.09f));
                k.On(CapturedRig.LFoot,m);k.Box(Ink,0,-.03f,0,.08f,.05f,.12f,.01f);
                k.On(CapturedRig.LToe,m);k.Spike(Ink,0,-.02f,.02f,.07f,.045f,new Vector3(0,-.015f,.1f));
            }
            Neck(k,.05f,.12f);
            k.On(CapturedRig.Head);
            // A narrow skull pushed forward, a snout of a grin under deep brow plates, back-swept horns.
            k.Span(Ink,-.11f,.11f,.0f,.24f,-.1f,.17f,.05f);k.Span(plate,-.115f,.115f,.17f,.24f,.0f,.18f,.025f);
            k.Span(Ink,-.08f,.08f,-.02f,.08f,.08f,.22f,.02f);
            k.Grin(0,.045f,.222f,.072f,.02f,.04f,9);k.Eyes(0,.13f,.18f,.1f,.05f,.03f,.35f);
            if(k.Look==1){for(int s=-1;s<=1;s+=2)k.Spike(plate,s*.08f,.22f,-.03f,.04f,.05f,new Vector3(s*.05f,.12f,-.14f));}
            else{k.Spike(k.Seam(1f),0,.24f,-.02f,.04f,.08f,new Vector3(0,.04f,-.2f));}
        }

        // ================================================================================================================
        // Brute: a shoulder-hunched mass. A corseted trunk laced in ember glow, a hump of couture pauldrons, forearms and fists
        // plated like gauntlets, a small head sunk under the shoulders with a wide grin; thick short legs.
        // ================================================================================================================
        private static void Brute(K k)
        {
            var plate=Sheen;
            k.On(CapturedRig.Pelvis);k.Span(Ink,-.21f,.21f,-.11f,.11f,-.15f,.17f,.035f);k.Span(plate,-.22f,.22f,-.02f,.06f,-.16f,.18f,.015f);
            if(k.Mid)k.Hem(-.04f,.22f,.17f,10,.17f,.22f);
            k.On(CapturedRig.Spine);k.Span(Ink,-.2f,.2f,-.02f,.18f,-.16f,.18f,.04f);
            // The corset: ember lacing crossing the belly.
            if(k.Mid)for(int i=0;i<3;i++){float y=.02f+i*.05f;k.Stitch(new Vector3(-.07f,y,.181f),new Vector3(.07f,y+.04f,.181f),Vector3.UnitZ,.009f,0,1.1f);k.Stitch(new Vector3(.07f,y,.181f),new Vector3(-.07f,y+.04f,.181f),Vector3.UnitZ,.009f,0,1.1f);}
            k.On(CapturedRig.Chest);
            // A chest that widens to great shoulders, lats sloping in to the waist, and a hump rising behind the head.
            k.Span(Ink,-.24f,.24f,-.05f,.2f,-.18f,.19f,.05f);k.Box(Ink,0,.24f,-.02f,.6f,.16f,.4f,.06f);
            foreach(int s in new[]{-1,1})k.Box(Ink,s*.2f,.08f,-.02f,.14f,.28f,.34f,.04f,0,0,s*-.3f);
            k.Span(plate,-.25f,.25f,.08f,.28f,.16f,.215f,.025f);
            k.Blob(Ink,0,.34f,-.1f,.25f,.13f,.15f);k.Span(plate,-.21f,.21f,.24f,.4f,-.24f,-.12f,.04f);
            for(int i=-1;i<=1;i++)k.Spike(i==0?k.Seam(1.2f):Ink,i*.1f,.4f-.03f*Math.Abs(i),-.17f,.07f,.06f,new Vector3(i*.03f,.11f,-.07f));
            k.Stitch(new Vector3(-.21f,.21f,.216f),new Vector3(.21f,.21f,.216f),Vector3.UnitZ,.011f,6,1.2f);
            foreach(bool m in new[]{false,true})
            {
                // Pauldrons: three flared couture shoulder plates over a heavy cap, the rims lit.
                k.On(CapturedRig.LClav,m);k.Box(Ink,-.06f,0,0,.18f,.17f,.25f,.04f);
                k.Box(plate,-.075f,.085f,0,.21f,.07f,.29f,.02f,0,0,.28f);k.Box(plate,-.11f,.04f,0,.15f,.06f,.28f,.014f,0,0,.55f);if(k.Mid)k.Box(plate,-.13f,-.01f,0,.09f,.05f,.26f,.01f,0,0,.85f);
                if(k.Mid)k.Box(k.Seam(1f),-.155f,.005f,0,.012f,.03f,.26f,0,0,0,.6f);
                // Arms: thick upper arms, gauntlet forearms and great plated fists.
                k.On(CapturedRig.LArm,m);k.Limb(Ink,-.0f,-.27f,.088f,.078f);
                k.On(CapturedRig.LElbow,m);k.Limb(Ink,.02f,-.22f,.09f,.105f);k.Box(plate,0,-.14f,0,.21f,.16f,.21f,.03f);if(k.Mid){for(int i=0;i<2;i++)k.Box(k.Seam(.9f),-.106f,-.1f-.06f*i,0,.008f,.014f,.17f);k.Spike(plate,-.06f,-.05f,-.06f,.05f,.05f,new Vector3(-.03f,.08f,-.06f));}
                k.On(CapturedRig.LHand,m);k.Box(Ink,0,-.06f,.01f,.16f,.15f,.16f,.04f);k.Box(plate,0,-.1f,.07f,.16f,.06f,.06f,.015f);
                if(k.Mid)for(int i=0;i<3;i++)k.Spike(Bone,(i-1)*.048f,-.11f,.1f,.03f,.03f,new Vector3(0,-.01f,.045f));
                k.On(CapturedRig.LHip,m);k.Limb(Ink,.02f,-.25f,.1f,.085f);
                k.On(CapturedRig.LKnee,m);k.Limb(Ink,.02f,-.24f,.085f,.075f);if(k.Mid)k.Box(plate,0,-.05f,.075f,.13f,.12f,.05f,.012f);
                k.On(CapturedRig.LFoot,m);k.Box(Ink,0,-.035f,.0f,.15f,.07f,.17f,.02f);
                k.On(CapturedRig.LToe,m);k.Box(Ink,0,-.015f,.035f,.14f,.05f,.1f,.015f);
            }
            Neck(k,.09f,.06f);
            // A small head, low and forward under the hump: a heavy brow, the wide grin, ember eye slits.
            k.On(CapturedRig.Head);k.Span(Ink,-.12f,.12f,-.03f,.19f,-.11f,.13f,.05f);k.Span(plate,-.135f,.135f,.12f,.2f,-.08f,.15f,.025f);
            k.Grin(0,.035f,.133f,.105f,.035f,.05f,11);
            if(k.Far)k.Box(k.Seam(1.6f),0,.1f,.134f,.16f,.02f,.012f);else foreach(int s in new[]{-1,1})k.Box(k.Seam(1.7f),s*.052f,.1f,.134f,.06f,.016f,.01f,0,0,0,s*.25f);
            if(k.Look==1){for(int s=-1;s<=1;s+=2)k.Spike(plate,s*.1f,.16f,.0f,.05f,.06f,new Vector3(s*.1f,.06f,.06f));}
        }

        // ================================================================================================================
        // Spitter: lanky and hunched over the sac that swells in its throat, lit acid green; a split jaw under the grin, glowing
        // pustules down its back, a ragged shawl; thin two-clawed arms.
        // ================================================================================================================
        private static void Spitter(K k)
        {
            var plate=Sheen;
            k.On(CapturedRig.Pelvis);k.Span(Ink,-.17f,.17f,-.09f,.09f,-.12f,.13f,.025f);k.Hem(-.03f,.18f,.14f,9,.24f,.1f);
            k.On(CapturedRig.Spine);k.Span(Ink,-.15f,.15f,-.01f,.17f,-.12f,.12f,.03f);
            k.On(CapturedRig.Chest);k.Span(Ink,-.2f,.2f,-.03f,.26f,-.14f,.14f,.04f);
            // The shawl over the shoulders and back.
            k.Span(Cloth,-.23f,.23f,.17f,.29f,-.17f,.06f,.03f);if(k.Mid)k.Hem(.2f,.22f,.15f,8,.17f,.25f,true,-.02f);
            // Pustules: glowing blisters down the back.
            for(int i=0;i<(k.Far?1:k.Mid?5:3);i++){float x=(i%2==0?-1:1)*(.05f+.03f*(i%3)),y=.06f+i*.045f;k.Blob(k.Seam(1.3f),x,y,-.145f,.035f,.035f,.03f);}
            // The throat sac (NativeHordeFx swells it in the wind-up): a glowing bulb under the jaw, veined.
            k.On(CapturedRig.Neck);k.Limb(Ink,-.04f,.08f,.055f,.05f);
            k.Blob(k.Seam(1.5f),0,.0f,.075f,.085f,.08f,.075f);if(k.Mid){k.Blob(Ink,0,.03f,.04f,.07f,.06f,.06f);k.Stitch(new Vector3(-.06f,-.03f,.13f),new Vector3(.05f,.05f,.13f),new Vector3(0,0,1),.006f,0,.5f);}
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);k.Box(Ink,-.05f,0,0,.13f,.1f,.15f,.02f);
                k.On(CapturedRig.LArm,m);k.Limb(Ink,-.01f,-.27f,.048f,.04f);
                k.On(CapturedRig.LElbow,m);k.Limb(Ink,.02f,-.24f,.042f,.034f);
                Hand(k,m,.075f,2,.09f,.5f);
                k.On(CapturedRig.LHip,m);k.Limb(Ink,.02f,-.25f,.06f,.048f);
                k.On(CapturedRig.LKnee,m);k.Limb(Ink,.02f,-.24f,.05f,.038f);
                k.On(CapturedRig.LFoot,m);k.Box(Ink,0,-.035f,0,.09f,.055f,.13f,.01f);
                k.On(CapturedRig.LToe,m);k.Box(Ink,0,-.012f,.035f,.085f,.04f,.1f,.008f);if(k.Mid)for(int i=-1;i<=1;i+=2)k.Spike(Bone,i*.025f,-.02f,.08f,.016f,.016f,new Vector3(0,-.015f,.05f));
            }
            k.On(CapturedRig.Head);
            // A long skull, the grin split down its length and the lower jaw hung open over the sac.
            k.Span(Ink,-.12f,.12f,.04f,.28f,-.13f,.14f,.05f);k.Span(plate,-.125f,.125f,.2f,.29f,-.1f,.12f,.03f);
            k.Span(Ink,-.09f,.09f,-.05f,.03f,-.02f,.15f,.02f);
            k.Grin(0,.085f,.143f,.09f,.022f,.06f,10);k.Eyes(0,.19f,.142f,.11f,.055f,.04f,.3f);
            k.Box(k.Seam(1.2f),0,.0f,.152f,.1f,.012f,.01f);
            if(k.Look==1){k.Collar(.2f,.13f,.12f,7,1.6f,.5f,.018f,-.02f);}
        }

        // ================================================================================================================
        // Crawler: low on four limbs (the pose pitches the trunk forward); long arms with heavy paws as forelegs, a spine of white
        // glow, the head thrust forward on a short neck with the widest grin of the horde.
        // ================================================================================================================
        private static void Crawler(K k)
        {
            var plate=Sheen;
            k.On(CapturedRig.Pelvis);k.Span(Ink,-.15f,.15f,-.08f,.08f,-.11f,.11f,.025f);
            k.On(CapturedRig.Spine);k.Span(Ink,-.14f,.14f,-.01f,.17f,-.11f,.11f,.03f);
            k.On(CapturedRig.Chest);k.Span(Ink,-.18f,.18f,-.04f,.25f,-.13f,.13f,.04f);k.Span(plate,-.15f,.15f,.0f,.24f,.11f,.14f,.015f);
            // The spine: a row of glowing vertebra plates along the back (the trunk is horizontal in the pose: they face up).
            for(int i=0;i<(k.Far?2:5);i++)k.Box(k.Seam(1.3f),0,.02f+i*.05f,-.135f,.05f,.02f,.03f,.004f);
            k.On(CapturedRig.Spine);for(int i=0;i<(k.Far?1:3);i++)k.Box(k.Seam(1.1f),0,.02f+i*.06f,-.115f,.045f,.018f,.025f,.003f);
            k.On(CapturedRig.Pelvis);if(k.Mid)k.Spike(Ink,0,.0f,-.11f,.08f,.05f,new Vector3(0,-.06f,-.18f));
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);k.Box(Ink,-.05f,0,0,.13f,.1f,.15f,.02f);
                // Forelegs: long arms, thick forearms and splayed paws with hooked claws.
                k.On(CapturedRig.LArm,m);k.Limb(Ink,-.01f,-.27f,.055f,.05f);
                k.On(CapturedRig.LElbow,m);k.Limb(Ink,.02f,-.24f,.06f,.05f);if(k.Mid)k.Spike(plate,0,-.02f,-.05f,.05f,.04f,new Vector3(0,.04f,-.08f));
                k.On(CapturedRig.LHand,m);k.Box(Ink,0,-.035f,.02f,.1f,.06f,.12f,.012f);
                for(int i=0;i<(k.Far?1:4);i++){float x=(i-1.5f)*.025f;k.Spike(Bone,x,-.06f,.07f,.018f,.016f,new Vector3(0,-.05f,.06f));}
                k.On(CapturedRig.LHip,m);k.Limb(Ink,.02f,-.25f,.06f,.05f);
                k.On(CapturedRig.LKnee,m);k.Limb(Ink,.02f,-.24f,.05f,.04f);
                k.On(CapturedRig.LFoot,m);k.Box(Ink,0,-.03f,0,.08f,.05f,.13f,.01f);
                k.On(CapturedRig.LToe,m);k.Box(Ink,0,-.012f,.035f,.075f,.04f,.1f,.008f);
            }
            Neck(k,.055f,.08f);
            k.On(CapturedRig.Head);
            // The head (the pose lifts it to look ahead): a broad flat skull and the widest grin.
            k.Span(Ink,-.13f,.13f,.0f,.22f,-.11f,.15f,.05f);k.Span(plate,-.135f,.135f,.15f,.23f,-.08f,.16f,.025f);
            k.Grin(0,.07f,.153f,.115f,.035f,.06f,12);k.Eyes(0,.165f,.152f,.13f,.06f,.035f,.35f);
            if(k.Look==1)for(int s=-1;s<=1;s+=2)k.Spike(plate,s*.1f,.2f,-.02f,.04f,.05f,new Vector3(s*.04f,.02f,-.16f));
        }

        // ================================================================================================================
        // Bloater: a swollen trunk laced shut over a belly of amber light about to burst, a small head with the grin on top,
        // stubby arms and legs; glowing vents swell on its gut.
        // ================================================================================================================
        private static void Bloater(K k)
        {
            var plate=Sheen;
            k.On(CapturedRig.Pelvis);k.Span(Ink,-.2f,.2f,-.1f,.08f,-.14f,.16f,.03f);
            // The belly: one great ellipsoid on the spine, stitched shut in glowing amber that strains as it swells.
            k.On(CapturedRig.Spine);k.Blob(Ink,0,.07f,.06f,.34f,.28f,.32f);
            if(!k.Far)
            {
                for(int i=-2;i<=2;i++){double a=i*.42;float x=(float)Math.Sin(a)*.33f,z=(float)Math.Cos(a)*.31f+.06f;k.Stitch(new Vector3(x*.86f,-.12f,z*.86f+.008f),new Vector3(x,.12f,z+.01f),new Vector3(x,0,z-.06f),.012f,3,1.25f);}
                if(k.Mid)for(int i=0;i<3;i++){double a=-.7+.7*i;float x=(float)Math.Sin(a)*.3f,z=(float)Math.Cos(a)*.28f+.06f;k.Blob(k.Seam(1.6f),x,.2f-.05f*i,z,.035f,.035f,.03f);}
            }
            else k.Box(k.Seam(1.4f),0,.07f,.36f,.3f,.2f,.01f);
            k.On(CapturedRig.Chest);k.Span(Ink,-.22f,.22f,-.02f,.22f,-.17f,.17f,.05f);k.Span(plate,-.18f,.18f,.12f,.24f,-.15f,.1f,.03f);
            if(k.Look==0)k.Collar(.2f,.15f,.12f,9,1.4f,.45f);
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);k.Box(Ink,-.05f,0,0,.14f,.12f,.17f,.025f);
                k.On(CapturedRig.LArm,m);k.Limb(Ink,-.01f,-.22f,.065f,.055f);
                k.On(CapturedRig.LElbow,m);k.Limb(Ink,.02f,-.2f,.055f,.05f);
                Hand(k,m,.09f,3,.05f,.3f);
                k.On(CapturedRig.LHip,m);k.Limb(Ink,.02f,-.25f,.085f,.07f);
                k.On(CapturedRig.LKnee,m);k.Limb(Ink,.02f,-.24f,.07f,.06f);
                k.On(CapturedRig.LFoot,m);k.Box(Ink,0,-.035f,0,.13f,.06f,.15f,.015f);
                k.On(CapturedRig.LToe,m);k.Box(Ink,0,-.015f,.03f,.12f,.045f,.09f,.012f);
            }
            Neck(k,.07f,.04f);
            k.On(CapturedRig.Head);k.Span(Ink,-.12f,.12f,-.02f,.2f,-.11f,.12f,.05f);k.Span(plate,-.125f,.125f,.13f,.21f,-.09f,.1f,.025f);
            k.Grin(0,.05f,.123f,.09f,.03f,.05f,10);k.Eyes(0,.13f,.122f,.1f,.05f,.035f,.3f);
            if(k.Look==1)k.Spike(k.Seam(1.2f),0,.2f,0,.05f,.05f,new Vector3(0,.09f,-.02f));
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

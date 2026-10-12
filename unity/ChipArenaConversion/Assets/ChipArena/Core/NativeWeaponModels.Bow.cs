using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // The Bow (owner 2026-10-12: "For ranged i want to add a "Bow" class"): a Standard recurve sculpted in code (original
    // geometry) and a simpler Prototype Series build in the old blocky weapon style. Both ride the left hand (the bow hand) in
    // the bow frame of Core/NativeBow.cs (+x the bow's left, +y up the riser, +z downrange): the fist round the grip, the arrow
    // line beside the riser at (ArrowX, ArrowY), the string at rest at BraceZ between the limb tips. The registry's model is
    // the whole bow at rest (every static consumer: the Locker stage, portraits, entrances, drops, ghosts); the live rig draws
    // the riser, each limb (flexed about its pivot) and the string to the nock as separate parts (NativeBowModels.Part) so the
    // string bends and the limbs load with the draw. Presentation only: no mesh defines a rule.
    public static partial class NativeWeaponModels
    {
        static partial void Bows(Dictionary<string,Design> d)
        {
            d[NativeBowWire.Spec]=new Design{Spec=NativeBowWire.Spec,Build=k=>NativeBowModels.Standard(k,NativeBowModels.Held),Bone=LeftHand,Muzzle=NativeBowModels.Rest};
        }
    }

    /// <summary>The bow's models (both skins), its parts, the arrow, the quiver and the string, and its sockets.</summary>
    public static class NativeBowModels
    {
        // ---- parts (bit mask) ----
        public const int Riser=1,UpperLimb=2,LowerLimb=4,StringPart=8,Limbs=UpperLimb|LowerLimb,Held=Riser|Limbs|StringPart,Body=Riser;
        // ---- sockets (bow frame) ----
        /// <summary>The arrow rest on the shelf (the muzzle-equivalent: the arrow leaves the bow here).</summary>
        public static readonly Vector3 Rest=new Vector3(NativeBow.ArrowX,NativeBow.ArrowY,NativeBow.RiserZ+.032f);
        /// <summary>The chip modules' 'muzzle' depth on the arrow line (NativeWeaponDressing.MuzzleZ): just ahead of the riser.</summary>
        public const float ModuleTip=.2f;
        /// <summary>The modules' frame offset onto the arrow line (they are laid out about the bow's axis, the gun way).</summary>
        public static readonly Vector3 ModuleOffset=new Vector3(NativeBow.ArrowX,NativeBow.ArrowY,0);
        /// <summary>The limbs' pivots (the limb pockets) at rest.</summary>
        public static Vector3 Pivot(bool top)=>new Vector3(0,top?NativeBow.PivotY:-NativeBow.PivotY,NativeBow.PivotZ);
        /// <summary>The quiver's mouth in chest space (the draw hand reaches here to pull an arrow: over the right shoulder).</summary>
        public static readonly Vector3 QuiverMouth=new Vector3(-.19f,.4f,-.215f),QuiverBase=new Vector3(.07f,-.04f,-.215f);
        /// <summary>Where the fingers hold the string in the draw hand's frame (+z along the arrow, +y up, the fist hooked round).</summary>
        public static readonly Vector3 Hook=new Vector3(.0f,-.07f,.075f);

        // ---- palette ----
        private const int Carbon=0x1d272c,CarbonLight=0x2b3a42,Machined=0x6c8088,MachinedLight=0x9db0b6,Wrap=0x2c2220,WrapLight=0x3b2d29,StringHex=0xd9e4e1;
        private static WeaponPaint P(int hex,float sheen=0)=>WeaponPaint.Solid(hex,sheen);
        private static readonly WeaponPaint Accent=WeaponPaint.Accent(1.8f,.4f),AccentSoft=WeaponPaint.Accent(1.1f,.32f);
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);

        /// <summary>The upper limb's centre line at rest (the lower one mirrors it in y): from the pocket, sweeping back toward the
        /// string, then the recurve curling forward past the string anchor at (ArrowX, TipY, TipZ).</summary>
        public static readonly Vector3[] LimbPath=Scale(new[]{V(0,.232f,.03f),V(0,.27f,.024f),V(0,.32f,.006f),V(0,.37f,-.022f),V(0,.42f,-.058f),V(0,.47f,-.094f),V(0,.508f,-.116f),V(0,.538f,-.126f),V(0,.558f,-.123f),V(0,.574f,-.111f),V(0,.584f,-.095f),V(0,.588f,-.079f)});
        // The limb drawn at 1.17 of its sketch length from the pocket (a longer bow for the voxel pilot's scale).
        private static Vector3[] Scale(Vector3[] p){for(int i=0;i<p.Length;i++)p[i].Y=.232f+(p[i].Y-.232f)*1.17f;return p;}
        private static float LimbHalf(int i,int n){float t=i/(float)(n-1);return .0068f-.0036f*t;}
        private static int Steps(WeaponSculpt k)=>k.Fine?LimbPath.Length:k.Mid?8:5;
        private static List<Vector3> LimbLine(WeaponSculpt k)
        {
            int n=Steps(k);var o=new List<Vector3>(n);
            for(int i=0;i<n;i++){float u=i*(LimbPath.Length-1f)/(n-1);int a=Math.Min(LimbPath.Length-2,(int)u);o.Add(Vector3.Lerp(LimbPath[a],LimbPath[a+1],u-a));}
            return o;
        }
        private static Vector3[] Ccw(Vector3[] o)
        {
            float area=0;for(int i=0;i<o.Length;i++){var a=o[i];var b=o[(i+1)%o.Length];area+=a.X*b.Y-b.X*a.Y;}
            if(area<0)Array.Reverse(o);return o;
        }
        // An outline in the bow's side view (z, y pairs) as a slab outline: the slab's local x is -z (Ry(pi/2) below).
        private static Vector3[] Side(params float[] zy){var o=new Vector3[zy.Length/2];for(int i=0;i<o.Length;i++)o[i]=new Vector3(-zy[i*2],zy[i*2+1],0);return Ccw(o);}
        // A slab cut from a side-view outline, x0..x1 thick (the Slab extrudes along its z: here the bow's x).
        private static void SideSlab(WeaponSculpt k,WeaponPaint side,WeaponPaint face,Vector3[] outline,float x0,float x1,float bevel)
        {k.Push(Ry(Pi/2));k.Slab(_=>side,face,face,outline,x0,x1,bevel);k.Pop();}
        private const float Pi=(float)Math.PI;

        // ---- the Standard recurve ----
        /// <summary>
        /// The Standard bow, the parts in `parts`: a machined riser (limb pockets with bolts, a palm-swell grip in a cord wrap,
        /// the arrow shelf and rest, a sight window cut past centre, accent inlays and a circuit emblem, a short front stabilizer
        /// with a lit weight), carbon limbs with an accent strip down each belly and pale recurve tips, and the string with its
        /// centre serving and a glowing nocking point.
        /// </summary>
        public static void Standard(WeaponSculpt k,int parts)
        {
            if((parts&Riser)!=0)StandardRiser(k);
            if((parts&UpperLimb)!=0)StandardLimb(k,true);
            if((parts&LowerLimb)!=0)StandardLimb(k,false);
            if((parts&StringPart)!=0)RestString(k,true);
        }
        private static void StandardRiser(WeaponSculpt k)
        {
            var metal=P(Machined,.08f);var light=P(MachinedLight,.12f);var dark=P(Carbon,.03f);var wrap=P(Wrap);var cord=P(WrapLight);var steel=P(0x8fa6ac,.1f);
            float zc=NativeBow.RiserZ,b=.0035f;
            // Lower riser and pocket (y -.25 .. .02), the palm swell bulging toward the string at the grip.
            SideSlab(k,metal,light,Side(zc-.02f,-.25f,zc+.026f,-.25f,zc+.03f,-.2f,zc+.024f,-.13f,zc+.012f,-.11f,zc+.018f,-.005f,zc+.04f,.004f,zc+.042f,.02f,zc-.016f,.02f,zc-.028f,-.004f,zc-.044f,-.06f,zc-.032f,-.115f,zc-.016f,-.14f,zc-.026f,-.2f),-.014f,.014f,b);
            // The sight-window section (y .02 .. .16), cut toward the bow's right so the arrow line passes the centre.
            SideSlab(k,metal,light,Side(zc-.012f,.02f,zc+.03f,.02f,zc+.026f,.09f,zc+.03f,.16f,zc-.01f,.16f,zc-.004f,.09f),-.016f,.0f,b);
            // Upper riser and pocket (y .16 .. .25).
            SideSlab(k,metal,light,Side(zc-.012f,.16f,zc+.03f,.16f,zc+.032f,.205f,zc+.026f,.25f,zc-.02f,.25f,zc-.022f,.205f),-.014f,.014f,b);
            // Limb pockets: dark caps over the riser ends, with a bolt through each (accent ring at full detail).
            for(int s=-1;s<=1;s+=2)
            {
                k.Box(dark,V(0,s*.236f,zc-.0f),V(.034f,.03f,.05f),.004f);
                k.Push(Ry(Pi/2)*T(-.0185f,s*.236f,zc+.002f));k.Cylinder(steel,.0075f,0,.037f,k.Sides(8),.0015f);k.Pop();
                if(k.Mid){k.Push(Ry(Pi/2)*T(.0185f,s*.236f,zc+.002f));k.Torus(Accent,V(0,0,.0005f),.0076f,.0012f,k.Sides(12),3);k.Pop();}
            }
            // The grip: a cord-wrapped palm swell round the fist (y -.115 .. -.005), bands at full detail.
            k.Box(wrap,V(0,-.06f,zc-.006f),V(.034f,.104f,.06f),.012f);
            if(k.Mid){int bands=k.Fine?7:4;for(int i=0;i<bands;i++){float y=-.105f+i*(.09f/(bands-1));k.Box(cord,V(0,y,zc-.006f),V(.036f,.0045f,.062f),.0018f);}}
            // The shelf and arrow rest on the bow's left, the plunger button behind it.
            k.Box(light,V(.006f,.012f,zc+.012f),V(.026f,.007f,.05f),.002f);
            k.Box(steel,V(NativeBow.ArrowX+.002f,NativeBow.ArrowY-.006f,zc+.03f),V(.009f,.0035f,.02f),.001f);
            if(k.Mid){k.Push(Ry(Pi/2)*T(.001f,NativeBow.ArrowY,zc+.02f));k.Cylinder(steel,.004f,0,.008f,k.Sides(8),.001f);k.Pop();}
            // Accent inlays down both faces of the riser and a circuit emblem on the upper face; a lit window edge.
            for(int s=-1;s<=1;s+=2)
            {
                k.Box(Accent,V(s*.0142f,-.17f,zc+.012f),V(.0014f,.07f,.004f),0);
                k.Box(Accent,V(s*.0142f,.2f,zc+.012f),V(.0014f,.05f,.004f),0);
            }
            k.Box(AccentSoft,V(-.016f,.09f,zc+.028f),V(.0016f,.13f,.0032f),0);
            if(k.Mid)
            {
                k.Decal(Accent,V(.0145f,.13f,zc+.006f),V(0,.012f,0),V(0,0,.006f),.0011f);k.Decal(Accent,V(.0145f,.118f,zc+.014f),V(0,.004f,0),V(0,0,.004f),.0011f);
                k.Decal(AccentSoft,V(-.0145f,-.2f,zc+.004f),V(0,.018f,0),V(0,0,.004f),.0011f);
            }
            // The front stabilizer: a short rod under the grip, its weight lit at the end.
            k.Cylinder(dark,.0055f,zc+.025f,zc+.2f,k.Sides(8),0,0,-.135f);
            k.Push(T(0,-.135f,0));k.Lathe(i=>i==1?Accent:metal,new[]{.012f,zc+.2f,.013f,zc+.206f,.013f,zc+.226f,.011f,zc+.236f},k.Sides(10),1,0,true);k.Pop();
            k.Box(metal,V(0,-.135f,zc+.02f),V(.016f,.018f,.016f),.003f);
        }
        private static void StandardLimb(WeaponSculpt k,bool top)
        {
            var carbon=P(Carbon,.06f);var tip=P(0xcfdcdc,.18f);var dark=P(0x121a1e);
            if(!top)k.Push(S(1,-1,1));
            var line=LimbLine(k);int n=line.Count;
            k.Tube(i=>i>=n-2?tip:carbon,line,i=>LimbHalf(i,n),k.Sides(8),Vector3.UnitX,1,true,false,2.8f);
            // The accent strip down the belly (the string side) and a pale stripe up the back at full detail.
            if(k.Mid)
            {
                var strip=new List<Vector3>();var radii=new List<float>();
                for(int i=1;i<n-2;i++){var a=line[Math.Max(0,i-1)];var c=line[Math.Min(n-1,i+1)];var t=Vector3.Normalize(c-a);var bi=Vector3.Cross(t,Vector3.UnitX);strip.Add(line[i]-bi*(LimbHalf(i,n)*.95f));radii.Add(LimbHalf(i,n)*.32f);}
                k.Tube(_=>Accent,strip,i=>radii[i],4,Vector3.UnitX,0,true,false,3.6f);
            }
            // The string nock groove and the tip cap.
            var end=line[n-1];k.Box(dark,end+V(0,-.006f,-.004f),V(.016f,.012f,.01f),.002f,-.9f);
            k.Box(tip,V(0,NativeBow.TipY,NativeBow.TipZ+.006f),V(.013f,.006f,.008f),.0015f);
            if(!top)k.Pop();
        }
        /// <summary>The string at rest (straight between the tip anchors), its serving and nocking point.</summary>
        private static void RestString(WeaponSculpt k,bool standard)
        {
            var a=NativeBow.Anchor(true,0);var c=NativeBow.Anchor(false,0);var s=P(StringHex,.15f);
            k.Tube(_=>s,new[]{a,c},_=>.0017f,k.Sides(standard?6:4),Vector3.UnitZ,0,true);
            if(standard&&k.Mid){k.Tube(_=>P(0x46545a,.05f),new[]{V(a.X,NativeBow.ArrowY-.06f,a.Z),V(a.X,NativeBow.ArrowY+.08f,a.Z)},_=>.0026f,k.Sides(6),Vector3.UnitZ,0,true);}
            k.Box(standard?Accent:AccentSoft,NativeBow.NockAt(0)+V(0,.006f,0),V(.006f,.004f,.006f),0);
        }

        // ---- the Prototype Series bow (the old weapon style: boxes, flat steel and guard tones, one dim accent) ----
        public static void Prototype(WeaponSculpt k,int parts)
        {
            var guard=P(0x5d7079);var steel=P(0x8fa6ac);var dark=P(0x263a43);var wrap=P(0x3b2a24);var edge=P(0xeaf6f4);float zc=NativeBow.RiserZ;
            if((parts&Riser)!=0)
            {
                k.Box(guard,V(0,-.13f,zc+.002f),V(.028f,.22f,.05f));k.Box(guard,V(-.004f,.09f,zc+.006f),V(.02f,.14f,.044f));k.Box(guard,V(0,.205f,zc+.002f),V(.028f,.09f,.05f));
                k.Box(wrap,V(0,-.06f,zc-.008f),V(.036f,.11f,.062f));k.Box(steel,V(.006f,.012f,zc+.014f),V(.026f,.008f,.05f));
                k.Box(WeaponPaint.Accent(.9f,.3f),V(.0145f,.16f,zc+.012f),V(.002f,.08f,.006f));
                for(int s=-1;s<=1;s+=2)k.Box(dark,V(0,s*.236f,zc),V(.034f,.03f,.052f));
            }
            for(int limb=0;limb<2;limb++)
            {
                bool top=limb==0;if((parts&(top?UpperLimb:LowerLimb))==0)continue;if(!top)k.Push(S(1,-1,1));
                int[] keys={0,3,6,9,11};
                for(int i=0;i+1<keys.Length;i++)
                {
                    var a=LimbPath[keys[i]];var c=LimbPath[keys[i+1]];var mid=(a+c)*.5f;var d=c-a;float len=d.Length(),ang=(float)Math.Atan2(d.Z,d.Y);
                    k.Box(i==keys.Length-2?steel:dark,mid,V(.03f-.004f*i,len+.006f,.013f-.0015f*i),0,-ang);
                }
                k.Box(steel,V(0,NativeBow.TipY,NativeBow.TipZ+.004f),V(.014f,.012f,.012f));
                if(!top)k.Pop();
            }
            if((parts&StringPart)!=0){var a=NativeBow.Anchor(true,0);var c=NativeBow.Anchor(false,0);k.Box(edge,(a+c)*.5f,V(.003f,(a-c).Length(),.003f));}
        }

        // ---- the arrow (model frame: the nock at the origin, +z to the head at ArrowLength) ----
        /// <summary>
        /// The Standard arrow: a carbon shaft with an accent cresting band, three vanes (one in the pilot's accent), a glowing
        /// nock and a three-blade broadhead with lit edges; the Prototype arrow is three boxes. Tier thins the sides and drops the
        /// cresting and blade edges.
        /// </summary>
        public static void Arrow(WeaponSculpt k,bool standard)
        {
            float L=NativeBow.ArrowLength,h=NativeBow.ArrowHead;
            if(!standard)
            {
                k.Box(P(0x5d7079),V(0,0,(L-h)*.5f),V(.008f,.008f,L-h));k.Box(P(0x8fa6ac),V(0,0,L-h*.5f),V(.018f,.018f,h),0,0,0,Pi/4);
                k.Box(P(0xeaf6f4),V(0,.009f,.07f),V(.002f,.014f,.08f));k.Box(P(0xeaf6f4),V(0,-.009f,.07f),V(.002f,.014f,.08f));k.Box(WeaponPaint.Accent(.9f,.3f),V(0,0,-.004f),V(.009f,.009f,.01f));
                return;
            }
            var carbon=P(0x8b9ea6,.12f);var steel=P(0xd2dee0,.3f);var vane=P(0xe6eeee,.05f);
            k.Cylinder(carbon,.0046f,.006f,L-h,k.Sides(6),0,0,0,1);
            if(k.Mid){k.Cylinder(Accent,.0041f,.13f,.16f,k.Sides(6),0);k.Cylinder(AccentSoft,.0041f,.172f,.18f,k.Sides(6),0);}
            k.Lathe(i=>Accent,new[]{.003f,-.012f,.0052f,-.008f,.0052f,.004f,.004f,.01f},k.Sides(6),1,0,true);
            for(int v=0;v<3;v++)
            {
                float a=v*Pi*2/3+Pi/2;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);var paint=v==0?WeaponPaint.Accent(1.3f,.5f):vane;
                Vector3 r0=n*.004f,r1=n*.018f,r2=n*.016f;float z0=.025f,z1=.115f;
                var p0=r0+V(0,0,z0);var p1=r0+V(0,0,z1);var p2=r2+V(0,0,z1-.02f);var p3=r1+V(0,0,z0+.006f);
                k.Quad(paint,p0,p1,p2,p3);k.Quad(paint,p0,p3,p2,p1);
            }
            // The broadhead: a ferrule then three blades to the point.
            k.Lathe(steel,new[]{.0042f,L-h,.0055f,L-h+.01f,.004f,L-h*.35f},k.Sides(6),1,0,false);
            for(int b=0;b<3;b++)
            {
                float a=b*Pi*2/3+Pi/6;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);
                var root=n*.004f+V(0,0,L-h+.008f);var wing=n*.013f+V(0,0,L-h+.018f);var point=V(0,0,L);
                k.Tri(steel,root,wing,point);k.Tri(steel,root,point,wing);
                if(k.Mid){var ew=n*.0136f+V(0,0,L-h+.016f);k.Tube(_=>Accent,new[]{ew,point},_=>.0011f,3,Vector3.UnitX,0,false);}
            }
        }

        // ---- the quiver (chest space: rides the chest joint, the mouth over the right shoulder) ----
        /// <summary>
        /// The back quiver: an oval carbon tube slung from the left hip to the right shoulder with accent bands and a lit rim, a
        /// strap across the chest and the fletched ends of six arrows over the mouth (one vane in the accent). The Prototype
        /// quiver is boxes. `arrows`: how many fletched ends show.
        /// </summary>
        public static void Quiver(WeaponSculpt k,bool standard,int arrows=6)
        {
            var axis=QuiverMouth-QuiverBase;float len=axis.Length();var dir=axis/len;
            // The quiver frame: z along its axis, x across the back (toward the rig's left), y out of the back.
            var x=Vector3.Normalize(Vector3.Cross(Vector3.UnitZ*-1,dir));var y=Vector3.Cross(dir,x);
            var m=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,dir.X,dir.Y,dir.Z,0,QuiverBase.X,QuiverBase.Y,QuiverBase.Z,1);
            k.Push(m);
            if(standard)
            {
                var carbon=P(CarbonLight,.05f);var dark=P(Carbon);var metal=P(Machined,.08f);
                k.Lathe(i=>i==0||i==4?metal:carbon,new[]{.03f,0,.044f,.012f,.05f,.06f,.052f,len-.05f,.056f,len-.01f,.054f,len},k.Sides(12),1,0,true,1,.75f);
                if(k.Mid){foreach(float z in new[]{.1f,len*.55f})k.Torus(Accent,V(0,0,z),.0505f,.0022f,k.Sides(16),3,0,(float)(Math.PI*2),.75f);}
                k.Torus(Accent,V(0,0,len-.004f),.054f,.003f,k.Sides(16),k.Sides(4),0,(float)(Math.PI*2),.75f);
                int shown=k.Fine?arrows:k.Mid?Math.Min(arrows,4):Math.Min(arrows,2);
                for(int i=0;i<shown;i++)
                {
                    float a=i*2.4f,r=.026f*(float)Math.Sqrt((i+.5f)/shown);var c=V(r*(float)Math.Cos(a),r*.75f*(float)Math.Sin(a),len);
                    k.Cylinder(dark,.0034f,len-.02f,len+.11f,4,0,c.X,c.Y);
                    for(int v=0;v<(k.Mid?3:2);v++){float va=v*Pi*2/3+a;var n=V((float)Math.Cos(va),(float)Math.Sin(va),0);var paint=v==0&&i%2==0?WeaponPaint.Accent(1.2f,.5f):P(0xe6eeee,.05f);
                        var p0=c+n*.003f+V(0,0,.02f);var p1=c+n*.003f+V(0,0,.1f);var p2=c+n*.014f+V(0,0,.085f);var p3=c+n*.016f+V(0,0,.03f);k.Quad(paint,p0,p1,p2,p3);k.Quad(paint,p0,p3,p2,p1);}
                }
            }
            else
            {
                k.Box(P(0x5d7079),V(0,0,len*.5f),V(.09f,.07f,len));k.Box(P(0x263a43),V(0,0,len-.01f),V(.1f,.08f,.02f));
                for(int i=0;i<3;i++)k.Box(P(0xeaf6f4),V(-.025f+i*.025f,0,len+.05f),V(.006f,.02f,.09f));
            }
            k.Pop();
            // The strap across the chest front (from under the left arm to over the right shoulder).
            var strap=standard?P(0x263038,.03f):P(0x3b2a24);
            k.Tube(_=>strap,new[]{V(.24f,-.06f,.0f),V(.2f,.0f,.157f),V(-.02f,.17f,.158f),V(-.18f,.31f,.12f),V(-.2f,.36f,-.05f)},_=>.006f,k.Sides(6),Vector3.UnitZ,1,true,false,3.5f);
            if(standard&&k.Mid)k.Box(Accent,V(.09f,.08f,.164f),V(.024f,.024f,.004f),.003f,0,0,-.72f);
        }

        // ---- builds (fresh meshes; the runtime bakes them once into the source bank) ----
        /// <summary>A part (or several) of a skin at a tier, on the left hand.</summary>
        public static WeaponMesh Part(bool standard,int parts,int tier)
        {
            tier=Math.Max(NativeWeaponModels.Full,Math.Min(NativeWeaponModels.Low,tier));var mesh=new WeaponMesh{Spec=NativeBowWire.Spec,Tier=tier};var k=new WeaponSculpt(mesh,tier);
            k.Into(NativeWeaponModels.LeftHand);if(standard)Standard(k,parts);else Prototype(k,parts);mesh.Sections.RemoveAll(s=>s.Positions.Count==0);return mesh;
        }
        /// <summary>The arrow model (root frame, bone -1) of a skin at a tier.</summary>
        public static WeaponMesh ArrowMesh(bool standard,int tier)
        {
            tier=Math.Max(0,Math.Min(2,tier));var mesh=new WeaponMesh{Spec="bow-arrow",Tier=tier};var k=new WeaponSculpt(mesh,tier);k.Into(-1);Arrow(k,standard);mesh.Sections.RemoveAll(s=>s.Positions.Count==0);return mesh;
        }
        /// <summary>The quiver (chest space, bone -1) of a skin at a tier.</summary>
        public static WeaponMesh QuiverMesh(bool standard,int tier)
        {
            tier=Math.Max(0,Math.Min(2,tier));var mesh=new WeaponMesh{Spec="bow-quiver",Tier=tier};var k=new WeaponSculpt(mesh,tier);k.Into(-1);Quiver(k,standard);mesh.Sections.RemoveAll(s=>s.Positions.Count==0);return mesh;
        }
        /// <summary>A unit string segment (bone -1): a thin tube along +z from 0 to 1, scaled per draw to its segment.</summary>
        public static WeaponMesh StringMesh(bool standard)
        {
            var mesh=new WeaponMesh{Spec="bow-string",Tier=0};var k=new WeaponSculpt(mesh,0);k.Into(-1);
            k.Tube(_=>P(StringHex,.15f),new[]{V(0,0,0),V(0,0,1)},_=>standard?.0017f:.0016f,standard?6:4,Vector3.UnitY,0,false);return mesh;
        }

        // ---- model ids (cached strings) ----
        public const string StringId="bow-string",StringProtoId="bow-proto-string";
        private static readonly string[] partIds=Ids();
        private static string[] Ids()
        {
            var o=new string[2*3*3];string[] names={"riser","limb-upper","limb-lower"};
            for(int s=0;s<2;s++)for(int p=0;p<3;p++)for(int t=0;t<3;t++)o[(s*3+p)*3+t]=(s==0?"bow-std-":"bow-proto-")+names[p]+(t==0?"":t==1?"-reduced":"-low");
            return o;
        }
        /// <summary>The live part's model id: part 0 the riser, 1 the upper limb, 2 the lower; `standard` the skin; tier 0..2.</summary>
        public static string PartId(bool standard,int part,int tier)=>partIds[((standard?0:1)*3+Math.Max(0,Math.Min(2,part)))*3+Math.Max(0,Math.Min(2,tier))];
        public static int PartMask(int part)=>part==0?Riser:part==1?UpperLimb:LowerLimb;
        private static readonly string[] arrowIds={"bow-arrow","bow-arrow-reduced","bow-arrow-low","bow-proto-arrow"},quiverIds={"bow-quiver","bow-quiver-reduced","bow-quiver-low","bow-proto-quiver"};
        public static string ArrowId(bool standard,int tier)=>standard?arrowIds[Math.Max(0,Math.Min(2,tier))]:arrowIds[3];
        public static string QuiverId(bool standard,int tier)=>standard?quiverIds[Math.Max(0,Math.Min(2,tier))]:quiverIds[3];
        /// <summary>The Prototype Series held / loose ids the skin map resolves from (NativeWeaponSkins.PrototypeHeld("bow")).</summary>
        public const string PrototypeHeldId="weapon-bow",PrototypeLooseId="loose-bow";

        /// <summary>A stable hash of a mesh's geometry (positions, normals, colours, indices; quantised), for the pinned Prototype.</summary>
        public static ulong Hash(WeaponMesh mesh)
        {
            ulong h=1469598103934665603UL;void Add(long v){unchecked{h^=(ulong)v;h*=1099511628211UL;}}
            foreach(var s in mesh.Sections){Add(s.Bone);foreach(var p in s.Positions){Add((long)Math.Round(p.X*1e5));Add((long)Math.Round(p.Y*1e5));Add((long)Math.Round(p.Z*1e5));}
                foreach(var c in s.Colors){Add((long)Math.Round(c.X*1e4));Add((long)Math.Round(c.Y*1e4));Add((long)Math.Round(c.Z*1e4));}foreach(int i in s.Indices)Add(i);}
            return h;
        }
    }
}

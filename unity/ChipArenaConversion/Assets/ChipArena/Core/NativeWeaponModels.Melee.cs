using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // Standard melee weapons, in the sword family's frame (weapon-visuals.ts): the blade's flat faces +-y, its edges +-x, the
    // grip about the hand at the origin, the guard at z ~.2, the blade-tip socket where the Prototype puts it (sword 1.28,
    // daggers .84, greatsword 1.69; shield (0,.4,.29)), the pommel within the clearance fit (MeleeCapture: .19 / .19 / .27)
    // and the greatsword's support grip (0,0,-.11) on its lower grip. Reach: the hilt is Rigid, the blade stretches from the
    // guard (StretchBase) to the tip socket; the shield keeps the Prototype's uniform stretch (its face is the striking edge).
    public static partial class NativeWeaponModels
    {
        static partial void Melee(Dictionary<string,Design> d)
        {
            d["sword"]=new Design{Spec="sword",Build=Sword,StretchBase=.232f,StretchTip=1.28f,BladeTip=V(0,0,1.275f),BladeBase=V(0,0,.232f)};
            d["daggers"]=new Design{Spec="daggers",Build=Dagger,Dual=true,MirrorLeft=true,StretchBase=.18f,StretchTip=.84f,BladeTip=V(.008f,0,.832f),BladeBase=V(0,0,.18f)};
            d["greatsword"]=new Design{Spec="greatsword",Build=Greatsword,StretchBase=.262f,StretchTip=1.69f,BladeTip=V(0,0,1.685f),BladeBase=V(0,0,.262f),Support=V(0,0,-.11f)};
            d["shield"]=new Design{Spec="shield",Build=Shield,BladeTip=V(0,.4f,.29f)};
        }
        // A blade section (14 points, counter-clockwise from the +x edge): width w, thickness t, edge bevel e, a fuller of width f
        // and depth dep on both flats (0: none), centred at cx. Paint bands: edges, flats, fuller.
        private static Vector3[] BladeRing(float z,float w,float t,float e,float f,float dep,float cx=0)
        {
            if(w<=1e-4f)return Point(14,V(cx,0,z));
            float hw=w/2,ht=t/2,fx=Math.Max(f/2,.0004f),ix=Math.Max(hw-e,fx+.0008f);dep=f>1e-4f?dep:0;
            return new[]{V(cx+hw,0,z),V(cx+ix,ht,z),V(cx+fx,ht,z),V(cx+fx*.45f,ht-dep,z),V(cx-fx*.45f,ht-dep,z),V(cx-fx,ht,z),V(cx-ix,ht,z),V(cx-hw,0,z),V(cx-ix,-ht,z),V(cx-fx,-ht,z),V(cx-fx*.45f,-ht+dep,z),V(cx+fx*.45f,-ht+dep,z),V(cx+fx,-ht,z),V(cx+ix,-ht,z)};
        }
        private static WeaponPaint BladeBand(int side,WeaponPaint edge,WeaponPaint flat,WeaponPaint fuller)
            =>side==0||side==6||side==7||side==13?edge:side>=2&&side<=4||side>=9&&side<=11?fuller:flat;
        // A wrapped grip: octagonal rings with raised wrap bands (full detail) between two metal ferrules.
        private static void WrappedGrip(WeaponSculpt k,float z0,float z1,float r,float swell,WeaponPaint wrap,WeaponPaint cord,WeaponPaint ferrule,int bands,float squash=1)
        {
            int sides=k.Sides(8);var rings=new List<Vector3[]>();int n=k.Fine?bands*2:k.Mid?bands:2;
            for(int i=0;i<=n;i++){float u=(float)i/n,z=z0+(z1-z0)*u,rr=r*(1+swell*(float)Math.Sin(u*Math.PI));if(k.Fine&&i%2==1)rr*=1.07f;rings.Add(Circle(sides,rr,rr*squash,z,Pi/8));}
            k.Loft(rings,(i,j)=>k.Fine?(i%2==0?wrap:cord):wrap,k.Fine?0:1);
            k.Cylinder(ferrule,r*1.18f,z0-.012f,z0+.004f,sides,.003f,0,0,0);k.Cylinder(ferrule,r*1.18f,z1-.004f,z1+.012f,sides,.003f,0,0,0);
        }

        // ---- sword: an arming longsword (wheel pommel, cord-wrapped grip, curved crossguard, ricasso, fullered blade) ----
        private static void Sword(WeaponSculpt k)
        {
            var gold=Paint(Gold,.12f);var goldDark=Paint(GoldDark,.08f);var wrap=Paint(Wrap);var cord=Paint(WrapDark);var edge=Paint(Edge,.25f);var flat=Paint(0xa9bcc2,.08f);var fuller=Paint(0x6f858c,.05f);
            var gem=WeaponPaint.Accent(2.2f,.45f);
            k.Into(RightHand,WeaponStretch.Rigid);
            // Wheel pommel (faces parallel to the blade flats) with an accent gem in each face, and the peen block.
            k.Push(S(1.15f,1.4f,1));
            k.Push(Rx(-Pi/2)*T(0,0,-.156f));k.Lathe(i=>i==2?gold:gold.Scaled(.9f),new[]{.021f,-.019f,.037f,-.0135f,.042f,-.004f,.042f,.004f,.037f,.0135f,.021f,.019f},k.Sides(12),0,Pi/12);k.Pop();
            for(int s=-1;s<=1;s+=2){k.Push(Rx(-s*Pi/2)*T(0,s*.0185f,-.156f));k.Gem(gem,V(0,0,0),.012f,.012f,k.Sides(6),.15f);k.Pop();}
            k.Box(goldDark,V(0,0,-.193f),V(.018f,.014f,.012f),.003f);k.Pop();
            k.Push(S(1.6f,1.6f,1));WrappedGrip(k,-.11f,.166f,.0175f,.08f,wrap,cord,gold,6,.86f);k.Pop();
            // Crossguard: a bar curving toward the blade, flaring to faceted finials, on a centre block with an accent inlay.
            int segs=k.Mid?8:4;var guard=new List<Vector3>();var radii=new List<float>();
            for(int i=0;i<=segs;i++){float u=-1+2f*i/segs,x=u*.165f;guard.Add(V(x,0,.205f+.024f*u*u));float a=Math.Abs(u);radii.Add(.0145f-.004f*a+.005f*a*a*a);}
            k.Tube(i=>i==segs/2?gold:gold.Scaled(.95f),guard,i=>radii[i],k.Sides(6),Vector3.UnitY,0,true,false,.75f);
            for(int s=-1;s<=1;s+=2){k.Push(Ry(s*Pi/2)*T(s*.17f,0,.229f));k.Gem(gold,V(0,0,0),.012f,.03f,k.Sides(6),.3f);k.Pop();}
            k.Box(gold,V(0,0,.205f),V(.06f,.046f,.056f),.01f);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(gem,V(0,s*.0232f,.207f),V(.018f,.0016f,.03f),0);
            // Blade: the ricasso (unsharpened), the shoulder, then a fullered blade narrowing to a reinforced point.
            k.Into(RightHand,WeaponStretch.Blade);
            float[] z={.232f,.30f,.31f,.46f,.70f,.90f,1.03f,1.16f,1.228f,1.275f},w={.064f,.064f,.084f,.08f,.074f,.066f,.06f,.048f,.028f,0},f={0,0,.026f,.026f,.022f,.01f,0,0,0,0};
            var rings=new List<Vector3[]>();for(int i=0;i<z.Length;i++){bool ric=i<2;rings.Add(BladeRing(z[i],w[i],ric?.019f:i>=8?.012f:.0165f,ric?.004f:.017f,f[i],.0034f));}
            if(!k.Mid){rings.RemoveAt(7);rings.RemoveAt(5);rings.RemoveAt(3);}
            k.Push(S(1.2f,1.6f,1));
            k.Loft(rings,(i,j)=>i==0?BladeBand(j,flat.Scaled(.92f),flat.Scaled(.92f),flat.Scaled(.92f)):BladeBand(j,edge,flat,fuller),0,true,false);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(gem,V(0,s*(.00825f-.0034f+.0005f),.6f),V(.0042f,.0012f,.5f),0);
            // Maker's mark on the ricasso: a small accent chevron.
            if(k.Fine)for(int s=-1;s<=1;s+=2)for(int c=-1;c<=1;c+=2)k.Box(gem,V(c*.008f,s*.0098f,.27f),V(.0035f,.0012f,.02f),0,0,c*.6f);
            k.Pop();
        }

        // ---- daggers: a pair of curved combat daggers (dark-coated, bright edge, glowing spine, finger guard, lanyard ring) ----
        private static Vector3[] Wedge(float z,float xs,float xe,float t)
        {
            if(xe-xs<=1e-4f)return Point(7,V(xs,0,z));float g=xe-(xe-xs)*.42f;
            return new[]{V(xe,0,z),V(g,t*.3f,z),V(xs+.003f,t/2,z),V(xs,t*.28f,z),V(xs,-t*.28f,z),V(xs+.003f,-t/2,z),V(g,-t*.3f,z)};
        }
        private static void Dagger(WeaponSculpt k)
        {
            var metal=Paint(0x56666d,.08f);var coat=Paint(0x2c353b,.04f);var edge=Paint(Edge,.28f);var polymer=Paint(Polymer);var polyDark=Paint(PolymerDark);var spine=WeaponPaint.Accent(1.8f,.4f);
            k.Into(RightHand,WeaponStretch.Rigid);
            // Skull-crusher cap and lanyard ring.
            k.Push(S(1.45f,1.45f,1));
            k.Lathe(metal,new[]{0f,-.186f,.012f,-.181f,.019f,-.168f,.02f,-.152f,.017f,-.14f},k.Sides(8),0,Pi/8);
            if(k.Mid){k.Push(Ry(Pi/2)*T(0,0,-.183f));k.Torus(metal,V(0,0,0),.011f,.0028f,k.Sides(10),4);k.Pop();}
            // Grip: an oval handle with finger grooves on the edge side, two-tone panels and an accent inlay on each flat.
            int rows=k.Fine?16:k.Mid?6:3;var grip=new List<Vector3[]>();int sides=k.Sides(12);
            for(int i=0;i<=rows;i++)
            {
                float u=(float)i/rows,gz=-.142f+u*.302f;var ring=Circle(sides,.0185f,.0135f,gz);float groove=1-.12f*(float)Math.Max(0,Math.Sin(u*Math.PI*4-.6f));
                for(int j=0;j<sides;j++)if(ring[j].X>0)ring[j].X*=groove;grip.Add(ring);
            }
            k.Loft(grip,(i,j)=>j==0||j==sides/2?polyDark:polymer,1);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(WeaponPaint.Accent(1.1f,.35f),V(0,s*.0136f,.01f),V(.006f,.0014f,.22f),0);
            k.Pop();k.Push(S(1.3f,1.5f,1));
            // Finger guard: a block toward the edge side with a forward-curving quillon.
            k.Box(metal,V(.008f,0,.168f),V(.07f,.026f,.022f),.006f);
            if(k.Mid)k.Tube(_=>metal,new[]{V(.036f,0,.17f),V(.05f,0,.162f),V(.058f,0,.15f)},i=>.0062f-i*.0012f,k.Sides(6),Vector3.UnitY,1,true);
            k.Pop();
            // The curved blade: spine and edge lines over z, the tip just off the axis by the blade-tip socket.
            k.Into(RightHand,WeaponStretch.Blade);k.Push(S(1.3f,1.5f,1));
            float[] z={.178f,.22f,.30f,.40f,.50f,.60f,.68f,.74f,.79f,.832f},xs={-.02f,-.02f,-.018f,-.013f,-.007f,0,.005f,.008f,.009f,.006f},xe={.028f,.032f,.037f,.04f,.04f,.036f,.03f,.022f,.012f,.006f};
            var blade=new List<Vector3[]>();
            for(int i=0;i<z.Length;i++)
            {
                if(!k.Mid&&(i==2||i==4||i==6))continue;blade.Add(Wedge(z[i],xs[i],xe[i],i>=8?.008f:.011f));
                // Full detail: a smoothed curve (midpoints between the control rings, eased toward the neighbours).
                if(k.Fine&&i+1<z.Length-1){int j=Math.Max(0,i-1),l=Math.Min(z.Length-1,i+2);float Mid(float[] v)=>(v[i]+v[i+1])*.5625f-(v[j]+v[l])*.0625f;blade.Add(Wedge((z[i]+z[i+1])/2,Mid(xs),Mid(xe),i+1>=8?.009f:.011f));}
            }
            k.Loft(blade,(i,j)=>j==0||j==6?edge:j==3?spine:j==2||j==4?metal:coat,0,true,false);
            // Jimping on the spine just ahead of the guard.
            if(k.Fine)for(int i=0;i<5;i++)k.Box(metal,V(-.0205f,0,.2f+i*.011f),V(.002f,.009f,.004f),0);
            k.Pop();
        }

        // ---- greatsword: a zweihander (pear pommel, long two-part grip, wide guard with side rings, wrapped ricasso, lugs) ----
        private static void Greatsword(WeaponSculpt k)
        {
            var gold=Paint(Gold,.12f);var goldDark=Paint(GoldDark,.08f);var wrap=Paint(Wrap);var cord=Paint(WrapDark);var edge=Paint(Edge,.25f);var flat=Paint(0xa9bcc2,.08f);var fuller=Paint(0x6c8188,.05f);
            var steel=Paint(Steel,.1f);var gem=WeaponPaint.Accent(2.2f,.45f);
            k.Into(RightHand,WeaponStretch.Rigid);
            // Faceted pear pommel with an accent ring.
            k.Push(S(1.3f,1.3f,1));
            k.Lathe(i=>i==2?goldDark:gold,new[]{0f,-.276f,.016f,-.27f,.033f,-.255f,.038f,-.238f,.032f,-.218f,.019f,-.2f,.016f,-.192f},k.Sides(8),0,Pi/8);
            if(k.Mid)k.Torus(gem,V(0,0,-.238f),.0375f,.0028f,k.Sides(16),4);
            // Two grips (lower for the support hand at z -.11, upper for the main hand) about a central swell ring.
            WrappedGrip(k,-.19f,-.02f,.0195f,.06f,wrap,cord,gold,4,.9f);
            WrappedGrip(k,.02f,.198f,.0195f,.06f,wrap,cord,gold,4,.9f);
            k.Lathe(gold,new[]{.02f,-.022f,.027f,-.012f,.027f,.012f,.02f,.022f},k.Sides(8),0,Pi/8);
            k.Pop();
            // The wide guard: straight bar, downturned flared ends, a centre block, and a side ring on each flat.
            int segs=k.Mid?10:4;var bar=new List<Vector3>();var radii=new List<float>();
            for(int i=0;i<=segs;i++){float u=-1+2f*i/segs,x=u*.235f,a=Math.Abs(u);bar.Add(V(x,0,.228f+.03f*a*a*a));radii.Add(.0165f-.0045f*a+.0065f*a*a*a*a);}
            k.Tube(_=>gold,bar,i=>radii[i],k.Sides(6),Vector3.UnitY,0,true,false,.8f);
            for(int s=-1;s<=1;s+=2){k.Push(Ry(s*Pi/2)*T(s*.24f,0,.262f));k.Gem(gold,V(0,0,0),.015f,.036f,k.Sides(6),.3f);k.Pop();}
            k.Box(gold,V(0,0,.228f),V(.076f,.05f,.07f),.012f);
            if(k.Mid)for(int s=-1;s<=1;s+=2){k.Push(Rx(Pi/2)*T(0,s*.032f,.236f));k.Torus(gold,V(0,0,0),.03f,.0055f,k.Sides(14),k.Sides(6));k.Pop();k.Box(gem,V(0,s*.0182f,.23f),V(.02f,.0016f,.034f),0);}
            // Blade: the wrapped ricasso, the lugs (parierhaken), then a long fullered blade to a reinforced point.
            k.Into(RightHand,WeaponStretch.Blade);k.Push(S(1.15f,1.4f,1));
            k.Loft(new[]{BladeRing(.262f,.056f,.024f,.004f,0,0),BladeRing(.47f,.056f,.024f,.004f,0,0)},(i,j)=>steel,0,false,false);
            WrappedGrip(k,.29f,.43f,.031f,0,wrap,cord,gold,4,.44f);
            for(int s=-1;s<=1;s+=2)
            {
                var lug=new[]{V(s*.026f,0,.47f),V(s*.062f,0,.474f),V(s*.09f,0,.464f),V(s*.1f,0,.448f)};
                k.Tube(i=>i==3?steel.Scaled(1.15f):steel,lug,i=>.0115f-i*.0025f,k.Sides(6),Vector3.UnitY,0,true,false,.7f);
            }
            k.Box(gold,V(0,0,.472f),V(.07f,.032f,.016f),.005f);
            float[] z={.48f,.62f,.9f,1.18f,1.42f,1.58f,1.645f,1.685f},w={.13f,.124f,.114f,.104f,.094f,.074f,.044f,0},f={.04f,.04f,.034f,.02f,0,0,0,0};
            var rings=new List<Vector3[]>();for(int i=0;i<z.Length;i++)rings.Add(BladeRing(z[i],w[i],i>=6?.014f:.021f,.026f,f[i],.0045f));
            if(!k.Mid){rings.RemoveAt(4);rings.RemoveAt(2);}
            k.Loft(rings,(i,j)=>BladeBand(j,edge,flat,fuller),0,true,false);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(gem,V(0,s*(.0105f-.0045f+.0006f),.78f),V(.006f,.0012f,.56f),0);
            if(k.Fine)for(int s=-1;s<=1;s+=2)for(int i=0;i<3;i++)k.Box(gem,V(0,s*.0123f,.31f+i*.05f),V(.03f,.0012f,.004f),0);
            k.Pop();
        }

        // ---- shield: a bowed heater shield (steel rim, enamel field, a chip-circuit emblem round a glowing boss) and a side sword ----
        private static readonly float[] HeaterOutline={0,-.34f,.12f,-.27f,.21f,-.17f,.27f,-.06f,.296f,.06f,.302f,.2f,.302f,.355f,.288f,.402f,.25f,.42f,0,.425f,-.25f,.42f,-.288f,.402f,-.302f,.355f,-.302f,.2f,-.296f,.06f,-.27f,-.06f,-.21f,-.17f,-.12f,-.27f};
        private static float Bow(float x)=>.032f*(1-(x/.3f)*(x/.3f));
        private static Vector3[] Bowed(Vector3[] ring,float z){var o=new Vector3[ring.Length];for(int i=0;i<o.Length;i++)o[i]=V(ring[i].X,ring[i].Y,z+Bow(ring[i].X));return o;}
        private static void Shield(WeaponSculpt k)
        {
            var steel=Paint(Steel,.1f);var steelBright=Paint(SteelBright,.14f);var steelDark=Paint(0x60747b,.06f);var field=Paint(0x1f3a4a,.04f);var fieldDeep=Paint(0x183040,.03f);
            var gold=Paint(Gold,.14f);var back=Paint(0x3a2c26);var leather=Paint(0x4a3226);var gem=WeaponPaint.Accent(2.4f,.5f);var trace=WeaponPaint.Accent(1.5f,.35f);
            var outline=Outline(0,HeaterOutline);
            // Body: back face, the rim's outer band and bevel, the rim top, then the field dishing up to its centre.
            var rings=new List<Vector3[]>{Bowed(Inset(outline,.004f,0),.178f),Bowed(outline,.19f),Bowed(outline,.238f),Bowed(Inset(outline,.012f,0),.252f),Bowed(Inset(outline,.026f,0),.252f),Bowed(Inset(outline,.034f,0),.24f)};
            var flatEdge=Inset(outline,.034f,0);
            if(k.Mid){rings.Add(Bowed(Moved(flatEdge,V(0,0,0),.72f),.248f));rings.Add(Bowed(Moved(flatEdge,V(0,0,0),.42f),.253f));}
            rings.Add(Bowed(Moved(flatEdge,V(0,0,0),.12f),.256f));
            k.Loft(rings,(i,j)=>i==0?steelDark:i==1?steel:i==2?steelBright:i==3?steel:i==4?steelDark:i==5?field:fieldDeep,0,true,true,back,false,false,fieldDeep);
            // Gold inner trim following the rim, and rivets round it.
            var trim=Bowed(Inset(outline,.06f,0),.247f);
            if(k.Mid)k.Tube(_=>gold,trim,_=>.0042f,4,Vector3.UnitZ,0,false,true,1);
            if(k.Fine){var studs=Bowed(Inset(outline,.018f,0),.255f);for(int i=0;i<studs.Length;i++)k.Sphere(steelBright,studs[i],V(.0065f,.0065f,.004f),3,6);}
            // Emblem: a hexagonal gold boss with a glowing chip-core gem, and circuit traces running out to the rim with pads.
            var boss=V(0,.06f,.2475f+Bow(0)+.014f);
            k.Push(T(boss.X,boss.Y,boss.Z-.012f));k.Cylinder(gold,.052f,0,.014f,6,.006f,0,0,0);k.Pop();
            k.Push(T(boss.X,boss.Y,boss.Z+.002f));k.Cylinder(Paint(GoldDark,.06f),.034f,0,.004f,6,.0015f,0,0,0);k.Pop();
            k.Gem(gem,boss+V(0,0,.012f),.024f,.018f,6,.2f,0,.55f);
            if(k.Mid)
            {
                var traces=new[]{new[]{V(0,.112f,0),V(0,.25f,0),V(.06f,.31f,0),V(.06f,.36f,0)},new[]{V(0,.112f,0),V(0,.24f,0),V(-.06f,.30f,0),V(-.06f,.35f,0)},
                    new[]{V(.045f,.035f,0),V(.16f,.035f,0),V(.2f,-.01f,0),V(.2f,-.06f,0)},new[]{V(-.045f,.035f,0),V(-.16f,.035f,0),V(-.2f,-.01f,0),V(-.2f,-.06f,0)},
                    new[]{V(0,.008f,0),V(0,-.14f,0),V(.04f,-.19f,0)},new[]{V(0,-.06f,0),V(-.05f,-.12f,0),V(-.05f,-.18f,0)}};
                foreach(var path in traces)
                {
                    var pts=new List<Vector3>();foreach(var p in path){float r=Vector2.Distance(new Vector2(p.X,p.Y),new Vector2(0,.06f));pts.Add(V(p.X,p.Y,.2475f+.0085f*Math.Max(0,1-r/.3f)+Bow(p.X)+.0035f));}
                    k.Tube(_=>trace,pts,_=>.0032f,4,Vector3.UnitZ,0,true,false,.6f,Pi/4);
                    var end=pts[pts.Count-1];k.Box(gold,end+V(0,0,.001f),V(.016f,.016f,.006f),.002f);k.Box(gem,end+V(0,0,.0045f),V(.008f,.008f,.002f),0);
                }
            }
            // Back: two leather straps and the hand grip bar.
            for(int i=0;i<2;i++){float y=i==0?-.02f:.2f;k.Box(leather,V(0,y,.168f+Bow(0)*.5f),V(.24f,.04f,.012f),.003f);}
            k.Box(steelDark,V(0,.09f,.15f),V(.026f,.24f,.03f),.006f);
            // The side sword: a short sword in a scabbard along the back, its hilt above the shield's top edge on the outer side.
            SideSword(k,V(-.205f,-.08f,.176f));
        }
        // A sheathed arming sword riding the shield's back (hilt up), source frame offset `at` = the scabbard's lower end.
        private static void SideSword(WeaponSculpt k,Vector3 at)
        {
            var gold=Paint(Gold,.12f);var wrap=Paint(Wrap);var cord=Paint(WrapDark);var leather=Paint(0x34241e);var chape=Paint(GoldDark,.1f);var gem=WeaponPaint.Accent(2f,.45f);
            // Up the shield (+y), the flat facing the shield back.
            k.Push(Rx(-Pi/2)*Rz(-.08f)*T(at.X,at.Y,at.Z));
            var scab=new List<Vector3[]>{Rect(.03f,.016f,.006f,0),Rect(.05f,.022f,.008f,.03f),Rect(.054f,.024f,.008f,.42f),Rect(.058f,.028f,.008f,.45f)};
            k.Loft(scab,(i,j)=>i==2?chape:leather,0,true,true,chape);
            k.Box(chape,V(0,0,.015f),V(.04f,.026f,.03f),.006f);
            if(k.Mid)k.Box(chape,V(0,0,.25f),V(.058f,.028f,.014f),.004f);
            // Crossguard, grip, pommel above the throat.
            k.Box(gold,V(0,0,.462f),V(.12f,.022f,.02f),.006f);
            for(int s=-1;s<=1;s+=2)k.Box(gold,V(s*.064f,0,.468f),V(.016f,.026f,.026f),.005f);
            WrappedGrip(k,.475f,.565f,.0135f,.05f,wrap,cord,gold,3);
            k.Push(T(0,0,.585f));k.Lathe(gold,new[]{.008f,-.012f,.019f,-.006f,.02f,.004f,.012f,.014f},k.Sides(8),0,Pi/8);k.Pop();
            if(k.Mid)for(int s=-1;s<=1;s+=2){k.Push(Rx(s<0?Pi:0)*T(0,s*.019f,.585f));k.Gem(gem,V(0,0,0),.007f,.008f,6,.2f);k.Pop();}
            k.Pop();
        }
    }
}

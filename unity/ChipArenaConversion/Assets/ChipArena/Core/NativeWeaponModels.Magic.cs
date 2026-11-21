using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // Standard magic staffs (fire, water, lightning, darkness), the L33t H4x holo staff and the DEV fighters' gauntlets. The
    // staffs ride the left hand as the Prototype does (weapon-visuals.ts: the main grip at the origin, the guard-grip second
    // hand at z .40, the heel at -.38, the element head round its energy core under the 'muzzle' socket at z .88; L33t's at
    // 1.52); the gauntlets enclose each fist like the Prototype box, the punching face at the blade-tip socket.
    public static partial class NativeWeaponModels
    {
        static partial void Magic(Dictionary<string,Design> d)
        {
            d["fire"]=new Design{Spec="fire",Build=k=>Staff(k,1),Bone=LeftHand,Muzzle=V(0,0,.88f),StaffTip=V(0,0,.74f),Support=V(0,0,.40f)};
            d["water"]=new Design{Spec="water",Build=k=>Staff(k,2),Bone=LeftHand,Muzzle=V(0,0,.88f),StaffTip=V(0,0,.745f),Support=V(0,0,.40f)};
            d["lightning"]=new Design{Spec="lightning",Build=k=>Staff(k,3),Bone=LeftHand,Muzzle=V(0,0,.88f),StaffTip=V(0,0,.805f),Support=V(0,0,.40f)};
            d["darkness"]=new Design{Spec="darkness",Build=k=>Staff(k,4),Bone=LeftHand,Muzzle=V(0,0,.88f),StaffTip=V(0,0,.77f),Support=V(0,0,.40f)};
            d["leet"]=new Design{Spec="leet",Build=Leet,Bone=LeftHand,Muzzle=V(0,0,1.52f),StaffTip=V(0,0,1.33f)};
            d["emperor"]=new Design{Spec="emperor",Build=Emperor,Dual=true,MirrorLeft=true,BladeTip=V(0,-.045f,.14f)};
            d["ansatsuken"]=new Design{Spec="ansatsuken",Build=Ansatsuken,Dual=true,MirrorLeft=true,BladeTip=V(0,-.045f,.115f)};
        }
        private static Vector3 Polar(float r,float a,float z)=>V(r*(float)Math.Cos(a),r*(float)Math.Sin(a),z);
        // The shared staff: a heel spike, a tapered shaft, the two wrapped grips, two accent bands and the head collar.
        private static void StaffBody(WeaponSculpt k,WeaponPaint shaft,WeaponPaint wrap,WeaponPaint cord,WeaponPaint metal,WeaponPaint trim)
        {
            k.Push(S(1.45f,1.45f,1));
            k.Lathe(metal,new[]{0f,-.41f,.012f,-.397f,.024f,-.372f,.029f,-.354f,.025f,-.332f},k.Sides(8),0,Pi/8);
            k.Lathe(shaft,new[]{.0215f,-.336f,.0205f,.1f,.019f,.5f},k.Sides(8),1,Pi/8,false);
            WrappedGrip(k,-.086f,.09f,.0235f,.05f,wrap,cord,trim,5);
            WrappedGrip(k,.34f,.46f,.0225f,.04f,wrap,cord,trim,4);
            if(k.Mid)foreach(float z in new[]{.175f,.26f})k.Torus(AccentStrip,V(0,0,z),.0212f,.0028f,k.Sides(12),4);
            k.Lathe(i=>i==2?trim:metal,new[]{.022f,.49f,.03f,.505f,.034f,.535f,.047f,.556f,.045f,.57f,.03f,.578f},k.Sides(10),0,Pi/10);
            k.Pop();
        }
        private static void Staff(WeaponSculpt k,int school){k.Push(S(1.18f,1.18f,1));StaffHead(k,school);k.Pop();}
        private static void StaffHead(WeaponSculpt k,int school)
        {
            var gold=Paint(Gold,.12f);var wrap=Paint(Wrap);var cord=Paint(WrapDark);var steel=Paint(Steel,.1f);
            if(school==1)
            {
                // Fire: a brazier on the collar, four iron claws whose tips run hot, cradling a faceted flame crystal.
                var iron=Paint(0x5a4a44,.06f);var hot=WeaponPaint.Glow(0xff8a3a,1.3f,.45f);var core=WeaponPaint.Glow(0xff7c3e,1.6f,.6f);var ember=WeaponPaint.Glow(0xffc46a,1.5f,.5f);
                StaffBody(k,Paint(0x5c3b2c,.03f),wrap,cord,iron,gold);
                k.Lathe(i=>i>=3?hot:iron,new[]{.03f,.565f,.05f,.585f,.062f,.61f,.065f,.632f,.059f,.642f,.0f,.645f},k.Sides(12),0,0,false);
                if(k.Mid)for(int i=0;i<6;i++){float a=i*Pi/3;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);k.Decal(hot,n*.0628f+V(0,0,.612f),V(0,0,.008f),Vector3.Cross(n,V(0,0,1))*.006f,.0015f);}
                int claws=4,steps=k.Mid?7:4;
                for(int c=0;c<claws;c++)
                {
                    float a=c*Pi/2+Pi/4;var path=new List<Vector3>();
                    for(int i=0;i<=steps;i++){float t=(float)i/steps,r=.06f+.042f*(float)Math.Sin(t*Pi*.85f)-.035f*t*t,z=.632f+t*.27f;path.Add(Polar(r,a+.25f*t,z));}
                    k.Tube(i=>i>=steps-1?hot:iron,path,i=>.0135f-.0085f*i/steps,k.Sides(5),V(0,0,1),0,true,false,.8f);
                }
                k.Gem(core,V(0,0,.74f),.05f,.23f,k.Sides(6),.3f,0,.55f);
                if(k.Fine)for(int i=0;i<3;i++){float a=i*Pi*2/3+.4f;k.Gem(ember,Polar(.05f,a,.8f+i*.02f),.008f,.022f,4,.4f,a);}
            }
            else if(school==2)
            {
                // Water: a glowing orb inside a cage of three spiralling silver bands, an equator ring and a crystal finial.
                var silver=Paint(0xcfe3ea,.15f);var orb=WeaponPaint.Glow(0x65d8ed,1.2f,.6f);var crystal=WeaponPaint.Glow(0x9ff2ff,1.5f,.5f);
                StaffBody(k,Paint(0x2f5470,.03f),wrap,cord,silver.Scaled(.8f),silver);
                k.Sphere(orb,V(0,0,.745f),V(.07f,.07f,.07f),k.Sides(8),k.Sides(12));
                int steps=k.Mid?12:6;
                for(int b=0;b<3;b++)
                {
                    float phase=b*Pi*2/3;var path=new List<Vector3>();
                    for(int i=0;i<=steps;i++){float t=(float)i/steps,r=.034f+.055f*(float)Math.Sin(t*Pi)-.018f*t,z=.578f+t*.322f;path.Add(Polar(r,phase+t*Pi*1.35f,z));}
                    k.Tube(_=>silver,path,_=>.0065f,k.Sides(5),V(0,0,1),1,true);
                }
                if(k.Mid){k.Push(Rx(.38f)*T(0,0,.745f));k.Torus(silver,V(0,0,0),.094f,.0045f,k.Sides(20),4);k.Pop();}
                k.Gem(crystal,V(0,0,.905f),.015f,.045f,k.Sides(6),.35f);
                if(k.Fine)for(int i=0;i<2;i++)k.Gem(orb,Polar(.105f,i*Pi+.7f,.71f+i*.06f),.009f,.024f,4,.6f);
            }
            else if(school==3)
            {
                // Lightning: a copper coil stack round the rod, three tesla prongs with glowing tips and the charged ball.
                var copper=Paint(0xc8834a,.18f);var ball=WeaponPaint.Glow(0xc2a2ff,1.6f,.6f);var arc=WeaponPaint.Glow(0xd6c6ff,1.2f,.3f);
                StaffBody(k,Paint(0x3d3d63,.03f),wrap,cord,steel.Scaled(.8f),copper);
                k.Cylinder(steel,.011f,.57f,.785f,k.Sides(8),0);
                int coils=k.Mid?5:3;for(int i=0;i<coils;i++)k.Torus(copper,V(0,0,.592f+i*(.09f/(coils-1))),.031f,.0068f,k.Sides(14),k.Sides(6));
                k.Lathe(steel,new[]{.012f,.772f,.022f,.78f,.02f,.79f},k.Sides(8),0,0,false);
                k.Sphere(ball,V(0,0,.808f),V(.046f,.046f,.046f),k.Sides(7),k.Sides(10));
                for(int p=0;p<3;p++)
                {
                    float a=p*Pi*2/3+Pi/2;var path=new[]{Polar(.03f,a,.685f),Polar(.062f,a,.735f),Polar(.078f,a,.79f),Polar(.083f,a,.84f)};
                    k.Tube(_=>steel,path,i=>.0068f-i*.0009f,k.Sides(6),V(0,0,1),1,true);
                    k.Sphere(ball,path[3]+V(0,0,.008f),V(.012f,.012f,.012f),4,k.Sides(6));
                }
                if(k.Fine)k.Torus(arc,V(0,0,.845f),.078f,.0026f,20,4);
            }
            else
            {
                // Darkness: a void crescent (horns up) on a neck, a glowing inner rim, and the void orb with its event horizon.
                var metal=Paint(0x6e5a92,.1f);var rim=WeaponPaint.Glow(0xb98bff,1.5f,.4f);var voidPaint=Paint(0x0d0a14,.02f);var horizon=WeaponPaint.Glow(0xc59bff,1.7f,.45f);
                StaffBody(k,Paint(0x3e2f50,.03f),wrap,cord,metal,Paint(0x9a82c4,.12f));
                k.Cylinder(metal,.017f,.575f,.648f,k.Sides(8),.003f);
                int steps=k.Mid?16:8;float zc=.765f,R=.118f;var arcPath=new List<Vector3>();var radii=new List<float>();var inner=new List<Vector3>();
                for(int i=0;i<=steps;i++)
                {
                    float t=(float)i/steps,th=(150+240*t)*Pi/180;arcPath.Add(V(R*(float)Math.Cos(th),0,zc+R*(float)Math.Sin(th)));
                    float edge=Math.Abs(t-.5f)*2;radii.Add(.024f*(1-edge*edge)+.003f);inner.Add(V((R-.021f)*(float)Math.Cos(th),.0f,zc+(R-.021f)*(float)Math.Sin(th)));
                }
                k.Tube(_=>metal,arcPath,i=>radii[i],k.Sides(6),V(0,1,0),0,true,false,.55f);
                k.Tube(_=>rim,inner.GetRange(steps/8,steps-steps/4+1),_=>.0062f,4,V(0,1,0),0,true,false,1.5f);
                k.Sphere(voidPaint,V(0,0,.775f),V(.046f,.046f,.046f),k.Sides(8),k.Sides(10));
                k.Push(Rx(Pi/2)*T(0,0,.775f));k.Torus(horizon,V(0,0,0),.056f,.0042f,k.Sides(20),4);k.Pop();
                if(k.Mid){k.Push(Ry(Pi/2)*Rx(.5f)*T(0,0,.775f));k.Torus(horizon.Scaled(.7f),V(0,0,0),.064f,.003f,k.Sides(20),4);k.Pop();}
                if(k.Fine)for(int i=0;i<3;i++){float a=i*Pi*2/3+.3f;k.Gem(metal,Polar(.085f,a,.78f+.03f*(float)Math.Sin(a*2)),.01f,.028f,4,.5f,a);}
            }
        }

        // ---- L33t H4x: a holo-tech staff (hex shaft with circuit traces and glowing bands, a cyberdeck at the grip, a three-fin
        // emitter projecting a wireframe octahedron with its core and two holo rings; the tip under the 'muzzle' at 1.52) ----
        private static void Leet(WeaponSculpt k)
        {
            var shell=Paint(0x1c2e24,.04f);var shellLight=Paint(0x3a5244,.06f);var green=WeaponPaint.Glow(0x52ff83,1.4f,.5f);var dim=WeaponPaint.Glow(0x52ff83,.8f,.35f);var core=WeaponPaint.Glow(0xa8ffc4,1.8f,.6f);
            k.Push(S(1.35f,1.35f,1));
            k.Lathe(shellLight,new[]{0f,-.49f,.02f,-.478f,.036f,-.455f,.032f,-.44f},6,0,Pi/6);
            k.Lathe(shell,new[]{.03f,-.442f,.03f,1.1f},6,0,Pi/6,false);
            // Circuit traces down three faces (segments with gaps), and the bands.
            if(k.Mid)for(int f=0;f<3;f++)
            {
                float a=Pi/6+f*Pi*2/3+Pi/6;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);var u=Vector3.Cross(V(0,0,1),n);float face=.03f*(float)Math.Cos(Pi/6);
                float z=-.4f+f*.07f;int seg=0;
                while(z<1.02f){float len=.06f+.05f*((seg*7+f*3)%4);float zz=Math.Min(1.04f,z+len);k.Decal(green,n*face+V(0,0,(z+zz)/2),u*.0028f,V(0,0,(zz-z)/2),.001f);z=zz+.03f;seg++;if(!k.Fine&&seg>5)break;}
            }
            int bands=k.Mid?9:4;for(int i=0;i<bands;i++){float z=-.35f+i*(1.44f/(bands-1));k.Lathe(dim,new[]{.034f,z,.036f,z+.006f,.036f,z+.018f,.034f,z+.024f},6,0,Pi/6);}
            // The cyberdeck at the grip: a bevelled slab with a screen and glowing keys.
            k.Box(Paint(0x111915,.03f),V(0,.046f,.2f),V(.074f,.026f,.19f),.007f);
            k.Box(green.Scaled(.7f),V(0,.0595f,.26f),V(.056f,.0012f,.05f),0);
            if(k.Mid){int cols=k.Fine?4:2,rows=k.Fine?5:3;for(int c=0;c<cols;c++)for(int r=0;r<rows;r++)k.Box(k.Fine&&(c+r)%3==0?green:shellLight,V(-.024f+c*(.048f/(cols-1)),.061f,.13f+r*(.09f/(rows-1))),V(.01f,.004f,.011f),.0015f);}
            k.Pop();
            // Emitter collar and fins.
            k.Lathe(i=>i==1?green:shellLight,new[]{.03f,1.06f,.046f,1.085f,.05f,1.1f,.044f,1.13f},6,0,Pi/6);
            for(int f=0;f<3;f++){float a=f*Pi*2/3+Pi/2;k.Push(Rz(a)*T(0,0,0));k.Box(shellLight,V(.05f,0,1.18f),V(.012f,.03f,.12f),.004f,0,-.28f);if(k.Mid)k.Box(green,V(.058f,0,1.19f),V(.002f,.012f,.08f),0,0,-.28f);k.Pop();}
            // The projected wireframe octahedron with its core, and two holo rings.
            Vector3 top=V(0,0,1.53f),bottom=V(0,0,1.13f);float c0=1.33f,r0=.13f;var mid=new[]{V(r0,0,c0),V(0,r0,c0),V(-r0,0,c0),V(0,-r0,c0)};
            for(int i=0;i<4;i++)
            {
                k.Tube(_=>green,new[]{mid[i],mid[(i+1)%4]},_=>.0055f,4,V(0,0,1),0,true);
                k.Tube(_=>green,new[]{bottom,mid[i]},_=>.0055f,4,V(0,0,1),0,true);
                k.Tube(_=>green,new[]{mid[i],top},_=>.0055f,4,V(0,0,1),0,true);
            }
            k.Gem(core,V(0,0,c0),.05f,.15f,4,.5f,Pi/4);
            if(k.Mid){k.Push(Rx(.6f)*Ry(.3f)*T(0,0,c0));k.Torus(green.Scaled(.8f),V(0,0,0),.24f,.0055f,k.Sides(28),4);k.Pop();k.Push(Rx(-.5f)*Ry(-.7f)*T(0,0,c0));k.Torus(dim,V(0,0,0),.3f,.005f,k.Sides(32),4);k.Pop();}
        }

        // ---- Emperor (DEV): imperial gauntlets (layered back plates with gold trim, a spiked knuckle guard, the red glow strip,
        // finger rolls and a crowned cuff) ----
        private static void Emperor(WeaponSculpt k)
        {
            var base_=Paint(0x1e1e2a,.03f);var plate=Paint(0x52546a,.08f);var gold=Paint(Gold,.14f);var red=WeaponPaint.Glow(0xef1e43,1.6f,.55f);var steel=Paint(SteelBright,.14f);
            k.Box(base_,V(0,-.012f,0),V(.208f,.188f,.222f),.03f);
            // Back-of-hand lames (overlapping, stepping up toward the wrist) with gold edges.
            for(int i=0;i<3;i++){float z=-.075f+i*.06f;k.Box(plate,V(0,.078f+.006f*(2-i),z),V(.214f,.026f,.075f),.009f,-.12f);if(k.Mid)k.Box(gold,V(0,.092f+.006f*(2-i),z+.036f),V(.2f,.006f,.008f),.002f,-.12f);}
            // The knuckle guard with four spikes, and the red strip across the fist's face.
            k.Box(gold,V(0,.038f,.112f),V(.214f,.05f,.03f),.01f);
            int spikes=k.Mid?4:2;for(int i=0;i<spikes;i++){float x=spikes==4?-.075f+i*.05f:-.05f+i*.1f;k.Gem(steel,V(x,.045f,.13f),.013f,.04f,4,.15f,Pi/4);}
            k.Box(red,V(0,-.045f,.112f),V(.2f,.034f,.012f),.004f);
            // Finger rolls under the fist and the three knuckle studs.
            if(k.Mid)for(int i=0;i<4;i++)k.Box(plate.Scaled(.8f),V(-.075f+i*.05f,-.095f,.05f),V(.046f,.03f,.09f),.01f);
            for(int i=0;i<3;i++)k.Box(steel,V((i-1)*.07f,-.104f,.1f),V(.045f,.03f,.05f),.008f);
            // Crowned cuff flaring at the wrist, its points gold.
            k.Lathe(i=>i==1?gold:plate,new[]{.1f,-.105f,.106f,-.122f,.115f,-.14f,.11f,-.146f},k.Sides(8),0,Pi/8,false);
            if(k.Mid)for(int i=0;i<8;i++){float a=i*Pi/4+Pi/8;var p=Polar(.114f,a,-.15f);k.Push(Ry(Pi)*T(p.X,p.Y,p.Z));k.Gem(gold,V(0,0,0),.012f,.03f,4,.1f,a);k.Pop();}
        }

        // ---- Ansatsuken (DEV): padded fighting gloves (rounded fist, knuckle pad with finger seams, cream back strap with a ki
        // emblem, wrapped wrist) ----
        private static void Ansatsuken(WeaponSculpt k)
        {
            var red=Paint(0x9b2030,.04f);var bright=Paint(0xe23a42,.05f);var cream=Paint(0xe8d6ad,.04f);var seam=Paint(0x5a1018);var ki=WeaponPaint.Glow(0x80dfff,1.4f,.45f);
            k.Box(red,V(0,-.016f,-.012f),V(.2f,.172f,.186f),.042f);
            k.Box(bright,V(0,-.03f,.064f),V(.206f,.136f,.075f),.032f);
            if(k.Fine)for(int i=0;i<3;i++){float x=(i-1)*.05f;k.Box(seam,V(x,-.032f,.1022f),V(.003f,.11f,.002f),0);k.Box(seam,V(x,-.099f,.06f),V(.003f,.002f,.07f),0);}
            // Full detail: a padded thumb along the fist's side and a stitched seam round the knuckle pad.
            if(k.Fine){k.Box(bright,V(.095f,-.06f,.02f),V(.03f,.06f,.11f),.012f);for(int i=0;i<9;i++)k.Box(cream,V(-.088f+i*.022f,.0395f,.08f),V(.008f,.002f,.003f),0);}
            k.Box(cream,V(0,.068f,-.012f),V(.13f,.026f,.17f),.009f);
            if(k.Mid){k.Box(seam,V(0,.0815f,-.012f),V(.11f,.0015f,.003f),0);k.Push(Rx(-Pi/2)*T(0,.082f,.01f));k.Gem(ki,V(0,0,0),.016f,.008f,6,.2f);k.Pop();}
            // Wrist wraps: three tilted bands round the cuff.
            int wraps=k.Fine?4:k.Mid?2:1;
            for(int i=0;i<wraps;i++){float z=-.104f-i*.019f,g=1+.03f*((i*5)%3-1),c=.024f;var ring=new List<Vector3[]>{Rect(.162f*g,.142f*g,c,z-.0095f,0,-.016f),Rect(.17f*g,.15f*g,c,z-.005f,0,-.016f),Rect(.17f*g,.15f*g,c,z+.005f,0,-.016f),Rect(.162f*g,.142f*g,c,z+.0095f,0,-.016f)};k.Loft(ring,(a,b)=>i%2==0?cream:cream.Scaled(.88f),0,false,false);}
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.WeaponSculpt;

namespace ChipArena.UnityConversion
{
    // Standard guns. Every bore sits on y = 0 and ends just behind its muzzle socket (NativeWeaponDressing.MuzzleZ: pistol
    // .51, rifle 1.22, machine gun .91); the pistol grip keeps the Prototype's raked grip about the hand (the grip box at
    // (0,-.14,-.045) / (0,-.13,-.035) raked .22 / .2 rad), the support grip stays where the poses put the free hand, the
    // magazine stays inside its frozen socket (0,-.19,.16) and the long guns' stock butt at z -.30.
    public static partial class NativeWeaponModels
    {
        static partial void Guns(Dictionary<string,Design> d)
        {
            d["pistol"]=new Design{Spec="pistol",Build=Pistol,Muzzle=V(0,0,.498f),Support=V(.06f,-.10f,-.01f)};
            d["rifle"]=new Design{Spec="rifle",Build=Rifle,Muzzle=V(0,0,1.205f),Support=V(.025f,-.03f,.10f)};
            d["machinegun"]=new Design{Spec="machinegun",Build=MachineGun,Muzzle=V(0,0,.896f),Support=V(.025f,-.03f,.10f)};
        }
        // A thin dark line or slot on a face: `side` +1/-1 picks the +x / -x face at |x| = x (a decal lifted off it).
        private static void SideSlot(WeaponSculpt k,WeaponPaint c,float x,int side,float y,float z,float halfY,float halfZ)
        {
            var at=V(side*x,y,z);if(side>0)k.Decal(c,at,V(0,halfY,0),V(0,0,halfZ));else k.Decal(c,at,V(0,0,halfZ),V(0,halfY,0));
        }
        private static void TopSlot(WeaponSculpt k,WeaponPaint c,float x,float y,float z,float halfX,float halfZ,bool bottom=false)
        {
            var at=V(x,y,z);if(!bottom)k.Decal(c,at,V(0,0,halfZ),V(halfX,0,0));else k.Decal(c,at,V(halfX,0,0),V(0,0,halfZ));
        }
        // A disc facing +z (a bore, a lens, a sight dot) at depth z: a flat fan.
        private static void Disc(WeaponSculpt k,WeaponPaint c,float x,float y,float z,float r,int sides)
        {
            var ring=Circle(sides,r,r,z,0,x,y);var o=V(x,y,z);for(int i=0;i<sides;i++)k.Tri(c,o,ring[i],ring[(i+1)%sides]);
        }
        // The raked pistol grip, as a loft down its own axis (local +z is down the grip, local +y toward the muzzle): finger
        // grooves on the front strap, a beavertail at the top rear, a flared magwell and a base plate.
        private static void PistolGrip(WeaponSculpt k,Vector3 center,float rake,float length,float width,float depth,WeaponPaint shell,WeaponPaint panel,WeaponPaint plate,bool grooves,bool basePlate)
        {
            k.Push(Rx(Pi/2)*Rx(rake)*T(center.X,center.Y,center.Z));
            int rows=k.Fine?9:k.Mid?6:3;var rings=new List<Vector3[]>();float top=-length/2,bottom=length/2;
            for(int i=0;i<=rows;i++)
            {
                float u=(float)i/rows,z=top+u*length;float front=depth*.5f,back=depth*.5f;
                if(grooves&&i>0&&i<rows)front+=.006f*(float)Math.Cos(u*Pi*6);   // three finger grooves
                back+=.008f*(1-u);                                                   // fuller under the web of the hand
                float w=width*(i==rows?1.06f:1);if(i==rows){front+=.004f;back+=.004f;}  // magwell flare
                rings.Add(Rect(w,front+back,Math.Min(w,front+back)*.28f,z,0,(front-back)/2));
            }
            var s=shell;k.Loft(rings,(i,j)=>s,0);
            // Stippled side panels (darker inset plates) on both sides.
            if(k.Mid)for(int side=-1;side<=1;side+=2)
            {
                k.Box(panel,V(side*(width/2+.0004f),0,-.004f+length*.06f),V(.0016f,depth*.62f,length*.62f),0);
                if(k.Fine)for(int a=0;a<4;a++)for(int b=0;b<5;b++)k.Box(shell.Scaled(.8f),V(side*(width/2+.0016f),-depth*.24f+a*depth*.16f,-length*.22f+b*length*.11f),V(.0012f,.006f,.006f),0);
            }
            if(basePlate)
            {
                k.Box(plate,V(0,-.002f,bottom+.006f),V(width*1.12f,depth*1.12f,.014f),.004f);
                k.Box(AccentStrip,V(0,depth*.3f,bottom+.0135f),V(width*.7f,.006f,.002f),0);
            }
            k.Pop();
        }
        // Trigger guard (a flat loop under the frame) and the trigger blade.
        private static void TriggerGroup(WeaponSculpt k,float frameY,float back,float front,float drop,WeaponPaint guard,WeaponPaint trigger)
        {
            float y0=frameY,y1=frameY-drop;
            var path=new List<Vector3>{V(0,y0+.002f,back),V(0,y0-drop*.6f,back+.004f),V(0,y1+.008f,back+.02f),V(0,y1,back+(front-back)*.45f),V(0,y1+.006f,front-.02f),V(0,y0-drop*.4f,front-.004f),V(0,y0+.002f,front)};
            if(!k.Mid)path=new List<Vector3>{path[0],path[2],path[4],path[6]};
            k.Tube(_=>guard,path,_=>.0072f,4,Vector3.UnitX,0,true,false,1.3f,Pi/4);
            float tz=back+(front-back)*.38f;
            if(k.Mid)k.Tube(_=>trigger,new[]{V(0,y0+.001f,tz+.004f),V(0,y0-drop*.35f,tz+.006f),V(0,y0-drop*.68f,tz-.002f),V(0,y0-drop*.82f,tz-.01f)},i=>.0042f-i*.0006f,4,Vector3.UnitX,0,true,false,1.2f,Pi/4);
            else k.Box(trigger,V(0,y0-drop*.4f,tz),V(.008f,drop*.7f,.008f),0);
        }
        // A picatinny-style rail along z on top of a receiver (y is its base): a bar and, at full detail, its teeth.
        private static void Rail(WeaponSculpt k,WeaponPaint c,float y,float z0,float z1,float width)
        {
            k.Box(c,V(0,y+.006f,(z0+z1)/2),V(width,.012f,z1-z0),.003f);
            if(k.Fine){int n=(int)((z1-z0)/.024f);for(int i=0;i<n;i++)k.Box(c,V(0,y+.0135f,z0+.012f+i*.024f),V(width*1.02f,.004f,.012f),.0012f);}
        }

        // ---- pistol: a compact service sidearm with a two-tone slide, slide serrations, a compensator and glowing sights ----
        private static void Pistol(WeaponSculpt k)
        {
            var slide=Paint(0x7f949b,.05f);var slideTop=Paint(0x95a9af,.07f);var polymer=Paint(Polymer);var polyDark=Paint(PolymerDark);var black=Paint(Black);
            var metal=Paint(GunMetal,.03f);var groove=Paint(0x1a2328);var brass=Paint(Brass,.08f);
            // Slide: an angled top, dehorned nose and rear (8-point section), z -.078 .. .458.
            var section=Outline(0,.052f,-.008f,.052f,.058f,.039f,.082f,-.039f,.082f,-.052f,.058f,-.052f,-.008f,-.047f,-.012f,.047f,-.012f);
            var rings=new List<Vector3[]>{Moved(Scaled(section,1,1,-.078f),V(0,0,0),.9f),Scaled(section,1,1,-.071f),Scaled(section,1,1,.424f),Moved(Scaled(section,1,1,.452f),V(0,-.002f,0),.9f),Moved(Scaled(section,1,1,.458f),V(0,-.003f,0),.82f)};
            k.Loft(rings,(i,j)=>j==1||j==3?slideTop:j==2?slideTop.Scaled(.95f):slide,0);
            // Rear serrations (and front cocking serrations at full detail).
            int rear=k.Fine?8:k.Mid?5:0;for(int i=0;i<rear;i++)for(int s=-1;s<=1;s+=2)SideSlot(k,groove,.0521f,s,.028f,-.062f+i*.0115f,.024f,.0024f);
            if(k.Fine)for(int i=0;i<4;i++)for(int s=-1;s<=1;s+=2)SideSlot(k,groove,.0521f,s,.03f,.33f+i*.012f,.02f,.0022f);
            // Ejection port on the right (-x) with the barrel hood showing in it.
            if(k.Mid){TopSlot(k,groove,-.012f,.0822f,.15f,.022f,.05f);k.Box(brass,V(-.012f,.0815f,.15f),V(.03f,.002f,.07f),0);}
            // Sights: the rear notch with two accent dots, the front post with one (the emissive sight picture).
            if(k.Mid)for(int s=-1;s<=1;s+=2){k.Box(black,V(s*.022f,.09f,-.058f),V(.022f,.017f,.024f),.003f);k.Box(AccentStrip,V(s*.013f,.0905f,-.0706f),V(.0065f,.0065f,.0016f),0);}
            else k.Box(black,V(0,.09f,-.058f),V(.066f,.017f,.024f),0);
            k.Box(black,V(0,.0905f,.418f),V(.012f,.018f,.016f),.002f);k.Box(WeaponPaint.Accent(2.4f,.5f),V(0,.093f,.4096f),V(.0062f,.0062f,.0014f),0);
            // Hammer / striker cap at the back of the slide.
            k.Box(black,V(0,.05f,-.083f),V(.03f,.03f,.012f),.003f);
            // Compensator ahead of the slide with two top ports, the bore through its front face.
            var comp=new List<Vector3[]>{Moved(Scaled(section,.94f,.86f,.456f),V(0,.004f,0),1),Moved(Scaled(section,.94f,.86f,.49f),V(0,.004f,0),1),Moved(Scaled(section,.94f,.86f,.498f),V(0,.004f,0),.86f)};
            k.Loft(comp,(i,j)=>j==1||j==3?metal.Scaled(1.25f):metal,0);
            if(k.Mid)for(int i=0;i<2;i++)TopSlot(k,groove,0,.0752f,.466f+i*.016f,.016f,.0045f);
            if(k.Mid)for(int s=-1;s<=1;s+=2)SideSlot(k,groove,.0491f,s,.03f,.474f,.012f,.008f);
            Disc(k,black,0,0,.4982f,.011f,k.Sides(10));
            // Frame (polymer) under the slide: dust cover with an accessory rail, accent strips along both sides, slide stop.
            var frame=new List<Vector3[]>{Rect(.088f,.056f,.007f,-.064f,0,-.043f),Rect(.096f,.062f,.008f,-.058f,0,-.043f),Rect(.096f,.062f,.008f,.405f,0,-.043f),Rect(.086f,.054f,.012f,.434f,0,-.041f)};
            k.Loft(frame,(i,j)=>polymer,0);
            if(k.Mid)for(int i=0;i<3;i++)TopSlot(k,groove,0,-.0742f,.28f+i*.04f,.034f,.008f,true);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(AccentStrip,V(s*.0484f,-.034f,.2f),V(.0016f,.0045f,.33f),0);
            if(k.Fine){k.Box(black,V(.0505f,-.008f,.13f),V(.004f,.01f,.045f),.0015f);k.Box(black,V(.0495f,-.03f,.25f),V(.003f,.008f,.016f),.001f);}
            TriggerGroup(k,-.072f,.04f,.17f,.052f,polymer,black);
            // Raked grip with the beavertail over the web of the hand.
            PistolGrip(k,V(0,-.15f,-.05f),.22f,.215f,.098f,.112f,polymer,polyDark,metal,true,true);
            k.Box(polymer,V(0,-.038f,-.094f),V(.084f,.022f,.05f),.007f,-.32f);
        }

        // ---- rifle: a long chassis precision rifle (fluted barrel, muzzle brake, free-float handguard, scope, folded bipod) ----
        private static void Rifle(WeaponSculpt k)
        {
            var metal=Paint(0x2f3f47,.04f);var receiver=Paint(0x3b4e57,.03f);var guard=Paint(Guard,.05f);var steel=Paint(Steel,.1f);var black=Paint(Black);
            var polymer=Paint(Polymer);var polyDark=Paint(PolymerDark);var groove=Paint(0x121a1e);var rubber=Paint(Rubber);var gold=Paint(Gold,.12f);
            // Barrel: fluted (a 16-point star section) from the receiver to the brake, a touch of taper.
            int flutes=k.Fine?8:k.Mid?6:4;
            Vector3[] Star(float r,float z){var o=new Vector3[flutes*2];for(int i=0;i<o.Length;i++){double a=i*Math.PI/flutes;float rr=i%2==0?r:r*.84f;o[i]=new Vector3(rr*(float)Math.Cos(a),rr*(float)Math.Sin(a),z);}return o;}
            k.Loft(new[]{Star(.027f,.24f),Star(.026f,.8f),Star(.024f,1.1f)},(i,j)=>j%2==0?metal:metal.Scaled(.8f),0,false,false);
            // Muzzle brake: a chamfered block with three side ports a side and a top port; the crown just behind the socket.
            k.Box(metal,V(0,0,1.151f),V(.064f,.056f,.108f),.011f);
            if(k.Mid)for(int i=0;i<3;i++)for(int s=-1;s<=1;s+=2)SideSlot(k,groove,.032f,s,0,1.115f+i*.03f,.016f,.009f);
            if(k.Fine)TopSlot(k,groove,0,.0281f,1.17f,.014f,.016f);
            Disc(k,black,0,0,1.2052f,.0125f,k.Sides(10));
            // Free-float handguard (chamfered octagon) with M-LOK slots and an accent line down each side; the top rail runs on.
            var hg=new List<Vector3[]>{Rect(.08f,.084f,.02f,.255f,0,-.006f),Rect(.09f,.094f,.024f,.264f,0,-.006f),Rect(.09f,.094f,.024f,.79f,0,-.006f),Rect(.08f,.084f,.02f,.80f,0,-.006f)};
            k.Loft(hg,(i,j)=>j==2||j==6?guard.Scaled(1.1f):guard,0);
            if(k.Mid)for(int i=0;i<(k.Fine?5:3);i++){float z=k.Fine?.33f+i*.1f:.36f+i*.15f;for(int s=-1;s<=1;s+=2)SideSlot(k,groove,.0452f,s,-.008f,z,.01f,.028f);TopSlot(k,groove,0,-.0532f,z,.01f,.028f,true);}
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(AccentStrip,V(s*.0454f,.02f,.53f),V(.0016f,.004f,.5f),0);
            // Receiver with the bolt shroud, ejection port and bolt handle on the right (-x), and the long top rail.
            var rc=new List<Vector3[]>{Rect(.086f,.094f,.014f,-.118f,0,.0f),Rect(.096f,.104f,.017f,-.11f,0,.0f),Rect(.096f,.104f,.017f,.25f,0,.0f),Rect(.086f,.094f,.014f,.262f,0,.0f)};
            k.Loft(rc,(i,j)=>receiver,0);
            Rail(k,metal,.052f,-.1f,.78f,.036f);
            k.Push(T(0,.01f,0));k.Cylinder(steel,.026f,-.15f,-.11f,k.Sides(10),.004f);k.Pop();
            if(k.Mid)SideSlot(k,groove,.0482f,-1,.016f,.07f,.018f,.045f);
            k.Tube(_=>steel,new[]{V(-.044f,.018f,-.02f),V(-.068f,.012f,-.026f),V(-.092f,-.002f,-.034f)},_=>.0065f,k.Sides(6),Vector3.UnitY,1,true);
            k.Sphere(black,V(-.099f,-.006f,-.037f),V(.016f,.016f,.016f),k.Sides(6),k.Sides(8));
            // Magazine (inside the frozen magazine socket at y -.19, z .16) with an accent round-count window.
            k.Box(polyDark,V(0,-.1f,.085f),V(.06f,.12f,.09f),.007f,-.12f);
            if(k.Mid)k.Box(AccentStrip,V(.0308f,-.1f,.09f),V(.0016f,.07f,.006f),0,-.12f);
            k.Box(metal,V(0,-.164f,.078f),V(.066f,.012f,.1f),.004f,-.12f);
            TriggerGroup(k,-.044f,-.012f,.075f,.048f,metal,black);
            PistolGrip(k,V(0,-.135f,-.04f),.2f,.2f,.072f,.082f,polymer,polyDark,metal,true,false);
            // Chassis stock: cheek riser (accent stripe), lower spar, butt pad; the butt at z -.30 (the 'stock-butt' socket).
            k.Box(polymer,V(0,.018f,-.205f),V(.064f,.05f,.19f),.014f);
            k.Box(polyDark,V(0,.049f,-.215f),V(.056f,.016f,.15f),.007f);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(AccentStrip,V(s*.0322f,.022f,-.205f),V(.0016f,.004f,.16f),0);
            k.Box(polymer,V(0,-.085f,-.2f),V(.054f,.034f,.17f),.01f,.18f);
            k.Box(polymer,V(0,-.03f,-.128f),V(.06f,.13f,.03f),.009f);
            k.Box(rubber,V(0,-.03f,-.296f),V(.066f,.19f,.024f),.01f);
            if(k.Fine)for(int i=0;i<4;i++)k.Box(rubber.Scaled(1.6f),V(0,-.1f+i*.045f,-.3085f),V(.046f,.006f,.002f),0);
            // Scope (bore line y .175): tube, objective bell with a glowing lens, eyepiece, turrets, rings down to the rail.
            float sy=.175f;k.Push(T(0,sy,0));
            k.Lathe(i=>i==0?rubber:i==1?black:metal,new[]{.037f,-.135f,.039f,-.105f,.031f,-.085f,.031f,.215f,.047f,.26f,.052f,.3f,.049f,.31f},k.Sides(14),1,0,true);
            k.Push(T(0,0,.3105f));Disc(k,WeaponPaint.Glow(0x86ddd8,1.2f,.4f),0,0,0,.041f,k.Sides(14));k.Pop();
            if(k.Mid){k.Torus(AccentStrip,V(0,0,.285f),.0525f,.003f,k.Sides(18),4);}
            k.Push(Rx(-Pi/2)*T(0,.031f,.1f));k.Lathe(i=>i==1?gold:metal,new[]{.017f,0,.019f,.004f,.019f,.022f,.017f,.026f},k.Sides(10),1,0,true);k.Pop();
            k.Push(Ry(-Pi/2)*T(-.031f,0,.1f));k.Lathe(metal,new[]{.014f,0,.015f,.004f,.015f,.018f},k.Sides(10),1,0,true);k.Pop();
            k.Pop();
            for(int i=0;i<2;i++){float z=i==0?-.02f:.17f;k.Box(metal,V(0,.106f,z),V(.04f,.07f,.024f),.005f);if(k.Mid)k.Torus(metal,V(0,sy,z),.0345f,.0055f,k.Sides(14),4);}
            // Bipod folded under the handguard: pivot block and two legs back along it, feet forward.
            k.Box(metal,V(0,-.064f,.75f),V(.05f,.024f,.03f),.005f);
            if(k.Mid)for(int s=-1;s<=1;s+=2){k.Cylinder(steel,.0068f,.52f,.745f,k.Sides(6),0,s*.018f,-.066f);k.Box(rubber,V(s*.018f,-.066f,.515f),V(.016f,.014f,.016f),.003f);}
            else k.Box(steel,V(0,-.066f,.63f),V(.044f,.012f,.23f),0);
        }

        // ---- machine gun: a heavy belt-fed gun (vented shroud, flash hider, carry handle, ammo drum and a short belt) ----
        private static void MachineGun(WeaponSculpt k)
        {
            var metal=Paint(0x2f3f47,.04f);var receiver=Paint(0x3a4c55,.03f);var steel=Paint(Steel,.1f);var black=Paint(Black);var polymer=Paint(Polymer);var polyDark=Paint(PolymerDark);
            var groove=Paint(0x0e1417);var rubber=Paint(Rubber);var olive=Paint(0x47503c,.02f);var brass=Paint(Brass,.15f);var shroud=Paint(0x3c4c54,.05f);
            // Receiver: a tall chamfered box with the feed cover (raised, with its latch), side panel lines and rivets.
            var rc=new List<Vector3[]>{Rect(.14f,.198f,.02f,-.145f,0,.012f),Rect(.152f,.21f,.024f,-.135f,0,.012f),Rect(.152f,.21f,.024f,.3f,0,.012f),Rect(.14f,.196f,.02f,.318f,0,.012f)};
            k.Loft(rc,(i,j)=>receiver,0);
            k.Box(metal,V(0,.125f,.07f),V(.13f,.022f,.27f),.008f);k.Box(steel,V(0,.14f,-.06f),V(.04f,.012f,.03f),.004f);
            if(k.Mid)for(int s=-1;s<=1;s+=2){SideSlot(k,groove,.0762f,s,.02f,.08f,.002f,.2f);SideSlot(k,groove,.0762f,s,-.04f,.08f,.002f,.2f);k.Box(AccentStrip,V(s*.0764f,-.012f,.08f),V(.0016f,.006f,.36f),0);}
            if(k.Fine)for(int s=-1;s<=1;s+=2)for(int i=0;i<5;i++)for(int j=0;j<2;j++){k.Push(Ry(s*Pi/2)*T(s*.076f,.05f-j*.12f,-.1f+i*.09f));k.Cylinder(steel,.005f,0,.003f,6,0);k.Pop();}
            // Barrel shroud: a vented tube with bevelled rings, then the gas block, front sight, barrel and flash hider.
            int sides=k.Sides(12);k.Push(T(0,.012f,0));
            k.Lathe(i=>i==1?shroud:metal,new[]{.056f,.3f,.058f,.32f,.054f,.33f,.054f,.68f,.058f,.69f,.056f,.71f},sides,1,0,true);
            if(k.Mid){int rows=k.Fine?5:3,around=k.Fine?8:6;for(int i=0;i<rows;i++)for(int j=0;j<around;j++){double a=(j+.5)*Math.PI*2/around;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);var at=n*.0545f+V(0,0,.37f+i*(.28f/(rows-1)));var u=V(0,0,.016f);var v=Vector3.Cross(n,u)/(.016f)*.007f;k.Decal(groove,at,u,v,.0012f);}}
            k.Pop();
            k.Box(metal,V(0,-.004f,.73f),V(.044f,.05f,.05f),.006f);k.Box(black,V(0,.05f,.715f),V(.008f,.05f,.012f),.002f);
            k.Cylinder(metal,.019f,.71f,.84f,k.Sides(10),0);
            k.Lathe(metal,new[]{.022f,.835f,.026f,.842f,.026f,.894f,.023f,.896f},k.Sides(10),1,0,true);
            if(k.Mid)for(int j=0;j<4;j++){double a=j*Math.PI/2+Math.PI/4;var n=V((float)Math.Cos(a),(float)Math.Sin(a),0);var u=V(0,0,.018f);k.Decal(groove,n*.0262f+V(0,0,.87f),u,Vector3.Cross(n,u)/.018f*.0035f,.001f);}
            Disc(k,black,0,0,.8962f,.012f,k.Sides(10));
            // Carry handle arching over the receiver (grip sleeve on it).
            var handle=new List<Vector3>{V(0,.136f,-.01f),V(0,.18f,.0f),V(0,.205f,.05f),V(0,.205f,.15f),V(0,.18f,.2f),V(0,.136f,.215f)};
            k.Tube(_=>steel,handle,_=>.0095f,k.Sides(6),Vector3.UnitX,1,true);
            if(k.Mid)k.Cylinder(polyDark,.0145f,.06f,.14f,k.Sides(8),.003f,0,.205f);
            // Ammo drum (axis x) under the receiver at the magazine socket, feed neck, counter window and a short belt.
            k.Push(Ry(Pi/2)*T(0,-.19f,.2f));
            k.Lathe(i=>i==2?olive.Scaled(.85f):olive,new[]{.09f,-.058f,.104f,-.052f,.108f,-.04f,.108f,.04f,.104f,.052f,.09f,.058f},k.Sides(16),1,0,true);
            if(k.Mid){k.Lathe(metal,new[]{.04f,.056f,.046f,.062f,.04f,.068f},k.Sides(10),1,0,true);k.Lathe(metal,new[]{.04f,-.068f,.046f,-.062f,.04f,-.056f},k.Sides(10),1,0,true);}
            k.Pop();
            if(k.Mid){k.Torus(AccentStrip,V(.0592f,-.19f,.2f),.068f,.0026f,k.Sides(20),4);k.Box(AccentStrip,V(.0596f,-.13f,.2f),V(.002f,.012f,.03f),0);}
            k.Box(olive,V(0,-.105f,.215f),V(.07f,.04f,.07f),.006f);
            if(k.Mid)
            {
                int rounds=k.Fine?6:4;
                for(int i=0;i<rounds;i++)
                {
                    float t=i/(float)(rounds-1),z=.12f-t*.05f,y=-.105f+t*.09f,x=.085f;
                    k.Box(black,V(x,y,z),V(.02f,.016f,.014f),.003f);
                    k.Push(Ry(Pi/2)*T(x-.028f,y,z));k.Lathe(i2=>i2==0?brass:brass.Scaled(.85f),new[]{.006f,0,.006f,.04f,.0035f,.055f},k.Sides(6),1,0,true);k.Pop();
                }
            }
            TriggerGroup(k,-.094f,-.02f,.07f,.05f,metal,black);
            PistolGrip(k,V(0,-.18f,-.045f),.2f,.16f,.062f,.072f,polymer,polyDark,metal,true,false);
            // Stock: a buffer tube, a skeleton stock (top bar, lower spar, butt) with the butt pad at z -.30.
            k.Cylinder(metal,.02f,-.2f,-.135f,k.Sides(10),.004f,0,.03f);
            k.Box(polymer,V(0,.032f,-.225f),V(.056f,.04f,.17f),.01f);
            k.Box(polymer,V(0,-.075f,-.22f),V(.05f,.034f,.17f),.009f,.22f);
            k.Box(polymer,V(0,-.02f,-.283f),V(.058f,.17f,.03f),.01f);
            k.Box(rubber,V(0,-.02f,-.302f),V(.06f,.18f,.014f),.006f);
            if(k.Mid)for(int s=-1;s<=1;s+=2)k.Box(AccentStrip,V(s*.0285f,.036f,-.225f),V(.0016f,.004f,.13f),0);
            // Bipod folded under the shroud front.
            k.Box(metal,V(0,-.05f,.64f),V(.05f,.026f,.034f),.005f);
            if(k.Mid)for(int s=-1;s<=1;s+=2){k.Cylinder(steel,.0068f,.4f,.635f,k.Sides(6),0,s*.017f,-.052f);k.Box(rubber,V(s*.017f,-.052f,.395f),V(.016f,.014f,.016f),.004f);}
            else k.Box(steel,V(0,-.052f,.52f),V(.04f,.012f,.24f),0);
        }
    }
}

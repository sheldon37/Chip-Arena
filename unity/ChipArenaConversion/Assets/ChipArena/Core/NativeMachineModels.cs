using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The PvE machines and the dispensed chip, sculpted with the Standard weapons' kit (NativeWeaponSculpt) in place of the
    // source's merged boxes (src/client/pve/machines.ts, chip-model.ts): graphite chassis with chamfered panels, brushed steel
    // trim, black glass and thin light strips in each machine's colour (chip violet, recruit cyan, med mint). Authored in the
    // Unity model frame the bank's machine models use (origin on the floor at the spot, +z toward the hall, +y up; no mirror),
    // so the Runtime uploads the sections as they are (Runtime/NativeMachineModels.cs). The bank's part convention holds: bone
    // 0 the lit body, bone 1 the lights the draw tints (machine activity and the deny flash; the chip's rarity), and the
    // moving parts keep the source pivots, so PveMachinePresentation's timelines drive them unchanged. Everything stays inside
    // the machine's collider (PveContent machineBlocks: 1.3 x 2.2 x 0.9 at spot - fwd x .2) apart from the shallow front trim
    // and the recruit's crown and emitter above it. Engine-free and deterministic: the checks build what the player builds.
    public static class NativeMachineModels
    {
        public const string Chip="chip",Recruit="recruit",Med="med";
        public static readonly string[] Kinds={Chip,Recruit,Med};
        public static string BodyId(string kind)=>"machine-std-"+kind;
        public static string MovingId(string kind)=>"machine-std-moving-"+kind;
        public const string PickupId="pve-pickup-std";
        /// <summary>Section bones: the body, the tinted lights, and the see-through parts of a moving model (glass, the pulse).</summary>
        public const int Body=0,Lights=1,Glass=-2;
        // Model-local points (machines.ts SLOT and MachineModel's pivots): the chip slot, the flap hinge, the pod door's hinge and
        // the med pulse's centre.
        public static readonly Vector3 Slot=new Vector3(0,1.02f,.27f),FlapPivot=new Vector3(0,1.12f,.27f),DoorPivot=new Vector3(.49f,0,.22f),PulsePivot=new Vector3(0,1.55f,.2f);
        /// <summary>The machine collider in model space (the hidden machineBlocks box).</summary>
        public static readonly Vector3 BlockMin=new Vector3(-.65f,0,-.65f),BlockMax=new Vector3(.65f,2.2f,.25f);
        /// <summary>How far the models may leave the collider: front trim (catch tray, bezels) and the recruit's crown.</summary>
        public const float FrontAllowance=.075f,TopAllowance=.22f;
        /// <summary>Alpha of the see-through parts (the bank's colour alpha, read by NativeSourceTransparent).</summary>
        public const float GlassAlpha=.2f,FrostAlpha=.42f,PulseAlpha=.75f,HaloAlpha=.22f,DisplayAlpha=.1f,GlintAlpha=.16f;

        private static readonly WeaponPaint Shell=WeaponPaint.Solid(0x2b2c35,.03f),ShellLight=WeaponPaint.Solid(0x3e404c,.04f),ShellDark=WeaponPaint.Solid(0x18191f,.02f),
            Trim=WeaponPaint.Solid(0x9ba2af,.06f),TrimDark=WeaponPaint.Solid(0x5c6270,.04f),Black=WeaponPaint.Solid(0x07080b),Rubber=WeaponPaint.Solid(0x101014),
            Enamel=WeaponPaint.Solid(0xe2e6ea,.03f),Gold=WeaponPaint.Solid(0xd9b15a,.14f),Spreader=WeaponPaint.Solid(0x6f7684,.06f);
        private const int Violet=0x8f6bff,VioletSoft=0x6f55c8,VioletInk=0xd8ccff,Cyan=0x4fe3ff,CyanSoft=0x1d7f99,CyanInk=0xb4f6ff,Mint=0x5dffa8,MintDeep=0x0c3a28,MintInk=0xc4ffe0,Red=0xff3048;
        /// <summary>The six rarity colours of the source's lamp strip (content.ts RARITIES).</summary>
        public static readonly int[] RarityColors={0xf1f5f9,0x79e59a,0x70b5ff,0xce8aff,0xffae55,0xff6475};
        private static WeaponPaint Light(int hex,float strength=1.6f,float albedo=.35f)=>WeaponPaint.Glow(hex,strength,albedo);
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Up=-(float)(Math.PI/2);

        /// <summary>The body of a machine kind (bones 0 and 1; the chip display's glass on bone -2), or null for an unknown kind.</summary>
        public static WeaponMesh Build(string kind)
        {
            var mesh=new WeaponMesh{Spec="machine-"+kind,Tier=0};var s=new WeaponSculpt(mesh,0);
            if(kind==Chip)ChipBody(s,mesh);else if(kind==Recruit)RecruitBody(s);else if(kind==Med)MedBody(s);else return null;
            return mesh;
        }
        /// <summary>A machine's moving part about its pivot (opaque bone -1, see-through bone -2), or null.</summary>
        public static WeaponMesh Moving(string kind)
        {
            var mesh=new WeaponMesh{Spec="machine-moving-"+kind,Tier=0};var s=new WeaponSculpt(mesh,0);
            if(kind==Chip)Flap(s);else if(kind==Recruit)Door(s,mesh);else if(kind==Med)Pulse(s,mesh);else return null;
            return mesh;
        }

        // ---- shared pieces ----
        // The footing: a chamfered plinth on a shadow gap, the machine's colour along its front edge.
        private static void Plinth(WeaponSculpt s,int glow)
        {
            s.Into(Body);s.Box(ShellDark,V(0,.075f,-.2f),V(1.28f,.13f,.86f),.03f);s.Box(Rubber,V(0,.01f,-.2f),V(1.2f,.02f,.8f),.004f);
            s.Box(TrimDark,V(0,.145f,-.2f),V(1.2f,.012f,.8f),.004f);
            s.Into(Lights);s.Box(Light(glow,1.9f,.4f),V(0,.08f,.233f),V(1f,.014f,.008f));
        }
        // A chamfered panel facing +z: outline w x h centred at (cx, cy), between z0 and z1.
        private static void Panel(WeaponSculpt s,in WeaponPaint paint,float w,float h,float corner,float cx,float cy,float z0,float z1,float bevel)
        {var p=paint;s.Slab(_=>p,p,p,WeaponSculpt.Rect(w,h,corner,0,cx,cy),z0,z1,bevel);}
        // A vertical cylinder (axis +y) of radius r from y0 to y1 at (x, z).
        private static void Column(WeaponSculpt s,in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides,float bevel=0)
        {s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,0,z));s.Cylinder(c,r,y0,y1,sides,bevel);s.Pop();}
        // A flat ring (annulus) in the xy plane at depth z facing +z, r0 to r1.
        private static void Ring(WeaponSculpt s,in WeaponPaint c,float r0,float r1,float z,int segments,float cx=0,float cy=0)
        {
            var a=WeaponSculpt.Circle(segments,r0,r0,z,0,cx,cy);var b=WeaponSculpt.Circle(segments,r1,r1,z,0,cx,cy);
            for(int i=0;i<segments;i++){int j=(i+1)%segments;s.Quad(c,a[i],b[i],b[j],a[j]);}
        }
        // A thin line from a to b (in the xy plane at depth z) as a box of the given width and depth.
        private static void Line(WeaponSculpt s,in WeaponPaint c,Vector2 a,Vector2 b,float z,float width,float depth)
        {
            var d=b-a;float len=d.Length();if(len<1e-5f)return;var m=(a+b)*.5f;
            s.Box(c,V(m.X,m.Y,z),V(len+width,width,depth),0,0,0,(float)Math.Atan2(d.Y,d.X));
        }
        // A standing dispenser chip (the display's stock): package, lit die in `rarity`, gold leads.
        private static void ShelfChip(WeaponSculpt s,float x,float y,float z,int rarity)
        {
            s.Push(WeaponSculpt.Rx(-.22f)*WeaponSculpt.T(x,y,z));
            s.Into(Body);s.Box(ShellDark,V(0,0,0),V(.1f,.1f,.014f),.005f);s.Box(Trim,V(0,0,.008f),V(.066f,.066f,.004f),.0015f);
            for(int i=-1;i<=1;i++){s.Box(Gold,V(i*.026f,.056f,0),V(.009f,.014f,.004f));s.Box(Gold,V(i*.026f,-.056f,0),V(.009f,.014f,.004f));}
            s.Into(Lights);s.Box(Light(rarity,1.7f,.4f),V(0,0,.0105f),V(.034f,.034f,.003f));
            s.Pop();
        }

        // ---- the chip dispenser: a display of stock behind a steel-framed window, the six rarity lamps, the slot and its tray --
        private static void ChipBody(WeaponSculpt s,WeaponMesh mesh)
        {
            Plinth(s,Violet);
            s.Into(Body);
            // Chassis, the raised fascia and the side cheeks (two-tone), the crown with its sign.
            Panel(s,Shell,1.14f,1.94f,.07f,0,1.12f,-.6f,.14f,.025f);Panel(s,ShellDark,1.02f,1.8f,.05f,0,1.13f,.14f,.17f,.01f);
            foreach(int side in new[]{-1,1}){s.Box(ShellLight,V(side*.578f,1.12f,-.23f),V(.035f,1.86f,.66f),.012f);s.Box(TrimDark,V(side*.535f,1.12f,.155f),V(.02f,1.82f,.03f),.006f);}
            s.Box(Shell,V(0,2.12f,-.2f),V(1.2f,.16f,.88f),.035f);s.Box(ShellDark,V(0,2.205f,-.22f),V(1.04f,.012f,.72f),.004f);
            s.Box(Black,V(0,2.12f,.243f),V(.86f,.1f,.01f),.003f);
            // The display: a steel bezel, three shelves of stock (every rarity, staggered), two reflections on the glass.
            s.Box(Trim,V(0,1.99f,.192f),V(.92f,.04f,.045f),.01f);s.Box(Trim,V(0,1.33f,.192f),V(.92f,.04f,.045f),.01f);
            foreach(int side in new[]{-1,1})s.Box(Trim,V(side*.44f,1.66f,.192f),V(.04f,.7f,.045f),.01f);
            for(int r=0;r<3;r++)
            {
                float y=1.43f+r*.2f;s.Box(Trim,V(0,y,.19f),V(.82f,.014f,.035f),.004f);
                for(int c=0;c<4;c++)ShelfChip(s,-.3f+c*.2f+(r%2==1?.05f:0),y+.065f,.186f,RarityColors[(r*4+c+r)%6]);
                s.Into(Lights);s.Box(Light(VioletInk,1.3f,.4f),V(0,y-.006f,.2075f),V(.8f,.005f,.004f));s.Into(Body);
            }
            // The rarity lamps in steel bezels.
            for(int i=0;i<6;i++){float x=-.3f+i*.12f;s.Into(Body);s.Cylinder(Trim,.03f,.17f,.19f,16,.004f,x,1.24f);s.Into(Lights);s.Cylinder(Light(RarityColors[i],1.7f,.4f),.021f,.19f,.198f,16,0,x,1.24f);}
            // The slot: black mouth, steel bezel, the catch tray under the flap (MachineModel's SLOT at y 1.02, z .27).
            s.Into(Body);Panel(s,Black,.56f,.2f,.04f,0,1.02f,.17f,.2f,.004f);
            s.Box(Trim,V(0,1.137f,.205f),V(.64f,.035f,.05f),.01f);s.Box(Trim,V(0,.903f,.205f),V(.64f,.035f,.05f),.01f);
            foreach(int side in new[]{-1,1})s.Box(Trim,V(side*.302f,1.02f,.205f),V(.035f,.27f,.05f),.01f);
            s.Box(Trim,V(0,.872f,.25f),V(.56f,.03f,.12f),.008f);s.Box(TrimDark,V(0,.89f,.25f),V(.5f,.006f,.1f));
            // Credit reader (screen, card slot, status lamp) and a speaker grille either side under the slot.
            s.Box(ShellDark,V(-.33f,.62f,.18f),V(.22f,.16f,.02f),.006f);s.Box(Black,V(-.33f,.48f,.178f),V(.14f,.022f,.02f),.004f);
            for(int i=0;i<6;i++)s.Box(TrimDark,V(.33f,.5f+i*.036f,.176f),V(.2f,.012f,.012f),.003f);
            // Rear conduits.
            foreach(int side in new[]{-1,1}){Column(s,TrimDark,.028f,side*.42f,-.615f,.2f,2.02f,10,.004f);for(int k=0;k<3;k++)Column(s,Trim,.034f,side*.42f,-.615f,.45f+k*.6f,.49f+k*.6f,10,.004f);}
            // Lights: the window's back glass, its lower band, the slot's glow and tray lip, the reader, the edge strips, the sign.
            s.Into(Lights);
            s.Box(Light(VioletSoft,.75f,.22f),V(0,1.66f,.173f),V(.84f,.62f,.004f));s.Box(Light(Violet,1.1f,.3f),V(0,1.38f,.1755f),V(.84f,.07f,.003f));
            s.Box(Light(Violet,1.3f,.35f),V(0,.945f,.2015f),V(.48f,.01f,.004f));s.Box(Light(VioletInk,1.6f,.4f),V(0,.872f,.3115f),V(.5f,.01f,.004f));
            s.Box(Light(VioletSoft,.6f,.2f),V(-.33f,.62f,.191f),V(.18f,.12f,.004f));
            float[] bars={.12f,.07f,.1f};for(int i=0;i<3;i++)s.Box(Light(VioletInk,1.3f,.4f),V(-.39f+bars[i]*.5f,.655f-i*.032f,.1935f),V(bars[i],.01f,.003f));
            s.Box(Light(Red,1.6f,.4f),V(-.24f,.48f,.19f),V(.016f,.016f,.004f));
            foreach(int side in new[]{-1,1}){s.Box(Light(Violet,1.6f,.4f),V(side*.5f,1.13f,.1755f),V(.012f,1.72f,.012f));s.Box(Light(Violet,1.2f,.35f),V(side*.597f,1.12f,-.06f),V(.006f,1.5f,.012f));}
            s.Box(Light(Violet,1.8f,.4f),V(-.05f,2.12f,.2495f),V(.5f,.022f,.004f));
            s.Box(Light(Violet,1.8f,.4f),V(.34f,2.12f,.2495f),V(.05f,.05f,.004f));
            // The display's glass: clear, with two soft glints.
            s.Into(Glass);Clear(mesh,DisplayAlpha,()=>s.Box(WeaponPaint.Glow(0xd8e4ff,.02f,.35f),V(0,1.66f,.211f),V(.84f,.62f,.004f)));
            Clear(mesh,GlintAlpha,()=>{s.Box(WeaponPaint.Glow(0xffffff,.25f,.6f),V(-.17f,1.7f,.2135f),V(.05f,.5f,.001f),0,0,0,-.5f);s.Box(WeaponPaint.Glow(0xffffff,.25f,.6f),V(-.07f,1.68f,.2135f),V(.018f,.42f,.001f),0,0,0,-.5f);});
            s.Into(Lights);
            for(int i=-1;i<=1;i++){s.Box(Light(VioletInk,1.4f,.4f),V(.34f+i*.015f,2.153f,.2495f),V(.006f,.014f,.004f));s.Box(Light(VioletInk,1.4f,.4f),V(.34f+i*.015f,2.087f,.2495f),V(.006f,.014f,.004f));}
            for(int i=0;i<3;i++)s.Box(Light(i==0?Mint:VioletInk,1.5f,.4f),V(-.37f+i*.025f,2.12f,.2495f),V(.012f,.012f,.004f));
        }
        // The slot flap about FlapPivot: a dark plate with a steel lip and a violet seam.
        private static void Flap(WeaponSculpt s)
        {
            s.Into(-1);s.Box(ShellDark,V(0,-.085f,0),V(.5f,.16f,.022f),.006f);s.Box(Trim,V(0,-.158f,.006f),V(.5f,.016f,.03f),.005f);
            s.Box(Light(Violet,1.5f,.4f),V(0,-.142f,.0125f),V(.44f,.006f,.004f));
        }

        // ---- the recruit pod: an open-fronted chamber between lit pillars, a glass door hinged on its +x side, an emitter crown --
        private static void RecruitBody(WeaponSculpt s)
        {
            Plinth(s,Cyan);
            s.Into(Body);
            s.Box(Shell,V(0,1.15f,-.58f),V(1.2f,2.0f,.12f),.02f);
            foreach(int side in new[]{-1,1})
            {
                s.Box(ShellLight,V(side*.575f,1.15f,-.21f),V(.15f,2.0f,.84f),.03f);s.Box(Trim,V(side*.615f,1.15f,.212f),V(.012f,1.72f,.01f),.003f);
                for(int k=0;k<4;k++)s.Box(ShellDark,V(side*.575f,.45f+k*.4f,-.21f),V(.15f,.014f,.844f),.002f);
            }
            s.Box(Black,V(-.575f,1.45f,.216f),V(.1f,.24f,.006f),.002f);
            // Crown and sign, the emitter dome on its steel ring.
            s.Box(Shell,V(0,2.24f,-.2f),V(1.3f,.2f,.9f),.035f);s.Box(Black,V(0,2.24f,.255f),V(.9f,.1f,.01f),.003f);
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,2.34f,-.2f));s.Torus(Trim,V(0,0,0),.11f,.016f,24,8);s.Pop();
            // The chamber: floor pad, ceiling emitter frame, back ribs over the scan glass.
            Column(s,ShellDark,.4f,0,-.2f,.14f,.17f,32,.006f);
            s.Box(Trim,V(0,2.125f,-.2f),V(.98f,.03f,.68f),.006f);
            foreach(int side in new[]{-1,1})s.Box(Trim,V(side*.47f,1.15f,-.505f),V(.03f,1.84f,.03f),.006f);
            s.Into(Lights);
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,.172f,-.2f));s.Torus(Light(CyanInk,1.9f,.45f),V(0,0,0),.34f,.012f,40,6);s.Torus(Light(Cyan,1.2f,.35f),V(0,0,0),.3f,.006f,40,4);s.Cylinder(Light(CyanSoft,.8f,.25f),.09f,0,.006f,24);s.Pop();
            s.Box(Light(CyanSoft,.9f,.25f),V(0,2.105f,-.2f),V(.9f,.012f,.6f));
            s.Box(Light(CyanSoft,.45f,.12f),V(0,1.15f,-.515f),V(.9f,1.84f,.008f));s.Box(Light(Cyan,.6f,.2f),V(0,.4f,-.5105f),V(.9f,.2f,.002f));s.Box(Light(Cyan,.35f,.15f),V(0,.62f,-.511f),V(.9f,.24f,.002f));s.Box(Light(CyanSoft,.5f,.15f),V(0,.88f,-.5112f),V(.9f,.28f,.002f));
            for(int i=0;i<7;i++)s.Box(Light(Cyan,.9f,.3f),V(0,.45f+i*.24f,-.5095f),V(.9f,.004f,.002f));s.Box(Light(CyanInk,1.6f,.45f),V(0,1.32f,-.509f),V(.9f,.012f,.004f));
            foreach(int side in new[]{-1,1})s.Box(Light(Cyan,1.6f,.4f),V(side*.535f,1.15f,.215f),V(.012f,1.7f,.01f));
            for(int i=0;i<4;i++)s.Box(Light(Cyan,1.3f,.4f),V(-.575f,1.37f+i*.05f,.2195f),V(.07f,.012f,.003f));
            s.Box(Light(CyanInk,1.8f,.4f),V(-.05f,2.24f,.2615f),V(.5f,.022f,.004f));
            s.Cylinder(Light(Cyan,1.8f,.4f),.018f,.256f,.2635f,16,0,.34f,2.262f);s.Box(Light(Cyan,1.8f,.4f),V(.34f,2.222f,.2615f),V(.045f,.032f,.004f),.004f);
            s.Push(WeaponSculpt.Rx(Up));s.Sphere(Light(Cyan,1.4f,.5f),V(0,.2f,2.34f),V(.09f,.09f,.07f),8,20);s.Pop();
        }
        // The door about DoorPivot (it spans x -0.98..0 from the hinge): steel frame, handle, a lit leading edge and the glass.
        private static void Door(WeaponSculpt s,WeaponMesh mesh)
        {
            s.Into(-1);
            s.Box(Shell,V(-.025f,1.15f,0),V(.05f,1.86f,.05f),.01f);s.Box(Shell,V(-.955f,1.15f,0),V(.05f,1.86f,.05f),.01f);
            s.Box(Shell,V(-.49f,2.055f,0),V(.98f,.05f,.05f),.01f);s.Box(Shell,V(-.49f,.245f,0),V(.98f,.05f,.05f),.01f);
            s.Box(Trim,V(-.49f,2.083f,.0f),V(.9f,.008f,.052f),.002f);s.Box(Trim,V(-.49f,.217f,0),V(.9f,.008f,.052f),.002f);
            s.Box(Trim,V(-.9f,1.15f,.042f),V(.025f,.34f,.035f),.006f);foreach(int k in new[]{-1,1})s.Box(TrimDark,V(-.9f,1.15f+k*.15f,.022f),V(.02f,.02f,.02f),.004f);
            s.Box(Light(Cyan,1.4f,.4f),V(-.978f,1.15f,.026f),V(.008f,1.7f,.008f));
            s.Into(Glass);
            Clear(mesh,GlassAlpha,()=>s.Box(WeaponPaint.Glow(0x7fe8ff,.12f,.55f),V(-.49f,1.15f,0),V(.88f,1.76f,.008f)));
            Clear(mesh,FrostAlpha,()=>s.Box(WeaponPaint.Glow(0xbff6ff,.12f,.55f),V(-.49f,.36f,0),V(.88f,.18f,.012f)));
        }

        // ---- the med station: a clean enamel cabinet, the lit cross in a steel ring, vitals, the dispenser alcove and tray --
        private static void MedBody(WeaponSculpt s)
        {
            Plinth(s,Mint);
            s.Into(Body);
            Panel(s,Enamel,1.1f,1.92f,.1f,0,1.1f,-.6f,.1f,.03f);
            foreach(int side in new[]{-1,1})s.Box(Shell,V(side*.565f,1.1f,-.25f),V(.04f,1.86f,.68f),.012f);
            s.Box(Shell,V(0,2.12f,-.24f),V(1.16f,.14f,.8f),.03f);
            // The emblem housing (MachineModel's pulse centre), the vitals screen, the alcove and tray, the grab bar.
            s.Cylinder(Trim,.3f,.1f,.14f,40,.01f,0,1.55f);s.Cylinder(ShellDark,.27f,.14f,.15f,40,0,0,1.55f);
            s.Box(ShellDark,V(0,1f,.115f),V(.56f,.28f,.03f),.01f);
            s.Box(Black,V(0,.5f,.112f),V(.46f,.32f,.026f),.008f);Column(s,Trim,.028f,0,.16f,.56f,.66f,12,.004f);Column(s,Trim,.04f,0,.16f,.64f,.68f,12,.004f);s.Box(Trim,V(0,.67f,.13f),V(.05f,.03f,.07f),.006f);
            s.Box(Trim,V(0,.37f,.16f),V(.42f,.03f,.15f),.008f);
            s.Box(Trim,V(-.6f,1.3f,.05f),V(.025f,.5f,.025f),.006f);foreach(int k in new[]{-1,1})s.Box(TrimDark,V(-.585f,1.3f+k*.22f,.05f),V(.03f,.03f,.04f),.004f);
            s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(0,2.19f,-.24f));s.Torus(Trim,V(0,0,0),.07f,.012f,20,6);s.Pop();
            s.Into(Lights);
            s.Box(Light(Mint,1.3f,.4f),V(0,1.55f,.154f),V(.34f,.1f,.008f));s.Box(Light(Mint,1.3f,.4f),V(0,1.55f,.154f),V(.1f,.34f,.008f));
            s.Box(Light(MintInk,1.7f,.5f),V(0,1.55f,.159f),V(.28f,.05f,.004f));s.Box(Light(MintInk,1.7f,.5f),V(0,1.55f,.159f),V(.05f,.28f,.004f));
            Ring(s,Light(Mint,1.6f,.4f),.278f,.292f,.1505f,48,0,1.55f);
            s.Box(Light(MintDeep,.35f,.1f),V(0,1f,.131f),V(.5f,.22f,.004f));
            var beat=new[]{new Vector2(-.22f,0),new Vector2(-.1f,0),new Vector2(-.07f,.045f),new Vector2(-.04f,-.06f),new Vector2(-.01f,.08f),new Vector2(.02f,-.025f),new Vector2(.05f,0),new Vector2(.2f,0)};
            for(int i=0;i+1<beat.Length;i++)Line(s,Light(MintInk,2f,.45f),beat[i]+new Vector2(0,1.02f),beat[i+1]+new Vector2(0,1.02f),.1345f,.008f,.003f);
            float[] read={.06f,.1f,.04f};for(int i=0;i<3;i++)s.Box(Light(Mint,1.2f,.35f),V(-.2f+i*.09f+read[i]*.5f,.915f,.1345f),V(read[i],.01f,.003f));
            s.Push(WeaponSculpt.Rx(Up));s.Sphere(Light(Mint,1.8f,.5f),V(0,-.16f,.555f),V(.02f,.02f,.02f),6,12);s.Pop();
            s.Box(Light(Mint,1.3f,.4f),V(0,.37f,.2375f),V(.38f,.008f,.006f));
            s.Box(Light(Mint,1.6f,.4f),V(0,2.12f,.1625f),V(.7f,.02f,.004f));
            s.Push(WeaponSculpt.Rx(Up));s.Sphere(Light(Mint,1.2f,.5f),V(0,.24f,2.2f),V(.055f,.055f,.04f),6,16);s.Pop();
        }
        // The heal pulse about PulsePivot (scaled 1 + 2.2k and faded by the draw): a clean ring with a soft halo, both see-through.
        private static void Pulse(WeaponSculpt s,WeaponMesh mesh)
        {
            s.Into(Glass);Clear(mesh,PulseAlpha,()=>Ring(s,WeaponPaint.Glow(MintInk,1.5f,.6f),.315f,.335f,0,64));
            Clear(mesh,HaloAlpha,()=>{Ring(s,WeaponPaint.Glow(Mint,.8f,.35f),.335f,.4f,0,64);Ring(s,WeaponPaint.Glow(Mint,.8f,.35f),.26f,.315f,0,64);});
        }
        // Writes `draw` into the see-through section with the given alpha (the bank's colour alpha, NativeSourceTransparent).
        private static void Clear(WeaponMesh mesh,float alpha,Action draw)
        {
            var section=mesh.Section(Glass,WeaponStretch.None);int from=section.Colors.Count;draw();
            for(int i=from;i<section.Colors.Count;i++){var c=section.Colors[i];section.Colors[i]=new Vector4(c.X,c.Y,c.Z,alpha);}
        }

        // ---- the dispensed chip (chip-model.ts's unit chip, about 1.1 across before the draw's .62): faces +-y ----
        /// <summary>The chip: package, heat spreaders and gold leads (bone 0); the die, its core, rims and traces (bone 1, the
        /// rarity tint), on both faces so it reads in its tier colour as it spins.</summary>
        public static WeaponMesh Pickup()
        {
            var mesh=new WeaponMesh{Spec="pve-pickup",Tier=0};var s=new WeaponSculpt(mesh,0);
            s.Into(Body);
            s.Push(WeaponSculpt.Rx(Up));
            Panel(s,ShellDark,.8f,.8f,.09f,0,0,-.05f,.05f,.016f);
            // The rarity band round the package's edge: the tier reads from any side as the chip spins, edge on included.
            s.Into(Lights);Panel(s,Light(0xffffff,1.5f,.55f),.814f,.814f,.095f,0,0,-.013f,.013f,0);s.Into(Body);
            foreach(int face in new[]{-1,1})
            {
                float y0=face>0?.05f:-.072f,y1=face>0?.072f:-.05f;Panel(s,Spreader,.6f,.6f,.07f,0,0,y0,y1,.008f);
                float z=face*.0735f;Panel(s,Black,.42f,.42f,.05f,0,0,face>0?.072f:-.076f,face>0?.076f:-.072f,.003f);
                s.Into(Lights);
                Panel(s,Light(0xffffff,1.15f,.55f),.36f,.36f,.04f,0,0,face>0?.076f:-.078f,face>0?.078f:-.076f,0);
                Panel(s,Light(0xffffff,2.1f,.8f),.18f,.18f,.024f,0,0,face>0?.078f:-.0795f,face>0?.0795f:-.078f,0);
                foreach(int k in new[]{-1,1}){s.Box(Light(0xffffff,1f,.5f),V(0,k*.24f,z),V(.49f,.008f,.004f));s.Box(Light(0xffffff,1f,.5f),V(k*.24f,0,z),V(.008f,.49f,.004f));}
                for(int side=0;side<4;side++)for(int i=0;i<3;i++)
                {
                    float u=-.13f+i*.13f;var along=side<2?new Vector2(u,(side==0?1:-1)*.335f):new Vector2((side==2?1:-1)*.335f,u);
                    var size=side<2?V(.012f,.08f,.003f):V(.08f,.012f,.003f);s.Box(Light(0xffffff,.8f,.45f),V(along.X,along.Y,face*.0515f),size);
                }
                s.Cylinder(Light(0xffffff,1.6f,.6f),.018f,face>0?.05f:-.054f,face>0?.054f:-.05f,10,0,-.33f,.33f);
                s.Into(Body);
            }
            // Gull-wing leads, five a side, out of the package's edge and down to the seating plane.
            for(int side=0;side<4;side++)for(int i=0;i<5;i++)
            {
                float u=-.28f+i*.14f;
                if(side<2){float y=side==0?1:-1;s.Box(Gold,V(u,y*.44f,0),V(.045f,.09f,.016f),.003f);s.Box(Gold,V(u,y*.495f,-.026f),V(.045f,.03f,.06f),.003f);}
                else{float x=side==2?1:-1;s.Box(Gold,V(x*.44f,u,0),V(.09f,.045f,.016f),.003f);s.Box(Gold,V(x*.495f,u,-.026f),V(.03f,.045f,.06f),.003f);}
            }
            s.Pop();
            return mesh;
        }

    }
}

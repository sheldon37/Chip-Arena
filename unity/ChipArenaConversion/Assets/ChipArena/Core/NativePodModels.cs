using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition drop pod (owner 2026-11-02: "When all players load in, we essentially drop in to the hub from a pod. (This
    // pod is how players can choose to leave as well) using their OWN pod. (Can have an LED sign with the player name) ... make
    // the pods customizable too"), remodelled for the crash (docs/EXPEDITION-OVERHAUL.md R7, W4): ablative tiles that glow white
    // hot on the way in and cool to char, four fins that are its landing legs, a porthole in the door, the explosive bolts that
    // blow the door, the harness bar over the pilot's shoulders. Each pilot's own pod, drawn by Runtime/NativePodPresentation.cs
    // from the source-model bank (Runtime/NativeMachineModels.cs adds it). Unity's model frame, y up, the base at the origin, the
    // doorway toward local +z. Bones: 0 the bare structure (gunmetal, never tinted), 1 the lights (seams, the base strip, the door
    // frame, the beacon: the draw tints them in the pod's light colour and level), 2 the paint (hull panels and nose, near white:
    // the draw tints them the pod's paint), 3 the trim (bands, ribs, fins: tinted the trim), 4 the cabin lamp (red on the way
    // down, warm once landed), 5 the retro nozzles' throats (the burn's colour and level), 6 the ablative tiles and the heat
    // shield (NativePodDrop.HullColor), 7 the explosive bolts (their flash), 8 the harness bar (turned about HarnessHinge); the
    // porthole's pane is the bank's see-through glass (bone -2). The LED name sign is a screen the draw lays over the marquee box
    // (SignCentre, SignWidth x SignHeight, facing +z). The crash's lasting mark is a model too: four crater shapes, each ploughed into its furrow,
    // flat paint at most FlatHeight over the floor (no solid, nothing to bump into). Engine-free and deterministic.
    public static class NativePodModels
    {
        public const string PodId="expedition-pod",DoorId="expedition-pod-door";
        /// <summary>The same pod and door in see-through glass (the bank builds them alongside): my own pod while it stands
        /// between my third-person camera and me.</summary>
        public const string GhostId="expedition-pod-ghost",DoorGhostId="expedition-pod-door-ghost";
        /// <summary>The crash's lasting mark: the crater with the furrow it ploughed (CraterShapes shapes, one per pod by its plan).</summary>
        public const string CraterId="expedition-pod-crater";
        public const int CraterShapes=4;
        public static readonly string[] CraterIds={CraterId+"-0",CraterId+"-1",CraterId+"-2",CraterId+"-3"};
        public const int Body=0,Lights=1,Paint=2,Trim=3,Cabin=4,Retro=5;
        /// <summary>The ablative tiles and heat shield, the explosive bolts round the doorway, the harness bar.</summary>
        public const int Heat=6,Bolts=7,Harness=8;
        /// <summary>The bank's see-through bone (NativeMachineModels.Glass): the porthole's pane.</summary>
        public const int Glass=-2;
        /// <summary>The door: its hinge (the doorway's sill) in the pod's frame; the door stands up from it (y) with its outer
        /// face toward +z, and opens by turning about x, its top falling outward (OpenAngle: resting on a level floor as a ramp;
        /// a sunk, leaning pod rests it at NativePodPlan.RestDoor).</summary>
        public static readonly Vector3 Hinge=new Vector3(0,.24f,.69f);
        public const float DoorWidth=.52f,DoorHeight=1.62f,OpenAngle=1.69f;
        /// <summary>The porthole in the door (its centre over the hinge, its pane's half size).</summary>
        public const float PortholeY=1.23f,PortholeHalf=.1f;
        /// <summary>The LED sign's lit area (centre, size; facing +z) and its dot grid.</summary>
        public static readonly Vector3 SignCentre=new Vector3(0,2.07f,.846f);
        public const float SignWidth=.9f,SignHeight=.135f;
        /// <summary>The pod's footprint radius (fins included) and its height to the beacon.</summary>
        public const float Radius=1.12f,Height=2.95f;
        /// <summary>The cabin floor over the base: where the pilot stands.</summary>
        public const float Floor=.18f;
        /// <summary>The harness bar's hinge (on the back wall over the pilot's shoulders) and its turn about x once it is up (0 down,
        /// over the chest).</summary>
        public static readonly Vector3 HarnessHinge=new Vector3(0,1.9f,-.44f);
        public const float HarnessUp=-1.6f;
        /// <summary>Astra's pod collider from A1 (two crossed 1.96 x 2.85 x 1.96 m boxes on the pod centre): the rested pod, sunk
        /// and leaning, keeps its hull inside it (NativePodDrop.RestPose).</summary>
        public const float EnvelopeHalf=.98f,EnvelopeHeight=2.85f;
        /// <summary>The crater's greatest height over the floor (flat paint), its lowest layer's lift off it (no z-fight at 100 m)
        /// and its radius to the rim (it is 5 m across; the draw scales it a little per pod).</summary>
        public const float FlatHeight=.06f,FlatLift=.016f,CraterRadius=2.5f;

        private static readonly WeaponPaint Frame=WeaponPaint.Solid(0x1d222c,.05f),FrameDark=WeaponPaint.Solid(0x111419,.02f),Seam=WeaponPaint.Solid(0x07080b),
            Shell=WeaponPaint.Solid(0xeceef2,.1f),Band=WeaponPaint.Solid(0xd4d8e0,.14f),Rubber=WeaponPaint.Solid(0x0c0d10,.01f),Glass0=WeaponPaint.Solid(0x0a0e16,.2f),
            Scorch=WeaponPaint.Solid(0x16120f),Grab=WeaponPaint.Solid(0xd9a521,.08f),Pad=WeaponPaint.Solid(0x1b1d22,.02f),Pane=WeaponPaint.Solid(0x9fb6c8,.05f),
            Tile=WeaponPaint.Glow(0xffffff,1,1),Liner=WeaponPaint.Solid(0x0b0c10);
        private static WeaponPaint Light(float strength)=>WeaponPaint.Glow(0xffffff,strength,.35f);
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Pi=(float)Math.PI;

        /// <summary>The pod (its base at the origin, the doorway toward +z): seven painted hull panels round an octagon (each with
        /// two trim ribs, a scorch line and a 2 x 2 grid of ablative tiles below the lower rib) and the doorway in the eighth, light
        /// seams between them, a painted nose with a trim collar and the beacon, the top and base bands, the heat shield under the
        /// base round four retro nozzles, four swept fins on the diagonals that end in landing pads, the marquee box over the door
        /// for the LED name, the door frame's light strips and its four explosive bolts, and the cabin (floor, a lamp and a red strip
        /// on the back wall, harness straps, the harness bar).</summary>
        public static WeaponMesh Pod()
        {
            var mesh=new WeaponMesh{Spec=PodId,Tier=0};var s=new WeaponSculpt(mesh,0);
            void Column(in WeaponPaint p,float[] rz,int sides,float phase){s.Push(WeaponSculpt.Rx(-Pi/2));s.Lathe(p,rz,sides,0,phase);s.Pop();}
            // the hull: seven panels (the eighth, toward +z, is the doorway)
            for(int i=1;i<8;i++)
            {
                float a=i*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(.66f*(float)Math.Sin(a),0,.66f*(float)Math.Cos(a)));
                s.Into(Paint);s.Box(Shell,V(0,1.1f,0),V(.56f,1.86f,.07f),.012f);
                s.Into(Trim);foreach(float y in new[]{.82f,1.62f})s.Box(Band,V(0,y,.045f),V(.5f,.05f,.03f),.008f);
                s.Into(Body);s.Box(Seam,V(0,1.22f,.037f),V(.5f,.012f,.004f));if(i%2==0)s.Box(FrameDark,V(0,1.02f,.04f),V(.16f,.22f,.012f),.004f);
                // the re-entry's scorch: dark streaks swept up the panel from the tiles
                s.Box(Scorch,V(-.12f+.05f*(i%3),1.02f,.0365f),V(.035f,.32f,.002f));s.Box(Scorch,V(.14f-.04f*(i%2),.98f,.0365f),V(.025f,.24f,.002f));
                // the ablative tiles: a 2 x 2 grid proud of the panel below its lower rib
                s.Into(Heat);for(int r=0;r<2;r++)for(int c=0;c<2;c++)s.Box(Tile,V(-.135f+.27f*c,.34f+.25f*r,.046f),V(.25f,.23f,.022f),.006f);
                s.Pop();
            }
            // the nose and its bands
            s.Into(Paint);Column(Shell,new[]{.76f,2.03f,.74f,2.05f,.52f,2.4f,.22f,2.7f,.07f,2.8f},8,.39f);
            s.Into(Trim);Column(Band,new[]{.78f,1.99f,.78f,2.11f},8,.39f);Column(Band,new[]{.8f,0f,.8f,.18f},8,.39f);Column(Band,new[]{.25f,2.62f,.25f,2.69f},8,.39f);
            s.Into(Body);Column(Frame,new[]{.5f,.18f,.5f,.2f},8,.39f);
            // the light seams between the panels, the base strip, the beacon
            s.Into(Lights);
            for(int i=0;i<8;i++){float a=i*Pi/4+.39f;s.Box(Light(1),V(.7f*(float)Math.Sin(a),1.1f,.7f*(float)Math.Cos(a)),V(.03f,1.8f,.03f));}
            Column(Light(1),new[]{.805f,.07f,.805f,.11f},16,.0f);
            s.Sphere(Light(1.2f),V(0,2.86f,0),V(.07f,.07f,.07f),6,8);
            // the heat shield under the base: an outer skirt round the nozzles and a centre plate (its tiles glow with the hull)
            s.Into(Heat);s.Push(WeaponSculpt.Rx(-Pi/2));s.Lathe(Tile,new[]{.79f,.012f,.55f,-.03f},16,0,.2f,false);s.Pop();Column(Tile,new[]{.3f,-.03f,.12f,-.05f,.01f,-.055f},10,0);
            for(int i=0;i<4;i++)
            {
                // four swept fins on the diagonals, root on the hull and tip out at the footprint, each ending in a landing pad
                float a=i*Pi/2+Pi/4;s.Push(WeaponSculpt.Ry(a-Pi/2));
                s.Into(Trim);s.Slab(_=>Band,Band,Band,WeaponSculpt.Outline(0,.7f,.07f,1.05f,.07f,1.02f,.6f,.7f,1.46f),-.032f,.032f,.008f);
                s.Into(Body);s.Box(Frame,V(.96f,.05f,0),V(.22f,.06f,.2f),.012f);s.Box(Rubber,V(.96f,.012f,0),V(.21f,.024f,.19f));s.Box(FrameDark,V(.86f,.42f,0),V(.04f,.62f,.075f),.006f);
                s.Pop();
                // a retro nozzle under the hull on each axis, pointing down and out (its throat the burn's glow)
                float b=i*Pi/2;s.Push(WeaponSculpt.Rx(Pi/2)*WeaponSculpt.Rx(-.35f)*WeaponSculpt.Ry(b)*WeaponSculpt.T(.42f*(float)Math.Sin(b),.16f,.42f*(float)Math.Cos(b)));
                s.Into(Body);s.Cylinder(Frame,.09f,-.06f,.1f,10,.012f);s.Cylinder(FrameDark,.065f,.1f,.15f,10);
                s.Into(Retro);s.Cylinder(Light(1),.05f,.151f,.156f,10);
                s.Pop();
            }
            // the doorway: posts and header in trim, the frame's light strips, the sill, the four explosive bolts
            s.Into(Trim);foreach(int x in new[]{-1,1})s.Box(Band,V(x*.3f,1.07f,.7f),V(.07f,1.7f,.09f),.01f);s.Box(Band,V(0,1.93f,.7f),V(.66f,.08f,.09f),.01f);
            s.Into(Body);s.Box(Frame,V(0,.21f,.69f),V(.6f,.06f,.12f),.01f);
            s.Into(Lights);foreach(int x in new[]{-1,1})s.Box(Light(1),V(x*.27f,1.07f,.752f),V(.014f,1.5f,.014f));
            s.Into(Bolts);foreach(int x in new[]{-1,1})foreach(float y in new[]{.36f,1.78f}){s.Push(WeaponSculpt.T(x*.3f,y,.745f));s.Cylinder(Tile,.032f,0,.03f,8,.006f);s.Pop();}
            // the marquee box over the door: a dark case with a trim bezel; the LED face (SignCentre) is the draw's screen
            s.Into(Body);s.Box(FrameDark,V(0,2.07f,.79f),V(1.0f,.21f,.1f),.015f);s.Box(Glass0,V(0,2.07f,.842f),V(.94f,.16f,.006f));
            s.Into(Trim);s.Box(Band,V(0,2.185f,.8f),V(1.02f,.025f,.11f),.006f);s.Box(Band,V(0,1.955f,.8f),V(1.02f,.025f,.11f),.006f);
            foreach(int x in new[]{-1,1})s.Box(Band,V(x*.5f,2.07f,.8f),V(.025f,.23f,.11f),.006f);
            // the cabin: the floor, a dark liner inside the hull (so the cabin reads by its own red lamps), the lamp and an emergency
            // strip on the back wall, two harness straps
            s.Into(Body);s.Push(WeaponSculpt.Rx(-Pi/2));s.Cylinder(FrameDark,.6f,.17f,.19f,8);s.Pop();
            for(int i=1;i<8;i++){float a=i*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(.6f*(float)Math.Sin(a),0,.6f*(float)Math.Cos(a)));s.Box(Liner,V(0,1.1f,0),V(.5f,1.82f,.02f));s.Pop();}
            // the cabin's ceiling, on the liner's top under the nose (whose shell faces only outward), so the cabin is closed overhead
            s.Push(WeaponSculpt.Rx(-Pi/2));s.Cylinder(Liner,.64f,2.0f,2.03f,8);s.Pop();
            s.Box(Frame,V(0,1.15f,-.56f),V(.5f,1.7f,.04f),.01f);s.Box(Pad,V(0,1.22f,-.52f),V(.34f,1.0f,.05f),.02f);
            s.Into(Trim);foreach(int x in new[]{-1,1})s.Box(Band,V(x*.14f,1.2f,-.49f),V(.05f,1.1f,.02f),.004f);
            s.Into(Cabin);s.Box(Light(1),V(0,1.82f,-.53f),V(.36f,.05f,.03f));s.Box(Light(.6f),V(0,.62f,-.53f),V(.3f,.03f,.03f));foreach(int x in new[]{-1,1})s.Box(Light(.8f),V(x*.27f,1.3f,-.5f),V(.02f,.5f,.02f));
            // the harness bar: hinged on the back wall over the shoulders, two padded arms down over them to a grab bar at the chest
            s.Into(Harness);var h=HarnessHinge;var pad=new Vector3(0,1.36f,.17f);var arm=pad-h;float len=arm.Length();float pitch=(float)Math.Atan2(-arm.Y,arm.Z);
            s.Box(FrameDark,h,V(.42f,.07f,.07f),.012f);
            foreach(int x in new[]{-1,1}){s.Push(WeaponSculpt.Rx(pitch)*WeaponSculpt.T(x*.17f,h.Y,h.Z));s.Box(Pad,V(0,0,len*.5f),V(.07f,.07f,len),.02f);s.Pop();}
            s.Box(Grab,pad,V(.44f,.06f,.06f),.015f);s.Box(Pad,pad+V(0,.0f,-.04f),V(.3f,.09f,.04f),.02f);
            return mesh;
        }
        /// <summary>The door (its hinge at the origin: the doorway's sill; it stands up y, its outer face toward +z): the painted
        /// plate in a trim frame round a porthole (a glass pane in a trim ring), two handles and the light strip across it.</summary>
        public static WeaponMesh Door()
        {
            var mesh=new WeaponMesh{Spec=DoorId,Tier=0};var s=new WeaponSculpt(mesh,0);float h=DoorHeight,c=h/2,py=PortholeY,ph=PortholeHalf;
            // the plate round the porthole's opening: below it, above it, and either side of it
            s.Into(Paint);s.Box(Shell,V(0,(py-ph)*.5f,0),V(DoorWidth,py-ph,.07f),.012f);s.Box(Shell,V(0,(py+ph+h)*.5f,0),V(DoorWidth,h-py-ph,.07f),.012f);
            foreach(int x in new[]{-1,1})s.Box(Shell,V(x*(ph+(DoorWidth*.5f-ph)*.5f),py,0),V(DoorWidth*.5f-ph,ph*2,.07f));
            s.Into(Trim);foreach(int x in new[]{-1,1})s.Box(Band,V(x*.24f,c,.03f),V(.04f,h-.04f,.03f),.006f);foreach(int y in new[]{0,1})s.Box(Band,V(0,.02f+y*(h-.04f),.03f),V(.5f,.04f,.03f),.006f);
            // the porthole: a trim ring outside and in, the pane between (see-through)
            foreach(float z in new[]{.042f,-.042f})s.Torus(Band,V(0,py,z),ph+.018f,.022f,12,6);
            s.Into(Glass);s.Box(Pane,V(0,py,0),V(ph*2+.01f,ph*2+.01f,.012f));
            s.Into(Body);foreach(int y in new[]{-1,1})s.Box(Frame,V(.15f,c-.12f+y*.2f,.06f),V(.04f,.16f,.03f),.008f);
            s.Box(Seam,V(0,c-.32f,.037f),V(.44f,.012f,.004f));
            s.Into(Lights);s.Box(Light(1),V(0,c-.08f,.045f),V(.3f,.05f,.02f));
            return mesh;
        }

        // ---- the crash's lasting marks ----
        private static readonly WeaponPaint Char=WeaponPaint.Solid(0x0c0b0a),Ash=WeaponPaint.Solid(0x2b2724),Burnt=WeaponPaint.Solid(0x1a1714),Dust=WeaponPaint.Solid(0x5e564d),
            Spoil=WeaponPaint.Solid(0x463f38),Embers=WeaponPaint.Glow(0xffffff,1,.15f);
        private static float H(int a,int b){unchecked{uint x=(uint)a*0x9E3779B1u+(uint)b*0x85EBCA77u+0x165667B1u;x^=x>>15;x*=0x2C1B3C6Du;x^=x>>12;x*=0x297A2D39u;x^=x>>15;return (x>>8)*(1f/16777216f);}}
        // A triangle facing up (+y) whatever order it comes in.
        private static void Up(WeaponSculpt s,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d){if(Vector3.Cross(b-a,d-a).Y>=0)s.Tri(c,a,b,d,true);else s.Tri(c,a,d,b,true);}
        private static void UpQuad(WeaponSculpt s,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e){Up(s,c,a,b,d);Up(s,c,a,d,e);}
        /// <summary>How far the crater's back half is drawn out behind it into the furrow the pod ploughed, as a share of its
        /// radius: a teardrop whose tail runs back down the approach (the draw turns its -z there).</summary>
        public const float Plough=.4f;
        /// <summary>
        /// Crater shape `shape` (0..CraterShapes-1) centred on the pod's rest centre, CraterRadius to its rim: a charred pit, a ring
        /// of cracked, burnt ground, a lip of spoil thrown up round it (its edge jagged by the shape) and scorch rays out past it,
        /// its back (-z) ploughed out by Plough into the furrow (charred down its middle, spoil banked either side), all faceted
        /// and flat (FlatLift up to FlatHeight): one surface, so nothing of it lies on another and z-fights. A few ember cracks in
        /// the pit glow while the pod is hot (bone Heat).
        /// </summary>
        public static WeaponMesh Crater(int shape)
        {
            shape=(shape%CraterShapes+CraterShapes)%CraterShapes;var mesh=new WeaponMesh{Spec=CraterIds[shape],Tier=0};var s=new WeaponSculpt(mesh,0);int seed=0x3c7a+shape*977;
            const int Sides=32;float R=CraterRadius;
            // the radii (fractions of R) and heights of the rings, and the paint between ring k and k+1
            float[] rr={0,.18f,.38f,.62f,.78f,.9f,1,1.06f},hh={.022f,.02f,.026f,.034f,.05f,.056f,.03f,FlatLift};
            var paints=new[]{Char,Char,Burnt,Ash,Spoil,Dust,Spoil};
            // the share of the back (-z) an angle faces: 1 straight back, 0 abeam and forward
            float Back(float a){float b=Math.Max(0,-(float)Math.Sin(a));return b*b;}
            Vector3 P(int k,int i)
            {
                float a=i*2*Pi/Sides+.08f*(H(seed,i*13+k)-.5f),back=Back(a),j=k==0?0:((k>=4?.16f:.07f)*(H(seed,i*7+k*3)-.5f)+(k>=5?.06f*(float)Math.Sin(a*(2+shape)+shape):0))*(1-.7f*back);
                float r=R*rr[k]*(1+j)*(1+Plough*back*(k<=2?.6f:1));
                return new Vector3(r*(float)Math.Cos(a),Math.Max(FlatLift,Math.Min(FlatHeight-.003f,hh[k]*(k==0?1:.85f+.3f*H(seed,i*5+k)))),r*(float)Math.Sin(a));
            }
            s.Into(Body);
            for(int k=0;k+1<rr.Length;k++)for(int i=0;i<Sides;i++)
            {
                var a=P(k,i);var b=P(k,(i+1)%Sides);var d=P(k+1,(i+1)%Sides);var e=P(k+1,i);
                // down the furrow's middle the ground is charred, beside it burnt
                float back=Back((i+.5f)*2*Pi/Sides);var paint=k>=2&&k<=4&&back>.6f?Char:k>=2&&k<=4&&back>.3f?Burnt:paints[k];
                if(k==0)Up(s,paint,a,d,e);else UpQuad(s,paint,a,b,d,e);
            }
            // scorch rays out past the rim: thin burnt wedges lying on the floor (under the rim where they start)
            int rays=9+shape*2;
            for(int i=0;i<rays;i++)
            {
                float a=(i+.6f*H(seed,500+i))*2*Pi/rays,w=.05f+.06f*H(seed,520+i),r0=R*.92f,r1=R*(1.1f+.35f*H(seed,540+i));if(Back(a)>.3f)continue;
                var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var side=new Vector3(-dir.Z,0,dir.X);float y=FlatLift+.01f;
                Up(s,Burnt,dir*r0+side*(w*R*.5f)+Vector3.UnitY*y,dir*r0-side*(w*R*.5f)+Vector3.UnitY*y,dir*r1+Vector3.UnitY*y);
            }
            // spoil: a few flat clods on the lip, more banked along the furrow
            for(int i=0;i<14;i++)
            {
                float a=i<10?(i+H(seed,600+i))*2*Pi/10:-Pi/2+(i%2==0?-1:1)*(.35f+.2f*H(seed,630+i)),r=R*(.8f+.2f*H(seed,610+i))*(1+Plough*Back(a)*.8f),e=.07f+.08f*H(seed,620+i);
                s.Box(i%3==0?Dust:Spoil,new Vector3(r*(float)Math.Cos(a),.05f,r*(float)Math.Sin(a)),new Vector3(e*1.4f,.016f,e),0,0,a);
            }
            // ember cracks in the pit (they glow with the hull, then go dark)
            s.Into(Heat);
            for(int i=0;i<7;i++)
            {
                float a=(i+.5f*H(seed,700+i))*2*Pi/7,r0=R*.04f,r1=R*(.22f+.18f*H(seed,710+i)),w=.03f;
                var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var side=new Vector3(-dir.Z,0,dir.X);float y=.034f;
                UpQuad(s,Embers,dir*r0+side*w+Vector3.UnitY*y,dir*r0-side*w+Vector3.UnitY*y,dir*r1-side*(w*.3f)+Vector3.UnitY*y,dir*r1+side*(w*.3f)+Vector3.UnitY*y);
            }
            return mesh;
        }
    }
}

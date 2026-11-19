using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition drop pod (owner 2026-11-02: "When all players load in, we essentially drop in to the hub from a pod. (This
    // pod is how players can choose to leave as well) using their OWN pod. (Can have an LED sign with the player name) ... make
    // the pods customizable too"), remodelled for the crash (docs/EXPEDITION-OVERHAUL.md R7, W4): ablative tiles that glow white
    // hot on the way in and cool to char, four fins that are its landing legs, a porthole in the door, the explosive bolts that
    // blow the door, a cabin lined in dark grey and lit red by its own strips and instruments. Each pilot's own pod, drawn by
    // Runtime/NativePodPresentation.cs from the source-model bank (Runtime/NativeMachineModels.cs adds it); the Deployment
    // Station's dock draws the same pod (W3). Unity's model frame, y up, the base at the origin, the doorway toward local +z.
    // Bones: 0 the bare structure (never tinted: its few instrument lights keep their own colours), 1 the lights (seams, the
    // base strip, the door frame, the beacon: the draw tints them in the pod's light colour and level), 2 the paint (hull panels
    // and nose, near white: the draw tints them the pod's paint), 3 the trim (bands, ribs, fins: tinted the trim), 4 the cabin
    // lamps (red on the way down, warm once landed), 5 the retro nozzles' throats (the burn's colour and level), 6 the ablative
    // tiles and the heat shield, 7 the explosive bolts. Bones 6 and 7 are built near black and all but unlit (HeatBase): a
    // drawer that leaves them untinted (the station's dock) shows cold char, and their glow comes only through the draw's tint
    // (HeatTint). The porthole's pane is the bank's see-through glass (bone -2). The LED name sign is a screen the draw lays over
    // the marquee box (SignCentre, SignWidth x SignHeight, facing +z). The cabin's jump seat and its harness bar are models of
    // their own (SeatId, HarnessId: the hub's pod draws them; the station's dock draws its own seat and bar, W3's, of the same
    // build): the seat's cushion folds up about its back edge (bone Fold) as the pilot stands, the bar turns about HarnessHinge
    // (bone Harness). The crash's lasting mark is a model too: four crater shapes, each ploughed into its furrow, flat paint at
    // most FlatHeight over the floor (no solid, nothing to bump into), its soot fading into the plaza at the rim (bone -2, the
    // see-through glass, by vertex alpha). Engine-free and deterministic.
    public static class NativePodModels
    {
        public const string PodId="expedition-pod",DoorId="expedition-pod-door";
        /// <summary>The same pod and door in see-through glass (the bank builds them alongside): my own pod while it stands
        /// between my third-person camera and me.</summary>
        public const string GhostId="expedition-pod-ghost",DoorGhostId="expedition-pod-door-ghost";
        /// <summary>The cabin's jump seat and the harness bar over it: their own models, so a drawer that has its own (the
        /// station's dock) never shows two.</summary>
        public const string SeatId="expedition-pod-seat",HarnessId="expedition-pod-harness";
        /// <summary>The crash's lasting mark: the crater with the furrow it ploughed (CraterShapes shapes, one per pod by its plan).</summary>
        public const string CraterId="expedition-pod-crater";
        public const int CraterShapes=4;
        public static readonly string[] CraterIds={CraterId+"-0",CraterId+"-1",CraterId+"-2",CraterId+"-3"};
        public const int Body=0,Lights=1,Paint=2,Trim=3,Cabin=4,Retro=5;
        /// <summary>The ablative tiles and heat shield, the explosive bolts round the doorway, the harness bar (HarnessId), the
        /// seat's folding cushion (SeatId).</summary>
        public const int Heat=6,Bolts=7,Harness=8,Fold=9;
        /// <summary>The bank's see-through bone (NativeMachineModels.Glass): the porthole's pane, the crater's fading rim.</summary>
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
        /// <summary>The cabin floor over the base: where the pilot's feet are.</summary>
        public const float Floor=.18f;
        /// <summary>The jump seat (the station's, W3's NativeDeployEjectModels, of the same build): the cushion's top over the base
        /// (under the seated pilot's hips: the lobby Chair pose puts them .43 m over the feet), the backrest's face, and the
        /// cushion's hinge at its back edge with its fold (radians about x: up against the backrest).</summary>
        public const float CushionTop=Floor+.33f,BackZ=-.2f,FoldUp=-1.5f;
        public static readonly Vector3 FoldHinge=new Vector3(0,CushionTop-.1f,-.19f);
        /// <summary>The harness bar's pivot (the seat's top-back, the station's) and its turn about x once it is up (0 down, its
        /// crossbar in front of the seated pilot's stomach).</summary>
        public static readonly Vector3 HarnessHinge=new Vector3(0,Floor+1.18f,-.17f);
        public static readonly Vector3 HarnessBar=new Vector3(0,-.46f,.46f);
        public const float HarnessUp=-1.7f;
        /// <summary>Astra's pod collider from A1 (two crossed 1.96 x 2.85 x 1.96 m boxes on the pod centre): the rested pod, sunk
        /// and leaning, keeps its hull inside it (NativePodDrop.RestPose).</summary>
        public const float EnvelopeHalf=.98f,EnvelopeHeight=2.85f;
        /// <summary>The crater's greatest height over the floor (flat paint), its lowest layer's lift off it (no z-fight at 100 m)
        /// and its radius to the rim of its soot (it is 5 m across, its ash fading out past it; the draw scales it a little per
        /// pod).</summary>
        public const float FlatHeight=.06f,FlatLift=.016f,CraterRadius=2.5f;
        /// <summary>How far past the rim the soot fades into the plaza (a share of CraterRadius), and how far the cracks run.</summary>
        public const float FadeOut=.25f,CrackOut=.42f;
        /// <summary>The heat parts' (bones 6, 7) light as built: a tint of HeatTint(glow) makes them glow `glow` (linear); none
        /// leaves them char.</summary>
        public const float HeatBase=.012f;
        public static Vector3 HeatTint(Vector3 glow)=>glow/HeatBase;

        private static readonly WeaponPaint Frame=WeaponPaint.Solid(0x1d222c,.05f),FrameDark=WeaponPaint.Solid(0x111419,.02f),Seam=WeaponPaint.Solid(0x07080b),
            Shell=WeaponPaint.Solid(0xeceef2,.1f),Band=WeaponPaint.Solid(0xd4d8e0,.14f),Rubber=WeaponPaint.Solid(0x0c0d10,.01f),Glass0=WeaponPaint.Solid(0x0a0e16,.2f),
            Scorch=WeaponPaint.Solid(0x16120f),Grab=WeaponPaint.Solid(0xd9a521,.08f),Pad=WeaponPaint.Solid(0x1b1d22,.02f),Pane=WeaponPaint.Solid(0x9fb6c8,.05f),
            // the cabin: a dark grey liner with its panel seams and ribs (it reads by the cabin's own lamps and the ambient)
            Liner=WeaponPaint.Solid(0x3a3f4a,.05f),LinerDark=WeaponPaint.Solid(0x232730,.03f),LinerRib=WeaponPaint.Solid(0x505663,.06f),Hazard=WeaponPaint.Solid(0xc9961f,.06f),
            // the seat (the station's build)
            Steel=WeaponPaint.Solid(0xa3aab6,.06f),SteelDark=WeaponPaint.Solid(0x4a505c,.04f),Graphite=WeaponPaint.Solid(0x2a2d35,.03f),GraphiteDark=WeaponPaint.Solid(0x15171c,.02f),
            Cushion=WeaponPaint.Solid(0x3a1c1f,.03f),CushionDark=WeaponPaint.Solid(0x22100f,.02f),
            // the heat parts: char black and all but unlit until the draw's tint lights them (HeatBase)
            Tile=new WeaponPaint(new Vector3(.006f,.0056f,.005f),new Vector3(HeatBase,HeatBase,HeatBase),0),
            // the instruments' own lights (bone 0: never tinted, so any drawer shows them as they are)
            Readout=WeaponPaint.Glow(0x4dffa6,.9f,.25f),Amber=WeaponPaint.Glow(0xffa23a,.9f,.25f),Ice=WeaponPaint.Glow(0x8fd8ff,.7f,.25f);
        private static WeaponPaint Light(float strength)=>WeaponPaint.Glow(0xffffff,strength,.35f);
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Pi=(float)Math.PI;

        /// <summary>The pod (its base at the origin, the doorway toward +z): seven painted hull panels round an octagon (each with
        /// two trim ribs, a scorch line and a 2 x 2 grid of ablative tiles below the lower rib) and the doorway in the eighth, light
        /// seams between them, a painted nose with a trim collar and the beacon, the top and base bands, the heat shield under the
        /// base round four retro nozzles, four swept fins on the diagonals that end in landing pads, the marquee box over the door
        /// for the LED name, the door frame's light strips and its four explosive bolts, and the cabin: its floor, the liner in
        /// panels with seams and ribs, a ceiling under the nose, red strips down the inside of the doorway and a red wash at their
        /// feet, an instrument panel beside the door, the lamps on the back wall. (The seat and its bar: Seat, HarnessBar.)</summary>
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
                s.Into(Trim);s.Slab(_=>Band,Band,Band,WeaponSculpt.Outline(0,.7f,.07f,1.02f,.07f,.98f,.6f,.7f,1.46f),-.032f,.032f,.008f);
                s.Into(Body);s.Box(Frame,V(.93f,.05f,0),V(.22f,.06f,.2f),.012f);s.Box(Rubber,V(.93f,.012f,0),V(.21f,.024f,.19f));s.Box(FrameDark,V(.86f,.42f,0),V(.04f,.62f,.075f),.006f);
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
            // the cabin: the floor (a hazard band at the sill), the liner inside the hull in panels (a seam across each, ribs between
            // them), the ceiling under the nose's base
            s.Into(Body);s.Push(WeaponSculpt.Rx(-Pi/2));s.Cylinder(FrameDark,.6f,.17f,.19f,8);s.Pop();s.Box(Hazard,V(0,.191f,.5f),V(.46f,.004f,.07f));
            for(int i=1;i<8;i++)
            {
                float a=i*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(.6f*(float)Math.Sin(a),0,.6f*(float)Math.Cos(a)));
                s.Box(i%2==0?Liner:LinerDark,V(0,1.1f,0),V(.5f,1.82f,.02f));
                foreach(float y in new[]{.62f,1.22f,1.72f})s.Box(LinerDark,V(0,y,-.0115f),V(.46f,.012f,.003f));
                s.Box(LinerRib,V(-.25f,1.1f,-.014f),V(.03f,1.8f,.012f));
                s.Pop();
            }
            s.Push(WeaponSculpt.Rx(-Pi/2));s.Cylinder(LinerDark,.64f,2.0f,2.03f,8);s.Pop();
            s.Box(Frame,V(0,1.15f,-.56f),V(.5f,1.7f,.04f),.01f);s.Box(Pad,V(0,1.22f,-.52f),V(.34f,1.0f,.05f),.02f);
            // the instrument panel on the cabin wall right of the door (inside the hull, angled to the seat): its readouts and dials
            s.Push(WeaponSculpt.Ry(-.95f)*WeaponSculpt.T(-.44f,0,.38f));
            s.Box(FrameDark,V(0,1.22f,0),V(.24f,.34f,.05f),.01f);
            for(int r=0;r<3;r++)s.Box(r==1?Amber:Readout,V(-.05f,1.33f-.08f*r,-.028f),V(.1f,.03f,.004f));
            s.Box(Ice,V(.07f,1.29f,-.028f),V(.05f,.05f,.004f));s.Box(Amber,V(.07f,1.17f,-.028f),V(.03f,.03f,.004f));
            s.Pop();
            // the cabin's lamps: on the back wall over the seat, red strips down the inside of the doorway's posts and a red wash
            // along the floor at their feet (the draw: red on the way down, warm once landed)
            s.Into(Cabin);s.Box(Light(1),V(0,1.82f,-.53f),V(.36f,.05f,.03f));s.Box(Light(.6f),V(0,.62f,-.53f),V(.3f,.03f,.03f));
            foreach(int x in new[]{-1,1})
            {
                s.Box(Light(.9f),V(x*.31f,1.08f,.6f),V(.02f,1.4f,.02f));
                s.Box(Light(.35f),V(x*.36f,.2f,.36f),V(.16f,.006f,.3f));
            }
            return mesh;
        }
        /// <summary>The door (its hinge at the origin: the doorway's sill; it stands up y, its outer face toward +z): the painted
        /// plate in a trim frame round a porthole (a glass pane in a trim ring), two handles and the light strip across it; inside,
        /// a dark lining round the porthole.</summary>
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
            // inside: the lining round the porthole (dark, so the cabin is not walled by the white plate) and a grab handle
            s.Box(LinerDark,V(0,(py-ph-.03f)*.5f,-.04f),V(DoorWidth-.06f,py-ph-.03f,.012f));s.Box(LinerDark,V(0,(py+ph+.03f+h-.04f)*.5f,-.04f),V(DoorWidth-.06f,h-py-ph-.07f,.012f));
            foreach(int x in new[]{-1,1})s.Box(LinerDark,V(x*(ph+.03f+(DoorWidth*.5f-ph-.06f)*.5f),py,-.04f),V(DoorWidth*.5f-ph-.06f,ph*2+.06f,.012f));
            s.Box(Steel,V(-.16f,c-.1f,-.07f),V(.03f,.3f,.03f),.008f);
            s.Into(Lights);s.Box(Light(1),V(0,c-.08f,.045f),V(.3f,.05f,.02f));
            return mesh;
        }
        /// <summary>The jump seat in the pod's frame (the station's seat, W3's NativeDeployEjectModels.SeatModel, as built): its
        /// pedestal, struts and backrest (tilted back) with its headrest wings and the bar's pivot housing (bone 0), the red strips
        /// down the backrest (bone Cabin), and the cushion with its side bolsters (bone Fold: it folds up about FoldHinge as the
        /// pilot stands, so the pilot stands clear of it on the pod centre).</summary>
        public static WeaponMesh Seat()
        {
            var mesh=new WeaponMesh{Spec=SeatId,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);float f=Floor;
            void Post(in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides){s.Push(WeaponSculpt.Rx(-Pi/2)*WeaponSculpt.T(x,0,z));s.Cylinder(c,r,y0,y1,sides);s.Pop();}
            // the pedestal on its foot plate, the struts to the back wall
            s.Box(SteelDark,V(0,f+.02f,-.05f),V(.5f,.04f,.5f),.01f);Post(Graphite,.08f,0,-.05f,f+.04f,CushionTop-.1f,12);
            s.Box(SteelDark,V(0,CushionTop-.12f,.02f),V(.5f,.05f,.48f),.012f);
            foreach(int side in new[]{-1,1})s.Box(SteelDark,V(side*.2f,f+.55f,BackZ-.16f),V(.04f,.9f,.04f),.008f,-.12f);
            // the backrest (tilted back), its wings and the headrest
            foreach(int side in new[]{-1,1})s.Box(CushionDark,V(side*.25f,f+.88f,BackZ+.02f),V(.08f,.62f,.16f),.03f,-.1f);
            s.Push(WeaponSculpt.Rx(-.1f)*WeaponSculpt.T(0,f+.85f,BackZ-.04f));s.Box(Cushion,V(0,0,0),V(.44f,.8f,.1f),.04f);s.Box(Graphite,V(0,0,-.07f),V(.5f,.86f,.05f),.015f);s.Pop();
            s.Box(Cushion,V(0,f+1.36f,BackZ-.1f),V(.3f,.18f,.1f),.04f,-.1f);
            foreach(int side in new[]{-1,1})s.Box(CushionDark,V(side*.17f,f+1.36f,BackZ-.05f),V(.08f,.22f,.14f),.03f,-.1f,side*.25f);
            // the bar's pivot housing over the backrest
            s.Box(SteelDark,HarnessHinge+V(0,.02f,-.04f),V(.36f,.1f,.12f),.02f);
            // the red strips down the backrest's sides
            s.Into(Cabin);foreach(int side in new[]{-1,1})s.Box(Light(.8f),V(side*.27f,f+.85f,BackZ+.06f),V(.012f,.6f,.012f),0,-.1f);
            // the cushion and its bolsters, folding about the hinge at its back edge; a red strip along its front
            s.Into(Fold);s.Box(Cushion,V(0,CushionTop-.05f,.04f),V(.46f,.1f,.46f),.035f);
            foreach(int side in new[]{-1,1})s.Box(CushionDark,V(side*.25f,CushionTop+.01f,.04f),V(.08f,.16f,.44f),.03f);
            s.Box(SteelDark,V(0,CushionTop-.11f,-.17f),V(.42f,.04f,.05f),.01f);
            return mesh;
        }
        /// <summary>The harness bar in the pod's frame (the station's, W3's NativeDeployEjectModels.HarnessModel, as built), down:
        /// from its pivot (HarnessHinge) two steel arms over the shoulders, padded, to the padded crossbar in front of the seated
        /// pilot's stomach with its grips (bone Harness: the draw turns it up by HarnessUp), and its lock lamp (bone Lights).</summary>
        public static WeaponMesh HarnessBarModel()
        {
            var mesh=new WeaponMesh{Spec=HarnessId,Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Harness);
            var h=HarnessHinge;Vector3 P(float x,float y,float z)=>h+V(x,y,z);float by=HarnessBar.Y,bz=HarnessBar.Z;
            foreach(int side in new[]{-1,1})
            {
                s.Tube(Steel,new[]{P(side*.15f,0,0),P(side*.19f,.03f,.2f),P(side*.21f,-.18f,.4f),P(side*.2f,by,bz)},.022f,10,Vector3.UnitY,1,true);
                s.Tube(Pad,new[]{P(side*.18f,.04f,.14f),P(side*.2f,-.04f,.33f),P(side*.21f,-.22f,.42f)},.05f,10,Vector3.UnitY,1,true);
            }
            s.Tube(Steel,new[]{P(-.2f,by,bz),P(.2f,by,bz)},.025f,10,Vector3.UnitY,1,true);
            s.Tube(Pad,new[]{P(-.1f,by,bz),P(.1f,by,bz)},.05f,12,Vector3.UnitY,1,true);
            foreach(int side in new[]{-1,1})s.Box(Grab,P(side*.17f,by,bz),V(.07f,.1f,.07f),.015f);
            s.Box(SteelDark,h,V(.34f,.06f,.06f),.015f);
            s.Into(Lights);s.Box(Light(1),P(0,by,bz+.055f),V(.08f,.02f,.008f));
            return mesh;
        }

        // ---- the crash's lasting marks ----
        private static readonly WeaponPaint Soot=WeaponPaint.Solid(0x0d0c0c),Char=WeaponPaint.Solid(0x161514),Burnt=WeaponPaint.Solid(0x232120),Ash=WeaponPaint.Solid(0x34322f),
            AshLight=WeaponPaint.Solid(0x4a4744),Slab=WeaponPaint.Solid(0x6a6670,.02f),SlabDark=WeaponPaint.Solid(0x4c4952),Crack=WeaponPaint.Solid(0x0e0d0d),
            Embers=new WeaponPaint(new Vector3(.006f,.004f,.003f),new Vector3(HeatBase,HeatBase,HeatBase),0);
        private static float H(int a,int b){unchecked{uint x=(uint)a*0x9E3779B1u+(uint)b*0x85EBCA77u+0x165667B1u;x^=x>>15;x*=0x2C1B3C6Du;x^=x>>12;x*=0x297A2D39u;x^=x>>15;return (x>>8)*(1f/16777216f);}}
        // A triangle facing up (+y) whatever order it comes in.
        private static void Up(WeaponSculpt s,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d){if(Vector3.Cross(b-a,d-a).Y>=0)s.Tri(c,a,b,d,true);else s.Tri(c,a,d,b,true);}
        private static void UpQuad(WeaponSculpt s,in WeaponPaint c,Vector3 a,Vector3 b,Vector3 d,Vector3 e){Up(s,c,a,b,d);Up(s,c,a,d,e);}
        // An upward triangle into `section` with its own vertex alpha (the see-through bone: the fading soot and the cracks).
        private static void Faded(WeaponSection sec,in WeaponPaint c,Vector3 a,float aa,Vector3 b,float ab,Vector3 d,float ad)
        {
            if(Vector3.Cross(b-a,d-a).Y<0){var t=b;b=d;d=t;float u=ab;ab=ad;ad=u;}
            int i=sec.Positions.Count;var n=Vector3.UnitY;
            foreach(var (p,al) in new[]{(a,aa),(b,ab),(d,ad)}){sec.Positions.Add(p);sec.Normals.Add(n);sec.Colors.Add(new Vector4(c.Color,al));sec.Emission.Add(new Vector4(c.Emit,c.Tag));sec.Data.Add(Vector2.Zero);}
            sec.Indices.Add(i);sec.Indices.Add(i+1);sec.Indices.Add(i+2);
        }
        /// <summary>How far the crater's back half is drawn out behind it into the furrow the pod ploughed, as a share of its
        /// radius: a teardrop whose tail runs back down the approach (the draw turns its -z there).</summary>
        public const float Plough=.4f;
        /// <summary>The share of the back (-z) an angle a (about y, from +x) faces: 1 straight back, 0 abeam and forward.</summary>
        public static float Back(float a){float b=Math.Max(0,-(float)Math.Sin(a));return b*b;}
        /// <summary>The crater's outline (its fading soot's outer edge, the cracks aside) at angle a, in metres from the centre, before
        /// the draw's scale: a teardrop, its tail the furrow.</summary>
        public static float Outline(float a)=>Math.Max(CraterRadius*(1+FadeOut)*(1+Plough*Back(a))*1.05f,Back(a)<=.5f?CraterRadius*(.95f+CrackOut)*1.08f:0);
        /// <summary>
        /// Crater shape `shape` (0..CraterShapes-1) centred on the pod's rest centre: a pit of soot (its ember cracks glow with the
        /// hull, bone Heat), charred ground, ash, and a lip of paving slabs broken and pushed up round it with the cracks between
        /// them dark; its back (-z) ploughed out by Plough into the furrow, charred down its middle. Past the rim the ash fades out
        /// into the plaza over FadeOut (the see-through bone by vertex alpha) and dark cracks run out radially, fading. All flat
        /// (FlatLift up to FlatHeight), its layers at least 1.5 cm apart where they overlap, so nothing z-fights.
        /// </summary>
        public static WeaponMesh Crater(int shape)
        {
            shape=(shape%CraterShapes+CraterShapes)%CraterShapes;var mesh=new WeaponMesh{Spec=CraterIds[shape],Tier=0};var s=new WeaponSculpt(mesh,0);int seed=0x3c7a+shape*977;
            const int Sides=36;float R=CraterRadius;
            // the radii (fractions of R) and heights of the rings, and the paint between ring k and k+1 (the soot pit, char, ash, the lip)
            float[] rr={0,.16f,.36f,.58f,.8f,1};float[] hh={.022f,.022f,.026f,.03f,.026f,.022f};
            var paints=new[]{Soot,Char,Burnt,Ash,AshLight};
            Vector3 P(int k,int i)
            {
                float a=i*2*Pi/Sides+.07f*(H(seed,i*13+k)-.5f),back=Back(a),j=k==0?0:((k>=3?.12f:.06f)*(H(seed,i*7+k*3)-.5f)+(k>=4?.05f*(float)Math.Sin(a*(2+shape)+shape):0))*(1-.7f*back);
                float r=R*rr[k]*(1+j)*(1+Plough*back*(k<=2?.6f:1));
                return new Vector3(r*(float)Math.Cos(a),Math.Max(FlatLift,Math.Min(FlatHeight-.003f,hh[k])),r*(float)Math.Sin(a));
            }
            s.Into(Body);
            for(int k=0;k+1<rr.Length;k++)for(int i=0;i<Sides;i++)
            {
                var a=P(k,i);var b=P(k,(i+1)%Sides);var d=P(k+1,(i+1)%Sides);var e=P(k+1,i);
                // down the furrow's middle the ground is charred, beside it burnt
                float back=Back((i+.5f)*2*Pi/Sides);var paint=k>=2&&k<=3&&back>.55f?Char:k>=2&&k<=3&&back>.25f?Burnt:paints[k];
                if(k==0)Up(s,paint,a,d,e);else UpQuad(s,paint,a,b,d,e);
            }
            // the lip: broken paving slabs pushed up round the rim (2 cm over the ash), their dark undersides showing at the crater
            // side, the ground between them the ash's
            int slabs=11+shape;
            for(int i=0;i<slabs;i++)
            {
                float a=(i+.4f*H(seed,800+i))*2*Pi/slabs;if(Back(a)>.7f)continue;float r=R*(.86f+.12f*H(seed,810+i))*(1+Plough*Back(a)),w=.32f+.22f*H(seed,820+i),d=.26f+.2f*H(seed,830+i),turn=a+(H(seed,840+i)-.5f)*.9f;
                var c=new Vector3(r*(float)Math.Cos(a),0,r*(float)Math.Sin(a));var u=new Vector3((float)Math.Cos(turn),0,(float)Math.Sin(turn));var v=new Vector3(-u.Z,0,u.X);
                var up=Vector3.UnitY*.05f;var q=new[]{c-u*w*.5f-v*d*.5f,c+u*w*.5f-v*d*.5f,c+u*w*.5f+v*d*.5f,c-u*w*.5f+v*d*.5f};
                for(int k=0;k<4;k++)q[k]+=new Vector3((H(seed,850+i*4+k)-.5f)*.08f,0,(H(seed,860+i*4+k)-.5f)*.08f);
                UpQuad(s,H(seed,870+i)<.5f?Slab:AshLight,q[0]+up,q[1]+up,q[2]+up,q[3]+up);
                // its broken edge toward the pit: a dark strip beside it (the slab's raised side), never under it
                int near=0;float best=float.MaxValue;for(int k=0;k<4;k++){float m=((q[k]+q[(k+1)%4])*.5f).Length();if(m<best){best=m;near=k;}}
                var e0=q[near];var e1=q[(near+1)%4];var toward=-Vector3.Normalize((e0+e1)*.5f)*.06f;
                UpQuad(s,SlabDark,e0+up,e1+up,e1+toward+up,e0+toward+up);
            }
            // ember cracks in the pit (they glow with the hull, then go dark), 1.5 cm over it
            s.Into(Heat);
            for(int i=0;i<7;i++)
            {
                float a=(i+.5f*H(seed,700+i))*2*Pi/7,r0=R*.04f,r1=R*(.22f+.18f*H(seed,710+i)),w=.03f;
                var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));var side=new Vector3(-dir.Z,0,dir.X);float y=.04f;
                UpQuad(s,Embers,dir*r0+side*w+Vector3.UnitY*y,dir*r0-side*w+Vector3.UnitY*y,dir*r1-side*(w*.3f)+Vector3.UnitY*y,dir*r1+side*(w*.3f)+Vector3.UnitY*y);
            }
            // past the rim: the ash fading out into the plaza (vertex alpha .85 to 0), and the cracks running out from it, fading
            var fade=mesh.Section(Glass,WeaponStretch.None);
            Vector3 O(int i,float out_)
            {
                float a=i*2*Pi/Sides,back=Back(a);float r=R*(1+out_)*(1+Plough*back)*(1+.04f*(H(seed,900+i)-.5f));
                return new Vector3(r*(float)Math.Cos(a),FlatLift,r*(float)Math.Sin(a));
            }
            for(int i=0;i<Sides;i++)
            {
                var a=P(5,i);var b=P(5,(i+1)%Sides);a.Y=b.Y=FlatLift+.004f;var d=O((i+1)%Sides,FadeOut);var e=O(i,FadeOut);
                Faded(fade,Ash,a,.85f,b,.85f,d,0);Faded(fade,Ash,a,.85f,d,0,e,0);
            }
            int cracks=8+2*shape;
            for(int i=0;i<cracks;i++)
            {
                float a=(i+.6f*H(seed,500+i))*2*Pi/cracks;if(Back(a)>.5f)continue;var at=new Vector3(R*.95f*(float)Math.Cos(a),FlatLift+.022f,R*.95f*(float)Math.Sin(a));
                float len=R*CrackOut*(.55f+.45f*H(seed,540+i)),heading=a,w=.045f;int segs=4;
                for(int k=0;k<segs;k++)
                {
                    heading+=(H(seed,560+i*segs+k)-.5f)*.7f;var dir=new Vector3((float)Math.Cos(heading),0,(float)Math.Sin(heading));var side=new Vector3(-dir.Z,0,dir.X);
                    float u0=k/(float)segs,u1=(k+1f)/segs,w0=w*(1-u0*.75f),w1=w*(1-u1*.75f);var next=at+dir*(len/segs);
                    Faded(fade,Crack,at+side*w0,.9f*(1-u0),at-side*w0,.9f*(1-u0),next-side*w1,.9f*(1-u1));Faded(fade,Crack,at+side*w0,.9f*(1-u0),next-side*w1,.9f*(1-u1),next+side*w1,.9f*(1-u1));
                    at=next;
                }
            }
            return mesh;
        }
    }
}

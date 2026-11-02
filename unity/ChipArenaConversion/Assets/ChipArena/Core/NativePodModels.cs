using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition drop pod (owner 2026-11-02: "When all players load in, we essentially drop in to the hub from a pod. (This
    // pod is how players can choose to leave as well) using their OWN pod. (Can have an LED sign with the player name) ... make
    // the pods customizable too"). Each pilot's own pod, drawn by Runtime/NativePodPresentation.cs from the source-model bank
    // (Runtime/NativeMachineModels.cs adds it). Unity's model frame, y up, the base at the origin, the doorway toward local +z.
    // Bones: 0 the bare structure (gunmetal, never tinted), 1 the lights (seams, the base strip, the door frame, the beacon: the
    // draw tints them in the pod's light colour and level), 2 the paint (hull panels and nose, near white: the draw tints them
    // the pod's paint), 3 the trim (bands, ribs, fins: tinted the trim), 4 the cabin lamp (warm, lit as the door opens), 5 the
    // retro nozzles' throats (the burn's colour and level). The LED name sign is a screen the draw lays over the marquee box
    // (SignCentre, SignWidth x SignHeight, facing +z). Engine-free and deterministic.
    public static class NativePodModels
    {
        public const string PodId="expedition-pod",DoorId="expedition-pod-door";
        /// <summary>The same pod and door in see-through glass (the bank builds them alongside): my own pod while it stands
        /// between my third-person camera and me.</summary>
        public const string GhostId="expedition-pod-ghost",DoorGhostId="expedition-pod-door-ghost";
        public const int Body=0,Lights=1,Paint=2,Trim=3,Cabin=4,Retro=5;
        /// <summary>The door: its hinge (the doorway's sill) in the pod's frame; the door stands up from it (y) with its outer
        /// face toward +z, and opens by turning about x, its top falling outward (OpenAngle: resting on the ground as a ramp).</summary>
        public static readonly Vector3 Hinge=new Vector3(0,.24f,.69f);
        public const float DoorWidth=.52f,DoorHeight=1.62f,OpenAngle=1.69f;
        /// <summary>The LED sign's lit area (centre, size; facing +z) and its dot grid.</summary>
        public static readonly Vector3 SignCentre=new Vector3(0,2.07f,.846f);
        public const float SignWidth=.9f,SignHeight=.135f;
        /// <summary>The pod's footprint radius (fins included) and its height to the beacon.</summary>
        public const float Radius=1.12f,Height=2.95f;
        /// <summary>The cabin floor over the base: where the pilot stands.</summary>
        public const float Floor=.18f;

        private static readonly WeaponPaint Frame=WeaponPaint.Solid(0x1d222c,.05f),FrameDark=WeaponPaint.Solid(0x111419,.02f),Seam=WeaponPaint.Solid(0x07080b),
            Shell=WeaponPaint.Solid(0xeceef2,.1f),Band=WeaponPaint.Solid(0xd4d8e0,.14f),Rubber=WeaponPaint.Solid(0x0c0d10,.01f),Glass=WeaponPaint.Solid(0x0a0e16,.2f);
        private static WeaponPaint Light(float strength)=>WeaponPaint.Glow(0xffffff,strength,.35f);
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Pi=(float)Math.PI;

        /// <summary>The pod (its base at the origin, the doorway toward +z): seven painted hull panels round an octagon (each
        /// with two trim ribs and a scorch line) and the doorway in the eighth, light seams between them, a painted nose with a
        /// trim collar and the beacon, the top and base bands, four landing legs with pads, four retro nozzles, the marquee box
        /// over the door for the LED name, the door frame's light strips, and the cabin (floor, a lamp on the back wall, harness
        /// straps).</summary>
        public static WeaponMesh Pod()
        {
            var mesh=new WeaponMesh{Spec=PodId,Tier=0};var s=new WeaponSculpt(mesh,0);
            void Column(in WeaponPaint p,float[] rz,int sides,float phase){s.Push(WeaponSculpt.Rx(-Pi/2));s.Lathe(p,rz,sides,0,phase);s.Pop();}
            // the hull: seven panels (the eighth, toward +z, is the doorway)
            for(int i=1;i<8;i++)
            {
                float a=i*Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(.66f*(float)Math.Sin(a),0,.66f*(float)Math.Cos(a)));
                s.Into(Paint);s.Box(Shell,V(0,1.1f,0),V(.56f,1.86f,.07f),.012f);
                s.Into(Trim);foreach(float y in new[]{.55f,1.62f})s.Box(Band,V(0,y,.045f),V(.5f,.05f,.03f),.008f);
                s.Into(Body);s.Box(Seam,V(0,1.1f,.037f),V(.5f,.012f,.004f));if(i%2==0)s.Box(FrameDark,V(0,.92f,.04f),V(.16f,.22f,.012f),.004f);
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
            // four landing legs with pads, four retro nozzles between them
            for(int i=0;i<4;i++)
            {
                float a=i*Pi/2+Pi/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(.82f*(float)Math.Sin(a),0,.82f*(float)Math.Cos(a)));
                s.Into(Trim);s.Box(Band,V(0,.55f,0),V(.06f,1f,.34f),.01f);
                s.Into(Body);s.Box(Frame,V(0,.035f,.06f),V(.16f,.07f,.46f),.012f);s.Box(Rubber,V(0,.006f,.06f),V(.15f,.012f,.44f));s.Box(FrameDark,V(0,.32f,.12f),V(.05f,.42f,.05f),.01f);
                s.Pop();
                // a retro nozzle under the hull, pointing down and out (its throat the burn's glow)
                s.Push(WeaponSculpt.Rx(Pi/2)*WeaponSculpt.Rx(-.35f)*WeaponSculpt.Ry(a)*WeaponSculpt.T(.52f*(float)Math.Sin(a),.16f,.52f*(float)Math.Cos(a)));
                s.Into(Body);s.Cylinder(Frame,.09f,-.06f,.1f,10,.012f);s.Cylinder(FrameDark,.065f,.1f,.15f,10);
                s.Into(Retro);s.Cylinder(Light(1),.05f,.151f,.156f,10);
                s.Pop();
            }
            // the doorway: posts and header in trim, the frame's light strips, the sill
            s.Into(Trim);foreach(int x in new[]{-1,1})s.Box(Band,V(x*.3f,1.07f,.7f),V(.07f,1.7f,.09f),.01f);s.Box(Band,V(0,1.93f,.7f),V(.66f,.08f,.09f),.01f);
            s.Into(Body);s.Box(Frame,V(0,.21f,.69f),V(.6f,.06f,.12f),.01f);
            s.Into(Lights);foreach(int x in new[]{-1,1})s.Box(Light(1),V(x*.27f,1.07f,.752f),V(.014f,1.5f,.014f));
            // the marquee box over the door: a dark case with a trim bezel; the LED face (SignCentre) is the draw's screen
            s.Into(Body);s.Box(FrameDark,V(0,2.07f,.79f),V(1.0f,.21f,.1f),.015f);s.Box(Glass,V(0,2.07f,.842f),V(.94f,.16f,.006f));
            s.Into(Trim);s.Box(Band,V(0,2.185f,.8f),V(1.02f,.025f,.11f),.006f);s.Box(Band,V(0,1.955f,.8f),V(1.02f,.025f,.11f),.006f);
            foreach(int x in new[]{-1,1})s.Box(Band,V(x*.5f,2.07f,.8f),V(.025f,.23f,.11f),.006f);
            // the cabin: the floor, the lamp on the back wall, two harness straps
            s.Into(Body);s.Push(WeaponSculpt.Rx(-Pi/2));s.Cylinder(FrameDark,.6f,.17f,.19f,8);s.Pop();
            s.Box(Frame,V(0,1.15f,-.56f),V(.5f,1.7f,.04f),.01f);
            s.Into(Trim);foreach(int x in new[]{-1,1})s.Box(Band,V(x*.14f,1.2f,-.53f),V(.05f,1.1f,.02f),.004f);
            s.Into(Cabin);s.Box(Light(1),V(0,1.82f,-.53f),V(.36f,.05f,.03f));s.Box(Light(.6f),V(0,.62f,-.53f),V(.3f,.03f,.03f));
            return mesh;
        }
        /// <summary>The door (its hinge at the origin: the doorway's sill; it stands up y, its outer face toward +z): the painted
        /// plate in a trim frame, a dark porthole slit, two handles and the light strip across it.</summary>
        public static WeaponMesh Door()
        {
            var mesh=new WeaponMesh{Spec=DoorId,Tier=0};var s=new WeaponSculpt(mesh,0);float h=DoorHeight,c=h/2;
            s.Into(Paint);s.Box(Shell,V(0,c,0),V(DoorWidth,h,.07f),.012f);
            s.Into(Trim);foreach(int x in new[]{-1,1})s.Box(Band,V(x*.24f,c,.03f),V(.04f,h-.04f,.03f),.006f);foreach(int y in new[]{0,1})s.Box(Band,V(0,.02f+y*(h-.04f),.03f),V(.5f,.04f,.03f),.006f);
            s.Into(Body);s.Box(Glass,V(0,c+.42f,.04f),V(.26f,.1f,.012f),.01f);foreach(int y in new[]{-1,1})s.Box(Frame,V(.15f,c+y*.32f,.06f),V(.04f,.16f,.03f),.008f);
            s.Box(Seam,V(0,c-.2f,.037f),V(.44f,.012f,.004f));
            s.Into(Lights);s.Box(Light(1),V(0,c,.045f),V(.3f,.05f,.02f));
            return mesh;
        }
    }
}

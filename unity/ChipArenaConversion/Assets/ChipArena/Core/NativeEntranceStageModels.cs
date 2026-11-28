using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The directed entrances' stage as sculpted models (owner 2026-10-29: the intros "are based in a voxel atmosphere or
    // models, review and revise"), drawn in place of the box pylons and the crossed-slab dais where the bank built them
    // (NativeEntranceStage.Arena falls back to its primitives otherwise). Source frame, y up; bone 0 the body, bone 1 the lights
    // in white (the draw tints them with the entrance's accent and the pylon's level). Engine-free and deterministic.
    public static class NativeEntranceStageModels
    {
        public const string PylonId="entrance-pylon",DaisId="entrance-dais";
        public const int Body=0,Lights=1;
        private static readonly WeaponPaint Metal=WeaponPaint.Solid(0x2e3852,.06f),MetalLight=WeaponPaint.Solid(0x5a6684,.1f),MetalDark=WeaponPaint.Solid(0x1a2132,.04f),
            Seam=WeaponPaint.Solid(0x06080e),Deck=WeaponPaint.Solid(0x232c44,.05f),Skirt=WeaponPaint.Solid(0x0e1322,.02f),Trim=WeaponPaint.Solid(0x5a6684,.12f);
        private static WeaponPaint Light(float strength)=>WeaponPaint.Glow(0xffffff,strength,.4f);
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);

        /// <summary>A pylon (its foot at the origin, NativeEntranceStage.PylonHeight tall), its lit face toward local -z (the
        /// ring's centre): a two-tier footing with corner bolts, a bevelled column with corner rails, panel seams and two collars,
        /// a recessed channel with the light strip in fourteen segments, a cable conduit down its back, and a lamp head (housing,
        /// lens, lit cap) with an antenna and its tip light.</summary>
        public static WeaponMesh Pylon()
        {
            var mesh=new WeaponMesh{Spec=PylonId,Tier=0};var s=new WeaponSculpt(mesh,0);float h=NativeEntranceStage.PylonHeight,top=h-.36f;
            s.Into(Body);
            s.Box(MetalDark,V(0,.05f,0),V(.78f,.1f,.78f),.02f);s.Box(Metal,V(0,.17f,0),V(.6f,.14f,.6f),.025f);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1}){s.Push(WeaponSculpt.Rx(-(float)Math.PI/2)*WeaponSculpt.T(x*.32f,0,z*.32f));s.Cylinder(MetalLight,.025f,.1f,.125f,6);s.Pop();}
            float c0=.24f,c1=top;s.Box(Metal,V(0,(c0+c1)*.5f,0),V(.4f,c1-c0,.4f),.03f);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})s.Box(MetalLight,V(x*.19f,(c0+c1)*.5f,z*.19f),V(.05f,c1-c0-.1f,.05f),.01f);
            for(float y=.74f;y<c1-.2f;y+=.5f)s.Box(Seam,V(0,y,0),V(.412f,.012f,.412f));
            foreach(float y in new[]{1f,2.4f})s.Box(MetalLight,V(0,y,0),V(.48f,.07f,.48f),.015f);
            // The channel on the lit face and the strip's segments in it.
            s.Box(Seam,V(0,(c0+c1)*.5f,-.2f),V(.12f,c1-c0-.3f,.02f));
            s.Push(WeaponSculpt.Rx(-(float)Math.PI/2)*WeaponSculpt.T(0,c0,.235f));s.Cylinder(MetalDark,.03f,0,c1-c0,8,.005f);s.Pop();
            // The lamp head: the housing, its lens on the lit face, the cap and the antenna.
            s.Box(Metal,V(0,top+.13f,0),V(.52f,.26f,.52f),.03f);s.Box(MetalLight,V(0,top+.02f,0),V(.56f,.04f,.56f),.01f);
            s.Push(WeaponSculpt.Rx(-(float)Math.PI/2)*WeaponSculpt.T(.12f,top+.26f,.12f));s.Cylinder(MetalLight,.012f,0,.34f,6);s.Pop();
            s.Into(Lights);
            float run=c1-c0-.4f,step=run/14;for(int i=0;i<14;i++)s.Box(Light(2f),V(0,c0+.2f+step*(i+.5f),-.212f),V(.06f,step*.82f,.012f));
            s.Box(Light(1.6f),V(0,top+.13f,-.262f),V(.34f,.09f,.01f));s.Box(Light(1.2f),V(0,top+.265f,0),V(.44f,.03f,.44f),.008f);
            s.Sphere(Light(2.4f),V(.12f,top+.62f,.12f),V(.02f,.02f,.02f),3,6,true);
            return mesh;
        }

        /// <summary>The dais (its top at NativeEntranceStage.FloorY, its foot at GroundY, circumradius DaisRadius): a bevelled
        /// hexagonal deck with dark seams from its middle to its corners and an inner hexagon of seams, a metal lip round its
        /// edge with a bolt at each corner, over a darker, wider skirt. Nothing on it glows (owner rule: no glowing disc or ring
        /// on the floor).</summary>
        public static WeaponMesh Dais()
        {
            var mesh=new WeaponMesh{Spec=DaisId,Tier=0};var s=new WeaponSculpt(mesh,0);float top=NativeEntranceStage.FloorY,foot=NativeEntranceStage.GroundY,R=NativeEntranceStage.DaisRadius;
            s.Into(Body);
            // Built in a frame whose +z is up (the slabs' thickness), its xy the floor.
            s.Push(WeaponSculpt.Rx(-(float)Math.PI/2));
            s.Slab(_=>Skirt,Skirt,Skirt,Hex(R+.12f,0),foot,foot+.06f,.02f);
            s.Slab(_=>Trim,Trim,Trim,Hex(R,0),foot+.04f,top-.012f,.015f);
            s.Slab(_=>Deck,Deck,Deck,Hex(R-.06f,0),top-.03f,top,.012f);
            var corner=Hex(R-.06f,0);var inner=Hex(1.55f,0);
            for(int k=0;k<6;k++)
            {
                var a=corner[k];Seam2(s,Vector2.Zero,new Vector2(a.X,a.Y)*.97f,top+.0015f,.014f);
                var b=inner[k];var c=inner[(k+1)%6];Seam2(s,new Vector2(b.X,b.Y),new Vector2(c.X,c.Y),top+.0015f,.014f);
                var at=Hex(R-.2f,0)[k];s.Cylinder(Trim,.035f,top-.002f,top+.004f,6,0,at.X,at.Y);
            }
            s.Pop();
            return mesh;
        }
        public const string PodId="entrance-drop-pod",PodDoorId="entrance-drop-pod-door";
        private static readonly WeaponPaint Hull=WeaponPaint.Solid(0x2a313f,.06f),HullTrim=WeaponPaint.Solid(0x4c5568,.1f),Scorch=WeaponPaint.Solid(0x14161b,.02f);
        /// <summary>Orbital Drop's pod (its base at the origin, the doorway toward local +z): seven bevelled hull panels round an
        /// octagon with ribs and a scorched panel line, the seams between all eight in the lights (the draw gives their heat),
        /// a faceted nose, a top and a base ring, four landing fins with foot pads and the floor inside.</summary>
        public static WeaponMesh Pod()
        {
            var mesh=new WeaponMesh{Spec=PodId,Tier=0};var s=new WeaponSculpt(mesh,0);
            s.Into(Body);
            for(int i=1;i<8;i++)
            {
                float a=i*(float)Math.PI/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(.66f*(float)Math.Sin(a),0,.66f*(float)Math.Cos(a)));
                s.Box(Hull,V(0,1.05f,0),V(.56f,2f,.07f),.012f);foreach(float y in new[]{.55f,1.55f})s.Box(HullTrim,V(0,y,.045f),V(.5f,.05f,.03f),.008f);s.Box(Scorch,V(0,1.05f,.037f),V(.5f,.012f,.004f));
                s.Pop();
            }
            void Column(in WeaponPaint p,float[] rz,int sides,float phase){s.Push(WeaponSculpt.Rx(-(float)Math.PI/2));s.Lathe(p,rz,sides,0,phase);s.Pop();}
            Column(Hull,new[]{.76f,2.03f,.74f,2.05f,.52f,2.4f,.22f,2.7f,.07f,2.8f},8,.39f);
            Column(HullTrim,new[]{.78f,1.99f,.78f,2.11f},8,.39f);Column(HullTrim,new[]{.8f,0f,.8f,.16f},8,.39f);
            for(int i=0;i<4;i++)
            {
                float a=i*(float)Math.PI/2+(float)Math.PI/4;s.Push(WeaponSculpt.Ry(a)*WeaponSculpt.T(.82f*(float)Math.Sin(a),0,.82f*(float)Math.Cos(a)));
                s.Box(HullTrim,V(0,.55f,0),V(.06f,1f,.34f),.01f);s.Box(Hull,V(0,.03f,.04f),V(.14f,.06f,.42f),.01f);s.Pop();
            }
            s.Box(Scorch,V(0,.17f,0),V(1f,.02f,1f));
            s.Into(Lights);
            for(int i=0;i<8;i++){float a=i*(float)Math.PI/4+.39f;s.Box(Light(1f),V(.7f*(float)Math.Sin(a),1.05f,.7f*(float)Math.Cos(a)),V(.03f,1.8f,.03f));}
            return mesh;
        }
        /// <summary>The pod's door (its centre at the origin, its outer face toward +z): a bevelled plate in a trim frame with two
        /// handles and its light strip (the draw gives the strip's glow).</summary>
        public static WeaponMesh PodDoor()
        {
            var mesh=new WeaponMesh{Spec=PodDoorId,Tier=0};var s=new WeaponSculpt(mesh,0);
            s.Into(Body);s.Box(Hull,V(0,0,0),V(.52f,1.62f,.07f),.012f);
            foreach(int x in new[]{-1,1})s.Box(HullTrim,V(x*.24f,0,.03f),V(.04f,1.58f,.03f),.006f);foreach(int y in new[]{-1,1})s.Box(HullTrim,V(0,y*.78f,.03f),V(.5f,.04f,.03f),.006f);
            foreach(int y in new[]{-1,1})s.Box(HullTrim,V(.15f,y*.32f,.06f),V(.04f,.16f,.03f),.008f);s.Box(Scorch,V(0,.2f,.037f),V(.44f,.012f,.004f));
            s.Into(Lights);s.Box(Light(1f),V(0,0,.045f),V(.3f,.05f,.02f));
            return mesh;
        }
        // A hexagon (counter-clockwise, a corner on +x) of circumradius r at depth z.
        private static Vector3[] Hex(float r,float z){var o=new Vector3[6];for(int i=0;i<6;i++){double a=i*Math.PI/3;o[i]=new Vector3(r*(float)Math.Cos(a),r*(float)Math.Sin(a),z);}return o;}
        // A dark seam from a to b (the deck's plane at depth z), `width` wide, as a flat quad facing +z.
        private static void Seam2(WeaponSculpt s,Vector2 a,Vector2 b,float z,float width)
        {
            var d=b-a;float len=d.Length();if(len<1e-5f)return;d*=width*.5f/len;var n=new Vector2(-d.Y,d.X);
            s.Quad(Seam,V(a.X-n.X,a.Y-n.Y,z),V(b.X-n.X,b.Y-n.Y,z),V(b.X+n.X,b.Y+n.Y,z),V(a.X+n.X,a.Y+n.Y,z));
        }
    }
}

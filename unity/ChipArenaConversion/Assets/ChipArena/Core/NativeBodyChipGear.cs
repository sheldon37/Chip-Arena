using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The body chips on the player's model (owner 2026-10-29: "go through the chips that provide visual changes and update them
    /// according to the new models (NO VOXEL appearances)"). NativeBodyChips builds what a pilot's Chips show (the source's
    /// bodyModifiers, kept for the Classic voxel body); on the model each piece becomes our own sculpted part, sized and placed
    /// for the model's body, never a primitive box at a voxel joint:
    /// <list type="bullet">
    /// <item>Chest (the pilot gear's core anchor, NativePilotGear: z out of the chest, y up, on the model's chest surface, measured
    /// per top by the bake): vital pips as red gems in bezels round the core, three teal block studs above it, green stamina
    /// lights down the ribs, the frame's two pectoral plates and violet mana orbs circling the torso (or parked behind the
    /// back while the frame plates are on, as the source's orbs are).</item>
    /// <item>Shoulders (the model's shoulder joint, the game arm's axes: y up the arm, x the rig's left): the frame's pauldrons,
    /// three lames over the deltoid.</item>
    /// <item>Feet (the model's ankle, the game foot's axes: y up, z to the toes): the boot chips' sabaton (an instep plate and an
    /// ankle cuff, gold with Salvage) and the dash chips' heel thruster, its flame lit while the pilot dashes.</item>
    /// </list>
    /// The parts are SourceLit sections (source frame; the Unity boundary mirrors x, as for the weapons): dark metal, the Chip's
    /// colour on tag 1 (lit on plates, glowing on lights; the draw passes it as the body tint). Presentation only, as the
    /// voxel pieces were: the same seat chips and stats decide what shows.
    /// </summary>
    public static class NativeBodyChipGear
    {
        public enum Anchor{Core,LeftArm,RightArm,LeftFoot,RightFoot}
        /// <summary>One model part: its mesh, where (anchor frame, source axes) and in what Chip colour.</summary>
        public struct Part{public string Id;public Anchor Anchor;public Vector3 Position;public Quaternion Rotation;public int Color;}
        public const string Pip="bodychip-pip",Node="bodychip-node",Strip="bodychip-strip",PlateL="bodychip-plate-l",PlateR="bodychip-plate-r",
            PauldronL="bodychip-pauldron-l",PauldronR="bodychip-pauldron-r",Sabaton="bodychip-sabaton",Nozzle="bodychip-nozzle",Flame="bodychip-flame",Orb="bodychip-orb";
        public static readonly string[] Ids={Pip,Node,Strip,PlateL,PlateR,PauldronL,PauldronR,Sabaton,Nozzle,Flame,Orb};
        /// <summary>Lights only (no housing to shade): no shadow.</summary>
        public static bool Light(string id)=>id==Pip||id==Node||id==Strip||id==Flame||id==Orb;
        /// <summary>Two parts at most per body chip (a dash jet's thruster and its flame; the frame's two plates).</summary>
        public const int MaxParts=NativeBodyChips.MaxPieces*2;

        // ---- the chest layout, round the core (anchor frame, metres; the core is 8.6 cm across, 6.5 cm tall) ----
        /// <summary>The chest's curve across (m): the surface falls back x²/2R from the core's plane.</summary>
        public const float ChestCurve=.2f;
        /// <summary>A point on the chest surface `lift` out, at (x, y) from the core.</summary>
        public static Vector3 OnChest(float x,float y,float lift)=>new Vector3(x,y,lift-x*x/(2*ChestCurve));
        /// <summary>The surface's turn there: out of the chest along the curve.</summary>
        public static Quaternion ChestTurn(float x)=>CapturedRig.Rot(Vector3.UnitY,(float)Math.Atan(x/ChestCurve));
        // Six vital pips: either side of the core, above it, then lower either side and below; three block studs over the top.
        public static readonly Vector2[] VitalSlots={new Vector2(-.068f,.016f),new Vector2(.068f,.016f),new Vector2(0,.058f),new Vector2(-.066f,-.021f),new Vector2(.066f,-.021f),new Vector2(0,-.058f)};
        public static readonly Vector2[] BlockSlots={new Vector2(-.04f,.088f),new Vector2(0,.095f),new Vector2(.04f,.088f)};
        /// <summary>Stamina light `index` (0-4 the rig's right column, 5-9 its left), up the ribs.</summary>
        public static Vector2 StaminaSlot(int index)=>new Vector2((index<5?-1:1)*.104f,-.095f+(index%5)*.016f);
        /// <summary>The orbs' circle about the torso (anchor frame) and where they park behind the back under the frame plates.</summary>
        public static readonly Vector3 OrbitCentre=new Vector3(0,.03f,-.10f),Parked=new Vector3(0,.05f,-.31f);
        public const float OrbitX=.32f,OrbitZ=.26f,OrbitRise=.06f;
        /// <summary>Orb `orbit` at `time` (the source's 1.5 rad/s, a sixth of a turn apart).</summary>
        public static Vector3 OrbAt(int orbit,float time)
        {
            if(orbit<0)return Parked;double a=time*1.5+orbit*Math.PI/3;
            return OrbitCentre+new Vector3((float)Math.Cos(a)*OrbitX,(float)Math.Sin(a*2)*OrbitRise,(float)Math.Sin(a)*OrbitZ);
        }

        /// <summary>
        /// The model parts of one body chip (NativeBodyChips.Build, animated), appended to `o` from `n`; returns the new count.
        /// A dash jet's flame shows only while it is lit (Visible), its thruster always.
        /// </summary>
        public static int Parts(in BodyChip c,float time,Part[] o,int n)
        {
            if(o==null)return n;int color=c.Piece.Color;
            void Add(string id,Anchor at,Vector3 p,Quaternion q){if(n<o.Length)o[n++]=new Part{Id=id,Anchor=at,Position=p,Rotation=q,Color=color};}
            switch(c.Role)
            {
                case BodyChipRole.Vital:{var s=VitalSlots[Math.Max(0,Math.Min(VitalSlots.Length-1,c.Index))];Add(Pip,Anchor.Core,OnChest(s.X,s.Y,.002f),ChestTurn(s.X));break;}
                case BodyChipRole.Block:{var s=BlockSlots[Math.Max(0,Math.Min(BlockSlots.Length-1,c.Index))];Add(Node,Anchor.Core,OnChest(s.X,s.Y,.002f),ChestTurn(s.X));break;}
                case BodyChipRole.Stamina:{var s=StaminaSlot(c.Index);Add(Strip,Anchor.Core,OnChest(s.X,s.Y,.005f),ChestTurn(s.X));break;}
                case BodyChipRole.Frame:Add(PlateL,Anchor.Core,Vector3.Zero,Quaternion.Identity);Add(PlateR,Anchor.Core,Vector3.Zero,Quaternion.Identity);break;
                case BodyChipRole.FrameArm:Add(c.Index==0?PauldronL:PauldronR,c.Index==0?Anchor.LeftArm:Anchor.RightArm,Vector3.Zero,Quaternion.Identity);break;
                case BodyChipRole.Orb:Add(Orb,Anchor.Core,OrbAt(c.Piece.Orbit,time),CapturedRig.Rot(Vector3.UnitY,time*2.1f+c.Index));break;
                case BodyChipRole.Boot:Add(Sabaton,c.Index==0?Anchor.LeftFoot:Anchor.RightFoot,Vector3.Zero,Quaternion.Identity);break;
                case BodyChipRole.Jet:{var at=c.Index==0?Anchor.LeftFoot:Anchor.RightFoot;Add(Nozzle,at,Vector3.Zero,Quaternion.Identity);if(c.Visible)Add(Flame,at,Vector3.Zero,Quaternion.Identity);break;}
            }
            return n;
        }

        // ---- the parts' meshes (source frame) ----
        private static readonly WeaponPaint Dark=WeaponPaint.Solid(0x1d252c,.03f),Metal=WeaponPaint.Solid(0x3c4a55,.05f),Rivet=WeaponPaint.Solid(0x8b99a3,.08f);
        private static WeaponPaint Tone(float k=.8f)=>new WeaponPaint(Vector3.One*k,Vector3.One*k*.04f,1);
        private static WeaponPaint Glow(float strength=2,float albedo=.3f)=>new WeaponPaint(Vector3.One*albedo,Vector3.One*strength,1);
        /// <summary>The frame whose x, y and z go along u, v and n (u × v = n), at o.</summary>
        private static Matrix4x4 Frame(Vector3 o,Vector3 u,Vector3 v,Vector3 n)=>new Matrix4x4(u.X,u.Y,u.Z,0,v.X,v.Y,v.Z,0,n.X,n.Y,n.Z,0,o.X,o.Y,o.Z,1);
        /// <summary>A plate's frame: its face out along `normal`, its x along `along` (made square to it).</summary>
        private static Matrix4x4 Facing(Vector3 o,Vector3 normal,Vector3 along)
        {
            var n=Vector3.Normalize(normal);var u=Vector3.Normalize(along-n*Vector3.Dot(along,n));return Frame(o,u,Vector3.Cross(n,u),n);
        }
        private static Vector3[] Outline(params float[] xy)=>WeaponSculpt.Outline(0,xy);
        /// <summary>A bevelled plate: dark edges and back, its face in the Chip's colour with a raised inner panel.</summary>
        private static void Plate(WeaponSculpt k,Vector3[] outline,float thick,float tone=.8f)
        {
            k.Slab(_=>Dark,Tone(tone),Dark,outline,-thick*.4f,thick*.6f,.0025f);
            k.Slab(_=>Tone(tone*.85f),Tone(tone*.78f),Dark,WeaponSculpt.Inset(outline,.0065f,0),thick*.6f-.0004f,thick*.6f+.0012f,.0006f);
        }
        private static void Rivets(WeaponSculpt k,float z,params float[] xy){for(int i=0;i+1<xy.Length;i+=2)k.Cylinder(Rivet,.0026f,z,z+.0022f,6,0,xy[i],xy[i+1],0);}

        /// <summary>A fresh mesh of one part, or null for an unknown id.</summary>
        public static WeaponMesh Build(string id)
        {
            bool right=id==PlateR||id==PauldronR;string built=id==PlateR?PlateL:id==PauldronR?PauldronL:id;
            var mesh=new WeaponMesh{Spec="bodychip",Tier=0};var k=new WeaponSculpt(mesh,0);k.Into(-1);
            switch(built)
            {
                case Pip:k.Cylinder(Metal,.0095f,0,.0035f,10,.0012f);k.Gem(Glow(2.2f),new Vector3(0,0,.0045f),.0068f,.007f,8,.35f);break;
                case Node:k.Cylinder(Dark,.011f,0,.0055f,6,.0015f);k.Cylinder(Glow(2),.0068f,.0055f,.0076f,6);break;
                case Strip:k.Box(Dark,new Vector3(0,0,.0018f),new Vector3(.017f,.0075f,.0036f),.0012f);k.Box(Glow(2.2f),new Vector3(0,0,.0039f),new Vector3(.0125f,.004f,.0012f));break;
                case PlateL:
                {
                    // The left pectoral (+x): on the chest's curve beside the core, turned with it.
                    var c=OnChest(.117f,.056f,.006f);var n=new Vector3(.117f/ChestCurve,0,1);
                    k.Push(Facing(c,n,Vector3.UnitX));Plate(k,Outline(-.034f,-.054f,.024f,-.044f,.04f,.006f,.026f,.05f,-.026f,.056f,-.038f,0),.012f);Rivets(k,.0078f,-.022f,-.04f,.024f,-.028f,-.016f,.044f,.026f,.034f);k.Pop();
                    break;
                }
                case PauldronL:
                {
                    // Three lames over the left deltoid (+x outward): the cap, the side plate and the lower lame.
                    k.Push(Facing(new Vector3(.034f,.05f,0),new Vector3(.8f,1,0),Vector3.UnitZ));Plate(k,Outline(-.06f,-.03f,.06f,-.03f,.066f,0,.054f,.03f,-.054f,.03f,-.066f,0),.01f,.82f);Rivets(k,.0068f,-.042f,0,.042f,0);k.Pop();
                    k.Push(Facing(new Vector3(.068f,-.002f,0),new Vector3(1,-.08f,0),Vector3.UnitZ));Plate(k,Outline(-.052f,-.028f,.052f,-.028f,.056f,0,.048f,.028f,-.048f,.028f,-.056f,0),.009f,.76f);k.Pop();
                    k.Push(Facing(new Vector3(.064f,-.054f,0),new Vector3(1,-.26f,0),Vector3.UnitZ));Plate(k,Outline(-.046f,-.02f,.046f,-.02f,.05f,0,.042f,.02f,-.042f,.02f,-.05f,0),.008f,.72f);k.Pop();
                    break;
                }
                case Sabaton:
                {
                    // The instep plate down over the shoe, clear of its top, and the cuff round the ankle.
                    var n=new Vector3(0,.883f,.469f);k.Push(Facing(new Vector3(0,-.022f,.082f)+n*.012f,n,Vector3.UnitX));
                    Plate(k,Outline(-.034f,-.06f,.034f,-.06f,.046f,0,.04f,.05f,-.04f,.05f,-.046f,0),.009f,.78f);Rivets(k,.0058f,-.028f,.03f,.028f,.03f);k.Pop();
                    k.Push(Frame(new Vector3(0,.035f,0),Vector3.UnitX,-Vector3.UnitZ,Vector3.UnitY));k.Cylinder(Tone(.72f),.058f,-.014f,.014f,10,.004f);k.Cylinder(Dark,.06f,-.003f,.003f,10);k.Pop();
                    break;
                }
                case Nozzle:
                {
                    // A heel thruster pointing back (-z): a dark bell round a lit throat.
                    k.Push(Frame(new Vector3(0,-.05f,-.06f),-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitZ));
                    k.Lathe(Metal,new[]{.011f,0f,.014f,.008f,.018f,.02f},10,1,0,false);k.Cylinder(Glow(2.4f),.0155f,.012f,.0155f,10);k.Cylinder(Dark,.0115f,-.006f,.001f,10,.002f);k.Pop();
                    break;
                }
                case Flame:
                    k.Push(Frame(new Vector3(0,-.05f,-.08f),-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitZ));k.Lathe(Glow(2.6f,.2f),new[]{.014f,0f,.012f,.03f,.007f,.08f,0f,.14f},8);k.Pop();break;
                case Orb:
                    k.Gem(Glow(2.2f),Vector3.Zero,.017f,.036f,6,.5f);k.Push(WeaponSculpt.Rx(1.1f));k.Torus(Glow(1.6f,.25f),Vector3.Zero,.027f,.0022f,18,4);k.Pop();break;
                default:return null;
            }
            mesh.Sections.RemoveAll(x=>x.Positions.Count==0);if(mesh.Sections.Count==0)return null;
            if(right)foreach(var x in mesh.Sections)
            {
                for(int i=0;i<x.Positions.Count;i++){var p=x.Positions[i];p.X=-p.X;x.Positions[i]=p;var nn=x.Normals[i];nn.X=-nn.X;x.Normals[i]=nn;}
                for(int i=0;i+2<x.Indices.Count;i+=3){int t=x.Indices[i+1];x.Indices[i+1]=x.Indices[i+2];x.Indices[i+2]=t;}
            }
            return mesh;
        }
    }
}

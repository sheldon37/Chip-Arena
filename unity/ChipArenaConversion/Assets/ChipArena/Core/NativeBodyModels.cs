using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One part of a built body skin: the exported part's name and joint, its triangles (bank frame) and its lights.</summary>
    public sealed class PilotBodyPart
    {
        public string Name;public int Bone;public bool Emitter;
        /// <summary>The part's emitter level (1.4 trims, 2.4 strips: emission over albedo, as NativeArmorLight reads the export).</summary>
        public float Level;
        public WeaponSection Mesh;
        /// <summary>
        /// Light boxes in the part's joint frame: an emitter part's own glowing voxels (its armour-light profile and spill), and
        /// the accent strips set into a receiver part (the visor, arm and thigh strips), which light the armour round them.
        /// </summary>
        public readonly List<(Vector3 min,Vector3 max,float level)> Lights=new List<(Vector3,Vector3,float)>();
        /// <summary>The Sheldon Clothes' coat panels on this joint as solids (joint frame), for the clipping measurement; Gib marks
        /// the main panels, which a voxel-burst or crushed body also throws as cloth (NativeBodyModels.Gibs).</summary>
        public readonly List<(Vector3 min,Vector3 max,bool gib)> Cloth=new List<(Vector3,Vector3,bool)>();
        public int Triangles=>Mesh.Triangles;
    }
    /// <summary>One detail tier of one body skin: every exported part, in the export's order.</summary>
    public sealed class PilotBodyMesh
    {
        public int Skin,Tier;public PilotBodyPart[] Parts;
        public int Triangles{get{int n=0;foreach(var p in Parts)n+=p.Triangles;return n;}}
        public int Vertices{get{int n=0;foreach(var p in Parts)n+=p.Mesh.Vertices;return n;}}
    }

    // The Standard and Sheldon Clothes bodies, sculpted in code (engine-free and deterministic, so the managed checks build
    // exactly what the player builds). Authored straight in the bank frame of the exported pilot (Unity frame: the source's
    // x mirrored; front +z, a left limb's outside -x), so the Runtime uploads the arrays as they are: no mirror, no winding
    // turn (the kit's faces already face out by the cross product of their edges, the bank's convention). Every part keeps
    // the exported part's name and joint and sits inside its box plus a little relief (NativeBodySkins.StandardRelief,
    // SheldonRelief). Right limbs are the left ones mirrored. Detail by tier: Full has everything; Reduced drops the panel
    // seams, fine greebles and the smallest bevels; Low drops every bevel and the secondary plates.
    public static class NativeBodyModels
    {
        // ---- paints (linear; tag 1 takes the pilot's body tint, 2 its accent, 0 stays as authored) ----
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static readonly Vector3 ShellColor=V(.784f,.799f,.831f);
        /// <summary>The export's shell self-glow (GLOW.body) over its albedo, kept so the plates take the arena light as the old shell.</summary>
        private const float ShellGlow=.227f;
        private static WeaponPaint Shell(float k)=>new WeaponPaint(ShellColor*k,ShellColor*k*ShellGlow,1);
        private static WeaponPaint Plain(Vector3 c)=>new WeaponPaint(c,Vector3.Zero,0);
        private static readonly Vector3 AccentColor=V(.044f,.505f,.41f);
        /// <summary>A glowing accent (tag 2): the export's ember at `level` (1.4 trims, 2.4 strips), tinted to the pilot's accent.</summary>
        private static WeaponPaint Glow(float level)=>new WeaponPaint(AccentColor,AccentColor*level,2);
        private static WeaponPaint Led(int hex){var c=NativePortraitRules.Linear(hex);return new WeaponPaint(c*.3f,c*2.2f,0);}
        private static WeaponPaint Cloth(int hex,float k=1){var c=NativePortraitRules.Linear(hex)*k;return new WeaponPaint(c,c*.12f,1);}
        private static readonly WeaponPaint
            Plate=Shell(1),Plate2=Shell(.9f),Plate3=Shell(.72f),Seam=Plain(V(.016f,.02f,.026f)),
            Suit=Plain(V(.047f,.065f,.078f)),SuitLight=Plain(V(.085f,.105f,.12f)),Grey=Plain(V(.468f,.485f,.509f)),GreyLight=Plain(V(.56f,.575f,.6f)),
            GreyDark=Plain(V(.257f,.267f,.28f)),Sole=Plain(V(.026f,.036f,.043f)),Trim=Glow(1.4f),Strip=Glow(2.4f),Visor=Glow(2.6f),
            LedCyan=Led(NativeBodySkins.CyberCyan),LedMagenta=Led(NativeBodySkins.CyberMagenta);
        // The Sheldon Clothes: the Sheldon Head's hood fabric (NativeSheldonHead.Fabric / FabricTop) as body-tinted cloth, so the
        // default body colour shows the hood's own navy and a picked colour a dark version of it.
        private static readonly WeaponPaint
            Fabric=Cloth(NativeSheldonHead.FabricTop,2f),FabricDark=Cloth(NativeSheldonHead.Fabric,1.3f),Lapel=Cloth(0x3a4466),Inner=Cloth(NativeSheldonHead.Inner),
            Frame=Plain(V(.02f,.024f,.034f)),Glove=Plain(V(.03f,.034f,.045f)),FacePlate=Plain(NativePortraitRules.Linear(NativeSheldonHead.PlateColor)),
            Mask=Cloth(0x2c3348),SheldonTrim=Glow(1.4f),SheldonStrip=Glow(2.2f);

        /// <summary>
        /// A body's armour-light sections (NativeArmorLight.Build): its parts in order (an emitter part with its glowing voxels,
        /// a receiver with its bounds), then one emitter section per strip set into a receiver (the visor, the arm and thigh
        /// strips), which lights that part and its neighbours but owns no material; the first Parts.Length plans are the parts'.
        /// </summary>
        public static List<NativeArmorLight.Section> LightSections(PilotBodyMesh body)
        {
            var sections=new List<NativeArmorLight.Section>();var strips=new List<NativeArmorLight.Section>();
            foreach(var p in body.Parts)
            {
                var min=new Vector3(float.MaxValue);var max=new Vector3(float.MinValue);foreach(var v in p.Mesh.Positions){min=Vector3.Min(min,v);max=Vector3.Max(max,v);}
                if(p.Mesh.Positions.Count==0)min=max=Vector3.Zero;
                var s=new NativeArmorLight.Section{Part=p.Name,Bone=p.Bone,Emitter=p.Emitter&&p.Lights.Count>0,Level=p.Level,Min=min,Max=max};
                if(s.Emitter)foreach(var l in p.Lights)s.Voxels.Add((l.min,l.max));else s.Voxels.Add((min,max));
                sections.Add(s);
                if(!s.Emitter)foreach(var l in p.Lights){var strip=new NativeArmorLight.Section{Part=p.Name+"#strip",Bone=p.Bone,Emitter=true,Level=l.level,Min=l.min,Max=l.max};strip.Voxels.Add((l.min,l.max));strips.Add(strip);}
            }
            sections.AddRange(strips);return sections;
        }
        /// <summary>The Sheldon Clothes' main coat panels as kill boxes (source axes, on their joints, owned by that joint's ragdoll
        /// part; fixed cloth colour, details), built once.</summary>
        public static KillBox[] SheldonGibs=>sheldonGibs??=MakeGibs();private static KillBox[] sheldonGibs;
        private static KillBox[] MakeGibs()
        {
            var list=new List<KillBox>();var body=Build(NativeBodySkins.SheldonIndex,NativeBodySkins.Full);int hex=Hex(NativePortraitRules.Linear(NativeSheldonHead.FabricTop)*2f);
            foreach(var p in body.Parts)foreach(var c in p.Cloth)
            {
                if(!c.gib)continue;int root=p.Bone;while(root>=0&&Array.IndexOf(NativeKillBoxes.PartRoots,root)<0)root=CapturedRig.Bones[root].Parent;
                var mid=(c.min+c.max)*.5f;var size=c.max-c.min;
                list.Add(new KillBox("sheldon-cloth",p.Bone,root,0,true,false,0,hex,0,0,0,-mid.X,mid.Y,mid.Z,0,0,0,1,size.X,size.Y,size.Z));
            }
            return list.ToArray();
        }
        private static int Hex(Vector3 linear)
        {
            int C(float v){v=Math.Max(0,Math.Min(1,v));double s=v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055;return (int)Math.Round(s*255);}
            return C(linear.X)<<16|C(linear.Y)<<8|C(linear.Z);
        }
        /// <summary>
        /// A corpse's kill boxes (NativeKillBoxes.Rig) with the body skin's cloth: the Sheldon Clothes append their coat panels
        /// (a voxel burst or a crush throws the cloth too; a severed leg already carries its panels on its joints). Every other
        /// skin adds nothing (the Standard plates are inside the exported boxes). Returns the new count; panels that do not fit
        /// are left out.
        /// </summary>
        public static int Gibs(int skin,KillBox[] into,int n)
        {
            if(skin!=NativeBodySkins.SheldonIndex)return n;foreach(var b in SheldonGibs)if(n<into.Length)into[n++]=b;return n;
        }
        /// <summary>Builds one tier of a body skin (NativeBodySkins.StandardIndex or SheldonIndex); null for the Prototype.</summary>
        public static PilotBodyMesh Build(int skin,int tier)
        {
            if(skin!=NativeBodySkins.StandardIndex&&skin!=NativeBodySkins.SheldonIndex)return null;
            tier=Math.Max(NativeBodySkins.Full,Math.Min(NativeBodySkins.Low,tier));
            var body=new PilotBodyMesh{Skin=skin,Tier=tier,Parts=new PilotBodyPart[NativeBodySkins.Parts.Length]};
            var k=new Sculptor(body,tier);
            if(skin==NativeBodySkins.StandardIndex)Standard(k);else SheldonClothes(k);
            for(int i=0;i<body.Parts.Length;i++)if(body.Parts[i]==null)k.Part(NativeBodySkins.Parts[i].Name,NativeBodySkins.Parts[i].Bone);
            return body;
        }

        // ---- the kit's adapter: one kit section per exported part, left-authored limbs mirrored for the right ----
        private sealed class Sculptor
        {
            public readonly int Tier;private readonly PilotBodyMesh body;private WeaponSculpt s;private PilotBodyPart part;private float side=1;
            public Sculptor(PilotBodyMesh body,int tier){this.body=body;Tier=tier;}
            public bool Fine=>Tier==0;public bool Mid=>Tier<=1;
            /// <summary>Starts (or resumes) the part `name` on joint `bone`; `mirror` draws a left-authored limb on the right.</summary>
            public void Part(string name,int bone,bool mirror=false)
            {
                int i=NativeBodySkins.PartIndex(name,bone);if(i<0)throw new ArgumentException("No exported part "+name+" on joint "+bone);
                var spec=NativeBodySkins.Parts[i];
                if(body.Parts[i]==null){var mesh=new WeaponMesh();var kit=new WeaponSculpt(mesh,Tier);kit.Into(bone);body.Parts[i]=new PilotBodyPart{Name=spec.Name,Bone=bone,Emitter=spec.Emitter,Mesh=mesh.Sections[0]};sculpts[i]=kit;}
                part=body.Parts[i];s=sculpts[i];side=mirror?-1:1;
            }
            private readonly WeaponSculpt[] sculpts=new WeaponSculpt[NativeBodySkins.Parts.Length];
            private (Vector3 min,Vector3 max) M(float x0,float x1,float y0,float y1,float z0,float z1)
            {
                float a=x0*side,b=x1*side;return (V(Math.Min(a,b),y0,z0),V(Math.Max(a,b),y1,z1));
            }
            /// <summary>A box from its bounds (left-authored x), chamfered by `bevel` (dropped on the low tier by the kit).</summary>
            public void Box(in WeaponPaint p,float x0,float x1,float y0,float y1,float z0,float z1,float bevel=0)
            {
                // Reduced keeps only the big plates' chamfers (the kit drops every bevel on Low).
                if(Tier==NativeBodySkins.Reduced&&bevel<.012f)bevel=0;
                var (min,max)=M(x0,x1,y0,y1,z0,z1);s.Box(p,(min+max)*.5f,max-min,bevel);
            }
            /// <summary>A glowing box that also lights its neighbours (an emitter's voxel, or a strip set into a receiver).</summary>
            public void Light(in WeaponPaint p,float level,float x0,float x1,float y0,float y1,float z0,float z1,float bevel=0)
            {
                Box(p,x0,x1,y0,y1,z0,z1,bevel);var (min,max)=M(x0,x1,y0,y1,z0,z1);part.Lights.Add((min,max,level));
                if(part.Emitter&&part.Level<level)part.Level=level;
            }
            /// <summary>A coat panel: a box that is also recorded as cloth for the clipping measurement (and, unless `flare`, the
            /// gibs).</summary>
            public void Panel(in WeaponPaint p,float x0,float x1,float y0,float y1,float z0,float z1,float bevel=0,bool flare=false)
            {
                Box(p,x0,x1,y0,y1,z0,z1,bevel);var (min,max)=M(x0,x1,y0,y1,z0,z1);part.Cloth.Add((min,max,!flare));
            }
            /// <summary>
            /// An inset panel line: a dark quad lifted .8 mm off the face of `axis` (0 x, 1 y, 2 z) at `at`, facing `sign`, spanning
            /// [u0,u1] x [v0,v1] on the next two axes in turn (x face: y then z; y face: z then x; z face: x then y; x mirrored
            /// like the boxes). Full detail only.
            /// </summary>
            public void Line(int axis,int sign,float at,float u0,float u1,float v0,float v1,in WeaponPaint? paint=null)
            {
                if(!Fine)return;var c=paint??Seam;
                Vector3 P(float u,float v){var q=new float[3];q[axis]=at+sign*.0008f;q[(axis+1)%3]=u;q[(axis+2)%3]=v;return V(q[0]*side,q[1],q[2]);}
                var n=Vector3.Zero;if(axis==0)n.X=sign*side;else if(axis==1)n.Y=sign;else n.Z=sign;
                Vector3 a=P(u0,v0),b=P(u1,v0),d=P(u1,v1),e=P(u0,v1);
                if(Vector3.Dot(Vector3.Cross(b-a,e-a),n)<0){var t=b;b=e;e=t;}
                s.Quad(c,a,b,d,e,true);
            }
            /// <summary>A hinge disc: a short cylinder along x through (x, y, z) (secondary detail: Full and Reduced).</summary>
            public void Disc(in WeaponPaint p,float x,float y,float z,float r,float half,int sides=8)
            {
                if(!Mid)return;float cx=x*side;s.Push(WeaponSculpt.Ry((float)Math.PI/2)*WeaponSculpt.T(0,y,z));s.Cylinder(p,r,cx-half,cx+half,s.Sides(sides),.004f);s.Pop();
            }
        }

        // ================================================================================================================
        // Standard: the same voxel silhouette in sharper armour (bevelled plates over a dark undersuit, inset panel lines,
        // layered chest and back plates, pauldrons, articulated elbows and knees, a tech spine and backpack, a visor slit).
        // ================================================================================================================
        private static void Standard(Sculptor k)
        {
            // ---- head: helmet, face plate, visor slit under a brow, cheek guards, ear modules with status lights ----
            k.Part("blank-head",CapturedRig.Head);
            k.Box(Plate,-.162f,.162f,.012f,.322f,-.152f,.148f,.03f);
            k.Box(Plate2,-.128f,.128f,.03f,.25f,.12f,.156f,.012f);
            k.Box(Suit,-.118f,.118f,.158f,.214f,.13f,.159f,.006f);
            k.Light(Visor,2.6f,-.104f,.104f,.172f,.198f,.15f,.161f);
            k.Box(Plate,-.15f,.15f,.214f,.248f,.06f,.158f,.01f);
            foreach(bool m in new[]{false,true})
            {
                k.Part("blank-head",CapturedRig.Head,m);
                k.Box(Plate2,-.166f,-.128f,.04f,.17f,0,.14f,.01f);
                k.Box(Grey,-.17f,-.152f,.11f,.2f,-.05f,.04f,.006f);
                if(k.Mid)k.Box(LedCyan,-.172f,-.168f,.17f,.185f,-.035f,-.02f);
                k.Line(0,-1,-.162f,.03f,.3f,.05f,.056f);
                k.Line(0,-1,-.166f,.155f,.161f,.01f,.13f);
            }
            k.Part("blank-head",CapturedRig.Head);
            if(k.Mid)k.Box(Plate2,-.1f,.1f,.07f,.235f,-.166f,-.14f,.008f);
            for(int i=0;i<3;i++)k.Line(2,1,.156f,-.06f,.06f,.066f+i*.022f,.074f+i*.022f);
            k.Line(2,-1,-.166f,-.08f,.08f,.15f,.156f);

            k.Part("crown",CapturedRig.Head);
            // The crown's top stays flat at the exported .36: hats sit on it.
            k.Box(Plate,-.14f,.14f,.32f,.36f,-.13f,.13f,.014f);
            k.Line(2,1,.13f,-.12f,.12f,.336f,.34f);k.Line(2,-1,-.13f,-.12f,.12f,.336f,.34f);

            k.Part("jaw",CapturedRig.Head);
            k.Box(Grey,-.13f,.13f,-.033f,.008f,-.115f,.146f,.01f);
            if(k.Mid)k.Box(GreyLight,-.06f,.06f,-.035f,0,.12f,.15f,.006f);

            k.Part("neck-joint",CapturedRig.Neck);
            k.Box(Suit,-.066f,.066f,-.045f,.095f,-.066f,.066f,.012f);
            if(k.Mid){k.Box(SuitLight,-.072f,.072f,-.005f,.015f,-.072f,.072f,.006f);k.Box(SuitLight,-.072f,.072f,.04f,.06f,-.072f,.072f,.006f);}
            if(k.Fine)foreach(bool m in new[]{false,true}){k.Part("neck-joint",CapturedRig.Neck,m);k.Box(Grey,.035f,.048f,-.04f,.09f,.062f,.074f);}

            // ---- chest: torso, framed pectoral plates round recessed badge panels, flank plates with vents, back plate ----
            k.Part("ribcage",CapturedRig.Chest);
            k.Box(Plate2,-.25f,.25f,.05f,.25f,-.142f,.14f,.025f);
            k.Box(Plate,-.225f,.225f,-.05f,.075f,-.132f,.126f,.016f);
            k.Box(Plate2,-.19f,.19f,-.045f,.07f,.1f,.134f,.01f);
            k.Box(Plate,-.2f,.2f,.06f,.24f,-.15f,-.12f,.012f);
            k.Line(2,1,.134f,-.17f,.17f,.01f,.015f);
            k.Line(2,1,.126f,-.2f,.2f,.068f,.073f);
            foreach(bool m in new[]{false,true})
            {
                k.Part("ribcage",CapturedRig.Chest,m);
                // The left breast's recess floor stays at z .149 so the Sheldon Head's chevron (bank -.12, .15, .1525) shows over it.
                k.Box(Plate,-.232f,-.032f,.185f,.218f,.1f,.158f,.01f);
                k.Box(Plate,-.232f,-.032f,.075f,.118f,.1f,.158f,.01f);
                k.Box(Plate,-.088f,-.032f,.118f,.185f,.1f,.158f,.008f);
                k.Box(Plate,-.232f,-.155f,.118f,.185f,.1f,.156f,.008f);
                k.Box(Plate2,-.155f,-.088f,.118f,.185f,.12f,.149f,.004f);
                k.Box(Plate,-.27f,-.248f,.085f,.235f,-.1f,.1f,.008f);
                for(int i=0;i<3;i++)k.Line(0,-1,-.27f,.12f+i*.025f,.128f+i*.025f,-.06f,.06f);
                k.Line(2,-1,-.15f,-.19f,-.03f,.15f,.155f);
            }
            k.Part("upper-chest",CapturedRig.Chest);
            k.Box(Plate,-.228f,.228f,.205f,.27f,-.136f,.136f,.016f);
            if(k.Mid)k.Box(Plate2,-.17f,.17f,.245f,.272f,.1f,.14f,.008f);
            k.Line(1,1,.27f,-.03f,-.025f,-.2f,.2f);
            k.Line(2,-1,-.136f,-.2f,.2f,.235f,.24f);

            k.Part("collar",CapturedRig.Chest);
            k.Box(Plate,-.135f,.135f,.265f,.305f,-.115f,.095f,.012f);
            k.Box(Suit,-.098f,.098f,.295f,.315f,-.08f,.062f,.006f);
            k.Line(2,1,.095f,-.004f,.004f,.27f,.3f);

            // The chest's core: a reactor in a dark housing, spine strips above and below it, rib lines under the pectorals.
            k.Part("sternum",CapturedRig.Chest);
            k.Box(Suit,-.048f,.048f,.115f,.185f,.13f,.168f,.008f);
            k.Light(Trim,1.4f,-.032f,.032f,.128f,.172f,.15f,.174f);
            k.Light(Trim,1.4f,-.01f,.01f,.025f,.115f,.14f,.163f);
            k.Light(Trim,1.4f,-.01f,.01f,.185f,.235f,.14f,.163f);
            k.Light(Trim,1.4f,.05f,.2f,.06f,.072f,.12f,.15f);k.Light(Trim,1.4f,-.2f,-.05f,.06f,.072f,.12f,.15f);

            // The backpack: a housing with vent fins and two status lights round the spine strip.
            k.Part("back-panel",CapturedRig.Chest);
            k.Box(Grey,-.125f,.125f,0,.2f,-.17f,-.136f,.012f);
            foreach(bool m in new[]{false,true})
            {
                k.Part("back-panel",CapturedRig.Chest,m);
                if(k.Fine)for(int i=0;i<3;i++)k.Box(GreyDark,-.11f+i*.025f,-.098f+i*.025f,.03f,.16f,-.178f,-.168f);
                else if(k.Mid)k.Box(GreyDark,-.11f,-.048f,.03f,.16f,-.176f,-.168f,.004f);
                k.Line(1,1,.2f,-.16f,-.155f,-.12f,-.02f);
            }
            k.Part("back-panel",CapturedRig.Chest);
            if(k.Mid){k.Box(LedCyan,.1f,.118f,.175f,.188f,-.176f,-.168f);k.Box(LedMagenta,-.118f,-.1f,.175f,.188f,-.176f,-.168f);}

            k.Part("armor-back-light",CapturedRig.Chest);
            k.Light(Strip,2.4f,-.009f,.009f,.02f,.172f,-.181f,-.17f);
            k.Light(Strip,2.4f,-.1f,.1f,.18f,.192f,-.181f,-.17f);
            if(k.Mid){k.Light(Strip,2.4f,.03f,.06f,.09f,.097f,-.181f,-.17f);k.Light(Strip,2.4f,-.06f,-.03f,.09f,.097f,-.181f,-.17f);}

            // ---- abdomen: a ribbed dark core in segmented plates (two rows at the front, plates at the back) ----
            k.Part("abdomen",CapturedRig.Spine);
            k.Box(Suit,-.168f,.168f,-.012f,.162f,-.12f,.12f,.02f);
            if(k.Mid){k.Box(SuitLight,-.172f,.172f,.03f,.045f,-.122f,.122f,.004f);k.Box(SuitLight,-.172f,.172f,.1f,.115f,-.122f,.122f,.004f);}
            k.Part("abdomen-shell",CapturedRig.Spine);
            k.Box(Plate2,-.176f,.176f,.012f,.128f,-.134f,.13f,.014f);
            k.Box(Plate3,-.168f,.168f,.128f,.148f,-.126f,.126f,.006f);
            foreach(bool m in new[]{false,true})
            {
                k.Part("abdomen-shell",CapturedRig.Spine,m);
                k.Box(Plate,-.09f,-.012f,.018f,.066f,.12f,.143f,.008f);k.Box(Plate,-.09f,-.012f,.074f,.122f,.12f,.143f,.008f);
                if(k.Mid)k.Box(Plate,-.12f,-.02f,.03f,.11f,-.142f,-.12f,.008f);
                k.Line(0,-1,-.176f,.06f,.065f,-.12f,.11f);
            }

            // ---- pelvis: faulds at the front, side tassets, a rear plate; the belt with its glowing line and buckle ----
            k.Part("hip-shell",CapturedRig.Pelvis);
            k.Box(Plate2,-.205f,.205f,-.105f,.105f,-.136f,.134f,.022f);
            if(k.Mid)k.Box(Plate,-.15f,.15f,-.09f,.03f,-.148f,-.12f,.01f);
            foreach(bool m in new[]{false,true})
            {
                k.Part("hip-shell",CapturedRig.Pelvis,m);
                k.Box(Plate,-.17f,-.025f,-.095f,.03f,.11f,.148f,.012f);
                k.Box(Plate,-.224f,-.2f,-.085f,.04f,-.08f,.08f,.008f);
                k.Line(2,1,.148f,-.16f,-.035f,-.035f,-.03f);
                k.Line(0,-1,-.224f,-.03f,-.025f,-.07f,.07f);
            }
            k.Part("waist-band",CapturedRig.Pelvis);
            k.Box(Suit,-.212f,.212f,.05f,.104f,-.142f,.142f,.008f);
            k.Light(Trim,1.4f,-.216f,.216f,.071f,.083f,-.146f,.146f);
            k.Box(SuitLight,-.045f,.045f,.045f,.115f,.138f,.152f,.006f);
            k.Light(Trim,1.4f,-.026f,.026f,.064f,.096f,.148f,.157f);

            // ---- arms (left authored, right mirrored) ----
            foreach(bool m in new[]{false,true})
            {
                string s=m?"right-":"left-";int clav=m?CapturedRig.RClav:CapturedRig.LClav,arm=m?CapturedRig.RArm:CapturedRig.LArm,elbow=m?CapturedRig.RElbow:CapturedRig.LElbow,hand=m?CapturedRig.RHand:CapturedRig.LHand;
                // Pauldron: a bevelled plate, a top layer, a glowing rim and an outer chevron.
                k.Part(s+"shoulder-cap",clav,m);
                // Stepped like the exported cap, so its top stays under the Sheldon Head's drape.
                k.Box(Plate,-.152f,.018f,-.058f,.02f,-.1f,.1f,.02f);
                k.Box(Plate,-.135f,.012f,0,.03f,-.082f,.082f,.012f);
                k.Box(Plate2,-.1f,-.006f,.024f,.05f,-.062f,.062f,.01f);
                k.Light(Trim,1.4f,-.157f,.022f,-.07f,-.056f,-.104f,.104f);
                if(k.Mid)k.Light(Trim,1.4f,-.158f,-.15f,-.035f,.012f,-.01f,.01f);
                k.Line(1,1,.05f,-.055f,.055f,-.064f,-.058f);
                // Upper arm: a dark sleeve under a bracer plate with an outer plate and a strip.
                k.Part(s+"upper-arm",arm,m);
                k.Box(Suit,-.07f,.07f,-.262f,-.04f,-.072f,.072f,.014f);
                k.Box(Plate,-.088f,.08f,-.232f,-.062f,-.088f,.088f,.016f);
                k.Box(Plate2,-.097f,-.084f,-.205f,-.08f,-.06f,.06f,.006f);
                if(k.Mid)k.Light(Trim,1.4f,-.099f,-.095f,-.19f,-.095f,-.008f,.008f);
                k.Line(2,1,.088f,-.07f,.06f,-.152f,-.147f);k.Line(2,-1,-.088f,-.07f,.06f,-.152f,-.147f);
                // Elbow: a hinge with discs and a cop behind.
                k.Part(s+"elbow-joint",elbow,m);
                k.Box(Suit,-.062f,.062f,-.045f,.045f,-.064f,.064f,.02f);
                k.Disc(Grey,-.064f,0,0,.036f,.006f);k.Disc(Grey,.064f,0,0,.036f,.006f);
                k.Box(Plate,-.05f,.05f,-.035f,.035f,-.074f,-.05f,.01f);
                // Forearm: a vambrace with a top band, an outer plate, a strip and a wrist ring.
                k.Part(s+"forearm",elbow,m);
                k.Box(Suit,-.06f,.06f,-.228f,-.04f,-.064f,.064f,.012f);
                k.Box(Plate,-.08f,.078f,-.212f,-.072f,-.082f,.082f,.016f);
                k.Box(Plate2,-.084f,.084f,-.07f,-.032f,-.088f,.088f,.01f);
                k.Box(Plate2,-.089f,-.078f,-.2f,-.115f,-.048f,.048f,.005f);
                if(k.Mid){k.Light(Trim,1.4f,-.091f,-.087f,-.19f,-.125f,-.007f,.007f);k.Box(SuitLight,-.066f,.066f,-.232f,-.214f,-.07f,.07f,.005f);}
                k.Line(2,1,.082f,-.06f,.06f,-.142f,-.137f);k.Line(2,-1,-.082f,-.06f,.06f,-.142f,-.137f);
                // Hand: the exported cuff boxes (their light keeps the held weapons' spill), a glove with a back plate.
                k.Part(s+"cuff",hand,m);
                k.Light(Trim,1.4f,-.075f,.075f,-.0125f,.0425f,-.095f,.095f,.006f);k.Light(Trim,1.4f,-.0875f,.0875f,-.0125f,.0425f,-.0825f,.0825f,.006f);
                if(k.Mid)k.Box(Suit,-.0885f,.0885f,.012f,.018f,-.0835f,.0835f);
                k.Part(s+"palm",hand,m);
                k.Box(Grey,-.07f,.07f,-.093f,.003f,-.068f,.078f,.014f);
                if(k.Mid)k.Box(GreyDark,-.05f,.05f,-.068f,-.008f,-.079f,-.064f,.006f);
                if(k.Fine)k.Box(GreyLight,-.062f,.062f,-.06f,-.04f,.07f,.084f,.004f);
                k.Part(s+"fingers",hand,m);
                for(int f=0;f<4;f++){float x0=-.062f+f*.0325f;k.Box(f%2==0?Grey:GreyLight,x0,x0+.027f,-.122f,-.048f,.02f,.121f,.006f);}
                k.Box(GreyDark,-.065f,.065f,-.113f,-.058f,.025f,.095f,.008f);
                k.Part(s+"thumb",hand,m);
                k.Box(Plate,.053f,.103f,-.078f,-.002f,-.005f,.075f,.01f);
            }

            // ---- legs (left authored, right mirrored) ----
            foreach(bool m in new[]{false,true})
            {
                string s=m?"right-":"left-";int hip=m?CapturedRig.RHip:CapturedRig.LHip,knee=m?CapturedRig.RKnee:CapturedRig.LKnee,foot=m?CapturedRig.RFoot:CapturedRig.LFoot,toe=m?CapturedRig.RToe:CapturedRig.LToe;
                k.Part(s+"hip-joint",hip,m);
                k.Box(Suit,-.072f,.072f,-.076f,.036f,-.078f,.078f,.02f);
                k.Disc(Grey,-.076f,-.02f,0,.04f,.006f);
                // Cuisse: a dark undersuit, the thigh shell, a front plate and an outer plate with a strip.
                k.Part(s+"thigh",hip,m);
                k.Box(Suit,-.076f,.076f,-.24f,-.02f,-.088f,.088f,.014f);
                k.Box(Plate,-.096f,.092f,-.225f,-.035f,-.104f,.106f,.018f);
                k.Box(Plate2,-.064f,.064f,-.19f,-.045f,.1f,.121f,.008f);
                k.Box(Plate2,-.106f,-.094f,-.165f,-.04f,-.07f,.07f,.005f);
                if(k.Mid)k.Light(Trim,1.4f,-.108f,-.104f,-.15f,-.055f,-.007f,.007f);
                k.Line(2,1,.121f,-.05f,.05f,-.122f,-.117f);k.Line(2,-1,-.104f,-.07f,.07f,-.13f,-.125f);
                // Knee: a hinge with discs, an articulated guard with a glowing core.
                k.Part(s+"knee-joint",knee,m);
                k.Box(Suit,-.078f,.078f,-.042f,.042f,-.082f,.082f,.022f);
                k.Disc(Grey,-.08f,0,0,.036f,.006f);k.Disc(Grey,.08f,0,0,.036f,.006f);
                k.Part(s+"kneecap",knee,m);
                k.Box(Plate,-.076f,.076f,-.066f,.05f,.064f,.12f,.016f);
                if(k.Mid){k.Box(Plate2,-.082f,-.06f,-.045f,.025f,.062f,.1f,.006f);k.Box(Plate2,.06f,.082f,-.045f,.025f,.062f,.1f,.006f);k.Box(Plate2,-.062f,.062f,.03f,.05f,.088f,.112f,.006f);}
                k.Light(Trim,1.4f,-.032f,.032f,-.05f,.016f,.112f,.133f);
                // Greave: a dark core, the shell, ridge plates either side of the shin light; the calf plate.
                k.Part(s+"shin",knee,m);
                k.Box(Suit,-.066f,.066f,-.246f,-.035f,-.078f,.078f,.012f);
                k.Box(Plate,-.084f,.084f,-.236f,-.048f,-.094f,.1f,.016f);
                if(k.Mid){k.Box(Plate2,-.058f,-.016f,-.232f,-.064f,.096f,.112f,.005f);k.Box(Plate2,.016f,.058f,-.232f,-.064f,.096f,.112f,.005f);}
                k.Line(0,-1,-.084f,-.152f,-.147f,-.08f,.08f);k.Line(0,1,.084f,-.152f,-.147f,-.08f,.08f);
                k.Part(s+"calf",knee,m);
                k.Box(Plate,-.07f,.07f,-.156f,-.034f,-.126f,-.074f,.014f);
                k.Line(2,-1,-.126f,-.05f,.05f,-.1f,-.096f);k.Line(2,-1,-.126f,-.05f,.05f,-.08f,-.076f);
                k.Part("armor-shin-light",knee,m);
                k.Light(Strip,2.4f,-.012f,.012f,-.222f,-.075f,.104f,.118f);
                // Boot: the upper with a sole, an ankle disc and a heel plate; the toe cap with its sole and a toe plate.
                k.Part(s+"heel",foot,m);
                k.Box(Suit,-.09f,.09f,-.052f,.018f,-.128f,.08f,.016f);
                k.Box(Sole,-.096f,.096f,-.07f,-.048f,-.134f,.084f,.006f);
                k.Disc(Grey,-.092f,-.01f,-.02f,.028f,.005f);
                if(k.Mid)k.Box(Grey,-.06f,.06f,-.05f,.01f,-.137f,-.12f,.006f);
                k.Part(s+"foot",toe,m);
                k.Box(Grey,-.094f,.094f,-.03f,.028f,-.06f,.15f,.016f);
                k.Box(GreyDark,-.102f,.102f,-.05f,-.028f,-.064f,.156f,.006f);
                if(k.Mid)k.Box(GreyLight,-.07f,.07f,.018f,.032f,0,.12f,.006f);
                k.Line(0,-1,-.102f,-.042f,-.036f,-.05f,.14f);
            }
        }

        // ================================================================================================================
        // Sheldon Clothes (DEV): a long dark coat in the hood's fabric over a slimmer frame. A wrap front closing on the right
        // with a glowing seam, a clasp, a stand collar, a sash; slim sleeves with flared cuffs; the skirt split into panels on
        // each thigh and shin (front, outer, back; the inner side open), so a leg only ever moves inside its own cloth; a hem
        // with a glowing edge at mid-shin; circuit trim down the back and on the panels; dark gloves and boots.
        // ================================================================================================================
        private static void SheldonClothes(Sculptor k)
        {
            // ---- head: a sleek mask helmet with a black face plate and a glowing visor slit (the hood covers it when worn) ----
            k.Part("blank-head",CapturedRig.Head);
            k.Box(Mask,-.158f,.158f,.012f,.322f,-.15f,.146f,.045f);
            k.Box(FacePlate,-.13f,.13f,.04f,.25f,.12f,.157f,.02f);
            k.Light(SheldonStrip,2.2f,-.095f,.095f,.175f,.193f,.15f,.16f);
            foreach(bool m in new[]{false,true}){k.Part("blank-head",CapturedRig.Head,m);if(k.Mid)k.Light(SheldonTrim,1.4f,-.162f,-.156f,.1f,.106f,-.06f,.06f);k.Line(0,-1,-.158f,.2f,.205f,-.12f,.12f);}
            k.Part("crown",CapturedRig.Head);
            k.Box(Mask,-.135f,.135f,.32f,.36f,-.125f,.125f,.02f);
            k.Part("jaw",CapturedRig.Head);
            k.Box(FacePlate,-.125f,.125f,-.03f,.008f,-.11f,.14f,.012f);
            k.Part("neck-joint",CapturedRig.Neck);
            k.Box(FabricDark,-.064f,.064f,-.045f,.095f,-.064f,.064f,.02f);

            // ---- the coat's body: a slimmer torso, an open front (the hood's V carries on down its piped edges) over a dark
            // inner layer with a glowing pull; the left breast stays under the Sheldon Head's chevron (bank -.12, .15, .1525) ----
            k.Part("ribcage",CapturedRig.Chest);
            k.Box(Fabric,-.236f,.236f,-.05f,.25f,-.138f,.13f,.035f);
            k.Box(Inner,-.05f,.05f,-.05f,.25f,.11f,.136f,.004f);
            foreach(bool m in new[]{false,true})
            {
                k.Part("ribcage",CapturedRig.Chest,m);
                k.Box(Lapel,-.226f,-.046f,-.05f,.248f,.112f,.146f,.008f);
                k.Box(SheldonStrip,-.05f,-.044f,-.05f,.248f,.13f,.149f);
                k.Line(2,1,.146f,-.22f,-.06f,.222f,.226f);
                k.Line(0,-1,-.236f,-.04f,.24f,-.004f,.004f);
            }
            k.Part("ribcage",CapturedRig.Chest);
            k.Line(2,-1,-.138f,-.004f,.004f,-.04f,.24f);
            k.Part("upper-chest",CapturedRig.Chest);
            k.Box(FabricDark,-.222f,.222f,.205f,.268f,-.132f,.132f,.025f);
            k.Part("collar",CapturedRig.Chest);
            k.Box(Lapel,-.122f,.122f,.265f,.325f,-.112f,.098f,.012f);
            if(k.Mid)k.Light(SheldonTrim,1.4f,-.124f,.124f,.321f,.326f,.07f,.1f);
            k.Part("sternum",CapturedRig.Chest);
            k.Box(FacePlate,-.02f,.02f,.16f,.214f,.134f,.142f,.004f);
            k.Light(SheldonTrim,1.4f,-.012f,.012f,.168f,.206f,.138f,.146f);
            if(k.Mid)k.Light(SheldonTrim,1.4f,-.003f,.003f,-.04f,.16f,.135f,.139f);
            k.Part("back-panel",CapturedRig.Chest);
            k.Box(FabricDark,-.13f,.13f,.02f,.2f,-.152f,-.13f,.012f);
            // Circuit trim down the spine: a line with branches and nodes.
            k.Part("armor-back-light",CapturedRig.Chest);
            k.Light(SheldonTrim,1.4f,-.004f,.004f,.03f,.2f,-.157f,-.151f);
            if(k.Mid)foreach(bool m in new[]{false,true})
            {
                k.Part("armor-back-light",CapturedRig.Chest,m);float y=m?.07f:.13f;
                k.Light(SheldonTrim,1.4f,-.05f,-.004f,y,y+.007f,-.157f,-.151f);k.Light(SheldonTrim,1.4f,-.062f,-.05f,y-.0045f,y+.0115f,-.158f,-.151f);
            }

            // ---- waist: the coat continues; a sash with a glowing line and a knot on the left ----
            k.Part("abdomen",CapturedRig.Spine);
            k.Box(FabricDark,-.165f,.165f,-.012f,.16f,-.118f,.118f,.02f);
            k.Part("abdomen-shell",CapturedRig.Spine);
            k.Box(Fabric,-.17f,.17f,.01f,.15f,-.13f,.12f,.025f);
            k.Box(Inner,-.046f,.046f,.01f,.15f,.1f,.128f,.004f);
            foreach(bool m in new[]{false,true}){k.Part("abdomen-shell",CapturedRig.Spine,m);k.Box(Lapel,-.168f,-.042f,.01f,.15f,.1f,.142f,.008f);k.Box(SheldonStrip,-.046f,-.04f,.012f,.148f,.12f,.145f);}
            k.Part("hip-shell",CapturedRig.Pelvis);
            k.Box(Fabric,-.2f,.2f,-.11f,.1f,-.136f,.13f,.028f);
            k.Box(Inner,-.042f,.042f,-.11f,.1f,.11f,.136f,.004f);
            foreach(bool m in new[]{false,true}){k.Part("hip-shell",CapturedRig.Pelvis,m);k.Box(Lapel,-.196f,-.038f,-.11f,.05f,.11f,.148f,.008f);k.Box(SheldonStrip,-.042f,-.036f,-.11f,.05f,.13f,.151f);}
            k.Part("waist-band",CapturedRig.Pelvis);
            k.Box(FabricDark,-.206f,.206f,.05f,.106f,-.14f,.14f,.01f);
            k.Light(SheldonTrim,1.4f,-.21f,.21f,.074f,.082f,-.144f,.144f);
            k.Box(Lapel,-.12f,-.06f,.04f,.112f,.136f,.152f,.006f);
            k.Light(SheldonTrim,1.4f,-.1f,-.08f,.066f,.09f,.148f,.156f);

            // ---- arms: rounded shoulders with a glowing seam, slim sleeves, flared cuffs with a glowing ring, dark gloves ----
            foreach(bool m in new[]{false,true})
            {
                string s=m?"right-":"left-";int clav=m?CapturedRig.RClav:CapturedRig.LClav,arm=m?CapturedRig.RArm:CapturedRig.LArm,elbow=m?CapturedRig.RElbow:CapturedRig.LElbow,hand=m?CapturedRig.RHand:CapturedRig.LHand;
                k.Part(s+"shoulder-cap",clav,m);
                k.Box(Fabric,-.135f,.01f,-.06f,.035f,-.09f,.09f,.03f);
                k.Light(SheldonTrim,1.4f,-.125f,-.015f,.03f,.0355f,-.006f,.006f);
                k.Part(s+"upper-arm",arm,m);
                k.Box(Fabric,-.08f,.072f,-.262f,-.04f,-.078f,.078f,.025f);
                k.Line(0,-1,-.08f,-.25f,-.05f,-.004f,.004f);
                k.Part(s+"elbow-joint",elbow,m);
                k.Box(FabricDark,-.06f,.06f,-.045f,.045f,-.064f,.064f,.025f);
                k.Part(s+"forearm",elbow,m);
                k.Box(Fabric,-.068f,.068f,-.2f,-.035f,-.074f,.074f,.022f);
                k.Box(Lapel,-.082f,.082f,-.235f,-.185f,-.088f,.088f,.01f);
                if(k.Mid)k.Light(SheldonTrim,1.4f,-.084f,.084f,-.19f,-.183f,-.09f,.09f);
                if(k.Fine)k.Light(SheldonTrim,1.4f,-.072f,-.068f,-.17f,-.06f,-.004f,.004f);
                k.Part(s+"cuff",hand,m);
                k.Box(Glove,-.078f,.078f,-.013f,.04f,-.086f,.086f,.008f);
                k.Light(SheldonTrim,1.4f,-.08f,.08f,.018f,.026f,-.088f,.088f);
                k.Part(s+"palm",hand,m);
                k.Box(Glove,-.068f,.068f,-.091f,.003f,-.066f,.076f,.016f);
                k.Part(s+"fingers",hand,m);
                for(int f=0;f<4;f++){float x0=-.06f+f*.0325f;k.Box(Glove,x0,x0+.025f,-.12f,-.05f,.022f,.119f,.007f);}
                k.Box(Frame,-.063f,.063f,-.111f,-.06f,.027f,.093f,.008f);
                k.Part(s+"thumb",hand,m);
                k.Box(Glove,.055f,.1f,-.076f,-.004f,-.003f,.073f,.012f);
            }

            // ---- legs: slim trousers under the split skirt; panels ride their own thigh and shin ----
            foreach(bool m in new[]{false,true})
            {
                string s=m?"right-":"left-";int hip=m?CapturedRig.RHip:CapturedRig.LHip,knee=m?CapturedRig.RKnee:CapturedRig.LKnee,foot=m?CapturedRig.RFoot:CapturedRig.LFoot,toe=m?CapturedRig.RToe:CapturedRig.LToe;
                k.Part(s+"hip-joint",hip,m);
                k.Box(FabricDark,-.07f,.07f,-.076f,.036f,-.078f,.078f,.022f);
                k.Part(s+"thigh",hip,m);
                k.Box(Frame,-.072f,.072f,-.24f,-.02f,-.082f,.082f,.02f);
                k.Panel(Fabric,-.1f,.09f,-.245f,-.025f,.084f,.118f,.01f);
                k.Panel(Fabric,-.118f,-.094f,-.245f,-.025f,-.112f,.112f,.008f);
                k.Panel(Fabric,-.1f,.09f,-.245f,-.025f,-.124f,-.09f,.01f);
                // The coat's front edges run on down each thigh's inner front edge to the hem.
                k.Box(SheldonStrip,.084f,.09f,-.245f,-.025f,.104f,.121f);
                if(k.Fine){k.Light(SheldonTrim,1.4f,-.12f,-.116f,-.2f,-.12f,-.004f,.004f);k.Light(SheldonTrim,1.4f,-.121f,-.116f,-.124f,-.116f,-.004f,.03f);}
                k.Part(s+"knee-joint",knee,m);
                k.Box(FabricDark,-.07f,.07f,-.04f,.04f,-.076f,.076f,.025f);
                k.Part(s+"kneecap",knee,m);
                if(k.Mid)k.Box(FacePlate,-.022f,.022f,-.064f,-.026f,.12f,.127f,.004f);
                k.Light(SheldonTrim,1.4f,-.016f,.016f,-.06f,-.03f,.122f,.13f);
                k.Part(s+"shin",knee,m);
                k.Box(Frame,-.06f,.06f,-.246f,-.035f,-.07f,.07f,.015f);
                k.Panel(Fabric,-.104f,.094f,-.105f,.03f,.088f,.126f,.01f);
                k.Panel(Fabric,-.11f,-.09f,-.105f,.03f,-.12f,.118f,.008f);
                // The hem flares a little over its last 4.5 cm.
                k.Panel(Fabric,-.106f,.096f,-.15f,-.105f,.09f,.132f,.008f,true);k.Panel(Fabric,-.115f,-.09f,-.15f,-.105f,-.124f,.124f,.008f,true);
                k.Box(SheldonStrip,.088f,.094f,-.15f,.03f,.108f,.129f);
                if(k.Mid){k.Light(SheldonTrim,1.4f,-.106f,.096f,-.15f,-.143f,.13f,.134f);k.Light(SheldonTrim,1.4f,-.117f,-.113f,-.15f,-.143f,-.124f,.124f);}
                k.Part(s+"calf",knee,m);
                k.Panel(Fabric,-.104f,.094f,-.105f,.03f,-.134f,-.098f,.01f);k.Panel(Fabric,-.106f,.096f,-.15f,-.105f,-.14f,-.098f,.008f,true);
                if(k.Mid)k.Light(SheldonTrim,1.4f,-.106f,.096f,-.15f,-.143f,-.142f,-.138f);
                k.Part("armor-shin-light",knee,m);
                k.Light(SheldonTrim,1.4f,-.006f,.006f,-.235f,-.16f,.068f,.076f);
                k.Part(s+"heel",foot,m);
                k.Box(Frame,-.084f,.084f,-.052f,.018f,-.124f,.078f,.02f);
                k.Box(Sole,-.09f,.09f,-.07f,-.048f,-.13f,.082f,.006f);
                k.Part(s+"foot",toe,m);
                k.Box(Frame,-.086f,.086f,-.03f,.026f,-.058f,.146f,.022f);
                k.Box(Sole,-.094f,.094f,-.05f,-.028f,-.062f,.152f,.008f);
            }
        }
    }
}

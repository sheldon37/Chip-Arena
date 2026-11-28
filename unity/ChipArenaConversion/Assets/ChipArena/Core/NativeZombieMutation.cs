using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Hulk's mutation on the player's model (owner 2026-10-29: "Hulk zombies need to look a LOT different, they are essentially
    // mutated. Theyre supposed to be huge, muscled zombies"; then the giant-brute archetype, a head sunk between boulder shoulders,
    // a barrel chest, huge long arms on small legs; then 2026-10-30, of the first pass's smooth masses: "I still see "clothes"
    // inside there muscle like theyre meshed together, their muscles look just like bubbles currently; Please work with much more
    // detail"). Over its build (Core NativeZombieLooks.Build: the neck shortened so the head sits low, the arms longer, the hands
    // huge, the right arm grown a little past the left) a mutant variant's whole upper body is grown flesh
    // (Runtime/NativeZombieModelPresentation.cs ZombieGrowthMake), and the model's own skin and clothes under it are hidden
    // (Distance; the presentation drops every worn triangle the flesh covers):
    // - anatomy, not lumps: each muscle its own form from its origin to its insertion on the model's rest joints (root space, +z
    //   its front, +y up, its left at -x), a spindle or a fan slab thickest at its belly (Muscle). Traps swollen up round the sunk
    //   head, three-headed deltoids, pecs in three fans, lats flaring to the arms, the scapula's muscles, the erectors, serratus
    //   fingers, an eight-pack between obliques; biceps, brachialis, both triceps heads, the forearm's brachioradialis, extensors
    //   and flexors over a mutant swelling below the elbow;
    // - each muscle a signed distance, joined by a tight smooth minimum (Crease), so they meet in creases instead of melting into
    //   one another (the first pass summed soft fields: bubbles); a core under them (the rib cage, the belly, the waist that goes
    //   into the trousers, the neck, the limbs' bones) fills between them with a softer join;
    // - polygonised on a fine grid (Cell, surface nets), shaded by its vertex colours: the skin's tone, darker and redder down each
    //   crease and in hollows (the creases' and the cavities' occlusion), lighter over the bellies, bruised mottling, stretch marks
    //   across the swollen masses, torn skin over raw striated muscle with a pale rim (Tears), the variant's top as a torn chest
    //   wrap on the women (top);
    // - raised veins (tubes laid on the surface: the forearms, the biceps, the shoulders, the chest, the neck), bone spurs breaking
    //   out of the right shoulder, the back and the right forearm, and pustules (they glow a little);
    // - skinned smoothly: each vertex on up to four of the bones its muscles act on, by its distance to their segments, so the
    //   shoulders, elbows and forearms bend and twist with the model's own bones (the forearms on their twist bones).
    // Presentation only.
    public static class NativeZombieMutation
    {
        /// <summary>A point on the model: Joint's rest position plus Off (a body frame offset: across toward its side, up, ahead),
        /// or, for a limb point, Share of the way from Joint to To plus Off in the limb's frame (out from the body, -, ahead).</summary>
        public readonly struct Mark
        {
            public readonly string Joint,To;public readonly float Share;public readonly Vector3 Off;public readonly bool Limb;
            public Mark(string joint,string to,float share,Vector3 off,bool limb){Joint=joint;To=to;Share=share;Off=off;Limb=limb;}
        }
        /// <summary>A muscle: from A to B, Width across and Thick out (half sizes at its belly, m), thickest at Peak (a share of its
        /// length), Ends its radius share at either end, Lift how far its belly stands out off the A-B line (a share of Thick),
        /// Out the direction it is thick in (a body frame direction; a limb's: out from the body, -, ahead), Bones the bones it
        /// moves with, Side whether it has a left twin (defined on the right; the left is mirrored). Pull: the striations' share.</summary>
        public sealed class Muscle
        {
            public string Name;public Mark A,B;public float Width,Thick,Peak=.5f,Ends=.3f,Lift=.2f;public Vector3 Out=Vector3.UnitZ;public string[] Bones;public bool Side=true;public bool Arm;
        }
        private static Mark J(string joint,float x,float y,float z)=>new Mark(joint,joint,0,new Vector3(x,y,z),false);
        private static Mark L(string from,string to,float share,float outward,float ahead)=>new Mark(from,to,share,new Vector3(outward,0,ahead),true);
        private static Muscle M(string name,Mark a,Mark b,float width,float thick,float peak,float ends,float lift,Vector3 o,bool arm,params string[] bones)
            =>new Muscle{Name=name,A=a,B=b,Width=width,Thick=thick,Peak=peak,Ends=ends,Lift=lift,Out=o,Arm=arm,Bones=bones};
        private static readonly string[] Trunk={"spine_03","spine_04","spine_05"},Shoulder={"spine_05","clavicle_r","upperarm_r"},UpperArm={"upperarm_r","lowerarm_r"},
            Forearm={"lowerarm_r","lowerarm_twist_02_r","lowerarm_twist_01_r","upperarm_r"};
        /// <summary>The muscles, on the right (the left twins mirrored; a name ends in -r or -l once placed).</summary>
        public static readonly Muscle[] Muscles=
        {
            // The neck and the trapezius: the upper trap swollen into a mound from the skull to the shoulder's top round the sunk
            // head, the middle and lower traps over the shoulder blades; the sternocleidomastoid down the neck's side from under the
            // jaw (the face stays clear of the flesh).
            M("trap-upper",J("neck_02",.035f,.03f,-.065f),J("upperarm_r",-.03f,.07f,-.03f),.075f,.08f,.42f,.4f,.35f,new Vector3(.15f,1,-.45f),false,"neck_01","spine_05","clavicle_r"),
            M("trap-mid",J("spine_05",.0f,.02f,-.14f),J("upperarm_r",-.07f,.05f,-.09f),.07f,.045f,.4f,.4f,.2f,new Vector3(0,.25f,-1),false,"spine_05","spine_04","clavicle_r"),
            M("trap-low",J("spine_04",.0f,-.05f,-.13f),J("spine_05",.11f,.02f,-.14f),.05f,.032f,.45f,.3f,.1f,new Vector3(0,0,-1),false,"spine_04","spine_05"),
            M("sterno",J("neck_02",.052f,.0f,-.015f),J("spine_05",.028f,.065f,.11f),.021f,.02f,.5f,.4f,.2f,new Vector3(.7f,0,.7f),false,"neck_01","spine_05"),
            // The chest: the pectoral in three fans from the sternum and the collarbone into the arm.
            M("pec-clav",J("spine_05",.045f,.045f,.15f),J("upperarm_r",.035f,-.07f,.075f),.05f,.048f,.44f,.18f,.25f,new Vector3(.1f,.15f,1),false,"spine_05","clavicle_r","upperarm_r"),
            M("pec-mid",J("spine_05",.032f,-.045f,.158f),J("upperarm_r",.03f,-.085f,.065f),.062f,.06f,.44f,.18f,.25f,new Vector3(0,0,1),false,"spine_05","spine_04","clavicle_r","upperarm_r"),
            M("pec-low",J("spine_04",.034f,.055f,.098f),J("upperarm_r",.02f,-.1f,.05f),.052f,.052f,.46f,.12f,.2f,new Vector3(0,-.25f,1),false,"spine_04","spine_05","upperarm_r"),
            // The shoulder: the deltoid's three heads from the collarbone, the acromion and the shoulder blade's spine to the arm.
            M("delt-front",J("clavicle_r",.15f,.0f,.065f),L("upperarm_r","lowerarm_r",.45f,.025f,.025f),.055f,.065f,.38f,.25f,.3f,new Vector3(.5f,.1f,1),false,"clavicle_r","upperarm_r"),
            M("delt-side",J("upperarm_r",.035f,.065f,-.01f),L("upperarm_r","lowerarm_r",.48f,.035f,0),.062f,.075f,.36f,.25f,.35f,new Vector3(1,.35f,0),false,"clavicle_r","upperarm_r"),
            M("delt-back",J("upperarm_r",-.055f,.05f,-.085f),L("upperarm_r","lowerarm_r",.45f,.025f,-.025f),.055f,.058f,.38f,.25f,.3f,new Vector3(.5f,.1f,-1),false,"clavicle_r","upperarm_r","spine_05"),
            // The back: the lats flaring from the lower back into the armpit, teres and infraspinatus on the shoulder blade, the
            // erectors up the spine.
            M("lat",J("spine_02",.055f,.0f,-.11f),J("upperarm_r",-.035f,-.1f,-.035f),.095f,.048f,.58f,.3f,.15f,new Vector3(.65f,0,-.8f),false,"spine_02","spine_03","spine_04","spine_05","upperarm_r"),
            M("teres",J("spine_04",.11f,.08f,-.14f),J("upperarm_r",-.02f,-.07f,-.05f),.036f,.036f,.5f,.4f,.15f,new Vector3(.3f,0,-1),false,"spine_04","spine_05","upperarm_r"),
            M("infra",J("spine_05",.065f,-.035f,-.16f),J("upperarm_r",-.045f,.0f,-.075f),.048f,.034f,.45f,.4f,.15f,new Vector3(0,.2f,-1),false,"spine_05","clavicle_r","upperarm_r"),
            M("erector",J("pelvis",.042f,.07f,-.09f),J("spine_05",.042f,-.02f,-.12f),.03f,.036f,.38f,.45f,.1f,new Vector3(0,0,-1),false,"spine_02","spine_03","spine_04","spine_05"),
            // The sides: four serratus fingers under the lat, the external oblique down to the hip.
            M("serratus-1",J("spine_04",.13f,.115f,.1f),J("spine_04",.175f,.17f,.0f),.02f,.018f,.5f,.4f,.1f,new Vector3(1,0,.5f),false,Trunk),
            M("serratus-2",J("spine_04",.14f,.065f,.105f),J("spine_04",.185f,.135f,.0f),.02f,.018f,.5f,.4f,.1f,new Vector3(1,0,.5f),false,Trunk),
            M("serratus-3",J("spine_04",.145f,.015f,.1f),J("spine_04",.19f,.095f,.0f),.02f,.018f,.5f,.4f,.1f,new Vector3(1,0,.5f),false,Trunk),
            M("serratus-4",J("spine_04",.145f,-.035f,.09f),J("spine_04",.185f,.05f,.0f),.019f,.017f,.5f,.4f,.1f,new Vector3(1,0,.5f),false,Trunk),
            M("oblique",J("spine_04",.135f,.0f,.075f),J("pelvis",.135f,.12f,.035f),.048f,.036f,.55f,.45f,.15f,new Vector3(1,0,.6f),false,"spine_02","spine_03","spine_04"),
            // The belly: the eight-pack, four blocks a side either side of the linea alba.
            M("abs-1",J("spine_04",.041f,.052f,.1f),J("spine_04",.041f,.012f,.116f),.038f,.019f,.5f,.55f,.2f,new Vector3(0,.2f,1),false,Trunk),
            M("abs-2",J("spine_04",.041f,.004f,.108f),J("spine_04",.041f,-.044f,.11f),.038f,.021f,.5f,.55f,.2f,new Vector3(0,0,1),false,Trunk),
            M("abs-3",J("spine_03",.04f,.022f,.104f),J("spine_03",.04f,-.026f,.102f),.037f,.021f,.5f,.55f,.2f,new Vector3(0,0,1),false,"spine_02","spine_03"),
            M("abs-4",J("spine_03",.039f,-.044f,.096f),J("spine_03",.037f,-.094f,.086f),.034f,.019f,.5f,.55f,.2f,new Vector3(0,-.2f,1),false,"spine_02","spine_03","pelvis"),
            // The arm (Arm: grown by the variant's right): biceps, brachialis, the triceps' long and lateral heads.
            M("biceps",L("upperarm_r","lowerarm_r",.12f,0,.055f),L("upperarm_r","lowerarm_r",.97f,0,.045f),.058f,.068f,.58f,.28f,.28f,new Vector3(0,0,1),true,UpperArm),
            M("brachialis",L("upperarm_r","lowerarm_r",.5f,.055f,.015f),L("upperarm_r","lowerarm_r",1.04f,.03f,.03f),.04f,.036f,.6f,.35f,.15f,new Vector3(1,0,.4f),true,UpperArm),
            M("triceps-long",L("upperarm_r","lowerarm_r",.0f,-.02f,-.065f),L("upperarm_r","lowerarm_r",1.06f,0,-.05f),.052f,.064f,.42f,.3f,.28f,new Vector3(0,0,-1),true,"upperarm_r","lowerarm_r","clavicle_r"),
            M("triceps-lat",L("upperarm_r","lowerarm_r",.1f,.055f,-.03f),L("upperarm_r","lowerarm_r",.92f,.02f,-.05f),.042f,.054f,.45f,.3f,.25f,new Vector3(1,0,-.6f),true,UpperArm),
            // The forearm: a mutant swelling below the elbow under the brachioradialis, the extensors and the flexors.
            M("forearm-mass",L("lowerarm_r","hand_r",.06f,0,0),L("lowerarm_r","hand_r",.62f,0,0),.068f,.07f,.32f,.45f,.0f,new Vector3(0,0,1),true,Forearm),
            M("brachiorad",L("upperarm_r","lowerarm_r",.74f,.065f,.005f),L("lowerarm_r","hand_r",.88f,.025f,.03f),.046f,.052f,.32f,.25f,.25f,new Vector3(.7f,0,.5f),true,"upperarm_r","lowerarm_r","lowerarm_twist_02_r","lowerarm_twist_01_r"),
            M("extensors",L("lowerarm_r","hand_r",.0f,.045f,-.03f),L("lowerarm_r","hand_r",.96f,.02f,-.02f),.046f,.048f,.3f,.3f,.25f,new Vector3(.6f,0,-.8f),true,Forearm),
            M("flexors",L("lowerarm_r","hand_r",.0f,-.045f,.02f),L("lowerarm_r","hand_r",.96f,-.012f,.025f),.052f,.052f,.28f,.3f,.25f,new Vector3(-.6f,0,.8f),true,Forearm),
        };
        /// <summary>The core the muscles lie on (each a capsule from A to B of radius Radius, or an ellipsoid at A of radii Radii),
        /// joined more softly (CoreSoft): the rib cage, the belly, the waist into the trousers, the neck, the shoulder girdle and the
        /// limbs' bones (Arm: grown by the right).</summary>
        public sealed class Core{public string Name;public Mark A,B;public float Radius;public Vector3 Radii;public bool Side,Arm;public string[] Bones;}
        private static Core E(string name,Mark at,Vector3 radii,params string[] bones)=>new Core{Name=name,A=at,B=at,Radii=radii,Bones=bones};
        private static Core C(string name,Mark a,Mark b,float radius,bool side,bool arm,params string[] bones)=>new Core{Name=name,A=a,B=b,Radius=radius,Side=side,Arm=arm,Bones=bones};
        public static readonly Core[] Cores=
        {
            E("ribs",J("spine_05",0,-.08f,.015f),new Vector3(.18f,.16f,.14f),"spine_04","spine_05"),
            E("belly",J("spine_03",0,-.0f,.005f),new Vector3(.155f,.13f,.1f),"spine_02","spine_03","spine_04"),
            E("waist",J("pelvis",0,.1f,.015f),new Vector3(.145f,.075f,.108f),"pelvis","spine_01","spine_02"),
            C("neck",J("neck_01",0,-.02f,-.01f),J("neck_02",0,-.005f,-.028f),.066f,false,false,"neck_01","spine_05"),
            C("girdle",J("clavicle_r",.07f,-.01f,-.02f),J("upperarm_r",.0f,.0f,.0f),.085f,true,false,"spine_05","clavicle_r","upperarm_r"),
            C("humerus",J("upperarm_r",0,0,0),J("lowerarm_r",0,0,0),.072f,true,true,"upperarm_r","lowerarm_r"),
            C("elbow",J("lowerarm_r",0,0,-.01f),J("lowerarm_r",0,0,-.01f),.083f,true,true,"upperarm_r","lowerarm_r"),
            C("radius",J("lowerarm_r",0,0,0),L("lowerarm_r","hand_r",1.03f,0,0),.064f,true,true,"lowerarm_r","lowerarm_twist_02_r","lowerarm_twist_01_r","hand_r"),
        };
        /// <summary>A vein: on Muscle from (T0, A0) to (T1, A1) (a share of its length; round it from its Out toward its side,
        /// radians), Radius thick (m).</summary>
        public readonly struct Vein{public readonly string Muscle;public readonly float T0,A0,T1,A1,Radius;public Vein(string muscle,float t0,float a0,float t1,float a1,float radius){Muscle=muscle;T0=t0;A0=a0;T1=t1;A1=a1;Radius=radius;}}
        public static readonly Vein[] Veins=
        {
            new Vein("flexors-r",.05f,.35f,.95f,-.25f,.0062f),new Vein("flexors-r",.45f,.0f,.9f,.6f,.0048f),new Vein("extensors-r",.08f,.2f,.92f,.55f,.0055f),new Vein("brachiorad-r",.25f,-.1f,.95f,.35f,.0058f),
            new Vein("flexors-l",.05f,-.3f,.95f,.2f,.0058f),new Vein("extensors-l",.1f,.3f,.9f,-.2f,.005f),new Vein("brachiorad-l",.3f,.1f,.95f,-.3f,.0052f),
            new Vein("biceps-r",.08f,.55f,.96f,.15f,.0068f),new Vein("biceps-r",.4f,-.2f,.85f,-.6f,.0045f),new Vein("biceps-l",.1f,-.5f,.96f,-.1f,.006f),
            new Vein("delt-front-r",.15f,.3f,.85f,.1f,.0045f),new Vein("delt-front-l",.2f,-.2f,.85f,.0f,.004f),
            new Vein("pec-mid-r",.35f,-.35f,.92f,.05f,.004f),new Vein("pec-clav-l",.4f,.2f,.9f,-.1f,.0038f),
            new Vein("sterno-r",.35f,.6f,.9f,.4f,.0045f),new Vein("sterno-l",.4f,-.6f,.88f,-.3f,.0042f),new Vein("trap-upper-r",.25f,.7f,.75f,.35f,.0042f),
        };
        /// <summary>Torn skin over raw muscle: on Muscle at T (a share of its length), round it by Angle, Radius across (m).</summary>
        public static readonly (string Muscle,float T,float Angle,float Radius)[] Tears=
        {
            ("trap-upper-r",.62f,-.25f,.04f),("delt-side-r",.58f,.4f,.034f),("pec-mid-l",.42f,.15f,.03f),("forearm-mass-l",.45f,.9f,.034f),("lat-r",.55f,.1f,.04f),
            ("oblique-l",.5f,.1f,.026f),
        };
        /// <summary>A bone spur out of a muscle at T, round it by Angle, tilted toward Tilt (a body frame direction), Length long.</summary>
        public static readonly (string Muscle,float T,float Angle,Vector3 Tilt,float Length,float Radius)[] Spurs=
        {
            ("delt-side-r",.32f,.2f,new Vector3(.6f,.7f,-.2f),.11f,.019f),("delt-side-r",.5f,1.1f,new Vector3(.5f,.4f,-.7f),.085f,.016f),("trap-upper-r",.55f,.25f,new Vector3(.3f,.8f,-.4f),.095f,.018f),
            ("infra-r",.45f,.0f,new Vector3(.2f,.4f,-1),.08f,.016f),("infra-l",.5f,.1f,new Vector3(-.3f,.4f,-1),.065f,.014f),("erector-r",.75f,.0f,new Vector3(0,.3f,-1),.06f,.013f),
            ("erector-l",.55f,.0f,new Vector3(0,.2f,-1),.05f,.012f),("extensors-r",.55f,.3f,new Vector3(.6f,0,-.6f),.075f,.015f),("trap-upper-l",.6f,.3f,new Vector3(-.3f,.8f,-.3f),.07f,.015f),
        };
        /// <summary>Pustules: on Muscle at T, round it by Angle, Radius big (m).</summary>
        public static readonly (string Muscle,float T,float Angle,float Radius)[] Pustules=
        {
            ("trap-upper-r",.4f,.9f,.016f),("trap-mid-l",.5f,.2f,.013f),("delt-back-r",.5f,.3f,.014f),("pec-low-r",.5f,.3f,.012f),("lat-l",.45f,-.3f,.013f),
            ("forearm-mass-r",.4f,.6f,.012f),("triceps-lat-r",.5f,.4f,.011f),("oblique-r",.4f,.3f,.011f),
        };
        /// <summary>The grid's step (m, at the model's rest), the muscles' crease (the smooth minimum's width) and the core's.</summary>
        public const float Cell=.0105f,Crease=.0055f,CoreSoft=.03f;
        public const int VeinSides=6,SpurSides=7;
        /// <summary>The flesh's tone against the variant's skin colour: the model's skin texture, toned to that colour, still draws
        /// warmer than it (measured in Unity on the head beside the flesh), so the flesh is warmed to meet it.</summary>
        public static readonly Vector4 SkinMatch=new Vector4(1.22f,1.03f,.97f,1);
        public const int RawColour=0x8a2a2e,RawDeep=0x4e1418,VeinColour=0x5a4a74,BoneColour=0xe2d8bd,BoneTipColour=0xa59a80,PusColour=0xd7dd56,CreaseColour=0x5c2a36,RimColour=0xd8c6a0;
        /// <summary>The joints the growth needs (beside the twist bones, which fall back on their limb when missing).</summary>
        public static readonly string[] Needs={"pelvis","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02","head","clavicle_l","clavicle_r","upperarm_l","upperarm_r","lowerarm_l","lowerarm_r","hand_l","hand_r"};

        public sealed class Growth
        {
            public readonly List<Vector3> Positions=new List<Vector3>(),Normals=new List<Vector3>();
            /// <summary>0..1 colours as authored (the runtime takes them to linear, as the sac's); alpha the glow's share (a pustule's).</summary>
            public readonly List<Vector4> Colors=new List<Vector4>();
            /// <summary>Each vertex's four bones (indices into Bones) and weights (summing to 1, largest first).</summary>
            public readonly List<int> Bone=new List<int>(),Bone2=new List<int>(),Bone3=new List<int>(),Bone4=new List<int>();public readonly List<float> Weight=new List<float>(),Weight2=new List<float>(),Weight3=new List<float>(),Weight4=new List<float>();
            public readonly List<int> Indices=new List<int>();
            public readonly List<string> Bones=new List<string>();
            /// <summary>The flesh's signed distance at its rest grid (m, negative inside), for hiding what it covers (Distance).</summary>
            internal float[] Field;internal Vector3 Low;internal int Nx,Ny,Nz;internal (Vector3,Vector3)[] Segments;internal float[] Scratch;
            /// <summary>How far a rest point lies outside the grown flesh (m; negative inside; far outside past its grid).</summary>
            public float Distance(Vector3 p)
            {
                if(Field==null)return 1;var g=(p-Low)/Cell;if(g.X<0||g.Y<0||g.Z<0||g.X>=Nx-1||g.Y>=Ny-1||g.Z>=Nz-1)return 1;
                int a=(int)g.X,b=(int)g.Y,c=(int)g.Z;float fx=g.X-a,fy=g.Y-b,fz=g.Z-c;
                float V(int i,int j,int k)=>Field[((c+k)*Ny+b+j)*Nx+a+i];
                float x00=V(0,0,0)+(V(1,0,0)-V(0,0,0))*fx,x10=V(0,1,0)+(V(1,1,0)-V(0,1,0))*fx,x01=V(0,0,1)+(V(1,0,1)-V(0,0,1))*fx,x11=V(0,1,1)+(V(1,1,1)-V(0,1,1))*fx;
                float y0=x00+(x10-x00)*fy,y1=x01+(x11-x01)*fy;return y0+(y1-y0)*fz;
            }
            /// <summary>Whether the flesh covers a worn rest point: within `reach` of it (m; a skin piece's reaches past the flesh's
            /// surface: the flesh replaces it), never past a wrist (the hands are the model's) nor, reaching out, up on the head.</summary>
            public bool Covers(Vector3 p,float reach)
            {
                if(Distance(p)>=reach||reach>0&&p.Y>Crown)return false;
                for(int side=0;side<2;side++){var w=p-Wrist[side];if(w.LengthSquared()<.09f&&Vector3.Dot(w,Along[side])>-.01f)return false;}
                return true;
            }
            internal readonly Vector3[] Wrist=new Vector3[2],Along=new Vector3[2];internal float Crown=float.MaxValue;
            /// <summary>The flesh's outward normal at a rest point (the field's gradient).</summary>
            public Vector3 Outward(Vector3 p){const float h=Cell;var n=new Vector3(Distance(p+new Vector3(h,0,0))-Distance(p-new Vector3(h,0,0)),Distance(p+new Vector3(0,h,0))-Distance(p-new Vector3(0,h,0)),Distance(p+new Vector3(0,0,h))-Distance(p-new Vector3(0,0,h)));return n.LengthSquared()>1e-12f?Vector3.Normalize(n):Vector3.UnitY;}
        }

        // ================================================================================================================
        // Placing
        // ================================================================================================================
        /// <summary>A muscle or core part placed: its ends, axis and length, its thick and across directions, sizes, bones (indices
        /// into the growth's) and box.</summary>
        public sealed class Placed
        {
            public string Name;public Vector3 A,B,U,Out,Across;public float Length,Width,Thick,Peak,Ends,Lift,Radius;public Vector3 Radii;public bool Ellipsoid,Core,Arm;public int[] Bones;public Vector3 Lo,Hi;public int Index;
        }
        private static string Mirror(string joint,bool left)=>left&&joint.EndsWith("_r",StringComparison.Ordinal)?joint.Substring(0,joint.Length-2)+"_l":joint;
        /// <summary>A mark's point at rest on `left`'s side (null when a joint is missing). A body frame offset's across is toward the
        /// side; a limb's frame: along the limb, out from the body square to it, ahead (the model's front) square to both.</summary>
        public static Vector3? Point(Mark m,bool left,Func<string,Vector3?> rest)
        {
            var a=rest(Mirror(m.Joint,left));if(a==null)return null;
            if(!m.Limb)return a.Value+new Vector3(left?-m.Off.X:m.Off.X,m.Off.Y,m.Off.Z);
            var b=rest(Mirror(m.To,left));if(b==null)return null;
            LimbFrame(a.Value,b.Value,left,out var along,out var outward,out var ahead);
            return Vector3.Lerp(a.Value,b.Value,m.Share)+outward*m.Off.X+ahead*m.Off.Z;
        }
        private static void LimbFrame(Vector3 a,Vector3 b,bool left,out Vector3 along,out Vector3 outward,out Vector3 ahead)
        {
            along=b-a;along=along.LengthSquared()>1e-10f?Vector3.Normalize(along):-Vector3.UnitY;
            ahead=Vector3.UnitZ-along*along.Z;ahead=ahead.LengthSquared()>1e-8f?Vector3.Normalize(ahead):Vector3.UnitZ;
            outward=Vector3.Cross(ahead,along);if(outward.X*(left?-1:1)<0)outward=-outward;
        }
        private static Vector3 Dir(Vector3 v,bool left,bool limb,Vector3 a,Vector3 b)
        {
            if(!limb){var d=new Vector3(left?-v.X:v.X,v.Y,v.Z);return d.LengthSquared()>1e-10f?Vector3.Normalize(d):Vector3.UnitZ;}
            LimbFrame(a,b,left,out var along,out var outward,out var ahead);var e=outward*v.X+along*v.Y+ahead*v.Z;return e.LengthSquared()>1e-10f?Vector3.Normalize(e):ahead;
        }
        /// <summary>Every muscle and core part placed on the rest joints (null when a joint is missing). Sizes scale with the
        /// shoulders' span against the male build's (.368 m); the arm's on the right by `right`.</summary>
        public static List<Placed> Place(Func<string,Vector3?> rest,float right,Growth g)
        {
            foreach(var n in Needs)if(rest(n)==null)return null;
            float span=(rest("upperarm_r").Value-rest("upperarm_l").Value).Length(),k=Math.Max(.75f,Math.Min(1.3f,span/.368f));
            var all=new List<Placed>();
            int BoneOf(string name,bool left)
            {
                string n=Mirror(name,left);if(rest(n)==null)n=n.Contains("twist")?(n.StartsWith("lowerarm",StringComparison.Ordinal)?Mirror("lowerarm_r",left):Mirror("upperarm_r",left)):n;
                int i=g.Bones.IndexOf(n);if(i<0){i=g.Bones.Count;g.Bones.Add(n);}return i;
            }
            int[] Bones(string[] names,bool left){var o=new List<int>();foreach(var n in names){int b=BoneOf(n,left);if(!o.Contains(b))o.Add(b);}return o.ToArray();}
            foreach(var m in Muscles)
                for(int s=0;s<(m.Side?2:1);s++)
                {
                    bool left=s==1;var a=Point(m.A,left,rest);var b=Point(m.B,left,rest);if(a==null||b==null)return null;
                    float grow=m.Arm&&!left?right:1;var p=new Placed{Name=m.Name+(m.Side?left?"-l":"-r":""),A=a.Value,B=b.Value,Width=m.Width*k*grow,Thick=m.Thick*k*grow,Peak=m.Peak,Ends=m.Ends,Lift=m.Lift,Arm=m.Arm,Bones=Bones(m.Bones,left),Index=all.Count};
                    var ab=p.B-p.A;p.Length=ab.Length();if(p.Length<1e-4f)return null;p.U=ab/p.Length;
                    // (A limb muscle's thick direction in its limb's frame: the limb from its first mark's joint to that mark's To.)
                    Vector3 la=rest(Mirror(m.A.Limb?m.A.Joint:m.B.Joint,left)).Value,lb=rest(Mirror(m.A.Limb?m.A.To:m.B.To,left)).Value;
                    var o=Dir(m.Out,left,m.A.Limb||m.B.Limb,la,lb);o-=p.U*Vector3.Dot(o,p.U);if(o.LengthSquared()<1e-8f)o=Vector3.Cross(p.U,Vector3.UnitX);
                    p.Out=Vector3.Normalize(o);p.Across=Vector3.Cross(p.U,p.Out);
                    float reach=Math.Max(p.Width,p.Thick)*(1+p.Lift)+Crease*9+Cell;p.Lo=Vector3.Min(p.A,p.B)-new Vector3(reach);p.Hi=Vector3.Max(p.A,p.B)+new Vector3(reach);
                    all.Add(p);
                }
            foreach(var c in Cores)
                for(int s=0;s<(c.Side?2:1);s++)
                {
                    bool left=s==1;var a=Point(c.A,left,rest);var b=Point(c.B,left,rest);if(a==null||b==null)return null;
                    float grow=c.Arm&&!left?right:1;var p=new Placed{Name=c.Name+(c.Side?left?"-l":"-r":""),A=a.Value,B=b.Value,Core=true,Arm=c.Arm,Bones=Bones(c.Bones,left),Index=all.Count};
                    p.Ellipsoid=c.Radius<=0;p.Radii=c.Radii*k;p.Radius=c.Radius*k*grow;var ab=p.B-p.A;p.Length=ab.Length();p.U=p.Length>1e-5f?ab/p.Length:Vector3.UnitY;
                    float reach=(p.Ellipsoid?Math.Max(p.Radii.X,Math.Max(p.Radii.Y,p.Radii.Z)):p.Radius)+CoreSoft*3+Cell;p.Lo=Vector3.Min(p.A,p.B)-new Vector3(reach);p.Hi=Vector3.Max(p.A,p.B)+new Vector3(reach);
                    all.Add(p);
                }
            return all;
        }
        private static float Profile(Placed m,float t)
        {
            float g=t<m.Peak?.5f*t/Math.Max(1e-3f,m.Peak):.5f+.5f*(t-m.Peak)/Math.Max(1e-3f,1-m.Peak);
            return m.Ends+(1-m.Ends)*MathF.Pow(Math.Max(0,MathF.Sin(MathF.PI*g)),.7f);
        }
        /// <summary>A placed part's signed distance at `p` (m, negative inside; approximate off a muscle's surface), and where along
        /// it (t) and round it (from Out toward Across, radians).</summary>
        public static float Distance(Placed m,Vector3 p,out float t,out float angle)
        {
            t=0;angle=0;
            if(m.Core)
            {
                if(m.Ellipsoid){var d=(p-m.A)/m.Radii;float l=d.Length();var q=p-m.A;return l<1e-6f?-Math.Min(m.Radii.X,Math.Min(m.Radii.Y,m.Radii.Z)):q.Length()*(1-1/l);}
                float s=m.Length>1e-5f?Math.Max(0,Math.Min(1,Vector3.Dot(p-m.A,m.U)/m.Length)):0;return (p-(m.A+m.U*s*m.Length)).Length()-m.Radius;
            }
            float along=Vector3.Dot(p-m.A,m.U);t=along/m.Length;float tc=Math.Max(0,Math.Min(1,t)),f=Profile(m,tc),w=m.Width*f,h=m.Thick*f;
            var c=m.A+m.U*(tc*m.Length)+m.Out*(h*m.Lift);var r=p-c;r-=m.U*Vector3.Dot(r,m.U);
            float x=Vector3.Dot(r,m.Across),y=Vector3.Dot(r,m.Out),e=(float)Math.Sqrt(x*x/(w*w)+y*y/(h*h)),q2=(float)Math.Sqrt(x*x+y*y);angle=(float)Math.Atan2(x,y);
            float radial=e<1e-6f?-Math.Min(w,h):q2*(1-1/e);float ax=t<0?-t*m.Length:t>1?(t-1)*m.Length:0;
            if(ax<=0)return radial;return radial>0?(float)Math.Sqrt(radial*radial+ax*ax):Math.Max(radial,ax-Math.Min(w,h)*.3f);
        }

        // A part's signed distance alone (the grid's and the skinning's: as Distance, without where along and round it).
        private static float Near(Placed m,Vector3 p)
        {
            if(m.Core)
            {
                if(m.Ellipsoid){var q=p-m.A;var d=q/m.Radii;float l=d.Length();return l<1e-6f?-Math.Min(m.Radii.X,Math.Min(m.Radii.Y,m.Radii.Z)):q.Length()*(1-1/l);}
                float s=m.Length>1e-5f?Math.Max(0,Math.Min(1,Vector3.Dot(p-m.A,m.U)/m.Length)):0;return (p-(m.A+m.U*s*m.Length)).Length()-m.Radius;
            }
            float t=Vector3.Dot(p-m.A,m.U)/m.Length,tc=Math.Max(0,Math.Min(1,t)),f=Profile(m,tc),w=m.Width*f,h=m.Thick*f;
            var r=p-(m.A+m.U*(tc*m.Length)+m.Out*(h*m.Lift));r-=m.U*Vector3.Dot(r,m.U);
            float x=Vector3.Dot(r,m.Across),y=Vector3.Dot(r,m.Out),e=MathF.Sqrt(x*x/(w*w)+y*y/(h*h)),q2=MathF.Sqrt(x*x+y*y);
            float radial=e<1e-6f?-Math.Min(w,h):q2*(1-1/e),ax=t<0?-t*m.Length:t>1?(t-1)*m.Length:0;
            if(ax<=0)return radial;return radial>0?MathF.Sqrt(radial*radial+ax*ax):Math.Max(radial,ax-Math.Min(w,h)*.3f);
        }

        // ================================================================================================================
        // Building
        // ================================================================================================================
        /// <summary>The mutation at rest for a model whose rest joints `rest` gives (root space), its flesh from `skin` (0xRRGGBB),
        /// varied by `seed`, its right arm grown by `right`, a woman's chest wrap in `top` (0xRRGGBB; negative: none); null when the
        /// model lacks the joints.</summary>
        public static Growth Build(Func<string,Vector3?> rest,int skin,uint seed,float right=1,int top=-1)
        {
            var g=new Growth();var parts=Place(rest,right,g);if(parts==null)return null;
            for(int side=0;side<2;side++){string s=side==0?"_l":"_r";var w=rest("hand"+s).Value;g.Wrist[side]=w;g.Along[side]=Vector3.Normalize(w-rest("lowerarm"+s).Value);}
            g.Crown=rest("neck_02").Value.Y+.035f;
            // The grid over every part's box.
            Vector3 lo=new Vector3(float.MaxValue),hi=new Vector3(float.MinValue);foreach(var p in parts){lo=Vector3.Min(lo,p.Lo);hi=Vector3.Max(hi,p.Hi);}
            int nx=(int)Math.Ceiling((hi.X-lo.X)/Cell)+1,ny=(int)Math.Ceiling((hi.Y-lo.Y)/Cell)+1,nz=(int)Math.Ceiling((hi.Z-lo.Z)/Cell)+1;
            int cells=nx*ny*nz;var muscles=new double[cells];var core=new double[cells];
            // Each part adds its share of the smooth minimum in its own box: exp(-d/k), summed (a log-sum-exp minimum); the grid's
            // slices in parallel (each slice its own cells).
            System.Threading.Tasks.Parallel.For(0,nz,c=>
            {
                foreach(var p in parts)
                {
                    int c0=(int)((p.Lo.Z-lo.Z)/Cell),c1=(int)((p.Hi.Z-lo.Z)/Cell)+1;if(c<c0||c>c1)continue;
                    float k=p.Core?CoreSoft:Crease;var acc=p.Core?core:muscles;
                    int a0=Math.Max(0,(int)((p.Lo.X-lo.X)/Cell)),a1=Math.Min(nx-1,(int)((p.Hi.X-lo.X)/Cell)+1),b0=Math.Max(0,(int)((p.Lo.Y-lo.Y)/Cell)),b1=Math.Min(ny-1,(int)((p.Hi.Y-lo.Y)/Cell)+1);
                    for(int b=b0;b<=b1;b++)for(int a=a0;a<=a1;a++)
                    {
                        float d=Near(p,lo+new Vector3(a,b,c)*Cell);if(d>k*9)continue;acc[(c*ny+b)*nx+a]+=MathF.Exp(-Math.Max(d,-.4f)/k);
                    }
                }
            });
            var field=new float[cells];
            for(int i=0;i<cells;i++)
            {
                double dc=core[i]>0?-CoreSoft*Math.Log(core[i]):1,sum=muscles[i]+(dc<.2?Math.Exp(-Math.Max(dc,-.5)/Crease):0);
                field[i]=sum>0?(float)(-Crease*Math.Log(sum)):1;
            }
            g.Field=field;g.Low=lo;g.Nx=nx;g.Ny=ny;g.Nz=nz;
            Surface(g,field,lo,nx,ny,nz);
            int flesh=g.Positions.Count;
            var noise=new Noise(seed);
            // Each vertex's bones (Weights; in parallel), then its colour (Paint).
            g.Segments=new (Vector3,Vector3)[g.Bones.Count];for(int b=0;b<g.Bones.Count;b++){Segment(g.Bones[b],rest,out var sa,out var sb);g.Segments[b]=(sa,sb);}
            var skinned=new (int b0,int b1,int b2,int b3,float w0,float w1,float w2,float w3)[flesh];
            System.Threading.Tasks.Parallel.For(0,flesh,()=>new float[g.Segments.Length],(i,_,scratch)=>{skinned[i]=Weights(g,g.Positions[i],scratch);return scratch;},_=>{});
            foreach(var k in skinned){g.Bone.Add(k.b0);g.Bone2.Add(k.b1);g.Bone3.Add(k.b2);g.Bone4.Add(k.b3);g.Weight.Add(k.w0);g.Weight2.Add(k.w1);g.Weight3.Add(k.w2);g.Weight4.Add(k.w3);}
            Paint(g,parts,flesh,skin,top,noise,rest);
            // (A woman's wrap covers her chest: no vein or pustule over it.)
            bool Bare(string muscle)=>top<0||!muscle.StartsWith("pec",StringComparison.Ordinal);
            foreach(var v in Veins){var m=parts.Find(q=>q.Name==v.Muscle);if(m!=null&&Bare(v.Muscle))VeinTube(g,parts,m,v,rest);}
            foreach(var (mus,t,ang,tilt,len,rad) in Spurs){var m=parts.Find(q=>q.Name==mus);if(m!=null)Spur(g,parts,m,t,ang,tilt,len,rad,rest);}
            foreach(var (mus,t,ang,rad) in Pustules){var m=parts.Find(q=>q.Name==mus);if(m!=null&&Bare(mus))Pustule(g,parts,m,t,ang,rad,rest);}
            return g;
        }
        // Surface nets over the field (negative inside): a vertex in each cell the surface crosses (the mean of its edges'
        // crossings), its normal down the field, and a quad round each grid edge the surface crosses, facing out.
        private static void Surface(Growth g,float[] f,Vector3 lo,int nx,int ny,int nz)
        {
            int I(int a,int b,int c)=>(c*ny+b)*nx+a;var cell=new int[(nx-1)*(ny-1)*(nz-1)];int C(int a,int b,int c)=>(c*(ny-1)+b)*(nx-1)+a;
            for(int c=0;c<nz-1;c++)for(int b=0;b<ny-1;b++)for(int a=0;a<nx-1;a++)
            {
                cell[C(a,b,c)]=-1;int inside=0;for(int q=0;q<8;q++)if(f[I(a+(q&1),b+(q>>1&1),c+(q>>2))]<0)inside++;
                if(inside==0||inside==8)continue;Vector3 sum=Vector3.Zero;int n=0;
                for(int e=0;e<12;e++)
                {
                    int axis=e/4,u=e&1,v=e>>1&1;Vector3 o0=axis==0?new Vector3(0,u,v):axis==1?new Vector3(u,0,v):new Vector3(u,v,0);Vector3 o1=o0+(axis==0?Vector3.UnitX:axis==1?Vector3.UnitY:Vector3.UnitZ);
                    float f0=f[I(a+(int)o0.X,b+(int)o0.Y,c+(int)o0.Z)],f1=f[I(a+(int)o1.X,b+(int)o1.Y,c+(int)o1.Z)];if(f0<0==f1<0)continue;sum+=Vector3.Lerp(o0,o1,f0/(f0-f1));n++;
                }
                var at=lo+(new Vector3(a,b,c)+sum/n)*Cell;cell[C(a,b,c)]=g.Positions.Count;g.Positions.Add(at);g.Normals.Add(g.Outward(at));
            }
            for(int c=1;c<nz-1;c++)for(int b=1;b<ny-1;b++)for(int a=1;a<nx-1;a++)
            {
                float f0=f[I(a,b,c)];
                for(int axis=0;axis<3;axis++)
                {
                    int a1=a+(axis==0?1:0),b1=b+(axis==1?1:0),c1=c+(axis==2?1:0);if(a1>=nx||b1>=ny||c1>=nz)continue;if(f0<0==f[I(a1,b1,c1)]<0)continue;
                    int q0,q1,q2,q3;
                    if(axis==0){q0=cell[C(a,b-1,c-1)];q1=cell[C(a,b,c-1)];q2=cell[C(a,b,c)];q3=cell[C(a,b-1,c)];}
                    else if(axis==1){q0=cell[C(a-1,b,c-1)];q1=cell[C(a,b,c-1)];q2=cell[C(a,b,c)];q3=cell[C(a-1,b,c)];}
                    else{q0=cell[C(a-1,b-1,c)];q1=cell[C(a,b-1,c)];q2=cell[C(a,b,c)];q3=cell[C(a-1,b,c)];}
                    if(q0<0||q1<0||q2<0||q3<0)continue;Face(g,q0,q1,q2);Face(g,q0,q2,q3);
                }
            }
        }
        private static void Face(Growth g,int a,int b,int c)
        {
            var f=Vector3.Cross(g.Positions[b]-g.Positions[a],g.Positions[c]-g.Positions[a]);if(f.LengthSquared()<1e-16f)return;
            if(Vector3.Dot(f,g.Normals[a]+g.Normals[b]+g.Normals[c])>=0){g.Indices.Add(a);g.Indices.Add(b);g.Indices.Add(c);}else{g.Indices.Add(a);g.Indices.Add(c);g.Indices.Add(b);}
        }

        // ================================================================================================================
        // Skinning
        // ================================================================================================================
        // A bone's segment at rest: from its joint toward the next joint down its chain (the hand a little past the wrist).
        private static bool Segment(string bone,Func<string,Vector3?> rest,out Vector3 a,out Vector3 b)
        {
            var p=rest(bone);a=b=p??Vector3.Zero;if(p==null)return false;
            string side=bone.EndsWith("_l",StringComparison.Ordinal)?"_l":"_r";
            string next=bone=="pelvis"?"spine_01":bone=="spine_01"?"spine_02":bone=="spine_02"?"spine_03":bone=="spine_03"?"spine_04":bone=="spine_04"?"spine_05":bone=="spine_05"?"neck_01":bone=="neck_01"?"head":
                bone.StartsWith("clavicle",StringComparison.Ordinal)?"upperarm"+side:bone.StartsWith("upperarm",StringComparison.Ordinal)?"lowerarm"+side:bone=="lowerarm"+side?(rest("lowerarm_twist_02"+side)!=null?"lowerarm_twist_02"+side:"hand"+side):
                bone.StartsWith("lowerarm_twist_02",StringComparison.Ordinal)?(rest("lowerarm_twist_01"+side)!=null?"lowerarm_twist_01"+side:"hand"+side):bone.StartsWith("lowerarm_twist_01",StringComparison.Ordinal)?"hand"+side:null;
            if(next!=null&&rest(next)!=null){b=rest(next).Value;return true;}
            if(bone.StartsWith("hand",StringComparison.Ordinal)){var e=rest("lowerarm"+side);if(e!=null)b=p.Value+(p.Value-e.Value)*.3f;return true;}
            b=p.Value+new Vector3(0,.08f,0);return true;
        }
        // A vertex's bones: every bone the muscles move with, each by the vertex's distance to its segment (the nearer, the more:
        // 1/(d^2+s^2)^2), the four most (Weights). By distance alone the weights change smoothly over the flesh, so no edge tears
        // where one muscle meets another of other bones (weighting each vertex by its own muscle's bones tore the creases: a
        // shoulder blade's vertex rode the forearm).
        private static void Skin(Growth g,List<Placed> parts,int i,Func<string,Vector3?> rest)
        {
            var k=Weights(g,g.Positions[i],g.Scratch??=new float[g.Segments.Length]);
            g.Bone.Add(k.b0);g.Bone2.Add(k.b1);g.Bone3.Add(k.b2);g.Bone4.Add(k.b3);g.Weight.Add(k.w0);g.Weight2.Add(k.w1);g.Weight3.Add(k.w2);g.Weight4.Add(k.w3);
        }
        // The four largest, less the fifth (so a bone trading places with the fifth eases out rather than dropping), summing to 1.
        private static (int b0,int b1,int b2,int b3,float w0,float w1,float w2,float w3) Weights(Growth g,Vector3 at,float[] weights)
        {
            for(int bone=0;bone<g.Segments.Length;bone++){var (a,b)=g.Segments[bone];float s=SegmentDistance(at,a,b),q=s*s+.0012f;weights[bone]=1/(q*q);}
            int b0=-1,b1=-1,b2=-1,b3=-1,b4=-1;float w0=0,w1=0,w2=0,w3=0,w4=0;
            for(int bone=0;bone<weights.Length;bone++)
            {
                float v=weights[bone];if(v<=w4)continue;
                if(v>w0){b4=b3;w4=w3;b3=b2;w3=w2;b2=b1;w2=w1;b1=b0;w1=w0;b0=bone;w0=v;}
                else if(v>w1){b4=b3;w4=w3;b3=b2;w3=w2;b2=b1;w2=w1;b1=bone;w1=v;}
                else if(v>w2){b4=b3;w4=w3;b3=b2;w3=w2;b2=bone;w2=v;}
                else if(v>w3){b4=b3;w4=w3;b3=bone;w3=v;}
                else{b4=bone;w4=v;}
            }
            if(b0<0)return (0,0,0,0,1,0,0,0);
            w0-=w4;w1=b1<0?0:w1-w4;w2=b2<0?0:w2-w4;w3=b3<0?0:w3-w4;float sum=w0+w1+w2+w3;if(sum<=0){w0=1;sum=1;}
            return (b0,b1<0?b0:b1,b2<0?b0:b2,b3<0?b0:b3,w0/sum,w1/sum,w2/sum,w3/sum);
        }
        private static bool In(Placed p,Vector3 at)=>at.X>=p.Lo.X&&at.Y>=p.Lo.Y&&at.Z>=p.Lo.Z&&at.X<=p.Hi.X&&at.Y<=p.Hi.Y&&at.Z<=p.Hi.Z;
        private static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b){var ab=b-a;float l2=ab.LengthSquared();float t=l2>1e-10f?Math.Max(0,Math.Min(1,Vector3.Dot(p-a,ab)/l2)):0;return (p-(a+ab*t)).Length();}
        // A part's added vertex (a vein's, a spur's, a pustule's) skinned as the flesh beneath it.
        private static void SkinLike(Growth g,List<Placed> parts,Vector3 under,Func<string,Vector3?> rest)
        {
            int n=g.Positions.Count;g.Positions.Add(under);Skin(g,parts,n,rest);g.Positions.RemoveAt(n);
        }

        // ================================================================================================================
        // Colour
        // ================================================================================================================
        private static void Paint(Growth g,List<Placed> parts,int flesh,int skin,int top,Noise noise,Func<string,Vector3?> rest)
        {
            Vector4 own=Clamp(Rgb(skin)*SkinMatch),raw=Rgb(RawColour),deep=Rgb(RawDeep),crease=Rgb(CreaseColour),rim=Rgb(RimColour),bruise=Rgb(0x6a3a5c),cloth=top>=0?Rgb(top):Vector4.Zero;
            var tears=new List<(Placed m,Vector3 at,Vector3 n,float r)>();
            foreach(var (mus,t,ang,rad) in Tears){var m=parts.Find(q=>q.Name==mus);if(m!=null&&(top<0||!mus.StartsWith("pec",StringComparison.Ordinal))&&OnSurface(g,m,t,ang,out var at,out var n))tears.Add((m,at,n,rad*(m.Arm&&!m.Name.EndsWith("-l",StringComparison.Ordinal)?1.05f:1)));}
            // (Where the flesh meets the head: under the jaw and behind the skull.)
            var neck=rest("neck_02").Value;float meet=neck.Y-.045f;
            float chestLo=0,chestHi=0;if(top>=0){float s4=rest("spine_04").Value.Y,s5=rest("spine_05").Value.Y;chestLo=s4+(s5-s4)*.1f;chestHi=s4+(s5-s4)*.92f;}
            var colours=new Vector4[flesh];
            System.Threading.Tasks.Parallel.For(0,flesh,i=>
            {
                var at=g.Positions[i];var n=g.Normals[i];
                // The two nearest muscles: how deep in a crease (the second as near as the first), which one leads.
                float d1=float.MaxValue,d2=float.MaxValue;Placed lead=null;float leadT=0,leadA=0;
                foreach(var p in parts){if(p.Core||!In(p,at))continue;float d=Distance(p,at,out float t,out float a);if(d<d1){d2=d1;d1=d;lead=p;leadT=t;leadA=a;}else if(d<d2)d2=d;}
                float inCrease=lead==null?0:1-Smooth((d2-d1)/(Crease*2.2f));
                // A hollow: the flesh a little way out along the normal is nearer than that (the creases' and the armpits' shade).
                float probe=.018f,hollow=Smooth((probe-g.Distance(at+n*probe))/probe*1.6f);
                float belly=lead==null?0:Smooth((d2-d1)/.03f)*Smooth(1-Math.Abs(leadT-lead.Peak)*2);
                float mottle=noise.At(at*9)*.6f+noise.At(at*23)*.4f;
                // The band where the flesh meets the head is the head's own skin, unmarked, so the two read as one body.
                float horizontal=new Vector2(at.X-neck.X,at.Z-neck.Z).Length(),nearHead=Smooth((at.Y-meet)/.05f)*Smooth((.16f-horizontal)/.06f);
                inCrease*=1-nearHead*.8f;hollow*=1-nearHead*.7f;belly*=1-nearHead;mottle=mottle+(.5f-mottle)*nearHead;
                var col=own*(.9f+.16f*belly+.08f*(mottle-.5f)+.08f*nearHead);col=Vector4.Lerp(col,bruise*.9f+own*.1f,Smooth((mottle-.68f)*4)*.35f);
                // Stretch marks across the swollen masses (the shoulders, the chest's edge, the lats, the biceps): thin pale lines
                // square to the fibres.
                if(lead!=null&&(lead.Name.StartsWith("delt",StringComparison.Ordinal)||lead.Name.StartsWith("lat",StringComparison.Ordinal)||lead.Name.StartsWith("biceps",StringComparison.Ordinal)||lead.Name.StartsWith("pec-mid",StringComparison.Ordinal)))
                {
                    float along=Vector3.Dot(at-lead.A,lead.U),wave=(float)Math.Abs(Math.Sin(along*120+noise.At(at*14)*5));
                    float patch=Smooth((noise.At(at*6+new Vector3(7))-.5f)*5);col=Vector4.Lerp(col,own*1.12f+new Vector4(.05f,.02f,.06f,0),Smooth((wave-.86f)*12)*patch*.6f);
                }
                col=Vector4.Lerp(col,crease*.55f+own*.45f,Math.Max(inCrease*.75f,hollow*.6f));
                // Torn skin: raw muscle striated along its fibres, a pale torn rim.
                foreach(var (m,c,cn,r) in tears)
                {
                    var off=at-c;var flat=off-cn*Vector3.Dot(off,cn);float dist=flat.Length()*(1+.7f*(noise.At(at*16)-.5f)+.35f*(noise.At(at*41)-.5f));if(dist>r*1.3f)continue;
                    float inside=1-Smooth((dist-r*.85f)/(r*.1f)),edge=Smooth((dist-r*.8f)/(r*.08f))*(1-Smooth((dist-r*.98f)/(r*.1f)));
                    float stripe=(float)(.5+.5*Math.Sin(Vector3.Dot(off,Vector3.Cross(m.U,cn))*260));var flesh2=Vector4.Lerp(raw,deep,.25f+.55f*stripe*stripe);
                    col=Vector4.Lerp(col,flesh2,inside);col=Vector4.Lerp(col,rim,edge*(1-inside)*.85f);
                }
                // A woman's chest wrap: the variant's top bound round the chest, its lower edge torn.
                if(top>=0&&lead!=null&&!lead.Arm&&!lead.Name.StartsWith("delt",StringComparison.Ordinal)&&!lead.Name.StartsWith("trap",StringComparison.Ordinal)&&!lead.Name.StartsWith("sterno",StringComparison.Ordinal))
                {
                    float torn=(float)(Math.Sin(at.X*60+mottle*4)*.012+noise.At(at*30)*.012),fold=(float)(.5+.5*Math.Sin(at.Y*170+at.X*20));
                    if(at.Y>chestLo+torn&&at.Y<chestHi-torn*.5f){col=cloth*(.82f+.22f*fold)*(1-.35f*Math.Max(inCrease,hollow));col.W=0;}
                }
                col=Clamp(col);col.W=0;colours[i]=col;
            });
            g.Colors.AddRange(colours);
        }
        // Where a muscle's surface lies at T round Angle: from its axis out until the flesh's edge.
        private static bool OnSurface(Growth g,Placed m,float t,float angle,out Vector3 at,out Vector3 n)
        {
            float f=Profile(m,Math.Max(0,Math.Min(1,t))),h=m.Thick*f;var c=m.A+m.U*(t*m.Length)+m.Out*(h*m.Lift);
            var dir=Vector3.Normalize(m.Out*(float)Math.Cos(angle)+m.Across*(float)Math.Sin(angle));
            float lo=0,hi=.35f;if(g.Distance(c)>0){at=c;n=g.Outward(c);return false;}
            for(int k=0;k<22;k++){float mid=(lo+hi)*.5f;if(g.Distance(c+dir*mid)<0)lo=mid;else hi=mid;}
            at=c+dir*((lo+hi)*.5f);n=g.Outward(at);return true;
        }

        // ================================================================================================================
        // Veins, spurs, pustules
        // ================================================================================================================
        // A vein: points along its path laid on the flesh, smoothed, a tube half sunk in it, tapering at its ends.
        private static void VeinTube(Growth g,List<Placed> parts,Placed m,Vein v,Func<string,Vector3?> rest)
        {
            const int Steps=16;var path=new List<Vector3>();var ns=new List<Vector3>();
            for(int s=0;s<=Steps;s++)
            {
                float u=s/(float)Steps,t=v.T0+(v.T1-v.T0)*u,a=v.A0+(v.A1-v.A0)*u+(float)Math.Sin(u*9+v.A0*5)*.12f;
                if(!OnSurface(g,m,t,a,out var at,out var n))continue;path.Add(at);ns.Add(n);
            }
            if(path.Count<4)return;
            for(int s=1;s<path.Count-1;s++)path[s]=(path[s-1]+path[s]*2+path[s+1])*.25f;
            var col=Rgb(VeinColour);col.W=0;int first=g.Positions.Count;var bottom=new List<int>();
            for(int s=0;s<path.Count;s++)
            {
                float u=s/(float)(path.Count-1),r=v.Radius*(float)Math.Sqrt(Math.Max(.05f,Math.Sin(Math.PI*Math.Min(1,u*1.1f))));
                var along=s<path.Count-1?path[s+1]-path[s]:path[s]-path[s-1];along=Vector3.Normalize(along);var n=Vector3.Normalize(ns[s]-along*Vector3.Dot(ns[s],along));var side=Vector3.Cross(along,n);
                var centre=path[s]+n*(r*.25f);
                for(int j=0;j<VeinSides;j++)
                {
                    float th=(float)(Math.PI*2*j/VeinSides);var dir=n*(float)Math.Cos(th)+side*(float)Math.Sin(th);g.Positions.Add(centre+dir*r);g.Normals.Add(dir);
                    var c=col*(.85f+.25f*Math.Max(0,(float)Math.Cos(th)));c.W=0;g.Colors.Add(c);SkinLike(g,parts,path[s],rest);
                }
            }
            for(int s=0;s<path.Count-1;s++)for(int j=0;j<VeinSides;j++)
            {
                int a0=first+s*VeinSides+j,a1=first+s*VeinSides+(j+1)%VeinSides,b0=a0+VeinSides,b1=a1+VeinSides;Tri(g,a0,b0,a1);Tri(g,a1,b0,b1);
            }
        }
        private static void Tri(Growth g,int a,int b,int c)
        {
            var f=Vector3.Cross(g.Positions[b]-g.Positions[a],g.Positions[c]-g.Positions[a]);if(f.LengthSquared()<1e-18f)return;
            if(Vector3.Dot(f,g.Normals[a]+g.Normals[b]+g.Normals[c])>=0){g.Indices.Add(a);g.Indices.Add(b);g.Indices.Add(c);}else{g.Indices.Add(a);g.Indices.Add(c);g.Indices.Add(b);}
        }
        // A spur: a seven-sided cone out of the flesh, blood at its root, ivory up to a darker tip, rigid on the flesh's bones there.
        private static void Spur(Growth g,List<Placed> parts,Placed m,float t,float angle,Vector3 tilt,float length,float radius,Func<string,Vector3?> rest)
        {
            if(!OnSurface(g,m,t,angle,out var at,out var n))return;bool left=m.Name.EndsWith("-l",StringComparison.Ordinal);
            var lean=new Vector3(left?-tilt.X:tilt.X,tilt.Y,tilt.Z);var dir=Vector3.Normalize(n*.6f+Vector3.Normalize(lean)*.4f);
            var u=Vector3.Normalize(Vector3.Cross(dir,Math.Abs(dir.Y)<.9f?Vector3.UnitY:Vector3.UnitX));var v=Vector3.Cross(dir,u);
            Vector4 ivory=Rgb(BoneColour),tip=Rgb(BoneTipColour),root=Rgb(RawColour);int first=g.Positions.Count;var basePoint=at-dir*radius*.8f;
            for(int ring=0;ring<4;ring++)
            {
                float s=ring==0?0:ring==1?.3f:ring==2?.6f:.85f,r=radius*(ring==0?1.15f:ring==1?.8f:ring==2?.5f:.22f);
                for(int j=0;j<SpurSides;j++)
                {
                    float th=(float)(Math.PI*2*j/SpurSides+ring*.3f);var d=u*(float)Math.Cos(th)+v*(float)Math.Sin(th);g.Positions.Add(basePoint+dir*(length*s)+d*r*(1+.15f*(float)Math.Sin(th*3+ring)));g.Normals.Add(Vector3.Normalize(d+dir*.3f));
                    var c=ring==0?root:Vector4.Lerp(ivory,tip,s);c.W=0;g.Colors.Add(c);SkinLike(g,parts,at,rest);
                }
            }
            int apex=g.Positions.Count;g.Positions.Add(basePoint+dir*length);g.Normals.Add(dir);var tc=tip;tc.W=0;g.Colors.Add(tc);SkinLike(g,parts,at,rest);
            for(int ring=0;ring<3;ring++)for(int j=0;j<SpurSides;j++){int a0=first+ring*SpurSides+j,a1=first+ring*SpurSides+(j+1)%SpurSides,b0=a0+SpurSides,b1=a1+SpurSides;Tri(g,a0,b0,a1);Tri(g,a1,b0,b1);}
            for(int j=0;j<SpurSides;j++)Tri(g,first+3*SpurSides+j,apex,first+3*SpurSides+(j+1)%SpurSides);
        }
        // A pustule: a small sphere half out of the flesh, glowing (alpha 1).
        private static void Pustule(Growth g,List<Placed> parts,Placed m,float t,float angle,float radius,Func<string,Vector3?> rest)
        {
            if(!OnSurface(g,m,t,angle,out var at,out var n))return;var centre=at+n*radius*.3f;var colour=Rgb(PusColour);colour.W=1;int first=g.Positions.Count;const int R=6,S=10;
            for(int r=0;r<=R;r++){double phi=Math.PI*r/R;for(int j=0;j<S;j++){double th=Math.PI*2*j/S;var d=new Vector3((float)(Math.Sin(phi)*Math.Sin(th)),(float)Math.Cos(phi),(float)(Math.Sin(phi)*Math.Cos(th)));g.Positions.Add(centre+d*radius);g.Normals.Add(d);g.Colors.Add(colour);SkinLike(g,parts,at,rest);}}
            for(int r=0;r<R;r++)for(int j=0;j<S;j++){int a0=first+r*S+j,a1=first+r*S+(j+1)%S,b0=a0+S,b1=a1+S;if(r>0)Tri(g,a0,b0,a1);if(r<R-1)Tri(g,a1,b0,b1);}
        }

        // ================================================================================================================
        // Helpers
        // ================================================================================================================
        private sealed class Noise
        {
            private readonly uint seed;public Noise(uint seed){this.seed=seed*747796405u+2891336453u;}
            private float H(int x,int y,int z){uint h=seed^(uint)x*73856093u^(uint)y*19349663u^(uint)z*83492791u;h^=h>>13;h*=1274126177u;h^=h>>16;return (h&0xffffff)/16777215f;}
            /// <summary>Smooth value noise, 0..1.</summary>
            public float At(Vector3 p)
            {
                int x=(int)Math.Floor(p.X),y=(int)Math.Floor(p.Y),z=(int)Math.Floor(p.Z);float fx=S(p.X-x),fy=S(p.Y-y),fz=S(p.Z-z);
                float L(float a,float b,float t)=>a+(b-a)*t;
                return L(L(L(H(x,y,z),H(x+1,y,z),fx),L(H(x,y+1,z),H(x+1,y+1,z),fx),fy),L(L(H(x,y,z+1),H(x+1,y,z+1),fx),L(H(x,y+1,z+1),H(x+1,y+1,z+1),fx),fy),fz);
            }
            private static float S(float t)=>t*t*(3-2*t);
        }
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static Vector4 Clamp(Vector4 c)=>new Vector4(Math.Max(0,Math.Min(1,c.X)),Math.Max(0,Math.Min(1,c.Y)),Math.Max(0,Math.Min(1,c.Z)),c.W);
        private static Vector4 Rgb(int c)=>new Vector4((c>>16&255)/255f,(c>>8&255)/255f,(c&255)/255f,0);
    }
}

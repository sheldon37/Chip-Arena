using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The City Ironworks' five enemies sculpted on the pilot-model zombie rig (docs/EXPEDITION-CITY-IRONWORKS.md 6; the kit,
    // tiers and palette of Core/NativeHordeModels and NativeZombieModels). Each reads against the furnace glow as a dark shape
    // with a non-orange tell (3.2): the Cinderling a charred runner with a white-hot coal in its ribs, the Stoker a silver heat
    // suit with one cyan visor slit, the Slagback a brute under black glassy slag plates (its own crust model, so it can shed)
    // with a bright fissure down its back, the Ladleman a big brute in a leather apron and a welder's mask with a cyan lens,
    // the Rigger a lean rigger in a harness and a cream hard hat. Every body stays in its kind's capsule (NativeHordeKinds; the
    // checks pose them on their gaits). The held things are props, their own models on their own matrices (Prop: the rake, the
    // ladle full and spilled, the crane hook, a chain link, the head lamp's lens), so they can leave the hands, swing and fall.
    // Also the brute's cream seam variant the Ironworks draws (owner decision 14). Built once with the bank; nothing per frame.
    public static partial class NativeHordeModels
    {
        // ---- the Ironworks palette (sRGB) ----
        private const int CharHex=0x17120f,CharDeepHex=0x0a0807,AshHex=0x4c443e,SilverHex=0xb4b1a9,SilverDarkHex=0x6a6863,QuiltHex=0x8a877f,FacePlateHex=0x0e1114,
            LeatherHex=0x5e3d25,LeatherDarkHex=0x342112,IronHex=0x2c2926,IronEdgeHex=0x4f4b46,SlagHex=0x131010,SlagEdgeHex=0x2a2427,WebHex=0x24262b,HatHex=0xd2c8ad,
            HatDarkHex=0x8f866e,SteelHex=0x5a5751,GreySkinHex=0x737671,SootHex=0x4d4540;
        private static WeaponPaint Paint(int hex,float sheen=0)=>WeaponPaint.Solid(hex,sheen);

        /// <summary>The five bodies (NativeHordeModels.Build dispatches a zone kind here; false: not an Ironworks kind).</summary>
        private static bool IronworksModel(K k)
        {
            switch(NativeIronworksKinds.Local(k.Kind))
            {
                case NativeIronworksKinds.Cinderling:Cinderling(k);return true;
                case NativeIronworksKinds.Stoker:Stoker(k);return true;
                case NativeIronworksKinds.Slagback:Slagback(k);return true;
                case NativeIronworksKinds.Ladleman:Ladleman(k);return true;
                case NativeIronworksKinds.Rigger:Rigger(k);return true;
            }
            return false;
        }

        // ================================================================================================================
        // Shared: a body's albedo remapped after its Siren builder (the glow parts kept), so a zone kind reuses an anatomy
        // ================================================================================================================
        private static bool Glowing(Vector4 c,Vector4 e)=>e.W>2.5f||Math.Max(e.X,Math.Max(e.Y,e.Z))>Math.Max(c.X,Math.Max(c.Y,c.Z))*.5f+.05f;
        private static float Luma(Vector4 c)=>.2126f*c.X+.7152f*c.Y+.0722f*c.Z;
        /// <summary>Remaps every lit vertex built so far (its sheen kept as a share of the new colour).</summary>
        private static void Recolor(K k,Func<Vector3,Vector3> albedo)
        {
            foreach(var s in k.Mesh.Sections)for(int i=0;i<s.Colors.Count;i++)
            {
                var c=s.Colors[i];var e=s.Emission[i];if(Glowing(c,e)||e.W!=0)continue;float sheen=(e.X+e.Y+e.Z)/Math.Max(1e-6f,c.X+c.Y+c.Z);
                var n=albedo(new Vector3(c.X,c.Y,c.Z));s.Colors[i]=new Vector4(n,c.W);s.Emission[i]=new Vector4(n*sheen,0);
            }
        }
        private static Vector3 Lin(int hex)=>WeaponPaint.Linear(hex);
        private static Vector3 Mix(Vector3 a,Vector3 b,float t)=>a+(b-a)*(t<0?0:t>1?1:t);
        private static float Step(float a,float b,float x){float t=(x-a)/(b-a);t=t<0?0:t>1?1:t;return t*t*(3-2*t);}
        /// <summary>A glowing crack across a part (a vein in the kind's seam: white-hot on the cinderling, dim on the slagback).</summary>
        private static void Crack(K k,float x,float y,float z,float len,float rz,float kk=1,float rx=0,float w=.014f)
        {
            if(k.Far)return;k.Box(Wound,x,y,z-.002f,w*2.2f,len,.008f,0,rx,0,rz);k.Box(k.Seam(kk),x,y,z+.002f,w,len*.9f,.009f,0,rx,0,rz);
            if(k.Fine)k.Box(k.Seam(kk*.7f),x+(float)Math.Sin(rz)*len*.3f+.012f,y+len*.22f,z,w*.7f,len*.45f,.008f,0,rx,0,rz+.75f);
        }

        // ================================================================================================================
        // Cinderling: a furnace hand burned to the bone (the runner at .93), white-hot cracks, a coal burning in its ribs
        // ================================================================================================================
        private static void Cinderling(K k)
        {
            int look=k.Look;Runner(k);
            // Charred: the skin black, the suit and rags deeper, the bone to ash (the white-hot veins and eyes kept).
            Vector3 ch=Lin(CharHex),deep=Lin(CharDeepHex),ash=Lin(AshHex);
            Recolor(k,c=>{float l=.2126f*c.X+.7152f*c.Y+.0722f*c.Z;return l<.08f?Mix(deep,ch,l/.08f):Mix(ch,ash,Step(.3f,.65f,l));});
            // The coal in the ribs (where the runner's chest is torn open): a white-hot core behind a cage of charred ribs.
            float side=look==0?1:-1;
            k.On(CapturedRig.Chest);var coal=k.Glow(0,4.6f,NativeIronworksKinds.WhiteHot);var halo=k.Glow(0,2.2f,0xffd08a);
            k.Blob(coal,side*.09f,.1f,.095f,.062f,.075f,.045f);if(k.Fine)k.Blob(halo,side*.09f,.1f,.08f,.085f,.095f,.05f);
            if(!k.Far)for(int i=0;i<4;i++)k.Box(Paint(CharDeepHex),side*.09f,.02f+i*.055f,.142f,.13f,.014f,.016f,.004f,0,0,side*.08f);
            // White-hot cracks over the char: chest, back, both arms and legs (the cinderling's tell is the white, never orange).
            Crack(k,-side*.12f,.17f,.135f,.12f,.5f,1.2f);Crack(k,.02f,.24f,-.135f,.14f,-.2f,1.1f);Crack(k,-.08f,.06f,-.133f,.1f,.6f,.9f);
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LArm,m);Crack(k,-.05f,-.15f,.055f,.12f,.25f,1);
                k.On(CapturedRig.LElbow,m);Crack(k,.0f,-.11f,.05f,.11f,-.3f,.9f);
                k.On(CapturedRig.LHip,m);Crack(k,-.04f,-.12f,.105f,.12f,.2f,.9f);
                k.On(CapturedRig.LKnee,m);if(m==(look==0))Crack(k,.02f,-.1f,.09f,.1f,-.25f,.8f);
            }
            k.On(CapturedRig.Spine);Crack(k,.06f,.08f,.11f,.09f,-.4f,1);
            // A scorched rag of an apron and a burned work boot (furnace hands, not pilots).
            k.On(CapturedRig.Pelvis);k.Box(Paint(0x231a14),0,-.13f,.125f,.26f,.22f,.012f,.004f,-.12f);if(k.Mid)k.Box(Paint(0x120d0a),.05f,-.24f,.13f,.12f,.05f,.01f,0,-.12f,0,.3f);
            // The skull: split by a white-hot crack where the smoke streams out (the plume is the effects'); look 1 a melted cap.
            k.On(CapturedRig.Head);float hw=.16f*HeadWidth[NativeHordeKinds.Runner];
            if(look==0){Crack(k,.02f,.3f,.02f,.13f,.35f,1.3f,1.45f,.012f);Crack(k,-.05f,.25f,.13f,.07f,-.3f,1.1f);}
            else{k.Box(Paint(0x1f1914),0,.3f,-.02f,hw*2.1f,.06f,.29f,.02f,-.1f);k.Box(Paint(0x0d0a08),0,.275f,.11f,hw*2.15f,.02f,.08f,.006f,.25f);Crack(k,.06f,.24f,.135f,.06f,.4f,1.2f);}
        }

        // ================================================================================================================
        // Stoker: the aluminised heat suit (silver catches every light as an edge), a square hood with one cyan visor slit, a coat
        // to the knees (its halves ride the thighs), gauntlets and boots; taller than a shambler (drawn at 1.07)
        // ================================================================================================================
        private static void Stoker(K k)
        {
            int look=k.Look;var silver=Paint(SilverHex,.1f);var dark=Paint(SilverDarkHex,.05f);var quilt=Paint(QuiltHex,.06f);var glove=Paint(LeatherDarkHex,.03f);var boot=Paint(0x15120f);
            // Hips and waist: the suit trunks, a belt; the coat's back panel hangs from the pelvis.
            k.On(CapturedRig.Pelvis);k.Box(silver,0,0,0,.42f,.22f,.27f,.03f);k.Box(Paint(0x2a2622),0,.08f,0,.43f,.05f,.28f,.01f);
            k.Box(silver,0,-.2f,-.14f,.42f,.36f,.035f,.012f,.14f);if(k.Mid){for(int i=0;i<3;i++)k.Box(quilt,(i-1)*.13f,-.2f,-.162f,.012f,.32f,.008f,0,.14f);}
            k.On(CapturedRig.Spine);k.Box(silver,0,.075f,0,.36f,.19f,.26f,.035f);if(k.Mid)k.Box(quilt,0,.1f,.132f,.3f,.012f,.008f);
            // The jacket, the hood's cape over the shoulders, a respirator canister and its cyan status lamp.
            k.On(CapturedRig.Chest);k.Box(silver,0,.1f,0,.52f,.31f,.3f,.045f);k.Box(silver,0,.24f,0,.47f,.08f,.28f,.025f);
            if(k.Mid){for(int i=0;i<3;i++)k.Box(quilt,0,.02f+i*.08f,.151f,.42f,.012f,.008f);k.Box(dark,0,.1f,-.152f,.36f,.22f,.012f,.006f);}
            k.Box(silver,0,.285f,-.02f,.6f,.06f,.36f,.025f);k.Box(silver,0,.255f,-.17f,.5f,.1f,.05f,.02f,.2f);
            k.On(CapturedRig.Chest);if(!k.Far){k.S.Push(WeaponSculpt.Rx(-(float)Math.PI/2)*WeaponSculpt.T(.11f,.12f,.16f));k.S.Cylinder(Paint(0x1c1a18,.04f),.05f,-.05f,.06f,k.S.Sides(10),.01f);k.S.Pop();}
            if(k.Mid)k.Box(k.Seam(.9f),.11f,.17f,.205f,.025f,.012f,.008f);
            // Shoulders: puffed silver pads with a dark seam.
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);k.Box(silver,-.07f,.0f,0,.2f,.13f,.23f,.04f,0,0,-.06f);if(k.Mid)k.Box(dark,-.07f,-.06f,0,.19f,.02f,.22f,.006f);
                // Sleeves, the elbow's gather, gauntlets.
                k.On(CapturedRig.LArm,m);k.Limb(silver,.03f,-.27f,.082f,.072f);if(k.Mid)k.Box(dark,0,-.24f,0,.15f,.03f,.15f,.008f);
                k.On(CapturedRig.LElbow,m);k.Limb(silver,.02f,-.17f,.07f,.064f);k.Box(glove,0,-.2f,0,.15f,.09f,.15f,.02f);
                k.On(CapturedRig.LHand,m);k.Box(glove,0,-.05f,.01f,.15f,.13f,.16f,.025f);if(!k.Far){for(int i=0;i<3;i++)k.Box(glove,(i-1)*.04f,-.13f,.06f,.035f,.07f,.045f,.008f,.4f);k.Box(glove,.07f,-.06f,.05f,.04f,.08f,.06f,.01f,.2f);}
                // Trousers to the boots; the coat's front half on the thigh (it swings with the leg, never through it).
                k.On(CapturedRig.LHip,m);k.Limb(silver,.02f,-.25f,.092f,.082f);k.Box(silver,0,-.17f,.105f,.19f,.38f,.03f,.012f,-.06f);k.Box(silver,-.095f,-.17f,.0f,.03f,.38f,.19f,.012f);
                if(k.Mid)k.Box(quilt,0,-.2f,.123f,.15f,.012f,.008f,0,-.06f);
                k.On(CapturedRig.LKnee,m);k.Limb(silver,.02f,-.2f,.078f,.07f);if(k.Mid)k.Box(dark,0,-.01f,.075f,.13f,.07f,.02f,.008f);
                k.On(CapturedRig.LFoot,m);k.Box(boot,0,-.025f,-.03f,.15f,.1f,.2f,.015f);k.On(CapturedRig.LToe,m);k.Box(boot,0,-.01f,.02f,.15f,.07f,.15f,.014f);
            }
            k.On(CapturedRig.Neck);k.Limb(silver,-.03f,.1f,.075f,.07f);
            // The square hood: taller than a skull, a black face plate with one cyan visor slit (the Stoker's tell; its weak
            // point), a grille under it. Look 1: a domed hood with a short brim.
            k.On(CapturedRig.Head);
            if(look==0){k.Box(silver,0,.18f,-.005f,.3f,.36f,.29f,.035f);k.Box(silver,0,.37f,-.005f,.27f,.03f,.25f,.012f);}
            else{k.Box(silver,0,.165f,-.005f,.3f,.33f,.29f,.035f);k.Blob(silver,0,.32f,-.005f,.15f,.065f,.145f);k.Box(silver,0,.275f,.155f,.28f,.02f,.05f,.008f,.2f);}
            k.Box(Paint(FacePlateHex,.02f),0,.17f,.142f,.23f,.19f,.02f,.008f);
            k.Box(k.Seam(1.9f),0,.205f,.155f,.19f,.03f,.01f,0);
            if(k.Mid){for(int i=0;i<3;i++)k.Box(dark,0,.115f-i*.022f,.154f,.13f,.008f,.006f);k.Box(dark,0,.03f,.11f,.27f,.06f,.11f,.012f);}
            if(!k.Far)k.Box(k.Seam(.6f),.12f,.28f,.135f,.012f,.012f,.006f);
        }

        // ================================================================================================================
        // Slagback: a brute burned black under glassy slag plates (the crust is its own model: Crust), dim cracks between, the
        // bright fissure down its back (its weak point)
        // ================================================================================================================
        private static void Slagback(K k)
        {
            Brute(k);
            Vector3 soot=Lin(0x241d19),deep=Lin(0x0e0b0a),scar=Lin(0x3b2f29);
            Recolor(k,c=>{float l=.2126f*c.X+.7152f*c.Y+.0722f*c.Z;return l<.06f?Mix(deep,soot,l/.06f):Mix(soot,scar,Step(.3f,.6f,l));});
            // The fissure: a white-gold split down the back of the chest and the spine (double damage; it glows through the crust).
            var fissure=k.Glow(0,3.4f,NativeIronworksKinds.Fissure);var rim=k.Glow(0,1.6f,0xff9a4a);
            k.On(CapturedRig.Chest);k.Box(Wound,0,.17f,-.205f,.08f,.34f,.012f,0,.05f);k.Box(fissure,0,.17f,-.212f,.03f,.31f,.01f,0,.05f);
            if(!k.Far){k.Box(rim,.022f,.2f,-.209f,.012f,.2f,.008f,0,.05f,0,.12f);k.Box(rim,-.02f,.1f,-.209f,.012f,.16f,.008f,0,.05f,0,-.15f);}
            k.On(CapturedRig.Spine);k.Box(Wound,0,.08f,-.15f,.06f,.18f,.012f);k.Box(fissure,0,.08f,-.157f,.024f,.16f,.01f);
        }
        /// <summary>The Slagback's crust (its own model on the same joints, drawn while crusted; Runtime sheds it at shedAt):
        /// black glassy plates on the chest, shoulders, upper back, arms, forearms, thighs and head, dim cracks along their edges.</summary>
        public static WeaponMesh IronworksCrust(int look,int tier)
        {
            int kind=NativeIronworksKinds.Kind(NativeIronworksKinds.Slagback);var k=new K(kind,Math.Max(0,Math.Min(1,look)),tier);k.ReducedBevel=.02f;
            var slag=Paint(SlagHex,.03f);var edge=Paint(SlagEdgeHex,.08f);float s=look==0?1:-1;
            void Plate(float x,float y,float z,float w,float h,float d,float rx=0,float ry=0,float rz=0)
            {
                k.Box(slag,x,y,z,w,h,d,.02f,rx,ry,rz);
                if(k.Mid)k.Box(edge,x,y+h*.42f,z+d*.35f*(z>=0?1:-1),w*.86f,h*.12f,d*.5f,.006f,rx,ry,rz);
                if(!k.Far)k.Box(k.Seam(.45f),x,y-h*.5f-.006f,z,w*.8f,.012f,d*.9f,0,rx,ry,rz);
            }
            // Chest front: three plates, a crack between each; the upper back either side of the fissure; a lump on one shoulder.
            k.On(CapturedRig.Chest);Plate(-.14f,.21f,.2f,.24f,.17f,.06f,.12f,.1f,-.08f);Plate(.15f,.2f,.2f,.22f,.16f,.06f,.1f,-.12f,.1f);Plate(.01f,.04f,.195f,.4f,.14f,.055f,-.08f);
            Plate(-.15f,.24f,-.2f,.22f,.24f,.06f,-.1f,-.15f,.06f);Plate(.15f,.26f,-.2f,.2f,.22f,.06f,-.12f,.15f,-.05f);Plate(s*.1f,.39f,-.08f,.26f,.08f,.22f,0,0,s*.3f);
            k.On(CapturedRig.Spine);Plate(-.12f,.08f,.17f,.15f,.12f,.05f);Plate(.13f,.06f,.168f,.14f,.11f,.05f);
            k.On(CapturedRig.Pelvis);Plate(0,-.04f,.165f,.36f,.13f,.05f,-.1f);
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);Plate(-.07f,.1f,0,.23f,.08f,.26f,0,0,-.18f);
                k.On(CapturedRig.LArm,m);Plate(-.09f,-.12f,.02f,.06f,.2f,.2f,0,0,.12f);
                k.On(CapturedRig.LElbow,m);Plate(-.05f,-.12f,.07f,.2f,.2f,.1f,.15f,.2f,.1f);
                k.On(CapturedRig.LHip,m);Plate(0,-.12f,.13f,.2f,.18f,.05f,-.05f);
                if(m==(look==1)){k.On(CapturedRig.LKnee,m);Plate(0,-.06f,.1f,.17f,.12f,.05f,.1f);}
            }
            k.On(CapturedRig.Head);Plate(0,.205f+HeadDrop[NativeHordeKinds.Brute],-.02f,.22f,.06f,.22f,.25f);
            return k.Mesh;
        }

        // ================================================================================================================
        // Ladleman: a big brute (drawn at 1.24) in a leather apron and a welder's mask with a cyan lens, gauntlets, boots
        // ================================================================================================================
        private static void Ladleman(K k)
        {
            Brute(k);
            Vector3 soot=Lin(SootHex),deep=Lin(0x15120f),pale=Lin(0x6a5e55);
            Recolor(k,c=>{float l=.2126f*c.X+.7152f*c.Y+.0722f*c.Z;return l<.06f?Mix(deep,Lin(0x231d19),l/.06f):Mix(soot,pale,Step(.3f,.6f,l));});
            var leather=Paint(LeatherHex,.05f);var dark=Paint(LeatherDarkHex,.03f);var iron=Paint(IronHex,.06f);var edge=Paint(IronEdgeHex,.1f);k.ReducedBevel=.06f;
            // The apron: a bib over the chest on straps, the skirt in two halves on the thighs, singed and spattered.
            k.On(CapturedRig.Chest);k.Box(leather,0,.08f,.215f,.46f,.34f,.025f,.008f,.06f);
            if(k.Fine){k.Box(dark,0,.24f,.212f,.46f,.025f,.026f);for(int i=0;i<3;i++)k.Box(Paint(0x231a12),(i-1)*.13f+.03f,.0f+i*.06f,.23f,.06f,.05f,.006f,0,0,0,.4f*i);}
            if(k.Mid)foreach(float sx in new[]{-1f,1f}){k.Box(dark,sx*.16f,.31f,.04f,.05f,.04f,.36f,.006f,-.25f);}
            if(!k.Far){k.On(CapturedRig.Pelvis);k.Box(leather,0,-.04f,.175f,.44f,.18f,.025f,.008f);if(k.Mid)k.Box(dark,0,.07f,0,.5f,.04f,.33f,.006f);}
            foreach(bool m in new[]{false,true})
            {
                if(!k.Far){k.On(CapturedRig.LHip,m);k.Box(leather,.02f,-.19f,.145f,.23f,.4f,.025f,.008f,-.07f);if(k.Fine)k.Box(dark,.02f,-.38f,.155f,.23f,.02f,.026f,0,-.07f);}
                // Gauntlets to the elbow, boots.
                if(k.Mid){k.On(CapturedRig.LElbow,m);k.Box(dark,0,-.17f,0,.29f,.13f,.29f,.03f);}
                if(!k.Far){k.On(CapturedRig.LHand,m);k.Box(dark,0,-.06f,.02f,.25f,.2f,.25f,.04f);}
                if(k.Mid){k.On(CapturedRig.LFoot,m);k.Box(Paint(0x1a130d),0,-.02f,-.035f,.18f,.11f,.21f,.018f);k.On(CapturedRig.LToe,m);k.Box(Paint(0x1a130d),0,-.006f,.015f,.18f,.09f,.16f,.016f);}
            }
            // The welder's mask over the sunken face: an iron plate, its cyan lens (the tell), rivets, the head strap.
            k.On(CapturedRig.Head);float y=HeadDrop[NativeHordeKinds.Brute];
            k.Box(iron,0,.16f+y,.125f,.25f,.24f,.03f,.01f,-.05f);if(k.Mid)k.Box(edge,0,.27f+y,.11f,.25f,.025f,.06f,.006f);
            if(!k.Far)k.Box(Paint(FacePlateHex),0,.195f+y,.142f,.17f,.05f,.01f,.004f);k.Box(k.Glow(1,2.4f,NativeIronworksKinds.Cyan),0,.195f+y,.148f,.15f,.032f,.008f);
            if(k.Mid){foreach(float sx in new[]{-1f,1f}){k.Box(edge,sx*.105f,.08f+y,.142f,.02f,.02f,.01f);k.Box(dark,sx*.13f,.19f+y,-.01f,.02f,.04f,.24f);}k.Box(dark,0,.29f+y,-.02f,.25f,.03f,.2f,.006f);}
        }

        // ================================================================================================================
        // Rigger: a lean crane rigger (the shambler's build, drawn at 1.04): dead grey skin, work overalls, a webbing harness
        // with two shackles, a chain coil over the chest, gloves, a cream hard hat with the head lamp's housing (its lens is a prop,
        // lit only while the lamp is on)
        // ================================================================================================================
        private static void Rigger(K k)
        {
            int look=k.Look;
            Hips(k,.9f,false,false,look==0?6:0);Chest(k,.86f,0,false);
            Shoulder(k,false,false,.86f);Shoulder(k,true,false,.86f);Arm(k,false,false,.84f,.03f);Arm(k,true,false,.86f,.03f);
            Leg(k,false,.88f,true,false,look==1,false);Leg(k,true,.88f,look==0,false,true,false);
            Neck(k,.052f,.11f,Skin);Head(k,HeadWidth[NativeHordeKinds.Shambler]*.94f,0,.3f,1);
            Vector3 grey=Lin(GreySkinHex),grey0=Lin(0x4f524d),cloth=Lin(0x2b3036),cloth0=Lin(0x15191d);
            Recolor(k,c=>{float l=.2126f*c.X+.7152f*c.Y+.0722f*c.Z;bool skinny=c.Y>c.Z*1.05f&&l>.08f;return skinny?Mix(grey0,grey,Step(.12f,.3f,l)):l<.05f?Mix(cloth0,cloth,l/.05f):Mix(cloth,Lin(0x9a968b),Step(.3f,.6f,l));});
            var web=Paint(WebHex,.04f);var steel=Paint(SteelHex,.12f);var glove=Paint(0x1e1a16,.03f);var hat=Paint(look==0?HatHex:0xc7b98a,.08f);var hatDark=Paint(HatDarkHex,.05f);
            // The harness: crossed straps front and back, a belt with two shackles, a ring at the sternum.
            k.On(CapturedRig.Chest);foreach(float sx in new[]{-1f,1f}){k.Box(web,sx*.06f,.12f,.152f,.045f,.34f,.014f,.004f,0,0,sx*.42f);if(!k.Far)k.Box(web,sx*.06f,.12f,-.153f,.045f,.34f,.014f,.004f,0,0,-sx*.42f);}
            if(!k.Far){k.S.Push(WeaponSculpt.T(0,.13f,.162f));k.S.Torus(steel,Vector3.Zero,.03f,.008f,k.S.Sides(10),k.S.Sides(6));k.S.Pop();}
            // The chain coil over the left shoulder and across the chest.
            // (a bandolier: a ring round the torso, squashed front to back, tilted from the left shoulder to the right hip)
            if(!k.Far){k.S.Push(WeaponSculpt.Rx((float)Math.PI/2)*WeaponSculpt.S(1,1,.62f)*WeaponSculpt.Rz(.62f)*WeaponSculpt.T(0,.12f,0));for(int i=0;i<(k.Fine?2:1);i++)k.S.Torus(steel,Vector3.Zero,.29f+i*.016f,.016f,k.S.Sides(k.Fine?22:12),k.S.Sides(k.Fine?5:4));k.S.Pop();}
            if(k.Mid){k.On(CapturedRig.Spine);k.Box(web,0,.03f,0,.33f,.05f,.25f,.006f);}
            if(!k.Far){k.On(CapturedRig.Pelvis);k.Box(web,0,.075f,0,.4f,.055f,.29f,.006f);}
            if(!k.Far)foreach(float sx in new[]{-1f,1f})k.Box(steel,sx*.21f,.0f,.06f,.025f,.08f,.045f,.006f);
            if(k.Mid)foreach(float sx in new[]{-1f,1f}){k.S.Push(WeaponSculpt.Ry((float)Math.PI/2)*WeaponSculpt.T(sx*.215f,-.05f,.06f));k.S.Torus(steel,Vector3.Zero,.03f,.007f,k.S.Sides(10),k.S.Sides(5),(float)Math.PI*.1f,(float)Math.PI*1.9f);k.S.Pop();}
            foreach(bool m in new[]{false,true}){k.On(CapturedRig.LHand,m);k.Box(glove,0,-.05f,.005f,.14f,.11f,.15f,.02f);if(k.Mid){k.On(CapturedRig.LElbow,m);k.Box(glove,0,-.21f,0,.12f,.05f,.12f,.01f);}}
            // The hard hat: a cream dome with a ridge and a brim, the lamp's housing on its front (the lens is lit by the prop).
            k.On(CapturedRig.Head);float hw=.16f*HeadWidth[NativeHordeKinds.Shambler]*.94f;
            k.Blob(hat,0,.31f,-.005f,hw*1.1f,.08f,.155f);k.Box(hat,0,.302f,.01f,hw*2.15f,.016f,.32f,.006f);if(k.Fine)k.Box(hatDark,0,.385f,-.005f,.035f,.02f,.26f,.006f);
            k.Box(Paint(0x1b1d20,.05f),0,.335f,.163f,.07f,.055f,.04f,.01f);if(!k.Far)k.Box(Paint(0x0b0c0e),0,.335f,.185f,.05f,.04f,.006f);
            if(k.Fine)k.Box(web,0,.32f,.0f,hw*2.1f,.02f,.28f);
        }

        // ================================================================================================================
        // The brute's cream seams (owner decision 14: the Ironworks only): the same model with its seams and eyes cream
        // ================================================================================================================
        /// <summary>The bank id of the cream brute's look at a tier.</summary>
        public static string CreamId(int look,int tier){if(creamIds==null){var a=new string[NativeHordeKinds.MostLooks*Tiers];for(int l=0;l<NativeHordeKinds.MostLooks;l++)for(int t=0;t<Tiers;t++)a[l*Tiers+t]="minion-brute-"+l+"-"+TierNames[t]+"-cream";creamIds=a;}return creamIds[look*Tiers+tier];}
        private static string[] creamIds;

        // ================================================================================================================
        // Props: the rake, the ladle (full and spilled), the crane hook, a chain link, the lamp's lens (one section, joint 0,
        // Unity axes: +z along the prop, +y up). Two tiers (full, reduced).
        // ================================================================================================================
        public const int PropRake=0,PropLadle=1,PropLadleEmpty=2,PropHook=3,PropLink=4,PropLamp=5,PropCount=6,PropTiers=2;
        public static readonly string[] PropNames={"rake","ladle","ladle-empty","hook","link","lamp"};
        public static string PropId(int prop,int tier){if(propIds==null){var a=new string[PropCount*PropTiers];for(int p=0;p<PropCount;p++)for(int t=0;t<PropTiers;t++)a[p*PropTiers+t]="iw-prop-"+PropNames[p]+"-"+t;propIds=a;}return propIds[prop*PropTiers+Math.Max(0,Math.Min(PropTiers-1,tier))];}
        private static string[] propIds;
        /// <summary>The rake's pole runs from its butt to the hook (m along +z from the right-hand grip); the ladle's bowl centre;
        /// the hook's point and its eye; a link's pitch along a chain.</summary>
        public const float RakeButt=-.55f,RakeTip=2.42f,LadleBowl=.76f,LadleBowlRadius=.28f,HookEye=.0f,HookDrop=.62f,LinkPitch=.074f;
        public static WeaponMesh IronworksProp(int prop,int tier)
        {
            var mesh=new WeaponMesh{Spec="iw-prop-"+prop,Tier=tier};var s=new WeaponSculpt(mesh,tier);s.Into(0);bool fine=tier==0;
            var iron=Paint(IronHex,.08f);var edge=Paint(IronEdgeHex,.12f);var steel=Paint(SteelHex,.14f);var worn=Paint(0x6f6a62,.16f);
            switch(prop)
            {
                case PropRake:
                {
                    // A long iron rake: the pole (a wrapped grip near the butt), the head's crossbar and its hooked tine.
                    s.Cylinder(iron,.022f,RakeButt,RakeTip-.05f,s.Sides(8),.006f);s.Cylinder(Paint(0x3a2618,.04f),.027f,-.2f,.32f,s.Sides(8),.004f);
                    if(fine)for(int i=0;i<4;i++)s.Cylinder(Paint(0x24170e),.029f,-.17f+i*.13f,-.15f+i*.13f,s.Sides(8));
                    s.Box(edge,new Vector3(0,0,RakeTip-.04f),new Vector3(.3f,.045f,.05f),.008f);
                    var tine=new List<Vector3>{new Vector3(0,0,RakeTip-.02f),new Vector3(0,-.02f,RakeTip+.08f),new Vector3(0,-.1f,RakeTip+.14f),new Vector3(0,-.2f,RakeTip+.12f),new Vector3(0,-.24f,RakeTip+.04f)};
                    tine.Add(new Vector3(0,-.31f,RakeTip-.03f));
                    s.Tube(_=>worn,tine,i=>i==5?.002f:.022f-.003f*i,s.Sides(6),Vector3.UnitX,1,true);
                    foreach(float x in new[]{-.12f,.12f})s.Box(edge,new Vector3(x,-.05f,RakeTip-.03f),new Vector3(.025f,.11f,.03f),.004f);
                    break;
                }
                case PropLadle:case PropLadleEmpty:
                {
                    // The shank with its cross grip, the fork to the bowl's trunnion ring, the crucible (lathe about +y), its pour lip;
                    // full: the molten surface (white-hot middle, orange rim) and bubbles: the brightest thing on any enemy.
                    s.Cylinder(iron,.03f,-.36f,LadleBowl-.34f,s.Sides(8),.008f);s.Box(edge,new Vector3(0,0,-.36f),new Vector3(.4f,.045f,.045f),.01f);
                    foreach(float x in new[]{-.18f,.18f})s.Cylinder(Paint(0x2c1d12),.03f,-.38f,-.34f,s.Sides(6),0,x,0);
                    foreach(float x in new[]{-1f,1f}){var fork=new List<Vector3>{new Vector3(0,0,LadleBowl-.36f),new Vector3(x*.2f,.02f,LadleBowl-.25f),new Vector3(x*(LadleBowlRadius+.025f),.035f,LadleBowl)};s.Tube(iron,fork,.02f,s.Sides(6),Vector3.UnitY);}
                    s.Push(WeaponSculpt.Rx(-(float)Math.PI/2)*WeaponSculpt.T(0,0,LadleBowl));
                    s.Lathe(iron,new[]{.17f,-.2f,.225f,-.16f,.265f,-.04f,.28f,.11f,.292f,.155f},s.Sides(16),1);
                    s.Torus(edge,new Vector3(0,0,.155f),.29f,.02f,s.Sides(16),s.Sides(6));s.Torus(edge,new Vector3(0,0,.015f),.277f,.016f,s.Sides(16),s.Sides(5));
                    s.Pop();
                    s.Box(iron,new Vector3(0,.125f,LadleBowl+LadleBowlRadius+.025f),new Vector3(.1f,.045f,.09f),.01f,-.35f);
                    if(prop==PropLadle)
                    {
                        var hot=WeaponPaint.Glow(NativeIronworksKinds.Molten,3.6f,.6f);var core=WeaponPaint.Glow(0xfff0c0,5.2f,.8f);
                        s.Push(WeaponSculpt.Rx(-(float)Math.PI/2)*WeaponSculpt.T(0,.13f,LadleBowl));s.Cylinder(hot,.265f,-.02f,.012f,s.Sides(16));s.Cylinder(core,.155f,0,.02f,s.Sides(12));s.Pop();
                        if(fine)for(int i=0;i<5;i++){double a=i*2.4;s.Sphere(core,new Vector3((float)Math.Cos(a)*.13f,.145f,LadleBowl+(float)Math.Sin(a)*.13f),new Vector3(.025f,.02f,.025f),3,6);}
                    }
                    else{s.Push(WeaponSculpt.Rx(-(float)Math.PI/2)*WeaponSculpt.T(0,.06f,LadleBowl));s.Cylinder(Paint(0x0c0a09),.25f,-.02f,.01f,s.Sides(14));s.Pop();if(fine)s.Box(WeaponPaint.Glow(0xff6a20,1.2f,.3f),new Vector3(.05f,.07f,LadleBowl+.04f),new Vector3(.1f,.01f,.04f),0,0,.4f);}
                    break;
                }
                case PropHook:
                {
                    // A crane hook (about .6 m): the eye at the origin, the shank, the C of the hook open to +z, its point; a latch.
                    s.Push(WeaponSculpt.Ry((float)Math.PI/2));s.Torus(steel,new Vector3(0,-.06f,0),.055f,.016f,s.Sides(12),s.Sides(6));s.Pop();
                    s.Box(steel,new Vector3(0,-.17f,0),new Vector3(.05f,.15f,.06f),.012f);s.Box(edge,new Vector3(0,-.26f,0),new Vector3(.07f,.05f,.075f),.012f);
                    // the C: down the shank's line, round the bowl (centre .1 ahead, .42 down, radius .1) and curling up to the point
                    var c=new List<Vector3>{new Vector3(0,-.28f,0)};for(int i=0;i<=10;i++){double a=Math.PI*(1+1.32*i/10.0);c.Add(new Vector3(0,-.42f+.1f*(float)Math.Sin(a),.1f+.1f*(float)Math.Cos(a)));}
                    s.Tube(_=>steel,c,i=>i==c.Count-1?.003f:.042f-.0028f*i,s.Sides(8),Vector3.UnitX,1,true);
                    if(fine)s.Box(worn,new Vector3(0,-.33f,.09f),new Vector3(.012f,.11f,.012f),0,.6f);
                    break;
                }
                case PropLink:
                {
                    // One oval link along +z (the chain is laid link by link, each turned a quarter to the last).
                    var path=new List<Vector3>();int n=fine?10:6;for(int i=0;i<n;i++){double a=Math.PI*2*i/n;path.Add(new Vector3(0,(float)Math.Sin(a)*.022f,(float)Math.Cos(a)*.043f));}
                    s.Tube(_=>steel,path,_=>.0085f,s.Sides(5),Vector3.UnitX,1,false,true);
                    break;
                }
                case PropLamp:
                {
                    // The head lamp's lens, lit: a cold white disc facing +z and its hot core.
                    s.Cylinder(WeaponPaint.Glow(NativeIronworksKinds.ColdWhite,4.5f,.8f),.024f,0,.008f,s.Sides(10));s.Cylinder(WeaponPaint.Glow(0xffffff,8f,1),.012f,.008f,.011f,s.Sides(8));
                    break;
                }
            }
            return mesh;
        }
    }
}

using System;

namespace ChipArena.UnityConversion
{
    // The zombies on the player's model (owner 2026-10-29: "Implement the attached zombie designs in Chip Arena using the existing
    // low-poly player model as the starting point", "Shambler, Runner, Spitter, Brute, and Hulk, with two male and two female visual
    // variants per type"; docs/ZOMBIE-MODELS.md). Each variant is the bake's one model (Runtime/NativeZombieModelPresentation.cs)
    // dressed and shaped from this table: a piece per wardrobe slot (Core/NativeLook's slots; the first candidate the bake carries,
    // the last always one of today's pieces, so every install dresses it), each piece's colour, the skin, the blank eyes, the body
    // and face blendshapes by name, the bone build (limb girth on the twist bones, hand and head size, shoulder width), the wear
    // (grime, torn patches, rotting skin with gashes, dried blood), its decay openings onto flesh and bone (Core NativeZombieDecay)
    // and the spitter's throat sac. Shared meshes, rig, materials and motion: a variant is data.
    // Presentation only: no rule, hit volume, timing, AI or network authority reads it; a variant is picked by the add's id, the
    // same on every peer, and changes nothing a fight reads.
    public static class NativeZombieLooks
    {
        /// <summary>Variants per kind: male A, male B, female A, female B.</summary>
        public const int Variants=4;
        /// <summary>A slot left bare (hair only: a bald head).</summary>
        public const string None="none";
        /// <summary>Pieces only the zombies wear (the bake adds them from the pack; never in the Locker's lists).</summary>
        public const string Own="z-";
        /// <summary>The blendshape names a variant sets (the pack's body and face shapes, matched by name on every worn mesh that has
        /// them, so clothes follow the body).</summary>
        public static readonly string[] ShapeNames={"BodyType","Weight","Muscle","Belly","Chest","NeckThickness","Curvy","ButtSize","WaistSize","Maturity","Sharpness","Roundness"};
        /// <summary>The body shapes every variant sets (the bake's preset weights never leak through).</summary>
        public const int BodyShapes=9;
        public sealed class Build
        {
            /// <summary>Girth of the upper arm, forearm, thigh and calf (their twist bones' cross-section), the hands' and head's size,
            /// the shoulders' span (the upper arm set out along the collarbone), and a mutant's: the right arm's own growth past the
            /// left (its girth and hand times Right), the neck's length (Neck: the head sunk between the shoulders) and the arms'
            /// (Reach: the elbow and the wrist set out along the limb).</summary>
            public float Arm=1,Forearm=1,Thigh=1,Calf=1,Hand=1,Head=1,Shoulder=1,Right=1,Neck=1,Reach=1;
        }
        public sealed class Variant
        {
            public string Id;public int Kind;public bool Female;
            /// <summary>Per slot (NativeLook.Slots): candidate piece ids, best first, '|' between ("none": bare).</summary>
            public string[] Pieces;
            /// <summary>Per slot: the piece's colour (0xRRGGBB; its own colours' light and shade kept), -1 as baked.</summary>
            public int[] Colours;
            public int Skin,Eyes;
            /// <summary>Blendshape weights by ShapeNames order (NaN: left as baked).</summary>
            public float[] Shapes;
            public Build Build;
            /// <summary>Grime on the clothes, torn patches, rot on the skin (with its gashes), dried blood in the clothes (0..1).</summary>
            public float Grime,Tears,Rot,Blood;
            /// <summary>The decay openings onto flesh and bone (Core NativeZombieDecay site names, at most NativeZombieDecay.Max).</summary>
            public string[] Decay;
            /// <summary>The spitter's throat sac (Runtime: a skinned sac on the neck that swells with the spit).</summary>
            public bool Sac;
            /// <summary>A mutant (the Hulk's): grown flesh, bone spurs and pustules over its build (Core NativeZombieMutation).</summary>
            public bool Mutant;
            /// <summary>Reflective tape on its upper piece: a hi-vis jacket's rings round the chest, the waist and both sleeves
            /// (Runtime ZombieTape; the shader's ZombieBand).</summary>
            public bool Tape;
            /// <summary>The gear it wears: the gear bank's props by id (Resources/ChipArenaPackGear, tools/gear), each on the model's
            /// bone its group names (Runtime ZombieGear). A prop the bank lacks is left off.</summary>
            public string[] Gear=Array.Empty<string>();
            /// <summary>A second commuter fused into its right shoulder: its own head, screaming, and a second right arm, raised
            /// (Runtime ZombieFuse; the gear's flesh joins them).</summary>
            public bool Fused;
            /// <summary>No face (the Faceling's): the eyes, brows, lashes and teeth left off, smooth skin over the face (its gear's
            /// skin-toned mask).</summary>
            public bool Faceless;
            /// <summary>Stripes round its upper and lower pieces (a vintage swimsuit's), their colour (0xRRGGBB), -1 none
            /// (Runtime ZombieStripes; the shader's ZombieStripe).</summary>
            public int Stripes=-1;
            /// <summary>A gear bank prop carried in its right hand (the Lifeguard's rescue hook), or null.</summary>
            public string Carry;
            /// <summary>How upright it stands (0..1: the takes' hunch straightened, Core NativeZombiePosture.Upright): the Lifeguard's
            /// tall, still figure, the Faceling's and the Drowned's a little.</summary>
            public float Upright;
        }

        private static float[] Body(float type,float weight,float muscle,float belly,float chest,float neck,float curvy,float butt,float waist,float maturity=float.NaN,float sharp=float.NaN,float round=float.NaN)
            =>new[]{type,weight,muscle,belly,chest,neck,curvy,butt,waist,maturity,sharp,round};
        private static Variant V(string id,int kind,bool female,string hair,string upper,string lower,string hands,string feet,int[] colours,int skin,float[] shapes,Build build,float grime,float tears,float rot,bool sac=false,int eyes=0xe9ece4,string decay=null,float blood=0,bool mutant=false)
            =>new Variant{Id=id,Kind=kind,Female=female,Pieces=new[]{hair,upper,lower,hands,feet},Colours=colours,Skin=skin,Eyes=eyes,Shapes=shapes,Build=build,Grime=grime,Tears=tears,Rot=rot,Sac=sac,Mutant=mutant,Decay=string.IsNullOrEmpty(decay)?Array.Empty<string>():decay.Split(','),Blood=blood};
        private const int Keep=-1;
        private static readonly int LinesmanKind=NativeStationKinds.Kind(NativeStationKinds.Linesman),ConductorKind=NativeStationKinds.Kind(NativeStationKinds.Conductor),
            UndertowKind=NativeStationKinds.Kind(NativeStationKinds.Undertow),PileupKind=NativeStationKinds.Kind(NativeStationKinds.Pileup);
        private static readonly int FacelingKind=NativeHordeKinds.Resolve("faceling"),HoundKind=NativeHordeKinds.Resolve("hound"),
            DrownedKind=NativeHordeKinds.Resolve("drowned"),LifeguardKind=NativeHordeKinds.Resolve("lifeguard");

        /// <summary>Every variant, by kind then male A, male B, female A, female B (the owner's sheets).</summary>
        public static readonly Variant[] All={
            // ---- Shambler: an average build in everyday clothes, the common one ----
            V("shambler-male-a",NativeHordeKinds.Shambler,false,"wild","z-hoodie-vest|open-hoodie","skinny-jeans","bare","sneakers",new[]{0x1c1b20,0x2f3a55,0x3a3d43,Keep,0x2a2a2e},0xa3ab97,
                Body(45,10,15,5,10,30,0,0,0,30),new Build(),.55f,.56f,.59f,false,0xe9ece4,decay:"ribs-l,forearm-r",blood:0.5f),
            V("shambler-male-b",NativeHordeKinds.Shambler,false,None,"z-work-jacket|hard-jacket","khakis","bare","work-boots",new[]{Keep,0x6b4a32,0x5a5c5e,Keep,0x3b2e24},0xa7a596,
                Body(55,45,10,45,15,40,0,0,10,65),new Build{Arm=1.03f,Thigh=1.03f},.6f,.48f,.65f,false,0xe9ece4,decay:"cheek-l,shin-r,back",blood:0.5f),
            V("shambler-female-a",NativeHordeKinds.Shambler,true,"side-swept","hoodie","skinny-jeans","bare","sneakers",new[]{0x1f1d22,0x5d3043,0x38393d,Keep,0x2e2e33},0xa9ae9f,
                Body(-10,5,10,0,35,10,25,25,0,30),new Build{Shoulder=.97f},.5f,.56f,.52f,false,0xe9ece4,decay:"forearm-l,ribs-r",blood:0.5f),
            V("shambler-female-b",NativeHordeKinds.Shambler,true,"tied-back","z-cardigan|sweater","khakis","bare","lace-ups",new[]{0x4b3a2c,0x3f6a6a,0x4d3b2e,Keep,0x3a2f28},0xa8a99b,
                Body(-5,40,5,25,40,15,35,35,10,55),new Build{Shoulder=.98f},.55f,.48f,.59f,false,0xe9ece4,decay:"upperarm-r,thigh-l",blood:0.5f),
            // ---- Runner: lean and athletic in sportswear ----
            V("runner-male-a",NativeHordeKinds.Runner,false,"spiky","z-hoodie-vest|open-hoodie","z-joggers|baggy-pants","bare","sneakers",new[]{0x16151a,0x8e2a2a,0x2a2a2f,Keep,0x7d2f2f},0xa3a69a,
                Body(50,0,45,0,15,20,0,0,0,20,60),new Build{Arm=.97f,Forearm=.97f,Thigh=.97f,Calf=.97f},.4f,.48f,.45f,false,0xe9ece4,decay:"cheek-r,forearm-l",blood:0.55f),
            V("runner-male-b",NativeHordeKinds.Runner,false,None,"z-track-jacket|hard-jacket","z-joggers|baggy-pants","bare","sneakers",new[]{Keep,0x253050,0x2c2c30,Keep,0xd8d8d8},0x6e6458,
                Body(55,0,40,0,15,25,0,0,0,30,55),new Build{Arm=.97f,Forearm=.97f},.4f,.4f,.39f,false,0xe9ece4,decay:"ribs-l,shin-l",blood:0.55f),
            V("runner-female-a",NativeHordeKinds.Runner,true,"fringe-tail","z-zip-vest|tank","z-capri-leggings|skinny-jeans","bare","sneakers",new[]{0x141318,0x6e2433,0x232326,Keep,0x7a2e3a},0xa6a99d,
                Body(-10,0,35,0,30,5,15,20,0,20,60),new Build{Arm=.94f,Forearm=.94f,Shoulder=.97f},.35f,.48f,.45f,false,0xe9ece4,decay:"upperarm-l,ribs-r",blood:0.55f),
            V("runner-female-b",NativeHordeKinds.Runner,true,"side-swept","hoodie","z-joggers|baggy-pants","bare","sneakers",new[]{0x6a2f22,0x4f5a3a,0x252528,Keep,0x5b5f4a},0xa9ab9e,
                Body(-10,0,25,0,30,5,15,20,0,25,55),new Build{Arm=.95f,Forearm=.95f,Shoulder=.97f},.4f,.48f,.45f,false,0xe9ece4,decay:"forearm-r,thigh-r",blood:0.55f),
            // ---- Spitter: gaunt and hunched, the throat sac ----
            V("spitter-male-a",NativeHordeKinds.Spitter,false,None,"z-work-shirt|tee","khakis","bare","lace-ups",new[]{Keep,0x4e7480,0x2f3034,Keep,0x3a3028},0xb0b39f,
                Body(30,0,0,0,0,0,0,0,0,85,75),new Build{Arm=.86f,Forearm=.86f,Thigh=.9f,Calf=.9f,Shoulder=.96f},.6f,.56f,.75f,true,0xe7e6d2,decay:"ribs-r,cheek-l,forearm-l",blood:0.45f),
            V("spitter-male-b",NativeHordeKinds.Spitter,false,"wild","z-utility-jacket|hard-jacket","baggy-pants","bare","work-boots",new[]{0x8c8c88,0x56603e,0x5a4433,Keep,0x3a2f26},0xaeb09c,
                Body(30,0,0,0,0,0,0,0,0,90,70),new Build{Arm=.87f,Forearm=.87f,Thigh=.9f,Calf=.9f,Shoulder=.96f},.6f,.62f,.75f,true,0xe7e6d2,decay:"back,upperarm-l,shin-r",blood:0.45f),
            V("spitter-female-a",NativeHordeKinds.Spitter,true,"side-swept","z-blouse|tee","skinny-jeans","bare","z-flats|lace-ups",new[]{0x1c1a20,0x6a4f80,0x2c2c30,Keep,0x2a2a2c},0xb1b2a0,
                Body(-15,0,0,0,15,0,5,10,0,60,70),new Build{Arm=.85f,Forearm=.85f,Thigh=.9f,Calf=.9f,Shoulder=.95f},.55f,.56f,.72f,true,0xe7e6d2,decay:"cheek-r,ribs-l",blood:0.45f),
            V("spitter-female-b",NativeHordeKinds.Spitter,true,"tied-back","z-scrubs-top|tee","z-scrub-pants|khakis","bare","sneakers",new[]{0x9a9894,0x7e9cc8,0x2f4470,Keep,0xe0e0e0},0xb3b3a2,
                Body(-15,5,0,0,20,0,5,10,0,90,65),new Build{Arm=.86f,Forearm=.86f,Thigh=.9f,Calf=.9f,Shoulder=.95f},.5f,.48f,.72f,true,0xe7e6d2,decay:"forearm-r,ribs-r,shin-l",blood:0.45f),
            // ---- Brute: heavy, broad and muscled, workwear ----
            V("brute-male-a",NativeHordeKinds.Brute,false,"wild","tank","z-overalls|baggy-pants","bare","work-boots",new[]{0x18171b,0x2c2d30,0xb8892e,Keep,0x4a3423},0x9ea595,
                Body(100,40,100,20,40,100,0,0,10,45),new Build{Arm=1.22f,Forearm=1.25f,Thigh=1.12f,Calf=1.12f,Hand=1.18f,Shoulder=1.08f,Head=1.02f},.6f,.56f,.59f,false,0xe9ece4,decay:"forearm-l,ribs-r",blood:0.6f),
            V("brute-male-b",NativeHordeKinds.Brute,false,None,"z-work-shirt|tee","z-cargo-pants|baggy-pants","bare","work-boots",new[]{Keep,0xa8492e,0x4a3a2c,Keep,0x3f2f22},0x6b6256,
                Body(90,100,55,90,30,100,0,0,40,55),new Build{Arm=1.2f,Forearm=1.22f,Thigh=1.15f,Calf=1.12f,Hand=1.16f,Shoulder=1.06f,Head=1.02f},.6f,.48f,.52f,false,0xe9ece4,decay:"cheek-l,upperarm-r",blood:0.6f),
            V("brute-female-a",NativeHordeKinds.Brute,true,"side-swept","tee","z-overalls|baggy-pants","bare","work-boots",new[]{0x1a1a1e,0xd9cfb4,0x4c5536,Keep,0x4a3a2a},0xa0a697,
                Body(15,40,85,10,40,70,15,25,10,40),new Build{Arm=1.18f,Forearm=1.2f,Thigh=1.12f,Calf=1.1f,Hand=1.12f,Shoulder=1.05f},.55f,.56f,.59f,false,0xe9ece4,decay:"forearm-r,thigh-l",blood:0.6f),
            V("brute-female-b",NativeHordeKinds.Brute,true,"wild","z-denim-vest|sweater","z-cargo-pants|baggy-pants","bare","work-boots",new[]{0xcfcfcf,0x6a2a2e,0x5c5e62,Keep,0x3a2f26},0xa3a597,
                Body(15,45,80,20,45,70,15,25,15,70),new Build{Arm=1.18f,Forearm=1.2f,Thigh=1.12f,Calf=1.1f,Hand=1.12f,Shoulder=1.05f},.55f,.56f,.59f,false,0xe9ece4,decay:"ribs-l,upperarm-l",blood:0.6f),
            // ---- Hulk: the mutant giant (drawn at 1.8x): a small head sunk between boulder shoulders, a barrel chest, long swollen
            // arms with club forearms and huge fists on small legs, every muscle defined, veins, torn skin, bone spurs and pustules
            // (Core NativeZombieMutation); pale grey-pink. The grown flesh is the whole upper body: none wears a top (a woman's top
            // colour is her torn chest wrap, painted on the flesh), and the decay opens where no flesh grows (the face, the legs) ----
            V("hulk-male-a",NativeHordeKinds.Hulk,false,None,None,"z-cargo-pants|baggy-pants","bare","work-boots",new[]{Keep,Keep,0x2a3047,Keep,0x1f1f22},0xb7a7a2,
                Body(100,30,100,0,100,100,0,0,10,60,40),new Build{Arm=1.75f,Forearm=2f,Thigh=1.3f,Calf=1.25f,Hand=2f,Shoulder=1.42f,Head=.82f,Right=1.08f,Neck=.6f,Reach=1.3f},.6f,.62f,.7f,false,0xe9ece4,decay:"cheek-l,thigh-r",blood:0.55f,mutant:true),
            V("hulk-male-b",NativeHordeKinds.Hulk,false,"spiky",None,"z-cargo-pants|baggy-pants","bare","work-boots",new[]{0xd8d6d0,Keep,0x7a2f2b,Keep,0x1f1f22},0xab9e98,
                Body(100,35,100,10,100,100,0,0,10,65,40),new Build{Arm=1.75f,Forearm=2f,Thigh=1.3f,Calf=1.25f,Hand=2f,Shoulder=1.42f,Head=.82f,Right=1.08f,Neck=.6f,Reach=1.3f},.6f,.62f,.7f,false,0xe9ece4,decay:"cheek-l,shin-r",blood:0.55f,mutant:true),
            V("hulk-female-a",NativeHordeKinds.Hulk,true,"spiky",None,"z-cargo-pants|baggy-pants","bare","work-boots",new[]{0x1d1b1f,0x2b3552,0x2a3047,Keep,0x1f1f22},0xb3a4a3,
                Body(65,30,100,0,70,100,0,30,10,50,45),new Build{Arm=1.62f,Forearm=1.85f,Thigh=1.28f,Calf=1.22f,Hand=1.85f,Shoulder=1.36f,Head=.85f,Right=1.06f,Neck=.65f,Reach=1.26f},.6f,.62f,.7f,false,0xe9ece4,decay:"cheek-r,thigh-l",blood:0.55f,mutant:true),
            V("hulk-female-b",NativeHordeKinds.Hulk,true,"wild",None,"baggy-pants","bare","work-boots",new[]{0xa9a9a6,0x6b2a2c,0x28282b,Keep,0x5a3d28},0xaaa39c,
                Body(65,35,100,10,70,100,0,30,10,70,45),new Build{Arm=1.62f,Forearm=1.85f,Thigh=1.28f,Calf=1.22f,Hand=1.85f,Shoulder=1.36f,Head=.85f,Right=1.06f,Neck=.65f,Reach=1.26f},.6f,.62f,.7f,false,0xe9ece4,decay:"cheek-r,shin-l",blood:0.55f,mutant:true),
            // ---- Central Station (owner 2026-11-03: the instance kinds off voxels; docs/EXPEDITION-ENEMY-REWORK.md): each on its kind's
            // base build at the kind's scale, its signature gear riding the bones (Runtime NativeZombieGear) ----
            // Linesman: the brute's body in hi-vis workwear, red insulated gloves, pale skin gone bluish, cold blue eyes.
            V("linesman-male-a",LinesmanKind,false,None,"z-work-jacket|hard-jacket","belted-pants","gloves","work-boots",new[]{Keep,0xFF6A10,0x2A3247,0xB8341C,0x3B2E24},0x9aa3a6,
                Body(100,30,90,15,40,100,0,0,10,50),new Build{Arm=1.2f,Forearm=1.22f,Thigh=1.12f,Calf=1.12f,Hand=1.16f,Shoulder=1.07f,Head=1.02f},.4f,.38f,.48f,false,NativeStationKinds.LinesmanBlue,decay:"cheek-l,forearm-l",blood:0.22f),
            V("linesman-male-b",LinesmanKind,false,None,"z-utility-jacket|hard-jacket","khakis","gloves","work-boots",new[]{Keep,0xE8D21A,0x3E4436,0xB8341C,0x2E241C},0x98a0a4,
                Body(90,70,60,60,30,100,0,0,30,60),new Build{Arm=1.18f,Forearm=1.2f,Thigh=1.14f,Calf=1.12f,Hand=1.15f,Shoulder=1.05f,Head=1.02f},.42f,.36f,.45f,false,NativeStationKinds.LinesmanBlue,decay:"ribs-r,shin-l",blood:0.22f),
            V("linesman-female-a",LinesmanKind,true,None,"z-work-jacket|hard-jacket","belted-pants","gloves","work-boots",new[]{Keep,0xFF6A10,0x2A3247,0xB8341C,0x3B2E24},0x9ca5a8,
                Body(15,35,80,10,40,70,15,25,10,45),new Build{Arm=1.15f,Forearm=1.18f,Thigh=1.1f,Calf=1.08f,Hand=1.1f,Shoulder=1.04f},.38f,.38f,.46f,false,NativeStationKinds.LinesmanBlue,decay:"forearm-r,thigh-l",blood:0.22f),
            V("linesman-female-b",LinesmanKind,true,None,"z-utility-jacket|hard-jacket","khakis","gloves","work-boots",new[]{Keep,0xE8D21A,0x3E4436,0xB8341C,0x2E241C},0x9aa2a6,
                Body(15,45,75,20,45,70,15,25,15,60),new Build{Arm=1.15f,Forearm=1.18f,Thigh=1.1f,Calf=1.08f,Hand=1.1f,Shoulder=1.04f},.4f,.36f,.45f,false,NativeStationKinds.LinesmanBlue,decay:"upperarm-l,ribs-l",blood:0.22f),
            // Conductor: tall and thin, old, in the long navy coat to the knees, white gloves, grey hair under the cap, cold white eyes.
            V("conductor-male-a",ConductorKind,false,"z-military-cap|side-swept","z-suit-jacket|hard-jacket","z-dress-pants|belted-pants","gloves","z-loafers|lace-ups",new[]{0x8a8a88,0x1C2230,0x12161F,0xE8E6DE,0x15151A},0xaab0aa,
                Body(50,0,10,0,10,10,0,0,0,95,70),new Build{Arm=.94f,Forearm=.94f,Thigh=.95f,Calf=.95f,Shoulder=.97f},.5f,.35f,.5f,false,NativeStationKinds.ConductorWhite,decay:"cheek-r,forearm-l",blood:0.3f),
            V("conductor-male-b",ConductorKind,false,"z-military-cap|side-swept","z-suit-jacket|hard-jacket","z-dress-pants|belted-pants","gloves","z-loafers|lace-ups",new[]{Keep,0x1C2230,0x12161F,0xE8E6DE,0x15151A},0xa7ada6,
                Body(55,25,5,20,10,20,0,0,10,100,60),new Build{Arm=.95f,Forearm=.95f,Thigh=.96f,Calf=.96f,Shoulder=.98f},.55f,.35f,.52f,false,NativeStationKinds.ConductorWhite,decay:"ribs-l,shin-r",blood:0.3f),
            V("conductor-female-a",ConductorKind,true,"z-military-cap|tied-back","z-suit-jacket|hard-jacket","z-dress-pants|belted-pants","gloves","z-loafers|lace-ups",new[]{0x9a9894,0x1C2230,0x12161F,0xE8E6DE,0x15151A},0xacb1ab,
                Body(-15,0,5,0,20,5,10,10,0,90,65),new Build{Arm=.92f,Forearm=.92f,Thigh=.94f,Calf=.94f,Shoulder=.95f},.5f,.35f,.5f,false,NativeStationKinds.ConductorWhite,decay:"forearm-r,cheek-l",blood:0.3f),
            V("conductor-female-b",ConductorKind,true,"z-military-cap|side-swept","z-suit-jacket|hard-jacket","z-dress-pants|belted-pants","gloves","z-loafers|lace-ups",new[]{0x6e6c6a,0x1C2230,0x12161F,0xE8E6DE,0x15151A},0xa9aea8,
                Body(-10,20,5,10,25,10,15,15,10,100,55),new Build{Arm=.93f,Forearm=.93f,Thigh=.95f,Calf=.95f,Shoulder=.96f},.52f,.35f,.52f,false,NativeStationKinds.ConductorWhite,decay:"upperarm-r,ribs-r",blood:0.3f),
            // Undertow: the runner's body drowned in the flooded lines, a torn glossy slicker, grey-green waterlogged skin, aqua eyes.
            V("undertow-male-a",UndertowKind,false,"wild","hard-jacket","skinny-jeans","bare","sneakers",new[]{0x1A1F1C,0x3E5C52,0x23302A,Keep,0x2A2E2A},0x5e6e62,
                Body(50,0,35,0,10,20,0,0,0,30,60),new Build{Arm=.98f,Forearm=.98f},.72f,.6f,.82f,false,NativeStationKinds.UndertowAqua,decay:"cheek-l,ribs-r,forearm-r",blood:0.15f),
            V("undertow-male-b",UndertowKind,false,None,"hard-jacket","baggy-pants","bare","work-boots",new[]{Keep,0x4E5C3C,0x2A2E30,Keep,0x24221E},0x596a5f,
                Body(55,10,30,10,10,25,0,0,0,40,55),new Build{Arm=.98f,Forearm=.98f},.72f,.62f,.85f,false,NativeStationKinds.UndertowAqua,decay:"back,shin-l,upperarm-l",blood:0.15f),
            V("undertow-female-a",UndertowKind,true,"side-swept","hard-jacket","skinny-jeans","bare","sneakers",new[]{0x1A1F1C,0x3E5C52,0x23302A,Keep,0x2A2E2A},0x5f6f63,
                Body(-10,0,25,0,30,5,15,20,0,30,60),new Build{Arm=.95f,Forearm=.95f,Shoulder=.97f},.72f,.6f,.82f,false,NativeStationKinds.UndertowAqua,decay:"forearm-l,ribs-l",blood:0.15f),
            V("undertow-female-b",UndertowKind,true,"tied-back","hard-jacket","baggy-pants","bare","sneakers",new[]{0x14181a,0x4E5C3C,0x2A2E30,Keep,0x2A2E2A},0x5a6a60,
                Body(-10,10,20,0,30,5,15,20,0,35,55),new Build{Arm=.95f,Forearm=.95f,Shoulder=.97f},.72f,.62f,.85f,false,NativeStationKinds.UndertowAqua,decay:"cheek-r,thigh-r",blood:0.15f),
            // Pileup: the brute's frame drawn wide, three commuters fused (the second head and arm grown out of the right shoulder:
            // NativeZombieGear's fused growth), torn office clothes.
            V("pileup-male-a",PileupKind,false,"wild","z-suit-jacket|hard-jacket","z-dress-pants|khakis","bare","z-loafers|lace-ups",new[]{0x2a2420,0x9C9A92,0x4A4D55,Keep,0x1c1a18},0x9a9488,
                Body(100,70,70,60,40,100,0,0,30,55),new Build{Arm=1.22f,Forearm=1.24f,Thigh=1.15f,Calf=1.12f,Hand=1.15f,Shoulder=1.12f,Head=1.02f},.7f,.6f,.62f,false,0xe9ece4,decay:"ribs-l,cheek-r,forearm-l",blood:0.7f),
            V("pileup-male-b",PileupKind,false,"z-ball-cap|wild","z-work-shirt|tee","z-dress-pants|belted-pants","bare","z-loafers|lace-ups",new[]{Keep,0x5A4A3A,0x7B6D58,Keep,0x2a221c},0x968e84,
                Body(95,90,50,80,35,100,0,0,40,65),new Build{Arm=1.2f,Forearm=1.22f,Thigh=1.16f,Calf=1.12f,Hand=1.14f,Shoulder=1.1f,Head=1.02f},.72f,.62f,.6f,false,0xe9ece4,decay:"back,shin-r,upperarm-l",blood:0.7f),
            V("pileup-female-a",PileupKind,true,"side-swept","z-blouse|tee","z-dress-pants|khakis","bare","z-flats|lace-ups",new[]{0x1f1c1a,0xC9C2B0,0x4A4D55,Keep,0x1c1a18},0x9c958a,
                Body(20,60,70,40,45,80,15,25,25,50),new Build{Arm=1.18f,Forearm=1.2f,Thigh=1.12f,Calf=1.1f,Hand=1.1f,Shoulder=1.08f},.7f,.6f,.62f,false,0xe9ece4,decay:"ribs-r,forearm-r",blood:0.7f),
            V("pileup-female-b",PileupKind,true,"tied-back","z-suit-jacket|sweater","z-dress-pants|belted-pants","bare","z-loafers|lace-ups",new[]{0x4a3a2c,0x3f6a6a,0x7B6D58,Keep,0x2a221c},0x9a9389,
                Body(20,75,60,55,45,80,15,25,30,60),new Build{Arm=1.18f,Forearm=1.2f,Thigh=1.12f,Calf=1.1f,Hand=1.1f,Shoulder=1.08f},.72f,.62f,.6f,false,0xe9ece4,decay:"cheek-l,thigh-l",blood:0.7f),
            // ---- the Backrooms (docs/EXPEDITION-ENEMY-REWORK.md section 4) ----
            // Faceling: office workers yellowed by the damp, no face.
            V("faceling-male-a",FacelingKind,false,"side-swept","z-work-shirt|tee","z-dress-pants|khakis","bare","z-loafers|lace-ups",new[]{0x3A3028,0xD9CDA2,0x4E4A42,Keep,0x221C18},0xBEB4A2,
                Body(70,35,25,30,20,50,0,0,30,55),new Build(),.6f,.38f,.4f,false,0xE8E2D0,decay:"forearm-l,thigh-r",blood:.12f),
            V("faceling-male-b",FacelingKind,false,"spiky","z-sweater-vest|sweater","khakis","bare","z-loafers|lace-ups",new[]{0x2A221C,0x8C7A4A,0x9A8C6C,Keep,0x2A2018},0xB9B0A0,
                Body(80,60,20,60,20,55,0,0,50,60),new Build{Arm=1.05f,Thigh=1.05f},.62f,.4f,.42f,false,0xE8E2D0,decay:"ribs-l,shin-r",blood:.12f),
            V("faceling-female-a",FacelingKind,true,"tied-back","z-blouse|tee","z-dress-pants|khakis","bare","z-heels|lace-ups",new[]{0x4A3424,0xE2D9BE,0x3C3C44,Keep,0x2A2226},0xC0B7A6,
                Body(-20,30,20,20,40,40,20,20,30,45),new Build(),.58f,.38f,.4f,false,0xE8E2D0,decay:"upperarm-r,thigh-l",blood:.12f),
            V("faceling-female-b",FacelingKind,true,"fringe-tail","z-sweater-vest|sweater","khakis","bare","z-flats|lace-ups",new[]{0x6A5038,0x6E6E5C,0x8A7E66,Keep,0x3A3028},0xBBB2A2,
                Body(-10,45,15,35,40,40,25,25,40,55),new Build(),.6f,.4f,.42f,false,0xE8E2D0,decay:"forearm-r,shin-l",blood:.12f),
            // Hound: gaunt and hairless, in rags (on all fours: the gait's own, NativeHordeKinds' hound).
            V("hound-male-a",HoundKind,false,None,"tank","z-shorts|cuffed-shorts","bare","z-bare|sneakers",new[]{Keep,0x4A4642,0x3A3632,Keep,0x2A2622},0xA89C94,
                Body(60,0,40,0,0,20,0,0,0,40),new Build{Arm=.9f,Forearm=.9f,Thigh=.9f,Calf=.9f,Hand=1.25f,Head=.95f},.8f,.6f,.7f,false,0xF0F0E8,decay:"ribs-r,thigh-l",blood:.5f),
            V("hound-male-b",HoundKind,false,None,"tank","z-shorts|cuffed-shorts","bare","z-bare|sneakers",new[]{Keep,0x5A4E44,0x2E2A28,Keep,0x2A2622},0x9E948E,
                Body(50,0,30,0,0,15,0,0,0,50),new Build{Arm=.88f,Forearm=.88f,Thigh=.9f,Calf=.88f,Hand=1.25f,Head=.95f},.82f,.62f,.72f,false,0xF0F0E8,decay:"back,forearm-r",blood:.55f),
            V("hound-female-a",HoundKind,true,None,"tank","z-shorts|cuffed-shorts","bare","z-bare|sneakers",new[]{Keep,0x48443E,0x3A3430,Keep,0x2A2622},0xA69A94,
                Body(-40,0,30,0,10,15,0,0,0,40),new Build{Arm=.86f,Forearm=.86f,Thigh=.88f,Calf=.86f,Hand=1.2f,Head=.95f},.8f,.6f,.7f,false,0xF0F0E8,decay:"thigh-l,upperarm-r",blood:.5f),
            V("hound-female-b",HoundKind,true,None,"tank","z-shorts|cuffed-shorts","bare","z-bare|sneakers",new[]{Keep,0x524840,0x302C28,Keep,0x2A2622},0x9C928C,
                Body(-30,0,25,0,10,15,0,0,0,50),new Build{Arm=.86f,Forearm=.86f,Thigh=.88f,Calf=.86f,Hand=1.2f,Head=.95f},.82f,.62f,.72f,false,0xF0F0E8,decay:"shin-l,ribs-l",blood:.55f),
            // Drowned: bloated bathers in striped vintage costumes, rubber caps and goggles (their gear), waterlogged pallor.
            V("drowned-male-a",DrownedKind,false,None,"tank","z-dolphin-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{Keep,0x2A4A7A,0x2A4A7A,Keep,0x3A6A8A},0xC6CBC2,
                Body(90,90,20,85,40,70,0,30,80,60),new Build{Arm=1.15f,Forearm=1.1f,Thigh=1.2f,Calf=1.15f,Hand=1.1f},.5f,.4f,.75f,false,0xD8E4E0,decay:"cheek-l,thigh-r",blood:.1f),
            V("drowned-male-b",DrownedKind,false,None,"tank","z-dolphin-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{Keep,0x7A2E2E,0x7A2E2E,Keep,0x2A2A2A},0xC2C8C0,
                Body(85,100,10,100,30,80,0,40,90,65),new Build{Arm=1.2f,Forearm=1.15f,Thigh=1.25f,Calf=1.2f,Hand=1.12f},.52f,.42f,.78f,false,0xD8E4E0,decay:"forearm-r,shin-l",blood:.1f),
            V("drowned-female-a",DrownedKind,true,None,"z-tube-top|tank","z-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{Keep,0x2E5A4A,0x2E5A4A,Keep,0xA03A3A},0xC8CCC4,
                Body(-10,80,10,60,60,50,40,50,60,55),new Build{Arm=1.12f,Forearm=1.08f,Thigh=1.2f,Calf=1.12f},.5f,.4f,.75f,false,0xD8E4E0,decay:"thigh-r,forearm-l",blood:.1f),
            V("drowned-female-b",DrownedKind,true,None,"tank","z-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{Keep,0x1E2E5A,0x1E2E5A,Keep,0x2A2A2A},0xC4C9C2,
                Body(-20,90,10,80,55,55,40,55,70,60),new Build{Arm=1.15f,Forearm=1.1f,Thigh=1.22f,Calf=1.15f},.52f,.42f,.78f,false,0xD8E4E0,decay:"ribs-l,shin-r",blood:.1f),
            // Lifeguard: tall and thin (the kind's 2.6 m), faded reds, sun-bleached, its whistle and hook (gear).
            V("lifeguard-male-a",LifeguardKind,false,"z-bucket-hat|spiky","tee","z-dolphin-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{0xB84A3E,0xC0453A,0xB03A30,Keep,0x2A2A2A},0xB0A493,
                Body(85,0,40,0,20,40,0,0,0,50),new Build{Hand=1.15f,Arm=.9f,Forearm=.9f,Thigh=.9f,Calf=.9f},.55f,.42f,.55f,false,0xE8E2D0,decay:"cheek-r,thigh-l",blood:.2f),
            V("lifeguard-male-b",LifeguardKind,false,"spiky","tank","z-dolphin-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{0xE6D6A0,0xB8443A,0xB03A30,Keep,0x2A2A2A},0xAC9F90,
                Body(80,0,50,0,20,40,0,0,0,55),new Build{Hand=1.15f,Arm=.92f,Forearm=.9f,Thigh=.9f,Calf=.9f},.55f,.42f,.55f,false,0xE8E2D0,decay:"shin-r,forearm-l",blood:.2f),
            V("lifeguard-female-a",LifeguardKind,true,"z-sun-hat|tied-back","tee","z-dolphin-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{0xD8C890,0xC0453A,0xB03A30,Keep,0x2A2A2A},0xB2A696,
                Body(-20,0,30,0,30,30,10,10,0,45),new Build{Hand=1.12f,Arm=.9f,Forearm=.88f,Thigh=.9f,Calf=.88f},.55f,.42f,.55f,false,0xE8E2D0,decay:"forearm-l,ribs-r",blood:.2f),
            V("lifeguard-female-b",LifeguardKind,true,"fringe-tail","tank","z-dolphin-shorts|cuffed-shorts","bare","z-flip-flops|sneakers",new[]{0xE6D6A0,0xB8443A,0xB03A30,Keep,0x2A2A2A},0xAEA294,
                Body(-30,0,30,0,30,30,10,10,0,50),new Build{Hand=1.12f,Arm=.9f,Forearm=.88f,Thigh=.9f,Calf=.88f},.55f,.42f,.55f,false,0xE8E2D0,decay:"thigh-l,upperarm-r",blood:.2f),
        };

        // The zone kinds' kit (docs/EXPEDITION-ENEMY-REWORK.md section 2: their signature gear modelled in Blender):
        //  - Linesman: the hi-vis jacket's tape, a hard hat with its headlamp (yellow, or white on the B looks), the battery pack
        //    on its harness (fitted to each build's back), the jumper cables coiled round the right forearm;
        //  - Conductor: the cap's brass badge, the whistle on its chain;
        //  - Undertow: kelp and weed draped over its shoulders with a drowned cable, weed wound round its forearms;
        //  - Pileup: a carriage grab pole bent across its chest (its door is its carried prop: Runtime StationGearProp);
        //  - the Backrooms': the Faceling's skin-toned mask over its face and its lanyard, the Drowned's rubber cap and goggles
        //    over a striped costume, the Lifeguard's whistle and its rescue hook in hand (the Smiler is no person: its own
        //    apparition, Core NativeSmilerLook).
        /// <summary>Where the gear's lights are, in their bones' frames (tools/gear/station_gear.py builds them there): the
        /// Linesman's headlamp lens on `head` and its aim, the battery pack's vents on `spine_05` (the brute's and the women's
        /// fits). The station's effects (its lamp's halo and beam, the pack's glow) are drawn from them on the model.</summary>
        public static readonly System.Numerics.Vector3 LinesmanLamp=new System.Numerics.Vector3(0,.1453f,.1944f),LinesmanLampAim=new System.Numerics.Vector3(0,-.139f,.990f),
            LinesmanVents=new System.Numerics.Vector3(.068f,.2715f,0),LinesmanVentsWoman=new System.Numerics.Vector3(.068f,.2515f,0);
        /// <summary>The kit given (the table above first: field initializers run in order).</summary>
        public static readonly bool Kitted=Kit();
        private static bool Kit()
        {
            foreach(var v in All)
            {
                bool b=v.Id.EndsWith("-b",StringComparison.Ordinal);
                if(v.Kind==LinesmanKind){v.Tape=true;v.Gear=new[]{b?"linesman-hardhat-white":"linesman-hardhat-yellow",v.Female?"linesman-pack-f":"linesman-pack","linesman-coil"};}
                else if(v.Kind==ConductorKind)v.Gear=new[]{"conductor-badge","conductor-whistle"};
                else if(v.Kind==UndertowKind)v.Gear=new[]{"undertow-weed-back","undertow-weed-arm-l","undertow-weed-arm-r"};
                else if(v.Kind==FacelingKind){v.Faceless=true;v.Upright=.45f;v.Gear=new[]{"faceling-mask-skin","faceling-lanyard"};}
                else if(v.Kind==DrownedKind){v.Stripes=0xE8E4D8;v.Upright=.35f;v.Gear=new[]{b?"drowned-cap-red":"drowned-cap-white","drowned-goggles"};}
                else if(v.Kind==LifeguardKind){v.Upright=.75f;v.Gear=new[]{"lifeguard-whistle"};v.Carry="lifeguard-hook";}
                else if(v.Kind==PileupKind){v.Fused=true;v.Gear=new[]{v.Female?"pileup-handrail-f":"pileup-handrail",v.Female?"pileup-flesh-f":"pileup-flesh"};}
            }
            return true;
        }

        /// <summary>The kinds drawn on the model in shipped builds (the owner's order: one male and one female shambler end to end first,
        /// then the other kinds once the pipeline is reviewed). All five are live since the PC captures of 2026-10-29 (each kind's
        /// variants beside the player, its blow at the authority's contact time, the spitter's sac through a spit); the crawler and
        /// bloater have no variants and keep their voxel models.</summary>
        public static readonly bool[] Live={true,true,true,true,false,false,true};
        /// <summary>Every kind with variants drawn on the model (the owner's review of the drafts: the local tool, or --ca-zombie-drafts).</summary>
        public static bool Drafts;
        /// <summary>Whether a kind has variants on the model.</summary>
        public static bool Has(int kind){foreach(var v in All)if(v.Kind==kind)return true;return false;}
        /// <summary>The zone kinds drawn on the model in shipped builds, each once it passed its review (docs/EXPEDITION-ENEMY-REWORK.md
        /// section 2): the Central Station's four since 2026-11-03 (turnarounds, close-ups of their gear, their moves at the
        /// authority's timings on the PC); the Backrooms' Faceling, Drowned and Lifeguard the same day (their looks and moves on the
        /// PC; the Hound stays a draft until it runs on all fours; the Smiler is its own apparition, not on the model); drafts
        /// draw them all.</summary>
        public static readonly System.Collections.Generic.HashSet<int> ZoneLive=new System.Collections.Generic.HashSet<int>{LinesmanKind,ConductorKind,UndertowKind,PileupKind,FacelingKind,DrownedKind,LifeguardKind};
        /// <summary>Whether adds of `kind` draw on the model now.</summary>
        public static bool Modelled(int kind)=>kind>=0&&(kind<NativeHordeKinds.Count?Live[kind]||Drafts:NativeHordeZones.Of(kind)!=null&&(ZoneLive.Contains(kind)||Drafts))&&Has(kind);
        /// <summary>The variant an add of `kind` wears, by its id (stable for its life, the same on every peer); null for a kind
        /// without variants. Allocation free.</summary>
        public static Variant Of(int kind,int id)
        {
            int pick=Math.Min(Variants-1,(int)(NativeHordeKinds.Hash(NativeHordeKinds.Seed(id),23)*Variants)),n=0;
            foreach(var v in All)if(v.Kind==kind){if(n==pick)return v;n++;}
            return null;
        }
        /// <summary>Whether a mesh's blendshape `name` is the shape `want` ("Shape_Muscle", "Body.Muscle", "Muscle"; any case), never a
        /// tag or an expression shape.</summary>
        public static bool ShapeMatches(string name,string want)
        {
            if(string.IsNullOrEmpty(name)||string.IsNullOrEmpty(want)||name.IndexOf("Tag_",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Expression",StringComparison.OrdinalIgnoreCase)>=0)return false;
            if(!name.EndsWith(want,StringComparison.OrdinalIgnoreCase))return false;
            if(name.Length==want.Length)return true;char c=name[name.Length-want.Length-1];return c=='_'||c=='.'||c==' ';
        }
        /// <summary>The candidate ids of a slot entry, best first (allocates: once per body made).</summary>
        public static string[] Candidates(string entry)=>string.IsNullOrEmpty(entry)?Array.Empty<string>():entry.Split('|');
    }
}

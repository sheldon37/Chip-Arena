using System;

namespace ChipArena.UnityConversion
{
    // The zombies on the player's model (owner 2026-10-29: "Implement the attached zombie designs in Chip Arena using the existing
    // low-poly player model as the starting point", "Shambler, Runner, Spitter, Brute, and Hulk, with two male and two female visual
    // variants per type"; docs/ZOMBIE-MODELS.md). Each variant is the bake's one model (Runtime/NativeZombieModelPresentation.cs)
    // dressed and shaped from this table: a piece per wardrobe slot (Core/NativeLook's slots; the first candidate the bake carries,
    // the last always one of today's pieces, so every install dresses it), each piece's colour, the skin, the blank eyes, the body
    // and face blendshapes by name, the bone build (limb girth on the twist bones, hand and head size, shoulder width), the wear
    // (grime, torn patches, rotting skin) and the spitter's throat sac. Shared meshes, rig, materials and motion: a variant is data.
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
            /// the shoulders' span (the upper arm set out along the collarbone).</summary>
            public float Arm=1,Forearm=1,Thigh=1,Calf=1,Hand=1,Head=1,Shoulder=1;
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
            /// <summary>Grime on the clothes, torn patches, rot on the skin (0..1).</summary>
            public float Grime,Tears,Rot;
            /// <summary>The spitter's throat sac (Runtime: a skinned sac on the neck that swells with the spit).</summary>
            public bool Sac;
        }

        private static float[] Body(float type,float weight,float muscle,float belly,float chest,float neck,float curvy,float butt,float waist,float maturity=float.NaN,float sharp=float.NaN,float round=float.NaN)
            =>new[]{type,weight,muscle,belly,chest,neck,curvy,butt,waist,maturity,sharp,round};
        private static Variant V(string id,int kind,bool female,string hair,string upper,string lower,string hands,string feet,int[] colours,int skin,float[] shapes,Build build,float grime,float tears,float rot,bool sac=false,int eyes=0xe9ece4)
            =>new Variant{Id=id,Kind=kind,Female=female,Pieces=new[]{hair,upper,lower,hands,feet},Colours=colours,Skin=skin,Eyes=eyes,Shapes=shapes,Build=build,Grime=grime,Tears=tears,Rot=rot,Sac=sac};
        private const int Keep=-1;

        /// <summary>Every variant, by kind then male A, male B, female A, female B (the owner's sheets).</summary>
        public static readonly Variant[] All={
            // ---- Shambler: an average build in everyday clothes, the common one ----
            V("shambler-male-a",NativeHordeKinds.Shambler,false,"wild","z-hoodie-vest|open-hoodie","skinny-jeans","bare","sneakers",new[]{0x1c1b20,0x2f3a55,0x3a3d43,Keep,0x2a2a2e},0xa3ab97,
                Body(45,10,15,5,10,30,0,0,0,30),new Build(),.55f,.35f,.45f),
            V("shambler-male-b",NativeHordeKinds.Shambler,false,None,"z-work-jacket|hard-jacket","khakis","bare","work-boots",new[]{Keep,0x6b4a32,0x5a5c5e,Keep,0x3b2e24},0xa7a596,
                Body(55,45,10,45,15,40,0,0,10,65),new Build{Arm=1.03f,Thigh=1.03f},.6f,.3f,.5f),
            V("shambler-female-a",NativeHordeKinds.Shambler,true,"side-swept","hoodie","skinny-jeans","bare","sneakers",new[]{0x1f1d22,0x5d3043,0x38393d,Keep,0x2e2e33},0xa9ae9f,
                Body(-10,5,10,0,35,10,25,25,0,30),new Build{Shoulder=.97f},.5f,.35f,.4f),
            V("shambler-female-b",NativeHordeKinds.Shambler,true,"tied-back","z-cardigan|sweater","khakis","bare","lace-ups",new[]{0x4b3a2c,0x3f6a6a,0x4d3b2e,Keep,0x3a2f28},0xa8a99b,
                Body(-5,40,5,25,40,15,35,35,10,55),new Build{Shoulder=.98f},.55f,.3f,.45f),
            // ---- Runner: lean and athletic in sportswear ----
            V("runner-male-a",NativeHordeKinds.Runner,false,"spiky","z-hoodie-vest|open-hoodie","z-joggers|baggy-pants","bare","sneakers",new[]{0x16151a,0x8e2a2a,0x2a2a2f,Keep,0x7d2f2f},0xa3a69a,
                Body(50,0,45,0,15,20,0,0,0,20,60),new Build{Arm=.97f,Forearm=.97f,Thigh=.97f,Calf=.97f},.4f,.3f,.35f),
            V("runner-male-b",NativeHordeKinds.Runner,false,None,"z-track-jacket|hard-jacket","z-joggers|baggy-pants","bare","sneakers",new[]{Keep,0x253050,0x2c2c30,Keep,0xd8d8d8},0x6e6458,
                Body(55,0,40,0,15,25,0,0,0,30,55),new Build{Arm=.97f,Forearm=.97f},.4f,.25f,.3f),
            V("runner-female-a",NativeHordeKinds.Runner,true,"fringe-tail","z-zip-vest|tank","z-capri-leggings|skinny-jeans","bare","sneakers",new[]{0x141318,0x6e2433,0x232326,Keep,0x7a2e3a},0xa6a99d,
                Body(-10,0,35,0,30,5,15,20,0,20,60),new Build{Arm=.94f,Forearm=.94f,Shoulder=.97f},.35f,.3f,.35f),
            V("runner-female-b",NativeHordeKinds.Runner,true,"side-swept","hoodie","z-joggers|baggy-pants","bare","sneakers",new[]{0x6a2f22,0x4f5a3a,0x252528,Keep,0x5b5f4a},0xa9ab9e,
                Body(-10,0,25,0,30,5,15,20,0,25,55),new Build{Arm=.95f,Forearm=.95f,Shoulder=.97f},.4f,.3f,.35f),
            // ---- Spitter: gaunt and hunched, the throat sac ----
            V("spitter-male-a",NativeHordeKinds.Spitter,false,None,"z-work-shirt|tee","khakis","bare","lace-ups",new[]{Keep,0x4e7480,0x2f3034,Keep,0x3a3028},0xb0b39f,
                Body(30,0,0,0,0,0,0,0,0,85,75),new Build{Arm=.86f,Forearm=.86f,Thigh=.9f,Calf=.9f,Shoulder=.96f},.6f,.35f,.6f,true,0xe7e6d2),
            V("spitter-male-b",NativeHordeKinds.Spitter,false,"wild","z-utility-jacket|hard-jacket","baggy-pants","bare","work-boots",new[]{0x8c8c88,0x56603e,0x5a4433,Keep,0x3a2f26},0xaeb09c,
                Body(30,0,0,0,0,0,0,0,0,90,70),new Build{Arm=.87f,Forearm=.87f,Thigh=.9f,Calf=.9f,Shoulder=.96f},.6f,.4f,.6f,true,0xe7e6d2),
            V("spitter-female-a",NativeHordeKinds.Spitter,true,"side-swept","z-blouse|tee","skinny-jeans","bare","z-flats|lace-ups",new[]{0x1c1a20,0x6a4f80,0x2c2c30,Keep,0x2a2a2c},0xb1b2a0,
                Body(-15,0,0,0,15,0,5,10,0,60,70),new Build{Arm=.85f,Forearm=.85f,Thigh=.9f,Calf=.9f,Shoulder=.95f},.55f,.35f,.55f,true,0xe7e6d2),
            V("spitter-female-b",NativeHordeKinds.Spitter,true,"tied-back","z-scrubs-top|tee","z-scrub-pants|khakis","bare","sneakers",new[]{0x9a9894,0x7e9cc8,0x2f4470,Keep,0xe0e0e0},0xb3b3a2,
                Body(-15,5,0,0,20,0,5,10,0,90,65),new Build{Arm=.86f,Forearm=.86f,Thigh=.9f,Calf=.9f,Shoulder=.95f},.5f,.3f,.55f,true,0xe7e6d2),
            // ---- Brute: heavy, broad and muscled, workwear ----
            V("brute-male-a",NativeHordeKinds.Brute,false,"wild","tank","z-overalls|baggy-pants","bare","work-boots",new[]{0x18171b,0x2c2d30,0xb8892e,Keep,0x4a3423},0x9ea595,
                Body(100,40,100,20,40,100,0,0,10,45),new Build{Arm=1.22f,Forearm=1.25f,Thigh=1.12f,Calf=1.12f,Hand=1.18f,Shoulder=1.08f,Head=1.02f},.6f,.35f,.45f),
            V("brute-male-b",NativeHordeKinds.Brute,false,None,"z-work-shirt|tee","z-cargo-pants|baggy-pants","bare","work-boots",new[]{Keep,0xa8492e,0x4a3a2c,Keep,0x3f2f22},0x6b6256,
                Body(90,100,55,90,30,100,0,0,40,55),new Build{Arm=1.2f,Forearm=1.22f,Thigh=1.15f,Calf=1.12f,Hand=1.16f,Shoulder=1.06f,Head=1.02f},.6f,.3f,.4f),
            V("brute-female-a",NativeHordeKinds.Brute,true,"side-swept","tee","z-overalls|baggy-pants","bare","work-boots",new[]{0x1a1a1e,0xd9cfb4,0x4c5536,Keep,0x4a3a2a},0xa0a697,
                Body(15,40,85,10,40,70,15,25,10,40),new Build{Arm=1.18f,Forearm=1.2f,Thigh=1.12f,Calf=1.1f,Hand=1.12f,Shoulder=1.05f},.55f,.35f,.45f),
            V("brute-female-b",NativeHordeKinds.Brute,true,"wild","z-denim-vest|sweater","z-cargo-pants|baggy-pants","bare","work-boots",new[]{0xcfcfcf,0x6a2a2e,0x5c5e62,Keep,0x3a2f26},0xa3a597,
                Body(15,45,80,20,45,70,15,25,15,70),new Build{Arm=1.18f,Forearm=1.2f,Thigh=1.12f,Calf=1.1f,Hand=1.12f,Shoulder=1.05f},.55f,.35f,.45f),
            // ---- Hulk: the giant (drawn at 1.8x), gorilla arms and huge forearms ----
            V("hulk-male-a",NativeHordeKinds.Hulk,false,None,"z-denim-vest|open-hoodie","z-cargo-pants|baggy-pants","bare","work-boots",new[]{Keep,0x2b3552,0x2a3047,Keep,0x1f1f22},0x8e8295,
                Body(100,30,100,0,100,100,0,0,10,60,40),new Build{Arm=1.4f,Forearm=1.55f,Thigh=1.2f,Calf=1.2f,Hand=1.45f,Shoulder=1.14f,Head=.96f},.6f,.4f,.5f),
            V("hulk-male-b",NativeHordeKinds.Hulk,false,"spiky","z-denim-vest|open-hoodie","z-cargo-pants|baggy-pants","bare","work-boots",new[]{0xd8d6d0,0xa8822e,0x7a2f2b,Keep,0x1f1f22},0x8d8780,
                Body(100,35,100,10,100,100,0,0,10,65,40),new Build{Arm=1.4f,Forearm=1.55f,Thigh=1.2f,Calf=1.2f,Hand=1.45f,Shoulder=1.14f,Head=.96f},.6f,.4f,.5f),
            V("hulk-female-a",NativeHordeKinds.Hulk,true,"spiky","z-denim-vest|hard-jacket","z-cargo-pants|baggy-pants","bare","work-boots",new[]{0x1d1b1f,0x2b3552,0x2a3047,Keep,0x1f1f22},0x8a7f92,
                Body(35,30,100,0,70,85,10,30,10,50,45),new Build{Arm=1.36f,Forearm=1.5f,Thigh=1.18f,Calf=1.18f,Hand=1.4f,Shoulder=1.12f,Head=.96f},.6f,.4f,.5f),
            V("hulk-female-b",NativeHordeKinds.Hulk,true,"wild","z-denim-vest|open-hoodie","baggy-pants","bare","work-boots",new[]{0xa9a9a6,0x6b2a2c,0x28282b,Keep,0x5a3d28},0x8f978a,
                Body(35,35,100,10,70,85,10,30,10,70,45),new Build{Arm=1.36f,Forearm=1.5f,Thigh=1.18f,Calf=1.18f,Hand=1.4f,Shoulder=1.12f,Head=.96f},.6f,.4f,.5f),
        };

        /// <summary>The kinds drawn on the model in shipped builds (the owner's order: one male and one female shambler end to end first,
        /// then the other kinds once the pipeline is reviewed). The rest keep their voxel models unless <see cref="Drafts"/>.</summary>
        public static readonly bool[] Live={true,false,false,false,false,false,false};
        /// <summary>Every kind with variants drawn on the model (the owner's review of the drafts: the local tool, or --ca-zombie-drafts).</summary>
        public static bool Drafts;
        /// <summary>Whether a kind has variants on the model.</summary>
        public static bool Has(int kind){foreach(var v in All)if(v.Kind==kind)return true;return false;}
        /// <summary>Whether adds of `kind` draw on the model now.</summary>
        public static bool Modelled(int kind)=>kind>=0&&kind<NativeHordeKinds.Count&&(Live[kind]||Drafts)&&Has(kind);
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

using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Central Station's four enemies (docs/EXPEDITION-CENTRAL-STATION.md section 7), sculpted on the same zombie kit as the
    // Siren's minions (Core/NativeZombieModels.cs: the Standard pilot's blocks rotted and torn, the ink veins flickering by cell,
    // every part on the joint the pilot's rides and inside the kind's capsule; Tests/HordeKindChecks and StationEnemyChecks pose
    // and check them). Each has an identity light that reads at 30 m in the dark and none repeats an existing seam hue:
    //  - Linesman (the brute's heavy body): an orange hi-vis vest with two reflective bands, a yellow hard hat with a cracked
    //    headlamp, red insulated gloves, jumper cables coiled round the right forearm with their clamps, the battery pack on his
    //    back glowing blue through its vents (the weak point); blue veins and eyes.
    //  - Conductor (the shambler's, drawn tall and thin): a long dark coat to the knees with brass buttons and lapels, a peaked
    //    cap with a brass band and a black visor, a whistle on a chain, cold white eyes; the red lantern is its own prop
    //    (StationProp.Lantern) hanging from the left hand, so it swings and breaks.
    //  - Undertow (the runner's, waterlogged): grey-green drowned skin, a torn glossy raincoat with its hood, weed and cable
    //    strands hanging off long arms, aqua eyes and veins.
    //  - Pileup (the brute's, drawn wide): three commuters fused, a second head and arm growing out of the right shoulder (the
    //    weak point), torn office clothes, a bent handrail across the chest; the carriage door it holds is its own prop
    //    (StationProp.Door: stainless, its window lit by a dying tube, a third arm reaching out through it).
    // Props are built once with the bank like the bodies (Runtime/NativeStationKindsPresentation.cs queues them).
    public static partial class NativeHordeModels
    {
        // ---- the station's palette (sRGB) ----
        private const int VestHex=NativeStationKinds.HiVis,BandHex=0xE6ECEE,LinesmanHatHex=0xF2C200,HatWhiteHex=0xD8D6CC,GloveHex=0xB8341C,CableHex=0x16161A,CopperHex=0xC07A3A,ClampRedHex=0xC0281E,
            PackHex=0x24292E,CoatHex=0x1C2230,CoatDarkHex=0x12161F,BrassHex=0xC8A040,VisorHex=0x0B0D11,LinenHex=0xC9C2B0,DrownedHex=0x5E6E62,DrownedDarkHex=0x3F4C44,SlickerHex=0x29382F,
            WeedHex=0x1E2B20,SuitGreyHex=0x4A4D55,SuitBeigeHex=0x7B6D58,ShirtHex=0x9C9A92,TieHex=0x6A1C24,StationSteelHex=0x8A9AA0,SteelDarkHex=0x5A666B,RubberHex=0x101214,LiveryHex=0xC0302A;
        private static WeaponPaint P(int hex,float sheen=0)=>WeaponPaint.Solid(hex,sheen);

        /// <summary>Builds a zone kind of the Central Station into `k` (false: not a station kind).</summary>
        private static bool StationBuild(K k)
        {
            switch(NativeStationKinds.Local(k.Kind))
            {
                case NativeStationKinds.Linesman:Linesman(k);return true;case NativeStationKinds.Conductor:Conductor(k);return true;
                case NativeStationKinds.Undertow:Undertow(k);return true;case NativeStationKinds.Pileup:Pileup(k);return true;
            }
            return false;
        }

        // ================================================================================================================
        // Linesman: the third rail walks. The brute's heavy body in a hi-vis vest, hard hat and headlamp, insulated gloves, the
        // jumper cables round the right forearm, the battery pack on his back (the weak point).
        // ================================================================================================================
        private static void Linesman(K k)
        {
            int look=k.Look;k.ReducedBevel=.025f;var vest=P(VestHex,.06f);var band=WeaponPaint.Glow(BandHex,.12f,.95f);var glove=P(GloveHex,.1f);var hat=P(look==0?LinesmanHatHex:HatWhiteHex,.18f);
            // Work trousers and boots on the brute's thick legs; the belt with a tool pouch.
            k.On(CapturedRig.Pelvis);k.Box(Suit,0,-.01f,0,.46f,.23f,.3f,.025f);k.Box(SuitDark,0,.08f,0,.47f,.06f,.31f,.01f);
            if(k.Mid){k.Box(P(0x2A2118),.17f,-.03f,.06f,.1f,.13f,.16f,.012f);k.Box(P(BrassHex,.2f),0,.08f,.157f,.06f,.04f,.012f);k.Hem(-.07f,.22f,.16f,5,.1f,.1f);}
            k.On(CapturedRig.Spine);k.Box(Muscle,0,.08f,.01f,.4f,.2f,.29f,.04f);k.Box(vest,0,.09f,.0f,.42f,.17f,.3f,.02f);
            if(k.Mid)k.Box(band,0,.05f,.0f,.425f,.035f,.305f,.004f);
            // The chest: the vest (open at the front over a torn shirt and the ink-veined chest), two bands round it, the back plate.
            k.On(CapturedRig.Chest);k.Box(Muscle,0,.12f,0,.58f,.32f,.34f,.05f);
            foreach(float s in new[]{-1f,1f}){k.Box(vest,s*.17f,.13f,.01f,.25f,.3f,.355f,.03f);k.Box(Muscle,s*.17f,.31f,-.03f,.2f,.13f,.24f,.04f,0,0,s*-.35f);}
            k.Box(vest,0,.15f,-.05f,.5f,.27f,.26f,.03f);
            if(k.Far)k.Box(band,0,.2f,.0f,.59f,.04f,.37f);
            else foreach(float y in new[]{.05f,.2f}){k.Box(band,-.17f,y,.012f,.255f,.032f,.36f,.004f);k.Box(band,.17f,y,.012f,.255f,.032f,.36f,.004f);if(look==0||y>.1f)k.Box(band,0,y,-.165f,.46f,.032f,.03f,.004f);}
            if(!k.Far){k.Box(Skin,0,.13f,.165f,.1f,.24f,.03f,.01f);k.Box(Wound,.02f,.1f,.177f,.05f,.13f,.01f);}Vein(k,-.01f,.16f,.183f,.13f,.3f,1.2f);Vein(k,.03f,.06f,.182f,.08f,-.4f);
            if(k.Mid){k.Box(Rag,.06f,.22f,.17f,.07f,.07f,.01f,0,0,0,.3f);k.Box(P(0x1A1A1A),-.19f,.24f,.19f,.06f,.08f,.02f);k.Box(WeaponPaint.Glow(0xFF3A22,1.5f,.6f),-.19f,.255f,.2f,.012f,.012f,.008f);}
            // The battery pack on his back: a dark case strapped on, its vents glowing blue (the weak point), terminals and a cable.
            {
                var pack=P(PackHex,.1f);var cell=k.Glow(1,2.4f,NativeStationKinds.LinesmanBlue);
                k.Box(pack,0,.14f,-.25f,.34f,.38f,.15f,.025f);if(k.Far){k.Box(cell,0,.14f,-.327f,.24f,.2f,.01f);goto Packed;}k.Box(P(0x3A4148,.1f),0,.34f,-.25f,.36f,.04f,.17f,.01f);
                for(int i=0;i<3;i++)k.Box(cell,0,.06f+i*.08f,-.327f,.24f,.022f,.01f);
                if(k.Mid){k.Box(cell,-.12f,.14f,-.327f,.03f,.26f,.01f);k.Box(cell,.12f,.14f,-.327f,.03f,.26f,.01f);}
                k.Box(P(ClampRedHex,.15f),.1f,.375f,-.25f,.045f,.035f,.045f,.008f);k.Box(P(0x101010),-.1f,.375f,-.25f,.045f,.035f,.045f,.008f);
                foreach(float s in new[]{-1f,1f})k.Box(P(0x22252A),s*.15f,.2f,-.1f,.04f,.36f,.2f,.008f);
                Packed:;
            }
            foreach(bool m in new[]{false,true})
            {
                // Heavy shoulders under the vest, arms in the torn sleeves, red rubber gauntlets.
                k.On(CapturedRig.LClav,m);k.Box(Muscle,-.06f,-.005f,0,.2f,.16f,.23f,.05f);if(!k.Far)k.Box(vest,-.04f,.03f,0,.17f,.1f,.24f,.02f);
                k.On(CapturedRig.LArm,m);k.Box(Muscle,0,-.13f,0,.19f,.27f,.2f,.045f);if(!k.Far)k.Box(Suit,0,-.05f,0,.205f,.1f,.215f,.02f);if(k.Mid)Vein(k,-.098f,-.15f,.03f,.12f,.2f,1);
                k.On(CapturedRig.LElbow,m);if(!k.Far)k.Box(SkinDark,0,0,0,.16f,.11f,.17f,.03f);k.Box(Muscle,0,-.1f,.005f,.21f,.16f,.21f,.045f);
                k.Box(glove,0,-.2f,.0f,.24f,.12f,.24f,.03f);if(k.Mid)k.Box(P(0x7A1E12),0,-.145f,0,.25f,.02f,.25f,.005f);
                k.On(CapturedRig.LHand,m);k.Box(glove,0,-.05f,.015f,.2f,.15f,.19f,.04f);
                if(!k.Far)for(int i=0;i<3;i++)k.Box(glove,(i-1)*.055f,-.13f,.08f,.05f,.07f,.06f,.012f,.4f);
                // Work trousers and heavy boots.
                k.On(CapturedRig.LHip,m);k.Box(P(0x2D3238,.04f),0,-.12f,0,.23f,.26f,.25f,.035f);if(k.Mid)k.Box(band,0,-.21f,.0f,.235f,.025f,.255f,.004f);
                k.On(CapturedRig.LKnee,m);k.Box(P(0x2D3238,.04f),0,-.1f,0,.21f,.24f,.21f,.03f);if(m==(look==0))k.Box(ShellDark,0,-.02f,.11f,.18f,.12f,.07f,.015f);
                k.On(CapturedRig.LFoot,m);k.Box(P(0x3A2A1A,.06f),0,-.025f,-.03f,.17f,.11f,.2f,.02f);k.On(CapturedRig.LToe,m);k.Box(P(0x3A2A1A,.06f),0,-.01f,.015f,.17f,.08f,.15f,.018f);
            }
            // The jumper cables coiled round the right forearm (bank +x is his right), their copper clamps hanging off the hand.
            k.On(CapturedRig.RElbow);
            {
                var cable=P(CableHex,.12f);int turns=k.Fine?4:k.Mid?3:1;
                for(int i=0;i<turns;i++){if(k.Far){k.Box(cable,0,-.08f,0,.26f,.12f,.26f);break;}k.S.Push(WeaponSculpt.T(0,-.03f-i*.045f,0));k.S.Push(WeaponSculpt.Rx((float)Math.PI/2+.15f*(i%2==0?1:-1)));k.S.Torus(cable,Vector3.Zero,.125f,.018f,k.Fine?14:8,k.Fine?6:4);k.S.Pop();k.S.Pop();}
                if(k.Mid){k.Box(P(ClampRedHex,.2f),.1f,-.2f,.08f,.04f,.08f,.03f,.006f,.5f);k.Box(P(CopperHex,.3f),.1f,-.25f,.1f,.03f,.04f,.025f,0,.5f);k.Box(cable,.12f,-.14f,.04f,.02f,.13f,.02f,0,.3f);}
            }
            k.On(CapturedRig.RHand);if(k.Mid){k.Box(P(0x101010,.15f),-.06f,-.17f,.06f,.035f,.07f,.03f,.006f,.3f);k.Box(P(CopperHex,.3f),-.06f,-.215f,.07f,.028f,.035f,.022f);}
            Neck(k,.1f,.06f,Muscle);
            // The head under its hard hat: the cracked headlamp on the front (its beam and halo are the presentation's).
            Head(k,NativeStationKinds.HeadWidth[NativeStationKinds.Linesman],NativeStationKinds.HeadDrop[NativeStationKinds.Linesman],.3f,3);
            k.On(CapturedRig.Head);
            {
                float y=NativeStationKinds.HeadDrop[NativeStationKinds.Linesman];
                k.Blob(hat,0,.305f+y,-.01f,.138f,.08f,.15f);k.Box(hat,0,.27f+y,.0f,.29f,.024f,.31f,.008f);if(!k.Far)k.Box(hat,0,.274f+y,.15f,.24f,.02f,.065f,.006f,-.25f);
                if(k.Mid)k.Box(P(look==0?0xC8A000:0xA8A69E,.1f),0,.365f+y,-.01f,.04f,.035f,.27f,.008f);
                if(!k.Far)k.Box(P(0x2A2A2A,.2f),0,.305f+y,.14f,.085f,.056f,.04f,.008f);k.Box(WeaponPaint.Glow(0xEAF4FF,6f,.9f),0,.305f+y,.162f,.062f,.04f,.012f);
                if(k.Fine)k.Box(P(0x4A4A4A),.012f,.315f+y,.169f,.048f,.006f,.004f,0,0,0,.6f);
                if(k.Mid){k.Box(P(0x1A1A1A),0,.24f+y,-.13f,.3f,.022f,.03f);}
            }
        }

        // ================================================================================================================
        // Conductor: all aboard. Tall and thin in a long dark coat to the knees, a peaked cap, brass buttons, a whistle on a chain
        // (the red lantern is its prop on the left hand).
        // ================================================================================================================
        private static void Conductor(K k)
        {
            int look=k.Look;const float w=.86f;var coat=P(CoatHex,.06f);var coatDark=P(CoatDarkHex,.03f);var brass=P(BrassHex,.35f);
            // The coat's skirt hangs from the hips to the knees: a panel on each thigh (so it parts as he walks) and the tails behind.
            k.On(CapturedRig.Pelvis);k.Box(coat,0,-.02f,0,.42f*w,.24f,.28f,.025f);k.Box(coatDark,0,.075f,0,.43f*w,.05f,.285f,.01f);
            if(k.Mid){k.Box(brass,0,.076f,.145f,.05f,.035f,.01f);k.Hem(-.12f,.19f*w,.13f,look==0?8:10,.26f,.02f,true,.0f);}
            k.On(CapturedRig.Spine);k.Box(coat,0,.075f,0,.36f*w,.19f,.26f,.02f);
            k.On(CapturedRig.Chest);
            k.Box(coat,0,.1f,0,.5f*w,.3f,.285f,.035f);k.Box(coat,0,.235f,-.01f,.45f*w,.075f,.26f,.02f);
            // The lapels open over a grey waistcoat and a dead white collar; two columns of brass buttons.
            k.Box(P(0x3A3C42,.05f),0,.14f,.135f,.13f,.22f,.02f,.006f);k.Box(P(LinenHex,.05f),0,.255f,.12f,.12f,.04f,.04f,.006f);
            if(look==1){k.Box(Skin,.07f,.08f,.143f,.08f,.14f,.012f);k.Box(Wound,.07f,.07f,.15f,.05f,.09f,.008f);if(k.Mid)for(int i=0;i<3;i++)k.Box(BoneWhite,.07f,.04f+i*.04f,.154f,.07f,.012f,.008f);}
            foreach(float s in new[]{-1f,1f}){k.Box(coatDark,s*.085f,.17f,.142f,.06f,.2f,.012f,0,0,0,s*-.18f);if(!k.Far)for(int i=0;i<4;i++)if(!(look==1&&s>0&&i<3))k.Box(brass,s*.115f,.22f-i*.065f,.148f,.022f,.022f,.012f,.004f);}
            Vein(k,-.04f,.17f,.147f,.07f,.2f,.9f);
            // The whistle on its chain across the chest.
            if(k.Mid){var chain=P(BrassHex,.25f);k.Box(chain,-.05f,.2f,.15f,.11f,.006f,.006f,0,0,0,.6f);k.Box(chain,-.015f,.15f,.152f,.006f,.07f,.006f);k.Box(P(0xD8D0B0,.4f),-.015f,.11f,.156f,.02f,.04f,.018f,.004f);}
            // The back: the coat's seam and a strap of the lantern's bag.
            k.Box(coatDark,0,.11f,-.145f,.02f,.24f,.01f);if(k.Mid)k.Box(P(0x2A2016),-.12f,.15f,-.146f,.04f,.3f,.01f,0,0,0,.5f);
            if(k.Mid)k.Collar(.255f,.12f,.06f,6,2.4f,.25f,.018f);
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);k.Box(coat,-.04f,-.005f,0,.12f,.1f,.16f,.02f);if(k.Mid)k.Box(P(BrassHex,.3f),-.05f,.05f,0,.09f,.012f,.03f);
                k.On(CapturedRig.LArm,m);k.Box(coat,0,-.14f,0,.13f,.26f,.135f,.02f);
                k.On(CapturedRig.LElbow,m);k.Box(coat,0,-.07f,0,.115f,.17f,.12f,.018f);k.Box(coatDark,0,-.165f,0,.125f,.04f,.13f,.006f);if(k.Mid)k.Box(brass,-.055f,-.165f,.03f,.012f,.018f,.018f);
                Hand(k,m,false,.86f,.04f);
                // Trousers below the coat's hem, polished shoes cracked open.
                k.On(CapturedRig.LHip,m);k.Box(coat,0,-.12f,.005f,.16f,.27f,.18f,.02f);k.Box(coatDark,0,-.24f,.01f,.17f,.04f,.185f,.006f);
                k.On(CapturedRig.LKnee,m);k.Box(P(0x22252C,.03f),0,-.04f,0,.13f,.1f,.14f,.012f);k.Box(P(0x22252C,.03f),0,-.13f,0,.12f,.17f,.13f,.015f);
                k.On(CapturedRig.LFoot,m);k.Box(P(0x0B0B0D,.4f),0,-.025f,-.03f,.12f,.08f,.18f,.015f);k.On(CapturedRig.LToe,m);k.Box(P(0x0B0B0D,.4f),0,-.012f,.02f,.12f,.06f,.14f,.012f);
                if(k.Mid&&m)k.Box(Skin,0,-.01f,.09f,.06f,.03f,.04f);
            }
            Neck(k,.055f,.1f,Skin);
            Head(k,NativeStationKinds.HeadWidth[NativeStationKinds.Conductor],NativeStationKinds.HeadDrop[NativeStationKinds.Conductor],.24f,3);
            // The peaked cap: a dark crown, the brass band and badge, the black visor (tilted down over the eyes); look 1 crushed.
            k.On(CapturedRig.Head);
            {
                float tilt=look==1?.1f:0;var crown=P(0x141820,.08f);
                k.Box(crown,0,.31f,-.015f,.27f,.065f,.27f,.02f,-.05f,0,tilt);k.Box(crown,0,.342f,-.02f,.285f,.022f,.285f,.01f,-.06f,0,tilt);
                k.Box(brass,0,.287f,-.01f,.28f,.02f,.28f,.006f,0,0,tilt);if(!k.Far)k.Box(P(0xE8D070,.5f),0,.315f,.125f,.05f,.034f,.012f,.004f,0,0,tilt);
                k.Box(P(VisorHex,.6f),0,.28f,.15f,.25f,.014f,.08f,.004f,.32f);
                if(k.Fine&&look==0)k.Box(P(0xC8C8C8,.3f),.1f,.292f,.14f,.03f,.012f,.012f);
            }
        }

        // ================================================================================================================
        // Undertow: something in the water. The runner's lean body drowned: grey-green skin, a torn glossy raincoat (its hood up on
        // look 0), weed and cable strands hanging off long arms, aqua eyes and veins.
        // ================================================================================================================
        private static void Undertow(K k)
        {
            int look=k.Look;const float w=.82f;var skin=P(DrownedHex,.14f);var skinDark=P(DrownedDarkHex,.12f);var slick=P(SlickerHex,.32f);var slickDark=P(0x1C2620,.25f);var weed=P(WeedHex,.08f);
            k.On(CapturedRig.Pelvis);k.Box(slickDark,0,0,0,.4f*w,.21f,.26f,.02f);if(k.Mid)k.Hem(-.05f,.19f*w,.14f,7,.24f,.1f);
            k.On(CapturedRig.Spine);k.Box(skin,0,.075f,0,.32f*w,.17f,.23f,.025f);k.Box(slick,-.05f,.08f,-.005f,.22f*w,.18f,.24f,.015f);
            k.On(CapturedRig.Chest);k.Box(skin,0,.11f,0,.44f*w,.28f,.26f,.03f);
            // The raincoat hangs open and torn: a panel each side, its collar, the back; the ribs and a vein under it.
            foreach(float s in new[]{-1f,1f})k.Box(slick,s*.13f*w,.12f,.0f,.17f*w,.3f,.28f,.02f,0,0,s*.04f);
            k.Box(slick,0,.13f,-.12f,.4f*w,.28f,.05f,.015f);
            if(k.Mid){for(int i=0;i<4;i++)k.Box(P(0xA8B0A0,.1f),0,.04f+i*.05f,.133f,.12f,.012f,.012f);k.Hem(.0f,.19f*w,.15f,6,.22f,.06f,true,-.02f);}
            Vein(k,-.05f,.17f,.134f,.12f,.35f,1.2f);Vein(k,.07f,.08f,.133f,.1f,-.3f,1);
            int ridge=k.Far?2:4;for(int i=0;i<ridge;i++)k.Box(BoneWhite,0,.25f-i*.055f,-.148f,.04f,.025f,.03f,.005f);
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);k.Box(slick,-.05f,0,0,.13f*w,.09f,.15f,.02f);
                // Long thin arms, the sleeves in rags, strands of weed and cable hanging off them (they sway with the arm).
                k.On(CapturedRig.LArm,m);k.Box(skin,0,-.14f,0,.1f,.26f,.105f,.02f);k.Box(slick,0,-.06f,0,.13f,.12f,.135f,.012f,0,0,.1f);
                if(k.Mid){k.Box(weed,-.05f,-.2f,.03f,.012f,.2f,.01f,0,.1f,0,.12f);k.Box(weed,.045f,-.17f,-.03f,.01f,.16f,.01f,0,-.1f,0,-.1f);}
                k.On(CapturedRig.LElbow,m);k.Box(skinDark,0,0,0,.09f,.08f,.095f,.014f);k.Box(skin,0,-.12f,0,.085f,.23f,.09f,.016f);
                if(k.Mid){k.Box(weed,-.04f,-.24f,.02f,.01f,.22f,.01f,0,.15f,0,.1f);k.Box(P(0x2A2622,.2f),.035f,-.2f,-.02f,.008f,.2f,.008f,0,-.1f,0,-.15f);k.Box(BoneWhite,.03f*w,-.21f,.0f,.03f,.045f,.035f,.005f);}
                Hand(k,m,false,.78f,.1f);
                k.On(CapturedRig.LHand,m);if(k.Mid)k.Box(weed,0,-.2f,.06f,.01f,.18f,.01f,0,.3f,0,.05f);
                k.On(CapturedRig.LHip,m);k.Box(skin,0,-.13f,0,.13f,.22f,.14f,.018f);k.Box(slick,0,-.08f,.0f,.17f,.22f,.18f,.015f,0,0,m?-.05f:.05f);if(k.Mid)k.Box(slickDark,-.02f,-.2f,.01f,.16f,.04f,.17f,.005f,0,0,.12f);
                if(k.Mid)k.Box(Rag,0,-.24f,0,.15f,.04f,.16f,.006f,0,0,.15f);
                k.On(CapturedRig.LKnee,m);k.Box(skinDark,0,0,0,.12f,.08f,.13f,.01f);k.Box(skin,0,-.12f,0,.11f,.18f,.12f,.014f);
                k.On(CapturedRig.LFoot,m);k.Box(skin,0,-.03f,-.03f,.12f,.07f,.17f,.014f);k.On(CapturedRig.LToe,m);k.Box(skinDark,0,-.012f,.02f,.12f,.045f,.13f,.01f);
                if(k.Mid)for(int i=0;i<3;i++)k.Box(BoneWhite,(i-1)*.035f,-.02f,.085f,.02f,.018f,.022f);
            }
            Neck(k,.05f,.12f,skin);
            Head(k,NativeStationKinds.HeadWidth[NativeStationKinds.Undertow],NativeStationKinds.HeadDrop[NativeStationKinds.Undertow],.46f,look==0?3:1);
            k.On(CapturedRig.Head);
            float y=NativeStationKinds.HeadDrop[NativeStationKinds.Undertow];
            if(look==0)
            {
                // The raincoat's hood pulled up, sodden: it frames the face and the aqua eyes from the dark.
                k.Box(slick,0,.335f+y,-.015f,.29f,.045f,.31f,.018f,-.08f);k.Box(slick,0,.23f+y,-.155f,.28f,.27f,.045f,.014f);
                foreach(float s in new[]{-1f,1f}){k.Box(slick,s*.145f,.225f+y,-.01f,.03f,.26f,.29f,.01f);if(k.Mid)k.Box(slickDark,s*.15f,.26f+y,.14f,.035f,.17f,.022f,.006f);}
                if(k.Mid){k.Box(slickDark,0,.12f+y,-.14f,.22f,.12f,.04f,.01f);for(int i=0;i<4;i++)k.Box(weed,-.1f+i*.065f,.29f+y,.145f,.012f,.11f,.01f,0,0,0,(i-1.5f)*.2f);}
            }
            else if(k.Mid)for(int i=0;i<6;i++){float a=i*1.05f;k.Box(weed,(float)Math.Sin(a)*.1f,.22f+y,(float)Math.Cos(a)*.1f-.03f,.018f,.22f,.018f,0,(float)Math.Cos(a)*.5f,0,(float)Math.Sin(a)*.5f);}
            // Dripping: thin streaks under the jaw and off the chin.
            if(k.Mid){var drip=WeaponPaint.Glow(0x6FB8A8,.25f,.5f);k.Box(drip,.03f,-.02f+y,.12f,.008f,.07f,.008f);k.Box(drip,-.05f,0+y,.1f,.006f,.05f,.006f);}
        }

        // ================================================================================================================
        // Pileup: rush hour. Three commuters fused into one wide body: a second head and arm out of the right shoulder (the
        // weak point), torn office clothes, a bent handrail across the chest. The door is its prop.
        // ================================================================================================================
        private static void Pileup(K k)
        {
            k.ReducedBevel=.03f;var grey=P(SuitGreyHex,.05f);var beige=P(SuitBeigeHex,.04f);var shirt=P(ShirtHex,.04f);var rail=P(0xB8C0C0,.5f);
            k.On(CapturedRig.Pelvis);k.Box(grey,0,-.01f,0,.5f,.24f,.32f,.025f);k.Box(SuitDark,0,.08f,0,.51f,.06f,.33f,.01f);if(k.Mid){k.Hem(-.08f,.24f,.17f,6,.15f,.12f);k.Box(beige,-.17f,-.06f,-.05f,.17f,.2f,.25f,.02f);}
            k.On(CapturedRig.Spine);k.Box(Muscle,0,.08f,.01f,.48f,.2f,.31f,.04f);k.Box(shirt,.05f,.09f,.02f,.3f,.18f,.3f,.02f);
            if(k.Mid){Vein(k,-.17f,.08f,.15f,.1f,.5f,1);k.Box(Wound,-.1f,.06f,.16f,.1f,.08f,.01f);}
            // The chest: two torsos fused side by side (a grey suit on the left, a beige raincoat on the right), a tie, the hump of
            // the third commuter's back behind, the handrail bent across.
            k.On(CapturedRig.Chest);
            k.Box(grey,-.13f,.12f,0,.36f,.32f,.36f,.045f);k.Box(beige,.16f,.11f,.0f,.36f,.31f,.37f,.045f);k.Box(Muscle,0,.27f,-.12f,.55f,.2f,.18f,.05f);
            k.Box(shirt,-.12f,.16f,.175f,.12f,.22f,.02f,.006f);k.Box(P(TieHex,.08f),-.12f,.13f,.188f,.04f,.18f,.012f,.004f,0,0,.08f);
            k.Box(Skin,.04f,.1f,.18f,.1f,.24f,.02f,.008f);k.Box(Wound,.04f,.08f,.19f,.05f,.18f,.008f);Vein(k,.04f,.16f,.195f,.12f,.2f,1.1f);
            k.Box(Skin,0,.26f,-.215f,.32f,.14f,.06f,.03f);if(k.Mid){k.Box(Wound,.05f,.25f,-.25f,.16f,.02f,.01f,0,0,0,.3f);Vein(k,-.08f,.24f,-.25f,.1f,-.4f,1);}
            k.Box(rail,0,.02f,.2f,.62f,.035f,.035f,.01f,0,0,.22f);k.Box(rail,.3f,.12f,.16f,.035f,.22f,.035f,.01f,0,0,.5f);
            // The second head at the right shoulder (bank +x): a commuter's face sunk into the shoulder, its own eyes and jaw.
            {
                float hx=.31f,hy=.38f,hz=.04f;k.Box(Skin,hx,hy,hz,.2f,.21f,.2f,.03f,0,0,-.35f);k.Box(Hair,hx+.02f,hy+.1f,hz-.02f,.19f,.04f,.19f,.006f,0,0,-.35f);
                if(!k.Far){foreach(float s in new[]{-1f,1f}){k.Box(Socket,hx+s*.05f-.02f,hy+.01f-s*.02f,hz+.1f,.05f,.035f,.012f,0,0,0,-.35f);k.Box(WeaponPaint.Glow(NativeStationKinds.WindowTube,2.2f,.7f),hx+s*.05f-.02f,hy+.01f-s*.02f,hz+.106f,.024f,.018f,.008f,0,0,0,-.35f);}
                    k.Box(Socket,hx-.03f,hy-.07f,hz+.095f,.09f,.03f,.012f,0,0,0,-.35f);if(k.Mid)k.Box(ToothPaint,hx-.03f,hy-.055f,hz+.1f,.08f,.012f,.008f,0,0,0,-.35f);}
                k.Box(Skin,.2f,.3f,.02f,.12f,.12f,.14f,.03f,0,0,-.6f);
            }
            if(k.Mid)k.Collar(.27f,.15f,.05f,7,2.6f,.3f,.018f,.02f);
            foreach(bool m in new[]{false,true})
            {
                // Huge fused shoulders, sleeves of two coats, big hands (they hold the door).
                k.On(CapturedRig.LClav,m);k.Box(Muscle,-.07f,0,0,.22f,.17f,.25f,.05f);k.Box(m?beige:grey,-.05f,.04f,0,.2f,.1f,.26f,.02f);
                k.On(CapturedRig.LArm,m);k.Box(m?beige:grey,0,-.13f,0,.21f,.27f,.22f,.04f);
                k.On(CapturedRig.LElbow,m);k.Box(m?beige:grey,0,-.06f,0,.19f,.16f,.2f,.035f);k.Box(Skin,0,-.17f,.005f,.18f,.13f,.19f,.04f);if(k.Mid)Vein(k,-.09f,-.14f,.05f,.1f,.25f,1);
                k.On(CapturedRig.LHand,m);k.Box(Skin,0,-.06f,.02f,.2f,.17f,.2f,.04f);if(!k.Far)for(int i=0;i<4;i++)k.Box(SkinDark,(i-1.5f)*.045f,-.14f,.08f,.04f,.06f,.05f,.008f,.5f);
                k.On(CapturedRig.LHip,m);k.Box(m?beige:grey,0,-.12f,0,.23f,.26f,.25f,.035f);
                k.On(CapturedRig.LKnee,m);k.Box(m?beige:grey,0,-.06f,0,.21f,.17f,.21f,.03f);k.Box(SuitDark,0,-.17f,0,.17f,.11f,.18f,.015f);
                k.On(CapturedRig.LFoot,m);k.Box(Sole,0,-.025f,-.03f,.17f,.1f,.2f,.018f);k.On(CapturedRig.LToe,m);k.Box(Sole,0,-.01f,.015f,.17f,.08f,.15f,.016f);
            }
            // The second commuter's arm out of the right shoulder, hanging and clawing.
            k.On(CapturedRig.RClav);k.Box(beige,.05f,.06f,-.1f,.1f,.1f,.12f,.02f);k.Box(Skin,.09f,-.05f,-.12f,.08f,.2f,.08f,.02f,.3f,0,-.25f);if(k.Mid)k.Box(SkinDark,.13f,-.17f,-.1f,.07f,.08f,.07f,.01f,.6f);
            Neck(k,.1f,.05f,Muscle);
            Head(k,NativeStationKinds.HeadWidth[NativeStationKinds.Pileup],NativeStationKinds.HeadDrop[NativeStationKinds.Pileup],.34f,1);
        }

        // ================================================================================================================
        // Props: the Conductor's lantern and the Pileup's carriage door, each its own model (one section on joint 0), so they
        // swing, fall, shatter and slam apart from the body. Built once per tier with the bank.
        // ================================================================================================================
        public enum StationProp{Lantern,BrokenLantern,Door}
        public const int StationProps=3;
        /// <summary>The prop's bank id at a tier ("station-lantern-full" ...), made once.</summary>
        public static string StationPropId(StationProp prop,int tier)=>(stationPropIds??=MakePropIds())[(int)prop*Tiers+Math.Max(Full,Math.Min(Impostor,tier))];
        // (Made on first use: the order of static initializers across this partial class's files is not defined.)
        private static string[] stationPropIds;
        private static string[] MakePropIds(){var a=new string[StationProps*Tiers];string[] n={"lantern","lantern-broken","door"};for(int p=0;p<StationProps;p++)for(int t=0;t<Tiers;t++)a[p*Tiers+t]="station-"+n[p]+"-"+TierNames[t];return a;}
        /// <summary>The lantern's pivot is its handle's top (the hand's grip); it hangs down -y. Glass centre .2 m under it.</summary>
        public const float LanternDrop=.2f,LanternHalf=.075f;
        /// <summary>The door in its own frame (bank axes, metres in the Pileup's rig): centred on its middle, +z its outer face,
        /// .7 wide and 1.26 tall (.86 x 1.56 m drawn at the Pileup's scale), its window's centre .3 above the middle.</summary>
        public const float DoorWidth=.7f,DoorHeight=1.26f,DoorWindowY=.3f,DoorThick=.05f;
        public static WeaponMesh BuildStationProp(StationProp prop,int tier)
        {
            tier=Math.Max(Full,Math.Min(Impostor,tier));var mesh=new WeaponMesh{Spec="station-"+prop,Tier=tier};var s=new WeaponSculpt(mesh,tier);s.Into(0);bool fine=tier==Full,mid=tier<=Reduced;
            void B(in WeaponPaint p,float x,float y,float z,float sx,float sy,float sz,float bevel=0,float rx=0,float ry=0,float rz=0){if(tier==Reduced&&bevel<.008f)bevel=0;s.Box(p,new Vector3(x,y,z),new Vector3(sx,sy,sz),bevel,rx,ry,rz);}
            switch(prop)
            {
                case StationProp.Lantern:case StationProp.BrokenLantern:
                {
                    // A railway hand lamp: a wire bail handle, a tin top and chimney, the red glass (its glow is the light), the base.
                    bool broken=prop==StationProp.BrokenLantern;var tin=P(0x3A3D40,.3f);var tinDark=P(0x24262A,.2f);float g=LanternDrop;
                    B(tin,0,-.01f,0,.012f,.03f,.012f);if(mid){B(tinDark,-.045f,-.05f,0,.008f,.08f,.008f,0,0,0,-.35f);B(tinDark,.045f,-.05f,0,.008f,.08f,.008f,0,0,0,.35f);}
                    B(tin,0,-.1f,0,.06f,.035f,.06f,.008f);B(tinDark,0,-.075f,0,.035f,.03f,.035f);
                    B(tin,0,-g-LanternHalf-.02f,0,.13f,.03f,.13f,.01f);B(tin,0,-g+LanternHalf+.012f,0,.12f,.025f,.12f,.008f);
                    if(!broken)
                    {
                        var glass=WeaponPaint.Glow(NativeStationKinds.LanternRed,5.5f,.55f);
                        s.Push(WeaponSculpt.Rx(-(float)Math.PI/2));s.Cylinder(glass,LanternHalf*.95f,-g-LanternHalf,-g+LanternHalf,fine?10:6,0);s.Pop();
                        B(WeaponPaint.Glow(0xFFE0B0,7,1),0,-g,0,.03f,.05f,.03f);
                    }
                    else{B(P(0x5A0C08,.2f),0,-g-LanternHalf+.02f,.0f,.11f,.035f,.11f);if(mid)for(int i=0;i<3;i++)B(P(0x7A1410,.6f),(i-1)*.04f,-g-.03f,.05f,.02f,.06f,.006f,0,0,0,.3f*(i-1));}
                    for(int i=0;i<4;i++){double a=Math.PI*.25+i*Math.PI*.5;B(tinDark,(float)Math.Cos(a)*.062f,-g,(float)Math.Sin(a)*.062f,.012f,LanternHalf*2+.02f,.012f);}
                    if(mid)B(tinDark,0,-g-LanternHalf-.04f,0,.08f,.012f,.08f);
                    break;
                }
                case StationProp.Door:
                {
                    // A stainless carriage door: brushed panels, the rubber seal, the red livery stripe, the window lit by a dying tube
                    // (its light: WeaponPaint glow, flickered by the presentation's glow), cracked glass, a dent, the handrail, and the
                    // third commuter's arm reaching out through the broken window.
                    var steel=P(StationSteelHex,.35f);var steelDark=P(SteelDarkHex,.25f);var rubber=P(RubberHex,.05f);float hw=DoorWidth*.5f,hh=DoorHeight*.5f,t=DoorThick,wy=DoorWindowY,ww=.22f,wh=.2f;
                    // The panel below the window, the strip above it and its two sides, framing a lit opening.
                    float top=hh,winTop=wy+wh,winBot=wy-wh;
                    B(steel,0,(-hh+winBot)*.5f,0,DoorWidth,winBot+hh,t,.01f);
                    B(steel,0,(top+winTop)*.5f,0,DoorWidth,top-winTop,t,.01f);B(steel,-(hw+ww)*.5f,wy,0,hw-ww,wh*2,t);B(steel,(hw+ww)*.5f,wy,0,hw-ww,wh*2,t);
                    B(rubber,0,0,-.002f,DoorWidth+.03f,DoorHeight+.03f,t*.6f);
                    if(mid){B(rubber,0,winTop+.012f,t*.5f+.002f,ww*2+.03f,.02f,.008f);B(rubber,0,winBot-.012f,t*.5f+.002f,ww*2+.03f,.02f,.008f);B(rubber,-ww-.012f,wy,t*.5f+.002f,.02f,wh*2,.008f);B(rubber,ww+.012f,wy,t*.5f+.002f,.02f,wh*2,.008f);}
                    var tube=WeaponPaint.Glow(NativeStationKinds.WindowTube,3.2f,.7f);B(tube,0,wy,-t*.2f,ww*2,wh*2,.01f);
                    if(mid){B(WeaponPaint.Glow(0xF4FFFA,6,1),0,winTop-.03f,-t*.1f,ww*1.7f,.02f,.012f);B(P(0x9AB0AC,.6f),-.12f,wy+.05f,t*.5f+.004f,.008f,.2f,.004f,0,0,0,.5f);B(P(0x9AB0AC,.6f),.05f,wy-.06f,t*.5f+.004f,.006f,.16f,.004f,0,0,0,-.7f);}
                    B(P(LiveryHex,.1f),0,-.08f,t*.5f+.003f,DoorWidth,.05f,.006f);B(P(0xE8E2D0,.1f),0,-.035f,t*.5f+.003f,DoorWidth,.012f,.006f);
                    B(steelDark,0,-hh+.06f,t*.5f+.003f,DoorWidth*.9f,.08f,.008f);
                    if(mid){B(steelDark,.16f,-.3f,t*.5f+.004f,.14f,.11f,.006f,0,0,0,.4f);B(P(0x3A3F42,.2f),-.27f,.05f,t*.5f+.01f,.035f,.16f,.025f,.006f);}
                    if(fine)for(int i=0;i<5;i++)B(P(0x9AA8AC,.5f),-.26f+i*.13f,-.42f,t*.5f+.002f,.003f,.34f,.002f,0,0,0,.05f*(i-2));
                    // The handrail bent across its back (inside face), the commuter's arm out of the window, clawing forward.
                    B(P(0xB8C0C0,.5f),0,-.15f,-t*.5f-.03f,DoorWidth*.85f,.03f,.03f,.008f);
                    // The third commuter's arm, out over the broken window's sill and down the door's face, its fingers clawing at it.
                    var skin=P(SkinHex,.07f);var dark=P(SkinDarkHex,.05f);var sleeve=P(SuitGreyHex,.05f);
                    var elbow=new Vector3(-.09f,wy-.17f,t*.5f+.06f);var wrist=new Vector3(-.15f,wy-.43f,t*.5f+.065f);
                    s.Tube(sleeve,new[]{new Vector3(-.05f,wy-.06f,-t*.3f),new Vector3(-.06f,wy-.12f,t*.5f+.03f),elbow},.042f,fine?7:5,Vector3.UnitY);s.Tube(skin,new[]{elbow,wrist},.034f,fine?7:5,Vector3.UnitY);
                    B(skin,wrist.X,wrist.Y-.05f,wrist.Z,.085f,.09f,.05f,.01f,.1f);
                    if(mid)for(int i=0;i<4;i++)B(dark,wrist.X-.035f+i*.024f,wrist.Y-.13f,wrist.Z+.012f,.016f,.075f,.016f,0,.2f,0,(i-1.5f)*.18f);
                    if(mid)B(P(0x9AB0AC,.6f),-.16f,wy-.17f,t*.5f+.004f,.06f,.006f,.004f,0,0,0,.8f);
                    break;
                }
            }
            return mesh;
        }
    }
}

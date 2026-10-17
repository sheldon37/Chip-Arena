using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Siren's minions as zombies built from the pilots (owner 2026-10-12: "Sirens minions need to look more zombie like
    // (Based on player models) ... Brutes can be big muscle versions"). Each kind is a pilot the Siren took: the Standard
    // pilot's own blocks and proportions (CapturedRig's voxel body, the cube head, the plated chest, pauldrons, cuffs, knee pads
    // and soles), rotted and torn. Pale grey-green skin with darker rot and bruised patches shows through a ripped undersuit,
    // the armour is cracked and half gone (each look keeps different pieces, so the limbs never match), the jaw hangs open over
    // a black mouth, the eyes glow in the kind's colour, and the Siren's ink runs through the wounds as veins that flicker cell
    // by cell (NativeMinionFx glow codes: the possession, the char and the failing power all still read). Every part rides
    // the joint the pilot's part rides and stays inside the authority's capsule (Tests/HordeKindChecks poses and checks them).
    //  - Shambler: the rank and file, three looks: a cracked helmet hanging off its skull, a bare rotted head under a torn
    //    shawl, a split crown spilling the Siren's light (the cracked look); an armoured arm against a bare one.
    //  - Runner: stripped to the torn undersuit, thin and long-clawed, a spine of bone down its back, the jaw thrown wide.
    //  - Brute: a big muscle version: a swollen chest bursting its plate, a hump of trapezius over a small sunken head, huge
    //    shoulders, forearms and fists, thick legs.
    //  - Spitter (a glowing throat sac), crawler (emaciated, long-armed), bloater (a swollen glowing belly), content-gated.
    // Three tiers as before: Full (bevels and fine details), Reduced (secondary forms, no fine bevels), Impostor (the blocks).
    public static partial class NativeHordeModels
    {
        // ---- the zombie palette (sRGB) ----
        private const int SkinHex=0x8e9a7d,SkinDarkHex=0x68745a,RotHex=0x4b5341,BruiseHex=0x5d4d69,WoundHex=0x2a1222,SuitHex=0x1f262d,SuitDarkHex=0x13181d,
            RagHex=0x2c302a,ShellHex=0xadb1b3,ShellDarkHex=0x6f7374,SoleHex=0x0e1114,BoneWhiteHex=0xc4bc9f,TeethHex=0xb3a984,GumHex=0x3b1018,HairHex=0x17140f,MuscleHex=0x84907a;
        private static WeaponPaint Skin=>WeaponPaint.Solid(SkinHex,.07f);private static WeaponPaint SkinDark=>WeaponPaint.Solid(SkinDarkHex,.05f);private static WeaponPaint Rot=>WeaponPaint.Solid(RotHex,.03f);
        private static WeaponPaint Bruise=>WeaponPaint.Solid(BruiseHex,.04f);private static WeaponPaint Wound=>WeaponPaint.Solid(WoundHex);private static WeaponPaint Suit=>WeaponPaint.Solid(SuitHex,.04f);
        private static WeaponPaint SuitDark=>WeaponPaint.Solid(SuitDarkHex);private static WeaponPaint Rag=>WeaponPaint.Solid(RagHex,.03f);private static WeaponPaint Shell=>WeaponPaint.Solid(ShellHex,.12f);
        private static WeaponPaint ShellDark=>WeaponPaint.Solid(ShellDarkHex,.06f);private static WeaponPaint Sole=>WeaponPaint.Solid(SoleHex);private static WeaponPaint BoneWhite=>WeaponPaint.Solid(BoneWhiteHex,.06f);
        private static WeaponPaint ToothPaint=>WeaponPaint.Solid(TeethHex,.08f);private static WeaponPaint Gum=>WeaponPaint.Solid(GumHex);private static WeaponPaint Hair=>WeaponPaint.Solid(HairHex);
        private static WeaponPaint Muscle=>WeaponPaint.Solid(MuscleHex,.07f);private static WeaponPaint Socket=>WeaponPaint.Solid(0x070405);
        /// <summary>The eyes: a steady glow in the kind's colour (no flicker code: they never drop out, as the Siren's white eyes).</summary>
        private static WeaponPaint EyeGlow(K k)=>WeaponPaint.Glow(k.SeamColor,2.1f,.7f);
        /// <summary>Dried blood and ink round the mouth and the wounds.</summary>
        private static WeaponPaint Gore=>WeaponPaint.Solid(0x3d0f1f,.02f);

        // ================================================================================================================
        // Shared anatomy: the pilot's blocks (CapturedRig.Boxes; bank frame, limbs authored on the left at -x and mirrored)
        // ================================================================================================================
        /// <summary>
        /// The zombie head on the pilot's cube skull: `w` its width scale, `drop` how far it sits down on its joint (the brute's
        /// sinks), `jaw` how far the slack jaw hangs (rad), `top` 0 a cracked helmet, 1 a bare scalp, 2 the split crown, 3 nothing.
        /// </summary>
        private static void Head(K k,float w,float drop,float jaw,int top)
        {
            k.On(CapturedRig.Head);float y=drop,hw=.16f*w,hd=.15f*w;
            // The skull (the pilot's .34 cube, shrunk by rot), its brow and cheeks.
            k.Box(Skin,0,.19f+y,-.005f,hw*2,.25f,hd*2,.035f);
            if(k.Far){k.Box(SkinDark,0,.06f+y-jaw*.04f,.015f,hw*1.6f,.06f,hd*1.7f,0,jaw);EyeAt(k,-.07f*w,.175f+y,hd+.004f);EyeAt(k,.07f*w,.175f+y,hd+.004f);return;}
            k.Box(SkinDark,0,.235f+y,hd-.005f,hw*1.9f,.045f,.04f,.012f);
            // Sunken sockets over the glowing eyes, the nose's hole, rotted cheeks.
            foreach(float s in new[]{-1f,1f}){k.Box(Socket,s*.068f*w,.178f+y,hd+.002f,.07f*w,.05f,.02f);EyeAt(k,s*.068f*w,.176f+y,hd+.008f);}
            k.Box(Wound,0,.135f+y,hd+.003f,.04f,.05f,.012f);
            if(k.Mid){k.Box(Rot,.105f*w,.115f+y,hd-.004f,.07f,.07f,.02f);k.Box(Bruise,-.11f*w,.21f+y,hd-.01f,.05f,.06f,.02f);}
            // The mouth: upper teeth under the lip, a black throat, the slack jaw hanging open with its own teeth.
            k.Box(Gum,0,.095f+y,hd-.02f,hw*1.3f,.03f,.05f);
            int teeth=k.Fine?6:k.Mid?4:0;for(int i=0;i<teeth;i++){float u=(i+.5f)/teeth-.5f;k.Box(ToothPaint,u*hw*1.15f,.077f+y,hd-.005f,hw*1.1f/teeth*.75f,.025f+.008f*((i*7)%3),.018f);}
            k.Box(Socket,0,.07f+y,.02f,hw*1.5f,.05f,hd*1.6f);
            if(k.Mid){k.Box(Gore,.04f*w,.1f+y,hd+.002f,.06f,.03f,.008f);k.Box(Gore,-.05f*w,.07f+y,hd-.006f,.05f,.05f,.008f);}
            // The jaw turns open about the skull's back (rx > 0 drops its front).
            // (A hinged kind's jaw is its own model on the head joint, opened by NativeHordeJaw about the same hinge.)
            float jz=.02f,jy=.07f+y;var head=k.S;if(k.SplitJaw){k.S=k.JawSculpt;k.S.Into(CapturedRig.Head);}
            k.S.Push(WeaponSculpt.T(0,-jy,-jz)*WeaponSculpt.Rx(jaw)*WeaponSculpt.T(0,jy,jz));
            k.Box(Skin,0,.04f+y,.01f,hw*1.75f,.055f,hd*1.85f,.018f);k.Box(SkinDark,0,.012f+y,.0f,hw*1.4f,.025f,hd*1.5f,.008f);
            for(int i=0;i<teeth-1;i++){float u=(i+.5f)/(teeth-1)-.5f;k.Box(ToothPaint,u*hw*1.0f,.073f+y,hd-.01f,hw*.9f/teeth,.022f,.016f);}
            if(k.Mid)k.Box(Gum,0,.068f+y,.04f,hw*1.3f,.012f,hd*1.4f);
            k.S.Pop();k.S=head;
            switch(top)
            {
                case 0:
                    // The pilot's helmet, cracked and twisted back off the skull; the visor's shard hangs over an eye.
                    k.Box(Shell,.0f,.29f+y,-.035f,hw*2.15f,.1f,hd*2.05f,.025f,-.12f,0,.06f);k.Box(ShellDark,-.13f*w,.24f+y,-.04f,.05f,.14f,hd*1.8f,.012f,0,0,.08f);
                    if(k.Mid){k.Box(ShellDark,.12f*w,.25f+y,-.06f,.045f,.11f,hd*1.4f,.01f,0,0,-.15f);k.Box(Shell,-.05f*w,.22f+y,hd+.012f,.12f*w,.035f,.012f,.006f,0,0,.32f);k.Box(k.Seam(.8f),-.05f*w,.22f+y,hd+.02f,.09f*w,.008f,.006f,0,0,0,.32f);}
                    if(k.Fine)for(int i=0;i<3;i++)k.Box(Hair,(i-1)*.07f*w,.33f+y,.06f+i*.01f,.03f,.03f,.05f);
                    break;
                case 1:
                    // A bare scalp: patchy hair, a torn flap of scalp, an ink vein out of a wound.
                    k.Box(Rot,0,.312f+y,-.01f,hw*1.95f,.02f,hd*1.9f,.008f);
                    if(k.Mid)for(int i=0;i<5;i++){float a=i*1.3f;k.Box(Hair,(float)Math.Sin(a)*hw*.75f,.325f+y,(float)Math.Cos(a*1.7f)*hd*.6f,.06f,.018f,.07f);}
                    k.Box(Wound,.07f*w,.3f+y,.05f,.07f,.025f,.06f);k.Box(k.Seam(1.1f),.07f*w,.305f+y,.05f,.015f,.012f,.1f,0,0,.5f,0);
                    break;
                case 2:
                    // The split crown: a jagged crack of the Siren's light (Crack is its centre).
                    k.Box(Rot,0,.312f+y,-.01f,hw*1.95f,.02f,hd*1.9f,.008f);
                    {var g=k.Seam(1.4f);k.Box(g,-.03f,.245f+y,hd+.004f,.016f,.07f,.012f,0,0,0,.4f);k.Box(g,-.015f,.29f+y,.12f,.014f,.05f,.04f,0,.6f,0,-.3f);k.Box(g,-.035f,.315f+y,.04f,.016f,.014f,.12f,0,0,.3f,0);
                     if(k.Mid){k.Box(k.Seam(1.1f),.05f,.27f+y,hd+.003f,.012f,.05f,.012f,0,0,0,-.5f);k.Box(Wound,-.02f,.3f+y,.07f,.05f,.02f,.1f);}}
                    break;
            }
        }
        private static void EyeAt(K k,float x,float y,float z){k.Box(EyeGlow(k),x,y,z,.026f,.022f,.01f);}
        /// <summary>A vein of the Siren's ink: a thin glowing strip over a part (flickers by cell).</summary>
        private static void Vein(K k,float x,float y,float z,float len,float rz,float kk=1,float rx=0)
        {
            if(k.Far)return;k.Box(k.Seam(kk),x,y,z,.012f,len,.008f,0,rx,0,rz);
            if(k.Fine)k.Box(k.Seam(kk*.8f),x+(float)Math.Sin(rz)*len*.3f+.012f,y+len*.25f,z,.009f,len*.5f,.007f,0,rx,0,rz+.7f);
        }
        private static void Neck(K k,float r,float h,in WeaponPaint p){k.On(CapturedRig.Neck);k.Limb(p,-.03f,h,r,r*.9f);if(k.Mid)Vein(k,-r*.6f,.04f,r*.8f,.06f,.2f,.8f);}

        // The pilot's hips (pelvis) and waist (spine): the undersuit, torn, with the belt and (`plate`) a hip plate.
        private static void Hips(K k,float w,bool plate,bool belly,int hem)
        {
            k.On(CapturedRig.Pelvis);k.Box(Suit,0,0,0,.42f*w,.21f,.27f,.02f);k.Box(SuitDark,0,.075f,0,.43f*w,.05f,.28f,.008f);
            if(plate)k.Box(ShellDark,-.13f*w,-.02f,.0f,.12f,.16f,.29f,.012f);
            if(k.Mid&&hem>0){k.Hem(-.06f,.2f*w,.145f,hem,.17f,.14f);}
            k.On(CapturedRig.Spine);
            if(belly){k.Box(Skin,0,.075f,.005f,.32f*w,.17f,.24f,.03f);k.Box(Suit,-.1f*w,.08f,-.005f,.15f*w,.18f,.255f,.015f);k.Box(Suit,.06f*w,.07f,-.03f,.2f*w,.15f,.2f,.012f);
                if(k.Mid){k.Box(Wound,.07f*w,.06f,.12f,.08f,.06f,.012f);Vein(k,.09f*w,.09f,.125f,.07f,.6f);Vein(k,.04f*w,.05f,.124f,.05f,-.4f,.8f);}}
            else{k.Box(Suit,0,.075f,0,.34f*w,.18f,.25f,.02f);if(k.Mid)k.Box(Rag,.05f*w,.03f,.126f,.12f*w,.05f,.01f,0,0,0,.2f);}
        }
        // The pilot's chest: the undersuit, the cracked chest plate on one side (`plateSide` -1 its left, 1 its right, 0 none),
        // the torn opening on the other showing ribs and the ink veins out of the wound, the back plate's remains.
        private static void Chest(K k,float w,int plateSide,bool collar)
        {
            k.On(CapturedRig.Chest);k.Box(Suit,0,.1f,0,.52f*w,.3f,.29f,.035f);k.Box(Suit,0,.235f,0,.46f*w,.075f,.27f,.02f);
            if(plateSide!=0)
            {
                float s=plateSide;k.Box(Shell,s*.12f*w,.13f,.13f,.25f*w,.25f,.07f,.025f,0,0,s*.05f);k.Box(ShellDark,s*.13f*w,.24f,.11f,.22f*w,.05f,.07f,.012f);
                if(k.Mid){k.Box(ShellDark,s*.2f*w,.05f,.135f,.08f,.08f,.05f,.008f,0,0,s*.3f);k.Box(k.Seam(.9f),s*.06f*w,.15f,.168f,.01f,.17f,.006f);}
            }
            // The wound: a torn opening on the bare side, its ribs, the ink running out of it.
            float o=plateSide==0?1:-plateSide;
            k.Box(Skin,o*.12f*w,.12f,.132f,.2f*w,.2f,.04f,.015f);k.Box(Wound,o*.12f*w,.1f,.15f,.11f*w,.12f,.012f);
            if(k.Mid){k.Box(Gore,o*.06f*w,.03f,.147f,.07f,.09f,.008f);k.Box(Gore,o*.17f*w,.2f,.15f,.05f,.06f,.008f);}
            if(k.Mid)for(int i=0;i<3;i++)k.Box(BoneWhite,o*.12f*w,.06f+i*.045f,.156f,.13f*w,.014f,.012f);
            Vein(k,o*.04f*w,.2f,.152f,.1f,o*.5f);Vein(k,o*.2f*w,.04f,.15f,.08f,-o*.3f,.8f);
            // The back: the back plate's dark remains on one side, rags.
            k.Box(ShellDark,-o*.07f*w,.11f,-.15f,.24f*w,.2f,.03f,.012f);
            if(k.Mid)k.Box(Rag,o*.1f*w,.06f,-.15f,.16f*w,.12f,.02f,.006f,0,0,.25f);
            if(collar&&k.Mid)k.Collar(.255f,.135f*w,.07f,8,1.9f,.35f,.02f);
        }
        // A shoulder on the left: the pilot's pauldron cracked (`armour`), or a bare rotted shoulder with the sleeve torn off.
        private static void Shoulder(K k,bool mirror,bool armour,float w=1)
        {
            k.On(CapturedRig.LClav,mirror);
            if(armour){k.Box(Shell,-.065f,0,0,.18f*w,.11f,.2f,.022f,0,0,-.08f);if(k.Mid)k.Box(ShellDark,-.08f,-.055f,0,.16f*w,.03f,.19f,.006f);}
            else{k.Box(Skin,-.06f,-.01f,0,.15f*w,.1f,.16f,.025f);if(k.Mid)k.Box(Rag,-.03f,.035f,0,.12f,.025f,.18f,.006f,0,0,.2f);}
        }
        // An arm on the left, armoured (the pilot's upper arm and forearm plates over the suit) or bare (thinner rotted skin, the
        // sleeve's torn stub, bone at the wrist); `claws` the finger length.
        private static void Arm(K k,bool mirror,bool armour,float w,float claws)
        {
            k.On(CapturedRig.LArm,mirror);
            if(armour){k.Box(Suit,0,-.14f,0,.15f*w,.25f,.16f,.02f);k.Box(Shell,0,-.13f,0,.17f*w,.17f,.18f,.02f);if(k.Mid)k.Box(k.Seam(.7f),-.085f*w,-.13f,0,.006f,.12f,.03f);}
            else{k.Box(Skin,0,-.14f,0,.12f*w,.25f,.125f,.022f);k.Box(Rag,0,-.035f,0,.145f*w,.07f,.15f,.01f,0,0,.12f);if(k.Mid){k.Box(Rot,-.055f*w,-.17f,.02f,.02f,.09f,.07f);Vein(k,-.062f*w,-.12f,.03f,.12f,.15f,.8f);}}
            k.On(CapturedRig.LElbow,mirror);
            if(armour){k.Box(Suit,0,0,0,.13f*w,.1f,.14f,.015f);k.Box(Shell,0,-.13f,0,.155f*w,.19f,.165f,.02f);if(k.Mid)k.Box(ShellDark,0,-.215f,0,.16f*w,.035f,.17f,.008f);}
            else{k.Box(SkinDark,0,0,0,.11f*w,.09f,.115f,.015f);k.Box(Skin,0,-.12f,0,.1f*w,.22f,.105f,.018f);if(k.Mid){k.Box(BoneWhite,.03f*w,-.21f,.0f,.035f,.05f,.04f,.006f);k.Box(Wound,-.045f*w,-.1f,.03f,.012f,.08f,.05f);}}
            Hand(k,mirror,armour,w,claws);
        }
        private static void Hand(K k,bool mirror,bool glove,float w,float claws)
        {
            k.On(CapturedRig.LHand,mirror);var palm=glove?SuitDark:Skin;
            if(glove)k.Box(ShellDark,0,.015f,0,.17f*w,.05f,.18f,.01f);
            k.Box(palm,0,-.045f,.005f,.14f*w,.1f,.145f,.018f);
            if(k.Far){k.Box(glove?palm:SkinDark,0,-.1f-claws*.3f,.05f,.12f*w,.07f+claws*.4f,.1f);return;}
            // Fingers curled forward into hooks, nails gone to bone; the thumb inside.
            int n=k.Fine?4:3;for(int i=0;i<n;i++){float u=(i+.5f)/n-.5f;float x=u*.11f*w;k.Box(glove?palm:SkinDark,x,-.1f-claws*.25f,.05f,.026f*w,.06f+claws*.5f,.032f,.006f,.35f);if(k.Mid)k.Box(BoneWhite,x,-.135f-claws*.65f,.075f+claws*.2f,.02f,.02f+claws*.25f,.022f,0,.7f);}
            k.Box(palm,.075f*w,-.045f,.04f,.04f,.07f,.06f,.008f,.2f);
        }
        // A leg on the left: the suit's thigh and shin (one torn open), the knee pad and shin plate where the look kept them, a boot.
        private static void Leg(K k,bool mirror,float w,bool pad,bool plate,bool torn,bool barefoot)
        {
            k.On(CapturedRig.LHip,mirror);k.Box(Suit,0,-.02f,0,.16f*w,.12f,.17f,.015f);k.Box(Suit,0,-.13f,0,.19f*w,.23f,.21f,.025f);
            if(k.Mid&&torn){k.Box(Skin,-.06f*w,-.15f,.075f,.07f,.1f,.07f,.008f);k.Box(Rag,0,-.25f,.0f,.18f*w,.04f,.2f,.006f,0,0,.15f);}
            k.On(CapturedRig.LKnee,mirror);k.Box(Suit,0,0,0,.165f*w,.09f,.175f,.012f);
            if(pad)k.Box(ShellDark,0,-.01f,.1f,.155f*w,.12f,.07f,.015f);else k.Box(SkinDark,0,-.005f,.085f,.12f*w,.08f,.04f,.01f);
            // The shin ends short of the ankle, narrowing into a cuff, so a deeply bent knee never pushes it through the floor.
            k.Box(Suit,0,-.115f,0,.17f*w,.17f,.18f,.02f);k.Box(SuitDark,0,-.205f,0,.13f*w,.05f,.13f,.01f);
            if(plate){k.Box(Shell,0,-.12f,.09f,.15f*w,.17f,.05f,.012f);if(k.Mid)k.Box(k.Seam(.7f),0,-.13f,.117f,.025f,.13f,.006f);}
            else if(k.Mid){k.Box(Skin,.03f*w,-.13f,.085f,.08f,.12f,.04f,.006f);Vein(k,.03f*w,-.12f,.106f,.08f,.2f,.7f);}
            k.On(CapturedRig.LFoot,mirror);
            if(barefoot){k.Box(Skin,0,-.03f,-.03f,.13f*w,.08f,.17f,.015f);k.On(CapturedRig.LToe,mirror);k.Box(SkinDark,0,-.012f,.02f,.13f*w,.05f,.13f,.01f);if(k.Mid)for(int i=0;i<3;i++)k.Box(BoneWhite,(i-1)*.04f*w,-.02f,.09f,.022f,.02f,.025f);}
            else{k.Box(Sole,0,-.025f,-.035f,.15f*w,.09f,.19f,.015f);k.On(CapturedRig.LToe,mirror);k.Box(Sole,0,-.01f,.02f,.15f*w,.07f,.15f,.014f);if(k.Mid)k.Box(Suit,0,.02f,-.01f,.13f*w,.02f,.11f,.005f);}
        }

        // ================================================================================================================
        // Shambler: the rank and file. A pilot in its torn suit and cracked armour, an armoured arm against a bare one.
        // ================================================================================================================
        private static void Shambler(K k)
        {
            int look=k.Look;
            Hips(k,1,look!=1,look!=0,look==1?10:7);Chest(k,1,look==0?-1:look==1?1:0,look==0);
            bool armL=look==0,armR=look==1;
            Shoulder(k,false,armL);Shoulder(k,true,armR||look==2);Arm(k,false,armL,1,.03f);Arm(k,true,armR,look==2?.9f:1,.04f);
            Leg(k,false,1,look!=2,look==0,true,false);Leg(k,true,1,look==1,look==2,look!=1,false);
            if(look==1&&k.Mid){k.On(CapturedRig.Chest);k.Span(Rag,-.27f,.27f,.17f,.3f,-.19f,.05f,.02f);k.Hem(.17f,.25f,.17f,9,.32f,.12f,true,-.02f);}
            Neck(k,.06f,.1f,Skin);Head(k,HeadWidth[NativeHordeKinds.Shambler],HeadDrop[NativeHordeKinds.Shambler],.32f+.08f*look,look==0?0:look==1?1:2);
        }

        // ================================================================================================================
        // Runner: stripped to the torn undersuit, thin, long-clawed, a spine of bone; the jaw thrown wide.
        // ================================================================================================================
        private static void Runner(K k)
        {
            int look=k.Look;const float w=.82f;
            Hips(k,w,false,true,look==0?6:0);
            k.On(CapturedRig.Chest);k.Box(Skin,0,.11f,0,.44f*w,.28f,.26f,.03f);k.Box(Suit,look==0?-.05f:.05f,.13f,.005f,.3f*w,.24f,.27f,.02f);
            k.Box(Wound,.1f,.09f,.13f,.09f,.1f,.012f);if(k.Mid)for(int i=0;i<4;i++)k.Box(BoneWhite,.1f*(look==0?1:-1),.03f+i*.05f,.133f,.14f*w,.013f,.012f);
            Vein(k,-.06f,.16f,.134f,.12f,.4f,1.1f);Vein(k,.13f,.2f,.132f,.09f,-.3f,1);
            // The spine's bone down the back, the shoulder blades through the skin.
            int ridge=k.Far?2:5;for(int i=0;i<ridge;i++)k.Box(BoneWhite,0,.26f-i*.055f,-.135f,.05f,.03f,.04f,.006f);
            if(k.Mid)foreach(float s in new[]{-1f,1f})k.Box(SkinDark,s*.1f,.2f,-.13f,.1f,.12f,.03f,.01f);
            k.On(CapturedRig.Spine);for(int i=0;i<(k.Far?1:3);i++)k.Box(BoneWhite,0,.14f-i*.05f,-.115f,.045f,.025f,.035f,.005f);
            Shoulder(k,false,look==0,w);Shoulder(k,true,false,w);
            Arm(k,false,false,.85f,.08f);Arm(k,true,false,.85f,.08f);
            Leg(k,false,w,false,false,true,look==1);Leg(k,true,w,look==0,false,true,look==1);
            Neck(k,.05f,.12f,Skin);Head(k,HeadWidth[NativeHordeKinds.Runner],HeadDrop[NativeHordeKinds.Runner],.5f,look==0?1:3);
            if(look==1&&k.Mid){k.On(CapturedRig.Head);k.Box(Rot,0,.31f,-.01f,.27f,.02f,.25f,.008f);k.Box(k.Seam(1.2f),0,.32f,-.02f,.012f,.012f,.2f);}
        }

        // ================================================================================================================
        // Brute: the big muscle version. A swollen chest bursting its plate, a hump of trapezius over a small sunken head, huge
        // shoulders, forearms and fists, thick legs (all inside the add capsule: the bulk is in the width).
        // ================================================================================================================
        private static void Brute(K k)
        {
            int look=k.Look;
            k.On(CapturedRig.Pelvis);k.Box(Suit,0,-.01f,0,.46f,.23f,.3f,.025f);k.Box(SuitDark,0,.08f,0,.47f,.06f,.31f,.01f);if(look==0)k.Box(ShellDark,.15f,-.02f,0,.13f,.17f,.32f,.012f);
            if(k.Mid)k.Hem(-.07f,.22f,.16f,6,.14f,.12f);
            k.On(CapturedRig.Spine);k.Box(Muscle,0,.08f,.01f,.4f,.2f,.29f,.04f);
            if(k.Mid){for(int i=0;i<3;i++)foreach(float s in new[]{-1f,1f})k.Box(Skin,s*.05f,.04f+i*.05f,.148f,.075f,.04f,.02f,.008f);Vein(k,.15f,.09f,.14f,.1f,.5f,1);}
            // The chest: swollen past the pilot's, the plate burst off it in pieces, pecs, the hump of the shoulders.
            k.On(CapturedRig.Chest);k.Box(Muscle,0,.12f,0,.6f,.32f,.34f,.05f);
            foreach(float s in new[]{-1f,1f}){k.Box(Skin,s*.13f,.16f,.15f,.23f,.17f,.08f,.03f);k.Box(Muscle,s*.17f,.31f,-.03f,.2f,.13f,.24f,.04f,0,0,s*-.35f);}
            k.Box(Muscle,0,.25f,-.15f,.42f,.2f,.1f,.04f);
            if(look==0){k.Box(Shell,-.15f,.2f,.19f,.18f,.13f,.04f,.015f,0,0,-.2f);k.Box(ShellDark,.2f,.05f,.17f,.12f,.1f,.04f,.012f,0,0,.35f);}
            else if(k.Mid){k.Box(Wound,-.1f,.06f,.175f,.16f,.012f,.01f,0,0,0,.4f);k.Box(Wound,.08f,.22f,.175f,.12f,.012f,.01f,0,0,0,-.3f);}
            Vein(k,-.05f,.13f,.19f,.14f,.35f,1.2f);Vein(k,.2f,.2f,.17f,.1f,-.5f);Vein(k,0,.28f,-.2f,.12f,.1f,.9f,-.3f);
            if(k.Mid)k.Box(Rag,0,.05f,-.17f,.3f,.08f,.02f,.008f,0,0,.15f);
            // Huge shoulders: deltoid masses on the clavicles, the pilot's pauldron split over one.
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LClav,m);k.Box(Muscle,-.06f,-.005f,0,.2f,.16f,.23f,.05f);
                if(m==(look==1)){k.Box(Shell,-.07f,.06f,0,.18f,.06f,.22f,.015f,0,0,-.1f);if(k.Mid)k.Box(k.Seam(.8f),-.085f,.1f,.0f,.16f,.008f,.03f);}
                k.On(CapturedRig.LArm,m);k.Box(Muscle,0,-.13f,0,.2f,.27f,.21f,.045f);if(k.Mid){k.Box(Skin,-.04f,-.12f,.07f,.11f,.15f,.08f,.02f);Vein(k,-.1f,-.12f,.03f,.14f,.2f,1);}
                k.On(CapturedRig.LElbow,m);k.Box(SkinDark,0,.0f,0,.17f,.12f,.18f,.03f);k.Box(Muscle,0,-.12f,.005f,.27f,.24f,.27f,.055f);
                if(m!=(look==1)){k.Box(ShellDark,0,-.17f,0,.28f,.09f,.28f,.015f);}else if(k.Mid)Vein(k,-.13f,-.1f,.06f,.13f,.25f,1.1f);
                // A fist like a block, knuckles of bone.
                k.On(CapturedRig.LHand,m);k.Box(Skin,0,-.06f,.02f,.22f,.18f,.22f,.045f);
                if(!k.Far){for(int i=0;i<4;i++)k.Box(SkinDark,(i-1.5f)*.05f,-.14f,.09f,.045f,.05f,.06f,.01f);if(k.Mid)for(int i=0;i<4;i++)k.Box(BoneWhite,(i-1.5f)*.05f,-.16f,.125f,.03f,.022f,.02f);}
            }
            // Thick legs in the burst suit.
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LHip,m);k.Box(Suit,0,-.12f,0,.23f,.26f,.25f,.035f);if(k.Mid&&m)k.Box(Muscle,-.04f,-.14f,.11f,.13f,.14f,.05f,.015f);
                k.On(CapturedRig.LKnee,m);k.Box(SkinDark,0,0,.0f,.19f,.1f,.2f,.02f);k.Box(Suit,0,-.11f,0,.21f,.18f,.21f,.03f);k.Box(SuitDark,0,-.205f,0,.15f,.05f,.15f,.01f);if(m==(look==0))k.Box(ShellDark,0,-.02f,.11f,.18f,.12f,.07f,.015f);
                k.On(CapturedRig.LFoot,m);k.Box(Sole,0,-.025f,-.035f,.165f,.1f,.19f,.018f);k.On(CapturedRig.LToe,m);k.Box(Sole,0,-.01f,.015f,.165f,.08f,.15f,.016f);
            }
            Neck(k,.1f,.06f,Muscle);
            // A small head sunk low between the shoulders, the jaw heavy and open.
            Head(k,HeadWidth[NativeHordeKinds.Brute],HeadDrop[NativeHordeKinds.Brute],.28f,look==0?3:1);
            if(look==0){k.On(CapturedRig.Head);k.Box(ShellDark,0,.19f,-.08f,.25f,.2f,.06f,.015f);if(k.Mid)k.Box(Hair,0,.205f,-.005f,.22f,.03f,.2f);}
        }

        // ================================================================================================================
        // Spitter: a hunched pilot, its throat swollen into a glowing acid sac (NativeHordeModels' swell grows it at the neck).
        // ================================================================================================================
        private static void Spitter(K k)
        {
            int look=k.Look;Hips(k,.92f,false,true,5);Chest(k,.92f,look==0?1:0,false);
            Shoulder(k,false,false,.92f);Shoulder(k,true,look==0,.92f);Arm(k,false,false,.88f,.05f);Arm(k,true,look==0,.92f,.04f);
            Leg(k,false,.92f,true,false,true,false);Leg(k,true,.92f,false,true,false,false);
            k.On(CapturedRig.Neck);k.Limb(Skin,-.03f,.1f,.065f,.06f);
            var acid=k.Glow(1,1.6f,NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);k.Blob(acid,0,.03f,.045f,.085f,.07f,.065f);
            if(k.Mid){k.Blob(SkinDark,0,.05f,.02f,.095f,.06f,.06f);Vein(k,.04f,.02f,.11f,.05f,.4f,1.2f);Vein(k,-.04f,.04f,.11f,.04f,-.3f);}
            Head(k,HeadWidth[NativeHordeKinds.Spitter],HeadDrop[NativeHordeKinds.Spitter],.3f,look==0?1:3);
            if(k.Mid){k.On(CapturedRig.Head);var drool=k.Glow(1,1.2f,NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);k.Box(drool,.02f,-.01f,.12f,.025f,.08f,.02f);}
        }

        // ================================================================================================================
        // Crawler: an emaciated pilot on all fours, long arms, bare feet, the spine's bone up its back.
        // ================================================================================================================
        private static void Crawler(K k)
        {
            int look=k.Look;const float w=.76f;Hips(k,w,false,true,0);
            k.On(CapturedRig.Chest);k.Box(Skin,0,.11f,0,.42f*w,.25f,.22f,.03f);k.Box(Suit,.04f,.08f,0,.3f*w,.17f,.23f,.018f);
            int ridge=k.Far?2:5;for(int i=0;i<ridge;i++)k.Box(BoneWhite,0,.24f-i*.05f,-.115f,.04f,.025f,.035f,.006f);
            Vein(k,-.08f,.12f,.113f,.12f,.3f,1);Vein(k,0,.2f,-.115f,.1f,.2f,.9f);
            Shoulder(k,false,false,w);Shoulder(k,true,false,w);Arm(k,false,false,.8f,.07f);Arm(k,true,look==1,.8f,.07f);
            // Its legs wasted to the bone (folded haunches: thin limbs, no knee blocks to drag on the floor).
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LHip,m);k.Limb(Skin,.02f,-.24f,.055f,.042f);if(k.Mid)k.Box(Rag,0,-.04f,0,.14f,.06f,.15f,.008f,0,0,.15f);
                k.On(CapturedRig.LKnee,m);k.Limb(SkinDark,.02f,-.23f,.045f,.032f);if(k.Mid)k.Box(BoneWhite,0,-.02f,.035f,.04f,.04f,.03f,.005f);
                k.On(CapturedRig.LFoot,m);k.Box(Skin,0,-.03f,-.02f,.1f,.06f,.14f,.012f);k.On(CapturedRig.LToe,m);k.Box(SkinDark,0,-.012f,.02f,.1f,.04f,.11f,.008f);
            }
            Neck(k,.05f,.08f,Skin);Head(k,HeadWidth[NativeHordeKinds.Crawler],HeadDrop[NativeHordeKinds.Crawler],.45f,look==0?1:2);
        }

        // ================================================================================================================
        // Bloater: a pilot swollen round a glowing gut (the swell grows it at the spine), the suit split open over it.
        // ================================================================================================================
        private static void Bloater(K k)
        {
            int look=k.Look;
            k.On(CapturedRig.Pelvis);k.Box(Suit,0,0,0,.44f,.22f,.3f,.025f);k.Box(SuitDark,0,.08f,0,.45f,.05f,.31f,.01f);
            k.On(CapturedRig.Spine);var gut=k.Glow(1,1.0f,NativeHordeKinds.Seam[NativeHordeKinds.Bloater]);
            k.Blob(Skin,0,.11f,.09f,.25f,.2f,.24f);k.Blob(Bruise,.0f,.1f,.15f,.18f,.13f,.18f);
            if(!k.Far){k.Blob(gut,.08f,.12f,.27f,.06f,.05f,.04f);k.Blob(gut,-.1f,.05f,.26f,.05f,.04f,.04f);}
            Vein(k,.02f,.14f,.32f,.12f,.3f,1.2f);Vein(k,-.14f,.12f,.27f,.1f,-.5f,1);
            if(k.Mid){k.Box(Suit,-.2f,.1f,-.02f,.08f,.2f,.24f,.015f);k.Box(Suit,.2f,.12f,-.02f,.07f,.18f,.24f,.015f);}
            Chest(k,1,look==0?-1:0,false);
            Shoulder(k,false,look==0);Shoulder(k,true,false);Arm(k,false,look==0,.95f,.03f);Arm(k,true,false,.9f,.03f);
            Leg(k,false,1.05f,true,false,false,false);Leg(k,true,1.05f,false,false,true,false);
            Neck(k,.07f,.08f,Skin);Head(k,HeadWidth[NativeHordeKinds.Bloater],HeadDrop[NativeHordeKinds.Bloater],.3f,look==0?0:1);
        }

        // ================================================================================================================
        // Hulk (owner 2026-10-13: "similar to brute but much bigger model size"): drawn 1.8 times the rig (NativeHordeKinds.Scale),
        // so here inside the rig's .53 x 1.7 capsule: a mountain of muscle bursting out of what is left of a pilot. Enormous
        // shoulders and a hump of trapezius that swallows the neck, a tiny sunken head with a hinged jaw (it roars), forearms and
        // fists like boulders, tree-trunk legs; the hide cracked all over, the Siren's ink glowing in the cracks.
        // ================================================================================================================
        private static void Hulk(K k)
        {
            int look=k.Look;
            // A crack in the hide: the split skin and the ink glowing in it (flickering by cell, as every vein).
            k.ReducedBevel=.055f;
            void Crack(float x,float y,float z,float len,float rz,float rx=0,float kk=1.25f)
            {
                if(k.Far)return;k.Box(Wound,x,y,z,.03f,len,.012f,0,rx,0,rz);if(!k.Far)k.Box(k.Seam(kk),x,y,z+(rx==0?.004f:0),.011f,len*.86f,.012f,0,rx,0,rz);
                if(k.Fine)k.Box(k.Seam(kk*.7f),x+(float)Math.Sin(rz)*len*.3f+.02f,y+len*.2f,z,.008f,len*.45f,.01f,0,rx,0,rz+.8f);
            }
            k.On(CapturedRig.Pelvis);k.Box(Muscle,0,-.01f,0,.5f,.25f,.34f,.04f);k.Box(Suit,0,-.05f,.0f,.52f,.17f,.35f,.02f);k.Box(SuitDark,0,.06f,0,.53f,.06f,.36f,.012f);
            if(k.Mid){k.Hem(-.1f,.25f,.18f,7,.16f,.13f);Crack(.16f,.04f,.172f,.12f,.5f);}
            k.On(CapturedRig.Spine);k.Box(Muscle,0,.08f,.01f,.5f,.22f,.34f,.05f);
            if(k.Mid){for(int i=0;i<3;i++)foreach(float sx in new[]{-1f,1f})k.Box(Skin,sx*.065f,.035f+i*.055f,.17f,.09f,.045f,.02f,.01f);}
            Crack(-.14f,.1f,.17f,.15f,-.4f);
            // The chest: a slab of muscle twice the pilot's, the chest plate's last shards sunk in it (look 0), pecs, the hump.
            k.On(CapturedRig.Chest);k.Box(Muscle,0,.13f,0,.8f,.36f,.42f,.07f);
            foreach(float sx in new[]{-1f,1f}){k.Box(Skin,sx*.17f,.17f,.18f,.3f,.2f,.1f,.04f);k.Box(Muscle,sx*.23f,.33f,-.04f,.28f,.16f,.3f,.05f,0,0,sx*-.32f);}
            // The hump of trapezius rising behind and over the head, swallowing the neck.
            k.Box(Muscle,0,.33f,-.13f,.5f,.26f,.24f,.06f);k.Box(SkinDark,0,.26f,-.2f,.56f,.2f,.1f,.04f);
            if(look==0){k.Box(Shell,-.2f,.22f,.225f,.2f,.14f,.04f,.015f,0,0,-.25f);k.Box(ShellDark,.24f,.07f,.215f,.13f,.11f,.04f,.012f,0,0,.4f);}
            else if(!k.Far){for(int i=0;i<(k.Fine?5:3);i++)k.Spike(BoneWhite,(i-2)*.08f,.42f+.02f*(i%2),-.18f,.05f,.05f,new Vector3(0,.09f+.03f*(i%2),-.06f));}
            Crack(-.08f,.16f,.235f,.2f,.35f);Crack(.22f,.24f,.2f,.13f,-.6f);Crack(.05f,.04f,.215f,.12f,.1f);Crack(0,.3f,-.25f,.16f,.15f,-.3f,1.1f);Crack(-.25f,.12f,-.215f,.12f,-.4f,0,1);
            if(k.Mid)k.Box(Rag,0,.05f,-.215f,.36f,.1f,.02f,.008f,0,0,.12f);
            foreach(bool m in new[]{false,true})
            {
                // Shoulders like boulders on the clavicles (one wears the pilot's split pauldron), arms thicker than its head.
                k.On(CapturedRig.LClav,m);k.Box(Muscle,-.085f,.0f,0,.25f,.22f,.3f,.06f);
                if(m==(look==1)){k.Box(Shell,-.09f,.09f,0,.22f,.07f,.28f,.018f,0,0,-.12f);if(k.Mid)k.Box(k.Seam(.8f),-.1f,.13f,0,.2f,.01f,.04f);}
                else Crack(-.12f,.03f,.15f,.12f,.6f);
                k.On(CapturedRig.LArm,m);k.Box(Muscle,0,-.14f,0,.26f,.3f,.27f,.06f);if(k.Mid){k.Box(Skin,-.05f,-.12f,.09f,.14f,.17f,.09f,.03f);}Crack(-.13f,-.13f,.02f,.16f,.15f,0,1.1f);
                k.On(CapturedRig.LElbow,m);k.Box(SkinDark,0,0,0,.22f,.13f,.22f,.04f);k.Box(Muscle,0,-.13f,.01f,.34f,.27f,.33f,.07f);
                if(m==(look==0))k.Box(ShellDark,0,-.2f,0,.35f,.09f,.34f,.02f);else Crack(-.16f,-.12f,.07f,.15f,.25f,0,1.2f);
                // A fist like a block of rubble, its knuckles bone.
                k.On(CapturedRig.LHand,m);k.Box(Skin,0,-.07f,.025f,.28f,.22f,.27f,.06f);
                if(!k.Far){for(int i=0;i<4;i++)k.Box(SkinDark,(i-1.5f)*.062f,-.17f,.11f,.056f,.06f,.07f,.012f);if(k.Mid)for(int i=0;i<4;i++)k.Box(BoneWhite,(i-1.5f)*.062f,-.19f,.15f,.036f,.026f,.024f);Crack(.1f,-.05f,.15f,.09f,-.4f,0,.9f);}
                // Legs like tree trunks in the burst suit, bare feet.
                k.On(CapturedRig.LHip,m);k.Box(Muscle,0,-.12f,0,.27f,.28f,.29f,.05f);k.Box(Suit,0,-.04f,0,.29f,.12f,.3f,.02f);if(k.Mid)Crack(-.13f,-.15f,.06f,.12f,.2f);
                k.On(CapturedRig.LKnee,m);k.Box(SkinDark,0,0,0,.22f,.11f,.2f,.03f);k.Box(Muscle,0,-.095f,-.01f,.23f,.15f,.19f,.04f);k.Box(SkinDark,0,-.18f,0,.15f,.04f,.15f,.012f);
                if(m==(look==0))k.Box(ShellDark,0,.005f,.1f,.19f,.1f,.05f,.015f);
                k.On(CapturedRig.LFoot,m);k.Box(Skin,0,-.028f,-.03f,.18f,.1f,.2f,.02f);k.On(CapturedRig.LToe,m);k.Box(SkinDark,0,-.012f,.02f,.18f,.07f,.16f,.016f);
                if(k.Mid)for(int i=0;i<3;i++)k.Box(BoneWhite,(i-1)*.05f,-.02f,.1f,.028f,.024f,.03f);
            }
            k.On(CapturedRig.Neck);k.Limb(Muscle,-.03f,.06f,.12f,.12f);
            // A tiny head sunk deep in the hump, the jaw hinged to roar.
            Head(k,HeadWidth[NativeHordeKinds.Hulk],HeadDrop[NativeHordeKinds.Hulk],.22f,look==0?3:1);
            if(look==1){k.On(CapturedRig.Head);k.Box(ShellDark,0,.13f,-.07f,.21f,.15f,.06f,.015f);}
        }
    }
}

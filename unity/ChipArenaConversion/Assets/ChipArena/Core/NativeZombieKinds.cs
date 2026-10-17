using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Siren's seven kinds on the rotted pilot's anatomy (Core/NativeZombieAnatomy.cs; docs/ZOMBIE-DESIGN.md). Each kind
    // exaggerates its own silhouette and each look keeps different pieces of the pilot's armour, so no two limbs match:
    //  - Shambler, the rank and file (three looks): the cracked helmet twisted back off the skull, the armoured left arm against a
    //    bare right one, the chest plate broken off over a wound of ribs; the bare scalp under the hood's torn shawl; the split
    //    crown spilling the Siren's light (the cracked look, NativeHordeModels.Crack).
    //  - Runner: lanky, stripped to the waist, starved ribs and a spine of bone, long hooked claws, the jaw thrown wide.
    //  - Brute: the big muscle version: a hump of trapezius over a small sunken head, deltoids and forearms swollen past the
    //    suit, fists like rocks with knuckles of bone; armour shards sunk in the chest or bone spurs out of the back.
    //  - Spitter: thin and hunched round a glowing acid sac that bulges through the throat's torn skin, drool on its hinged jaw.
    //  - Crawler: emaciated, every rib and vertebra showing, long arms, wasted legs, bare feet.
    //  - Bloater: swollen round a gut of stretched, bruised skin with glowing boils, the suit split open over it.
    //  - Hulk: a mountain of muscle (drawn 1.8 times the rig): the hide cracked all over with the ink glowing in the cracks,
    //    a tiny head sunk in the hump, a hinged roaring jaw, fists like boulders, armour shards or a ridge of bone spikes.
    public static partial class NativeHordeModels
    {
        private static Zb Body(K k){int b=NativeHordeKinds.Base(k.Kind);return new Zb{HeadW=HeadWidth[b],HeadDrop=HeadDrop[b]};}
        private static void Whole(K k,Zb b)
        {
            ZTorso(k,b);ZArm(k,b,false);ZArm(k,b,true);ZLeg(k,b,false);ZLeg(k,b,true);ZNeck(k,b,b.Bare?ZSkin:ZSkin);ZHead(k,b);
        }
        // ================================================================================================================
        private static void Shambler(K k)
        {
            var b=Body(k);
            switch(k.Look)
            {
                case 0:b.Top=0;b.Jaw=.18f;b.Pauldron=1;b.Brace=1;b.Glove=1;b.Plate=-1;b.Wound=1;b.Back=true;b.ThighTear=2;b.ShinTear=1;b.KneePad=1;b.ShinPlate=2;break;
                case 1:b.Top=1;b.Jaw=.24f;b.Tear=-1;b.Shawl=true;b.Pauldron=2;b.Brace=2;b.Plate=1;b.Wound=-1;b.ThighTear=1;b.ShinTear=2;b.KneePad=3;break;
                default:b.Top=2;b.Jaw=.3f;b.Tear=1;b.Pauldron=2;b.Wound=-1;b.Back=true;b.ThighTear=1;b.ShinTear=3;b.KneePad=2;b.ShinPlate=1;break;
            }
            Whole(k,b);
        }
        // ================================================================================================================
        private static void Runner(K k)
        {
            var b=Body(k);b.W=.84f;b.Depth=.9f;b.Arm=.8f;b.Fore=.82f;b.Leg=.88f;b.Shin=.86f;b.Delt=.85f;b.Trap=.8f;b.Bare=true;b.Ribs=.7f;b.Spine=true;b.Claw=.036f;b.Finger=1.2f;b.Hand=1.04f;b.Jaw=.55f;b.Neck=.9f;
            if(k.Look==0){b.Top=1;b.Tear=1;b.Pauldron=1;b.ThighTear=2;b.ShinTear=3;b.KneePad=1;}else{b.Top=3;b.Tear=-1;b.ThighTear=1;b.ShinTear=1;b.Barefoot=true;}
            Whole(k,b);
        }
        // ================================================================================================================
        private static void Brute(K k)
        {
            var b=Body(k);b.W=1.2f;b.Depth=1.15f;b.Delt=1.45f;b.Trap=1.9f;b.Arm=1.6f;b.Fore=1.85f;b.Leg=1.3f;b.Shin=1.2f;b.Pec=1.6f;b.Bare=true;b.Hump=.9f;b.Fist=true;b.Hand=1.45f;b.Top=4;b.Neck=1.9f;b.Jaw=.28f;b.Foot=1f;
            if(k.Look==0){b.Pauldron=2;b.KneePad=1;b.ShinPlate=2;b.ThighTear=3;b.Tear=1;}else{b.Pauldron=1;b.Brace=2;b.KneePad=2;b.ThighTear=1;}
            Whole(k,b);Burst(k,b,k.Look==0,k.Look==1,1);
        }
        /// <summary>The big kinds' extras: the hide torn over swollen muscle, armour shards sunk in the chest, bone spurs out of the back
        /// and shoulders, the suit burst into rags round the waist; `cracks` the hide's glowing cracks (the Hulk).</summary>
        private static void Burst(K k,Zb b,bool shards,bool spurs,float scale,bool cracks=false)
        {
            float w=b.W,dp=b.Depth;
            k.On(CapturedRig.Chest);
            var ch=k.Mass((i,q)=>null,new Vector3(0,.15f,0),new Vector3(.26f*w,.19f,.15f*dp),2,null,8,8,null,false);
            // Tears in the hide over the muscle: wet red slashes with the ink running out of them.
            foreach(var (u,v,du,dv) in new[]{(.42f,.2f,.06f,.02f),(.55f,.32f,.05f,-.03f),(.3f,.75f,.06f,.02f)})k.Crack(ch,u,v,du,dv,4,.014f,cracks,1.2f);
            if(shards)k.Piece(2,null,()=>{foreach(var (x,y,a,r) in new[]{(-.13f,.21f,.3f,.35f),(.16f,.07f,-.45f,-.5f),(.02f,.27f,.1f,1.2f)})k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(x*w,y,.15f*dp+.01f),Vector3.Normalize(new Vector3(a,.15f,1)),Vector3.UnitY,r),.14f*w,.1f,.026f,(int)(k.H((int)(y*100),0xfc)*4),.55f,1,null,0,true);});
            if(spurs)
            {
                for(int i=0;i<(k.Fine?6:k.Mid?3:2);i++){float t=i/5f;var root=new Vector3((t-.5f)*.22f*w,.33f+.04f*(float)Math.Sin(t*3.1f),-.13f*dp-.03f*b.Hump);
                    k.Tube(ZBone,new[]{root,root+new Vector3((t-.5f)*.03f,.05f,-.025f)*scale,root+new Vector3((t-.5f)*.05f,.09f+.025f*(i%2),-.06f)*scale},new[]{.016f*scale,.01f*scale,.002f},k.Fine?6:4,true,true);}
                foreach(bool m in new[]{false,true}){k.On(CapturedRig.LClav,m);for(int i=0;i<(k.Fine?3:k.Mid?0:1);i++){var root=new Vector3(-.07f*b.Delt,.05f*b.Delt,-.03f+.035f*i);k.Tube(ZBone,new[]{root,root+new Vector3(-.012f,.035f,-.006f)*scale,root+new Vector3(-.02f,.07f,-.012f)*scale},new[]{.012f*scale,.007f*scale,.002f},5,true,true);}}
                k.On(CapturedRig.Chest);
            }
            // The arms' torn hide and the ink in the cracks; the big muscles standing out of them.
            foreach(bool m in new[]{false,true})
            {
                k.On(CapturedRig.LArm,m);k.Mass(ZSkin,new Vector3(.006f,-.1f,.04f*b.Arm),new Vector3(.05f,.075f,.04f)*b.Arm,2,null,6,8);k.Mass(ZSkinDark,new Vector3(-.01f,-.12f,-.04f*b.Arm),new Vector3(.048f,.085f,.036f)*b.Arm,2,null,6,8);
                k.On(CapturedRig.LElbow,m);k.Mass(ZSkin,new Vector3(-.022f*b.Fore,-.07f,.012f*b.Fore),new Vector3(.04f,.08f,.036f)*b.Fore,2,Matrix4x4.CreateRotationZ(.15f),6,8);k.Mass(ZSkinDark,new Vector3(.024f*b.Fore,-.065f,-.01f*b.Fore),new Vector3(.034f,.07f,.032f)*b.Fore,2,Matrix4x4.CreateRotationZ(-.1f),6,8);
                k.On(CapturedRig.LArm,m);var ua=k.Mass((i,q)=>null,new Vector3(0,-.13f,0),new Vector3(.068f*b.Arm,.15f,.07f*b.Arm),2,null,8,8,null,false);k.Crack(ua,.4f,.6f,.07f,.03f,3,.012f,cracks);
                k.On(CapturedRig.LElbow,m);var fa=k.Mass((i,q)=>null,new Vector3(0,-.11f,.003f),new Vector3(.058f*b.Fore,.14f,.052f*b.Fore),2,null,8,8,null,false);k.Crack(fa,.35f,.45f,.06f,-.04f,3,.012f,cracks);
                k.Vein(fa,.3f,.3f,.08f,.04f,3,.007f,1.1f,1,51+(m?1:0));
                if(cracks){k.On(CapturedRig.LHip,m);var th=k.Mass((i,q)=>null,new Vector3(0,-.11f,.0f),new Vector3(.08f*b.Leg,.15f,.085f*b.Leg),2,null,8,8,null,false);k.Crack(th,.35f,.4f,.06f,.04f,3,.012f,true);}
            }
            // The waist: the suit burst into rags.
            k.On(CapturedRig.Spine);
            if(k.Mid)for(int i=0;i<(k.Fine?8:4);i++){float a=(float)Math.PI*2*(i+.4f*k.H(i,0xf9))/(k.Fine?8:4);var t=new Vector3((float)Math.Cos(a)*.18f*w,.02f,(float)Math.Sin(a)*.13f*dp);
                k.Tatter(i%2==0?ZSuit:ZRag,t,new Vector3(0,-1,0),new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),.08f+.06f*k.H(i,0xfa),.05f,.15f);}
        }
        // ================================================================================================================
        private static void Spitter(K k)
        {
            var b=Body(k);b.W=.92f;b.Depth=.95f;b.Arm=.9f;b.Fore=.9f;b.Leg=.95f;b.Shin=.94f;b.Jaw=.3f;b.Neck=1.05f;b.Claw=.02f;
            if(k.Look==0){b.Top=1;b.Tear=1;b.Pauldron=2;b.Brace=2;b.Plate=1;b.Wound=-1;b.ThighTear=1;b.ShinTear=2;b.KneePad=1;}else{b.Top=3;b.Tear=-1;b.Wound=1;b.Back=true;b.ThighTear=2;b.ShinTear=1;b.ShinPlate=2;}
            Whole(k,b);
            // The acid sac bulging out of the throat (NativeHordeModels' swell grows it about the neck), its skin torn into a web the
            // glow shines through, veins over it.
            k.On(CapturedRig.Neck);var acid=k.Glow(1,1.6f,NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);
            k.Mass(acid,new Vector3(0,.035f,.05f),new Vector3(.08f,.068f,.062f),2,null,7,10);
            if(!k.Far)
            {
                var web=k.Mass((i,q)=>i==0||k.H(i*13+q,0xfb)<.45f?(WeaponPaint?)ZSkinDark:null,new Vector3(0,.037f,.052f),new Vector3(.086f,.074f,.068f),2,null,7,12,null,false);
                k.Vein(web,.35f,.25f,.08f,.04f,3,.005f,1.3f,1,61);k.Vein(web,.4f,.3f,.06f,-.05f,3,.005f,1.2f,0,63);
            }
            // Acid drool strung from the upper teeth (a dangling part on the head), glowing in the gape.
            if(k.Mid)
            {
                k.On(CapturedRig.Head);var drool=k.Glow(1,1.3f,NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);
                for(int i=0;i<(k.Fine?3:1);i++){float fx=(i-1)*.28f;var a=Face(b,fx,-.66f,out var n)-n*.004f;var e=a+new Vector3((i-1)*.006f,-.045f-.012f*i,.004f);var top=a;float len=Vector3.Distance(a,e);
                    k.Piece(0,q=>Vector3.Distance(q,top)/len,()=>k.Tube(drool,new[]{a,(a+e)*.5f+new Vector3(0,0,.003f),e},new[]{.0045f,.003f,.0055f},4,true,false));}
            }
        }
        // ================================================================================================================
        private static void Crawler(K k)
        {
            var b=Body(k);b.HeadH=.9f;b.W=.76f;b.Depth=.84f;b.Arm=.76f;b.Fore=.78f;b.Leg=.6f;b.Shin=.58f;b.Delt=.78f;b.Trap=.75f;b.Ribs=1;b.Spine=true;b.Bare=true;b.Finger=1.25f;b.Claw=.03f;b.Hand=1.04f;b.Barefoot=true;b.Jaw=.45f;b.Neck=.85f;b.Foot=.85f;
            if(k.Look==0){b.Top=1;b.Tear=1;b.ThighTear=3;b.ShinTear=3;}else{b.Top=3;b.Tear=-1;b.Brace=2;b.ThighTear=3;b.ShinTear=3;}
            Whole(k,b);
        }
        // ================================================================================================================
        private static void Bloater(K k)
        {
            var b=Body(k);b.W=1.1f;b.Depth=1.15f;b.Belly=1;b.Arm=1.06f;b.Fore=1.02f;b.Leg=1.12f;b.Shin=1.06f;b.Neck=1.3f;b.Jaw=.3f;b.Trap=1.2f;
            if(k.Look==0){b.Top=0;b.Pauldron=1;b.Brace=1;b.Glove=1;b.Plate=-1;b.KneePad=1;b.ThighTear=2;}else{b.Top=1;b.Tear=1;b.Wound=1;b.ShinTear=3;b.KneePad=2;}
            Whole(k,b);
            // The gut: stretched, bruised skin over glowing boils, the suit split open round it (the swell grows it at the spine).
            k.On(CapturedRig.Spine);var gutGlow=k.Glow(1,1.0f,NativeHordeKinds.Seam[NativeHordeKinds.Bloater]);
            var gut=k.Mass((i,q)=>i>=8?ZRotten:i>=6?ZSkinDark:i<=1?ZSkin:ZSkinPale,new Vector3(0,.105f,.085f),new Vector3(.25f,.205f,.235f),2,null,10,14);
            if(!k.Far)
            {
                foreach(var (u,v,s) in new[]{(.45f,.2f,1f),(.62f,.3f,.8f),(.38f,.34f,.7f),(.55f,.14f,.6f),(.7f,.22f,.65f)}){gut(u,v,out var p,out var n);k.Mass(gutGlow,p+n*.006f,new Vector3(.03f,.026f,.022f)*s,2,null,4,7,false);
                    if(k.Mid)k.Mass(ZSkinDark,p-n*.004f,new Vector3(.038f,.034f,.026f)*s,2,null,4,7,false);}
                k.Vein(gut,.4f,.3f,.08f,.05f,4,.007f,1.1f,2,71);k.Vein(gut,.55f,.2f,.06f,-.05f,4,.006f,1,1,73);
                // The suit split open: rags of it round the gut.
                if(k.Mid)for(int i=0;i<(k.Fine?6:3);i++){float a=(float)Math.PI*(.0f+1f*(i+.5f)/(k.Fine?6:3));var t=new Vector3((float)Math.Cos(a)*.24f,.25f,(float)Math.Sin(a)*.2f+.06f);k.Tatter(ZSuit,t,new Vector3((float)Math.Cos(a)*.3f,-1,(float)Math.Sin(a)*.5f),new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),.08f,.05f,.3f);}
            }
            else k.Box(gutGlow,0,.12f,.25f,.2f,.14f,.06f);
        }
        // ================================================================================================================
        private static void Hulk(K k)
        {
            var b=Body(k);k.ReducedBevel=.055f;
            b.W=1.55f;b.Depth=1.35f;b.Delt=1.85f;b.Trap=2.4f;b.Arm=1.95f;b.Fore=2.3f;b.Leg=1.6f;b.Shin=1.45f;b.Pec=2.1f;b.Bare=true;b.Hump=1.5f;b.Fist=true;b.Hand=1.85f;b.Top=4;b.Neck=2.4f;b.Jaw=.14f;b.Foot=1.3f;b.Barefoot=true;
            if(k.Look==0){b.Pauldron=2;b.KneePad=1;b.Tear=1;}else{b.Pauldron=1;b.ShinPlate=2;}
            Whole(k,b);Burst(k,b,k.Look==0,k.Look==1,1.35f,true);
        }
    }
}

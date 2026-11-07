using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Siren's seven kinds on the rotted pilot's voxel anatomy (Core/NativeZombieAnatomy.cs; docs/ZOMBIE-DESIGN.md). Each kind
    // exaggerates its own silhouette and each look keeps different pieces of the pilot's armour, so no two limbs match:
    //  - Shambler, the rank and file (three looks): the helmet cracked into shards, the armoured left arm against a bare right
    //    one, the chest plate broken off over a wound of ribs; the bare scalp under the hood's torn shawl; the split crown
    //    crusted with blood over the bone (the cracked look, NativeHordeModels.Crack).
    //  - Runner: lanky, stripped to the waist, rows of starved ribs and a spine of bone blocks, long claws, the jaw thrown wide.
    //  - Brute: the big muscle version: stacked, offset blocks of muscle, a hump over a small sunken head, fists like rocks with
    //    knuckles of bone; shards of armour driven into the chest or spurs of bone blocks out of the back.
    //  - Spitter: thin and hunched round a throat sac of glowing voxels behind a torn web of skin, drool on its hinged jaw.
    //  - Crawler: emaciated, every rib and vertebra showing, long arms, wasted legs, bare feet.
    //  - Bloater: swollen round a voxel gut of stretched, bruised skin with glowing boils, the suit torn open over it.
    //  - Hulk: a mountain of block muscle (drawn 1.8 times the rig): the hide split all over in stepped tears onto raw muscle,
    //    a tiny head sunk in the hump, a hinged roaring jaw, fists like boulders, shards or a ridge of bone spurs.
    // Since 2026-10-19 (owner: "more standard zombies") no kind carries the Siren's glowing ink veins or black-flame tongues: the
    // veins are dead and dark, the splits raw flesh; the eyes and the kinds' own glow (the acid, the boils) are kept, steady.
    public static partial class NativeHordeModels
    {
        private static Zb Body(K k){int b=NativeHordeKinds.Base(k.Kind);return new Zb{HeadW=HeadWidth[b],HeadDrop=HeadDrop[b]};}
        private static void Whole(K k,Zb b)
        {
            ZTorso(k,b);ZArm(k,b,false);ZArm(k,b,true);ZLeg(k,b,false);ZLeg(k,b,true);ZNeck(k,b);ZHead(k,b);
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
        /// <summary>A block of muscle: big blocks keep a small bevel on Full (their edges catch the light), small ones are crisp.</summary>
        private static void Mu(K k,in WeaponPaint p,float x,float y,float z,float sx,float sy,float sz,float rz=0)
        {
            k.Box(p,x,y,z,sx,sy,sz,k.Fine&&Math.Min(sx,Math.Min(sy,sz))>.09f?.01f:0,0,0,rz);
        }
        private static void Brute(K k)
        {
            var b=Body(k);b.W=1.15f;b.Depth=1.12f;b.Delt=1.4f;b.Trap=1.9f;b.Arm=1.5f;b.Fore=1.6f;b.Leg=1.25f;b.Shin=1.15f;b.Pec=1.6f;b.Bare=true;b.Hump=.9f;b.Fist=true;b.Hand=1.45f;b.Top=4;b.Neck=1.9f;b.Jaw=.28f;b.Foot=1f;
            if(k.Look==0){b.Pauldron=2;b.KneePad=1;b.ShinPlate=2;b.ThighTear=3;b.Tear=1;}else{b.Pauldron=1;b.Brace=2;b.KneePad=2;b.ThighTear=1;}
            Whole(k,b);Burst(k,b,k.Look==0,k.Look==1,1,false);
        }
        /// <summary>The big kinds' block muscle over the voxel body: stacked, offset pecs, abs, serratus, deltoids, biceps and
        /// forearms; armour shards driven into the chest or spurs of bone blocks out of the back and shoulders; the suit burst
        /// into rags round the waist; `cracks` the hide's stepped splits onto raw muscle (the Hulk). `m` scales to the kind (1 brute).</summary>
        private static void Burst(K k,Zb b,bool shards,bool spurs,float m,bool cracks)
        {
            float w=b.W,dp=b.Depth;bool far=k.Far;
            // ---- the chest: pecs in two stepped blocks a side, the serratus in steps down the flanks ----
            k.On(CapturedRig.Chest);float front=.145f*dp;
            foreach(int s in new[]{-1,1})
            {
                Mu(k,ZSkin,s*.12f*w,.175f,front+.012f*m,.21f*w,.14f,.05f*m);
                if(far)continue;
                Mu(k,ZSkinDark,s*.11f*w,.105f,front+.006f*m,.19f*w,.045f,.04f*m);
                if(k.Fine)Mu(k,ZSkin,s*.05f*w,.2f,front+.024f*m,.07f*w,.08f,.03f*m);
                if(k.Fine)for(int i=0;i<3;i++)Mu(k,i%2==0?ZSkin:ZSkinDark,s*(.24f*w-.004f*i),.05f+.045f*i,.07f*dp-.012f*i,.035f,.035f,.07f*dp);
            }
            if(!far)
            {
                // The back: the lats in steps under the hump.
                foreach(int s in new[]{-1,1}){Mu(k,ZSkinDark,s*.15f*w,.17f,-front-.008f*m,.16f*w,.16f,.03f*m);if(k.Fine)Mu(k,ZSkin,s*.12f*w,.07f,-front-.004f*m,.12f*w,.06f,.02f*m);}
                if(cracks)
                {
                    foreach(var (x,y,ax,ay) in new[]{(-.08f,.2f,.4f,1f),(.16f,.12f,-.3f,1f),(-.18f,.08f,.2f,1f)})k.Zig(new Vector3(x*w,y,front+.012f*m+.026f*m),new Vector3(ax,ay,0),Vector3.UnitZ,3,.036f*m,.009f*m,true,1.25f);
                    foreach(var (x,y,ax,ay) in new[]{(0f,.2f,.2f,1f),(.14f,.24f,-.4f,1f)})k.Zig(new Vector3(x*w,y,-front-.012f*m-.016f*m),new Vector3(ax,ay,0),-Vector3.UnitZ,3,.036f*m,.009f*m,true,1.2f);
                }
                else if(k.Mid)foreach(var (x,y) in new[]{(-.1f,.2f),(.13f,.1f)})k.Zig(new Vector3(x*w,y,front+.04f*m),new Vector3(.4f,1,0),Vector3.UnitZ,3,.026f,.007f,false);
            }
            if(shards&&!far)k.Piece(2,null,()=>
            {
                // Shards of the pilot's chest plate driven into the muscle: small cracked tiles, sunk, at angles.
                foreach(var (x,y,a,r) in new[]{(-.13f,.21f,.3f,.35f),(.16f,.07f,-.45f,-.5f),(.02f,.27f,.1f,1.2f)})
                    k.Tiles(ZShellDirty,K.PlateAt(new Vector3(x*w,y,front+.03f*m),Vector3.Normalize(new Vector3(a,.4f,1)),Vector3.UnitY,r),.1f*w,.075f,.024f,2,1,.3f,.006f,.004f,.12f,.005f);
            });
            if(spurs&&!far)
            {
                // Spurs of bone out of the back: stepped blocks, each narrower and further out.
                int n=k.Fine?4:k.Mid?3:2;for(int i=0;i<n;i++){float t=n==1?.5f:i/(float)(n-1);var root=new Vector3((t-.5f)*.22f*w,.33f+.03f*(float)Math.Sin(t*3.1f),-.13f*dp-.03f*b.Hump);
                    for(int j=0;j<3;j++){float sz=(.032f-.008f*j)*m;k.Box(ZBone,root.X+(t-.5f)*.012f*j*m,root.Y+(.02f+.022f*j)*m,root.Z-(.012f+.014f*j)*m,sz,sz*1.4f,sz);}}
                foreach(bool mi in new[]{false,true}){k.On(CapturedRig.LClav,mi);for(int i=0;i<1;i++){var root=new Vector3(-.06f*b.Delt,.05f*b.Delt,-.03f+.04f*i);
                    for(int j=0;j<3;j++){float sz=(.024f-.006f*j)*m;k.Box(ZBone,root.X-.006f*j*m,root.Y+(.016f+.017f*j)*m,root.Z,sz,sz*1.4f,sz);}}}
                k.On(CapturedRig.Chest);
            }
            // ---- the waist: abs in two columns of three, the suit burst into rags ----
            k.On(CapturedRig.Spine);
            if(!far)for(int i=0;i<3;i++)foreach(int s in new[]{-1,1})Mu(k,i==1?ZSkinDark:ZSkin,s*.05f*w,.03f+.055f*i,.125f*dp+.014f*m,.075f*w,.045f,.024f*m);
            if(k.Mid)for(int i=0;i<(k.Fine?7:3);i++){float a=(float)Math.PI*2*(i+.4f*k.H(i,0xf9))/(k.Fine?7:3);var t=new Vector3((float)Math.Cos(a)*.18f*w,.0f,(float)Math.Sin(a)*.13f*dp);
                k.Strand(i%2==0?ZSuit:ZRag,t,new Vector3(0,-1,0),new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),.07f+.05f*k.H(i,0xfa),.05f,.15f,2);}
            // ---- the arms: deltoids, biceps and forearms in offset blocks ----
            foreach(bool mi in new[]{false,true})
            {
                float d=b.Delt,a=b.Arm,f=b.Fore;
                k.On(CapturedRig.LClav,mi);Mu(k,ZSkin,-.07f-.01f*(d-1),.0f,.0f,.1f*d,.1f*d,.15f*d);if(!far)Mu(k,ZSkinDark,-.075f-.01f*(d-1),-.05f*d,.02f,.08f*d,.05f*d,.12f*d);
                k.On(CapturedRig.LArm,mi);
                if(!far){Mu(k,ZSkin,-.005f,-.1f,.045f*a,.08f*a,.12f,.05f*a);Mu(k,ZSkinDark,-.04f*a,-.15f,-.02f*a,.05f*a,.14f,.08f*a);
                    if(cracks&&k.Fine)k.Zig(new Vector3(-.065f*a,-.06f,.0f),new Vector3(0,-1,.2f),-Vector3.UnitX,4,.03f*m,.009f*m,true,1.15f);}
                k.On(CapturedRig.LElbow,mi);
                Mu(k,ZSkin,0,-.075f,.008f*f,.125f*f,.1f,.12f*f);
                if(!far){Mu(k,ZSkinDark,-.045f*f,-.12f,-.005f,.05f*f,.12f,.1f*f);Mu(k,ZSkin,.03f*f,-.17f,.012f,.08f*f,.07f,.08f*f);
                    if(cracks&&k.Fine)k.Zig(new Vector3(.02f*f,-.04f,.068f*f),new Vector3(.2f,-1,0),Vector3.UnitZ,4,.026f*m,.008f*m,true,1.2f);
                    else if(!cracks&&k.Fine)k.Zig(new Vector3(-.07f*f,-.06f,.02f),new Vector3(0,-1,.3f),-Vector3.UnitX,3,.028f,.006f,true,1);}
                if(cracks&&k.Fine){k.On(CapturedRig.LHip,mi);k.Zig(new Vector3(-.03f,-.06f,.105f*b.Leg+.002f),new Vector3(.3f,-1,0),Vector3.UnitZ,4,.03f*m,.009f*m,true,1.15f);}
            }
        }
        // ================================================================================================================
        private static void Spitter(K k)
        {
            var b=Body(k);b.W=.92f;b.Depth=.95f;b.Arm=.9f;b.Fore=.9f;b.Leg=.95f;b.Shin=.94f;b.Jaw=.3f;b.Neck=1.05f;b.Claw=.02f;
            if(k.Look==0){b.Top=1;b.Tear=1;b.Pauldron=2;b.Brace=2;b.Plate=1;b.Wound=-1;b.ThighTear=1;b.ShinTear=2;b.KneePad=1;}else{b.Top=3;b.Tear=-1;b.Wound=1;b.Back=true;b.ThighTear=2;b.ShinTear=1;b.ShinPlate=2;}
            Whole(k,b);
            // The acid sac bulging out of the throat (NativeHordeModels' swell grows it about the neck): glowing voxels behind a torn
            // web of dark skin, the web's holes letting the glow through.
            k.On(CapturedRig.Neck);var acid=k.Glow(1,1.25f,NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);
            if(k.Far)k.Box(acid,0,.035f,.05f,.14f,.11f,.11f);
            else
            {
                var v=Vox.Over(-.08f,.08f,-.03f,.1f,-.012f,.118f,.026f);var c=new Vector3(0,.035f,.053f);var r=new Vector3(.08f,.068f,.065f);
                Part(k,v,(x,y,z)=>{var p=v.Centre(x,y,z)-c;return p.X*p.X/(r.X*r.X)+p.Y*p.Y/(r.Y*r.Y)+p.Z*p.Z/(r.Z*r.Z)<=1.05f;},
                    (L,x,y,z,d)=>d>=1?L.Juice:(k.H(x*17+y*5+z*29,0xfb)<.42f?0:k.H(x*3+y*41+z*7,0xfc)<.35f?L.Rot:L.Dark),0xfd,.06f,1.25f);
                // The glow the web lets through is the sac's own acid (the juice paint: steady, the kind's colour).
            }
            // Acid drool strung from the upper teeth (a dangling part on the head), glowing in the gape.
            if(k.Mid)
            {
                k.On(CapturedRig.Head);var drool=k.Glow(1,1.3f,NativeHordeKinds.Seam[NativeHordeKinds.Spitter]);var g=HeadGrid(b);
                for(int i=0;i<(k.Fine?3:1);i++){var a=g.At(3.5f+i*1.4f,1.8f,9.3f);float len=.035f+.012f*i;var top=a;
                    k.Piece(0,q=>Math.Max(0,(top.Y-q.Y)/len),()=>{for(int j=0;j<3;j++){float s=.008f-.0015f*j;k.Box(drool,a.X+.002f*j,a.Y-len*(j+.5f)/3,a.Z+.002f*j,s,len/3*1.05f,s);}});}
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
            // The gut (the swell grows it at the spine): a voxel belly of stretched, bruised skin, its top still in the suit, torn
            // open in a stepped line; glowing boils with dark rims, a few standing out as blocks; rot underneath.
            k.On(CapturedRig.Spine);var boil=k.Glow(1,1.0f,NativeHordeKinds.Seam[NativeHordeKinds.Bloater]);var c=new Vector3(0,.105f,.085f);var r=new Vector3(.25f,.205f,.235f);
            if(k.Far){k.Box(ZSkinPale,c.X,c.Y,c.Z+.03f,r.X*1.7f,r.Y*1.7f,r.Z*1.5f);k.Box(boil,0,.12f,.29f,.16f,.1f,.04f);return;}
            if(!k.Fine)
            {
                // At range a stepped stack of blocks: the belly, its front, the suit's band over it, the rot under it, a boil.
                k.Box(ZSkinPale,c.X,c.Y,c.Z,r.X*1.72f,r.Y*1.6f,r.Z*1.6f);k.Box(ZSkinPale,c.X,c.Y-.01f,c.Z+r.Z*.55f,r.X*1.3f,r.Y*1.2f,r.Z*.7f);
                k.Box(ZSuit,c.X,c.Y+r.Y*.72f,c.Z-.01f,r.X*1.6f,r.Y*.36f,r.Z*1.5f);k.Box(ZRotten,c.X,c.Y-r.Y*.72f,c.Z+.02f,r.X*1.4f,r.Y*.36f,r.Z*1.4f);
                k.Box(boil,c.X+.06f,c.Y+.01f,c.Z+r.Z*.9f,.06f,.05f,.02f);return;
            }
            var v=Vox.Over(c.X-r.X,c.X+r.X,c.Y-r.Y,c.Y+r.Y,c.Z-r.Z,c.Z+r.Z,BodyCell(k,1,.048f));int ny=v.NY;
            Part(k,v,(x,y,z)=>{var p=v.Centre(x,y,z)-c;return p.X*p.X/(r.X*r.X)+p.Y*p.Y/(r.Y*r.Y)+p.Z*p.Z/(r.Z*r.Z)<=1;},(L,x,y,z,d)=>
            {
                if(d>=1)return L.Flesh;
                if(y>=ny-3+Jag(k,x*3+z,0xd1))return y==ny-3?L.Fray:L.Suit;              // the suit torn open in a stepped line
                if(y<=1)return L.Rot;if(!k.Fine)return L.Pale;
                float h=k.H(x*19+y*7+z*31,0xd2);if(h<.06f&&z>=v.NZ/2)return L.Juice;   // the boils' glow (steady)
                if(h<.12f)return L.Bruise;
                return y%3==0&&k.H(x*5+z,0xd3)<.3f?L.Dark:(k.H((x>>1)*7+(y>>1)*13+(z>>1)*3,0xd4)<.3f?L.Pale:L.Skin);
            },0xd5,.08f,1.0f);
            if(k.Fine)foreach(var (u,vv,s) in new[]{(.45f,.42f,1f),(.62f,.55f,.8f),(.36f,.6f,.7f),(.55f,.3f,.6f)})
            {
                // A boil standing out of the skin: a dark rim and a glowing core.
                var p=c+new Vector3((u-.5f)*r.X*1.6f,(vv-.5f)*r.Y*1.4f,r.Z*.86f);k.Box(ZRotten,p.X,p.Y,p.Z,.05f*s,.045f*s,.026f);k.Box(boil,p.X,p.Y,p.Z+.008f,.032f*s,.028f*s,.022f);
            }
        }
        // ================================================================================================================
        private static void Hulk(K k)
        {
            var b=Body(k);k.ReducedBevel=.055f;
            b.W=1.5f;b.Depth=1.32f;b.Delt=1.75f;b.Trap=2.4f;b.Arm=1.85f;b.Fore=2.1f;b.Leg=1.5f;b.Shin=1.4f;b.Pec=2.1f;b.Bare=true;b.Hump=1.4f;b.Fist=true;b.Hand=1.85f;b.Top=4;b.Neck=2.4f;b.Jaw=.14f;b.Foot=1.2f;b.Barefoot=true;
            if(k.Look==0){b.Pauldron=2;b.KneePad=1;b.Tear=1;}else{b.Pauldron=1;b.ShinPlate=2;}
            Whole(k,b);Burst(k,b,k.Look==0,k.Look==1,1.35f,true);
        }
    }
}

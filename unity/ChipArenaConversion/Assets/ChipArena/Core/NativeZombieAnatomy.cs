using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The rotted pilot in voxels, the anatomy the Siren's seven kinds share (docs/ZOMBIE-DESIGN.md; the kinds choose from it in
    // NativeZombieKinds.cs). The owner kept the game's voxel style (2026-10-17: "Keep voxel style, just improve it. The sculpted
    // looks odd"), so every zombie is the pilot's blocky body, CapturedRig's voxel proportions and the armoured pilot's chunky
    // plates, rotted: many small cubes at a few sizes instead of one box per limb.
    //  - Body voxels (about 3.5 cm): each part a grid laid in layers, the suit over the skin over the flesh and the bone, so a
    //    torn suit is cells taken off with a stepped edge, a wound goes a layer deeper and the ribs show as rows of bone voxels.
    //  - Head voxels (3.2 cm on the pilot's cube): recessed sockets with glowing block pupils, a stepped brow, a broken block
    //    nose, gaunt cheekbones, a cheek torn open onto the teeth, a hanging block jaw with uneven block teeth.
    //  - Plates (the pilot's chunky slabs): cracked into tiles, offset and tilted, some gone; the helmet cracked into shards.
    //  - Details (1-2 cm): teeth, pupils, claws, nails, knuckles, ink veins as glowing voxels, stepped glowing cracks.
    // Left limbs are authored at bank -x and mirrored for the right.
    public static partial class NativeHordeModels
    {
        // ---- the rotted pilot's palette (sRGB), each on its ZombieLit surface class ----
        private static WeaponPaint Z(int hex,int surf,float k=1)=>new WeaponPaint(WeaponPaint.Linear(hex)*k,Vector3.Zero,0,surf);
        private static readonly WeaponPaint
            ZSkin=Z(0x97a08f,SurfSkin),ZSkinPale=Z(0xaab0a2,SurfSkin),ZSkinDark=Z(0x69725f,SurfSkin),ZRotten=Z(0x535c47,SurfSkin),ZBruise=Z(0x6e6676,SurfSkin),
            ZFlesh=Z(0x6a1c24,SurfFlesh),ZFleshDeep=Z(0x2e0a10,SurfFlesh),ZGum=Z(0x5a1a20,SurfFlesh),ZMouth=Z(0x1a0507,SurfFlesh),
            ZBone=Z(0xd0c6a6,SurfBone),ZTooth=Z(0xc7b98f,SurfBone),ZToothDark=Z(0x8f7f5c,SurfBone),ZNail=Z(0x2e2620,SurfBone),
            ZSuit=Z(0x343d48,SurfCloth),ZSuitLight=Z(0x56616d,SurfCloth),ZSuitDark=Z(0x1d232a,SurfCloth),ZSuitFray=Z(0x5a5c58,SurfCloth),ZRag=Z(0x3a3832,SurfCloth),ZHood=Z(0x2a2433,SurfCloth),
            ZShell=Z(0xd0d3cf,SurfShell),ZShellDirty=Z(0xb9bdb8,SurfShell),ZShellDark=Z(0x5d6368,SurfShell),ZMetal=Z(0x8f969c,SurfWire),
            ZBoot=Z(0x2a3036,SurfCloth),ZSole=Z(0x15181b,SurfMatte),ZHair=Z(0x2a241c,SurfMatte),ZSocket=Z(0x0b0607,SurfMatte),ZWire=Z(0x24282c,SurfWire),ZVisor=Z(0x0f171b,SurfShell),
            ZDeadAccent=Z(0x173230,SurfShell),ZSeamLine=Z(0x101215,SurfMatte);

        /// <summary>A zombie body's proportions and choices (the kinds set them; NativeZombieKinds.cs).</summary>
        private sealed class Zb
        {
            public float W=1,Depth=1,Arm=1,Fore=1,Leg=1,Shin=1,Delt=1,Trap=1,Pec=0,Belly=0,Ribs=0,Hump=0,Neck=1,Finger=1,Claw=.014f,Hand=1,Foot=1;
            public float HeadW=1,HeadH=1,HeadDrop=0,Jaw=.35f,Eye=1;
            /// <summary>The head's top: 0 the cracked helmet, 1 a bare scalp with patchy hair, 2 the split crown (the cracked look),
            /// 3 bald with the skull bared through a torn scalp, 4 a heavy bald brow (the big kinds).</summary>
            public int Top;
            /// <summary>The cheek torn open to the molars: -1 the left, 1 the right, 0 none.</summary>
            public int Tear;
            /// <summary>Armour kept per side (bit 1 left, 2 right) on the shoulder, the upper arm, the forearm and glove; the chest
            /// plate's side (-1 left, 1 right, 0 none) and whether the back unit stays.</summary>
            public int Pauldron,Brace,Glove;public int Plate;public bool Back;
            /// <summary>Where the suit is torn open: the chest wound's side (-1, 1; 0 none), a bare torso (the stripped kinds), a
            /// torn thigh / shin (bit 1 left, 2 right), bare feet.</summary>
            public int Wound;public bool Bare;public int ThighTear,ShinTear,KneePad,ShinPlate;public bool Barefoot;
            /// <summary>Fists instead of clawing hands (the big kinds).</summary>
            public bool Fist;
            /// <summary>Bones down the back (the starved kinds' spine and shoulder blades).</summary>
            public bool Spine;
            /// <summary>The hood's torn shawl over the shoulders (the shambler's second look).</summary>
            public bool Shawl;
            public int Tatters=1;
        }

        // ================================================================================================================
        // Grid painting: the layers a part's cells take
        // ================================================================================================================
        /// <summary>The paints of one grid (ids into its Paints), its ink vein one flicker cell.</summary>
        private struct Layers{public int Skin,Pale,Dark,Rot,Bruise,Flesh,Deep,Bone,Suit,SuitDark,SuitLight,Fray,Socket,Mouth,Gum,Hair,Vein,Shell,ShellDark;}
        private static Layers Lay(K k,Vox v,float vein=1)
        {
            return new Layers{Skin=v.Add(ZSkin),Pale=v.Add(ZSkinPale),Dark=v.Add(ZSkinDark),Rot=v.Add(ZRotten),Bruise=v.Add(ZBruise),Flesh=v.Add(ZFlesh),Deep=v.Add(ZFleshDeep),Bone=v.Add(ZBone),
                Suit=v.Add(ZSuit),SuitDark=v.Add(ZSuitDark),SuitLight=v.Add(ZSuitLight),Fray=v.Add(ZSuitFray),Socket=v.Add(ZSocket),Mouth=v.Add(ZMouth),Gum=v.Add(ZGum),Hair=v.Add(ZHair),
                Vein=v.Add(k.Seam(vein)),Shell=v.Add(ZShellDirty),ShellDark=v.Add(ZShellDark)};
        }
        /// <summary>Dead skin in blocky patches: two-cell clusters of rot, darker skin, bruise and pallor over the grey-green.</summary>
        private static int Mot(K k,in Layers L,int x,int y,int z,int salt)
        {
            // (Below Full one skin, so the faces merge into a few large quads.)
            if(!k.Fine)return L.Skin;
            float r=k.H((x>>1)*73+(y>>1)*151+(z>>1)*37,salt);return r>.86f?L.Rot:r>.72f?L.Dark:r<.08f?L.Bruise:r<.17f?L.Pale:L.Skin;
        }
        /// <summary>A shade step for every two-cell cluster (the per-voxel variation of voxel art, in patches the mesher can merge).</summary>
        private static void Shades(K k,Vox v,int salt){for(int z=0;z<v.NZ;z++)for(int y=0;y<v.NY;y++)for(int x=0;x<v.NX;x++)v.SetShade(x,y,z,(int)(k.H((x>>1)*13+(y>>1)*71+(z>>1)*29,salt)*4));}
        /// <summary>Corner columns cut off a grid's vertical edges (n cells), so a limb or a torso reads round in blocks.</summary>
        private static bool Chamfer(Vox v,int x,int z,int n=1)=>!((x<n||x>=v.NX-n)&&(z<n||z>=v.NZ-n));
        /// <summary>A grid drawn as a layered part: filled by `inside`, painted by `paint`, a shade step per cell.</summary>
        private static Vox Part(K k,Vox v,Func<int,int,int,bool> inside,Func<Layers,int,int,int,int,int> paint,int salt,float jitter=.08f,float vein=1)
        {
            var L=Lay(k,v,vein);v.Shape(inside,L.Skin);v.Paint((x,y,z,d)=>paint(L,x,y,z,d));Shades(k,v,salt);k.Voxels(v,jitter);return v;
        }
        /// <summary>A body voxel for a part `scale` times the pilot's: the big kinds' voxels grow with them (the same count of blocks
        /// over a bigger body), the small kinds keep the pilot's; the reduced tier's are larger (fewer, bigger blocks at range).</summary>
        private static float BodyCell(K k,float scale,float c)=>c*Math.Max(1,scale*.9f)*(k.Fine?1:1.4f);
        /// <summary>A stepped tear edge: 0 or 1 cell of jag for a column, by hash.</summary>
        private static int Jag(K k,int i,int salt)=>k.H(i,salt)<.5f?0:1;

        // ================================================================================================================
        // The head: the pilot's cube skull in voxels, the eyes, nose, teeth, the jaw, the scalp or the helmet's shards
        // ================================================================================================================
        /// <summary>The head's voxel (cubic; the narrow kinds' heads are narrower in cells of the same size).</summary>
        private static float HeadCell(Zb b)=>.032f*b.HeadW;
        private const int HeadNX=10,HeadNZ=11;
        /// <summary>The skull's grid: 10 cells wide, 10 deep plus a front layer for the brow and nose, from the jaw hinge up.</summary>
        private static Vox HeadGrid(Zb b){float c=HeadCell(b);int ny=Math.Max(8,Math.Min(11,(int)Math.Round(.256f*b.HeadH*(.55f+.45f*b.HeadW)/c)));return new Vox(HeadNX,ny,HeadNZ,c,new Vector3(-HeadNX*c*.5f,.07f+b.HeadDrop,-5*c));}
        private static void ZHead(K k,Zb b)
        {
            k.On(CapturedRig.Head);var g=HeadGrid(b);float c=g.C;int ny=g.NY;
            if(k.Far)
            {
                // The impostor: the block skull, the eyes, the dark mouth, the jaw hanging.
                var mn=g.At(0,0,0);var mx=g.At(HeadNX,ny,10);k.Box(ZSkin,(mn.X+mx.X)*.5f,(mn.Y+mx.Y)*.5f,(mn.Z+mx.Z)*.5f,mx.X-mn.X,mx.Y-mn.Y,mx.Z-mn.Z);
                foreach(int s in new[]{-1,1}){var e=g.At(s<0?3f:7f,5f,10f);k.Box(ZEye(k),e.X,e.Y,e.Z,c*1.6f,c*1.1f,.01f);}
                var m=g.At(5f,1f,10f);k.Box(ZMouth,m.X,m.Y,m.Z,c*5,c*1.4f,.01f);ZJaw(k,b,g);return;
            }
            if(!k.Fine){ZHeadBlocks(k,b,g);return;}
            // Rows from the hinge: 0-1 the mouth, 2-3 the nose and cheekbones, 4-5 the eyes, 6 the brow, the cranium above.
            const int nose=2,eye=4,brow=6,front=9;int tear=b.Tear;
            bool Inside(int x,int y,int z)
            {
                if(z>front)return false;
                if((x==0||x==HeadNX-1)&&(z==0||z==front))return false;                         // the vertical edges
                if(y==ny-1&&(x==0||x==HeadNX-1||z==0||z==front))return false;                  // the crown's edges
                if(y==ny-2&&(x==0||x==HeadNX-1)&&(z<=1||z==front))return false;
                if(y>=ny-3&&z==0&&(x<=1||x>=HeadNX-2))return false;
                if(y<=1&&(x==0||x==HeadNX-1))return false;                                      // the gaunt jaw line
                if(y==0&&z<=2)return false;                                                     // the neck under the skull's back
                return true;
            }
            g.Shape(Inside,1);
            var L=Lay(k,g,1.1f);int glow=g.Add(k.Seam(1.4f)),browP=g.Add(ZSkinDark);
            bool Socket(int x,int y)=>y>=eye&&y<=eye+1&&(x==2||x==3||x==6||x==7||(x==1&&y==eye&&b.Top!=4));
            bool Cavity(int x,int y,int z)=>y<=1&&x>=2&&x<=7&&z>=7;
            bool TornCheek(int x,int y,int z)=>tear!=0&&y<=nose&&z>=5&&(tear<0?x<=2:x>=HeadNX-3)&&!(y==nose&&(tear<0?x==2:x==HeadNX-3));
            int Paint(int x,int y,int z,int d)
            {
                // The eyes: two cells deep, the socket's floor black.
                if(Socket(x,y)){if(z>=front-1)return 0;if(z==front-2)return L.Socket;}
                // The nose: the bridge's cell kept, its broken half a black hole.
                if(x==5&&y==nose&&z>=front-1)return 0;if(x==5&&y==nose&&z==front-2)return L.Socket;
                if(x==4&&y==nose&&z==front)return L.Dark;
                // The mouth: a cavity three cells deep, black at its back, gums along its roof.
                if(Cavity(x,y,z))return 0;if(y<=1&&x>=2&&x<=7&&z==6)return L.Mouth;if(y==2&&x>=2&&x<=7&&z>=7&&z<=8)return L.Gum;
                // The torn cheek: its skin gone, the flesh under it, the mouth open to the side.
                if(TornCheek(x,y,z)){if(d==0)return 0;if(y<=1&&z>=7&&(tear<0?x<=2:x>=HeadNX-3))return 0;return d<=1?L.Flesh:L.Deep;}
                int top=y>=ny-2&&z<front-1?1:0;
                switch(b.Top)
                {
                    case 1:if(top==1&&d==0&&k.H((x>>1)*31+(z>>1)*17,0xe1)<.55f)return L.Hair;break;
                    case 2:{int line=4+(int)(k.H(z,0xe2)*2.2f);if(y>=ny-2&&x==line&&z<=front-1){if(d==0&&y==ny-1)return 0;return d<=1?glow:L.Bone;}
                            if(y>=brow+1&&z==front&&x==line+1&&d==0)return glow;break;}
                    case 3:if(y>=ny-2&&x<=4&&z>=2&&z<=7){if(d==0)return 0;return L.Bone;}break;
                }
                if(d>=1)return L.Bone;
                return Mot(k,L,x,y,z,0xe3);
            }
            g.Paint(Paint);
            // The front layer: the stepped brow (a cell missing where it broke), gaunt cheekbones, the nose's bridge and tip.
            for(int x=1;x<HeadNX-1;x++){if(b.Top!=4&&k.H(x,0xe4)<.14f)continue;g.Set(x,brow,10,browP);if(b.Top==4&&x>=2&&x<=HeadNX-3&&k.H(x,0xe4)>.2f)g.Set(x,brow+1,10,browP);}
            foreach(int x in new[]{1,HeadNX-2})if(!(tear<0&&x==1)&&!(tear>0&&x==HeadNX-2))g.Set(x,nose+1,10,L.Pale);
            g.Set(4,nose+1,10,L.Dark);
            Shades(k,g,0xe5);k.Voxels(g,.09f);
            // An ink vein down the temple on the side away from the torn cheek: a stepped glowing strip.
            if(k.Mid){bool left=tear>0;var p=g.At(left?0:HeadNX,brow+.6f,6.5f);k.Zig(p,new Vector3(0,-1,.35f),left?-Vector3.UnitX:Vector3.UnitX,3,c*.9f,c*.22f,true,1.1f,false);}
            // The pupils: a glowing block deep in each socket (in the kind's colour).
            foreach(int s in new[]{-1,1}){var e=g.At(s<0?3f:7f,eye+1f,front-1f);k.Box(ZEye(k),e.X,e.Y-c*.1f,e.Z+.004f,c*.95f*b.Eye,c*.8f*b.Eye,c*.35f);}
            // The upper teeth: uneven blocks hanging from the roof of the mouth, one gone, one a long fang.
            int teeth=k.Fine?6:3;for(int i=0;i<teeth;i++)
            {
                float u=2+6f*(i+.5f)/teeth;if(k.H(i,0xe6)<.15f)continue;float len=c*(.55f+.6f*k.H(i,0xe7))*(i==1||i==teeth-2&&k.H(9,0xe7)<.5f?1.5f:1);
                var t=g.At(u,2f,front+.6f);k.Box(k.H(i,0xe8)<.25f?ZToothDark:ZTooth,t.X,t.Y-len*.5f,t.Z-.002f,c*6f/teeth*.72f,len,c*.7f);
            }
            if(tear!=0&&k.Fine)for(int i=0;i<2;i++){var t=g.At(tear<0?1.5f:HeadNX-1.5f,1.6f,7.6f+i*1.1f);k.Box(ZTooth,t.X,t.Y,t.Z,c*.7f,c*.75f,c*.85f);}
            ZScalp(k,b,g);ZJaw(k,b,g);
        }
        /// <summary>The reduced tier's head (20 to 40 m): the same skull in a dozen blocks on the same grid's lines: the cranium, the
        /// cheeks either side of the black mouth, the stepped brow, the nose, the sockets and their pupils, three teeth.</summary>
        private static void ZHeadBlocks(K k,Zb b,Vox g)
        {
            float c=g.C;int ny=g.NY;
            void B(in WeaponPaint p,float x0,float x1,float y0,float y1,float z0,float z1){var a=g.At(x0,y0,z0);var e=g.At(x1,y1,z1);k.Box(p,(a.X+e.X)*.5f,(a.Y+e.Y)*.5f,(a.Z+e.Z)*.5f,e.X-a.X,e.Y-a.Y,e.Z-a.Z);}
            B(ZSkin,0,10,2,ny,0,8.6f);B(ZSkin,.6f,9.4f,2,ny-.6f,8.6f,10);
            B(ZSkin,1,2.2f,0,2,3,9.6f);B(ZSkin,7.8f,9,0,2,3,9.6f);B(ZMouth,2.2f,7.8f,0,2,3,7.2f);
            B(ZSkinDark,1,9,6,7,10,11);B(ZSkinDark,4,5,2,4,10,11);
            foreach(int s in new[]{-1,1}){float x0=s<0?2:6;B(ZSocket,x0,x0+2,4,6,9.96f,10.04f);var e=g.At(x0+1,5f,10f);k.Box(ZEye(k),e.X,e.Y-c*.1f,e.Z+.006f,c*1.1f*b.Eye,c*.9f*b.Eye,c*.3f);}
            for(int i=0;i<3;i++){var t=g.At(3+2f*i,2f,9.6f);float len=c*(.7f+.4f*k.H(i,0xe7));k.Box(ZTooth,t.X,t.Y-len*.5f,t.Z,c*1.1f,len,c*.7f);}
            if(b.Top==1){B(ZHair,1,6,ny-.2f,ny+.3f,1,6);B(ZHair,5,9,ny-.2f,ny+.25f,4,8);}
            else if(b.Top==2){var p=k.Seam(1.4f);B(p,4.4f,5.4f,ny-.2f,ny+.15f,1,9);B(p,4.4f,5.4f,6.5f,ny,10,10.2f);}
            else if(b.Top==3)B(ZBone,1,5,ny-.2f,ny+.15f,2,7);
            ZScalp(k,b,g);ZJaw(k,b,g);
        }
        /// <summary>The head's top: the cracked helmet's shards, hair tufts, the torn scalp's flap.</summary>
        private static void ZScalp(K k,Zb b,Vox g)
        {
            float c=g.C;int ny=g.NY;var top=g.At(HeadNX*.5f,ny,5f);var lo=g.At(0,0,0);var hi=g.At(HeadNX,ny,10);float hw=hi.X-lo.X,hd=hi.Z-lo.Z,hh=hi.Y-lo.Y;
            switch(b.Top)
            {
                case 0:
                {
                    // The pilot's cube helmet cracked into shards round the skull, shoved back: the crown in three tiles, the back
                    // plate with a corner gone, the left side, the right ear module with its dead light, the visor's brow broken in
                    // two, a shard of visor hanging at the temple.
                    float t=.024f,tilt=-.08f;var up=Vector3.Normalize(new Vector3(.04f,1,-.05f));
                    k.Piece(0,null,()=>
                    {
                        k.Tiles(ZShellDirty,K.PlateAt(top+new Vector3(0,t*.5f+.002f,-.02f),up,-Vector3.UnitZ),hw-.03f,hd-.03f,t,3,1,.0f,.008f,.003f,.05f,.008f,null,true);
                        k.Tiles(ZShellDirty,K.PlateAt(new Vector3(0,lo.Y+hh*.6f,lo.Z-t*.5f-.003f),-Vector3.UnitZ,Vector3.UnitY,tilt),hw+.01f,hh*.72f,t,2,2,.3f,.007f,.006f,.07f,.008f);
                        k.Tiles(ZShellDirty,K.PlateAt(new Vector3(lo.X-t*.3f,lo.Y+hh*.62f,lo.Z+hd*.4f),-Vector3.UnitX,Vector3.UnitY),hd*.72f,hh*.5f,t*.8f,2,2,.35f,.007f,.003f,.06f,.008f);
                        var ear=new Vector3(hi.X+.004f,lo.Y+hh*.5f,lo.Z+hd*.42f);k.Box(ZShellDark,ear.X,ear.Y,ear.Z,.022f,.06f,.08f,.005f);k.Box(ZDeadAccent,ear.X+.012f,ear.Y+.01f,ear.Z,.004f,.012f,.026f);
                        var vb=g.At(HeadNX*.5f,7.2f,10.6f);k.Box(ZVisor,vb.X-hw*.2f,vb.Y,vb.Z,hw*.42f,c*.8f,c*.7f,0,0,0,.05f);k.Box(ZVisor,vb.X+hw*.25f,vb.Y-.004f,vb.Z-.004f,hw*.34f,c*.75f,c*.65f,0,0,0,-.12f);
                        if(k.Mid)k.Box(k.Seam(1.2f),vb.X+hw*.05f,vb.Y,vb.Z+c*.36f,.006f,c*.6f,.004f,0,0,0,.4f);
                    });
                    if(k.Mid)k.Piece(1,q=>Math.Max(0,(lo.Y+hh*.62f-q.Y)/.07f),()=>{var s=g.At(.6f,5.6f,10.5f);k.Box(ZVisor,s.X,s.Y-.022f,s.Z,.024f,.05f,.008f,0,0,0,-.2f);});
                    break;
                }
                case 1:
                    // Tufts standing off the hair patches; a flap of scalp torn and hanging.
                    if(k.Mid)for(int i=0;i<(k.Fine?7:3);i++){int x=1+(int)(k.H(i,0xe9)*(HeadNX-2)),z=1+(int)(k.H(i,0xea)*7);var p=g.At(x+.5f,ny,z+.5f);if(g.Get(x,ny-1,z)==0)p.Y-=c;k.Box(ZHair,p.X,p.Y+c*.25f,p.Z,c*.5f,c*.5f+c*.3f*k.H(i,0xeb),c*.6f);}
                    if(k.Mid)k.Strand(ZSkinDark,g.At(HeadNX-1.2f,ny-.6f,7f),new Vector3(.3f,-1,.2f),new Vector3(1,.1f,.3f),.06f,.035f,.3f,2);
                    break;
                case 3:
                    if(k.Mid)k.Strand(ZSkinDark,g.At(1.2f,ny-.6f,3f),new Vector3(-.4f,-1,-.2f),new Vector3(-1,.1f,-.2f),.07f,.04f,.35f,2);
                    break;
            }
        }
        /// <summary>The jaw: a block jaw below the skull's mouth, gums and uneven lower teeth on it, hanging open by the look's slack
        /// (a hinged kind's is its own model on the head joint, opened by NativeHordeJaw about the same hinge).</summary>
        private static void ZJaw(K k,Zb b,Vox g)
        {
            float c=g.C;var hinge=JawHinge(k.Kind);var head=k.S;if(k.SplitJaw){k.S=k.JawSculpt;k.S.Into(CapturedRig.Head);}
            k.S.Push(Matrix4x4.CreateTranslation(-hinge)*Matrix4x4.CreateRotationX(b.Jaw)*Matrix4x4.CreateTranslation(hinge));
            if(k.Far){var a=g.At(2,-2,5);var e=g.At(8,0,10);k.Box(ZSkinDark,(a.X+e.X)*.5f,(a.Y+e.Y)*.5f,(a.Z+e.Z)*.5f,e.X-a.X,e.Y-a.Y,e.Z-a.Z);}
            else
            {
                // Eight cells wide, two deep, five long from the hinge forward; a chipped corner, gums on top behind the teeth.
                var j=new Vox(8,2,5,c,g.At(1,-2,5));int tear=b.Tear;
                Part(k,j,(x,y,z)=>!(y==0&&(x==0||x==7)&&z==4)&&!(y==0&&z==0)&&!(tear!=0&&(tear<0?x==0:x==7)&&z>=3&&y==1),
                    (L,x,y,z,d)=>y==1&&x>=1&&x<=6&&z>=1&&z<=3?L.Gum:y==1&&z==0?L.Dark:Mot(k,L,x,y,z,0xec),0xed,.09f);
                int teeth=k.Fine?5:3;for(int i=0;i<teeth;i++)
                {
                    if(k.H(i,0xee)<.2f)continue;float u=2+5f*(i+.5f)/teeth,len=c*(.4f+.55f*k.H(i,0xef));var t=g.At(u,0,9.55f);
                    k.Box(k.H(i,0xf0)<.3f?ZToothDark:ZTooth,t.X,t.Y+len*.5f,t.Z,c*5f/teeth*.7f,len,c*.6f);
                }
            }
            k.S.Pop();k.S=head;
        }
        /// <summary>The eyes: a steady glow in the kind's colour, a little hotter than the old cube heads' (no flicker code: they never
        /// drop out).</summary>
        private static WeaponPaint ZEye(K k)=>WeaponPaint.Glow(k.SeamColor,2.5f,.75f);

        // ================================================================================================================
        // The neck
        // ================================================================================================================
        private static void ZNeck(K k,Zb b)
        {
            k.On(CapturedRig.Neck);float n=b.Neck,r=Math.Min(.062f*n,.11f*b.HeadW),len=Math.Min(.15f,.15f+b.HeadDrop*.8f);
            if(k.Far){k.Box(ZSkin,0,(len-.04f)*.5f,0,r*2,len+.04f,r*2);return;}
            bool collar=!b.Bare;
            Part(k,Vox.Over(-r,r,-.045f,len,-r,r,.034f),(x,y,z)=>true,(L,x,y,z,d)=>
            {
                if(collar&&y==0)return L.SuitDark;if(collar&&y==1&&k.H(x*7+z,0xf1)<.5f)return L.Fray;
                return d>=1?L.Flesh:Mot(k,L,x,y,z,0xf2);
            },0xf3,.08f,.9f);
            if(k.Fine)k.Zig(new Vector3(.025f,-.005f,r),new Vector3(.3f,1,0),Vector3.UnitZ,3,.024f,.006f,true,.9f,false);
        }

        // ================================================================================================================
        // The torso: pelvis, waist and chest in layers, the tears, the wound and its ribs, the plates
        // ================================================================================================================
        /// <summary>Whether a cell of a torso grid lies in the torn region on side `side` (-1 the -x half): rows y0..y1 with a jagged,
        /// stepped edge, the front and the flank only.</summary>
        private static bool TornAt(K k,Vox v,int x,int y,int z,int side,int y0,int y1,int inset,int salt)
        {
            if(side==0)return false;int mid=v.NX/2;int fromMid=side<0?mid-1-x:x-mid;if(fromMid<inset+Jag(k,y,salt))return false;
            if(y<y0+Jag(k,x,salt+1)||y>y1-Jag(k,x+40,salt+2))return false;return z>=v.NZ/3;
        }
        private static void ZTorso(K k,Zb b)
        {
            float w=b.W,dp=b.Depth;int torn=b.Wound;
            // ---- pelvis: the suit's seat, the belt and its buckle, a hip plate, rags ----
            k.On(CapturedRig.Pelvis);
            if(k.Far){k.Box(ZSuit,0,0,0,.42f*w,.21f,.27f*dp);}
            else
            {
                var pv=Vox.Over(-.21f*w,.21f*w,-.105f,.105f,-.135f*dp,.135f*dp,BodyCell(k,w,.035f));
                Part(k,pv,(x,y,z)=>Chamfer(pv,x,z),(L,x,y,z,d)=>y==pv.NY-1?L.SuitDark:d>=1?L.Skin:L.Suit,0xf4,.05f);
                k.Box(ZShellDark,0,.07f,.135f*dp+.006f,.07f,.045f,.014f,.004f);k.Box(k.Seam(.8f),0,.07f,.135f*dp+.014f,.03f,.012f,.004f);
                if(b.Tatters>0&&k.Mid)for(int i=0;i<(k.Fine?5:2);i++){float a=(.15f+.7f*k.H(i,0xf5))*(float)Math.PI*2;var t=new Vector3((float)Math.Cos(a)*.2f*w,-.09f,(float)Math.Sin(a)*.13f*dp);
                    k.Strand(i%3==0?ZRag:ZSuitDark,t,new Vector3(0,-1,0),new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),.08f+.06f*k.H(i,0xf6),.05f,.12f,2);}
            }
            // ---- the waist ----
            k.On(CapturedRig.Spine);float belly=b.Belly;
            if(k.Far)k.Box(b.Bare?ZSkin:ZSuit,0,.08f,0,.36f*w,.2f,.26f*dp);
            else
            {
                var wv=Vox.Over(-.18f*w,.18f*w,-.035f,.2f,-.125f*dp,.125f*dp,BodyCell(k,w,.035f));
                Part(k,wv,(x,y,z)=>Chamfer(wv,x,z),(L,x,y,z,d)=>
                {
                    if(b.Bare)return d>=1?L.Flesh:Mot(k,L,x,y,z,0xf8);
                    if(TornAt(k,wv,x,y,z,torn,1,wv.NY-2,2,0xf7)){if(d==0)return 0;return d==1?Mot(k,L,x,y,z,0xf8):L.Flesh;}
                    return d>=1?L.Skin:y==0?L.SuitDark:L.Suit;
                },0xf9,.07f);
                // The abs: blocks on a bare belly, in two columns of three.
                if(b.Bare&&k.Mid&&belly<=0)for(int i=0;i<3;i++)foreach(int s in new[]{-1,1})k.Box(ZSkin,s*.045f*w,.03f+.055f*i,.125f*dp+.01f,.07f*w,.045f,.022f);
            }
            // ---- the chest ----
            k.On(CapturedRig.Chest);
            if(k.Far){k.Box(b.Bare?ZSkin:ZSuit,0,.11f,0,.52f*w,.32f,.29f*dp);}
            else ZChest(k,b);
            // The trapezius slope from the neck to the shoulders (a stepped pair of blocks a side).
            if(!k.Far)foreach(int s in new[]{-1,1}){var paint=b.Bare?ZSkin:ZSuit;k.Box(paint,s*.15f*w,.275f,-.02f,.13f*w,.05f*b.Trap,.17f*dp);if(k.Mid)k.Box(paint,s*.09f*w,.3f,-.025f,.07f*w,.04f*b.Trap,.15f*dp);}
            if(b.Hump>0)k.Box(ZSkin,0,.29f,-.08f,.36f*w,.16f*b.Hump,.18f*b.Hump,k.Fine?.01f:0);
            // Bones down the back: vertebrae as stepped bone blocks, the shoulder blades through the skin.
            if(b.Spine&&!k.Far)
            {
                for(int i=0;i<(k.Fine?6:3);i++)k.Box(ZBone,0,.28f-i*.05f,-.145f*dp-.006f,.036f,.026f,.024f);
                foreach(int s in new[]{-1,1})k.Box(ZSkinDark,s*.1f*w,.2f,-.142f*dp,.09f*w,.11f,.02f);
                k.On(CapturedRig.Spine);for(int i=0;i<(k.Fine?3:2);i++)k.Box(ZBone,0,.17f-i*.055f,-.125f*dp-.006f,.032f,.024f,.022f);k.On(CapturedRig.Chest);
            }
            ZPlates(k,b);
        }
        /// <summary>The chest grid: the suit over the skin over the ribs; the tear on the wound's side with a stepped edge, the wound
        /// a layer deeper with its ribs; a starved chest's ribs in recessed rows; veins of ink out of the wound.</summary>
        private static void ZChest(K k,Zb b)
        {
            float w=b.W,dp=b.Depth;int torn=b.Wound;
            var v=Vox.Over(-.26f*w,.26f*w,-.05f,.29f,-.145f*dp,.145f*dp,BodyCell(k,w,.036f));int nx=v.NX,ny=v.NY,nz=v.NZ,mid=nx/2;
            bool Inside(int x,int y,int z)
            {
                if(!Chamfer(v,x,z))return false;
                if(y>=ny-2&&(x==0||x==nx-1))return false;                       // the shoulders' slope
                if(y==ny-1&&(x<=1||x>=nx-2||z==0||z==nz-1))return false;
                if(y==0&&(z==0||z==nz-1))return false;
                return true;
            }
            Part(k,v,Inside,(L,x,y,z,d)=>
            {
                int fromMid=x<mid?mid-1-x:x-mid;
                // The wound: two layers deep inside the tear, its ribs as rows of bone over the dark of the chest.
                bool tear=TornAt(k,v,x,y,z,torn,1,ny-3,1,0xfa),wound=TornAt(k,v,x,y,z,torn,2,ny-4,2,0xfb);
                if(wound&&z>=nz-2&&d<=1)return 0;
                if(wound&&d==2)return y%2==0?L.Bone:L.Deep;
                if(tear&&!b.Bare){if(d==0)return 0;if(d==1)return Mot(k,L,x,y,z,0xfc);}
                if(b.Bare)
                {
                    // A starved chest: rows of rib under the skin, the rows between them sunk a cell into darker skin.
                    bool ribs=b.Ribs>0&&y>=1&&y<=ny-3&&fromMid>=1&&z>=1;
                    if(ribs&&y%2==1){if(d==0)return 0;if(d==1)return L.Dark;}
                    if(ribs&&y%2==0&&d==0)return L.Pale;
                    if(d>=1)return L.Flesh;
                    return Mot(k,L,x,y,z,0xfd);
                }
                if(d>=2)return L.Flesh;if(d==1)return L.Skin;
                // The suit: its lighter chest band and its seams.
                return y>=ny-4&&y<=ny-3&&z==nz-1?L.SuitLight:y==0?L.SuitDark:L.Suit;
            },0xfe,.07f);
            // Veins of ink out of the wound over the skin it bared.
            if(torn!=0&&k.Mid){var a=v.At(torn<0?mid-3.5f:mid+3.5f,ny-3.4f,nz+.02f);k.Zig(a,new Vector3(torn<0?.3f:-.3f,1,0),Vector3.UnitZ,3,.03f,.007f,true,1.1f,false);}
        }
        /// <summary>The chest plate (cracked tiles either side of a sternum ridge carrying the pilot's centre strip; the wound's half
        /// broken off when Plate is negative), the collar plate, the back unit and its cables, the waist's ab plates.</summary>
        private static void ZPlates(K k,Zb b)
        {
            float w=b.W,dp=b.Depth;int torn=b.Wound;
            if(b.Plate!=0)k.Piece(2,null,()=>
            {
                k.On(CapturedRig.Chest);
                foreach(int s in new[]{-1,1})
                {
                    if(b.Plate<0&&torn!=0&&s==torn)continue;bool hurt=torn!=0&&s==torn;
                    var at=K.PlateAt(new Vector3(s*.12f*w,.15f,.146f*dp+.016f),Vector3.Normalize(new Vector3(s*.22f,.05f,1)),Vector3.UnitY);
                    k.Tiles(ZShellDirty,at,.2f*w,.22f,.032f,2,2,hurt?.4f:.18f,.008f,.006f,.07f,.008f,null,true);
                }
                k.Tiles(ZShellDark,K.PlateAt(new Vector3(0,.15f,.146f*dp+.022f),Vector3.UnitZ,Vector3.UnitY),.034f,.2f,.026f,1,2,0,.006f,.002f,.03f,.004f,k.Seam(.9f));
                k.Tiles(ZShellDirty,K.PlateAt(new Vector3(0,.272f,.118f*dp+.012f),Vector3.Normalize(new Vector3(0,.7f,.7f)),Vector3.UnitY),.36f*w,.07f,.028f,3,1,b.Plate<0?.34f:.12f,.008f,.005f,.07f,.006f);
                if(k.Mid)k.Chain(ZWire,new Vector3(b.Plate<0?-.03f:.03f,.06f,.16f*dp),new Vector3(b.Plate<0?.05f:-.05f,-.04f,.16f*dp),.02f);
            });
            if(b.Back)k.Piece(3,null,()=>
            {
                k.On(CapturedRig.Chest);var at=K.PlateAt(new Vector3(0,.12f,-.145f*dp-.024f),-Vector3.UnitZ,Vector3.UnitY);
                k.Tiles(ZShellDark,at,.3f*w,.22f,.042f,2,2,.25f,.008f,.006f,.06f,.008f);
                if(k.Fine)for(int i=0;i<4;i++)k.Box(ZSole,-.05f*w+.033f*w*i,.11f,-.145f*dp-.048f,.012f,.1f,.006f);
                if(k.Mid){k.Chain(ZWire,new Vector3(.06f,.02f,-.17f*dp),new Vector3(.1f,-.09f,-.17f*dp),.02f);k.Chain(ZMetal,new Vector3(-.05f,.015f,-.17f*dp),new Vector3(-.04f,-.07f,-.17f*dp),.012f,.007f);}
            });
            if(b.Plate!=0&&!b.Bare)k.Piece(2,null,()=>
            {
                k.On(CapturedRig.Spine);
                for(int i=0;i<4;i++){int col=i%2,row=i/2;if(k.H(i,0xf6)<.3f||(torn!=0&&(col==0?-1:1)==torn&&row==1))continue;
                    k.Tiles(ZShellDirty,K.PlateAt(new Vector3((col==0?-.062f:.062f)*w,.035f+.08f*row,.125f*dp+.016f),Vector3.UnitZ,Vector3.UnitY),.11f*w,.07f,.028f,1,1,0,.006f,.004f,.06f,.006f);}
            });
            if(!b.Bare&&k.Mid)
            {
                // The collar: the suit's torn neck band as block rags.
                k.On(CapturedRig.Chest);
                for(int i=0;i<(k.Fine?5:2);i++){float a=(float)Math.PI*2*(i+.3f*k.H(i,0xf5))/(k.Fine?7:4);var t=new Vector3((float)Math.Cos(a)*.1f*w,.3f,(float)Math.Sin(a)*.08f*dp);
                    k.Strand(ZSuitDark,t,new Vector3((float)Math.Cos(a)*.3f,-1,(float)Math.Sin(a)*.3f),new Vector3((float)Math.Cos(a),.4f,(float)Math.Sin(a)),.045f+.02f*k.H(i,0xf6),.035f,.25f,2);}
            }
            if(b.Shawl&&k.Mid)
            {
                // The hood's torn shawl over the shoulders and down the back, block rags hanging and swaying.
                k.On(CapturedRig.Chest);
                Part(k,Vox.Over(-.28f*w,.28f*w,.2f,.31f,-.17f*dp,.12f*dp,BodyCell(k,1,.036f)),(x,y,z)=>true,(L,x,y,z,d)=>d>=1?0:(k.Fine&&k.H(x*5+y*31+z*7,0xf7)<.12f?0:L.SuitDark),0xf8,.05f);
                for(int i=0;i<(k.Fine?7:3);i++){float a=(float)Math.PI*(1.05f+.9f*(i+.5f)/(k.Fine?7:3));var t=new Vector3((float)Math.Cos(a)*.27f*w,.21f,(float)Math.Sin(a)*.16f*dp-.005f);
                    k.Strand(i%3==1?ZRag:ZHood,t,new Vector3(0,-1,0),new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),.12f+.1f*k.H(i,0xf8),.07f,.1f,3);}
            }
        }

        // ================================================================================================================
        // Arms: the shoulder and the pauldron, the upper arm and its sleeve, the forearm and its guard, the hand
        // ================================================================================================================
        /// <summary>A limb grid hanging down a joint's -y from `top` to `bottom`, rx x rz, its edges cut, its lower end tapering
        /// (`taper` cells off each side over its lowest third).</summary>
        private static Vox LimbGrid(float top,float bottom,float rx,float rz,float c,float x=0,float z=0)=>Vox.Over(x-rx,x+rx,bottom,top,z-rz,z+rz,c);
        private static bool LimbInside(Vox v,int x,int y,int z,int taper)
        {
            if(v.NX>=4&&v.NZ>=4&&!Chamfer(v,x,z))return false;
            if(taper>0&&y<v.NY/3&&(x<taper&&v.NX>3||x>=v.NX-taper&&v.NX>3)&&(z==0||z==v.NZ-1))return false;
            return true;
        }
        private static void ZArm(K k,Zb b,bool mirror)
        {
            int bit=mirror?2:1;bool pauldron=(b.Pauldron&bit)!=0,brace=(b.Brace&bit)!=0,glove=(b.Glove&bit)!=0;float d=b.Delt,a=b.Arm,f=b.Fore;bool bare=b.Bare||!brace;int salt=mirror?0x40:0;
            // ---- the shoulder ----
            k.On(CapturedRig.LClav,mirror);
            if(k.Far)k.Box(bare?ZSkin:ZSuit,-.07f,-.012f,0,.15f*d,.12f*d,.17f*d);
            else{float sd=Math.Min(d,1.15f);var sv=Vox.Over(-.145f*sd,.005f,-.072f*sd,.058f*sd,-.085f*sd,.085f*sd,BodyCell(k,sd,.034f));
                Part(k,sv,(x,y,z)=>Chamfer(sv,x,z)&&!(y==0&&(x==0||z==0||z==sv.NZ-1)),(L,x,y,z,dd)=>dd>=1?L.Flesh:bare?Mot(k,L,x,y,z,0x10+salt):L.Suit,0x11+salt,.07f);}
            if(pauldron)k.Piece(mirror?4:1,null,()=>
            {
                // The pilot's pauldron: a cracked top slab and an outer slab, one broken at the front.
                float dd=Math.Min(d,1.25f),ds=(float)Math.Sqrt(dd);
                k.Tiles(ZShellDirty,K.PlateAt(new Vector3(-.07f*dd,.066f*dd,0),Vector3.Normalize(new Vector3(-.3f,1,0)),Vector3.UnitZ),.17f*ds,.19f*ds,.032f,2,2,.12f,.008f,.006f,.08f,.008f,null,true);
                k.Tiles(ZShellDirty,K.PlateAt(new Vector3(-.07f*dd-.07f*ds,-.006f,0),Vector3.Normalize(new Vector3(-1,-.15f,0)),Vector3.UnitY),.17f*ds,.1f*ds,.028f,2,1,.3f,.008f,.006f,.08f,.008f);
                if(k.Mid)k.Chain(ZWire,new Vector3(-.07f*dd-.075f*ds,-.06f,.06f),new Vector3(-.07f*dd-.075f*ds,-.13f,.07f),.012f);
            });
            else if(!b.Bare&&k.Mid)for(int i=0;i<2;i++)k.Strand(ZSuitDark,new Vector3(-.09f*d,-.05f,-.03f+.06f*i),new Vector3(-.2f,-1,0),new Vector3(-1,0,(i-.5f)),.06f+.02f*i,.035f,.2f,2);
            // ---- the upper arm: the sleeve (the armoured arm) or the bare arm with the sleeve's torn stub ----
            k.On(CapturedRig.LArm,mirror);
            if(k.Far)k.Box(bare?ZSkin:ZSuit,0,-.13f,0,.12f*a,.27f,.125f*a);
            else
            {
                float rx=(bare?.058f:.066f)*a,rz=(bare?.06f:.066f)*a;var v=LimbGrid(.03f,-.275f,rx,rz,BodyCell(k,a,.033f));int ny=v.NY;bool stub=bare&&!b.Bare;
                Part(k,v,(x,y,z)=>LimbInside(v,x,y,z,1),(L,x,y,z,dd)=>
                {
                    if(stub&&y>=ny-2-Jag(k,x*3+z,0x12+salt))return dd==0?(y==ny-1?L.SuitDark:L.Suit):L.Skin;
                    if(!bare)return dd>=1?L.Skin:L.Suit;
                    // A wound on the outside of the arm: a cell bitten in, the flesh under it, a vein down from it.
                    if(x==0&&y==ny/2&&z==v.NZ/2&&dd==0)return 0;if(x==1&&y==ny/2&&z==v.NZ/2)return L.Flesh;
                    return dd>=1?L.Flesh:Mot(k,L,x,y,z,0x13+salt);
                },0x14+salt,.08f,.9f);
                if(k.Fine&&bare)k.Box(ZBone,0,-.282f,0,.04f*a,.016f,.04f*a);
                if(bare&&k.Mid){var p=v.Centre(0,ny/2-1,v.NZ/2);p.X=v.O.X;k.Zig(p,new Vector3(0,-1,.25f),-Vector3.UnitX,3,v.C*.9f,.006f,true,.9f,false);}
            }
            if(brace)k.Piece(mirror?4:1,null,()=>
            {
                // The armoured sleeve: cracked slabs round the outside and front of the upper arm.
                k.Tiles(ZShellDirty,K.PlateAt(new Vector3(-.078f*a,-.13f,0),-Vector3.UnitX,Vector3.UnitY),.15f*a,.19f,.03f,2,2,.15f,.007f,.005f,.07f,.008f,null,true);
                k.Tiles(ZShellDirty,K.PlateAt(new Vector3(-.01f,-.13f,.078f*a),Vector3.UnitZ,Vector3.UnitY),.12f*a,.17f,.028f,1,2,.2f,.007f,.005f,.07f,.007f);
            });
            // ---- the forearm ----
            k.On(CapturedRig.LElbow,mirror);
            if(k.Far)k.Box(bare?ZSkin:ZSuit,0,-.11f,0,.1f*f,.24f,.1f*f);
            else
            {
                k.Box(bare?ZSkinDark:ZSuit,0,0,-.012f,.09f*f,.07f,.085f*f);if(bare&&k.Mid)k.Box(ZBone,0,.004f,-.05f*f,.03f,.03f,.02f);
                float rx=(bare?.05f:.058f)*f,rz=(bare?.046f:.055f)*f;var v=LimbGrid(-.012f,-.24f,rx,rz,BodyCell(k,f,.032f));int ny=v.NY;
                Part(k,v,(x,y,z)=>LimbInside(v,x,y,z,1),(L,x,y,z,dd)=>
                {
                    if(!bare)return dd>=1?L.Skin:(y==0?L.SuitDark:L.Suit);
                    // The rotted forearm: the bone bared down its outside, the flesh round it, a vein to the wrist.
                    if(!b.Fist&&x==0&&y>=2&&y<=4&&z==v.NZ/2){if(dd==0)return L.Bone;}
                    if(!b.Fist&&x==0&&y>=2&&y<=4&&(z==v.NZ/2-1||z==v.NZ/2+1)&&dd==0)return L.Flesh;
                    return dd>=1?L.Flesh:Mot(k,L,x,y,z,0x15+salt);
                },0x16+salt,.08f,.9f);
                if(bare&&k.Mid){var p=v.Centre(v.NX-1,ny-2,v.NZ-1);p.X=v.O.X+v.NX*v.C;k.Zig(p,new Vector3(0,-1,-.3f),Vector3.UnitX,3,v.C*.9f,.006f,true,.9f,false);}
            }
            if(brace)k.Piece(mirror?4:1,null,()=>
            {
                // The forearm guard: cracked slabs round the outside and back, the pilot's strip down it (the ink on the right arm).
                k.Tiles(ZShellDirty,K.PlateAt(new Vector3(-.068f*f,-.125f,0),-Vector3.UnitX,Vector3.UnitY),.13f*f,.18f,.03f,1,2,.1f,.007f,.005f,.07f,.007f,mirror?k.Seam(.8f):(WeaponPaint?)ZDeadAccent,true);
                k.Tiles(ZShellDirty,K.PlateAt(new Vector3(0,-.13f,.066f*f),Vector3.UnitZ,Vector3.UnitY),.11f*f,.16f,.026f,1,2,.25f,.007f,.005f,.07f,.007f);
            });
            ZHand(k,b,mirror,glove);
        }
        /// <summary>A hand in blocks: the palm, four fingers as stepped blocks curled into hooks with nails or claws, the thumb; gloved
        /// on an armoured arm (the claws through the glove), a fist of knuckles on the big kinds.</summary>
        private static void ZHand(K k,Zb b,bool mirror,bool glove)
        {
            k.On(CapturedRig.LHand,mirror);float h=b.Hand,fl=b.Finger;var palm=glove?ZSuitDark:ZSkin;
            if(b.Fist)
            {
                // A fist like a block: the knuckles a row of blocks, their bone through the skin, the thumb across.
                k.Box(ZSkin,0,-.06f*h,.035f*h,.14f*h,.12f*h,.13f*h,k.Fine?.008f:0);
                if(k.Far)return;
                for(int i=0;i<4;i++){float x=(i-1.5f)*.036f*h;k.Box(i%2==0?ZSkinDark:ZSkin,x,-.128f*h,.05f*h,.034f*h,.04f*h,.06f*h);if(k.Mid)k.Box(ZBone,x,-.146f*h,.072f*h,.02f*h,.016f*h,.014f*h);}
                k.Box(ZSkinDark,.06f*h,-.09f*h,.07f*h,.03f*h,.06f*h,.04f*h);return;
            }
            k.Box(palm,0,-.045f*h,.005f,.11f*h,.08f*h,.04f*h);k.Box(palm,0,-.05f*h,.005f,.1f*h,.07f*h,.012f+.02f*h);
            if(glove&&!k.Far)k.Tiles(ZShellDark,K.PlateAt(new Vector3(0,-.02f*h,.034f*h),Vector3.UnitZ,Vector3.UnitY),.1f*h,.05f*h,.018f,2,1,.2f,.005f,.003f,.06f,.004f);
            if(k.Far){k.Box(glove?palm:ZSkinDark,0,-.11f*h,.03f,.1f*h,.07f*h,.06f);return;}
            int n=k.Fine?4:3;float claw=b.Claw;
            for(int i=0;i<n;i++)
            {
                float u=(i+.5f)/n-.5f,x=u*.09f*h,fw=.02f*h,len=fl*(.9f+.2f*(float)Math.Cos(u*3));var fp=glove&&i!=1?ZSuitDark:ZSkinDark;
                // Three stepped blocks curling forward: the knuckle, the middle, the tip; the nail or claw at its end.
                if(k.Fine){k.Box(fp,x,-.095f*h,.022f,fw,.032f*len,fw*1.05f);k.Box(fp,x,-.12f*h*len,.04f,fw*.95f,.026f*len,fw);k.Box(fp,x,-.137f*h*len,.058f,fw*.9f,.02f*len,fw*.95f);}
                else k.Box(fp,x,-.11f*h*len,.035f,fw,.05f*len,fw*1.4f);
                k.Box(claw>.02f?ZBone:ZNail,x,-.148f*h*len-claw*.3f,.068f+claw*.35f,fw*.7f,.012f+claw*.6f,fw*.6f);
            }
            k.Box(palm,.062f*h,-.05f*h,.034f,.026f*h,.04f*h,.026f*h);if(k.Mid)k.Box(ZSkinDark,.066f*h,-.08f*h,.05f,.022f*h,.03f*h,.022f*h);
        }

        // ================================================================================================================
        // Legs: the thigh, the knee and its pad, the shin and its plate, the boot or the bare foot
        // ================================================================================================================
        private static void ZLeg(K k,Zb b,bool mirror)
        {
            int bit=mirror?2:1;float l=b.Leg,s=b.Shin;bool thighTear=(b.ThighTear&bit)!=0,shinTear=(b.ShinTear&bit)!=0,pad=(b.KneePad&bit)!=0,plate=(b.ShinPlate&bit)!=0;int salt=mirror?0x40:0;
            k.On(CapturedRig.LHip,mirror);
            if(k.Far)k.Box(ZSuit,0,-.1f,0,.19f*l,.29f,.21f*l);
            else
            {
                var v=LimbGrid(.04f,-.25f,.094f*l,.104f*l,BodyCell(k,l,.035f));int ny=v.NY;
                Part(k,v,(x,y,z)=>LimbInside(v,x,y,z,0),(L,x,y,z,dd)=>
                {
                    // The torn thigh: the suit's front opened with a stepped edge onto the skin, a bite of wound in it.
                    if(thighTear&&z>=v.NZ-2&&y>=1+Jag(k,x,0x18+salt)&&y<=ny-3-Jag(k,x+9,0x19+salt)&&x<=v.NX-2)
                    {if(dd==0)return 0;if(x==1&&y==ny/2&&dd==1)return 0;if(dd==1)return Mot(k,L,x,y,z,0x1a+salt);return L.Flesh;}
                    return dd>=1?L.Skin:y==ny-1?L.SuitDark:L.Suit;
                },0x1b+salt,.06f);
            }
            k.On(CapturedRig.LKnee,mirror);bool wasted=b.Leg<.7f;
            // The shin ends short of the ankle (shorter and no deeper on the big kinds), its front lowest edge cut, and a wasted leg
            // has no knee block, so a deeply bent knee never pushes a block through the floor.
            if(k.Far){k.Box(ZSuit,0,-.1f,0,.17f*s,.2f,.17f*Math.Min(s,1.1f));}
            else
            {
                if(!wasted)k.Box(ZSuit,0,0,0,.15f*Math.Min(s,1.2f),.08f,.16f*Math.Min(s,1.1f));
                var v=LimbGrid(-.03f,-.2f+.03f*Math.Max(0,s-1),.084f*Math.Min(s,1.2f),.09f*Math.Min(s,1.1f),BodyCell(k,s,.034f));int ny=v.NY;
                Part(k,v,(x,y,z)=>LimbInside(v,x,y,z,0)&&!(y==0&&z==v.NZ-1),(L,x,y,z,dd)=>
                {
                    if(shinTear&&z>=v.NZ-2&&y>=Jag(k,x,0x1c+salt)&&y<=ny-2&&x>=1)
                    {if(dd==0)return 0;if(dd==1)return Mot(k,L,x,y,z,0x1d+salt);return L.Flesh;}
                    return dd>=1?L.Skin:y==0?L.SuitDark:L.Suit;
                },0x1e+salt,.06f);
                float cuff=-.2f+.03f*Math.Max(0,s-1);k.Box(ZSuitDark,0,cuff-.004f,0,.12f*Math.Min(s,1.2f),.034f,.12f*Math.Min(s,1.1f));
            }
            float sz=Math.Min(s,1.1f);
            if(pad)k.Piece(mirror?4:1,null,()=>k.Tiles(ZShellDark,K.PlateAt(new Vector3(0,-.01f,.1f*sz),Vector3.UnitZ,Vector3.UnitY),.14f*Math.Min(s,1.2f),.11f,.04f,2,1,.2f,.007f,.004f,.07f,.008f));
            else if(!k.Far&&!wasted)k.Box(ZSkinDark,0,-.005f,.085f*sz,.1f*Math.Min(s,1.2f),.07f,.03f);
            if(plate)k.Piece(mirror?4:1,null,()=>k.Tiles(ZShellDirty,K.PlateAt(new Vector3(0,-.1f,.09f*sz+.006f),Vector3.UnitZ,Vector3.UnitY),.13f*Math.Min(s,1.2f),.12f,.024f,1,2,.15f,.007f,.002f,.05f,.007f,k.Seam(.7f),true));
            // ---- the foot ----
            k.On(CapturedRig.LFoot,mirror);float ft=b.Foot;
            if(b.Barefoot)
            {
                k.Box(ZSkin,0,-.03f,-.012f,.11f*ft,.07f,.14f*ft);if(!k.Far)k.Box(ZSkinDark,0,-.05f,-.05f*ft,.1f*ft,.04f,.04f*ft);
                k.On(CapturedRig.LToe,mirror);
                if(k.Far){k.Box(ZSkinDark,0,-.015f,.02f,.11f*ft,.04f,.11f*ft);return;}
                k.Box(ZSkinDark,0,-.02f,.0f,.11f*ft,.04f,.06f*ft);
                for(int i=0;i<(k.Fine?4:2);i++){float x=(i-(k.Fine?1.5f:.5f))*.027f*ft;k.Box(ZSkin,x,-.024f,.045f*ft,.022f*ft,.03f,.035f*ft);if(k.Mid)k.Box(ZNail,x,-.03f,.066f*ft,.018f*ft,.014f,.01f);}
                return;
            }
            // The boot: a stacked sole, the upper, a cracked toe cap.
            k.Box(ZSole,0,-.055f,-.035f,.15f*ft,.035f,.19f*ft);k.Box(ZBoot,0,-.015f,-.04f,.13f*ft,.05f,.17f*ft);
            if(k.Mid)k.Box(ZSuitDark,0,.02f,-.04f,.12f*ft,.02f,.14f*ft);
            k.On(CapturedRig.LToe,mirror);
            k.Box(ZSole,0,-.032f,.01f,.14f*ft,.03f,.13f*ft);k.Box(ZBoot,0,-.003f,.004f,.125f*ft,.035f,.11f*ft);
            if(!k.Far)k.Tiles(ZShellDark,K.PlateAt(new Vector3(0,.0f,.062f*ft),Vector3.Normalize(new Vector3(0,.5f,1)),Vector3.UnitY),.11f*ft,.036f,.014f,2,1,.25f,.005f,.002f,.05f,.004f);
        }
    }
}

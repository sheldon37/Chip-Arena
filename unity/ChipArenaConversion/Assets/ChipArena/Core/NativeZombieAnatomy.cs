using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The rotted pilot's anatomy for the Siren's zombies (docs/ZOMBIE-DESIGN.md; the kit is Core/NativeZombieKit.cs, the kinds
    // Core/NativeZombieKinds.cs). Each zombie is a pilot the Siren took: the pilot's rounded-cube skull and big-headed build, its
    // dark undersuit and pale armour shells, rotted and torn. A sculpted face (heavy brow over deep sockets with the kind's glow
    // in them, cheekbones over sunken cheeks, the nose rotted to a cavity, gums and uneven teeth bared, a cheek torn open to the
    // molars on some looks, the hanging jaw with its own teeth and tongue, sinews across the tear); a muscled body of smooth
    // limbs; real hands with three-jointed fingers, nails and claws; the undersuit torn in jagged holes with frayed tatters that
    // hang and sway; the armour as curved shells with broken, chipped edges, panel seams, dents, cracks, dead accent strips and
    // wiring hanging out; wounds of wet flesh with ribs, the Siren's ink glowing in branching veins out of them. Every part
    // rides the joint the pilot's part rides (one section per joint) and the joints a severed limb leaves show a stump.
    // Left limbs are authored at bank -x and mirrored for the right.
    public static partial class NativeHordeModels
    {
        // ---- the rotted pilot's palette (sRGB), each on its ZombieLit surface class ----
        private static WeaponPaint Z(int hex,int surf,float k=1)=>new WeaponPaint(WeaponPaint.Linear(hex)*k,Vector3.Zero,0,surf);
        private static readonly WeaponPaint
            ZSkin=Z(0x97a08f,SurfSkin),ZSkinPale=Z(0xaab0a2,SurfSkin),ZSkinDark=Z(0x69725f,SurfSkin),ZRotten=Z(0x535c47,SurfSkin),
            ZFlesh=Z(0x6a1c24,SurfFlesh),ZFleshDeep=Z(0x2e0a10,SurfFlesh),ZMuscle=Z(0x8a3a3c,SurfFlesh),ZGum=Z(0x5a1a20,SurfFlesh),ZTongue=Z(0x6e2a30,SurfFlesh),ZMouth=Z(0x1a0507,SurfFlesh),
            ZBone=Z(0xd0c6a6,SurfBone),ZTooth=Z(0xc7b98f,SurfBone),ZToothDark=Z(0x8f7f5c,SurfBone),ZNail=Z(0x2e2620,SurfBone),
            ZSuit=Z(0x343d48,SurfCloth),ZSuitLight=Z(0x56616d,SurfCloth),ZSuitDark=Z(0x1d232a,SurfCloth),ZSuitFray=Z(0x5a5c58,SurfCloth),ZRag=Z(0x3a3832,SurfCloth),ZHood=Z(0x2a2433,SurfCloth),
            ZShell=Z(0xd0d3cf,SurfShell),ZShellDirty=Z(0xb9bdb8,SurfShell),ZShellDark=Z(0x5d6368,SurfShell),ZMetal=Z(0x8f969c,SurfWire),
            ZBoot=Z(0x2a3036,SurfCloth),ZSole=Z(0x15181b,SurfMatte),ZHair=Z(0x1e1a15,SurfMatte),ZSocket=Z(0x0b0607,SurfMatte),ZWire=Z(0x24282c,SurfWire),ZVisor=Z(0x0f171b,SurfShell),
            ZDeadAccent=Z(0x173230,SurfShell);
        private static WeaponPaint SeamDark=>Z(0x101215,SurfMatte);
        private static WeaponPaint Copper=>Z(0xb36b3c,SurfWire);

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
        // The head: the sculpted skull on the pilot's rounded-cube head, the eyes, gums and teeth, the jaw, the scalp
        // ================================================================================================================
        // Warps that crowd the grid's rows round the face's middle and its columns round the front.
        private static float WarpRow(float u){float t=2*u-1;const float a=.55f;return .5f+.5f*t*(a+(1-a)*t*t);}
        private static float WarpCol(float v){float t=2*v-1;const float a=.45f;return .25f+.5f*t*(a+(1-a)*t*t);}
        private static float Smooth(float a,float b,float x){float t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return t*t*(3-2*t);}
        private static float G(float x,float y,float sx,float sy)=>(float)Math.Exp(-(x*x/sx+y*y/sy));
        /// <summary>The face's features at a unit skull point q (x right, y up, z front): socket, nose, nostrils, mouth masks.</summary>
        private static float SocketOf(Vector3 q)=>Math.Max(G(q.X-.4f,q.Y-.05f,.045f,.028f),G(q.X+.4f,q.Y-.05f,.045f,.028f));
        private static float NoseOf(Vector3 q)=>G(q.X,q.Y+.27f,.01f,.02f);
        private static float NostrilOf(Vector3 q)=>Math.Max(G(q.X-.075f,q.Y+.33f,.003f,.005f),G(q.X+.075f,q.Y+.33f,.003f,.005f));
        private static float MouthOf(Vector3 q)=>Smooth(-.47f,-.62f,q.Y)*Smooth(.6f,.42f,Math.Abs(q.X));
        /// <summary>The skull's push along the radial (m) at a unit point q: the sockets, brow, cheekbones and hollows, the rotted nose,
        /// the mouth's recess, the torn cheek, the scalp's hair or bared bone.</summary>
        private static float FaceDisp(Vector3 q,Zb b)
        {
            float front=Smooth(.15f,.6f,q.Z),fx=q.X,fy=q.Y,d=0;float brow=b.Top==4?1.5f:1;
            d-=.052f*SocketOf(q)*front;
            for(int s=-1;s<=1;s+=2)
            {
                d+=brow*.026f*G(fx-s*.38f,fy-.27f,.07f,.01f)*front;                // the brow shelf over each socket
                d+=.02f*G(fx-s*.62f,fy+.18f,.03f,.014f)*front;                      // the cheekbones
                d-=.04f*G(fx-s*.6f,fy+.52f,.05f,.04f)*front;                        // the hollow cheeks
                d-=.018f*G(fx-s*.9f,fy-.12f,.025f,.05f)*Smooth(.3f,.7f,Math.Abs(fx)); // the temples
                if(s==b.Tear)d-=.016f*G(fx-s*.6f,fy+.58f,.045f,.03f);              // the torn cheek, sunk to the molars
            }
            d+=brow*.008f*G(fx,fy-.22f,.012f,.01f)*front;                           // the frown between the brows
            d+=.007f*G(fx,fy+.08f,.003f,.02f)*front;                                // the bridge
            d-=.03f*NoseOf(q)*front;d-=.016f*NostrilOf(q)*front;                     // the nose rotted to its cavity
            d+=.012f*G(fx,fy-.05f,1,.3f)*Smooth(-.2f,-.75f,q.Z);                    // the back of the skull, fuller
            d-=.012f*MouthOf(q)*front;                                              // the mouth's recess behind the teeth
            if(b.Top==3&&fy>.66f&&fx<.15f&&fx>-.6f)d-=.006f;                        // the torn scalp
            return d;
        }
        /// <summary>The skull's centre and radii on the head joint for a kind's head width and drop.</summary>
        private static void SkullOf(Zb b,out Vector3 c,out Vector3 r){c=new Vector3(0,.195f+b.HeadDrop-.142f*(1-b.HeadH)*.5f,-.014f);r=new Vector3(.148f*b.HeadW,.142f*b.HeadH,.15f*b.HeadW);}
        private const float SkullPower=2.7f;
        /// <summary>The unit skull point (the rounded cube) for a direction.</summary>
        private static Vector3 SkullUnit(Vector3 d){float s=(float)Math.Pow(Math.Pow(Math.Abs(d.X),SkullPower)+Math.Pow(Math.Abs(d.Y),SkullPower)+Math.Pow(Math.Abs(d.Z),SkullPower),1/SkullPower);return d/Math.Max(1e-6f,s);}
        /// <summary>The sculpted skull's surface for a direction: the rounded cube narrowed to a gaunt lower face, the features pushed in.</summary>
        private static Vector3 SkullAt(Zb b,Vector3 d)
        {
            SkullOf(b,out var c,out var r);var q=SkullUnit(d);var local=q*r;
            if(q.Y<0){float t=(float)Math.Pow(-q.Y,1.5);local.X*=1-.17f*t;if(q.Z<0)local.Z*=1-.1f*t;}
            return c+local+Vector3.Normalize(q*r)*FaceDisp(q,b);
        }
        /// <summary>A point on the sculpted face at unit-skull (x, y) on the front, its outward normal.</summary>
        private static Vector3 Face(Zb b,float fx,float fy,out Vector3 n)
        {
            float z=(float)Math.Pow(Math.Max(1e-4,1-Math.Pow(Math.Abs(fx),SkullPower)-Math.Pow(Math.Abs(fy),SkullPower)),1/SkullPower);var d=Vector3.Normalize(new Vector3(fx,fy,z));
            var p=SkullAt(b,d);var e=.012f;var px=SkullAt(b,Vector3.Normalize(d+new Vector3(e,0,0)));var py=SkullAt(b,Vector3.Normalize(d+new Vector3(0,e,0)));
            n=Vector3.Normalize(Vector3.Cross(px-p,py-p));if(n.Z<0&&d.Z>0)n=-n;return p;
        }
        private static Vector3 Rotate(Vector3 v,float ax,float ay,float az)=>Vector3.Transform(v,Matrix4x4.CreateRotationX(ax)*Matrix4x4.CreateRotationY(ay)*Matrix4x4.CreateRotationZ(az));
        /// <summary>Whether a scalp point keeps its hair (patches torn out round two hashed bald spots).</summary>
        private static bool HairAt(K k,Vector3 q)
        {
            if(q.Y<.42f||q.Y<.6f&&q.Z>.55f)return false;
            for(int i=0;i<3;i++){var spot=Vector3.Normalize(new Vector3(k.H(i,0xe7)*2-1,1.2f,k.H(i,0xe8)*2-1));if(Vector3.Dot(Vector3.Normalize(q),spot)>.93f+.02f*i)return false;}
            return true;
        }
        private static void ZHead(K k,Zb b)
        {
            k.On(CapturedRig.Head);SkullOf(b,out var c,out var r);int tear=b.Tear;
            if(k.Far)
            {
                // The impostor: the skull, the eyes, the dark mouth, the jaw hanging.
                k.Mass(ZSkin,c,r,SkullPower,null,6,8);foreach(int s in new[]{-1,1}){var e=Face(b,s*.4f,.05f,out _);k.Box(EyeGlow(k),e.X,e.Y,e.Z-.002f,.04f*b.Eye,.026f*b.Eye,.016f);}
                var m=Face(b,0,-.62f,out _);k.Box(ZMouth,m.X,m.Y,m.Z-.004f,.1f*b.HeadW,.03f,.012f);ZJaw(k,b);return;
            }
            int R=k.Fine?18:11,C=k.Fine?24:14;var pts=new Vector3[R][];var qs=new Vector3[R][];
            for(int i=0;i<R;i++){pts[i]=new Vector3[C];qs[i]=new Vector3[C];for(int j=0;j<C;j++)
            {
                double ph=Math.PI*WarpRow(i/(R-1f)),th=Math.PI*2*WarpCol(j/(float)C);var d=new Vector3((float)(Math.Sin(ph)*Math.Cos(th)),(float)Math.Cos(ph),(float)(Math.Sin(ph)*Math.Sin(th)));
                var q=SkullUnit(d);qs[i][j]=q;var p=SkullAt(b,d);if(b.Top==1&&HairAt(k,q))p+=Vector3.Normalize(p-c)*.004f;pts[i][j]=p;
            }}
            WeaponPaint? Paint(int i,int j)
            {
                var q=(qs[i][j]+qs[i+1][j]+qs[i][(j+1)%C]+qs[i+1][(j+1)%C])*.25f;float front=q.Z;
                if(b.Top==0&&q.Y>.8f&&q.Z<.3f)return null;// under the helmet's top plate
                if(front>.3f)
                {
                    float so=SocketOf(q);if(so>.6f)return ZSocket;if(so>.38f)return ZFleshDeep;if(so>.22f)return ZRotten;
                    if(NostrilOf(q)>.45f)return ZSocket;if(NoseOf(q)>.5f)return ZRotten;
                    float mo=MouthOf(q);if(mo>.5f)return q.Y<-.6f?ZMouth:ZGum;if(mo>.25f)return ZFlesh;
                    if(tear!=0&&q.X*tear>.42f&&q.Y<-.3f&&q.Y>-.82f&&k.H(i*37+j,0xd1)<.65f+(q.X*tear-.42f))return q.Y<-.5f?ZFleshDeep:ZFlesh;
                }
                if(b.Top==1&&HairAt(k,q))return ZHair;
                if(b.Top==3&&q.Y>.66f&&q.X<.15f&&q.X>-.6f)return q.Y>.8f&&q.X<.05f&&q.X>-.45f?ZBone:ZFlesh;
                if(q.Y<-.55f&&front<.2f)return ZSkinDark;
                return ZSkin;
            }
            k.Prim(()=>{k.Grid(pts,true,Paint);k.Occ(c,(r.X+r.Y+r.Z)/3*.93f);});
            // The eyes: the kind's glow small and deep in each socket.
            foreach(int s in new[]{-1,1}){var e=Face(b,s*.4f,.05f,out var en);k.Mass(EyeGlow(k),e+en*.004f,new Vector3(.0145f,.011f,.008f)*b.Eye,2,null,5,8,false);}
            // The upper teeth under the gum, blocky and uneven, some broken or gone; the molars through a torn cheek.
            int teeth=k.Fine?10:5;var gp=new Vector3[teeth+1];var gn=new Vector3[teeth+1];
            for(int i=0;i<=teeth;i++){float fx=-.44f+.88f*i/teeth;gp[i]=Face(b,fx,-.55f-.08f*fx*fx,out gn[i]);}
            if(k.Mid){var gs=new Vector3[teeth+1];for(int i=0;i<=teeth;i++)gs[i]=gp[i]+new Vector3(0,.004f,0)-gn[i]*.003f;k.Strip(ZGum,gs,gn,.016f,1,.001f);}
            for(int i=0;i<teeth;i++)
            {
                if(k.H(i,0xd3)<.12f&&Math.Abs(i-teeth/2+.5f)>1)continue;var root=(gp[i]+gp[i+1])*.5f+(gn[i]+gn[i+1])*.004f;var o=Vector3.Normalize(gn[i]+gn[i+1]);
                float cen=Math.Abs((i+.5f)/teeth-.5f)*2;float len=(.017f-.004f*cen+.005f*k.H(i,0xd4))*(k.H(i,0xd5)<.14f?.55f:1);
                k.ToothAt(k.H(i,0xd6)<.25f?ZToothDark:ZTooth,root,new Vector3(0,-1,.12f),o,Vector3.Distance(gp[i],gp[i+1])*.96f,len,.012f,cen>.6f&&cen<.85f?.55f:.86f);
            }
            if(tear!=0&&k.Fine)for(int i=0;i<3;i++){var p=Face(b,tear*(.56f+.09f*i),-.57f,out var n);k.ToothAt(ZToothDark,p-n*.004f,new Vector3(0,-1,0),n,.014f,.017f,.012f,.8f);}
            // The skull's base (the flying head's stump).
            if(k.Fine)k.Cap(ZFlesh,Ring(new Vector3(0,c.Y-r.Y-.002f,-.02f),.065f*b.HeadW,.06f*b.HeadW,8),new Vector3(0,-1,0));
            ZScalp(k,b);ZJaw(k,b);
        }
        /// <summary>A ring of `n` points round a centre in the xz plane.</summary>
        private static Vector3[] Ring(Vector3 c,float rx,float rz,int n){var o=new Vector3[n];for(int i=0;i<n;i++){double a=Math.PI*2*i/n;o[i]=c+new Vector3((float)Math.Cos(a)*rx,0,(float)Math.Sin(a)*rz);}return o;}
        /// <summary>The scalp by the look: the cracked helmet, patchy hair and a torn flap, the split crown's light, bared skull.</summary>
        private static void ZScalp(K k,Zb b)
        {
            SkullOf(b,out var c,out var r);var scalp=k.Mass((i,q)=>null,c,r,SkullPower,null,8,8,null,false);
            switch(b.Top)
            {
                case 0:
                {
                    // The pilot's cube helmet, its face plate and visor smashed in, cracked and twisted back off the skull; a visor shard
                    // hanging at the temple.
                    // The helmet as the pilot's: a box of plates round the skull (top, back, sides, the brow of the smashed face plate),
                    // turned a little back and askew about the skull's centre.
                    var hc=new Vector3(0,.204f+b.HeadDrop,-.016f);float hx=.158f*b.HeadW,hy=.126f,hz=.156f*b.HeadW,t=.028f;
                    var tw=Matrix4x4.CreateTranslation(-hc)*Matrix4x4.CreateRotationX(-.08f)*Matrix4x4.CreateRotationZ(.05f)*Matrix4x4.CreateTranslation(hc+new Vector3(0,.004f,-.004f));
                    Matrix4x4 P(Vector3 at,Vector3 face,Vector3 up)=>K.PlateAt(hc+at,face,up)*tw;
                    k.Piece(0,null,()=>
                    {
                        k.Armor(ZShellDirty,ZMetal,P(new Vector3(0,hy+t*.5f,0),Vector3.UnitY,-Vector3.UnitZ),hx*2+t,hz*2+t,t,3,.4f,0,null,1,true,.012f);
                        k.Armor(ZShellDirty,ZMetal,P(new Vector3(0,-.01f,-hz-t*.5f),-Vector3.UnitZ,Vector3.UnitY),hx*2+t,hy*2-.02f,t,-1,.4f,1,null,0,false,.012f);
                        k.Armor(ZShellDirty,ZMetal,P(new Vector3(-hx-t*.5f,-.005f,-.01f),-Vector3.UnitX,Vector3.UnitY),hz*2-.01f,hy*2-.01f,t,2,.52f,0,null,0,false,.012f);
                        k.Armor(ZShellDirty,ZMetal,P(new Vector3(hx+t*.5f,.02f,-.035f),Vector3.UnitX,Vector3.UnitY),hz*2-.06f,hy*2-.06f,t,3,.6f,0,null,1,true,.012f);
                        k.Armor(ZShellDark,ZMetal,P(new Vector3(.02f,hy-.012f,hz-.004f),Vector3.Normalize(new Vector3(0,.25f,1)),Vector3.UnitY),.2f,.05f,.026f,1,.6f,1,k.Seam(.9f),0,false,.01f);

                    });
                    if(k.Mid)k.Piece(1,q=>Math.Max(0,(.17f-q.Y)/.06f),()=>
                    {
                        var at=Face(b,-.82f,.22f,out var n);var down=new Vector3(-.15f,-1,.1f);var side=Vector3.Normalize(Vector3.Cross(n,down));var o=at+n*.012f;
                        k.Facing(ZVisor,o-side*.02f,o+side*.025f,o+down*.055f+side*.006f,o+down*.04f-side*.015f,n);k.Facing(ZMetal,o-side*.02f,o+side*.025f,o+down*.055f+side*.006f,o+down*.04f-side*.015f,-n);
                    });
                    break;
                }
                case 1:
                    // Clumps of hair standing off the patches, a flap of scalp torn and hanging, a vein over the bald spot.
                    if(k.Mid)for(int i=0;i<(k.Fine?10:4);i++){var d=Vector3.Normalize(new Vector3(k.H(i,0xe1)*2-1,.5f+k.H(i,0xe2),k.H(i,0xe3)*1.6f-1));if(!HairAt(k,SkullUnit(d)))continue;var p=SkullAt(b,d);var n=Vector3.Normalize(p-c);
                        k.Tube(ZHair,new[]{p-n*.004f,p+n*.012f+new Vector3(0,0,-.012f),p+n*.016f+new Vector3(k.H(i,0xe4)*.02f-.01f,-.01f,-.03f)},new[]{.008f,.006f,.001f},4,true,false);}
                    if(k.Mid)k.Tatter(ZSkinDark,c+new Vector3(.075f*b.HeadW,r.Y*.82f,.05f),new Vector3(.45f,-1,.25f),new Vector3(.85f,.15f,.5f),.07f,.04f,.3f);
                    k.Vein(scalp,.3f,.7f,.03f,.02f,4,.005f,1.1f,1,3);
                    break;
                case 2:
                    // The split crown: a jagged crack from the brow over the scalp, the Siren's light pouring out of it, its lips of bone.
                    k.Crack(scalp,.42f,.27f,-.05f,.012f,6,.017f,true,1.5f);if(k.Fine)k.StripOn(ZBone,scalp,new[]{(.42f,.245f),(.34f,.255f),(.25f,.245f),(.16f,.26f)},.006f,1,.0025f);
                    k.Vein(scalp,.42f,.27f,.03f,.035f,4,.006f,1.2f,1,5);
                    if(!k.Fine){var p=Face(b,.15f,.62f,out _);k.Box(k.Seam(1.4f),p.X,p.Y,p.Z,.016f,.06f,.012f,0,0,0,.4f);}
                    break;
                case 3:
                    if(k.Mid)k.Tatter(ZSkinDark,c+new Vector3(-.09f*b.HeadW,r.Y*.78f,-.02f),new Vector3(-.5f,-1,-.2f),new Vector3(-.9f,.1f,-.2f),.08f,.05f,.4f);
                    k.Vein(scalp,.3f,.85f,.04f,-.03f,4,.005f,1,1,7);
                    break;
                case 4:
                    k.Vein(scalp,.35f,.3f,.05f,.02f,4,.006f,1.1f,1,9);
                    break;
            }
        }
        /// <summary>The jaw: a chin of skin over bone hanging from the hinge, the mouth's floor and tongue on it, its own blocky teeth and
        /// gum along the rim, sinews across a torn cheek; turned open by the kind's slack (a hinged kind's jaw is its own model, opened
        /// further by NativeHordeJaw).</summary>
        private static void ZJaw(K k,Zb b)
        {
            float drop=b.HeadDrop,w=b.HeadW;var hinge=new Vector3(0,.07f+drop,.02f);var head=k.S;
            if(k.SplitJaw){k.S=k.JawSculpt;k.S.Into(CapturedRig.Head);}
            k.S.Push(WeaponSculpt.T(-hinge.X,-hinge.Y,-hinge.Z)*WeaponSculpt.Rx(b.Jaw)*WeaponSculpt.T(hinge.X,hinge.Y,hinge.Z));
            var jc=new Vector3(0,.058f+drop,.05f);var jr=new Vector3(.09f*w,.03f,.085f*w);
            if(k.Far)k.Box(ZSkinDark,jc.X,jc.Y,jc.Z+.01f,jr.X*1.8f,jr.Y*1.6f,jr.Z*1.8f);
            else
            {
                // The mandible: a U from the left ramus round the chin to the right one, each section a rounded box: skin outside, the
                // gum along its top, the mouth's dark inside; the floor of the mouth across the U, the tongue on it.
                var path=new[]{new Vector3(-.1f*w,.096f+drop,-.012f),new Vector3(-.104f*w,.07f+drop,-.006f),new Vector3(-.1f*w,.048f+drop,.012f),new Vector3(-.088f*w,.04f+drop,.05f),
                    new Vector3(-.066f*w,.037f+drop,.088f),new Vector3(-.034f*w,.036f+drop,.11f*w),new Vector3(0,.035f+drop,.118f*w),
                    new Vector3(.034f*w,.036f+drop,.11f*w),new Vector3(.066f*w,.037f+drop,.088f),new Vector3(.088f*w,.04f+drop,.05f),new Vector3(.1f*w,.048f+drop,.012f),new Vector3(.104f*w,.07f+drop,-.006f),new Vector3(.1f*w,.096f+drop,-.012f)};
                var hh=new[]{.014f,.019f,.025f,.029f,.032f,.035f,.037f,.035f,.032f,.029f,.025f,.019f,.014f};var tt=new[]{.011f,.014f,.017f,.019f,.02f,.021f,.022f,.021f,.02f,.019f,.017f,.014f,.011f};
                int n=path.Length,C=k.Fine?8:5;var pts=new Vector3[n][];var top=new Vector3[n];var inner=new Vector3[n];var tops=new Vector3[n];
                for(int i=0;i<n;i++)
                {
                    var t=Vector3.Normalize(path[Math.Min(n-1,i+1)]-path[Math.Max(0,i-1)]);var outw=new Vector3(path[i].X,0,path[i].Z-.03f);outw=Vector3.Normalize(outw-t*Vector3.Dot(outw,t));
                    var up=Vector3.Normalize(Vector3.Cross(t,outw));if(up.Y<0)up=-up;var side=Vector3.Normalize(Vector3.Cross(up,t));bool flipU=Vector3.Dot(side,outw)<0;
                    pts[i]=new Vector3[C];for(int q=0;q<C;q++){double ang=Math.PI*2*q/C;float cs=(float)Math.Cos(ang),sn=(float)Math.Sin(ang);float ex=Math.Sign(cs)*(float)Math.Pow(Math.Abs(cs),.6),ey=Math.Sign(sn)*(float)Math.Pow(Math.Abs(sn),.6);
                        pts[i][q]=path[i]+(flipU?-side:side)*ex*tt[i]+(flipU?-up:up)*ey*hh[i];}
                    tops[i]=path[i]+up*hh[i];top[i]=up;inner[i]=path[i]-outw*tt[i]*.8f-up*hh[i]*.3f;
                }
                k.Prim(()=>
                {
                    k.Grid(pts,true,(i,q)=>{var m=(pts[i][q]+pts[i+1][q]+pts[i][(q+1)%C]+pts[i+1][(q+1)%C])*.25f-(path[i]+path[i+1])*.5f;var up=Vector3.Normalize(top[i]+top[i+1]);var outw=new Vector3(path[i].X,0,path[i].Z-.03f);
                        float u=Vector3.Dot(m,up)/((hh[i]+hh[i+1])*.5f),o=Vector3.Dot(m,Vector3.Normalize(outw));return u>.6f?(o<-.002f?ZGum:ZSkinDark):o<-.006f?ZMouth:u<-.55f?ZSkinDark:ZSkin;});
                    k.Cap(ZFlesh,pts[0],path[0]-path[1]);k.Cap(ZFlesh,pts[n-1],path[n-1]-path[n-2]);
                    for(int i=0;i<n-1;i++)k.Facing(ZMouth,inner[i],inner[i+1],jc+new Vector3(0,-.004f,.01f),jc+new Vector3(0,-.004f,.01f),Vector3.UnitY);
                    for(int i=0;i<n;i+=3)k.Occ(path[i],hh[i]*.9f);
                });
                k.Mass(ZTongue,new Vector3(0,jc.Y+.006f,jc.Z+.005f),new Vector3(.042f*w,.01f,.05f*w),2,null,4,8,false);
                // The lower teeth round the front, crooked, a gap or two, out of the gum.
                int teeth=k.Fine?9:4;
                for(int i=0;i<teeth;i++)
                {
                    if(k.H(i,0xd7)<.14f)continue;float f=3.4f+5.2f*(i+.5f)/teeth;int a0=(int)f;float fr=f-a0;var root=Vector3.Lerp(tops[a0],tops[a0+1],fr);var o=Vector3.Normalize(new Vector3(root.X,0,root.Z-.03f));
                    k.ToothAt(k.H(i,0xd8)<.3f?ZToothDark:ZTooth,root-new Vector3(0,.003f,0),new Vector3((k.H(i,0xd9)-.5f)*.3f,1,.03f),o,.0115f*w,.012f+.006f*k.H(i,0xda),.009f,.75f);
                }
                // Skin webbing torn at the mouth's corners (or sinews across the torn cheek), up from the jaw toward the skull.
                for(int sd=-1;sd<=1;sd+=2)
                {
                    if(!k.Mid)break;var lo=new Vector3(sd*.092f*w,.066f+drop,.055f);var hi=new Vector3(sd*.108f*w,.104f+drop,.065f);var hi2=new Vector3(sd*.098f*w,.106f+drop,.1f);var lo2=new Vector3(sd*.074f*w,.064f+drop,.092f);
                    if(sd==b.Tear){if(k.Fine)for(int i=0;i<2;i++){var a=Vector3.Lerp(lo,lo2,.3f+.4f*i);var e=Vector3.Lerp(hi,hi2,.2f+.5f*i);k.Tube(ZFlesh,new[]{a,(a+e)*.5f+new Vector3(sd*.004f,0,.003f),e},new[]{.0032f,.0022f,.0028f},4,true,true);}continue;}
                    k.Facing(ZSkinDark,lo,lo2,Vector3.Lerp(lo2,hi2,.6f),Vector3.Lerp(lo,hi,.85f),new Vector3(sd,0,.2f));k.Facing(ZFleshDeep,lo,lo2,Vector3.Lerp(lo2,hi2,.6f),Vector3.Lerp(lo,hi,.85f),new Vector3(-sd,0,-.2f));
                }
            }
            k.S.Pop();k.S=head;
        }
        private static Vector3 Arc(Vector3[] path,float t){float f=t*(path.Length-1);int i=Math.Min(path.Length-2,(int)f);return Vector3.Lerp(path[i],path[i+1],f-i);}

        // ================================================================================================================
        // The neck
        // ================================================================================================================
        private static void ZNeck(K k,Zb b,WeaponPaint skin,float len=.165f)
        {
            k.On(CapturedRig.Neck);float n=b.Neck;
            bool collar=!b.Bare;
            // The big kinds' thick necks end inside their small sunken skulls (a shorter neck, its top no wider than the skull).
            float topR=Math.Min(.064f*n,.11f*b.HeadW);len=Math.Min(len,.165f+b.HeadDrop*.8f);
            k.Limb(new Vector3(0,-.045f,0),new Vector3(0,len,0),new[]{0,.086f*n,.08f*n,0,-.006f, .3f,.078f*n,.072f*n,0,0, .62f,Math.Max(topR,.069f*n*.8f),Math.Max(topR*.97f,.066f*n*.8f),0,.006f, 1,topR,topR*.97f,0,.01f},
                (r,c)=>collar&&(r==0||r==1&&k.H(c,0xe9)<.55f)?(r==0?ZSuitDark:ZSuitFray):skin,null,k.Fine?ZFlesh:(WeaponPaint?)null,10,-Vector3.UnitX);
            if(k.Mid){foreach(int s in new[]{-1,1})k.Tube(skin,new[]{new Vector3(s*.052f*n,len-.012f,.0f),new Vector3(s*.04f*n,.04f,.05f*n),new Vector3(s*.016f,-.01f,.068f*n)},new[]{.009f,.011f,.008f},5,false);
                k.Mass(skin,new Vector3(0,.05f,.07f*n),new Vector3(.013f,.016f,.01f),2,null,4,6,false);}
            if(k.Fine)k.Mass(ZBone,new Vector3(0,len-.004f,-.006f),new Vector3(.014f,.008f,.014f),2,null,3,6,false);
        }

        // ================================================================================================================
        // The torso: pelvis, waist and chest; the suit torn open; the chest plate and the back unit; the belt
        // ================================================================================================================
        /// <summary>Whether quad (row, col of a torso loft of 16 sides) is torn open on side `side` (-1 left, 1 right) within rows.</summary>
        private static bool Torn(K k,int row,int col,int side,int r0,int r1,float c0,float c1,int salt,int sides=16)
        {
            // (Torso lofts run their angle from the left over the front to the right; limbs from their outside... both measured here
            // from the side's own flank toward the front.)
            if(side==0||row<r0||row>r1)return false;float a=(col+.5f)/sides*360;if(side>0)a=180-a;a=(a%360+360)%360;
            float jag=(k.H(row*17+col,salt)-.5f)*28;return a+jag>c0&&a-jag<c1;
        }
        private static void ZTorso(K k,Zb b)
        {
            float w=b.W,dp=b.Depth;
            // ---- pelvis: the suit's seat and hips, the belt, a hip plate ----
            k.On(CapturedRig.Pelvis);
            var torn=b.Wound;
            k.Limb(new Vector3(0,-.125f,0),new Vector3(0,.135f,0),new[]{0,.12f*w,.1f*dp,0,0, .22f,.19f*w,.13f*dp,0,.005f, .47f,.215f*w,.14f*dp,0,.006f, .75f,.205f*w,.137f*dp,0,.01f, 1,.19f*w,.13f*dp,0,.012f},
                (r,c)=>ZSuit,ZSuitDark,null,16,-Vector3.UnitX);
            k.Limb(new Vector3(0,.04f,0),new Vector3(0,.105f,0),new[]{0,.205f*w,.142f*dp,0,.012f,1,.2f*w,.14f*dp,0,.013f},(r,c)=>c==4?ZShellDark:ZSuitDark,null,null,16,-Vector3.UnitX);
            k.Armor(ZShellDark,ZMetal,K.PlateAt(new Vector3(0,.072f,.152f*dp),Vector3.UnitZ,Vector3.UnitY),.075f,.05f,.02f,-1,.4f,2,k.Seam(.8f));
            if(k.Mid&&b.Tatters>0)for(int i=0;i<(k.Fine?6:3);i++){float a=(.15f+.7f*k.H(i,0xf1))*(float)Math.PI*2;var t=new Vector3((float)Math.Cos(a)*.2f*w,.04f,(float)Math.Sin(a)*.14f*dp);
                k.Tatter(i%3==0?ZRag:ZSuitDark,t,new Vector3(0,-1,0),new Vector3((float)Math.Cos(a),.0f,(float)Math.Sin(a)),.1f+.07f*k.H(i,0xf2),.04f,.12f);}
            // ---- the waist ----
            k.On(CapturedRig.Spine);float belly=b.Belly;
            var waist=k.Limb(new Vector3(0,-.04f,0),new Vector3(0,.215f,0),new[]{0,.19f*w,.132f*dp,0,.012f, .25f,.172f*w,(.123f+.05f*belly)*dp,0,.014f+.03f*belly, .4f,.18f*w,(.13f+.055f*belly)*dp,0,.016f+.032f*belly, .55f,.172f*w,(.125f+.06f*belly)*dp,0,.016f+.035f*belly, .75f,.183f*w,(.13f+.05f*belly)*dp,0,.015f+.03f*belly, 1,.198f*w,.133f*dp,0,.01f},
                (r,c)=>b.Bare?ZSkin:(torn!=0&&Torn(k,r,c,torn,0,3,22,82,0xf3))?ZSkin:(torn!=0&&Torn(k,r,c,torn,0,3,14,92,0xf3))?ZSuitFray:ZSuit,null,null,16,-Vector3.UnitX);
            if(b.Bare||torn!=0){k.Vein(waist,.3f,torn>0?.36f:.14f,.12f,.0f,3,.006f,.9f,1,11);}
            if(b.Bare&&k.Mid)for(int i=0;i<3;i++)foreach(int s in new[]{-1,1})k.Mass(ZSkin,new Vector3(s*.045f*w,.035f+.055f*i,.128f*dp+.012f),new Vector3(.038f*w,.024f,.012f),2,null,4,6,false);
            // ---- the chest: the ribcage loft, the suit, the wound and its ribs, the plates ----
            k.On(CapturedRig.Chest);
            float[] chest={0,.195f*w,.13f*dp,0,.005f, .2f,.214f*w,.142f*dp,0,.01f, .42f,.236f*w,.149f*dp,0,.014f, .62f,.252f*w,.146f*dp,0,.011f, .8f,.25f*w,.132f*dp,0,-.004f, .92f,.19f*w,.114f*dp,0,-.01f, 1,.1f*w,.082f*dp,0,0};
            if(b.Ribs>0)for(int i=1;i<5;i++)chest[i*5+1]*=1-.06f*b.Ribs;
            WeaponPaint? ChestPaint(int r,int c)
            {
                if(b.Bare)return r>=5?ZSkinDark:ZSkin;
                if(torn!=0&&Torn(k,r,c,torn,1,3,30,78,0xf4))return r==1?ZFlesh:ZFleshDeep;
                if(torn!=0&&Torn(k,r,c,torn,0,4,18,92,0xf4))return ZSkin;
                if(torn!=0&&Torn(k,r,c,torn,0,5,10,100,0xf4))return ZSuitFray;
                float a=(c+.5f)/16f*360;return r>=5?ZSuitDark:a>58&&a<122&&r>=1&&r<=4?ZSuitLight:ZSuit;
            }
            var ch=k.Limb(new Vector3(0,-.035f,0),new Vector3(0,.335f,0),chest,ChestPaint,null,null,16,-Vector3.UnitX);
            // The suit's construction: seams down the front panel and across the yoke, the zip's track.
            if(!b.Bare){foreach(float v in new[]{.16f,.34f})k.Seamline(ch,.08f,v,.82f,v,5,.005f,ZSuitFray);k.Seamline(ch,.82f,.12f,.82f,.38f,5,.005f,ZSuitFray);if(torn==0||torn>0)k.Seamline(ch,.1f,.27f,.7f,.265f,5,.0055f);}
            // The ribs across the wound (or down a starved chest), the ink's veins out of it.
            if(k.Mid&&(torn!=0||b.Ribs>0))
            {
                int sides=torn!=0?1:2;
                for(int sd=0;sd<sides;sd++){float s=torn!=0?torn:sd==0?-1:1;int ribs=k.Fine?4:2;
                for(int i=0;i<ribs;i++){float y=.035f+.05f*i;var pts=new Vector3[4];var rr=new float[4];
                    for(int j=0;j<4;j++){float deg=(torn!=0?80-17*j:88-24*j)-3*i;float v=(s<0?deg:180-deg)/360f;ch((y+.035f-.012f*j)/.37f,v,out var p,out var n);pts[j]=p+n*(.003f-.004f*b.Ribs);rr[j]=.0075f*(1-.15f*j/3f);}
                    k.Tube(ZBone,pts,rr,5,true,true);}}
                k.Vein(ch,.45f,torn>0?.33f:.17f,.13f,torn>0?.04f:-.04f,4,.0065f,1.1f,2,13);k.Vein(ch,.3f,torn>0?.36f:.14f,-.1f,0,3,.0055f,1,1,15);
            }
            if(b.Bare&&k.Mid&&b.Pec>0)foreach(int s in new[]{-1,1})k.Mass(ZSkin,new Vector3(s*.1f*w,.19f,.12f*dp),new Vector3(.1f*w,.07f*(float)Math.Sqrt(b.Pec),.045f*b.Pec),2,Matrix4x4.CreateRotationZ(-s*.15f),6,10);
            // The trapezius slope from the neck to the shoulders.
            foreach(int s in new[]{-1,1})k.Mass(b.Bare?ZSkin:ZSuit,new Vector3(s*.125f*w,.29f,-.025f),new Vector3(.115f*w,.06f*b.Trap,.08f*dp),2,Matrix4x4.CreateRotationZ(-s*.38f),5,8);
            if(b.Hump>0)k.Mass(ZSkin,new Vector3(0,.29f,-.08f),new Vector3(.2f*w,.11f*b.Hump,.11f*b.Hump),2,null,8,12);
            if(b.Spine)
            {
                for(int i=0;i<(k.Fine?7:k.Mid?4:2);i++)k.Mass(ZBone,new Vector3(0,.3f-i*.05f,-.142f*dp-.004f),new Vector3(.018f,.014f,.016f),2,null,4,6,false);
                foreach(int s in new[]{-1,1})k.Mass(ZSkinDark,new Vector3(s*.1f*w,.2f,-.12f*dp),new Vector3(.075f*w,.085f,.035f),2,Matrix4x4.CreateRotationZ(s*.2f),5,8);
                k.On(CapturedRig.Spine);for(int i=0;i<(k.Fine?4:2);i++)k.Mass(ZBone,new Vector3(0,.19f-i*.055f,-.128f*dp-.004f),new Vector3(.016f,.013f,.015f),2,null,4,6,false);k.On(CapturedRig.Chest);
            }
            // The chest plate: the pilot's, cracked across and broken off over the wound; its centre strip (the Siren's ink now); the
            // collar plate over it; the back unit with its grille, cracked, wiring hanging out.
            if(b.Plate!=0)k.Piece(2,null,()=>
            {
                int brk=b.Plate<0?(torn>0?3:0):(torn<0?2:1);
                k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(0,.135f,.16f*dp),new Vector3(0,.06f,1),Vector3.UnitY),.44f*w,.25f,.042f,brk,.58f,2,k.Seam(.9f),1,true,.012f);
                k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(0,.262f,.124f*dp),new Vector3(0,.75f,.66f),Vector3.UnitY),.36f*w,.075f,.032f,b.Plate<0?0:1,.4f,0,null,0,false,.01f);
                if(k.Mid)k.Cable(ZWire,new Vector3(b.Plate<0?-.02f:.02f,.04f,.17f*dp),new Vector3(b.Plate<0?.06f:-.06f,-.07f,.16f*dp),.02f);
            });
            if(b.Back)k.Piece(3,null,()=>
            {
                var at=K.PlateAt(new Vector3(0,.12f,-.172f*dp),-Vector3.UnitZ,Vector3.UnitY);
                k.Armor(ZShellDark,ZMetal,at,.3f*w,.22f,.045f,2,.42f,1,null,2,true);
                if(k.Fine)for(int i=0;i<4;i++)k.Box(ZSole,-.05f*w+.033f*w*i,.11f,-.172f*dp-.024f,.012f,.1f,.006f);
                if(k.Mid){k.Cable(ZWire,new Vector3(.06f,.02f,-.19f*dp),new Vector3(.1f,-.09f,-.18f*dp),.018f);k.Cable(Copper,new Vector3(-.05f,.015f,-.19f*dp),new Vector3(-.04f,-.07f,-.19f*dp),.01f,.0032f);}
            });
            // The segmented ab plates on the waist, some torn away.
            if(b.Plate!=0&&!b.Bare)
            {
                k.On(CapturedRig.Spine);
                for(int i=0;i<4;i++){int col=i%2,row=i/2;if(k.H(i,0xf6)<.3f||(torn!=0&&(col==0?-1:1)==torn&&row==1))continue;
                    k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3((col==0?-.062f:.062f)*w,.035f+.08f*row,.135f*dp+.01f),new Vector3(0,.05f,1),Vector3.UnitY),.115f*w,.072f,.03f,k.H(i,0xf7)<.4f?(col==0?2:3):-1,.4f,0,null,0,false,.01f);}
                k.On(CapturedRig.Chest);
            }
            // The collar: the suit's torn neck band.
            if(!b.Bare&&k.Mid)for(int i=0;i<(k.Fine?5:2);i++){float a=(float)Math.PI*2*(i+.3f*k.H(i,0xf5))/(k.Fine?7:4);var t=new Vector3((float)Math.Cos(a)*.105f*w,.31f,(float)Math.Sin(a)*.085f*dp);
                k.Tatter(ZSuitDark,t+new Vector3(0,.02f,0),new Vector3((float)Math.Cos(a)*.3f,-1,(float)Math.Sin(a)*.3f),new Vector3((float)Math.Cos(a),.4f,(float)Math.Sin(a)),.045f+.02f*k.H(i,0xf6),.035f,.25f);}
            if(b.Shawl&&k.Mid)
            {
                // The hood's torn shawl over the shoulders and down the back, rags hanging and swaying.
                k.Limb(new Vector3(0,.18f,-.005f),new Vector3(0,.33f,-.01f),new[]{0,.285f*w,.17f*dp,0,-.01f,.5f,.25f*w,.155f*dp,0,-.012f,1,.13f*w,.105f*dp,0,-.01f},(r,c)=>k.H(r*16+c,0xf7)<.15f?(WeaponPaint?)null:ZHood,null,null,16,-Vector3.UnitX);
                for(int i=0;i<(k.Fine?11:5);i++){float a=(float)Math.PI*(1.05f+.9f*(i+.5f)/(k.Fine?11:5));var t=new Vector3((float)Math.Cos(a)*.28f*w,.19f,(float)Math.Sin(a)*.165f*dp-.005f);
                    k.Tatter(i%3==1?ZRag:ZHood,t,new Vector3(0,-1,0),new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a)),.12f+.1f*k.H(i,0xf8),.07f,.1f);}
            }
        }

        // ================================================================================================================
        // Arms: the deltoid and the pauldron, the upper arm and its sleeve, the forearm and its guard, the hand
        // ================================================================================================================
        private static void ZArm(K k,Zb b,bool mirror)
        {
            int bit=mirror?2:1;bool pauldron=(b.Pauldron&bit)!=0,brace=(b.Brace&bit)!=0,glove=(b.Glove&bit)!=0;float d=b.Delt,a=b.Arm,f=b.Fore;bool bare=b.Bare||!brace;
            // ---- the shoulder ----
            k.On(CapturedRig.LClav,mirror);
            k.Mass(bare?ZSkin:ZSuit,new Vector3(-.07f,-.012f,0),new Vector3(.072f,.068f,.082f)*d,2,null,7,10);
            if(pauldron)k.Piece(mirror?4:1,null,()=>
            {
                // The pilot's pauldron: its top plate and its outer plate, cracked, the outer one broken at the front.
                // (On the big kinds' swollen deltoids the pilot's plates ride high and tight: they never grow past the capsule.)
                float dd=Math.Min(d,1.25f),ds=(float)Math.Sqrt(dd);
                k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(-.072f*dd,.056f*d,0),Vector3.Normalize(new Vector3(-.3f,1,0)),Vector3.UnitZ),.18f*ds,.2f*ds,.036f,0,.4f,0,null,1,true,.012f);
                k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(-.07f*dd-.066f*ds,-.002f,0),Vector3.Normalize(new Vector3(-1,-.15f,0)),Vector3.UnitY),.16f*ds,.11f*ds,.03f,mirror?2:3,.38f,1,null,0,false,.012f);
                if(k.Mid)k.Cable(ZWire,new Vector3(-.07f*dd-.07f*ds,-.06f,.07f),new Vector3(-.07f*dd-.07f*ds,-.14f,.08f),.012f);
            });
            else if(!b.Bare&&k.Mid)for(int i=0;i<2;i++)k.Tatter(ZSuitDark,new Vector3(-.09f*d,-.05f,-.03f+.06f*i),new Vector3(-.2f,-1,0),new Vector3(-1,0,(i-.5f)),.06f+.02f*i,.035f,.2f);
            // ---- the upper arm: the sleeve (the armoured arm) or the bare arm with the sleeve's stub ----
            k.On(CapturedRig.LArm,mirror);
            float[] upper={0,.06f*a,.064f*a,0,0, .2f,.064f*a,.07f*a,0,.004f, .45f,.058f*a,.062f*a,0,-.002f, .7f,.05f*a,.055f*a,0,-.004f, .9f,.046f*a,.05f*a,0,0, 1,.045f*a,.047f*a,0,0};
            var ua=k.Limb(new Vector3(0,.035f,0),new Vector3(0,-.28f,0),upper,(r,c)=>bare?(r==0?(b.Bare?ZSkin:ZSuit):r==1&&!b.Bare&&k.H(c,0xf9+bit)<.5f?ZSuitFray:ZSkin):ZSuit,ZFlesh,ZFlesh,10);
            if(k.Fine)k.Mass(ZBone,new Vector3(0,-.282f,0),new Vector3(.024f,.01f,.024f)*a,2,null,3,6,false);
            if(bare)k.Vein(ua,.35f,.35f,.12f,.03f,3,.005f,.9f,1,17+bit);
            if(bare&&!b.Bare&&k.Mid)for(int i=0;i<2;i++)k.Tatter(ZSuit,new Vector3(-.06f*a,-.035f,(i-.5f)*.07f*a),new Vector3(-.1f,-1,0),new Vector3(-1,0,(i-.5f)*.6f),.055f+.025f*i,.03f,.15f);
            if(brace)k.Piece(mirror?4:1,null,()=>{k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(-.08f*a,-.13f,0),-Vector3.UnitX,Vector3.UnitY),.16f*a,.2f,.036f,2,.42f,0,null,1,false,.011f);
                k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(-.012f,-.13f,.078f*a),Vector3.UnitZ,Vector3.UnitY),.13f*a,.18f,.032f,-1,.4f,0,null,2,true,.011f);});
            // ---- the forearm ----
            k.On(CapturedRig.LElbow,mirror);
            k.Mass(bare?ZSkinDark:ZSuit,new Vector3(0,.0f,-.022f),new Vector3(.043f,.042f,.038f)*f,2,null,5,8);
            if(bare&&k.Mid)k.Mass(ZBone,new Vector3(0,.004f,-.05f*f),new Vector3(.016f,.018f,.012f),2,null,4,6,false);
            float[] fore={0,.05f*f,.047f*f,0,0, .15f,.057f*f,.05f*f,.003f,.003f, .4f,.049f*f,.042f*f,0,0, .75f,.039f*f,.032f*f,0,0, 1,.035f*f,.029f*f,0,0};
            var fa=k.Limb(new Vector3(0,.012f,0),new Vector3(0,-.25f,0),fore,(r,c)=>bare?(r>=2&&r<=3&&(c==4||c==5)&&!b.Fist?ZFlesh:ZSkin):ZSuit,ZFlesh,null,10);
            if(bare&&!b.Fist&&k.Mid){k.Tube(ZBone,new[]{new Vector3(-.018f*f,-.11f,.035f*f),new Vector3(-.02f*f,-.16f,.037f*f),new Vector3(-.018f*f,-.2f,.03f*f)},new[]{.008f,.0085f,.0075f},5,true,true);k.Vein(fa,.25f,.3f,.15f,.04f,4,.005f,1,1,21+bit);}
            if(brace)k.Piece(mirror?4:1,null,()=>{k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(-.068f*f,-.125f,0),-Vector3.UnitX,Vector3.UnitY),.14f*f,.19f,.034f,-1,.4f,2,mirror?k.Seam(.8f):(WeaponPaint?)null,1,true,.011f);
                k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(0,-.13f,.066f*f),Vector3.UnitZ,Vector3.UnitY),.12f*f,.17f,.03f,3,.45f,0,null,0,false,.011f);});
            ZHand(k,b,mirror,glove);
        }
        /// <summary>A hand: the palm, four three-jointed fingers curled into hooks with nails or claws, the thumb; gloved on an armoured
        /// arm (the glove torn, the claws through it), a fist of knuckles on the big kinds.</summary>
        private static void ZHand(K k,Zb b,bool mirror,bool glove)
        {
            k.On(CapturedRig.LHand,mirror);float h=b.Hand;var skin=glove?ZSuitDark:ZSkin;
            if(b.Fist)
            {
                // A fist like a rock: the palm block, four curled finger rolls, knuckles of bone.
                k.Mass(ZSkin,new Vector3(0,-.055f,.015f)*h,new Vector3(.062f,.06f,.05f)*h,3,null,6,10);
                if(k.Far)return;
                for(int i=0;i<4;i++){float x=(.045f-.03f*i)*h;k.Tube(ZSkinDark,new[]{new Vector3(x,-.1f*h,-.01f*h),new Vector3(x,-.125f*h,.03f*h),new Vector3(x,-.105f*h,.068f*h)},new[]{.016f*h,.017f*h,.015f*h},6,true,true);
                    if(k.Mid)k.Mass(ZBone,new Vector3(x,-.112f*h,-.012f*h),new Vector3(.011f,.009f,.009f)*h,2,null,3,5,false);}
                k.Tube(ZSkinDark,new[]{new Vector3(.06f*h,-.04f*h,.03f*h),new Vector3(.065f*h,-.075f*h,.06f*h),new Vector3(.04f*h,-.095f*h,.075f*h)},new[]{.015f*h,.014f*h,.012f*h},6,true,true);
                return;
            }
            // The palm, a little cupped; the back of the glove's armour.
            if(k.Far){k.Box(skin,0,-.075f*h,.012f*h,.09f*h,.12f*h+b.Claw,.045f*h);return;}
            k.Mass(skin,new Vector3(0,-.044f,.006f)*h,new Vector3(.043f,.047f,.019f)*h,2.8f,null,5,8);
            if(glove)k.Armor(ZShellDark,ZMetal,K.PlateAt(new Vector3(0,-.04f,-.024f)*h,-Vector3.UnitZ,Vector3.UnitY),.09f*h,.07f*h,.016f,0,.4f,1,k.Seam(.7f));
            if(k.Far){k.Box(skin,0,-.105f*h,.02f*h,.085f*h,.05f*h+b.Claw,.04f*h);return;}
            int fingers=k.Fine?4:3;float fl=b.Finger;
            for(int i=0;i<fingers;i++)
            {
                float u=fingers==4?i/3f:i/2f;float x=(.029f-.057f*u)*h,len=(i==1?1.08f:i==fingers-1?.82f:1)*fl*h;
                // Each finger curls into a hook toward the palm (+z), more at the tip; a little splay.
                var p0=new Vector3(x,-.088f*h,.008f*h);float a=.32f+.12f*k.H(i,0xc4),s=(u-.5f)*.18f;
                var d1=Vector3.Normalize(new Vector3(-s*.5f,-(float)Math.Cos(a),(float)Math.Sin(a)));var p1=p0+d1*.032f*len;a+=.55f;
                var d2=Vector3.Normalize(new Vector3(-s*.6f,-(float)Math.Cos(a),(float)Math.Sin(a)));var p2=p1+d2*.023f*len;a+=.5f;
                var d3=Vector3.Normalize(new Vector3(-s*.6f,-(float)Math.Cos(a),(float)Math.Sin(a)));var p3=p2+d3*.018f*len;
                if(k.Fine)k.Tube(skin,new[]{p0,p1,p2,p3},new[]{.0086f*h,.0078f*h,.007f*h,.0052f*h},5,true,false);
                else k.Tube(skin,new[]{p0,p1,p3},new[]{.0085f*h,.0078f*h,.0055f*h},4,true,false);
                if(k.Fine)k.Claw(glove?ZNail:k.H(i,0xc5)<.5f?ZNail:ZBone,p3-d3*.004f,d3,new Vector3(0,0,1),b.Claw*(.8f+.4f*k.H(i,0xc6))+.006f,.012f*h);
            }
            // The thumb from the palm's inner side, folded forward.
            var t0=new Vector3(.043f*h,-.03f*h,.016f*h);var t1=t0+Vector3.Normalize(new Vector3(.25f,-.5f,.6f))*.032f*h;var t2=t1+Vector3.Normalize(new Vector3(-.05f,-.75f,.55f))*.026f*h;
            k.Tube(skin,new[]{t0,t1,t2},new[]{.012f*h,.011f*h,.009f*h},k.Fine?6:4,true,false);
            if(k.Fine)k.Claw(ZNail,t2,Vector3.Normalize(new Vector3(-.05f,-.75f,.55f)),new Vector3(-1,0,0),b.Claw*.7f+.006f,.012f*h);
            if(glove&&k.Mid)k.Mass(ZSkin,new Vector3(.01f,-.06f,.03f)*h,new Vector3(.025f,.02f,.006f),2,null,3,6,false);
        }

        // ================================================================================================================
        // Legs: the thigh in the torn suit, the knee and its pad, the shin, the boot or a bare foot
        // ================================================================================================================
        private static void ZLeg(K k,Zb b,bool mirror)
        {
            int bit=mirror?2:1;float l=b.Leg,s=b.Shin;bool thighTear=(b.ThighTear&bit)!=0,shinTear=(b.ShinTear&bit)!=0,pad=(b.KneePad&bit)!=0,plate=(b.ShinPlate&bit)!=0;
            k.On(CapturedRig.LHip,mirror);
            float[] thigh={0,.083f*l,.087f*l,0,0, .2f,.087f*l,.094f*l,0,.006f, .55f,.075f*l,.081f*l,0,.004f, .85f,.064f*l,.067f*l,0,0, 1,.06f*l,.062f*l,0,0};
            var th=k.Limb(new Vector3(0,.045f,0),new Vector3(0,-.262f,0),thigh,(r,c)=>thighTear&&Torn(k,r,c,-1,1,2,105,200,0xe5,12)?(r==2&&c%5==1?ZFlesh:ZSkin):thighTear&&Torn(k,r,c,-1,1,3,96,212,0xe5,12)?ZSuitFray:ZSuit,ZFlesh,ZFlesh,10);
            if(k.Fine)k.Mass(ZBone,new Vector3(0,-.264f,0),new Vector3(.03f,.01f,.03f)*l,2,null,3,6,false);
            if(thighTear)k.Vein(th,.35f,.42f,.1f,.02f,3,.005f,.9f,1,31+bit);
            // ---- the knee and shin ----
            k.On(CapturedRig.LKnee,mirror);
            k.Mass(ZSkinDark,new Vector3(0,-.004f,.036f*s),new Vector3(.04f,.043f,.03f)*s,2,null,5,8);
            if(pad)k.Piece(mirror?4:1,null,()=>{k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(0,-.006f,.074f*s),new Vector3(0,.2f,1),Vector3.UnitY),.14f*s,.115f,.045f,mirror?2:3,.35f,1,null,0,true,.013f);});
            float hem=shinTear?.5f:.75f;bool boot=!b.Barefoot;
            float[] shin={0,.06f*s,.063f*s,0,0, .25f,.063f*s,.071f*s,0,-.011f, .6f,.05f*s,.054f*s,0,-.003f, 1,.043f*s,.045f*s,0,0};
            var sh=k.Limb(new Vector3(0,.006f,0),new Vector3(0,-.215f,0),shin,(r,c)=>(r+.5f)/3f<hem+(k.H(c,0xe6+bit)-.5f)*.25f?ZSuit:(r+.5f)/3f<hem+.12f?ZSuitFray:ZSkin,ZFlesh,null,10);
            if(shinTear&&k.Mid){k.Vein(sh,.6f,.3f,.1f,.04f,3,.0045f,.9f,0,35+bit);for(int i=0;i<2;i++)k.Tatter(ZSuit,new Vector3(-.03f+.05f*i,-.09f,.055f-.07f*i),new Vector3(0,-1,0),new Vector3(-.4f+.8f*i,0,1-1.6f*i),.05f,.03f,.12f);}
            if(plate)k.Piece(mirror?4:1,null,()=>{k.Armor(ZShellDirty,ZMetal,K.PlateAt(new Vector3(0,-.098f,.064f*Math.Min(s,1.2f)),new Vector3(0,-.05f,1),Vector3.UnitY),.125f*Math.Min(s,1.2f),.15f,.032f,0,.38f,2,k.Seam(.85f),1,true,.011f);});
            if(boot&&!k.Far)k.Limb(new Vector3(0,-.155f,0),new Vector3(0,-.25f,0),new[]{0,.05f*s,.053f*s,0,0,.6f,.054f*s,.058f*s,0,0,1,.057f*s,.062f*s,0,-.004f},ZSuitDark,null,null,10);
            // ---- the foot ----
            k.On(CapturedRig.LFoot,mirror);float ft=b.Foot;
            if(boot)
            {
                // The pilot's boot: a chamfered upper and sole, the toe cap and heel plates scuffed and cracked.
                k.Box(ZShellDirty,0,-.024f,-.032f,.134f*ft,.08f,.176f*ft,.018f);k.Box(ZSuitDark,0,-.06f,-.034f,.142f*ft,.022f,.184f*ft,.006f);
                k.On(CapturedRig.LToe,mirror);
                k.Box(ZShellDirty,0,-.01f,.014f,.134f*ft,.064f,.13f*ft,.02f);k.Box(ZSuitDark,0,-.04f,.016f,.14f*ft,.022f,.134f*ft,.01f);
                if(k.Fine)k.Armor(ZShellDark,ZMetal,K.PlateAt(new Vector3(0,.023f,.03f),Vector3.Normalize(new Vector3(0,1,.35f)),Vector3.UnitZ),.1f*ft,.085f*ft,.012f,bit==1?0:1,.45f,0,null,0,true);
            }
            else
            {
                k.Mass(ZSkin,new Vector3(0,-.034f,-.03f),new Vector3(.055f,.038f,.088f)*ft,2.4f,null,6,10);
                k.Mass(ZSkinDark,new Vector3(0,-.04f,-.1f),new Vector3(.036f,.028f,.026f)*ft,2,null,4,6);
                k.On(CapturedRig.LToe,mirror);
                k.Mass(ZSkin,new Vector3(0,-.028f,.0f),new Vector3(.058f,.03f,.05f)*ft,2.4f,null,5,8);
                if(!k.Far)for(int i=0;i<(k.Fine?5:3);i++){float x=(.038f-.076f*i/(k.Fine?4f:2f))*ft;float len=(i==0?.05f:.042f-.004f*i)*ft;var p0=new Vector3(x,-.03f,.02f);var p1=p0+new Vector3(0,-.006f,len*.6f);var p2=p0+new Vector3(0,-.016f,len);
                    k.Tube(ZSkin,new[]{p0,p1,p2},new[]{.011f*ft,.01f*ft,.008f*ft},k.Fine?5:4,true,false);if(k.Fine)k.Claw(ZNail,p2-new Vector3(0,-.004f,.004f),new Vector3(0,-.4f,1),new Vector3(0,-1,0),.008f,.012f);}
            }
        }
    }
}

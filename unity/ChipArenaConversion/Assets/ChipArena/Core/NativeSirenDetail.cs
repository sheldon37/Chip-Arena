using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Siren's Unity finish beyond the pinned source, "The Fallen Couturiere" (owner request 2026-10-10: "make the siren
    /// model more detailed, a great design"): a pleated standing collar behind the head, pagoda pauldrons, a plunging bodice V
    /// piped in glow with a dorsal ridge, a cinched girdle, a bell gown open at the front with slit front panels, a burning
    /// train and hem ash that dissolve like the leg wisps, bell gauntlets with elbow spikes and knuckle lames, glowing claw
    /// rings, a fractured nimbus that turns behind the head, the sculpted mask (raked brows with ash trims, a frown crease,
    /// sockets, stepped temples, jawline plates, chin and crest) and cheekbones that lift with the squint. Every piece is a
    /// unit box (or, for the two glows in the nimbus breaks, an eye-halo sprite) on an existing joint; nothing changes a
    /// joint, a hitbox, a collider, the simulation, a cue or a timing.
    /// tools/siren_design.mjs holds the same table; Tests/SirenDetailChecks.cs holds the two to the matrices its run of a
    /// dressed source rig writes (Tests/siren-detail-parity.json). Frame and layout as NativeSirenLook: source frame (three.js,
    /// right-handed, +x the Siren's left), row-major for column vectors, 16 values per piece, joint-relative.
    /// The pieces follow the source's 136 in NativeSirenLook (index NativeSirenLook.Source + i). Static pieces are written once
    /// (Rest); Evaluate writes only the moving ones (the nimbus and its shards, the train, the hem ash, the cheeks and the claw
    /// rings, which ride the claw groups), with no allocation.
    /// </summary>
    public static class NativeSirenDetail
    {
        /// <summary>Tones: the ink plates (NativeSourceLit, _Ink 1), the glow lines, the tongues' flame box, the nimbus glows.</summary>
        public const int Plate=0,Deep=1,Sheen=2,Ash=3,Seam=4,Filament=5,Flame=6,Sprite=7;
        /// <summary>258 unit boxes and 2 sprites.</summary>
        public const int Count=260,Boxes=258;
        /// <summary>Ink tones: albedo (sRGB hex) and NativeSourceLit rim and rising-streak strengths (plate, deep, sheen, ash).</summary>
        public static readonly int[] InkHex={0x0b0910,0x060508,0x151120,0x15131b};
        public static readonly double[] InkRim={.42,.26,.55,.42},InkFlow={.05,.02,.08,.05};
        /// <summary>The ash trims' vertex emission (linear): they catch the eye glow.</summary>
        public const double AshEmitR=.07,AshEmitG=.064,AshEmitB=.09;
        /// <summary>Glow tones (unlit): the seams 0xcfc3ff at .82, the filaments 0xf1edff at 1.</summary>
        public const int SeamHex=0xcfc3ff,FilamentHex=0xf1edff;public const double SeamIntensity=.82,FilamentIntensity=1;
        /// <summary>The nimbus glows: the eye-halo quad in 0xd8ceff at .3, breathing to .8 of it.</summary>
        public const int GlowHex=0xd8ceff;public const double GlowOpacity=.3;
        /// <summary>Seam cross-section (m).</summary>
        public const double SeamWidth=.011;
        /// <summary>Talons (baked into the bank's claw parts): the long claws x1.15 along the blade, the thumb hook x1.08.</summary>
        public const double Talon=1.15,ThumbTalon=1.08;
        /// <summary>The nimbus: hub on the head, tilt, ring radius and turn rate (rad/s of the Siren clock).</summary>
        public const double HubY=.32,HubZ=-.24,HubTilt=-.1,Radius=.33,Spin=.16;
        /// <summary>Cheek lift per unit c (head-local metres, radians).</summary>
        public const double CheekX=.003,CheekY=.014,CheekZ=.006,CheekTurn=.14;
        /// <summary>The portrait framing's top for the Siren: about this far above the head joint, so the nimbus is not cropped.</summary>
        public const float PortraitTop=.66f;
        /// <summary>Kill switches (owner taste): the nimbus (-51 boxes, -2 sprites) and the gown with its train and hem ash.</summary>
        public static readonly bool Nimbus=true,Gown=true;
        // Exporter bone order.
        private const int Pelvis=1,Spine=2,Chest=3,Head=5,LShoulder=6,LArm=7,LElbow=8,LHand=9,RShoulder=10,RArm=11,RElbow=12,RHand=13,LLeg=14,RLeg=18;
        // How a piece moves: still, under the turning nimbus hub, a drifting shard, under a train hinge, a dissolving ash step,
        // under a cheek frame, on a claw group.
        private const int Still=0,Hub=1,Shard=2,Hinge=3,Ember=4,Cheek=5,Claw=6,HingeEmber=7;
        private sealed class Def
        {
            public string Id,Group;public int Bone,Tone=-1,Parent=-1,Claw=-1,Anim,I,K,Side;public double A;public bool Yxz;
            public double[] P={0,0,0},R={0,0,0},S={1,1,1};
        }
        private static readonly int[] bone=new int[Count],tone=new int[Count],claw=new int[Count],motion=new int[Count],node=new int[Count],ai=new int[Count],ak=new int[Count];
        private static readonly string[] id=new string[Count],group=new string[Count];
        private static readonly double[] aa=new double[Count],pre=new double[Count*16],post=new double[Count*16],own=new double[Count*9],rest=new double[Count*16];
        private static readonly bool[] hasPre=new bool[Count],visible=new bool[Count];
        // The moving nodes: the hub, five train hinges, two cheek frames (their rest pose, for the formulas).
        private static readonly double[] hinge=new double[5*3],cheekRest=new double[2*4];
        private static readonly int[] moving;
        [ThreadStatic] private static double[] scratch;
        /// <summary>The joint (exporter bone index) piece i rides (the claw rings: their hand).</summary>
        public static int Bone(int i)=>bone[i];
        /// <summary>Piece i's tone (Plate .. Sprite).</summary>
        public static int Tone(int i)=>tone[i];
        /// <summary>Piece i's id in tools/siren_design.mjs and its group (collar, pauldron, bodice, girdle, gown, front, hem, train, gauntlet, rings, nimbus, face, cheek).</summary>
        public static string Id(int i)=>id[i];
        public static string Group(int i)=>group[i];
        /// <summary>The claw group (0 left, 1 right) a claw ring rides, else -1.</summary>
        public static int ClawGroup(int i)=>claw[i];
        /// <summary>True for the pieces Evaluate writes every frame.</summary>
        public static bool Moves(int i)=>motion[i]!=Still;
        /// <summary>The moving pieces' indices.</summary>
        public static int MovingCount=>moving.Length;
        public static int Moving(int k)=>moving[k];
        /// <summary>A glow line's linear colour at its intensity (Seam, Filament), else white.</summary>
        public static void GlowColor(int t,out double r,out double g,out double b)
        {
            if(t!=Seam&&t!=Filament){r=g=b=1;return;}double k=t==Seam?SeamIntensity:FilamentIntensity;NativeBlastSim.Linear(t==Seam?SeamHex:FilamentHex,out r,out g,out b);r*=k;g*=k;b*=k;
        }
        /// <summary>
        /// The glow lines' strength: the eyes' breath (.9 at the trough to 1) times the distance fade (to .55 from 12 to 22 m),
        /// which keeps the one-pixel seams from shimmering at range without hiding the read.
        /// </summary>
        public static double LineGlow(double breath,double distance)
        {double f=Math.Max(0,Math.Min(1,(distance-12)/10)),b=Math.Max(0,Math.Min(1,breath));return (.9+.1*b)*(1+(.55-1)*f);}
        /// <summary>The nimbus glows' opacity over a breath.</summary>
        public static double GlowAlpha(double breath)=>GlowOpacity*(.8+.2*Math.Max(0,Math.Min(1,breath)));
        /// <summary>The arena cheek lift: .6 of the squint (a scream or a squint lifts them).</summary>
        public static double CheekLift(double squint)=>.6*Math.Max(0,Math.Min(1,squint));

        static NativeSirenDetail()
        {
            var defs=Table();
            var frame=new double[defs.Count*16];var mov=new List<int>();int n=0;
            for(int d=0;d<defs.Count;d++)
            {
                var e=defs[d];var local=new double[16];Trs(local,0,e.P,e.R,e.Yxz,e.Tone<0?null:e.S);
                if(e.Parent<0)Array.Copy(local,0,frame,d*16,16);else Mul(frame,d*16,frame,e.Parent*16,local,0);
                if(e.Tone<0)continue;
                // A drawn piece: find the moving node above it (at most one), split its chain into pre (joint to that node's
                // parent) and post (that node to the piece, the piece's own transform included).
                int k=n++;id[k]=e.Id;group[k]=e.Group;bone[k]=e.Bone;tone[k]=e.Tone;claw[k]=e.Claw;Array.Copy(frame,d*16,rest,k*16,16);
                int mover=-1;for(int q=e.Parent;q>=0;q=defs[q].Parent)if(defs[q].Anim!=Still){mover=q;break;}
                if(mover>=0&&defs[mover].Parent>=0)throw new InvalidOperationException("Moving Siren detail nodes hang from their joints: "+e.Id);
                if(e.Anim==Ember||e.Anim==Shard)
                {
                    // A shard (under the hub) or an ash step (under a train hinge, else under still nodes): its own transform moves.
                    ai[k]=e.I;ak[k]=e.K;aa[k]=e.A;own[k*9]=e.P[0];own[k*9+1]=e.P[1];own[k*9+2]=e.P[2];own[k*9+3]=e.S[0];own[k*9+4]=e.S[1];own[k*9+5]=e.S[2];own[k*9+6]=e.R[0];own[k*9+7]=e.R[1];own[k*9+8]=e.R[2];
                    if(e.Anim==Shard){if(mover<0||defs[mover].Anim!=Hub||e.Parent!=mover)throw new InvalidOperationException("A nimbus shard hangs from the hub: "+e.Id);motion[k]=Shard;}
                    else if(mover>=0){if(defs[mover].Anim!=Hinge||e.Parent!=mover)throw new InvalidOperationException("An ash step hangs from its hinge: "+e.Id);motion[k]=HingeEmber;node[k]=defs[mover].I;}
                    else{motion[k]=Ember;if(e.Parent>=0){hasPre[k]=true;Array.Copy(frame,e.Parent*16,pre,k*16,16);}}
                    mov.Add(k);continue;
                }
                if(e.Claw>=0){motion[k]=Claw;Array.Copy(local,0,post,k*16,16);mov.Add(k);continue;}
                if(mover<0){motion[k]=Still;continue;}
                // Under a moving node: post is the chain from that node (exclusive) down to the piece (its own transform included).
                var chain=new double[16];Identity(chain);var stack=new List<int>();for(int q=d;q!=mover;q=defs[q].Parent)stack.Add(q);
                for(int s=stack.Count-1;s>=0;s--){var c=defs[stack[s]];var m=new double[16];Trs(m,0,c.P,c.R,c.Yxz,c.Tone<0?null:c.S);var t=new double[16];Mul(t,0,chain,0,m,0);chain=t;}
                Array.Copy(chain,0,post,k*16,16);var mv=defs[mover];
                motion[k]=mv.Anim;node[k]=mv.Anim==Hinge?mv.I:mv.Anim==Cheek?(mv.Side>0?0:1):0;mov.Add(k);
            }
            if(n!=Count)throw new InvalidOperationException("Siren detail table has "+n+" pieces, expected "+Count);
            foreach(var e in defs)
            {
                if(e.Anim==Hinge){hinge[e.I*3]=e.P[0];hinge[e.I*3+1]=e.P[1];hinge[e.I*3+2]=e.P[2];if(e.I==0)hingeRx=e.R[0];hingeTheta[e.I]=e.R[1];}
                if(e.Anim==Cheek){int s=e.Side>0?0:1;cheekRest[s*4]=e.P[0];cheekRest[s*4+1]=e.P[1];cheekRest[s*4+2]=e.P[2];cheekRest[s*4+3]=e.R[2];}
            }
            for(int i=0;i<Count;i++)visible[i]=(Nimbus||group[i]!="nimbus")&&(Gown||group[i]!="gown"&&group[i]!="train"&&group[i]!="hem");
            moving=mov.ToArray();
        }
        private static readonly double hingeRx;private static readonly double[] hingeTheta=new double[5];

        // ------------------------------------------------------------ the table (tools/siren_design.mjs table(), in order)
        private static List<Def> Table()
        {
            var P=new List<Def>();var at=new Dictionary<string,int>();
            Def Add(string idv,string grp,int b,int t,string parent,double[] p,double[] r=null,double[] s=null,bool yxz=false,int anim=Still)
            {var e=new Def{Id=idv,Group=grp,Bone=b,Tone=t,Parent=parent==null?-1:at[parent],P=p,R=r??new double[]{0,0,0},S=s??new double[]{1,1,1},Yxz=yxz,Anim=anim};at[idv]=P.Count;P.Add(e);return e;}
            double[] V(double x,double y,double z)=>new[]{x,y,z};
            const double PI=Math.PI;
            // 1. Collar.
            for(int i=0;i<15;i++)
            {
                double u=i/7.0-1,th=(-104+208.0*i/14)*PI/180,H=.24+.30*Math.Pow(1-u*u,.7),z=i%2==1?-.004:0;int t=i%2==1?Deep:Sheen;string c="collar-"+i;
                Add(c,"collar",Chest,-1,null,V(.2*Math.Sin(th),.25,-.2*Math.Cos(th)),V(.10,PI-th,0),null,true);
                Add(c+"-foot","collar",Chest,t,c,V(0,.275*H,z),null,V(.082,.5665*H,.016));
                Add(c+"-h","collar",Chest,-1,c,V(0,.55*H,0),V(.24,0,0));
                Add(c+"-blade","collar",Chest,t,c+"-h",V(0,.225*H,z),null,V(.1,.4635*H,.016));
                Add(c+"-tip","collar",Chest,t,c+"-h",V(0,.45*H,z),V(0,0,PI/4),V(.07,.07,.014));
                if(i%2==0)Add(c+"-point","collar",Chest,Seam,c+"-h",V(0,.45*H+.044,.006),V(0,0,PI/4),V(.02,.02,.01));
            }
            // Mirrored pairs: s +1 is the left (-l), -1 the right (-r): x, y-rotation and z-rotation negate.
            double[] M(int s,double x,double y,double z)=>new[]{s*x,y,z};
            double[] MR(int s,double x,double y,double z)=>new[]{x,s*y,s*z};
            string Sd(string name,int s)=>name+(s>0?"-l":"-r");
            // 2. Pauldrons.
            foreach(int s in new[]{1,-1})
            {
                int sh=s>0?LShoulder:RShoulder,arm=s>0?LArm:RArm;
                Add(Sd("pauldron-top",s),"pauldron",sh,Sheen,null,M(s,.085,.075,0),MR(s,0,0,-.16),V(.25,.04,.27));
                Add(Sd("pauldron-tip",s),"pauldron",sh,Sheen,null,M(s,.228,.102,0),MR(s,0,0,.6),V(.08,.035,.25));
                Add(Sd("pauldron-mid",s),"pauldron",arm,Plate,null,M(s,.045,.015,0),MR(s,0,0,-.62),V(.23,.035,.255));
                Add(Sd("pauldron-low",s),"pauldron",arm,Deep,null,M(s,.06,-.06,0),MR(s,0,0,-.95),V(.2,.03,.235));
            }
            // 3. Bodice and dorsal ridge.
            foreach(int s in new[]{1,-1})
            {
                string panel=Sd("bodice",s);Add(panel,"bodice",Chest,-1,null,M(s,.11,.11,.162),MR(s,0,.14,-.28));
                Add(Sd("bodice-plate",s),"bodice",Chest,Sheen,panel,V(0,0,0),null,V(.17,.29,.02));
                Add(Sd("bodice-seam",s),"bodice",Chest,Seam,panel,M(s,-.081,0,.012),null,V(SeamWidth,.3,SeamWidth));
            }
            for(int k=0;k<5;k++)Add("ridge-"+k,"bodice",Chest,Sheen,null,V(0,.27-.075*k,-.168),V(-.62,0,0),V(.075-.008*k,.03,.06));
            // 4. Girdle.
            for(int i=0;i<8;i++){double ph=(i+.5)*PI/4;Add("girdle-"+i,"girdle",Spine,i==0||i==7?Sheen:Plate,null,V(.19*Math.Sin(ph),.03,.142*Math.Cos(ph)),V(0,ph,0),V(.11,.075,.016));}
            // 5. Gown tiers.
            var skirtH=new double[9];
            void Tier(string name,int n,double from,double to,double rx,double rz,double y,double h,double wTop,double wBot,double flare,int t)
            {
                for(int i=0;i<n;i++)
                {
                    double th=(from+(to-from)*i/(n-1))*PI/180;string c=name+"-"+i;if(name=="skirt")skirtH[i]=h;
                    Add(c,"gown",Pelvis,-1,null,V(rx*Math.Sin(th),y,rz*Math.Cos(th)),V(-flare,th,0),null,true);
                    Add(c+"-top","gown",Pelvis,t,c,V(0,-.26*h,0),null,V(wTop,.52*h,.016));
                    Add(c+"-bot","gown",Pelvis,t,c,V(0,-.75*h,.004),null,V(wBot,.5*h,.016));
                }
            }
            Tier("peplum",8,72,288,.255,.185,.075,.2,.13,.15,.38,Sheen);
            Tier("skirt",9,96,264,.27,.205,-.04,.46,.14,.185,.3,Plate);
            // 6. Front panels.
            foreach(int s in new[]{1,-1})
            {
                int leg=s>0?LLeg:RLeg;string a=Sd("front-a",s),b=Sd("front-b",s);
                Add(a,"front",leg,-1,null,M(s,.02,.02,.125),MR(s,-.08,.10,0),null,true);
                Add(Sd("front-a-plate",s),"front",leg,Sheen,a,M(s,.06,-.125,0),null,V(.2,.25,.016));
                Add(Sd("front-a-slit",s),"front",leg,Seam,a,M(s,-.036,-.125,.011),null,V(SeamWidth,.25,SeamWidth));
                Add(b,"front",leg,-1,a,V(0,-.25,0),V(.5,0,0));
                Add(Sd("front-b-plate",s),"front",leg,Plate,b,M(s,.065,-.11,.002),null,V(.215,.22,.016));
                Add(Sd("front-b-slit",s),"front",leg,Seam,b,M(s,-.039,-.11,.013),null,V(SeamWidth,.22,SeamWidth));
                for(int k=0;k<2;k++){double w=k==0?.075:.05;var e=Add(Sd("front-ash-"+k,s),"hem",leg,Flame,b,M(s,.04+.05*k,-.25-.06*k,0),null,V(w,.9*w,.6*w),false,Ember);e.I=20+k;e.K=k;}
            }
            // 7. Train.
            for(int i=0;i<5;i++)
            {
                double th=(140+20*i)*PI/180,h=.40-.04*Math.Abs(i-2);string c="train-"+i;
                var hg=Add(c,"train",Pelvis,-1,null,V(.31*Math.Sin(th),-.46,.27*Math.Cos(th)),V(-.5,th,0),null,true,Hinge);hg.I=i;
                Add(c+"-panel","train",Pelvis,i%2==1?Deep:Plate,c,V(0,-h/2,0),null,V(.165,h,.016));
                Add(c+"-hem","train",Pelvis,Seam,c,V(0,-h+.006,.012),null,V(.15,SeamWidth,SeamWidth));
                for(int k=0;k<3;k++){double w=k==0?.11:k==1?.07:.045;var e=Add(c+"-ash-"+k,"hem",Pelvis,Flame,c,V((k%2==1?-1:1)*.03,-h-.05-.075*k,0),null,V(w,.9*w,.6*w),false,Ember);e.I=i;e.K=k;}
            }
            // 8. Hem ash under skirt plates 1, 2, 6 and 7.
            foreach(int i in new[]{1,2,6,7})for(int k=0;k<2;k++){double w=k==0?.08:.05;var e=Add("skirt-"+i+"-ash-"+k,"hem",Pelvis,Flame,"skirt-"+i,V((k==1?-1:1)*.025,-skirtH[i]-.045-.07*k,0),null,V(w,.9*w,.6*w),false,Ember);e.I=10+i;e.K=k;}
            // 9. Gauntlets.
            foreach(int s in new[]{1,-1})
            {
                int el=s>0?LElbow:RElbow,hand=s>0?LHand:RHand;
                Add(Sd("vambrace-a",s),"gauntlet",el,Plate,null,V(0,-.075,0),null,V(.175,.07,.185));
                Add(Sd("vambrace-b",s),"gauntlet",el,Plate,null,V(0,-.14,0),null,V(.19,.065,.2));
                Add(Sd("cuff",s),"gauntlet",el,Sheen,null,V(0,-.205,.004),V(.12,0,0),V(.23,.045,.24));
                for(int k=0;k<3;k++){double w=k==0?.05:k==1?.034:.018,l=k==0?.07:k==1?.06:.055;Add(Sd("elbow-spike-"+k,s),"gauntlet",el,Plate,null,V(0,.012+.02*k,-.1-.055*k),V(.35,0,0),V(w,w,l));}
                Add(Sd("knuckle-a",s),"gauntlet",hand,Sheen,null,V(0,-.025,.02),null,V(.158,.032,.12));
                Add(Sd("knuckle-b",s),"gauntlet",hand,Plate,null,V(0,-.068,.07),V(.35,0,0),V(.146,.03,.1));
            }
            // 11. Claw rings (on the claw groups).
            foreach(int s in new[]{1,-1})foreach(double x in new[]{-.046,0,.046})
            {var e=Add(Sd("ring-"+(x<0?"a":x>0?"c":"b"),s),"rings",s>0?LHand:RHand,Filament,null,M(s,x,-.006,0),MR(s,0,0,1.6*x),V(.028,.009,.026));e.Claw=s>0?0:1;}
            // 12. Nimbus.
            Add("nimbus","nimbus",Head,-1,null,V(0,HubY,HubZ),V(HubTilt,0,0),null,false,Hub);
            var gaps=new HashSet<int>{3,10,11};
            for(int i=0;i<18;i++)
            {
                if(gaps.Contains(i))continue;double a=PI/2-i*PI/9,dr=i==4?.034:i==9?.028:i==15?.018:0,tw=i==4?.28:i==9?-.24:i==15?.16:0,rr=Radius+dr,len=2*PI*Radius/18*.9;string c="nimbus-seg-"+i;
                Add(c,"nimbus",Head,-1,"nimbus",V(rr*Math.Cos(a),rr*Math.Sin(a),0),V(0,0,a-PI/2+tw));
                Add(c+"-ink","nimbus",Head,Sheen,c,V(0,0,0),null,V(len,.028,.026));
                Add(c+"-fil","nimbus",Head,Filament,c,V(0,-.005,.016),null,V(len*.94,.016,.012));
            }
            int[] rays={0,1,-1,2,-2,4,-4};
            for(int j=0;j<rays.Length;j++)
            {
                double a=PI/2-rays[j]*PI/6;string c="nimbus-ray-"+j;Add(c,"nimbus",Head,-1,"nimbus",V((Radius+.022)*Math.Cos(a),(Radius+.022)*Math.Sin(a),-.004),V(0,0,a-PI/2));
                double y=0;double[,] steps={{.034,.065},{.023,.065},{.012,.06}};
                for(int q=0;q<(j<3?3:2);q++){double w=steps[q,0],h=steps[q,1];Add(c+"-"+q,"nimbus",Head,Sheen,c,V(0,y+h/2,0),null,V(w,h,.018));y+=.92*h;}
            }
            double[] shardA={2.1,3.35,5.5,5.9},shardS={.03,.022,.026,.018};
            for(int k=0;k<4;k++){double a=shardA[k],s=shardS[k];var e=Add("nimbus-shard-"+k,"nimbus",Head,k%2==1?Filament:Sheen,"nimbus",V((Radius+.012)*Math.Cos(a),(Radius+.012)*Math.Sin(a),.01),V(0,0,a+.7),V(s,.55*s,.55*s),false,Shard);e.K=k;e.A=a;}
            // 13. The sculpted mask.
            foreach(int s in new[]{1,-1})
            {
                Add(Sd("brow-out",s),"face",Head,Plate,null,M(s,.122,.295,.171),MR(s,0,0,.1),V(.056,.016,.022));
                Add(Sd("brow-trim-out",s),"face",Head,Ash,null,M(s,.1228,.2865,.1825),MR(s,0,0,.1),V(.05,.004,.005));
                Add(Sd("brow-in",s),"face",Head,Plate,null,M(s,.066,.2965,.171),MR(s,0,0,.42),V(.064,.016,.022));
                Add(Sd("brow-trim-in",s),"face",Head,Ash,null,M(s,.0695,.2887,.1825),MR(s,0,0,.42),V(.058,.004,.005));
                Add(Sd("crease",s),"face",Head,Plate,null,M(s,.011,.262,.1665),MR(s,0,0,-.45),V(.007,.03,.008));
                Add(Sd("socket",s),"face",Head,Plate,null,M(s,.0356,.1517,.1672),MR(s,0,0,-1.0491),V(.036,.008,.01));
                Add(Sd("temple",s),"face",Head,Plate,null,M(s,.173,.215,.06),null,V(.012,.14,.12));
                Add(Sd("temple-step",s),"face",Head,Plate,null,M(s,.178,.165,.04),null,V(.008,.05,.08));
                Add(Sd("temple-trim",s),"face",Head,Ash,null,M(s,.1795,.139,.04),null,V(.005,.004,.075));
                Add(Sd("jawline",s),"face",Head,Plate,null,M(s,.128,.004,.125),MR(s,0,-.3,0),V(.06,.02,.07));
                Add(Sd("crest-side",s),"face",Head,Plate,null,M(s,.095,.343,-.03),null,V(.045,.016,.2));
            }
            Add("chin","face",Head,Plate,null,V(0,-.033,.148),V(0,0,PI/4),V(.026,.026,.02));
            Add("crest","face",Head,Plate,null,V(0,.348,-.04),null,V(.055,.024,.22));
            // 14. Cheeks: a moving frame per side, the cheekbone and its ash trim riding on it.
            foreach(int s in new[]{1,-1})
            {
                string f=Sd("cheek",s);var e=Add(f,"cheek",Head,-1,null,M(s,.163,.176,.17),MR(s,0,0,.42),null,false,Cheek);e.Side=s;
                Add(Sd("cheek-box",s),"cheek",Head,Plate,f,V(0,0,0),null,V(.032,.024,.024));
                double dx=.1625-.163,dy=.1865-.176,c=Math.Cos(.42),sn=Math.Sin(.42);
                Add(Sd("cheek-trim",s),"cheek",Head,Ash,f,M(s,c*dx+sn*dy,-sn*dx+c*dy,.012),null,V(.026,.004,.005));
            }
            // The two glows in the nimbus breaks (sprites).
            double[] glowSlot={3,10.5},glowSize={.15,.18};
            for(int k=0;k<2;k++){double a=PI/2-glowSlot[k]*PI/9;Add("nimbus-glow-"+k,"nimbus",Head,Sprite,"nimbus",V(Radius*Math.Cos(a),Radius*Math.Sin(a),.01),null,V(glowSize[k],glowSize[k],1));}
            return P;
        }

        // ------------------------------------------------------------ matrices (row-major, top three rows used)
        private static void Identity(double[] m){Array.Clear(m,0,16);m[0]=m[5]=m[10]=m[15]=1;}
        /// <summary>m[o..] = T(p)·R·S, R the Euler rotation in XYZ (Rx·Ry·Rz) or YXZ (Ry·Rx·Rz) order (three.js), S null for 1.</summary>
        private static void Trs(double[] m,int o,double[] p,double[] r,bool yxz,double[] s)=>Trs(m,o,p[0],p[1],p[2],r[0],r[1],r[2],yxz,s==null?1:s[0],s==null?1:s[1],s==null?1:s[2]);
        private static void Trs(double[] m,int o,double px,double py,double pz,double rx,double ry,double rz,bool yxz,double sx,double sy,double sz)
        {
            double a=Math.Cos(rx),b=Math.Sin(rx),c=Math.Cos(ry),d=Math.Sin(ry),e=Math.Cos(rz),f=Math.Sin(rz),r00,r01,r02,r10,r11,r12,r20,r21,r22;
            if(yxz){double ce=c*e,cf=c*f,de=d*e,df=d*f;r00=ce+df*b;r01=de*b-cf;r02=a*d;r10=a*f;r11=a*e;r12=-b;r20=cf*b-de;r21=df+ce*b;r22=a*c;}
            else{double ae=a*e,af=a*f,be=b*e,bf=b*f;r00=c*e;r01=-c*f;r02=d;r10=af+be*d;r11=ae-bf*d;r12=-b*c;r20=bf-ae*d;r21=be+af*d;r22=a*c;}
            m[o]=r00*sx;m[o+1]=r01*sy;m[o+2]=r02*sz;m[o+3]=px;m[o+4]=r10*sx;m[o+5]=r11*sy;m[o+6]=r12*sz;m[o+7]=py;m[o+8]=r20*sx;m[o+9]=r21*sy;m[o+10]=r22*sz;m[o+11]=pz;
            m[o+12]=0;m[o+13]=0;m[o+14]=0;m[o+15]=1;
        }
        /// <summary>dst[d..] = a[ao..]·b[bo..] for affine matrices (dst may not alias a or b).</summary>
        private static void Mul(double[] dst,int d,double[] a,int ao,double[] b,int bo)
        {
            for(int r=0;r<3;r++)
            {
                int i=ao+r*4;double x=a[i],y=a[i+1],z=a[i+2];
                dst[d+r*4]=x*b[bo]+y*b[bo+4]+z*b[bo+8];dst[d+r*4+1]=x*b[bo+1]+y*b[bo+5]+z*b[bo+9];dst[d+r*4+2]=x*b[bo+2]+y*b[bo+6]+z*b[bo+10];dst[d+r*4+3]=x*b[bo+3]+y*b[bo+7]+z*b[bo+11]+a[i+3];
            }
            dst[d+12]=0;dst[d+13]=0;dst[d+14]=0;dst[d+15]=1;
        }

        // ------------------------------------------------------------ evaluation
        /// <summary>Every static piece's joint-relative transform into m (piece i at offset (first + i)·16) and shown.</summary>
        public static void Rest(double[] m,bool[] shown,int first)
        {
            for(int i=0;i<Count;i++){shown[first+i]=visible[i];if(motion[i]==Still)Array.Copy(rest,i*16,m,(first+i)*16,16);}
        }
        /// <summary>
        /// The moving pieces for look state s into m (offset (first + i)·16): the nimbus turns on the Siren clock and its shards
        /// drift; the train trails the forward lean and swings with the flare; the hem ash bobs, pulses and trails like the
        /// leg wisps; the cheeks lift by `cheek` (0..1); the claw rings ride the claw groups, already in m at `claws`·16.
        /// </summary>
        public static void Evaluate(in SirenLookState s,double[] m,bool[] shown,int first,int claws,double cheek)
        {
            var w=scratch??(scratch=new double[16*12]);double t=s.Time,c=s.Clock,lean=Math.Max(0,s.Lean),flare=s.Flare;cheek=Math.Max(0,Math.Min(1,cheek));
            // w[0]: the hub; w[16..95]: the train hinges; w[96..127]: the cheek frames; w[128..]: one piece's own transform.
            Trs(w,0,0,HubY,HubZ,HubTilt,0,Spin*c,false,1,1,1);
            for(int i=0;i<5;i++)Trs(w,16+i*16,hinge[i*3],hinge[i*3+1],hinge[i*3+2],hingeRx-.55*lean-.05*Math.Sin(1.9*t+1.3*i)-.12*flare,hingeTheta[i],0,true,1,1,1);
            for(int k=0;k<2;k++){double sd=k==0?1:-1;Trs(w,96+k*16,cheekRest[k*4]+sd*CheekX*cheek,cheekRest[k*4+1]+CheekY*cheek,cheekRest[k*4+2]+CheekZ*cheek,0,0,cheekRest[k*4+3]+sd*CheekTurn*cheek,false,1,1,1);}
            foreach(int i in moving)
            {
                int o=(first+i)*16,kind=motion[i];shown[first+i]=visible[i];
                switch(kind)
                {
                    case Hub:Mul(m,o,w,0,post,i*16);break;
                    case Hinge:Mul(m,o,w,16+node[i]*16,post,i*16);break;
                    case Cheek:Mul(m,o,w,96+node[i]*16,post,i*16);break;
                    case Claw:Mul(m,o,m,(claws+claw[i])*16,post,i*16);break;
                    case Shard:Own(w,128,i,t,c,lean);Mul(m,o,w,0,w,128);break;
                    case HingeEmber:Own(w,128,i,t,c,lean);Mul(m,o,w,16+node[i]*16,w,128);break;
                    case Ember:Own(w,128,i,t,c,lean);if(hasPre[i])Mul(m,o,pre,i*16,w,128);else Array.Copy(w,128,m,o,16);break;
                }
            }
        }
        // A shard's or an ash step's own transform this frame.
        private static void Own(double[] w,int o,int i,double t,double c,double lean)
        {
            int b=i*9,k=ak[i],n=ai[i];
            if(motion[i]==Shard)
            {
                double dr=.012*Math.Sin(1.3*c+2.1*k),a=aa[i]+.03*Math.Sin(.9*c+k),r=Radius+.012+dr;
                Trs(w,o,r*Math.Cos(a),r*Math.Sin(a),.01+.006*Math.Sin(1.7*c+k),0,0,a+.7+.6*c*(k%2==1?1:-1),false,own[b+3],own[b+4],own[b+5]);return;
            }
            double f=(k+1)/3.0,mm=.8+.3*Math.Sin(6.4*t+2.3*n+1.1*k);
            Trs(w,o,own[b]+.02*f*Math.Sin(4.3*t+1.7*n+k),own[b+1]-.012*Math.Sin(5.1*t+n+2*k),own[b+2]-.06*f*lean,own[b+6],own[b+7],own[b+8],false,own[b+3]*mm,own[b+4]*(.85+.3*Math.Sin(5.7*t+n)),own[b+5]*mm);
        }
    }
}

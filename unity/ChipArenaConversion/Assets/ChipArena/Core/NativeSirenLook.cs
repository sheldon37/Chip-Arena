using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>Resources/SirenLook.json (tools/export_siren_look.mjs): per Siren motion clip, the face targets.</summary>
    [Serializable] public sealed class SirenLookSet {public int fps;public SirenLookClip[] clips;}
    /// <summary>One clip: frames × (open, squint), finishLook's targets before smoothing.</summary>
    [Serializable] public sealed class SirenLookClip {public string key;public float duration;public bool loop;public int frames;public float[] values;}

    /// <summary>The look state applySirenLook reads (siren/model.ts SirenParts.state, with SIREN_CLOCK as Clock).</summary>
    public struct SirenLookState {public double Ranged,Open,Squint,Anger,Glow,Time,Lean,Flare,Clock;}

    /// <summary>
    /// client/siren/model.ts applySirenLook at 05950ea, per animated piece of the Siren: the claw groups retract to .32 of
    /// their length in ranged mode, the black-fire palm orbs and their halos swell in from 2% ranged and pulse on the Siren
    /// clock, the eye halos glow, the grin rows part, the eyes widen and squint and take one of 41 brow cuts by anger, the 16
    /// flame tongues flicker and writhe step by step and the 12 leg wisps sway and trail the lean. Each piece's transform is
    /// written relative to its joint in the source frame (three.js, right-handed), row-major for column vectors, 16 values per
    /// piece; the runtime mirrors x into Unity and places it on the posed joint. Pure and allocation-free.
    /// Piece order: 2 eyes (side +1, -1), 2 eye halos, upper and lower grin row, 2 claw groups (left, right hand), 2 orbs,
    /// 2 orb halos, 12 wisps (left knee then right, top to tip), 16 tongues × 7 steps (TONGUES order, base to tip); then, from
    /// Source on, the Unity finish beyond the source (NativeSirenDetail: the couture, the mask and the cheeks, 260 pieces).
    /// The Unity finish also turns each tongue step about its own vertical axis and swells or thins it (Twist: stratified,
    /// faceted fire instead of square posts; the tip positions, the flicker and the flame pool's emitters are the source's).
    /// </summary>
    public static class NativeSirenLook
    {
        public const int Eye=0,EyeHalo=2,GrinUpper=4,GrinLower=5,Claw=6,Orb=8,OrbHalo=10,Wisp=12,Tongue=24,Source=136,Detail=Source,Count=Source+NativeSirenDetail.Count;
        public const int TongueCount=16,TongueSteps=7,WispSteps=6,EyeSegments=40,Shapes=41;
        public const double EyeHaloOpacity=.42,OrbHaloOpacity=.32,Swap=.35,FaceRate=18,LeanRate=6;
        /// <summary>motion.ts's swap shudder runs SIREN_TIMING.swap + .15 s from the mode change.</summary>
        public const double Shudder=Swap+.15;
        /// <summary>The idle clips (tools/export_motion.mjs) the body blends between while the mode swaps.</summary>
        public const string IdleMelee="siren-idle-melee",IdleRanged="siren-idle-ranged";
        /// <summary>Sprite colours (haloMaterial: additive, radial alpha, not tone mapped).</summary>
        public const int EyeHaloColor=0xf4f0ff,OrbHaloColor=0xd8ceff;
        // Joints in the exporter's bone order: head 5, chest 3, shoulders 6/10, hands 9/13, knees 15/19.
        private const int Head=5,Chest=3,LShoulder=6,RShoulder=10,LHand=9,RHand=13,LKnee=15,RKnee=19;
        /// <summary>TONGUES: joint, x, y, z, height, lean.</summary>
        private static readonly double[] Tongues=
        {
            Head,-.115,.33,.01,.85,-.2, Head,-.045,.35,-.03,1.2,-.32, Head,.04,.35,-.01,1.05,-.28, Head,.115,.33,.02,.8,-.18, Head,0,.31,-.12,1.0,-.62, Head,-.09,.29,-.13,.75,-.7, Head,.09,.29,-.12,.7,-.7,
            LShoulder,.1,.07,0,.8,-.15, LShoulder,.17,.04,-.05,.6,-.3,
            RShoulder,-.1,.07,0,.8,-.15, RShoulder,-.17,.04,-.05,.6,-.3,
            Chest,-.11,.31,-.12,.95,-.45, Chest,.09,.32,-.13,1.05,-.5, Chest,0,.22,-.17,.85,-.75, Chest,-.2,.12,-.15,.6,-.7, Chest,.2,.12,-.15,.6,-.7,
        };
        /// <summary>TONGUE_STEPS: size, height.</summary>
        private static readonly double[] TongueStep={.074,.06, .062,.062, .05,.062, .04,.06, .03,.056, .021,.05, .012,.044};
        /// <summary>WISP_STEPS: y, size.</summary>
        private static readonly double[] WispStep={-.06,.15, -.14,.122, -.21,.095, -.28,.07, -.34,.048, -.39,.03};
        private static readonly int[] joint=BuildJoints();
        /// <summary>The joint (exporter bone index) each piece rides.</summary>
        public static int Joint(int piece)=>joint[piece];
        private static int[] BuildJoints()
        {
            var j=new int[Count];
            for(int i=0;i<2;i++){j[Eye+i]=Head;j[EyeHalo+i]=Head;j[Claw+i]=j[Orb+i]=j[OrbHalo+i]=i==0?LHand:RHand;}
            j[GrinUpper]=j[GrinLower]=Head;
            for(int i=0;i<2*WispSteps;i++)j[Wisp+i]=i<WispSteps?LKnee:RKnee;
            for(int t=0;t<TongueCount;t++)for(int s=0;s<TongueSteps;s++)j[Tongue+t*TongueSteps+s]=(int)Tongues[t*6];
            for(int i=0;i<NativeSirenDetail.Count;i++)j[Detail+i]=NativeSirenDetail.Bone(i);
            return j;
        }
        private static double Clamp(double x,double a,double b)=>x<a?a:x>b?b:x;
        /// <summary>The state before any motion (the runtime's rest look): melee, closed, anger .5, lit.</summary>
        public static SirenLookState Neutral(double time)=>new SirenLookState{Anger=.5,Glow=1,Time=time,Clock=time};
        /// <summary>applySirenLook's Math.round(anger·40)/40 (JavaScript rounds halves up).</summary>
        public static double Quantized(double anger)=>Math.Floor(anger*40+.5)/40;
        /// <summary>The eye outline index for an anger (shapeEye clamps the anger to 0..1).</summary>
        public static int Shape(double anger)=>(int)Clamp(Math.Floor(anger*40+.5),0,40);
        public static double EyeHaloAlpha(in SirenLookState s)=>EyeHaloOpacity*s.Glow;
        /// <summary>
        /// shapeEye: 40 fan triangles (centre (0,-.15), then the rim points i and i+1 of a unit circle whose top is cut by a
        /// brow line sloping toward the nose); x, y of the 120 vertices into xy (240 values, z is 0). Side +1 is the left eye.
        /// </summary>
        public static void EyeOutline(int side,double anger,double[] xy)
        {
            double cut=1.25-1.07*Clamp(anger,0,1);
            for(int i=0;i<EyeSegments;i++)
            {
                int o=i*6;xy[o]=0;xy[o+1]=-.15;
                for(int k=0;k<2;k++){double a=(double)(i+k)/EyeSegments*Math.PI*2,x=Math.Cos(a);xy[o+2+k*2]=x;xy[o+3+k*2]=Math.Min(Math.Sin(a),cut+.5*x*side);}
            }
        }
        /// <summary>m[o..o+15] = T·R(Euler XYZ)·S, row-major (three.js Matrix4.compose).</summary>
        private static void Compose(double[] m,int o,double px,double py,double pz,double rx,double ry,double rz,double sx,double sy,double sz)
        {
            double a=Math.Cos(rx),b=Math.Sin(rx),c=Math.Cos(ry),d=Math.Sin(ry),e=Math.Cos(rz),f=Math.Sin(rz),ae=a*e,af=a*f,be=b*e,bf=b*f;
            m[o]=c*e*sx;m[o+1]=-c*f*sy;m[o+2]=d*sz;m[o+3]=px;
            m[o+4]=(af+be*d)*sx;m[o+5]=(ae-bf*d)*sy;m[o+6]=-b*c*sz;m[o+7]=py;
            m[o+8]=(bf-ae*d)*sx;m[o+9]=(be+af*d)*sy;m[o+10]=a*c*sz;m[o+11]=pz;
            m[o+12]=0;m[o+13]=0;m[o+14]=0;m[o+15]=1;
        }
        /// <summary>
        /// The stratified flame (Unity finish): tongue n's step i turns .52·i + .9·n about its own vertical axis and its girth
        /// scales by 1 + .16·sin(3.7·n + 2.3·i), fixed per step (tools/siren_design.mjs TWIST).
        /// </summary>
        public static void Twist(int n,int i,out double yaw,out double girth){yaw=.52*i+.9*n;girth=1+.16*Math.Sin(3.7*n+2.3*i);}
        private static readonly double[] twist=BuildTwist();
        private static double[] BuildTwist(){var t=new double[TongueCount*TongueSteps*3];for(int n=0;n<TongueCount;n++)for(int i=0;i<TongueSteps;i++){Twist(n,i,out double yaw,out double k);int o=(n*TongueSteps+i)*3;t[o]=Math.Cos(yaw);t[o+1]=Math.Sin(yaw);t[o+2]=k;}return t;}
        /// <summary>m[o..] = parent · T(p)·Ry(yaw)·S(sx·k, sy, sz·k), parent a TRS matrix with no shear (the tongue group); the twist (cos, sin, k) at tw.</summary>
        private static void Child(double[] m,int o,double[] g,double px,double py,double pz,double sx,double sy,double sz,int tw)
        {
            double c=twist[tw],sn=twist[tw+1],k=twist[tw+2];
            for(int r=0;r<3;r++)
            {
                int i=r*4;m[o+i]=(g[i]*c-g[i+2]*sn)*sx*k;m[o+i+1]=g[i+1]*sy;m[o+i+2]=(g[i]*sn+g[i+2]*c)*sz*k;m[o+i+3]=g[i]*px+g[i+1]*py+g[i+2]*pz+g[i+3];
            }
            m[o+12]=0;m[o+13]=0;m[o+14]=0;m[o+15]=1;
        }
        [ThreadStatic] private static double[] group;
        /// <summary>
        /// Every piece's joint-relative transform into m (Count·16) and its visibility into shown (Count), as applySirenLook
        /// leaves them for state s (the eye outline is Shape(s.Anger); the eye halos' opacity is EyeHaloAlpha).
        /// </summary>
        public static void Evaluate(in SirenLookState s,double[] m,bool[] shown)
        {
            double t=s.Time,ranged=s.Ranged,open=s.Open,squint=s.Squint;
            for(int i=0;i<2;i++)
            {
                double side=i==0?1:-1;
                // Eyes: blank ovals slanted down toward the nose; they widen with the mouth and narrow with the squint.
                Compose(m,(Eye+i)*16,side*.082,.206,.1645,0,0,side*.12,.05*(1+.06*open),.08*(1-.3*squint),1);shown[Eye+i]=true;
                Compose(m,(EyeHalo+i)*16,side*.079,.21,.2,0,0,0,.26,.26,1);shown[EyeHalo+i]=true;
                // Claws: the group shortens along the fingers in ranged mode (never below .32, so always shown).
                double k=1-.68*ranged;Compose(m,(Claw+i)*16,0,-.1,.06,0,0,0,1,k,1);shown[Claw+i]=k>.05;
                // Palm orbs from 2% ranged, pulsing on the Siren clock and swelling with the flare; the halo 4.2 times as wide.
                bool lit=ranged>.02;double r=.085*(.15+.85*ranged)*(1+.12*Math.Sin(s.Clock*12+i))*(1+s.Flare*.6),orb=lit?r:.001,halo=lit?r*4.2:.001;
                Compose(m,(Orb+i)*16,0,-.13,.07,0,0,0,orb,orb,orb);shown[Orb+i]=lit;
                Compose(m,(OrbHalo+i)*16,0,-.13,.07,0,0,0,halo,halo,halo);shown[OrbHalo+i]=lit;
            }
            // The grin group sits at z .1638 of the head; the lower row drops and stretches as the jaw parts.
            Compose(m,GrinUpper*16,0,.006*open,.1638,0,0,0,1,1,1);shown[GrinUpper]=true;
            Compose(m,GrinLower*16,0,-.03*open,.1638,0,0,0,1+.06*open,1+.5*open,1);shown[GrinLower]=true;
            // Living flame: tongues flicker in height and whip sideways; moving bends them back; each step sways more.
            var g=group??(group=new double[16]);
            for(int n=0;n<TongueCount;n++)
            {
                int o=n*6;double x=Tongues[o+1],y=Tongues[o+2],z=Tongues[o+3],height=Tongues[o+4],lean=Tongues[o+5],phase=n*1.7+x*9;
                double flick=.5+.5*Math.Sin(t*7.3+phase)*Math.Sin(t*3.1+phase*1.9);
                Compose(g,0,x,y,z,lean-.5*s.Lean+.14*Math.Sin(t*2.6+phase),0,.24*Math.Sin(t*3.4+phase*1.3),1,height*(.7+.55*flick)*(1+s.Flare*.5),1);
                double at=0;
                for(int i=0;i<TongueSteps;i++)
                {
                    double size=TongueStep[i*2],h=TongueStep[i*2+1];int piece=Tongue+n*TongueSteps+i;
                    Child(m,piece*16,g,.012*i*Math.Sin(t*6.1+phase+i*.7),at+h/2,.008*i*Math.Sin(t*5.3+phase*1.3+i*.9),size,h,size,(n*TongueSteps+i)*3);shown[piece]=true;at+=h*.9;
                }
            }
            // Below each knee the leg is a tapering wisp: each box sways, bobs and pulses; the tips trail the lean.
            for(int i=0;i<2*WispSteps;i++)
            {
                int step=i%WispSteps;double y=WispStep[step*2],size=WispStep[step*2+1],f=(double)step/WispSteps;
                Compose(m,(Wisp+i)*16,.035*f*Math.Sin(t*4.1+i*1.3),y-.01*Math.Sin(t*5+i),-.01-.12*f*s.Lean,0,0,0,size*(.85+.25*Math.Sin(t*6.2+i*2.1)),.1,size*(.85+.25*Math.Sin(t*5.3+i)));shown[Wisp+i]=true;
            }
            // The Unity finish's moving pieces (the static ones hold what Rest wrote): the cheeks lift with the squint.
            NativeSirenDetail.Evaluate(s,m,shown,Detail,Claw,NativeSirenDetail.CheekLift(squint));
        }
        /// <summary>The Unity finish's static pieces (written once: Evaluate leaves them as they are) and every detail piece's visibility.</summary>
        public static void Rest(double[] m,bool[] shown)=>NativeSirenDetail.Rest(m,shown,Detail);
        /// <summary>
        /// The face targets of clip at age (NativeSourceMotion.Sample's frame mapping: looped clips wrap, others span
        /// duration, or the clip's own length when duration is 0). False when the clip has no frames.
        /// </summary>
        public static bool Targets(SirenLookClip clip,double age,double duration,out double open,out double squint)
        {
            open=squint=0;if(clip==null||clip.frames<1||clip.values==null||clip.values.Length<clip.frames*2)return false;
            double length=clip.duration>0?clip.duration:1,fraction=clip.loop?(age-Math.Floor(age/length)*length)/length:Clamp(age/(duration>0?duration:length),0,1);
            double index=fraction*(clip.frames-1);int a=Math.Min(clip.frames-1,Math.Max(0,(int)index)),b=Math.Min(clip.frames-1,a+1);double w=index-a;var v=clip.values;
            open=v[a*2]+(v[b*2]-v[a*2])*w;squint=v[a*2+1]+(v[b*2+1]-v[a*2+1])*w;return true;
        }
        /// <summary>
        /// animateSirenRig's mode blend: smoothstep over .35 s from the mode change (modeAt) toward the replicated mode, settled
        /// before it (a guest's modeAt can sit ahead of its render clock). Drives the claws, the palm orbs and the stance.
        /// </summary>
        public static double SwapBlend(bool ranged,double time,double modeAt)
        {double swap=Clamp((time-modeAt)/Swap,0,1),eased=swap*swap*(3-2*swap);return ranged?eased:1-eased;}
        /// <summary>The swap shudder's weight, sin(π·span(time, modeAt, modeAt + .5)) inside the window, else 0.</summary>
        public static double ShudderWeight(double time,double modeAt)=>time-modeAt<Shudder?Math.Sin(Clamp((time-modeAt)/Shudder,0,1)*Math.PI):0;
        /// <summary>
        /// The mode change time a viewer can know: the authoritative time of the snapshot that first shows the new mode (the
        /// host's tick, a guest's newest frame, the killcam's frame), which only lags the source's modeAt by one snapshot. A
        /// stamp more than a second from the presentation clock is not trusted.
        /// </summary>
        public static double ModeStamp(double stamp,double time)=>!double.IsNaN(stamp)&&Math.Abs(stamp-time)<=1?stamp:time;
        /// <summary>three.js Euler.setFromRotationMatrix, order XYZ, of a unit quaternion.</summary>
        private static void ToEuler(Quaternion q,out double x,out double y,out double z)
        {
            double qx=q.X,qy=q.Y,qz=q.Z,qw=q.W,m11=1-2*(qy*qy+qz*qz),m12=2*(qx*qy-qw*qz),m13=2*(qx*qz+qw*qy),m22=1-2*(qx*qx+qz*qz),m23=2*(qy*qz-qw*qx),m32=2*(qy*qz+qw*qx),m33=1-2*(qx*qx+qy*qy);
            y=Math.Asin(Clamp(m13,-1,1));if(Math.Abs(m13)<.9999999){x=Math.Atan2(-m23,m33);z=Math.Atan2(-m12,m11);}else{x=Math.Atan2(m32,m22);z=0;}
        }
        private static double MixAngle(double a,double b,double w){double d=b-a;d-=2*Math.PI*Math.Floor(d/(2*Math.PI)+.5);return a+d*w;}
        /// <summary>
        /// motion.ts stance(t, ranged) and the swap shudder from the two idle clips sampled at the same time (source frame).
        /// Every joint the mode moves is linear in the blend in its XYZ Euler angles (the arms, elbows and hands), so blending
        /// the angles of the melee and ranged stances reproduces the source's in-between stance; the shudder then adds
        /// .15·k and sin(50t)·.05·k to the chest, -.3·k to the head's roll and -.6·k to each hand. `into` may be `melee`.
        /// </summary>
        public static void SwapBody(CapturedRig.Pose melee,CapturedRig.Pose ranged,double blend,double shudder,double time,CapturedRig.Pose into)
        {
            float w=(float)blend;double chestRoll=Math.Sin(time*50)*.05*shudder;
            for(int b=0;b<CapturedRig.Bones.Length;b++)
            {
                Quaternion q0=melee.Rotation[b],q1=ranged.Rotation[b];into.Position[b]=Vector3.Lerp(melee.Position[b],ranged.Position[b],w);
                double sx=b==CapturedRig.Chest?.15*shudder:b==CapturedRig.LHand||b==CapturedRig.RHand?-.6*shudder:0,sz=b==CapturedRig.Chest?chestRoll:b==CapturedRig.Head?-.3*shudder:0;
                if(q0==q1&&sx==0&&sz==0){into.Rotation[b]=q0;continue;}
                ToEuler(q0,out double x0,out double y0,out double z0);ToEuler(q1,out double x1,out double y1,out double z1);
                NativeFxMath.EulerQuaternion(MixAngle(x0,x1,blend)+sx,MixAngle(y0,y1,blend),MixAngle(z0,z1,blend)+sz,out double qx,out double qy,out double qz,out double qw);
                into.Rotation[b]=new Quaternion((float)qx,(float)qy,(float)qz,(float)qw);
            }
        }
        /// <summary>finishLook's flare: Hellfire and Damnation burn hardest at the release (their windup), fading over .2 s.</summary>
        public static double Flare(string action,double age)
        {double hit=action=="hellfire"?HellfireHit:action=="damnation"?DamnationHit:-1;return hit<0?0:Math.Max(0,1-Math.Abs(age-hit)/.2);}
        /// <summary>The releases (NativeSirenRules windups) the flare peaks at.</summary>
        public const double HellfireHit=.16,DamnationHit=.55;
        /// <summary>The Damnation ball's halo sprite (haloMaterial 0x8f78e8 at .6).</summary>
        public const int ChargeHaloColor=0x8f78e8;public const double ChargeHaloOpacity=.6;
        /// <summary>
        /// finishLook's Damnation ball: through the windup until .02 s past the release it swells overhead (radius .08 + .44·p²,
        /// rising from 1.02 to 1.42 above the rig root, spinning and quivering) with a halo six radii wide. The ball's and the
        /// halo's transforms relative to the rig root into m (32 values); false when no ball shows.
        /// </summary>
        public static bool Charge(string action,double age,double time,double[] m)
        {
            if(action!="damnation"||age>=DamnationHit+.02)return false;
            double dm=Clamp(age/Math.Max(.05,DamnationHit),0,1),r=.08+.44*dm*dm,y=1.42-(1-dm)*.4,ball=r*(1+.08*Math.Sin(time*30));
            Compose(m,0,0,y,.12,0,time*5,0,ball,ball,ball);Compose(m,16,0,y,.12,0,0,0,r*6,r*6,r*6);return true;
        }

        // The Unity finish of the black-flame look (owner playtest 2026-10-10), on top of the source's shapes and colours.
        /// <summary>
        /// The ink rim and rising-streak strengths of buildSiren's InkMaterials: the body (INK, rim .2, flow .025), the claws
        /// (CLAW, .75 and .02) and the flame tongues and leg wisps (TONGUE, .42 and .1), where the bank keeps one ink flag. The
        /// arena draws them without the source's tone mapping, so the rims read a little brighter on the dark maps.
        /// </summary>
        public const double ClawRim=.75,ClawFlow=.02,TongueRim=.42,TongueFlow=.1,BodyRim=.2,BodyFlow=.025;
        /// <summary>
        /// The world flame pool's cubes (flames.ts, unlit flat colour in the source) take a faint ash rim at this strength,
        /// pow(1-|N·V|,3) like the ink: their black cores stay black, their edges catch a violet line, so the fire reads as
        /// black flame on a dark map instead of scattered violet tips and embers. Reduced effects draw them flat.
        /// </summary>
        public const double FlameRim=.42;
        /// <summary>The eyes' and grin's slow breathing, 0..1 on the Siren clock (about .38 Hz); 1 is the source's look.</summary>
        public static double Breath(double clock)=>.5+.5*Math.Sin(clock*2.4);
        /// <summary>The grin's glow over a breath: .88 at the trough to the source's 1.</summary>
        public static double GrinGlow(double breath)=>.88+.12*Clamp(breath,0,1);
        /// <summary>The eye halos' size over a breath: .92 at the trough to the source's 1.</summary>
        public static double HaloSize(double breath)=>.92+.08*Clamp(breath,0,1);
    }

    /// <summary>
    /// One Siren's smoothed look (motion.ts animateSirenRig + finishLook): the claw/orb blend and the stance follow the mode
    /// swap (smoothstep over .35 s from modeAt, then the shudder to .5 s), the mouth and squint ease toward the clip's
    /// targets at rate 18, and the forward drift eases at rate 6 into the lean. The source reads its replicated modeAt; the
    /// native pose replicates only the mode, so modeAt is the authoritative time of the first snapshot that shows the new
    /// mode (ModeStamp): exact to a tick on the host, at most one frame late on a guest, whose render clock trails its newest
    /// frame (the blend no longer starts early there), and in the killcam replay.
    /// </summary>
    public sealed class NativeSirenLookSmoother
    {
        public double Open,Squint,Forward,ModeAt=-100,Last;public bool Ranged,Started;
        public void Reset(){Started=false;Open=Squint=Forward=0;ModeAt=-100;}
        /// <summary>The frame step for a draw at `time` (clamped to .05 s like the source; 0 on the first frame).</summary>
        public double Delta(double time)=>!Started||time<Last-.25?0:Math.Max(0,Math.Min(.05,time-Last));
        /// <summary>
        /// This frame's look: `ranged` is the replicated mode and `stamp` the authoritative time of the snapshot it came from
        /// (NativeArena.Time: the host's sim time, a guest's newest frame, the killcam's frame).
        /// </summary>
        public SirenLookState Step(double time,double dt,double open,double squint,double flare,bool ranged,double forwardSpeed,bool alive,double stamp,double? sourceModeAt=null)
        {
            // A fresh Siren (or a replay that jumped back) starts settled in its current mode, mouth closed, upright. One drawn
            // again after a gap (respawned, back from first person or the killcam) settles into its mode: its swap is unknown.
            if(!Started||time<Last-.25){Started=true;Open=Squint=Forward=0;Ranged=ranged;ModeAt=-100;}
            else if(time>Last+.25){Ranged=ranged;ModeAt=-100;}
            Last=time;if(sourceModeAt.HasValue){Ranged=ranged;ModeAt=sourceModeAt.Value;}else if(ranged!=Ranged){Ranged=ranged;ModeAt=NativeSirenLook.ModeStamp(stamp,time);}
            dt=Math.Max(0,Math.Min(.05,dt));double k=1-Math.Exp(-dt*NativeSirenLook.FaceRate),k6=1-Math.Exp(-dt*NativeSirenLook.LeanRate);
            Forward+=(forwardSpeed-Forward)*k6;Open+=(open-Open)*k;Squint+=(squint-Squint)*k;
            return new SirenLookState{Ranged=NativeSirenLook.SwapBlend(ranged,time,ModeAt),Open=Open,Squint=Squint,Anger=Math.Max(0,Math.Min(1,.5+Squint*.8+Open*.5)),Glow=alive?1:0,Time=time,Lean=Math.Max(-1,Math.Min(1,Forward/6.8)),Flare=flare,Clock=time};
        }
        /// <summary>Whether the swap still moves the body at `time` (the blend or the shudder, or a modeAt still ahead).</summary>
        public bool Swapping(double time)=>Started&&time<ModeAt+NativeSirenLook.Shudder;
        /// <summary>The swap shudder's weight at `time` (NativeSirenLook.ShudderWeight).</summary>
        public double Shudder(double time)=>NativeSirenLook.ShudderWeight(time,ModeAt);
    }
}

using System;

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
    /// 2 orb halos, 12 wisps (left knee then right, top to tip), 16 tongues × 7 steps (TONGUES order, base to tip).
    /// </summary>
    public static class NativeSirenLook
    {
        public const int Eye=0,EyeHalo=2,GrinUpper=4,GrinLower=5,Claw=6,Orb=8,OrbHalo=10,Wisp=12,Tongue=24,Count=136;
        public const int TongueCount=16,TongueSteps=7,WispSteps=6,EyeSegments=40,Shapes=41;
        public const double EyeHaloOpacity=.42,OrbHaloOpacity=.32,Swap=.35,FaceRate=18,LeanRate=6;
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
        /// <summary>m[o..] = parent · T(p)·S(s), where parent is a TRS matrix with no shear (the tongue group).</summary>
        private static void Child(double[] m,int o,double[] g,double px,double py,double pz,double sx,double sy,double sz)
        {
            for(int r=0;r<3;r++)
            {
                int i=r*4;m[o+i]=g[i]*sx;m[o+i+1]=g[i+1]*sy;m[o+i+2]=g[i+2]*sz;m[o+i+3]=g[i]*px+g[i+1]*py+g[i+2]*pz+g[i+3];
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
                    Child(m,piece*16,g,.012*i*Math.Sin(t*6.1+phase+i*.7),at+h/2,.008*i*Math.Sin(t*5.3+phase*1.3+i*.9),size,h,size);shown[piece]=true;at+=h*.9;
                }
            }
            // Below each knee the leg is a tapering wisp: each box sways, bobs and pulses; the tips trail the lean.
            for(int i=0;i<2*WispSteps;i++)
            {
                int step=i%WispSteps;double y=WispStep[step*2],size=WispStep[step*2+1],f=(double)step/WispSteps;
                Compose(m,(Wisp+i)*16,.035*f*Math.Sin(t*4.1+i*1.3),y-.01*Math.Sin(t*5+i),-.01-.12*f*s.Lean,0,0,0,size*(.85+.25*Math.Sin(t*6.2+i*2.1)),.1,size*(.85+.25*Math.Sin(t*5.3+i)));shown[Wisp+i]=true;
            }
        }
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
    }

    /// <summary>
    /// One Siren's smoothed look (motion.ts animateSirenRig + finishLook): the claw/orb blend follows the mode swap
    /// (smoothstep over .35 s from the frame the replicated ranged flag flipped; the source reads its replicated modeAt), the
    /// mouth and squint ease toward the clip's targets at rate 18, and the forward drift eases at rate 6 into the lean.
    /// </summary>
    public sealed class NativeSirenLookSmoother
    {
        public double Open,Squint,Forward,ModeAt=-100,Last;public bool Ranged,Started;
        public void Reset(){Started=false;Open=Squint=Forward=0;ModeAt=-100;}
        /// <summary>The frame step for a draw at `time` (clamped to .05 s like the source; 0 on the first frame).</summary>
        public double Delta(double time)=>!Started||time<Last-.25?0:Math.Max(0,Math.Min(.05,time-Last));
        public SirenLookState Step(double time,double dt,double open,double squint,double flare,bool ranged,double forwardSpeed,bool alive)
        {
            // A fresh Siren (or a replay that jumped back) starts settled in its current mode, mouth closed, upright.
            if(!Started||time<Last-.25){Started=true;Open=Squint=Forward=0;Ranged=ranged;ModeAt=-100;}
            Last=time;if(ranged!=Ranged){Ranged=ranged;ModeAt=time;}
            double swap=Math.Max(0,Math.Min(1,(time-ModeAt)/NativeSirenLook.Swap)),eased=swap*swap*(3-2*swap);
            dt=Math.Max(0,Math.Min(.05,dt));double k=1-Math.Exp(-dt*NativeSirenLook.FaceRate),k6=1-Math.Exp(-dt*NativeSirenLook.LeanRate);
            Forward+=(forwardSpeed-Forward)*k6;Open+=(open-Open)*k;Squint+=(squint-Squint)*k;
            return new SirenLookState{Ranged=ranged?eased:1-eased,Open=Open,Squint=Squint,Anger=Math.Max(0,Math.Min(1,.5+Squint*.8+Open*.5)),Glow=alive?1:0,Time=time,Lean=Math.Max(-1,Math.Min(1,Forward/6.8)),Flare=flare,Clock=time};
        }
    }
}

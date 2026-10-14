using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The heavy kinds' body animation (owner 2026-10-14: "Slam/landing/wall break animation also needs improvements from
    /// hulk/brute. Hulk zombie needs adjustments on their movement (normal zombie movement looks odd at their size). I'd like them
    /// to all have a charge animation (they can charge through walls) and a couple of punch animations (like a straight punch,
    /// and a swipe)"). Presentation only: poses from the replicated state and the clock, never read by the authority; no hit
    /// timing, hit volume, damage, cooldown, movement or network change. Every contact lands on the time the rules (or the
    /// proposed contract, Core/NativeHulkMoves) already use, so the effects keyed on those times line up.
    /// <list type="bullet">
    /// <item>The Hulk's locomotion is its own (<see cref="HeavyStride"/>): a long, slow, heavy stride the planted feet walk on
    /// (no zombie shamble scaled up), the body swaying over the stance foot, the pelvis and shoulders rolling and counter-turning,
    /// a jolt down the body at each footfall, arms hanging wide and swinging late, the head held steady, the feet lifted and
    /// stamped down; standing, it breathes and turns in place on pivot steps, its shoulders leading.</item>
    /// <item>Keyed moves over the capture (tables of key poses, each eased into the next, a side mirrored for the left arm): the
    /// smash's rise, drop and fists on the floor at the strike (the Hulk and the brute, over the pack's hammer), the breach's
    /// three blows on the wall (a right, a left, the shoulder that breaks it on the break, then the lowered-shoulder carry
    /// through), and the proposed straight punch, swipe and charge (wind-up, lowered-shoulder run, the hit, the solid stop, the
    /// spent run), dormant until the rules replicate them.</item>
    /// <item>The landings (Drop):
    /// fists raised for the last of the fall, then the Hulk's three-point landing (a fist on the floor) and the brute's two-fist
    /// landing at LandAt exactly, held through the slam and rising out of it by ActiveAt.</item>
    /// </list>
    /// Fixed tables and fields: no allocation per frame.
    /// </summary>
    public sealed partial class NativeHordeGait
    {
        // ================================================================================================================
        // The heavy stride: rig metres per cycle (two steps) against the rig speed, the stance share, the stamp's lift
        // ================================================================================================================
        // (The Hulk's .75 / 1.15 m/s are .42 / .64 on the rig: steps of about .8 / 1 m in the world, under one a second; a
        // charge's 4.5 m/s (2.5 on the rig) runs long and low on short quick strides, the legs being a pilot's grown.)
        private static readonly float[] HeavyV={0,.2f,.4167f,.639f,1.2f,2.5f},HeavyS={.5f,.68f,.88f,1.06f,1.3f,1.5f};
        /// <summary>The heavy stride at rig speed `v` (m per cycle of two steps).</summary>
        public static float HeavyStride(float v)
        {
            if(v<=HeavyV[0])return HeavyS[0];
            for(int i=1;i<HeavyV.Length;i++)if(v<HeavyV[i]){float u=(v-HeavyV[i-1])/(HeavyV[i]-HeavyV[i-1]);return HeavyS[i-1]+(HeavyS[i]-HeavyS[i-1])*u;}
            return HeavyS[HeavyS.Length-1];
        }
        /// <summary>The share of the cycle each foot stands (a walk keeps both feet down long; a charge flies between steps).</summary>
        public static float HeavyDuty(float v)=>v<=.4167f?.66f:v<=.639f?Lerp(.66f,.5f,(v-.4167f)/.2223f):Lerp(.5f,.38f,Clamp01((v-.639f)/1.36f));
        /// <summary>How high a foot is lifted (rig m) before it is stamped down.</summary>
        public static float HeavyLift(float v)=>v<=.4167f?.09f:Lerp(.09f,.14f,Clamp01((v-.4167f)/1.6f));
        /// <summary>How much of a sprint (a charge's run) the speed is (0 walking and plodding .. 1).</summary>
        public static float Sprint(float v)=>Clamp01((v-.8f)/1.2f);
        /// <summary>A turn in place: a pivot starts past <see cref="PivotFrom"/> degrees, turns at most <see cref="PivotMost"/> in
        /// one pivot step of up to <see cref="PivotSeconds"/>; the other foot follows.</summary>
        public const float PivotFrom=9,PivotMost=50,PivotSeconds=.55f;
        /// <summary>The Hulk's turn rate walking and running (deg/s; a wind-up, a knock or a charge's wind-up turns fast).</summary>
        public const float HeavyTurn=135,HeavyTurnFast=410;

        // ---- heavy state ----
        private float lastLand=float.NaN,pivotFrom,pivotBy,pivotT0,pivotT1,turnLead,scrapedFor=float.NaN;private bool pivoting,heavy;
        private void HeavyReset(){lastLand=float.NaN;pivoting=false;turnLead=0;scrapedFor=float.NaN;hipsBack=Vector2.Zero;yawRate=0;wasMoving=false;}
        private bool wasMoving;
        /// <summary>The cycle phase a heavy gait sets off on (the left foot's mid-stance at the walk's .66 stance share).</summary>
        public const float StartPhase=.33f;
        /// <summary>The facing: walking it turns at the heavy rate; standing it turns on pivot steps (a foot lifted on the side it
        /// turns to, the body turning while it swings, the other foot following); a wind-up, a knock or a charge's start turns fast.</summary>
        private void HeavyYaw(in HordeMotionInput f,float dt,bool fast,bool fresh)
        {
            float diff=Wrap(f.Yaw-Yaw);
            if(fresh){Yaw=f.Yaw;pivoting=false;}
            else if(Speed<=.2f&&!fast&&!f.Charging)
            {
                if(pivoting){float s=Clamp01((f.Time-pivotT0)/Math.Max(1e-3f,pivotT1-pivotT0));Yaw=Wrap(pivotFrom+pivotBy*Sm(s));if(s>=1)pivoting=false;}
                else if(Math.Abs(diff)>PivotFrom&&!feet[0].Swing&&!feet[1].Swing)
                {
                    // Turning right (a larger Unity yaw) the right foot leads.
                    pivotBy=Math.Max(-PivotMost,Math.Min(PivotMost,diff));pivotFrom=Yaw;pivotT0=f.Time;pivotT1=f.Time+PivotSeconds*(.6f+.4f*Math.Abs(pivotBy)/PivotMost);pivoting=true;
                    ref var ft=ref feet[pivotBy>0?1:0];ft.Swing=true;ft.Scrape=false;ft.From=ft.W;ft.T0=f.Time;ft.T1=pivotT1;ft.Lift=.075f;
                }
            }
            else{pivoting=false;float rate=(fast?HeavyTurnFast:HeavyTurn)*dt;Yaw=Wrap(Yaw+Math.Max(-rate,Math.Min(rate,diff)));}
            turnLead=Wrap(f.Yaw-Yaw)*(float)Math.PI/180;
        }
        /// <summary>A stamped step's lift (0..1 over the swing): up quickly, carried, dropped hard at the end.</summary>
        public static float Stamp(float s)=>Sm(s/.38f)*(1-Sm((s-.68f)/.32f));
        /// <summary>The jolt down the body after a footfall `t` s ago (peaks about 1 at .04 s, gone by .6 s).</summary>
        public static float Jolt(float t)=>t<0||t>.6f?0:(1-(float)Math.Exp(-t/.022f))*(float)Math.Exp(-t/.11f)*1.72f;

        /// <summary>The Hulk's arms over the capture's (chest-space reach targets, before the rig is built): hanging wide off the
        /// big shoulders, swung late against the legs (further and more bent running), the idle's own arms a quarter at rest.</summary>
        private void HeavyArms(float[] row,float moving,float run,float sprint,float t)
        {
            float u=Frac(Phase),sw=(float)Math.Cos(2*Math.PI*(u-.07f)),amp=(.26f+.22f*run+.25f*sprint)*moving;
            float len=.48f-.05f*run-.06f*sprint,lean=HeavyLean(moving,run,sprint),k=.75f+.25f*moving;
            for(int h=0;h<2;h++)
            {
                int c=h==0?39:51,pc=h==0?48:60;float side=h==0?-1:1;
                float a=lean+(h==0?amp:-amp)*sw+.05f*(1-moving)*(float)Math.Sin(t*1.3f+h*2.1f),flare=.1f+.04f*run;
                float x=.345f*side+side*flare,y=.18f-len*(float)Math.Cos(a),z=len*(float)Math.Sin(a)+.04f*run;
                row[c]+=(x-row[c])*k;row[c+1]+=(y-row[c+1])*k;row[c+2]+=(z-row[c+2])*k;
                row[pc]+=(side*.8f-row[pc])*k;row[pc+1]+=(-.35f-row[pc+1])*k;row[pc+2]+=(-.5f-row[pc+2])*k;
            }
        }
        private static float HeavyLean(float moving,float run,float sprint)=>LeanBase+(LeanWalk+LeanRun*run+LeanSprint*sprint)*moving;
        private const float LeanBase=.03f,LeanWalk=.08f,LeanRun=.1f,LeanSprint=.08f;
        /// <summary>Where the hips sit off the root (source frame x, z; smoothed): a heavy body balancing its lean over the hit
        /// volume sits its hips back, and its feet are planted under them, not under the root.</summary>
        private Vector2 hipsBack;
        private void HeavyBalanced(Vector3 shift){float k=1-(float)Math.Exp(-lastDt/.12f);hipsBack+=(new Vector2(shift.X,shift.Z)-hipsBack)*k;}
        /// <summary>The presented yaw's turn rate (rad/s, positive to the right; smoothed).</summary>
        private float yawRate;
        private void HeavyTurning(float degrees,float dt,bool fresh){if(fresh||dt<=0){yawRate=0;return;}float k=1-(float)Math.Exp(-dt/.15f);yawRate+=(degrees*(float)Math.PI/180/dt-yawRate)*k;}
        /// <summary>A turning stride's reach for foot `i`: the foot on the outside of the turn travels further under the body, so it
        /// lands further ahead (and the inside one shorter), keeping both inside the legs' reach.</summary>
        private float TurnReach(int i,float width)=>Speed<.05f?1:Math.Max(.6f,Math.Min(1.4f,1-yawRate*SideOf(i)*width/Speed));
        /// <summary>The hips' balance offset in the world (source x is the add's left), walking (standing, the feet stay put).</summary>
        private Vector2 HipsUnder()=>(-Right*hipsBack.X+Forward*hipsBack.Y)*Clamp01(Speed/.3f);
        /// <summary>The Hulk's body over its stride: the weight over the stance foot, the plod's dip after each strike, bent knees,
        /// the lean, the pelvis and shoulders rolling and counter-turning, the footfall's jolt (the chest and head a beat later),
        /// a slow breath standing, the shoulders leading a turn, and the head held steady against it all.</summary>
        private void HeavyTrunk(CapturedRig.Pose p,in HordeMotionInput f,float moving,float run,float sprint)
        {
            float u=Frac(Phase),cl=duty0*.5f,m=moving;
            float c1=(float)Math.Cos(2*Math.PI*(u-cl)),c2=(float)Math.Cos(4*Math.PI*(u-cl)),c0=(float)Math.Cos(2*Math.PI*u);
            float sway=(.032f+.015f*run-.025f*sprint)*m,bob=(.016f+.012f*run+.012f*sprint)*m,crouch=.025f+(.025f+.03f*run+.025f*sprint)*m,lean=HeavyLean(m,run,sprint);
            float twistP=(.09f+.04f*run)*m,twistC=(.14f+.08f*run)*m,rollP=.055f*m,rollC=(.06f+.03f*run)*m;
            float breathe=(float)Math.Sin(f.Time*2*Math.PI/3.4f+NativeHordeKinds.Hash(seed,47)*6.28f)*(1-.6f*m);
            float since=float.IsNaN(lastLand)?9:f.Time-lastLand,jolt=Jolt(since),nod=Jolt(since-.05f),J=(.03f+.025f*run+.02f*sprint)*Math.Max(.45f,m);
            var b=p.Position[CapturedRig.Body];b.X+=sway*c1;b.Y+=bob*c2-crouch-J*jolt+.006f*breathe;p.Position[CapturedRig.Body]=b;
            // (A positive bend yaw turns the rig toward its left; a positive lead is the authority's facing to the right of it.)
            float lead=-Math.Max(-.5f,Math.Min(.5f,turnLead))*(1-.6f*m);
            Bend(p,CapturedRig.Pelvis,.03f*m,-twistP*c0+lead*.15f,rollP*c1);
            Bend(p,CapturedRig.Spine,lean*.45f+.03f*nod,twistC*.4f*c0+lead*.3f,-rollC*.5f*c1);
            Bend(p,CapturedRig.Chest,lean*.55f+.05f*nod-.015f*breathe,twistC*.6f*c0+lead*.4f,-rollC*.5f*c1);
            Bend(p,CapturedRig.Neck,-lean*.3f+.05f*nod,-twistC*.3f*c0+lead*.25f,rollC*.3f*c1);
            Bend(p,CapturedRig.Head,-lean*.35f+.07f*nod,-twistC*.35f*c0+lead*.3f,rollC*.35f*c1);
        }
        /// <summary>A heavy leg's knee: forward and a little out (bowed under the weight).</summary>
        private static Vector3 HeavyPole(bool left)=>Vector3.Normalize(new Vector3(left?.18f:-.18f,0,1));
        /// <summary>A heavy swinging foot: the toe hangs as it lifts, comes up before the stamp, lands flat.</summary>
        private static float HeavyToe(float s)=>.35f*(1-Sm(s/.55f))-.18f*Sm((s-.5f)/.3f)*(1-Sm((s-.9f)/.1f));

        // ================================================================================================================
        // Keyed moves: key poses (body offset, pelvis / spine / chest / head bends, both hands' targets) eased into each other
        // ================================================================================================================
        // A key: {anchor, offset, ease into it, channels}. Anchors: 0 seconds from the start, 1 from the strike, 2 from the
        // recovery, 3 the offset times the strike, 4 the strike plus the offset times (recovery - strike). Eases: 0 smooth,
        // 1 in (accelerating into the key: a blow's contact), 2 out (settling into it), 3 linear. Hands are rig-frame wrist
        // targets (+x the add's left, +z ahead, the floor at -.85) with a weight (0: the arm is the gait's or the take's).
        public const int KBx=0,KBy=1,KBz=2,KPp=3,KPy=4,KPr=5,KSp=6,KSy=7,KSr=8,KCp=9,KCy=10,KCr=11,KHp=12,KHy=13,KHr=14,KRx=15,KRy=16,KRz=17,KRw=18,KLx=19,KLy=20,KLz=21,KLw=22,KN=23;
        private static float[] K(int a,float o,int e=0,float bx=0,float by=0,float bz=0,float pp=0,float py=0,float pr=0,float sp=0,float sy=0,float sr=0,float cp=0,float cy=0,float cr=0,float hp=0,float hy=0,float hr=0,
            float rx=0,float ry=0,float rz=0,float rw=0,float lx=0,float ly=0,float lz=0,float lw=0)
            =>new[]{a,o,e,bx,by,bz,pp,py,pr,sp,sy,sr,cp,cy,cr,hp,hy,hr,rx,ry,rz,rw,lx,ly,lz,lw};
        private const int Smooth=0,In=1,Out=2,Linear=3;
        /// <summary>A key's time for a move striking at `h` and recovering at `r`.</summary>
        private static float KeyTime(float[] k,float h,float r)
        {
            switch((int)k[0]){case 1:return h+k[1];case 2:return r+k[1];case 3:return k[1]*h;case 4:return h+k[1]*(r-h);default:return k[1];}
        }
        private static float Ease(int e,float s)=>e==In?s*s*s:e==Out?1-(1-s)*(1-s)*(1-s):e==Linear?s:Sm(s);
        /// <summary>
        /// The move's channels at `age` into `into` (the hands' targets held where a key does not use them), mirrored for the left
        /// arm (`side` -1). Returns the move's end (its last key's time). `times` is scratch at least as long as the table.
        /// </summary>
        public static float Keys(float[][] table,float h,float r,float age,int side,float[] into,float[] times)
        {
            int n=table.Length;float last=0;
            for(int i=0;i<n;i++){float t=KeyTime(table[i],h,r);if(i>0&&t<last+.005f)t=last+.005f;times[i]=t;last=t;}
            int seg=n-1;float s=1;
            if(age<=times[0]){seg=0;s=0;}
            else for(int i=0;i+1<n;i++)if(age<times[i+1]){seg=i;s=(age-times[i])/(times[i+1]-times[i]);break;}
            var a=table[seg];var b=seg+1<n?table[seg+1]:a;float e=seg+1<n?Ease((int)b[2],Clamp01(s)):0;
            for(int c=0;c<KN;c++)into[c]=a[3+c]+(b[3+c]-a[3+c])*e;
            // A hand's target only matters where its weight is: held at the key that uses it.
            for(int hand=0;hand<2;hand++)
            {
                int c0=hand==0?KRx:KLx;float wa=a[3+c0+3],wb=b[3+c0+3];
                if(wa<=0&&wb>0)for(int c=0;c<3;c++)into[c0+c]=b[3+c0+c];else if(wb<=0&&wa>0)for(int c=0;c<3;c++)into[c0+c]=a[3+c0+c];
            }
            if(side<0)Mirror(into);
            return times[n-1];
        }
        /// <summary>The left arm's version of a right-armed pose: the sideways offset, every yaw and roll and the hands swapped across.</summary>
        private static void Mirror(float[] c)
        {
            c[KBx]=-c[KBx];for(int j=KPy;j<=KHr;j+=3){c[j]=-c[j];c[j+1]=-c[j+1];}
            for(int j=0;j<4;j++){float r=c[KRx+j],l=c[KLx+j];c[KRx+j]=l;c[KLx+j]=r;}c[KRx]=-c[KRx];c[KLx]=-c[KLx];
        }
        /// <summary>Lays a move's body and trunk on the pose by `w` (the spine, chest and head from `lag`: a beat behind the hips).</summary>
        private static void KeyTrunk(CapturedRig.Pose p,float[] c,float[] lag,float w)
        {
            if(w<=0)return;var b=p.Position[CapturedRig.Body];b+=new Vector3(c[KBx],c[KBy],c[KBz])*w;p.Position[CapturedRig.Body]=b;
            Bend(p,CapturedRig.Pelvis,c[KPp]*w,c[KPy]*w,c[KPr]*w);Bend(p,CapturedRig.Spine,lag[KSp]*w,lag[KSy]*w,lag[KSr]*w);Bend(p,CapturedRig.Chest,lag[KCp]*w,lag[KCy]*w,lag[KCr]*w);
            Bend(p,CapturedRig.Neck,lag[KHp]*.45f*w,lag[KHy]*.45f*w,lag[KHr]*.45f*w);Bend(p,CapturedRig.Head,lag[KHp]*.55f*w,lag[KHy]*.55f*w,lag[KHr]*.55f*w);
        }
        /// <summary>Reaches both hands toward a move's targets by their weights times `w`.</summary>
        private static void KeyHands(CapturedRig.Pose p,float[] c,float w)
        {
            if(w<=0)return;
            if(c[KRw]>0)Reach(p,false,new Vector3(c[KRx],c[KRy],c[KRz]),Math.Min(1,c[KRw])*w);
            if(c[KLw]>0)Reach(p,true,new Vector3(c[KLx],c[KLy],c[KLz]),Math.Min(1,c[KLw])*w);
        }
        /// <summary>One wrist toward a rig-frame target by `k` (the arm re-reached with its elbow's own direction, the fist
        /// continuing the forearm).</summary>
        public static void Reach(CapturedRig.Pose p,bool left,Vector3 target,float k)
        {
            if(k<=0)return;p.World();int h=left?CapturedRig.LHand:CapturedRig.RHand;var w=p.WorldPosition[h];var t=Vector3.Lerp(w,target,Math.Min(1,k));
            var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
            var elbow=p.WorldPosition[h-1];var shoulder=p.WorldPosition[h-2];var pole=elbow-(shoulder+w)*.5f;
            pole=CapturedRig.Unit(pole,Vector3.UnitY)+new Vector3(left?.45f:-.45f,-.55f,-.2f)*k;
            CapturedRig.Arm(p,left,Vector3.Transform(t-cpos,inv),inv*p.WorldRotation[h],Vector3.Transform(pole,inv));
            p.Rotation[h]=Quaternion.Slerp(p.Rotation[h],CapturedRig.Euler(-.12f),Math.Min(1,k));p.World();
        }

        // ---- the smash, over the pack's hammer: rising tall, dropping into it, both fists on the floor at the strike ----
        // (The pack's hammer already bends far over the blow: the keys add the rise, the drop and the fists on the floor, little bend.)
        private static readonly float[][] HulkSmash={
            K(0,0),
            K(3,.5f,by:.04f,bz:-.03f,pp:-.03f,sp:-.06f,cp:-.16f,hp:-.08f),
            K(1,-.22f,by:.07f,bz:-.05f,pp:-.05f,sp:-.12f,cp:-.26f,hp:-.16f),
            K(1,0,In,by:-.1f,bz:.04f,pp:.05f,sp:.09f,cp:.12f,hp:.04f,rx:-.17f,ry:-.6f,rz:.6f,rw:1,lx:.17f,ly:-.6f,lz:.6f,lw:1),
            K(1,.1f,Out,by:-.12f,bz:.03f,pp:.05f,sp:.1f,cp:.13f,hp:.05f,rx:-.18f,ry:-.6f,rz:.58f,rw:1,lx:.18f,ly:-.6f,lz:.58f,lw:1),
            K(1,.5f,by:-.08f,bz:.02f,pp:.03f,sp:.04f,cp:.05f,hp:.04f,rx:-.2f,ry:-.58f,rz:.56f,rw:.6f,lx:.2f,ly:-.58f,lz:.56f,lw:.6f),
            K(4,.6f,by:-.02f,cp:-.04f,hp:-.04f),
            K(2,0)};
        private static readonly float[][] BruteSmash={
            K(0,0),
            K(3,.5f,by:.03f,bz:-.02f,sp:-.05f,cp:-.12f,hp:-.06f),
            K(1,-.12f,by:.05f,bz:-.04f,pp:-.04f,sp:-.09f,cp:-.2f,hp:-.12f),
            K(1,0,In,by:-.06f,bz:.03f,pp:.02f,sp:.04f,cp:.05f,hp:.05f,rx:-.15f,ry:-.62f,rz:.52f,rw:1,lx:.15f,ly:-.62f,lz:.52f,lw:1),
            K(1,.08f,Out,by:-.08f,bz:.02f,pp:.03f,sp:.05f,cp:.06f,hp:.06f,rx:-.16f,ry:-.62f,rz:.5f,rw:1,lx:.16f,ly:-.62f,lz:.5f,lw:1),
            K(1,.3f,by:-.06f,sp:.03f,cp:.04f,hp:.03f,rx:-.18f,ry:-.6f,rz:.48f,rw:.55f,lx:.18f,ly:-.6f,lz:.48f,lw:.55f),
            K(4,.6f,by:-.02f,cp:-.03f),
            K(2,0)};
        // ---- the proposed straight punch (the right arm; mirrored for the left): coiled back, driven through to full reach at
        // the strike, held, recoiled ----
        private static readonly float[][] Punch={
            K(0,0),
            K(3,.55f,bx:-.03f,by:-.04f,bz:-.06f,py:-.12f,pr:.02f,sp:.06f,sy:-.15f,cp:.08f,cy:-.32f,cr:.04f,hp:-.05f,hy:.3f,rx:-.36f,ry:.02f,rz:-.22f,rw:1,lx:.28f,ly:.05f,lz:.38f,lw:.8f),
            K(1,-.12f,bx:-.04f,by:-.05f,bz:-.08f,py:-.15f,pr:.02f,sp:.07f,sy:-.18f,cp:.09f,cy:-.38f,cr:.05f,hp:-.05f,hy:.34f,rx:-.38f,ry:.04f,rz:-.26f,rw:1,lx:.29f,ly:.05f,lz:.4f,lw:.8f),
            K(1,0,In,bx:.02f,by:-.05f,bz:.12f,py:.14f,sp:.08f,sy:.12f,cp:.14f,cy:.3f,cr:-.04f,hp:.05f,hy:-.25f,rx:-.12f,ry:.16f,rz:.98f,rw:1,lx:.3f,ly:-.05f,lz:.05f,lw:.8f),
            K(1,.12f,Out,bx:.02f,by:-.06f,bz:.15f,py:.16f,sp:.09f,sy:.14f,cp:.16f,cy:.36f,cr:-.05f,hp:.06f,hy:-.28f,rx:-.12f,ry:.14f,rz:1f,rw:1,lx:.3f,ly:-.06f,lz:.04f,lw:.8f),
            K(4,.35f,by:-.03f,bz:.04f,py:.04f,sy:.04f,cp:.06f,cy:.08f,hy:-.06f,rx:-.3f,rz:.3f,rw:.6f,lx:.3f,ly:-.1f,lz:.1f,lw:.3f),
            K(2,-.25f,cp:.02f,rx:-.36f,ry:-.2f,rz:.12f,rw:.15f),
            K(2,0)};
        // ---- the proposed swipe (the right arm sweeping across to the left): drawn back wide, crossing the facing at the strike,
        // carried through and over-balanced ----
        private static readonly float[][] Swipe={
            K(0,0),
            K(3,.5f,bx:-.04f,by:-.03f,bz:-.03f,py:-.18f,sp:.04f,sy:-.2f,sr:.05f,cp:.05f,cy:-.55f,cr:.08f,hy:.4f,rx:-.78f,ry:.12f,rz:-.12f,rw:1,lx:.35f,ly:-.1f,lz:.3f,lw:.6f),
            K(1,-.14f,bx:-.05f,by:-.04f,bz:-.04f,py:-.2f,sp:.05f,sy:-.22f,sr:.06f,cp:.06f,cy:-.62f,cr:.09f,hy:.44f,rx:-.8f,ry:.16f,rz:-.2f,rw:1,lx:.36f,ly:-.1f,lz:.32f,lw:.6f),
            K(1,0,In,by:-.06f,bz:.06f,py:.05f,sp:.06f,sy:.1f,cp:.1f,cy:.2f,cr:-.05f,hy:-.15f,rx:-.05f,ry:.02f,rz:.84f,rw:1,lx:.4f,ly:-.15f,lz:.05f,lw:.6f),
            K(1,.16f,Out,bx:.05f,by:-.07f,bz:.05f,py:.2f,sp:.06f,sy:.25f,sr:-.04f,cp:.12f,cy:.7f,cr:-.1f,hy:-.4f,rx:.62f,ry:-.05f,rz:.38f,rw:1,lx:.45f,ly:-.2f,lz:-.25f,lw:.5f),
            K(4,.3f,bx:.04f,by:-.05f,bz:.02f,py:.16f,sy:.2f,cp:.08f,cy:.6f,cr:-.08f,hy:-.32f,rx:.55f,ry:-.15f,rz:.3f,rw:.8f,lx:.42f,ly:-.2f,lz:-.2f,lw:.4f),
            K(2,-.35f,py:.03f,sy:.04f,cy:.1f,hy:-.05f,rx:.2f,ry:-.3f,rz:.2f,rw:.2f),
            K(2,0)};
        /// <summary>The breach's blows (s from the knock's start): a right and a left into the wall on NativeBreach.Blows' first two,
        /// the shoulder driven through it on the break, then the lowered-shoulder carry through the hole (gone by <see cref="KnockEnd"/>).</summary>
        public const float KnockEnd=3.4f;
        private static readonly float[][] Knock={
            K(0,0),
            K(0,.3f,by:-.03f,bz:-.05f,py:-.1f,sy:-.12f,cp:.06f,cy:-.3f,hy:.25f,rx:-.36f,ry:.05f,rz:-.2f,rw:1,lx:.28f,ly:.05f,lz:.3f,lw:.6f),
            K(0,.43f,by:-.04f,bz:-.06f,py:-.12f,sy:-.14f,cp:.07f,cy:-.34f,hy:.28f,rx:-.37f,ry:.06f,rz:-.22f,rw:1,lx:.28f,ly:.05f,lz:.3f,lw:.6f),
            K(0,.55f,In,by:-.04f,bz:.1f,py:.12f,sy:.1f,cp:.12f,cy:.28f,hy:-.2f,rx:-.1f,ry:.18f,rz:.8f,rw:1,lx:.3f,ly:-.05f,lz:.05f,lw:.6f),
            K(0,.66f,Out,by:-.04f,bz:.06f,py:.08f,sy:.08f,cp:.1f,cy:.2f,hy:-.15f,rx:-.15f,ry:.12f,rz:.62f,rw:1,lx:.3f,ly:-.05f,lz:.1f,lw:.6f),
            K(0,.95f,by:-.03f,bz:-.05f,py:.1f,sy:.12f,cp:.06f,cy:.3f,hy:-.25f,rx:-.28f,ry:.05f,rz:.3f,rw:.6f,lx:.36f,ly:.05f,lz:-.2f,lw:1),
            K(0,1.08f,by:-.04f,bz:-.06f,py:.12f,sy:.14f,cp:.07f,cy:.34f,hy:-.28f,rx:-.28f,ry:.05f,rz:.3f,rw:.6f,lx:.37f,ly:.06f,lz:-.22f,lw:1),
            K(0,1.2f,In,by:-.04f,bz:.1f,py:-.12f,sy:-.1f,cp:.12f,cy:-.28f,hy:.2f,rx:-.3f,ry:-.05f,rz:.05f,rw:.6f,lx:.1f,ly:.18f,lz:.8f,lw:1),
            K(0,1.32f,Out,by:-.04f,bz:.06f,py:-.08f,sy:-.08f,cp:.1f,cy:-.2f,hy:.15f,rx:-.3f,ry:-.05f,rz:.1f,rw:.6f,lx:.15f,ly:.12f,lz:.62f,lw:1),
            K(0,1.65f,by:-.06f,bz:-.14f,pp:.1f,py:.2f,sp:.2f,sy:.2f,cp:.25f,cy:.45f,hp:-.25f,hy:-.35f,rx:-.05f,ry:-.05f,rz:.35f,rw:1,lx:.42f,ly:-.2f,lz:-.2f,lw:.8f),
            K(0,1.95f,by:-.08f,bz:-.16f,pp:.12f,py:.22f,sp:.22f,sy:.22f,cp:.28f,cy:.5f,hp:-.27f,hy:-.38f,rx:-.05f,ry:-.05f,rz:.36f,rw:1,lx:.43f,ly:-.22f,lz:-.22f,lw:.8f),
            K(0,2.1f,In,by:-.04f,bz:.2f,pp:.15f,py:.2f,sp:.3f,sy:.2f,cp:.35f,cy:.55f,hp:-.2f,hy:-.4f,rx:-.05f,ry:-.02f,rz:.45f,rw:1,lx:.4f,ly:-.2f,lz:-.1f,lw:.8f),
            K(0,2.3f,Out,by:-.08f,bz:.24f,pp:.16f,py:.18f,sp:.3f,sy:.18f,cp:.45f,cy:.5f,hp:-.22f,hy:-.38f,rx:-.05f,rz:.42f,rw:1,lx:.4f,ly:-.2f,lz:-.12f,lw:.6f),
            K(0,2.9f,by:-.06f,bz:.08f,pp:.1f,py:.12f,sp:.2f,sy:.12f,cp:.3f,cy:.35f,hp:-.15f,hy:-.25f,rx:-.05f,rz:.38f,rw:.8f),
            K(0,KnockEnd)};
        // ---- the proposed charge: its wind-up (rearing, the right foot scraped back, the shoulder lowered and coiled, the lunge
        // forward on its last beat), the run's lowered-shoulder pose over the heavy gait, and its ends ----
        private static float[] RunPose(int a,float o,int e=0)=>K(a,o,e,by:-.06f,bz:.04f,pp:.06f,py:.12f,sp:.1f,sy:.15f,cp:.12f,cy:.38f,cr:.04f,hp:-.25f,hy:-.34f,rx:-.06f,ry:-.05f,rz:.38f,rw:1);
        private static readonly float[][] ChargeWind={
            K(0,0),
            K(0,.2f,by:.03f,cp:-.12f,hp:-.1f,rx:-.42f,rz:.1f,rw:.5f,lx:.42f,lz:.1f,lw:.5f),
            K(0,.45f,by:-.06f,bz:-.04f,pp:.06f,py:.12f,sp:.12f,sy:.15f,cp:.16f,cy:.3f,hp:-.18f,hy:-.25f,rx:-.12f,ry:-.1f,rz:.32f,rw:.9f,lx:.45f,ly:-.15f,lz:-.2f,lw:.8f),
            K(0,.75f,by:-.1f,bz:-.06f,pp:.1f,py:.15f,sp:.16f,sy:.18f,cp:.2f,cy:.42f,cr:.05f,hp:-.28f,hy:-.38f,rx:-.06f,ry:-.08f,rz:.36f,rw:1,lx:.46f,ly:-.22f,lz:-.28f,lw:.9f),
            RunPose(0,NativeHulkMoves.ChargeWind,In)};
        private static readonly float[][] ChargeRun={RunPose(0,0)};
        private static readonly float[][] ChargeHit={
            RunPose(0,0),
            K(0,.07f,In,by:-.08f,bz:.14f,pp:.08f,py:.14f,sp:.12f,sy:.16f,cp:.18f,cy:.5f,cr:.04f,hp:-.28f,hy:-.36f,rx:-.06f,ry:-.05f,rz:.42f,rw:1),
            K(0,.25f,Out,by:-.04f,bz:-.06f,pp:.04f,py:.06f,sp:.08f,sy:.08f,cp:-.05f,cy:.2f,hp:-.45f,hy:-.15f,rx:-.2f,ry:-.1f,rz:.3f,rw:.6f),
            K(0,.55f,bz:-.02f,sp:.05f,cp:.15f,cy:.1f,hp:-.1f,rx:-.3f,ry:-.2f,rz:.2f,rw:.3f),
            K(0,NativeHulkMoves.Stop(NativeHulkMoves.ChargeEnd.Pilot))};
        private static readonly float[][] ChargeSolid={
            RunPose(0,0),
            K(0,.06f,In,by:-.12f,bz:.1f,pp:.08f,py:.12f,sp:.14f,sy:.15f,cp:.2f,cy:.38f,hp:-.26f,hy:-.34f,rx:-.06f,ry:-.05f,rz:.4f,rw:1),
            K(0,.22f,Out,by:-.02f,bz:-.2f,pp:-.1f,sp:-.2f,cp:-.35f,cy:.1f,hp:-.6f,rx:-.55f,ry:-.1f,rz:.15f,rw:.8f,lx:.55f,ly:-.1f,lz:.15f,lw:.8f),
            K(0,.5f,by:-.08f,bz:-.12f,sp:.05f,cp:.1f,cr:.12f,hp:.2f,hr:.15f,rx:-.5f,ry:-.3f,rz:.1f,rw:.3f,lx:.5f,ly:-.3f,lz:.1f,lw:.3f),
            K(0,.9f,by:-.1f,sp:.15f,cp:.3f,cr:-.08f,hp:.35f,hr:-.2f),
            K(0,1.3f,by:-.04f,sp:.06f,cp:.12f,hp:.05f),
            K(0,NativeHulkMoves.Stop(NativeHulkMoves.ChargeEnd.Solid))};
        private static readonly float[][] ChargeSpent={
            RunPose(0,0),
            K(0,.25f,Out,bz:-.06f,pp:-.05f,sp:.05f,cp:.1f,cy:.2f,hp:-.1f,rx:-.2f,rz:.3f,rw:.4f),
            K(0,NativeHulkMoves.Stop(NativeHulkMoves.ChargeEnd.Spent))};
        // ---- the landings: the Hulk's three-point (a fist on the floor, the other arm thrown back), the brute's on both fists;
        // their key times are the profile's recovery from the touchdown (LandAt) ----
        private static readonly float[][] HulkLand={
            K(0,0,by:-.26f,pp:.02f,sp:.02f,cp:0,hp:-.1f,rx:-.24f,ry:-.56f,rz:.4f,rw:1,lx:.5f,ly:-.3f,lz:-.22f,lw:.8f),
            K(0,.1f,Out,by:-.3f,pp:.03f,sp:.02f,cp:-.02f,hp:-.2f,rx:-.24f,ry:-.56f,rz:.4f,rw:1,lx:.55f,ly:-.33f,lz:-.26f,lw:.8f),
            K(0,.5f,by:-.29f,pp:.02f,sp:.02f,cp:-.02f,hp:-.2f,rx:-.25f,ry:-.56f,rz:.4f,rw:1,lx:.52f,ly:-.32f,lz:-.24f,lw:.7f),
            K(0,.95f,by:-.13f,cp:-.04f,hp:-.12f,rx:-.3f,ry:-.4f,rz:.32f,rw:.4f,lx:.45f,ly:-.25f,lz:-.1f,lw:.3f),
            K(0,1.25f,by:-.03f,sp:-.04f,cp:-.1f,hp:-.1f,rx:-.48f,ry:-.15f,rz:.12f,rw:.4f,lx:.48f,ly:-.15f,lz:.12f,lw:.4f),
            K(0,1.5f)};
        private static readonly float[][] BruteLand={
            K(0,0,by:-.24f,pp:.02f,sp:.02f,cp:0,hp:-.12f,rx:-.17f,ry:-.58f,rz:.36f,rw:1,lx:.17f,ly:-.58f,lz:.36f,lw:1),
            K(0,.07f,Out,by:-.29f,pp:.03f,sp:.02f,cp:0,hp:-.16f,rx:-.17f,ry:-.58f,rz:.36f,rw:1,lx:.17f,ly:-.58f,lz:.36f,lw:1),
            K(0,.3f,by:-.28f,pp:.02f,sp:.02f,cp:0,hp:-.16f,rx:-.18f,ry:-.58f,rz:.35f,rw:1,lx:.18f,ly:-.58f,lz:.35f,lw:1),
            K(0,.6f,by:-.1f,cp:-.04f,hp:-.08f,rx:-.25f,ry:-.4f,rz:.3f,rw:.3f,lx:.25f,ly:-.4f,lz:.3f,lw:.3f),
            K(0,.9f)};

        /// <summary>The move a frame plays (none: the gait, the take and the procedural attacks).</summary>
        public enum Keyed{None,Smash,Punch,Swipe,Knock,ChargeWind,ChargeRun,ChargeStop}
        /// <summary>This frame's keyed move, its age, and whether it replaces the attack's take (punch, swipe, knock, charge).</summary>
        public Keyed Move{get;private set;}
        public float MoveAge{get;private set;}
        private float[][] moveTable;private float moveH,moveR,moveW;private int moveSide;
        private readonly float[] keyNow=new float[KN],keyLag=new float[KN],keyLag2=new float[KN],keyTimes=new float[16];
        private bool Replaces=>Move==Keyed.Punch||Move==Keyed.Swipe||Move==Keyed.Knock||Move==Keyed.ChargeWind||Move==Keyed.ChargeRun||Move==Keyed.ChargeStop;
        /// <summary>Which keyed move the input asks for: a charge, a knock, the proposed punch or swipe, or the smash's overlay.</summary>
        private void ResolveMove(in HordeMotionInput f,bool attacking)
        {
            Move=Keyed.None;moveTable=null;moveW=0;moveSide=1;moveH=f.Strike;moveR=f.Recover;
            if(!NativeHulkMoves.Heavy(kind)||quad)return;
            if(f.Charging)
            {
                var ph=NativeHulkMoves.PhaseOf(f.ChargeAge,f.ChargeEnded?f.ChargeEndAge:float.NaN,f.ChargeHow);
                if(ph==NativeHulkMoves.ChargePhase.Wind){Move=Keyed.ChargeWind;moveTable=ChargeWind;MoveAge=f.ChargeAge;moveW=Sm(f.ChargeAge/.15f);}
                else if(ph==NativeHulkMoves.ChargePhase.Run){Move=Keyed.ChargeRun;moveTable=ChargeRun;MoveAge=f.ChargeAge;moveW=1;}
                else if(ph==NativeHulkMoves.ChargePhase.Stop)
                {
                    Move=Keyed.ChargeStop;MoveAge=f.ChargeEndAge;moveTable=f.ChargeHow==NativeHulkMoves.ChargeEnd.Pilot?ChargeHit:f.ChargeHow==NativeHulkMoves.ChargeEnd.Solid?ChargeSolid:ChargeSpent;
                    float stop=NativeHulkMoves.Stop(f.ChargeHow);moveW=1-Sm((f.ChargeEndAge-stop)/NativeHulkMoves.BlendOut);
                }
                return;
            }
            if(f.Knocking&&f.KnockAge>=0&&f.KnockAge<KnockEnd+NativeHulkMoves.BlendOut)
            {Move=Keyed.Knock;moveTable=Knock;MoveAge=f.KnockAge;moveW=Sm(f.KnockAge/.12f)*(1-Sm((f.KnockAge-KnockEnd)/NativeHulkMoves.BlendOut));return;}
            if(!attacking)return;float age=f.AttackAge;MoveAge=age;moveSide=f.Side<0?-1:1;
            float fade=Sm(age/.1f)*(1-Sm((age-f.Recover)/.25f));
            if(f.Move==NativeHulkMoves.Move.Punch&&!f.Crawl){Move=Keyed.Punch;moveTable=Punch;moveW=fade;}
            else if(f.Move==NativeHulkMoves.Move.Swipe&&!f.Crawl){Move=Keyed.Swipe;moveTable=Swipe;moveW=fade;}
            else if(f.Attack==NativeHordeKinds.Attack.Smash&&!f.Crawl&&!NativeHordeKinds.Armless(f.Lost)){Move=Keyed.Smash;moveTable=kind==NativeHordeKinds.Hulk?HulkSmash:BruteSmash;moveW=fade;moveSide=1;}
        }
        /// <summary>The keyed move's body and trunk (after the capture and the gait's trunk, before the head looks and the hips balance).</summary>
        private void MoveTrunk(CapturedRig.Pose p,in HordeMotionInput f)
        {
            if(moveTable==null||moveW<=0)return;
            // (The trunk reaches each key with the hips, so a fist is at full reach on the contact; the head follows a beat late.)
            Keys(moveTable,moveH,moveR,MoveAge,moveSide,keyNow,keyTimes);Keys(moveTable,moveH,moveR,MoveAge-.06f,moveSide,keyLag2,keyTimes);
            for(int c=0;c<KN;c++)keyLag[c]=c>=KHp&&c<=KHr?keyLag2[c]:keyNow[c];
            // The charge's wind-up trembles as it builds; a solid stop leaves the head reeling; a wall broken on the run jolts it.
            if(Move==Keyed.ChargeWind){float k=Sm(MoveAge/.4f);keyNow[KBx]+=(float)Math.Sin(MoveAge*41)*.006f*k;keyLag[KCr]+=(float)Math.Sin(MoveAge*37+1)*.02f*k;}
            if(Move==Keyed.ChargeStop&&f.ChargeHow==NativeHulkMoves.ChargeEnd.Solid){float k=Sm((MoveAge-.4f)/.2f)*(1-Sm((MoveAge-1.2f)/.4f));keyLag[KHr]+=(float)Math.Sin(MoveAge*7.5f)*.22f*k;keyLag[KHy]+=(float)Math.Sin(MoveAge*5.1f+1)*.15f*k;}
            if(f.Charging&&f.ChargeBroke&&f.ChargeBreakAge>=0&&f.ChargeBreakAge<.4f){float j=Sm(f.ChargeBreakAge/.04f)*(1-Sm((f.ChargeBreakAge-.06f)/.3f));keyNow[KBz]-=.08f*j;keyLag[KCp]+=.14f*j;keyLag[KHp]+=.18f*j;keyLag[KCy]+=.12f*j;}
            KeyTrunk(p,keyNow,keyLag,moveW);
        }
        /// <summary>The keyed move's hands (after the legs, before the reactions' arms and the reach limit).</summary>
        private void MoveHands(CapturedRig.Pose p){if(moveTable!=null&&moveW>0)KeyHands(p,keyNow,moveW);}

        // ================================================================================================================
        // The landings (NativeHordeGait.Drop's touchdown and the fall's last beat)
        // ================================================================================================================
        private static readonly float[] staticKeys=new float[KN],staticKeyLag=new float[KN],staticKeyTimes=new float[16];
        /// <summary>The landing's key pose `into` s after the touchdown on a profile (the Hulk's three-point, the brute's two fists).</summary>
        private static void LandKeys(CapturedRig.Pose p,in NativeHordeDrop.DropProfile profile,uint seed,float into)
        {
            var table=profile.Heavy?HulkLand:BruteLand;int side=NativeHordeKinds.Hash(seed,301)<.5f?1:-1;float scale=profile.Recover/(profile.Heavy?1.5f:.9f);
            Keys(table,0,0,into/Math.Max(.1f,scale),side,staticKeys,staticKeyTimes);Keys(table,0,0,(into-.05f)/Math.Max(.1f,scale),side,staticKeyLag,staticKeyTimes);
            // (The height is the key's own: the take's compression is replaced, so the deepest is just after the touchdown.)
            var b=p.Position[CapturedRig.Body];b.Y=staticKeys[KBy];b.X+=staticKeys[KBx];b.Z+=staticKeys[KBz];p.Position[CapturedRig.Body]=b;
            staticKeys[KBx]=staticKeys[KBy]=staticKeys[KBz]=0;KeyTrunk(p,staticKeys,staticKeyLag,1);
        }
        /// <summary>The fall's last .3 s: both fists raised to slam down with the landing, the chest thrown back.</summary>
        private static float FistsUp(float toLand)=>toLand<0||toLand>.3f?0:Sm(1-toLand/.3f);
    }
}

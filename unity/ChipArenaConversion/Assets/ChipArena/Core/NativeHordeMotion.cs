using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>What one horde add's motion reads this frame: only what host, guests and the killcam all hold (the add's id, kind
    /// and replicated state) and the presentation clock. Unity world axes; Yaw in degrees as the authority writes it.</summary>
    public struct HordeMotionInput
    {
        public int Kind,Look,Id;public uint Seed;public float Time,Yaw,Ground;public Vector3 Root,Velocity;public bool Crawl,Possessed;public int Lost;
        /// <summary>Seconds since the attack started (LastAttack), NaN with no attack showing; its strike and recovery
        /// (NativeHordeKinds.Strike / Recover, the authority's timing) and the arm it swings (1 right, -1 left).</summary>
        public float AttackAge,Strike,Recover;public NativeHordeKinds.Attack Attack;public int Side;
        /// <summary>The pilot it is going for (nearest within reach of its senses), for the alert stance and the head.</summary>
        public bool HasTarget;public Vector3 Target;
        /// <summary>How hurt it is (0 dead .. 1 whole): a hurt add limps and sags.</summary>
        public float Health;
        /// <summary>The DEV preview's gallery drives a treadmill: the feet plant as if the body moved at Velocity.</summary>
        public bool Treadmill;
    }

    /// <summary>
    /// One horde add's animation (presentation only: never read by the authority, no change to hit timing, hitboxes, damage,
    /// cooldowns, movement or the network frame). Procedural, so every kind moves as itself and every add moves on its own:
    /// <list type="bullet">
    /// <item>Feet that plant: each foot stands where it landed in the world until its turn to step, then swings to where the
    /// body will be when it lands, on a cadence that follows the add's real ground speed (stride per kind and speed), so
    /// nothing skates, on turns or under a shove either. Standing still, a foot left behind steps home.</item>
    /// <item>Per kind: the shambler's dragged foot and lurch, the runner's pitched sprint, the brute's wide stomp and rolling
    /// shoulders, the spitter's hunched skitter, the crawler's four-limbed trot (its hands plant like feet), the bloater's
    /// waddle. Arms swing against the feet that are actually forward.</item>
    /// <item>Crowd variety: each add's seed sets its phase, cadence (plus or minus 6 %), hunch, head tilt, limp side, arm hang
    /// and idle rhythm (the Siren's twitches come at hashed times).</item>
    /// <item>Idle, alert (crouched, head locked on its pilot), attack (wind-up into a held telegraph, the strike arriving at
    /// LastAttack + Strike exactly, follow-through and recovery to Recover), hit and stagger (Core/NativeHordeReact layers them),
    /// and a death per kind.</item>
    /// </list>
    /// Pure arithmetic on fixed fields: no allocation per frame.
    /// </summary>
    public sealed class NativeHordeGait
    {
        // ---- per-kind gait (stride per cycle of two steps at walk and run, duty, lift, bob, crouch, lean, width) ----
        private struct Def{public float WalkStride,RunStride,WalkSpeed,RunSpeed,Duty,RunDuty,Lift,RunLift,Bob,RunBob,Crouch,RunCrouch,Hunch,RunLean,Width,ArmSwing,RunArm,Hang,Sway,Roll,Twist;}
        private static readonly Def[] defs={
            // shambler: short shuffling steps, one foot dragged, slumped, a lurch
            new Def{WalkStride=.86f,RunStride=1.42f,WalkSpeed=1.5f,RunSpeed=3.4f,Duty=.64f,RunDuty=.42f,Lift=.05f,RunLift=.11f,Bob=.018f,RunBob=.04f,Crouch=.05f,RunCrouch=.09f,Hunch=.42f,RunLean=.5f,Width=.12f,ArmSwing=.32f,RunArm=.55f,Hang=.3f,Sway=.035f,Roll=.09f,Twist=.14f},
            // runner: long low strides, pitched forward
            new Def{WalkStride=1.0f,RunStride=1.72f,WalkSpeed=2.6f,RunSpeed=4.6f,Duty=.58f,RunDuty=.34f,Lift=.07f,RunLift=.15f,Bob=.02f,RunBob=.05f,Crouch=.08f,RunCrouch=.13f,Hunch=.45f,RunLean=.55f,Width=.1f,ArmSwing=.35f,RunArm=.6f,Hang=.1f,Sway=.02f,Roll=.06f,Twist=.2f},
            // brute: wide heavy stomps
            new Def{WalkStride=.9f,RunStride=1.25f,WalkSpeed=1.3f,RunSpeed=2.4f,Duty=.66f,RunDuty=.48f,Lift=.07f,RunLift=.1f,Bob=.035f,RunBob=.055f,Crouch=.1f,RunCrouch=.13f,Hunch=.62f,RunLean=.72f,Width=.18f,ArmSwing=.26f,RunArm=.42f,Hang=.3f,Sway=.05f,Roll=.13f,Twist=.1f},
            // spitter: hunched skitter
            new Def{WalkStride=.8f,RunStride=1.3f,WalkSpeed=1.6f,RunSpeed=2.9f,Duty=.6f,RunDuty=.42f,Lift=.06f,RunLift=.1f,Bob=.015f,RunBob=.03f,Crouch=.08f,RunCrouch=.11f,Hunch=.62f,RunLean=.72f,Width=.11f,ArmSwing=.18f,RunArm=.3f,Hang=-.2f,Sway=.025f,Roll=.07f,Twist=.12f},
            // crawler: a four-limbed trot
            new Def{WalkStride=.62f,RunStride=1.05f,WalkSpeed=2.2f,RunSpeed=3.9f,Duty=.6f,RunDuty=.44f,Lift=.05f,RunLift=.09f,Bob=.012f,RunBob=.025f,Crouch=0,RunCrouch=0,Hunch=0,RunLean=0,Width=.12f,ArmSwing=0,RunArm=0,Hang=0,Sway=.015f,Roll=.06f,Twist=.1f},
            // bloater: a short waddle
            new Def{WalkStride=.66f,RunStride=.95f,WalkSpeed=1.1f,RunSpeed=1.8f,Duty=.7f,RunDuty=.56f,Lift=.045f,RunLift=.07f,Bob=.03f,RunBob=.045f,Crouch=.13f,RunCrouch=.15f,Hunch=-.08f,RunLean=.05f,Width=.17f,ArmSwing=.2f,RunArm=.3f,Hang=-.3f,Sway=.06f,Roll=.17f,Twist=.06f}};

        public const float AnkleHeight=.07f,MaxDt=.1f,ResetGap=.5f;
        // ---- state ----
        private float time=float.NaN;private uint seed;private int id=int.MinValue;
        /// <summary>The cycle phase (cycles; one cycle is a step of each foot), the smoothed ground speed and the presented yaw (Unity, deg).</summary>
        public float Phase,Speed,Yaw;
        private Vector2 root;private Vector2 velocity;
        private struct Foot{public Vector2 W,From;public bool Swing;public float T0,T1,Lift;public int Cycle;public float Y;}
        private readonly Foot[] feet=new Foot[4];
        /// <summary>The presented yaw's facing and right (Unity world, horizontal).</summary>
        public Vector2 Forward,Right;
        /// <summary>The last frame's foot (and the crawler's hand) targets in Unity world, and whether each was in the air.</summary>
        public readonly Vector3[] FootWorld=new Vector3[4];public readonly bool[] FootAir=new bool[4];
        /// <summary>Footfalls this frame (a foot that planted), for the dust and the heavy kinds' step cue.</summary>
        public int Landed;
        // per-add variety, set from the seed
        private float cadence=1,hunchJitter,tilt,hang,idleRate=1,limpSide=1,drag;private int kind;
        /// <summary>Forgets the add (a new arena, a rewind, a jump): the next frame plants its feet where it stands.</summary>
        public void Reset(){time=float.NaN;}

        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float Frac(float x)=>x-(float)Math.Floor(x);
        private static float Wrap(float deg){deg%=360;if(deg>180)deg-=360;if(deg<-180)deg+=360;return deg;}
        private static Vector2 Dir(float yaw){float r=yaw*(float)Math.PI/180;return new Vector2((float)Math.Sin(r),(float)Math.Cos(r));}

        /// <summary>The walk-run blend at ground speed `v` for a kind (0 walk, 1 run).</summary>
        public static float RunBlend(int kind,float v){var d=defs[Math.Max(0,Math.Min(defs.Length-1,kind))];return Clamp01((v-d.WalkSpeed*1.05f)/Math.Max(.1f,d.RunSpeed*.8f-d.WalkSpeed*1.05f));}
        /// <summary>The stride (m per cycle) at speed `v`: the gait's cadence is v / stride.</summary>
        public static float Stride(int kind,float v){var d=defs[Math.Max(0,Math.Min(defs.Length-1,kind))];float r=RunBlend(kind,v);return Lerp(d.WalkStride,d.RunStride,r)*(.75f+.25f*Clamp01(v/Math.Max(.1f,d.WalkSpeed)));}
        public static float Duty(int kind,float v){var d=defs[Math.Max(0,Math.Min(defs.Length-1,kind))];return Lerp(d.Duty,d.RunDuty,RunBlend(kind,v));}

        /// <summary>
        /// Steps the add to `f.Time` and writes its living pose into `p` (source frame, the rig root at the body's position,
        /// rotated by <see cref="Yaw"/>). `react` adds the felt hits (null: none). The rig root's yaw to present is <see cref="Yaw"/>.
        /// </summary>
        public void Pose(in HordeMotionInput f,CapturedRig.Pose p,NativeHordeReact react=null)
        {
            Step(f,react);p.Reset();kind=f.Kind;var d=defs[kind];
            float v=Speed,run=RunBlend(kind,v),moving=Sm((v-.12f)/.5f),hurt=1-Clamp01(f.Health*1.6f);
            // ---- the attack's channels ----
            float age=f.AttackAge;bool attacking=!float.IsNaN(age)&&age>=0&&age<f.Recover+.25f;
            AttackCurve(f,out float cock,out float swing,out float follow,out float settle,out float attackW);
            // ---- alert ----
            float alert=0,lookYaw=0,lookPitch=0;
            if(f.HasTarget)
            {
                var to=new Vector2(f.Target.X-f.Root.X,f.Target.Z-f.Root.Z);float dist=to.Length();
                if(dist>.05f){to/=dist;float ang=(float)Math.Atan2(Vector2.Dot(to,Right),Vector2.Dot(to,Forward));lookYaw=Math.Max(-1.1f,Math.Min(1.1f,ang));lookPitch=(float)Math.Atan2(f.Target.Y+.5f-(f.Root.Y+.6f),dist);}
                alert=Clamp01((14-dist)/6)*(1-moving*.6f);
            }
            if(kind==NativeHordeKinds.Crawler){CrawlerPose(f,p,d,v,run,moving,alert,lookYaw,cock,swing,follow,settle,attackW,react);return;}
            // ---- hips ----
            float t=f.Time,ph=Phase*(float)Math.PI*2;
            float crouch=Lerp(d.Crouch,d.RunCrouch,run)*moving+alert*.05f+hurt*.04f+cock*CockCrouch(f.Attack)-swing*(f.Attack==NativeHordeKinds.Attack.Smash?-.02f:.0f);
            float bob=Lerp(d.Bob,d.RunBob,run)*moving*(float)Math.Cos(ph*2)+ (1-moving)*.006f*(float)Math.Sin(t*2.2f*idleRate+seed%7);
            float sway=d.Sway*moving*(float)Math.Sin(ph)*(kind==NativeHordeKinds.Shambler?1+.6f*drag:1);
            p.Position[CapturedRig.Body]=new Vector3(sway,-crouch+bob,0);
            float lean=Lerp(d.Hunch,d.RunLean,run*moving)+hunchJitter+alert*.12f+hurt*.12f;
            float pelvisYaw=d.Twist*moving*(float)Math.Sin(ph),pelvisRoll=d.Roll*moving*(float)Math.Cos(ph)*(kind==NativeHordeKinds.Bloater?1.3f:1);
            // ---- the attack's body (overlaid on the gait; the strike lands at Strike) ----
            AttackBody(f,cock,swing,follow,settle,out float aPitch,out float aYaw,out float aRoll,out float aCrouch,out float aHead);
            p.Position[CapturedRig.Body].Y-=aCrouch;
            float spinePitch=lean*.45f+aPitch*.5f,chestPitch=lean*.55f+aPitch*.5f;
            p.Rotation[CapturedRig.Pelvis]=CapturedRig.Euler(lean*.15f,pelvisYaw+aYaw*.3f,pelvisRoll*.5f);
            p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(spinePitch,-pelvisYaw*.5f+aYaw*.35f,-pelvisRoll*.4f+aRoll*.5f);
            p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(chestPitch,-pelvisYaw*.6f+aYaw*.35f,-pelvisRoll*.3f+aRoll*.5f+ (kind==NativeHordeKinds.Brute?d.Roll*moving*.6f*(float)Math.Cos(ph):0));
            // ---- head: counters the hunch to look ahead, tilts (the Siren's crooked head), twitches, tracks its pilot ----
            float twitch=Twitch(t);float headPitch=-(lean*.82f)+lookPitch*.6f*alert+aHead,headYaw=lookYaw*(.55f+.35f*alert)*(1-attackW*.5f)+twitch*.5f,headRoll=tilt+twitch*.8f+(kind==NativeHordeKinds.Shambler?.08f*(float)Math.Sin(ph):0);
            p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(headPitch*.45f,headYaw*.45f,headRoll*.4f);
            p.Rotation[CapturedRig.Head]=CapturedRig.Euler(headPitch*.55f,headYaw*.55f,headRoll*.6f);
            // ---- reactions over the trunk (hips shove, recoil, head snap) ----
            react?.Trunk(p,f.Time,Forward,Right);
            Balance(p,kind);
            // ---- legs: feet to their planted (or swinging) targets ----
            p.World();
            for(int i=0;i<2;i++)
            {
                bool left=i==0;Vector3 w=FootWorld[i];Vector3 local=ToLocal(w,f.Root);local.Y=f.Ground-f.Root.Y+AnkleHeight+feet[i].Y;
                // A foot can only reach so far: past it the hips sink (never the foot through the floor).
                CapturedRig.Leg(p,left,local,FootTurn(i));
                // The dragged foot's toe scrapes (shambler) and the run's toe-off.
                float toe=feet[i].Swing?(-.35f+(kind==NativeHordeKinds.Shambler&&(i==0)==(limpSide<0)?.5f*drag:0)):0;
                p.Rotation[i==0?CapturedRig.LToe:CapturedRig.RToe]=CapturedRig.Euler(-toe*.4f);
            }
            // ---- arms: swing against the feet that are forward, then the attack ----
            p.World();float zl=ToLocal(FootWorld[0],f.Root).Z,zr=ToLocal(FootWorld[1],f.Root).Z,reach=Math.Max(.1f,Stride(kind,Math.Max(v,.5f))*.5f);
            float swingAmp=Lerp(d.ArmSwing,d.RunArm,run)*moving;
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int arm=left?CapturedRig.LArm:CapturedRig.RArm,elbow=arm+1,clav=arm-1,hand=arm+2;float sgn=left?1:-1;
                float fwd=Math.Max(-1.2f,Math.Min(1.2f,(left?zl:zr)/reach));
                float hangX=-(d.Hang+(kind==NativeHordeKinds.Shambler&&(left?limpSide>0:limpSide<0)?.3f+hang*.6f:hang*.3f))*(1-run*.4f);
                float x=hangX+swingAmp*fwd,z=sgn*(.12f+.04f*moving),elbowX=-(.3f+.35f*run+(kind==NativeHordeKinds.Runner?.4f*run:0)+(kind==NativeHordeKinds.Spitter?.9f:0));
                // The runner sweeps its blades back as it sprints; the shambler's reaching arm rises when it runs at you.
                if(kind==NativeHordeKinds.Runner)x+=.7f*run*moving;
                if(kind==NativeHordeKinds.Shambler)x-=.42f*run*moving*(left?.8f:1);
                if(kind==NativeHordeKinds.Brute){z=sgn*.22f;elbowX=-.45f-.2f*run;}
                if(kind==NativeHordeKinds.Bloater){z=sgn*(.2f+.04f*(float)Math.Sin(ph));elbowX=-.75f;}
                // Alert: claws come up ready.
                x-=alert*(.35f+(kind==NativeHordeKinds.Brute?.2f:0));elbowX-=alert*.35f;
                p.Rotation[clav]=CapturedRig.Euler(0,sgn*(kind==NativeHordeKinds.Brute?-.25f:-.05f)-sgn*alert*.05f,sgn*(kind==NativeHordeKinds.Brute?-.08f:0));
                p.Rotation[arm]=CapturedRig.Euler(x,0,z);p.Rotation[elbow]=CapturedRig.Euler(elbowX,0,0);
                p.Rotation[hand]=CapturedRig.Euler(-.2f-alert*.3f,0,sgn*.1f);
            }
            AttackArms(f,p,cock,swing,follow,settle,attackW);
            react?.Arms(p,f.Time);
            p.World();
        }

        /// <summary>The farthest the face and the chest may reach ahead of the root (m): the hit capsule's radius, less a margin.</summary>
        public static float Reach(int kind)=>NativeHordeKinds.Radius[Math.Max(0,Math.Min(NativeHordeKinds.Count-1,kind))]-.06f;
        /// <summary>
        /// Keeps the trunk over the hit volume: a hunched or lunging add sits its hips back (its planted feet stay; the legs angle
        /// under it) until its face and chest are no farther ahead of the root than <see cref="Reach"/>, as a crouched creature
        /// balances, so what the camera shows is where the authority's capsule is (shots at the head land on the head).
        /// </summary>
        private static void Balance(CapturedRig.Pose p,int kind)
        {
            p.World();var head=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(new Vector3(0,.24f,.18f),p.WorldRotation[CapturedRig.Head]);
            var chest=p.WorldPosition[CapturedRig.Chest]+Vector3.Transform(new Vector3(0,.4f,.19f),p.WorldRotation[CapturedRig.Chest]);
            float over=Math.Max(head.Z,chest.Z)-Reach(kind);if(over<=0)return;
            var b=p.Position[CapturedRig.Body];b.Z-=Math.Min(over,kind==NativeHordeKinds.Runner?.26f:.32f);p.Position[CapturedRig.Body]=b;
        }

        // ================================================================================================================
        // Stepping: the clock, the yaw, the speed and the feet
        // ================================================================================================================
        private void Step(in HordeMotionInput f,NativeHordeReact react)
        {
            kind=Math.Max(0,Math.Min(NativeHordeKinds.Count-1,f.Kind));var d=defs[kind];
            float dt=float.IsNaN(time)?0:f.Time-time;bool fresh=float.IsNaN(time)||id!=f.Id||dt<-.001f||dt>ResetGap;
            if(fresh)
            {
                id=f.Id;seed=f.Seed;Yaw=f.Yaw;root=new Vector2(f.Root.X,f.Root.Z);velocity=Vector2.Zero;Speed=0;dt=0;
                float H(int k)=>NativeHordeKinds.Hash(seed,40+k);
                Phase=H(0);cadence=.94f+.12f*H(1);hunchJitter=(H(2)-.5f)*.12f;tilt=(H(3)-.5f)*.36f;hang=H(4)*.3f;idleRate=.8f+.4f*H(5);limpSide=H(6)<.5f?-1:1;drag=kind==NativeHordeKinds.Shambler?.4f+.6f*H(7):0;
                Forward=Dir(Yaw);Right=new Vector2(Forward.Y,-Forward.X);
                for(int i=0;i<4;i++){feet[i]=default;feet[i].W=Home(i,new Vector2(f.Root.X,f.Root.Z),d);FootWorld[i]=new Vector3(feet[i].W.X,f.Ground,feet[i].W.Y);FootAir[i]=false;}
            }
            time=f.Time;dt=Math.Max(0,Math.Min(MaxDt,dt));
            // The facing turns quickly toward the authority's yaw (which snaps), faster while winding up an attack, so the strike faces
            // where the authority aims it.
            bool winding=!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Strike;
            float rate=(winding?16:9)*dt*57.3f*(kind==NativeHordeKinds.Brute?.7f:1),diff=Wrap(f.Yaw-Yaw);Yaw=Wrap(Yaw+Math.Max(-rate,Math.Min(rate,diff)));if(fresh)Yaw=f.Yaw;
            Forward=Dir(Yaw);Right=new Vector2(Forward.Y,-Forward.X);
            // Ground speed from how the body really moved (what the camera sees), or the treadmill's velocity.
            var now=new Vector2(f.Root.X,f.Root.Z);
            Vector2 vel=f.Treadmill?new Vector2(f.Velocity.X,f.Velocity.Z):dt>1e-4f?(now-root)/dt:velocity;
            if(vel.Length()>12)vel=velocity;float k=1-(float)Math.Exp(-dt/.1f);velocity+=(vel-velocity)*k;root=now;
            Speed+=(velocity.Length()-Speed)*k;
            if(react!=null)react.Step(f.Time);
            // The hit-stop holds the gait for a moment on a melee blow (the strike's own clock never waits).
            float held=react!=null?react.Hold(f.Time):0;float gdt=dt*(1-held);
            // ---- the cycle ----
            float stride=Stride(kind,Speed),duty=Duty(kind,Speed);bool moving=Speed>.2f;
            float rateHz=moving?Speed/Math.Max(.2f,stride)*cadence:0;Phase+=rateHz*gdt;
            Landed=0;Vector2 ahead=Forward*(duty*stride*.5f);
            // A shove moves the hips off the feet: standing feet step back under them (the stagger-back steps).
            Vector2 shove=react!=null?react.Shove(f.Time):Vector2.Zero;bool staggering=react!=null&&react.Staggering(f.Time);
            int feetCount=kind==NativeHordeKinds.Crawler?4:2;
            for(int i=0;i<feetCount;i++)
            {
                ref var ft=ref feet[i];
                float off=FootOffset(i);float u=Frac(Phase+off);int cycle=(int)Math.Floor(Phase+off);
                Vector2 home=Home(i,now+shove*(staggering?1.15f:.8f),d);
                if(!ft.Swing)
                {
                    // Moving: lift off once the cycle passes the duty (the foot is behind); standing: step only if left behind.
                    bool lift=false;float swingTime=0;
                    if(moving&&u>=duty&&ft.Cycle!=cycle){lift=true;swingTime=(1-u)/Math.Max(.3f,rateHz)+.02f;}
                    else if(!moving&&Vector2.Distance(ft.W,home)>(staggering?.05f:.11f)&&!OtherSwinging(i,feetCount)){lift=true;swingTime=.2f+.06f*NativeHordeKinds.Hash(seed,60+i);}
                    // A planted foot the hips (or a shove) have left out of reach steps at once.
                    else if(Vector2.Distance(ft.W,home)>.45f&&!OtherSwinging(i,feetCount)){lift=true;swingTime=.16f;}
                    if(lift){ft.Swing=true;ft.From=ft.W;ft.T0=f.Time;ft.T1=f.Time+Math.Max(.08f,Math.Min(.5f,swingTime));ft.Cycle=cycle;ft.Lift=(moving?Lerp(d.Lift,d.RunLift,RunBlend(kind,Speed)):d.Lift*.7f)*(kind==NativeHordeKinds.Shambler&&(i==0)==(limpSide<0)?1-.6f*drag:1);}
                }
                if(ft.Swing)
                {
                    float s=Clamp01((f.Time-ft.T0)/Math.Max(1e-3f,ft.T1-ft.T0));
                    // Where the body will be when the foot lands, half a stance ahead of the hip.
                    Vector2 land=home+velocity*Math.Max(0,ft.T1-f.Time)+(moving?ahead:Vector2.Zero);
                    ft.W=Vector2.Lerp(ft.From,land,Sm(s));ft.Y=ft.Lift*(float)Math.Sin(Math.PI*s);
                    if(s>=1){ft.Swing=false;ft.W=land;ft.Y=0;Landed|=1<<i;}
                }
                else{ft.Y=0;if(!moving)ft.Cycle=cycle;}
                FootWorld[i]=new Vector3(ft.W.X,f.Ground,ft.W.Y);FootAir[i]=ft.Swing;
            }
        }
        // Left 0, right .5; the crawler's hands (2, 3) trot with the opposite feet.
        private static float FootOffset(int i)=>i==0?0:i==1?.5f:i==2?.5f:0;
        private static float SideOf(int i)=>i==0||i==2?-1:1;
        private bool OtherSwinging(int i,int count){for(int j=0;j<count;j++)if(j!=i&&feet[j].Swing&&((j&1)!=(i&1)||count==2))return true;return false;}
        private Vector2 Home(int i,Vector2 at,in Def d)
        {
            float side=SideOf(i);var right=Right.LengthSquared()<.5f?new Vector2(1,0):Right;var fwd=Forward.LengthSquared()<.5f?new Vector2(0,1):Forward;
            // The crawler's hands stand ahead of its shoulders; a seed staggers the idle stance a little.
            float ahead=i>=2?.62f:i==0?.03f*(NativeHordeKinds.Hash(seed,70)-.5f):-.03f*(NativeHordeKinds.Hash(seed,70)-.5f);
            float width=i>=2?d.Width*1.3f:d.Width;return at+right*(side*width)+fwd*(ahead-(i<2&&kind==NativeHordeKinds.Crawler?.1f:0));
        }
        private float FootTurn(int i)=>0;
        /// <summary>A Unity world point in the rig's source frame (root at the body, +x the add's left, +z forward).</summary>
        public Vector3 ToLocal(Vector3 w,Vector3 at){float dx=w.X-at.X,dz=w.Z-at.Z;return new Vector3(-(dx*Right.X+dz*Right.Y),w.Y-at.Y,dx*Forward.X+dz*Forward.Y);}
        /// <summary>A point in the rig's source frame back in the Unity world.</summary>
        public Vector3 ToWorld(Vector3 l,Vector3 at)=>new Vector3(at.X-l.X*Right.X+l.Z*Forward.X,at.Y+l.Y,at.Z-l.X*Right.Y+l.Z*Forward.Y);
        // The Siren's twitches: a sharp head jerk at hashed times about every 2-4 s, settling back.
        private float Twitch(float t)
        {
            float period=2.2f+1.8f*NativeHordeKinds.Hash(seed,80),k=(t+period*NativeHordeKinds.Hash(seed,81))/period;int n=(int)Math.Floor(k);float u=(k-n)*period;
            float amp=(float)(NativeGoreRules.Hash01(seed,n,82)-.5)*.9f;return u<.06f?amp*u/.06f:u<.5f?amp*(1-Sm((u-.06f)/.44f)):0;
        }

        // ================================================================================================================
        // Attacks: the wind-up into a held telegraph, the strike arriving at Strike exactly, follow-through and recovery
        // ================================================================================================================
        /// <summary>How long before the strike the blow starts to travel (the swing), per attack: the telegraph holds until then.</summary>
        public static float SwingLead(NativeHordeKinds.Attack a)=>a==NativeHordeKinds.Attack.Smash?.12f:a==NativeHordeKinds.Attack.Spit?.1f:a==NativeHordeKinds.Attack.Belly?.14f:.08f;
        /// <summary>
        /// The attack's channels at age a: cock (0..1 into the wind-up's telegraph pose, held), swing (0..1 across the strike,
        /// exactly 1 at Strike), follow (the follow-through after it), settle (back to the gait by Recover) and the overall weight.
        /// </summary>
        public static void Channels(float a,float strike,float recover,NativeHordeKinds.Attack kind,out float cock,out float swing,out float follow,out float settle,out float weight)
        {
            cock=swing=follow=settle=weight=0;if(float.IsNaN(a)||a<0||a>=recover+.25f)return;
            float lead=Math.Min(SwingLead(kind),strike*.45f),windEnd=strike-lead;
            cock=Sm(a/Math.Max(.05f,windEnd*.75f));
            swing=a<windEnd?0:Clamp01((a-windEnd)/lead);swing=swing*swing*(3-2*swing);
            float through=kind==NativeHordeKinds.Attack.Smash?.28f:.16f;
            follow=a<strike?0:1-Sm((a-strike-through)/Math.Max(.05f,(recover-strike-through)*.6f));
            settle=a<strike?0:Sm((a-strike)/Math.Max(.05f,recover-strike));
            weight=a<recover?1:1-Sm((a-recover)/.25f);
            if(a>=strike)cock=1-Sm((a-strike)/through);
        }
        private static void AttackCurve(in HordeMotionInput f,out float cock,out float swing,out float follow,out float settle,out float w)
            =>Channels(f.AttackAge,f.Strike,f.Recover,f.Attack,out cock,out swing,out follow,out settle,out w);
        private static float CockCrouch(NativeHordeKinds.Attack a)=>a==NativeHordeKinds.Attack.Lunge?.12f:a==NativeHordeKinds.Attack.Pounce?.14f:a==NativeHordeKinds.Attack.Smash?.02f:.04f;
        // The trunk's part of each attack (pitch + forward, yaw toward the striking side, roll), the crouch and the head.
        private static void AttackBody(in HordeMotionInput f,float cock,float swing,float follow,float settle,out float pitch,out float yaw,out float roll,out float crouch,out float head)
        {
            pitch=yaw=roll=crouch=head=0;if(cock+swing+follow<=0)return;float s=f.Side,hold=swing>0?1-swing:cock,hit=Math.Max(swing,follow*(1-settle));
            switch(f.Attack)
            {
                case NativeHordeKinds.Attack.Swipe:pitch=-.18f*hold+.38f*hit;yaw=s*(.45f*hold-.55f*hit);roll=-s*.12f*hold;head=.1f*hold-.1f*hit;break;
                case NativeHordeKinds.Attack.Lunge:pitch=.25f*hold+.4f*hit;crouch=.05f*hold-.03f*hit;head=-.3f*hold-.2f*hit;break;
                case NativeHordeKinds.Attack.Smash:pitch=-.42f*hold+.95f*hit;crouch=-.03f*hold+.13f*hit;head=.25f*hold-.45f*hit;break;
                case NativeHordeKinds.Attack.Spit:pitch=-.45f*hold+.5f*hit;head=.35f*hold-.55f*hit;crouch=.03f*hold;break;
                case NativeHordeKinds.Attack.Belly:pitch=-.4f*hold+.3f*hit;crouch=.06f*hold-.04f*hit;head=.2f*hold;break;
                case NativeHordeKinds.Attack.Pounce:pitch=.2f*hold-.1f*hit;crouch=.08f*hold-.12f*hit;break;
            }
            float w=1-settle;pitch*=w;yaw*=w;roll*=w;crouch*=w;head*=w;
        }
        // The arms' part: FK targets layered over the gait's arms, weighted by the attack.
        private static void AttackArms(in HordeMotionInput f,CapturedRig.Pose p,float cock,float swing,float follow,float settle,float w)
        {
            if(w<=0)return;float hold=swing>0?1-swing:cock,hit=Math.Max(swing,follow*(1-settle)),k=w*(1-settle*.85f);
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int arm=left?CapturedRig.LArm:CapturedRig.RArm,elbow=arm+1,hand=arm+2;float sgn=left?1:-1;bool striker=(f.Side>0)!=left;
                Vector3 e=Vector3.Zero,el=Vector3.Zero;
                switch(f.Attack)
                {
                    case NativeHordeKinds.Attack.Swipe:
                        // The striking claw reared back and high, then raked across and down; the other arm braces forward.
                        if(striker){e=new Vector3(-2.5f*hold-1.15f*hit,-sgn*.3f*hold,sgn*(.9f*hold-.15f*hit));el=new Vector3(-1.5f*hold-.25f*hit,0,0);}
                        else{e=new Vector3(-.7f*hold+.3f*hit,0,sgn*.2f);el=new Vector3(-.8f*hold,0,0);}break;
                    case NativeHordeKinds.Attack.Lunge:
                        // Both blades drawn back low, then thrust forward together.
                        e=new Vector3(.9f*hold-1.65f*hit,0,sgn*(.35f*hold+.05f*hit));el=new Vector3(-1.2f*hold-.15f*hit,0,0);break;
                    case NativeHordeKinds.Attack.Smash:
                        // Both fists raised high over the head, then hammered down to the floor ahead.
                        e=new Vector3(-3.0f*hold-1.0f*hit,0,sgn*(.25f*hold+.05f*hit));el=new Vector3(-1.0f*hold-.15f*hit,0,0);break;
                    case NativeHordeKinds.Attack.Spit:
                        e=new Vector3(.5f*hold-.2f*hit,0,sgn*(.55f*hold+.2f*hit));el=new Vector3(-1.4f*hold,0,0);break;
                    case NativeHordeKinds.Attack.Belly:
                        e=new Vector3(.4f*hold-.6f*hit,0,sgn*(.8f*hold+.3f*hit));el=new Vector3(-.6f*hold,0,0);break;
                    case NativeHordeKinds.Attack.Pounce:
                        e=new Vector3(.4f*hold-1.6f*hit,0,sgn*.3f);el=new Vector3(-1.1f*hold,0,0);break;
                }
                if(f.Attack!=NativeHordeKinds.Attack.Swipe&&f.Attack!=NativeHordeKinds.Attack.Lunge&&f.Attack!=NativeHordeKinds.Attack.Smash&&!striker)e*=1;
                var target=CapturedRig.Euler(e.X,e.Y,e.Z);p.Rotation[arm]=Quaternion.Slerp(p.Rotation[arm],target,k);
                p.Rotation[elbow]=Quaternion.Slerp(p.Rotation[elbow],CapturedRig.Euler(el.X,el.Y,el.Z),k);
                // Claws spread in the telegraph and close through the blow.
                p.Rotation[hand]=Quaternion.Slerp(p.Rotation[hand],CapturedRig.Euler(-.5f*hold+.35f*hit,0,sgn*.25f*hold),k);
            }
        }

        // ================================================================================================================
        // The crawler: trunk pitched level, hands planted as forefeet (the same planting as the feet), head up looking ahead
        // ================================================================================================================
        /// <summary>How high the crawler carries its rig root above the floor (its pelvis sits .24 below it).</summary>
        public const float CrawlTrunk=.46f;
        private void CrawlerPose(in HordeMotionInput f,CapturedRig.Pose p,in Def d,float v,float run,float moving,float alert,float lookYaw,float cock,float swing,float follow,float settle,float attackW,NativeHordeReact react)
        {
            float ph=Phase*(float)Math.PI*2,t=f.Time;float hold=swing>0?1-swing:cock,hit=Math.Max(swing,follow*(1-settle));
            float bob=d.Bob*moving*(float)Math.Cos(ph*2)+.006f*(1-moving)*(float)Math.Sin(t*2.6f*idleRate);
            // The body: its root sits at the crawl capsule's middle; the trunk is pitched near level, the pounce coils it back and launches.
            float pitch=1.28f+.12f*hold-.25f*hit+alert*.05f;
            p.Position[CapturedRig.Body]=new Vector3(d.Sway*moving*(float)Math.Sin(ph),f.Ground-f.Root.Y+CrawlTrunk+bob-.06f*hold+.1f*hit,-.12f-.12f*hold+.25f*hit);
            p.Rotation[CapturedRig.Pelvis]=CapturedRig.Euler(pitch,d.Twist*moving*(float)Math.Sin(ph),d.Roll*moving*(float)Math.Cos(ph));
            p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(.05f-.1f*hold,-d.Twist*.5f*moving*(float)Math.Sin(ph),0);
            p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(.02f-.1f*hold+.1f*hit,-d.Twist*.5f*moving*(float)Math.Sin(ph),0);
            float twitch=Twitch(t);
            p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-.42f-.12f*alert+.2f*hold,lookYaw*.4f+twitch*.3f,tilt*.4f);
            p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.62f-.1f*alert+.25f*hold-.3f*hit,lookYaw*.5f+twitch*.5f,tilt*.6f+twitch*.6f);
            react?.Trunk(p,f.Time,Forward,Right);
            // Its shoulders sit narrower than a biped's (inside the crawl capsule), the forelegs under the chest.
            p.Position[CapturedRig.LClav]=new Vector3(.2f,.18f,.02f);p.Position[CapturedRig.RClav]=new Vector3(-.2f,.18f,.02f);
            p.World();
            for(int i=0;i<2;i++){Vector3 local=ToLocal(FootWorld[i],f.Root);local.Y=f.Ground-f.Root.Y+AnkleHeight+.04f+feet[i].Y;Leg(p,i==0,local,HindPole);}
            // The forefeet: hands to their planted targets (chest frame), the pounce reaching them forward.
            p.World();var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int h=2+i;Vector3 local=ToLocal(FootWorld[h],f.Root);local.Y=f.Ground-f.Root.Y+.09f+feet[h].Y;
                local+=new Vector3(0,.25f*hit*(1-swing*.5f),.35f*hit);
                Vector3 inChest=Vector3.Transform(local-cpos,inv);Vector3 pole=Vector3.Transform(new Vector3(left?.15f:-.15f,.8f,-.55f),inv);
                var wrist=Quaternion.Normalize(CapturedRig.Euler(-1.5f+.5f*hold,0,0));
                CapturedRig.Arm(p,left,inChest,inv*wrist,pole);
            }
            react?.Arms(p,f.Time);
            p.World();
        }

        /// <summary>The crawler's hind knees point up and forward (a folded haunch), not straight ahead as a biped's.</summary>
        private static readonly Vector3 HindPole=Vector3.Normalize(new Vector3(0,.75f,.66f));
        /// <summary>CapturedRig.Leg with a chosen knee direction (rig frame): the hip and knee turned so the ankle meets `target`.</summary>
        private static void Leg(CapturedRig.Pose p,bool left,Vector3 target,Vector3 pole)
        {
            p.World();int hip=left?CapturedRig.LHip:CapturedRig.RHip,knee=hip+1,foot=hip+2;var parent=p.WorldRotation[CapturedRig.Pelvis];var inv=Quaternion.Inverse(parent);
            Vector3 d=Vector3.Transform(target-p.WorldPosition[hip],inv);float reach=Math.Max(.1f,Math.Min(.497f,d.Length()));d=d.LengthSquared()<1e-10f?-Vector3.UnitY:Vector3.Normalize(d);
            Vector3 pl=Vector3.Transform(pole,inv);Vector3 bend=pl-d*Vector3.Dot(d,pl);bend=bend.LengthSquared()<1e-8f?Vector3.UnitZ:Vector3.Normalize(bend);
            Vector3 upper=Vector3.Normalize(d*(reach*.5f)+bend*(float)Math.Sqrt(Math.Max(0,.0625f-reach*reach*.25f)));p.Rotation[hip]=CapturedRig.FromTo(-Vector3.UnitY,upper);
            Vector3 lower=Vector3.Transform(d*reach-upper*.25f,Quaternion.Inverse(p.Rotation[hip]));p.Rotation[knee]=CapturedRig.FromTo(-Vector3.UnitY,lower.LengthSquared()<1e-10f?-Vector3.UnitY:Vector3.Normalize(lower));
            p.Rotation[foot]=Quaternion.Inverse(parent*p.Rotation[hip]*p.Rotation[knee]);
        }

        // ================================================================================================================
        // Deaths (the body chars and burns away by height on NativeMinionFx's clock; these are the bodies' falls)
        // ================================================================================================================
        /// <summary>
        /// The kind's death at `age` seconds (0..NativeMinionFx.DeathSeconds), from where it stood: the shambler buckles to its
        /// knees and topples forward, the runner tumbles on with its momentum, the brute reels back to a knee then crashes
        /// backward, the spitter chokes and crumples sideways, the crawler flips onto its back, the bloater swells and bursts
        /// (its body hides at <see cref="BurstAt"/>; Core/NativeHordeFx throws the burst). `dir` (source frame, horizontal)
        /// is the killing blow's push; a sliced death falls away from it.
        /// </summary>
        public static void Death(CapturedRig.Pose p,int kind,uint seed,float age,Vector2 dir)
        {
            p.Reset();float a=Math.Max(0,age);float R(int k)=>NativeHordeKinds.Hash(seed,90+k);float side=R(0)<.5f?-1:1;
            float push=dir.LengthSquared()>1e-4f?(float)Math.Atan2(dir.X,dir.Y):0;
            float recoil=Sm(a/.12f)*(1-Sm((a-.18f)/.2f));
            switch(kind)
            {
                case NativeHordeKinds.Runner:
                {
                    float fall=Sm((a-.05f)/.45f),slide=Sm((a-.1f)/.6f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.35f*fall-.33f*Sm((a-.35f)/.3f),.55f*slide);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(1.45f*fall,push*.2f,side*.2f*fall);
                    Limp(p,fall,1.2f,side);break;
                }
                case NativeHordeKinds.Brute:
                {
                    float reel=Sm(a/.3f),kneel=Sm((a-.25f)/.35f),crash=Sm((a-.75f)/.45f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.25f*kneel-.4f*crash,-.12f*reel-.35f*crash);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(-.25f*reel+.15f*kneel-1.25f*crash,push*.15f,side*.1f*crash);
                    p.Rotation[side>0?CapturedRig.LHip:CapturedRig.RHip]=CapturedRig.Euler(-1.3f*kneel*(1-crash*.6f));p.Rotation[side>0?CapturedRig.LKnee:CapturedRig.RKnee]=CapturedRig.Euler(1.8f*kneel*(1-crash*.4f));
                    p.Rotation[side>0?CapturedRig.RKnee:CapturedRig.LKnee]=CapturedRig.Euler(.9f*kneel);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-.4f*reel-1.4f*crash,0,.6f*crash);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.4f*reel-1.4f*crash,0,-.6f*crash);
                    p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.5f*reel+.6f*crash);break;
                }
                case NativeHordeKinds.Spitter:
                {
                    float choke=Sm(a/.35f)*(1-Sm((a-.5f)/.3f)),fall=Sm((a-.35f)/.55f);
                    p.Position[CapturedRig.Body]=new Vector3(side*.15f*fall,-.62f*fall,0);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(.4f*choke+.3f*fall,0,side*1.35f*fall);
                    p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-.6f*choke+.4f*fall);p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.5f*choke+(float)Math.Sin(a*40)*.06f*choke);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-1.5f*choke,0,.3f);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-1.5f*choke,0,-.3f);p.Rotation[CapturedRig.LElbow]=p.Rotation[CapturedRig.RElbow]=CapturedRig.Euler(-1.6f*choke);
                    Limp(p,fall,.8f,side);break;
                }
                case NativeHordeKinds.Crawler:
                {
                    float flip=Sm((a-.05f)/.4f),curl=Sm((a-.3f)/.6f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.25f-.12f*flip,0);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(1.3f-2.9f*flip,push*.3f,side*.4f*flip);
                    for(int i=0;i<2;i++){int hip=i==0?CapturedRig.LHip:CapturedRig.RHip,arm=i==0?CapturedRig.LArm:CapturedRig.RArm;p.Rotation[hip]=CapturedRig.Euler(-1.4f*curl);p.Rotation[hip+1]=CapturedRig.Euler(2.1f*curl);p.Rotation[arm]=CapturedRig.Euler(-1.9f*curl,0,(i==0?1:-1)*.4f);p.Rotation[arm+1]=CapturedRig.Euler(-1.5f*curl);}
                    p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-.6f+.5f*curl);break;
                }
                case NativeHordeKinds.Bloater:
                {
                    float swell=Sm(a/BurstAt),shake=a<BurstAt?(float)Math.Sin(a*55)*.04f*swell:0,slump=Sm((a-BurstAt)/.5f);
                    p.Position[CapturedRig.Body]=new Vector3(shake,-.05f*swell-.5f*slump,0);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(-.25f*swell+.6f*slump,0,side*.2f*slump);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-.4f*swell,0,.9f*swell);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.4f*swell,0,-.9f*swell);
                    p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.4f*swell);
                    Limp(p,slump,1,side);break;
                }
                default:
                {
                    // The shambler: recoils off the blow, its knees buckle, it drops to them and pitches onto its face.
                    float buckle=Sm((a-.1f)/.35f),topple=Sm((a-.45f)/.45f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.38f*buckle-.3f*topple,-.06f*recoil+.25f*topple);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(-.3f*recoil+.25f*buckle+1.2f*topple,push*.25f,side*.15f*buckle);
                    p.Rotation[CapturedRig.LHip]=p.Rotation[CapturedRig.RHip]=CapturedRig.Euler(-1.4f*buckle*(1-topple*.7f));
                    p.Rotation[CapturedRig.LKnee]=p.Rotation[CapturedRig.RKnee]=CapturedRig.Euler(2.0f*buckle*(1-topple*.5f));
                    p.Rotation[CapturedRig.LFoot]=p.Rotation[CapturedRig.RFoot]=CapturedRig.Euler(-.6f*buckle);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-.3f*recoil-1.2f*topple,0,.4f*recoil);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.3f*recoil-1.0f*topple,0,-.5f*recoil);
                    p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.45f*recoil+.3f*topple,0,side*.3f*buckle);break;
                }
            }
            p.World();
        }
        /// <summary>When the bloater's body bursts after death (its parts hide; the burst's ink and gibs fly).</summary>
        public const float BurstAt=.55f;
        private static void Limp(CapturedRig.Pose p,float k,float spread,float side)
        {
            p.Rotation[CapturedRig.LArm]=Quaternion.Slerp(p.Rotation[CapturedRig.LArm],CapturedRig.Euler(-.4f,0,.9f*spread),k*.8f);p.Rotation[CapturedRig.RArm]=Quaternion.Slerp(p.Rotation[CapturedRig.RArm],CapturedRig.Euler(-.4f,0,-.9f*spread),k*.8f);
            p.Rotation[CapturedRig.LHip]=Quaternion.Slerp(p.Rotation[CapturedRig.LHip],CapturedRig.Euler(-.2f,0,.15f*side),k);p.Rotation[CapturedRig.RHip]=Quaternion.Slerp(p.Rotation[CapturedRig.RHip],CapturedRig.Euler(-.1f,0,-.1f),k);
            p.Rotation[CapturedRig.LKnee]=Quaternion.Slerp(p.Rotation[CapturedRig.LKnee],CapturedRig.Euler(.5f),k);p.Rotation[CapturedRig.RKnee]=Quaternion.Slerp(p.Rotation[CapturedRig.RKnee],CapturedRig.Euler(.3f),k);
        }
    }
}

using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM's motion (docs/EXPEDITION-BOSS-BACKROOMS.md section 6), on the shared boss motion library (docs/BOSS-MOTION.md): a pure reader of the view and the presentation clock that writes a <see cref="BossPose"/>,
    /// one joint per <see cref="NativeHumBossLook"/> joint: its world place and its turn away from the bind pose, so a skinned vertex is place + turn (v - bind).
    /// <list type="bullet">
    /// <item>Root: <see cref="BossRootFollow"/>; through the intro a presentation path from behind the wall out to the authority's spawn (<see cref="HumBossArrival"/>).</item>
    /// <item>Gait: <see cref="BossGait"/> with four limbs in a lateral sequence (left foot, left hand, right foot, right hand), very long strides; planted limbs locked; every body move goes through the gait's
    /// ExtraOffset/ExtraRotation so they stay planted. The hands stand on long arched fingers, each finger on its own second-order curl.</item>
    /// <item><b>The head is stabilised, like a bird's:</b> it holds its place and stays level, locked on its target, while the body moves round it; when the body has left it too far behind it thrusts to catch up. The neck's
    /// joints are solved along a curve between them, so the body can turn right round under it.</item>
    /// <item>Actions: one <see cref="BossActionPlayer"/> for the moves (contact on the record's `hitAt`), one for the phase changes, one for the staggers; the arrival, the clipped hold and the death are their own tracks.</item>
    /// <item>Second order: the strips and the wire tails are <see cref="BossChain"/>s, the arms lag and overshoot, the tubes whip on the spine, the tiles swing, the hitch of the flicker is an additive layer.</item>
    /// <item>The ceiling: the whole pose is computed on a floor and turned about the line the root was on when it began: a fixed half turn, so planted limbs stay planted upside down.</item>
    /// </list>
    /// Fixed fields and arrays: no allocation per frame. Nothing here is read by the authority.
    /// </summary>
    public sealed class HumBossRig
    {
        public const int Count=NativeHumBossLook.Count;private const float Deg=BossMath.Deg;
        private const int LegL=0,LegR=1,ArmL=2,ArmR=3;
        public readonly BossPose Pose=new BossPose(Count),Shown=new BossPose(Count);
        public readonly BossRootFollow Follow=new BossRootFollow{PositionHalfLife=.12f,MaxExtrapolate=.3f,TurnHalfLife=.18f,MaxTurnRate=NativeHumBossMotion.TurnRate*Deg,MaxTurnAccel=160*Deg,
            BankScale=.5f,MaxBank=3.5f*Deg,PitchScale=.5f,SpeedPitch=.006f,MaxPitch=3*Deg,LeanFrequency=1.1f,LeanDamping=.5f};
        public readonly BossGaitDef Gait=MakeGait();public readonly BossGait Legs;
        public readonly BossActionPlayer Actions=new BossActionPlayer(),Phases=new BossActionPlayer(),Staggers=new BossActionPlayer();
        public readonly BossFlinch Flinch=new BossFlinch{PitchGain=.05f,BankGain=.04f,ShiftGain=.05f,Rise=.08f,Wobble=.3f,WobbleHz=1.5f};
        public readonly BossStagger Stagger=new BossStagger{Throw=.08f,Shift=.5f,PitchGain=.12f,BankGain=.1f,Crouch=.28f};
        public readonly BossInertializer Inertia=new BossInertializer(Count);
        public readonly BossChain[] StripChain=new BossChain[NativeHumBossLook.Strips];public readonly BossChain[] WireChain=new BossChain[NativeHumBossLook.Tubes];
        private BossTurn lookYaw;private BossSpring lookPitch;private BossSpring3 headAt;private Vector3 headAnchor;private bool anchored;private float thrust;
        private BossSecondOrder tileA,tileB,jaw,ribs,heave;private BossSecondOrder3 chestLag;private readonly BossSecondOrder[] tubeWhip=new BossSecondOrder[NativeHumBossLook.Tubes];private readonly BossSecondOrder[] curl=new BossSecondOrder[2*NativeHumBossLook.Fingers];private bool configured;
        private readonly float[] ch=new float[HumCh.Count],sample=new float[HumCh.Count],sample2=new float[HumCh.Count];
        private readonly Quaternion[] linkRot=new Quaternion[Count];private readonly bool[] linked=new bool[Count];private readonly Vector3[] bindDir=new Vector3[Count];
        private bool started;private float seenSpawned=float.NaN,seenAt=float.NaN;private int actionSide=1,actionSideId;private float lastT=float.NaN;private bool arrivedBefore;
        // The turn over: the fixed line it turns about, latched as it begins; how far round (0 the floor .. 1 the ceiling).
        private Vector3 flipPoint,flipAxis=Vector3.UnitZ;private bool flipLatched,flipDown;private Quaternion flipTurn=Quaternion.Identity;private const float FlipMid=BackroomsLayout.Height*.5f;
        // ---- outputs the effects read ----
        public bool Visible,Arriving;public float ArrivalAge=float.NaN;
        /// <summary>The root as presented (in the room's world), its yaw (rad), ground speed; this frame's landings (bit per limb) and where.</summary>
        public Vector3 Root;public float Yaw,Speed;public int Landed;
        public readonly Vector3[] LimbAt=new Vector3[4];
        /// <summary>The action now, the move index and the side a sided move plays to (+1 right, -1 left).</summary>
        public BossActionSample Act;public int ActMove=-1,ActSide=1;
        public float[] Channels=>ch;
        /// <summary>How far round it has turned toward the ceiling (0..1).</summary>
        public float Flip;
        /// <summary>The body's stutter with the lamps this frame: the stage's flicker hitch (Reduced effects: none).</summary>
        public float Hitch;public bool Reduced;
        /// <summary>The jaw and the cage as drawn (0..1), the head's world place and its forward.</summary>
        public float JawOpen,RibOpen;public Vector3 HeadPos,HeadDir;public Vector3 Target;public bool HasTarget;
        /// <summary>How far each of the neck's three bones stretches along itself (the drawn skin scales its bones' local z by these: the neck's bind axis).</summary>
        public readonly float[] NeckStretch={1,1,1};
        /// <summary>Scratching: the planted fingers' drumming amount.</summary>
        public float Scratch;
        public uint Seed=0x4A1u;
        public float LastHit=float.NaN;
        public HumBossRig()
        {
            Legs=new BossGait(Gait);
            for(int i=0;i<Count;i++)linkRot[i]=Quaternion.Identity;
            var B=NativeHumBossLook.Bind;
            for(int s=0;s<StripChain.Length;s++)StripChain[s]=new BossChain(StripDef(s));
            for(int i=0;i<WireChain.Length;i++)WireChain[i]=new BossChain(new BossChainDef{Count=NativeHumBossLook.WireLinks+1,Length=NativeHumBossLook.WireLength,Rest=new Vector3(0,-1,0),Stiffness=.4f,TipStiffness=.1f,Damping=.8f,Gravity=9.8f,Radius=.02f,Friction=3});
        }
        private static BossChainDef StripDef(int s)=>new BossChainDef{Count=NativeHumBossLook.StripLinks+1,Length=NativeHumBossLook.StripLength[s]/NativeHumBossLook.StripLinks,Rest=new Vector3(0,-1,0),Stiffness=1.1f,TipStiffness=.12f,Damping=.75f,Gravity=9.8f,Radius=.03f,Friction=2.5f,Iterations=3};
        private static BossGaitDef MakeGait()
        {
            var L=NativeHumBossLook.Bind;var body=new Vector3(0,L[NativeHumBossLook.Pelvis].Y,0);
            BossLegDef Leg(int limb,int hip,int foot,float phase,float upper,float lower,float ankle)
            {
                var h=L[hip]-body;var f=L[foot];float s=limb%2==0?-1:1;
                return new BossLegDef{Hip=h,Home=new Vector3(f.X,0,limb>=2?.42f:f.Z),Pole=HumCh.DefaultPole(limb),Upper=upper,Lower=lower,Phase=phase,Side=s,Ankle=ankle};
            }
            return new BossGaitDef{Legs=new[]{
                    Leg(LegL,NativeHumBossLook.ThighL,NativeHumBossLook.FootL,0,NativeHumBossLook.Thigh,NativeHumBossLook.Shin,NativeHumBossLook.AnkleHeight),
                    Leg(LegR,NativeHumBossLook.ThighR,NativeHumBossLook.FootR,.5f,NativeHumBossLook.Thigh,NativeHumBossLook.Shin,NativeHumBossLook.AnkleHeight),
                    Leg(ArmL,NativeHumBossLook.UpperL,NativeHumBossLook.HandL,.25f,NativeHumBossLook.UpperArm,NativeHumBossLook.ForeArm,NativeHumBossLook.WristHeight),
                    Leg(ArmR,NativeHumBossLook.UpperR,NativeHumBossLook.HandR,.75f,NativeHumBossLook.UpperArm,NativeHumBossLook.ForeArm,NativeHumBossLook.WristHeight)},
                WalkSpeed=NativeHumBossMotion.WalkSpeed,RunSpeed=NativeHumBossMotion.RunSpeed,WalkStride=NativeHumBossMotion.Stride,RunStride=NativeHumBossMotion.RunStride,WalkDuty=NativeHumBossMotion.Duty,RunDuty=NativeHumBossMotion.RunDuty,
                WalkLift=NativeHumBossMotion.Lift,RunLift=NativeHumBossMotion.RunLift,MinFrequency=.38f,MinSwing=.26f,MaxSwing=1.1f,StartSpeed=.22f,StopSpeed=.08f,SettleDistance=.34f,SettleSwing=.62f,ReachMargin=.4f,MaxSink=.06f,
                BodyHeight=L[NativeHumBossLook.Pelvis].Y,Bob=.05f,RunBob=.1f,Sway=.09f,Roll=3.2f*Deg,Twist=5*Deg,LandDip=.1f,SupportFrequency=.95f,SupportDamping=.5f,LiftPeak=.4f,ToeOff=.3f,HeelStrike=.14f,FloorFollow=.5f,TurnRadius=1.6f};
        }
        /// <summary>A blow landed: the body flinches, the tubes flicker.</summary>
        public void Hit(double at,Vector3 push,float size){Flinch.Add(at,push,size,Follow.Yaw);LastHit=(float)at;}
        /// <summary>A pose in the room's world from one on the rig's floor (on the floor: the same).</summary>
        public Vector3 ToWorld(Vector3 p)=>Flip<=0?p:flipPoint+BossMath.Rotate(flipTurn,p-flipPoint);

        /// <summary>Poses the boss at `t` after a frame of `dt`. `velocity` the root's ground velocity when the caller has it; `externalRoot` when the caller stepped <see cref="Follow"/> itself (a guest); `target` what the head holds on.
        /// `arrival` is the room's arrival path (null: no intro).</summary>
        public void Update(float t,float dt,in HumBossView v,IBossGround ground,Vector3 velocity,bool externalRoot,bool hasTarget,Vector3 target)
        {
            Visible=v.live;if(!v.live){started=false;return;}
            if(!configured)Configure();dt=BossMath.Clamp(dt,0,.1f);
            Reduced=ReducedEffects;
            // ---- the turn over ----
            // Up: the line it turns about is the root's as it begins, along its facing. Down: a new line through where it is then (the body on the ceiling is re-seated on it; the pop killer takes the difference).
            float wantFlip=v.Turned(t);bool reseat=false;
            bool goingDown=HumBossView.At(v.floorAt)&&(!HumBossView.At(v.ceilingAt)||v.floorAt>=v.ceilingAt)&&t>=v.floorAt;
            if(wantFlip>0&&!flipLatched){flipLatched=true;flipDown=false;flipAxis=BossMath.Forward(v.yaw*Deg);flipPoint=new Vector3(v.x,v.y+FlipMid,v.z);}
            if(flipLatched&&goingDown&&!flipDown){flipDown=true;flipAxis=BossMath.Forward(v.yaw*Deg);flipPoint=new Vector3(v.x,v.y+FlipMid,v.z);reseat=true;}
            if(flipLatched&&wantFlip<=0&&(!HumBossView.At(v.ceilingAt)||goingDown))flipLatched=false;
            Flip=wantFlip;flipTurn=BossMath.AxisAngle(flipAxis,BossMath.Pi*Flip);
            float vyaw=v.yaw*Deg;var vroot=new Vector3(v.x,v.y,v.z);
            // ---- the arrival (presentation, from `spawned`), late joiners skip it ----
            if(!seenSpawned.Equals(v.spawned)){seenSpawned=v.spawned;seenAt=t;}
            float age=HumBossView.At(v.spawned)?t-v.spawned:float.PositiveInfinity;var beats=HumBossBeats.Current;
            Arriving=HumBossView.At(v.spawned)&&seenAt-v.spawned<=LateJoin&&age<beats.Hold&&age>=-1;ArrivalAge=Arriving?age:float.NaN;
            bool jump=false;
            if(Arriving)
            {
                HumBossArrival.Root(age,beats,vroot,vyaw,out var ap,out var av,out float ay);Follow.MaxTurnRate=3;Follow.MaxTurnAccel=10;Follow.Follow(ap,av,ay,dt,!started);
            }
            else if(!externalRoot)
            {
                // On the ceiling the record's root lies mirrored on the rig's floor. A teleport (Lights Out's reappearance) is a snap: nobody saw it.
                var goal=vroot;var gv=velocity;float gy=vyaw;
                if(flipLatched){var q=BossMath.AxisAngle(flipAxis,BossMath.Pi);goal=flipPoint+BossMath.Rotate(q,new Vector3(v.x,v.y+BackroomsLayout.Height,v.z)-flipPoint);gv=BossMath.Rotate(q,velocity);gv.Y=0;var fw=BossMath.Rotate(q,BossMath.Forward(vyaw));gy=BossMath.YawOf(fw);goal.Y=v.y;}
                bool cut=v.action=="cut"||v.action=="lunge";Follow.MaxTurnRate=(cut?NativeHumBossMotion.TurnRateFast:NativeHumBossMotion.TurnRate)*Deg;Follow.MaxTurnAccel=(cut?420:160)*Deg;
                float jumpAt=v.action=="cut"?2.5f:10;jump=(started&&Vector3.DistanceSquared(goal,Follow.Position)>jumpAt*jumpAt)||reseat;
                Follow.Follow(goal,gv,gy,dt,!started||jump);
            }
            bool first=!started;started=true;float yaw=Follow.Yaw;var facing=BossMath.Yaw(yaw);var fwd=BossMath.Forward(yaw);var right=BossMath.Right(yaw);Speed=Follow.Speed;Yaw=yaw;
            // ---- the record's move, phase change and stagger on their players ----
            int move=Arriving?-1:v.Move;
            if(move>=0&&v.actionId!=0&&HumBossView.At(v.actionAt))
            {
                if(actionSideId!=v.actionId){actionSideId=v.actionId;actionSide=SideOf(move,hasTarget,target);}
                float hit=HumBossView.At(v.hitAt)?v.hitAt-v.actionAt:float.NaN,dur=HumBossView.At(v.duration)?v.duration:float.NaN;
                Actions.Play(HumBossMoves.Defs[move],v.actionId,v.actionAt,hit,dur);if(HumBossView.At(v.cancelAt))Actions.Cancel(v.cancelAt);
            }
            if(HumBossView.At(v.phaseAt)&&v.phase>=2)Phases.Play(v.phase==2?HumBossMoves.Scream:HumBossMoves.RearUp,v.phase,v.phaseAt,v.phase==2?2.25f:1.5f,NativeHumBoss.PhaseChangeSeconds);
            if(HumBossView.At(v.staggerAt)&&v.staggerSeconds>0)
            {
                bool part=v.staggerCause=="part",choke=v.staggerCause=="choke";int key=v.staggerAt.GetHashCode();
                Staggers.Play(part?HumBossMoves.Flinch:choke?HumBossMoves.Choke:HumBossMoves.Stagger,key,v.staggerAt,part||choke?.1f:.14f,Math.Max(.6f,v.staggerSeconds));
                if(!part&&v.staggerCause!="phase"){var push=new Vector3(v.staggerPushX,0,v.staggerPushZ);if(push.LengthSquared()<1e-6f)push=-fwd;Stagger.Start(v.staggerAt,push,.7f,v.staggerSeconds,yaw);}
            }
            Actions.Sample(t);Phases.Sample(t);Staggers.Sample(t);
            Act=Actions.Current;ActMove=Act.Active&&move>=0&&ReferenceEquals(Actions.CurrentDef,HumBossMoves.Defs[move])?move:-1;ActSide=actionSide;
            // ---- the channels: neutral, then each layer in priority ----
            Array.Copy(HumCh.Neutral,ch,ch.Length);levelHand=0;
            Layer(Actions,MoveKeysFn,true);Layer(Phases,PhaseKeysFn,false);Layer(Staggers,StaggerKeysFn,false);
            if(Arriving)ArrivalLayer(age,beats);
            if(HumBossView.At(v.clipAt)&&v.clipSeconds>0&&t>=v.clipAt){float k=t-v.clipAt;Blend(HumBossMoves.Clipped,BossMath.Smooth(k/.12f)*(1-BossMath.Smooth((k-v.clipSeconds+.3f)/.3f)));}
            if(v.Dead(t))DeathLayer(t-v.diedAt);
            Additive(v,t);
            // ---- the body through the gait (planted limbs stay planted) ----
            Stagger.Sample(t,out var sShift,out float sPitch,out float sBank,out float sCrouch,out _);Flinch.Sample(t,out float fPitch,out float fBank,out var fShift);
            var lean=BossMath.Euler(ch[HumCh.BP]*Deg,ch[HumCh.BW]*Deg,ch[HumCh.BR]*Deg);float lead=BossMath.Clamp(Follow.YawRate*.14f,-9*Deg,9*Deg);
            float heaveY=heave.Step(ch[HumCh.BY],dt);
            var offset=BossMath.Rotate(facing,new Vector3(ch[HumCh.BX],heaveY-sCrouch,ch[HumCh.BZ])+sShift+fShift);
            Legs.ExtraOffset=offset;Legs.ExtraRotation=Quaternion.Normalize(BossMath.AxisAngle(Vector3.UnitY,lead)*lean*BossMath.AxisAngle(Vector3.UnitX,sPitch+fPitch)*BossMath.AxisAngle(Vector3.UnitZ,-(sBank+fBank)));
            for(int i=0;i<4;i++)Legs.Free[i]=ch[HumCh.Limb(i,HumCh.W)]>=.98f;
            var groundShown=ground;
            if(first||jump){Legs.Reset(Follow.Position,yaw,groundShown);for(int i=0;i<Count;i++)linked[i]=false;anchored=false;for(int i=0;i<4;i++)bendKnown[i]=false;}
            Legs.Step(Follow.Position,yaw,Follow.YawRate,Follow.Velocity,Follow.Body,dt,groundShown);Landed=Legs.Landed;
            var bodyOrigin=Legs.BodyPosition;var bodyRot=Legs.BodyRotation;
            // ---- FK: the trunk ----
            var P=Pose;float pz=NativeHumBossLook.Bind[NativeHumBossLook.Pelvis].Z;
            var pelvis=bodyOrigin+BossMath.Rotate(bodyRot,new Vector3(0,0,pz));
            P.RootPosition=Follow.Position;P.RootRotation=facing;P.Set(NativeHumBossLook.Root,Follow.Position,facing);P.Set(NativeHumBossLook.Pelvis,pelvis,bodyRot);
            float bend=ch[HumCh.Bend]*Deg,twist=ch[HumCh.Twist]*Deg,roll=ch[HumCh.Roll]*Deg,rear=BossMath.Clamp01(ch[HumCh.Rear]);
            // Rearing unbends the stoop: the spine's bind curve is taken back out.
            var unbendA=BossMath.AxisAngle(Vector3.UnitX,-rear*17*Deg);var unbendB=BossMath.AxisAngle(Vector3.UnitX,-rear*30*Deg);var unbendC=BossMath.AxisAngle(Vector3.UnitX,-rear*50*Deg);
            var lag=chestLag.Step(new Vector3(BossMath.Clamp(-Vector3.Dot(Follow.Velocity,fwd)*.015f,-.12f,.12f),BossMath.Clamp(Follow.YawRate*-.06f,-.14f,.14f),BossMath.Clamp(Legs.BankOffset*-.8f,-.08f,.08f)),dt);
            var sp0=Quaternion.Normalize(bodyRot*unbendA*BossMath.Euler(bend*.28f,twist*.25f+Legs.TwistOffset*-.8f,roll*.25f));
            var sp1=Quaternion.Normalize(sp0*unbendB*BossMath.Euler(bend*.34f+lag.X*.5f,twist*.35f+Legs.TwistOffset*-.6f+lag.Y*.5f,roll*.35f+lag.Z*.5f));
            var chest=Quaternion.Normalize(sp1*unbendC*BossMath.Euler(bend*.38f+lag.X*.5f,twist*.4f+Legs.TwistOffset*-.5f+lag.Y*.5f,roll*.4f+lag.Z*.5f));
            var pSp0=pelvis+BossMath.Rotate(bodyRot,NativeHumBossLook.Rest[NativeHumBossLook.Spine0]);var pSp1=pSp0+BossMath.Rotate(sp0,NativeHumBossLook.Rest[NativeHumBossLook.Spine1]);var pChest=pSp1+BossMath.Rotate(sp1,NativeHumBossLook.Rest[NativeHumBossLook.Chest]);
            // The back scrapes the ceiling: it never pushes up through it unless it means to rear.
            if(rear<.05f){float over=pChest.Y+.26f-(BackroomsLayout.Height-.25f);if(over>0){pChest.Y-=over;pSp1.Y-=over*.7f;pSp0.Y-=over*.35f;}}
            P.Set(NativeHumBossLook.Spine0,pSp0,sp0);P.Set(NativeHumBossLook.Spine1,pSp1,sp1);P.Set(NativeHumBossLook.Chest,pChest,chest);
            // ---- the head: level, holding its place, locked on its target; the neck solved between ----
            HasTarget=hasTarget;Target=hasTarget?(Flip>0?flipPoint+BossMath.Rotate(BossMath.Inverse(flipTurn),target-flipPoint):target):Vector3.Zero;
            SolveHead(t,dt,v,yaw,pChest,chest,fwd);
            // ---- arms, legs ----
            float shrug=ch[HumCh.Shrug];
            for(int side=0;side<2;side++)
            {
                int clav=side==0?NativeHumBossLook.ClavL:NativeHumBossLook.ClavR,upper=clav+1,fore=clav+2,hand=clav+3,limb=side==0?ArmL:ArmR;
                var pc=pChest+BossMath.Rotate(chest,NativeHumBossLook.Rest[clav]);var pu=pc+BossMath.Rotate(chest,NativeHumBossLook.Rest[upper]+new Vector3(0,shrug,0));
                P.Set(clav,pc,chest);
                SolveLimb(limb,upper,fore,hand,pu,facing,NativeHumBossLook.UpperArm,NativeHumBossLook.ForeArm,.03f,t,dt,ground);
            }
            for(int side=0;side<2;side++)
            {
                int thigh=side==0?NativeHumBossLook.ThighL:NativeHumBossLook.ThighR,limb=side==0?LegL:LegR;
                var hip=pelvis+BossMath.Rotate(bodyRot,NativeHumBossLook.Rest[thigh]);
                SolveLimb(limb,thigh,thigh+1,thigh+2,hip,facing,NativeHumBossLook.Thigh,NativeHumBossLook.Shin,0,t,dt,ground);
            }
            Hands(t,dt);
            // ---- the cage, the Ballast, the tubes, the tiles, the wires, the strips ----
            RibOpen=ribs.Step(ch[HumCh.Ribs],dt);JawOpen=jaw.Step(ch[HumCh.Jaw],dt);
            for(int s=0;s<2;s++){int j=s==0?NativeHumBossLook.RibL:NativeHumBossLook.RibR;float sd=s==0?-1:1;var r=Quaternion.Normalize(chest*BossMath.AxisAngle(Vector3.UnitY,-sd*RibOpen*38*Deg)*BossMath.AxisAngle(Vector3.UnitZ,sd*RibOpen*8*Deg));P.Set(j,pChest+BossMath.Rotate(chest,NativeHumBossLook.Rest[j]),r);}
            float hum=HumOf(v,t);
            var ballastShake=new Vector3(.006f*hum*BossBreath.Idle(t,Seed,70,31),.006f*hum*BossBreath.Idle(t,Seed,71,29),0);P.Set(NativeHumBossLook.Ballast,pChest+BossMath.Rotate(chest,NativeHumBossLook.Rest[NativeHumBossLook.Ballast]+ballastShake),chest);
            for(int i=0;i<NativeHumBossLook.Tubes;i++)
            {
                int j=NativeHumBossLook.Tube(i),par=NativeHumBossLook.Parent[j];
                float whip=tubeWhip[i].Step(Legs.Bob*2.2f+Legs.BankOffset*(i%2==0?1:-1)*.8f+Follow.YawRate*.02f*(i-3),dt);
                var pr=P.Rotation[par];P.Set(j,P.Position[par]+BossMath.Rotate(pr,NativeHumBossLook.Rest[j]),Quaternion.Normalize(pr*BossMath.Euler(whip*.5f,0,whip*.7f)));
            }
            {
                float a=tileA.Step(Legs.BankOffset*4+Follow.YawRate*.2f,dt),b=tileB.Step(-Legs.Bob*5+Follow.YawRate*-.15f,dt);
                for(int i=0;i<NativeHumBossLook.Tiles;i++){int j=NativeHumBossLook.Tile(i),par=NativeHumBossLook.Parent[j];var pr=P.Rotation[par];P.Set(j,P.Position[par]+BossMath.Rotate(pr,NativeHumBossLook.Rest[j]),Quaternion.Normalize(pr*BossMath.Euler((i%2==0?a:b)*.5f,0,(i%2==0?b:a)*.6f)));}
            }
            float g=9.8f*(float)Math.Cos(Math.PI*Flip);var chainUp=Flip>0?BossMath.Inverse(flipTurn):Quaternion.Identity;
            for(int i=0;i<WireChain.Length;i++)
            {
                int w=NativeHumBossLook.Wire(i,0),par=NativeHumBossLook.Parent[w];var pr=P.Rotation[par];var c=WireChain[i];c.Def.Gravity=g;var anchor=P.Position[par]+BossMath.Rotate(pr,NativeHumBossLook.Rest[w]);
                c.Step(anchor,chainUp,dt,Flip>0?null:ground);Put(c,w,NativeHumBossLook.WireLinks);
            }
            for(int s=0;s<StripChain.Length;s++)
            {
                int w=NativeHumBossLook.Strip(s,0),par=NativeHumBossLook.Parent[w];var pr=P.Rotation[par];var c=StripChain[s];c.Def.Gravity=g;var anchor=P.Position[par]+BossMath.Rotate(pr,NativeHumBossLook.Rest[w]);
                // In a thrown lunge they stream behind it (their rest shape stays straight down in the room).
                c.Step(anchor,chainUp,dt,Flip>0?null:ground);Put(c,w,NativeHumBossLook.StripLinks);
            }
            WallArm(t,v);
            // ---- into the room's world (the half turn on the ceiling), and the pop killer ----
            if(Flip>0)for(int i=0;i<NativeHumBossLook.WallFore;i++){P.Position[i]=flipPoint+BossMath.Rotate(flipTurn,P.Position[i]-flipPoint);P.Rotation[i]=Quaternion.Normalize(flipTurn*P.Rotation[i]);}
            Root=Flip>0?flipPoint+BossMath.Rotate(flipTurn,Follow.Position-flipPoint):Follow.Position;
            for(int i=0;i<4;i++)LimbAt[i]=Flip>0?flipPoint+BossMath.Rotate(flipTurn,Legs.Foot[i]-flipPoint):Legs.Foot[i];
            HeadPos=P.Position[NativeHumBossLook.Head];HeadDir=BossMath.Rotate(P.Rotation[NativeHumBossLook.Head],Vector3.UnitZ);
            if(!first&&(jump||ArrivalEnded()))Inertia.Transition(Shown,Pose);
            Inertia.Apply(Pose,.16f,dt,Shown);lastT=t;
        }
        /// <summary>A late joiner (more than this long after the arrival, s) sees the boss in place.</summary>
        public const float LateJoin=3;
        /// <summary>Reduced effects (the presenter's): the flicker hitch stays off.</summary>
        public bool ReducedEffects;
        private bool ArrivalEnded(){bool now=Arriving;bool ended=arrivedBefore&&!now;arrivedBefore=now;return ended;}
        private void Configure()
        {
            jaw.Configure(3.2f,.5f,.4f);ribs.Configure(2.2f,.45f,0);heave.Configure(1.5f,.6f,1.2f);tileA.Configure(1.1f,.22f,0);tileB.Configure(1.2f,.2f,0);chestLag.Configure(1.3f,.45f,0);
            for(int i=0;i<tubeWhip.Length;i++)tubeWhip[i].Configure(3.4f+i*.22f,.28f,0);
            for(int i=0;i<curl.Length;i++)curl[i].Configure(3.4f+.3f*(i%4),.5f,.3f);
            for(int i=0;i<armGoal.Length;i++)armGoal[i].Configure(3.6f,.62f,.4f);
            configured=true;
        }
        // ---- layers ----
        private static readonly Func<BossActionDef,HumSnap[]> MoveKeysFn=HumBossMoves.KeysOf,PhaseKeysFn=PhaseKeys,StaggerKeysFn=StaggerKeys;
        private static HumSnap[] PhaseKeys(BossActionDef d)=>ReferenceEquals(d,HumBossMoves.Scream)?HumBossMoves.ScreamKeys:ReferenceEquals(d,HumBossMoves.RearUp)?HumBossMoves.RearKeys:null;
        private static HumSnap[] StaggerKeys(BossActionDef d)=>ReferenceEquals(d,HumBossMoves.Flinch)?HumBossMoves.FlinchKeys:ReferenceEquals(d,HumBossMoves.Choke)?HumBossMoves.ChokeKeys:HumBossMoves.StaggerKeys;
        private void Layer(BossActionPlayer p,Func<BossActionDef,HumSnap[]> keysOf,bool sided)
        {
            var a=p.Current;var b=p.Previous;float wa=a.Active?a.Weight:0,wb=b.Active?b.Weight:0;if(wa+wb<=1e-5f)return;
            var ka=wa>0?keysOf(p.CurrentDef):null;var kb=wb>0?keysOf(p.PreviousDef):null;if(ka==null)wa=0;if(kb==null)wb=0;if(wa+wb<=1e-5f)return;
            if(wa>0)HumBossMoves.Sample(ka,a.Progress,sided&&Mirrored(p.CurrentDef),sample);if(wb>0)HumBossMoves.Sample(kb,b.Progress,sided&&Mirrored(p.PreviousDef),sample2);
            float w=Math.Min(1,wa+wb),na=wa/(wa+wb),nb=wb/(wa+wb);
            for(int c=0;c<ch.Length;c++){float s=(wa>0?sample[c]*na:0)+(wb>0?sample2[c]*nb:0);ch[c]+=(s-ch[c])*w;}
        }
        private bool Mirrored(BossActionDef d){for(int i=0;i<HumBossMoves.Defs.Length;i++)if(ReferenceEquals(HumBossMoves.Defs[i],d)){int s=HumBossMoves.Side[i];return s!=0&&s!=actionSide;}return false;}
        private int SideOf(int move,bool hasTarget,Vector3 target)
        {
            if(HumBossMoves.Side[move]==0)return 1;if(!hasTarget)return HumBossMoves.Side[move];var d=target-Follow.Position;return Vector3.Dot(d,BossMath.Right(Follow.Yaw))>=0?1:-1;
        }
        private void Blend(HumSnap s,float w){if(w<=0)return;w=Math.Min(1,w);var x=s.V;for(int c=0;c<ch.Length;c++)ch[c]+=(x[c]-ch[c])*w;}
        // ---- the intro's own track: the arm through the wall, the walk out, the look, the jaw, the straightening, the stoop ----
        private float levelHand;
        private void ArrivalLayer(float a,in HumBossBeats b)
        {
            // The hand: from behind the wall out through the tear. Root-frame targets: the root is still behind the wall here.
            float reach=BossMath.Smoother((a-(b.Bulge-.45f))/(b.Fingers-b.Bulge+.25f)),back=BossMath.Smoother((a-b.WalkOut)/.8f);
            if(a<b.WalkOut+.8f)
            {
                var wr=HumBossArrival.HandThrough(a,b,Follow.Position,Follow.Yaw);
                float w=reach*(1-back);int l=HumCh.Limb(ArmR,0);ch[l]=w;ch[l+1]=wr.X;ch[l+2]=wr.Y;ch[l+3]=wr.Z;var pole=Vector3.Normalize(new Vector3(.9f,.25f,-.4f));ch[l+4]=pole.X;ch[l+5]=pole.Y;ch[l+6]=pole.Z;
                // Through, the fingers hook over the tear's lower lip and pull: that's what opens it.
                float grip=HumBossArrival.Grip(a,b);ch[HumCh.CurlR]+=(-.58f+.08f*(float)Math.Sin(a*2.4f)+grip*(1.08f+.05f*(float)Math.Sin(a*5.1f))-ch[HumCh.CurlR])*w;ch[HumCh.SpreadR]=w*(.75f+.3f*grip);levelHand=w;
                ch[HumCh.BW]+=8*w;ch[HumCh.Bend]+=10*w;
            }
            // Walking out it stays low and looks at nothing (its head follows the body) until the head catches: then it locks on the party.
            ch[HumCh.Lock]=BossMath.Smoother((a-b.Look)/.45f);
            float flick=BossMath.Smooth((a-b.HeadFlicker)/.3f)*(1-BossMath.Smooth((a-b.Look)/.5f));ch[HumCh.HP]+=6*flick;
            ch[HumCh.Jaw]=Math.Max(ch[HumCh.Jaw],BossMath.Smoother((a-b.JawSplit)/.3f)*(1-BossMath.Smoother((a-b.Straighten-.2f)/.35f)*.25f)*(1-BossMath.Smoother((a-b.Stoop)/.32f)));
            // It straightens, head and shoulders through the ceiling, then stoops as the lights slam on.
            float up=BossMath.Smoother((a-b.Straighten)/(b.Stoop-b.Straighten))*(1-BossMath.Smoother((a-b.Stoop-.05f)/(b.Hold-b.Stoop-.05f)));
            Blend(HumBossMoves.Upright,up);
        }
        // ---- the death: the arms give way, it folds forward slowly onto its knuckles, the head left hanging ----
        private void DeathLayer(float d)
        {
            float fold=BossMath.Smoother((d-.5f)/1.9f);Blend(HumBossMoves.Folded,fold);
            float sag=(float)Math.Sin(Math.Min(1,d/2.4f)*Math.PI)*.0f;ch[HumCh.Lock]*=1-BossMath.Smooth((d-.2f)/1.0f);ch[HumCh.Jaw]=Math.Max(0,ch[HumCh.Jaw]-sag);
        }
        // How loud the hum is in the body (0..1): wind-ups, the pulse's charge.
        private float HumOf(in HumBossView v,float t)
        {
            float k=.25f;if(ActMove>=0){var m=NativeHumBoss.Moves[ActMove];float u=m.Wind>0?Act.Age/Math.Max(.01f,Act.HitAt):1;k=Math.Max(k,u<1?.25f+.75f*u:Math.Max(.25f,1-(Act.Age-Act.HitAt)/1.1f));}
            if(v.Dead(t))k*=1-BossMath.Clamp01((t-v.diedAt)/3.2f);return k;
        }
        private void Additive(in HumBossView v,float t)
        {
            // Breath, and an idle sway in the stoop; the head has none of it (the body moves round it).
            float br=BossBreath.Sample(t,.2f,Seed);ch[HumCh.Bend]+=1.3f*br;ch[HumCh.BY]+=.012f*br;
            ch[HumCh.BR]+=.7f*BossBreath.Idle(t,Seed,5,.17f);ch[HumCh.BP]+=.6f*BossBreath.Idle(t,Seed,6,.14f);ch[HumCh.Twist]+=2.5f*BossBreath.Idle(t,Seed,7,.12f);ch[HumCh.Roll]+=1.5f*BossBreath.Idle(t,Seed,8,.15f);
            // Max Hum: it shakes with the charge; a scream shakes it too.
            float shake=0;if(ActMove>=0&&NativeHumBoss.Moves[ActMove].Id=="pulse")shake=BossMath.Clamp01(Act.Age/Math.Max(.1f,Act.HitAt))*(1-BossMath.Smooth((Act.Age-Act.Duration+.8f)/.4f));
            if(Phases.Current.Active&&ReferenceEquals(Phases.CurrentDef,HumBossMoves.Scream))shake=Math.Max(shake,BossMath.Smooth(Phases.Current.Age/1.6f)*(1-BossMath.Smooth((Phases.Current.Age-2.05f)/.1f)));
            if(shake>0){ch[HumCh.BR]+=.9f*shake*BossBreath.Idle(t,Seed,40,13);ch[HumCh.BP]+=.7f*shake*BossBreath.Idle(t,Seed,41,14);ch[HumCh.Bend]+=2*shake*BossBreath.Idle(t,Seed,42,11);}
            // The flicker hitch: a stepped jolt in the shoulders and spine with the lamps; never in the root, never for the head.
            if(Hitch>0&&!Reduced){float s=(float)Math.Floor(t*22);float h(int c)=>(BossMath.Hash(Seed^(uint)c,(int)s)-.5f)*2*Hitch;ch[HumCh.Bend]+=5*h(1);ch[HumCh.Twist]+=7*h(2);ch[HumCh.Roll]+=4*h(3);ch[HumCh.Shrug]+=.06f*h(4);}
            Scratch=ActMove>=0?0:.5f+.5f*BossBreath.Idle(t,Seed,9,.3f);
        }
        // ---- the head ----
        private void SolveHead(float t,float dt,in HumBossView v,float yaw,Vector3 pChest,Quaternion chest,Vector3 bodyFwd)
        {
            var P=Pose;float lockOn=BossMath.Clamp01(ch[HumCh.Lock]);float rear=BossMath.Clamp01(ch[HumCh.Rear]);
            var toT=HasTarget?Target-pChest:bodyFwd;var flat=BossMath.Flat(toT);float dist=flat.Length();var towardT=dist>1e-3f?flat/dist:bodyFwd;
            // The direction the head stands off the chest: toward the target, but it can only come so far round the body (it wraps the chest on the near side).
            var along=Vector3.Lerp(bodyFwd,towardT,lockOn*.78f);if(along.LengthSquared()<.05f)along=BossMath.Right(yaw);along=Vector3.Normalize(along);
            var side=new Vector3(along.Z,0,-along.X);
            var rest=NativeHumBossLook.Bind[NativeHumBossLook.Head]-NativeHumBossLook.Bind[NativeHumBossLook.Chest];
            // Its height holds from the floor up, not from the chest: the body's bob and sag never reach it. Only a move's channel moves it.
            float headH=NativeHumBossLook.Bind[NativeHumBossLook.Head].Y+ch[HumCh.HY];
            var goal=pChest+along*(rest.Z+ch[HumCh.HZ])+side*ch[HumCh.HX];goal.Y=Follow.Position.Y+headH;
            if(rear>.01f)goal.Y=Math.Max(goal.Y,pChest.Y+rest.Y*(1-rear)+rear*1.0f+ch[HumCh.HY]*(1-rear));
            // Stick and slip: it holds its place until the goal has left it behind (or a channel moves it), then thrusts over and past.
            if(!anchored){headAt.Reset(goal);headAnchor=goal;anchored=true;}
            var moving=BossMath.Flat(goal-headAnchor);bool chan=Math.Abs(ch[HumCh.HY])+Math.Abs(ch[HumCh.HZ])>.02f||rear>.01f||lockOn<.6f;
            float slack=NativeHumBossMotion.HeadSlack;
            if(chan){headAnchor=goal;thrust=1;}
            else if(moving.Length()>slack){headAnchor=goal+Follow.Velocity*.16f;thrust=1;}
            else headAnchor.Y=goal.Y;
            thrust=Math.Max(0,thrust-dt/.3f);
            float hl=BossMath.Lerp(NativeHumBossMotion.HeadHold,NativeHumBossMotion.HeadThrust,chan?1:thrust);
            var hp=headAt.Step(headAnchor,hl,dt);
            // The neck can only reach so far: past it, it's dragged.
            var fromChest=hp-pChest;float reach=fromChest.Length(),maxReach=rear>.4f?2.6f:2.25f;if(reach>maxReach){hp=pChest+fromChest*(maxReach/reach);headAt.Reset(hp);}
            // The floor: it never goes through (a low Stoop's head stops above it).
            hp.Y=Math.Max(hp.Y,Follow.Position.Y+.34f);
            // The look: yaw and pitch level and locked: no roll, ever.
            float goalYaw=HasTarget&&lockOn>.02f?yaw+BossMath.WrapPi(BossMath.YawOf(Target-hp)-yaw)*lockOn:yaw;
            lookYaw.Step(goalYaw,.05f,NativeHumBossMotion.LookRate,NativeHumBossMotion.LookAccel,dt);
            float flatD=Math.Max(.2f,BossMath.FlatLength(Target-hp));float goalPitch=(HasTarget?(float)Math.Atan2(hp.Y-Target.Y,flatD)*lockOn:0)+ch[HumCh.HP]*Deg;lookPitch.Step(goalPitch,.09f,dt);
            var headRot=Quaternion.Normalize(BossMath.Yaw(lookYaw.Yaw)*BossMath.AxisAngle(Vector3.UnitX,lookPitch.Value));
            P.Set(NativeHumBossLook.Head,hp,headRot);
            float j=JawOpen;
            P.Set(NativeHumBossLook.JawU,hp+BossMath.Rotate(headRot,NativeHumBossLook.Rest[NativeHumBossLook.JawU]),Quaternion.Normalize(headRot*BossMath.AxisAngle(Vector3.UnitX,-j*72*Deg)));
            P.Set(NativeHumBossLook.JawL,hp+BossMath.Rotate(headRot,NativeHumBossLook.Rest[NativeHumBossLook.JawL]),Quaternion.Normalize(headRot*BossMath.AxisAngle(Vector3.UnitX,j*72*Deg)));
            // The neck: three joints down a curve from the chest to the back of the fixture, each turned along it from the last.
            var a=pChest+BossMath.Rotate(chest,NativeHumBossLook.Rest[NativeHumBossLook.Neck0]);var d=hp+BossMath.Rotate(headRot,new Vector3(0,0,-.2f));
            var ta=BossMath.Rotate(chest,Vector3.Normalize(NativeHumBossLook.Rest[NativeHumBossLook.Neck1]));var td=BossMath.Rotate(headRot,Vector3.UnitZ);float L=Math.Max(.2f,(d-a).Length());
            var b=a+ta*(L*.42f);var c=d-td*(L*.42f);
            Vector3 Q(float u){float w=1-u;return a*(w*w*w)+b*(3*w*w*u)+c*(3*w*u*u)+d*(u*u*u);}
            var p0=a;var p1=Q(.36f);var p2=Q(.7f);var p3=Q(1);
            var dir0=NativeHumBossLook.Bind[NativeHumBossLook.Neck1]-NativeHumBossLook.Bind[NativeHumBossLook.Neck0];var r0=Quaternion.Normalize(BossMath.FromTo(BossMath.Rotate(chest,dir0),p1-p0)*chest);
            var dir1=NativeHumBossLook.Bind[NativeHumBossLook.Neck2]-NativeHumBossLook.Bind[NativeHumBossLook.Neck1];var r1=Quaternion.Normalize(BossMath.FromTo(BossMath.Rotate(r0,dir1),p2-p1)*r0);
            var dir2=NativeHumBossLook.Bind[NativeHumBossLook.Head]-NativeHumBossLook.Bind[NativeHumBossLook.Neck2];var r2=Quaternion.Normalize(BossMath.FromTo(BossMath.Rotate(r1,dir2),p3-p2)*r1);
            P.Set(NativeHumBossLook.Neck0,p0,r0);P.Set(NativeHumBossLook.Neck1,p1,r1);P.Set(NativeHumBossLook.Neck2,p2,r2);
            // The neck pays out: each of its bones stretches along itself to span the curve (the wet carpet going thin).
            NeckStretch[0]=BossMath.Clamp((p1-p0).Length()/dir0.Length(),.4f,5);NeckStretch[1]=BossMath.Clamp((p2-p1).Length()/dir1.Length(),.4f,5);NeckStretch[2]=BossMath.Clamp((p3-p2).Length()/dir2.Length(),.4f,5);
        }
        // ---- a limb: the gait's planted foot or the channels' target, two-bone IK ----
        private readonly BossSecondOrder3[] armGoal=new BossSecondOrder3[2];private readonly Vector3[] bend=new Vector3[4];private readonly bool[] bendKnown=new bool[4];private readonly Vector3[] lastHinge=new Vector3[4];private readonly bool[] hingeKnown=new bool[4];
        private void SolveLimb(int limb,int upper,int lower,int end,Vector3 root,Quaternion facing,float l1,float l2,float soft,float t,float dt,IBossGround ground)
        {
            int b=HumCh.Limb(limb,0);float w=ch[b];var bodyRot=Legs.BodyRotation;
            var gaitFoot=Legs.Foot[limb];var gaitPole=BossMath.Rotate(bodyRot,Gait.Legs[limb].Pole);
            var tw=Follow.Position+BossMath.Rotate(facing,new Vector3(ch[b+1],ch[b+2],ch[b+3]));
            var pole=Vector3.Normalize(Vector3.Lerp(gaitPole,BossMath.Rotate(facing,new Vector3(ch[b+4],ch[b+5],ch[b+6])),w));
            // The arms lag a move's target a little and overshoot it (second order on the target only: planted hands stay exact).
            if(limb>=2){ref var lag=ref armGoal[limb-2];if(w<.01f)lag.Reset(tw);else tw=lag.Step(tw,dt);}
            var target=Vector3.Lerp(gaitFoot,tw,w);
            // A planted hand stays planted: past the arm's reach the shoulder itself comes down toward it (the blade sliding on the ribs).
            if(limb>=2){var toHand=target-root;float over=toHand.Length()-(l1+l2)*.995f;if(over>0)root+=Vector3.Normalize(toHand)*Math.Min(over,.42f);}
            // The elbow or knee comes round the limb to where it should bend at a limited rate: it can't flip across in a frame when its pole passes along the limb.
            {
                var toEnd=target-root;float tl=toEnd.Length();var along=tl>1e-4f?toEnd/tl:-Vector3.UnitY;var side=pole-along*Vector3.Dot(pole,along);float sl=side.Length();
                if(!bendKnown[limb]){bend[limb]=sl>1e-3f?side/sl:Vector3.UnitX;bendKnown[limb]=true;}
                var was=bend[limb]-along*Vector3.Dot(bend[limb],along);float wl=was.Length();was=wl>1e-3f?was/wl:(sl>1e-3f?side/sl:bend[limb]);
                if(sl>1e-3f)
                {
                    side/=sl;float angle=(float)Math.Atan2(Vector3.Dot(along,Vector3.Cross(was,side)),Vector3.Dot(was,side));
                    float turn=BossMath.Clamp(angle*(1-(float)Math.Exp(-dt*16)),-11*dt,11*dt);was=BossMath.Rotate(BossMath.AxisAngle(along,turn),was);
                }
                bend[limb]=was;pole=was;
            }
            var knee=BossIk.TwoBone(root,target,l1,l2,pole,out var e,w>.01f?soft*w:0);
            var hinge=Vector3.Cross(knee-root,e-knee);if(hinge.LengthSquared()<1e-8f)hinge=Vector3.Cross(e-root,pole);
            if(hingeKnown[limb]&&Vector3.Dot(hinge,lastHinge[limb])<0&&hinge.LengthSquared()<1e-4f)hinge=lastHinge[limb];lastHinge[limb]=hinge;hingeKnown[limb]=true;
            var up=NativeHumBossLook.Limb(root,knee,hinge,pole);var lo=NativeHumBossLook.Limb(knee,e,hinge,pole);
            var P=Pose;
            P.Set(upper,root,Quaternion.Normalize(up*BossMath.Inverse(NativeHumBossLook.BindLimb[upper])));P.Set(lower,knee,Quaternion.Normalize(lo*BossMath.Inverse(NativeHumBossLook.BindLimb[lower])));
            // The end: on the gait its planted turn; on a channel it carries on along the forearm.
            var gaitEnd=Legs.FootRotation[limb];
            var dir=Vector3.Normalize(e-knee);var alongEnd=Quaternion.Normalize(BossMath.FromTo(BossMath.Rotate(facing,Vector3.UnitZ),dir)*facing);
            var endRot=w>0?BossMath.Slerp(gaitEnd,limb>=2?alongEnd:gaitEnd,BossMath.Smooth(w)):gaitEnd;
            if(limb==ArmR&&levelHand>0)endRot=BossMath.Slerp(endRot,facing,levelHand);
            P.Set(end,e,Quaternion.Normalize(endRot));
        }
        /// <summary>Whether the Wall Hand's arm is out this frame (its joints are hidden otherwise).</summary>
        public bool WallArmOut;
        // Wall Hand: an arm out of the surface by the pilot: fingers feeling through first, then the forearm thrust out and gripping, held, dragged back in. In the room's world (the record's point), whatever the body's doing.
        private void WallArm(float t,in HumBossView v)
        {
            var P=Pose;var B=NativeHumBossLook.Bind;WallArmOut=false;
            if(!HumBossView.At(v.handAt)){for(int j=NativeHumBossLook.WallFore;j<Count;j++)P.Set(j,Follow.Position,Quaternion.Identity);return;}
            var m=NativeHumBoss.Move("hand");float k=t-v.handAt;float end=HumBossView.At(v.handUntil)?v.handUntil-v.handAt:m.Strike+m.Follow;
            WallArmOut=k>-.42f&&k<end+.5f;if(!WallArmOut){for(int j=NativeHumBossLook.WallFore;j<Count;j++)P.Set(j,Follow.Position,Quaternion.Identity);return;}
            var n=new Vector3(v.handNx,0,v.handNz);var side=new Vector3(n.Z,0,-n.X);var at=new Vector3(v.handX,v.handY,v.handZ);
            float wind=BossMath.Smooth((k+.42f)/.42f),thrust=BossMath.Smoother(k/m.Strike),back=BossMath.Smoother((k-end)/.5f);
            // The wrist: behind the paper (the fingers just through), then well out over the floor; the forearm trailing from inside the wall.
            float outBy=(-.75f+.42f*wind+1.05f*thrust)*(1-back)+(-1.2f)*back;
            var wrist=at+n*outBy+new Vector3(0,.18f*thrust*(float)Math.Sin(k*2.1f)*.3f,0);
            var reach=Vector3.Normalize(n+new Vector3(0,-.32f,0));var elbow=wrist-reach*NativeHumBossLook.ForeArm;
            var upright=BossMath.AxisAngle(Vector3.UnitY,(float)Math.Atan2(n.X,n.Z));
            var dir0=BossMath.Rotate(upright,B[NativeHumBossLook.WallHand]-B[NativeHumBossLook.WallFore]);var r=Quaternion.Normalize(BossMath.FromTo(dir0,wrist-elbow)*upright);
            P.Set(NativeHumBossLook.WallFore,elbow,r);
            var hr=Quaternion.Normalize(BossMath.FromTo(BossMath.Rotate(upright,Vector3.UnitZ),Vector3.Normalize(n+new Vector3(0,-.2f,0)))*upright);P.Set(NativeHumBossLook.WallHand,wrist,hr);
            float c=k<0?-.75f:(k<m.Strike?-.75f+.9f*thrust:.15f+.65f*BossMath.Smooth((k-m.Strike)/.3f));
            for(int f=0;f<NativeHumBossLook.Fingers;f++)
            {
                float yawHere=-NativeHumBossLook.FingerYawL[f]*Deg;var hinge=new Vector3((float)Math.Cos(yawHere),0,-(float)Math.Sin(yawHere));
                var rf=Quaternion.Normalize(hr*BossMath.AxisAngle(Vector3.UnitY,(f-1.5f)*.2f*(1-thrust*.5f)));var pp=wrist;int par=NativeHumBossLook.WallHand;
                for(int l=0;l<NativeHumBossLook.FingerLinks;l++)
                {
                    int j=NativeHumBossLook.WallFinger(f,l);float bp=NativeHumBossLook.FingerPitch[f,l],pv=l>0?NativeHumBossLook.FingerPitch[f,l-1]:0;
                    float straight=l==0?bp:bp-pv,claw=l==0?46:l==1?24:22;float turn=c>=0?claw*c:straight*-c;float jitter=(float)Math.Sin(t*23+f*2.3f+l)*2.2f*(k<0?1:.3f);
                    var pos=pp+BossMath.Rotate(l==0?rf:P.Rotation[par],NativeHumBossLook.Rest[j]);rf=Quaternion.Normalize(rf*BossMath.AxisAngle(hinge,(turn+jitter)*Deg));P.Set(j,pos,rf);par=j;pp=pos;
                }
            }
        }
        // ---- the hands' fingers: three links each, FK off the hand, curling on second-order springs ----
        private void Hands(float t,float dt)
        {
            var P=Pose;var B=NativeHumBossLook.Bind;
            for(int h=0;h<2;h++)
            {
                int hand=h==0?NativeHumBossLook.HandL:NativeHumBossLook.HandR,limb=h==0?ArmL:ArmR;var hr=P.Rotation[hand];var hp=P.Position[hand];float s=h==0?-1:1;
                float c0=ch[h==0?HumCh.CurlL:HumCh.CurlR],sp=ch[h==0?HumCh.SpreadL:HumCh.SpreadR];bool planted=Legs.Planted[limb]&&ch[HumCh.Limb(limb,0)]<.02f;
                // Swinging, the hand gathers its fingers; planted, they settle; idle, one or two scratch.
                float swing=Legs.Swing[limb]>=0?(float)Math.Sin(Math.PI*Legs.Swing[limb]):0;
                for(int f=0;f<NativeHumBossLook.Fingers;f++)
                {
                    float goal=c0+.5f*swing;
                    if(planted&&Scratch>0){float k=BossBreath.Idle(t,Seed+(uint)(f*11+h*3),60,1.2f);if(k>.35f)goal-=.16f*(k-.35f)*Scratch*(1+(float)Math.Sin(t*19+f*2.1f+h));}
                    float c=curl[h*NativeHumBossLook.Fingers+f].Step(goal,dt);
                    float yawHere=NativeHumBossLook.FingerYawL[f]*Deg*(h==0?1:-1);
                    // The finger's own hinge (level, across it) and its fan (about the hand's up axis).
                    var hinge=new Vector3((float)Math.Cos(yawHere),0,-(float)Math.Sin(yawHere));
                    var fan=BossMath.AxisAngle(Vector3.UnitY,(f-1.5f)*(h==0?-1:1)*-.22f*sp);
                    var r=Quaternion.Normalize(hr*fan);int parent=hand;var ppos=hp;
                    for(int k=0;k<NativeHumBossLook.FingerLinks;k++)
                    {
                        int j=NativeHumBossLook.Finger(h,f,k);float bindPitch=NativeHumBossLook.FingerPitch[f,k],prevBind=k>0?NativeHumBossLook.FingerPitch[f,k-1]:0;
                        // Straight (c -1): each link's total turn cancels its bind pitch. Claw (c 1): each closes further.
                        float straight=(k==0?bindPitch:bindPitch-prevBind);float claw=(k==0?46:k==1?24:22);
                        float turn=c>=0?claw*c:straight*-c;
                        var at=ppos+BossMath.Rotate(k==0?r:P.Rotation[parent],NativeHumBossLook.Rest[j]);
                        r=Quaternion.Normalize(r*BossMath.AxisAngle(hinge,turn*Deg));P.Set(j,at,r);parent=j;ppos=at;
                    }
                }
            }
        }
        // A chain's links: turned from their bind direction by parallel transport, so a strip hanging along itself never flips about its own axis.
        private void Put(BossChain c,int first,int links)
        {
            var P=Pose;var B=NativeHumBossLook.Bind;
            for(int k=0;k<links&&k<c.Rotation.Length;k++)
            {
                int j=first+k;var dir=c.Point[k+1]-c.Point[k];
                if(!linked[j]){var bind=k+1<links?B[j+1]-B[j]:B[j]-B[j-1];if(k==0&&links==1)bind=-Vector3.UnitY;bindDir[j]=bind;linkRot[j]=BossMath.FromTo(bind,dir);linked[j]=true;}
                else{var was=BossMath.Rotate(linkRot[j],bindDir[j]);linkRot[j]=Quaternion.Normalize(BossMath.FromTo(was,dir)*linkRot[j]);}
                P.Set(j,c.Point[k],linkRot[j]);
            }
        }
        /// <summary>Whether limb i is planted by the gait and not taken by a channel (the planted-limb checks measure these).</summary>
        public bool PlantedFree(int i)=>Legs.Planted[i]&&ch[HumCh.Limb(i,HumCh.W)]<1e-4f;
        public Vector3 Limb(int i)=>Pose.Position[i==0?NativeHumBossLook.FootL:i==1?NativeHumBossLook.FootR:i==2?NativeHumBossLook.HandL:NativeHumBossLook.HandR];
    }

    /// <summary>
    /// The arrival's presentation path (doc section 3, timed on <see cref="HumBossBeats"/>): behind the wall through the dark rows, the hand through the tear, then it walks out through the wall without opening it, along its facing,
    /// to the authority's spawn by the hold's end. Pure functions of the age.
    /// </summary>
    public static class HumBossArrival
    {
        /// <summary>How far behind the wall it waits (m). Its root's z in its end pose's frame = -(wall distance + this) until it walks.</summary>
        public const float BehindWall=1.5f;
        public static void Root(float a,in HumBossBeats b,Vector3 end,float endYaw,out Vector3 pos,out Vector3 vel,out float yaw)
        {
            Path(a,b,end,endYaw,out pos);Path(a+1/120f,b,end,endYaw,out var q);vel=(q-pos)*120;yaw=endYaw;
        }
        private static void Path(float a,in HumBossBeats b,Vector3 end,float endYaw,out Vector3 p)
        {
            float start=-(NativeHumArena.IntroWallBehind+BehindWall);var f=BossMath.Forward(endYaw);
            // Two slow stretches: out through the wall, then the last steps to its mark on the song's build.
            float walk=BossMath.Smoother((a-b.WalkOut)/(b.Straighten-b.WalkOut));
            float z=start*(1-walk);p=end+f*z;
        }
        /// <summary>The hand coming through the tear, in the root's frame at age `a`: the tips reaching the paper and pressing it out, then through, the wrist following up to the wall.</summary>
        public static Vector3 HandThrough(float a,in HumBossBeats b,Vector3 root,float yaw)
        {
            float k=BossMath.Smoother((a-b.Bulge)/(b.Fingers-b.Bulge)),through=BossMath.Smoother((a-b.Fingers)/(b.TearOpen-b.Fingers+.35f));
            float wall=BehindWall;// the wall, in the root's frame while it waits behind it
            float grip=Grip(a,b);float z=wall-1.42f+.14f*k+1.24f*through+.2f*grip;float x=TearX+.03f*(float)Math.Sin(a*.9f),y=TearY-.02f+.05f*through-.16f*grip;
            return new Vector3(x,y,z);
        }
        /// <summary>How far the hand has closed on the tear's edge (0..1).</summary>
        public static float Grip(float a,in HumBossBeats b)=>BossMath.Smoother((a-b.TearOpen+.05f)/.42f);
        /// <summary>Where the tear is on the wall (in the end pose's frame, x its right; y up).</summary>
        public const float TearX=.86f,TearY=1.62f;
    }
}

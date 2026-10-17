using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE 6:47's motion (docs/EXPEDITION-BOSS-CENTRAL-STATION.md section 7 and "Presentation build"), built on the shared boss
    /// motion library (docs/BOSS-MOTION.md) the way its reference rigs are: presentation only, a pure reader of the boss view and the
    /// presentation clock that writes a <see cref="BossPose"/> (one joint per <see cref="NativeStationBossLook"/> joint: world
    /// position and rotation, geometry hanging off it).
    /// <list type="bullet">
    /// <item>Root: <see cref="BossRootFollow"/> (the authority's root through its snapshots on a guest, or a continuous target), a
    /// heavy turn and lean; during the arrival a presentation path from the west tunnel to the authority's spawn pose.</item>
    /// <item>Gait: <see cref="BossGait"/> with six legs in two alternating tripods that morph into a front-mid-rear bound on the charge;
    /// planted hands are locked (the gait never moves them) and solved by two-bone IK; every body move (crouch, rear-up, slam,
    /// stagger) goes through the gait's ExtraOffset/ExtraRotation so planted hands stay planted.</item>
    /// <item>Actions: one <see cref="BossActionPlayer"/> for the moves (<see cref="StationBossMoves"/>: contact on the record's
    /// `hitAt`), one for the phase changes, one for the staggers; the arrival and the death are their own tracks.</item>
    /// <item>Secondary: <see cref="BossChain"/>s for the coupling chain, the torn cables, the PA horns' stalks, the window arms and
    /// the coat tails; second-order dynamics for the Stationmaster's chest and hat, the pantograph, the grab arms and the body's
    /// heave; <see cref="BossLookAt"/> for his head and the headlamp's aim; breath, the Heart's pulse, <see cref="BossFlinch"/> and
    /// <see cref="BossStagger"/> on top; <see cref="BossInertializer"/> so a jump (a teleport, a late join, the arrival's hand-off)
    /// never pops.</item>
    /// </list>
    /// Fixed fields and arrays: no allocation per frame. Nothing here is read by the authority: hit timing, hitboxes, damage,
    /// cooldowns, movement and networking are unchanged (the strikes only align their contact frame with the record's hit).
    /// </summary>
    public sealed class StationBossRig
    {
        public const int Count=NativeStationBossLook.Count;
        private const float Deg=BossMath.Deg;
        /// <summary>The pose written each frame (and the inertialized one actually shown).</summary>
        public readonly BossPose Pose=new BossPose(Count),Shown=new BossPose(Count);
        private readonly BossPose last=new BossPose(Count);
        public readonly BossRootFollow Follow=new BossRootFollow{PositionHalfLife=.12f,MaxExtrapolate=.3f,TurnHalfLife=.14f,MaxTurnRate=NativeStationBossMotion.TurnRate*Deg,MaxTurnAccel=180*Deg,
            BankScale=.6f,MaxBank=4*Deg,PitchScale=.5f,SpeedPitch=.004f,MaxPitch=3*Deg,LeanFrequency=1.2f,LeanDamping=.5f};
        public readonly BossGaitDef Gait=MakeGait();public readonly BossGait Legs;
        public readonly BossActionPlayer Actions=new BossActionPlayer(),Phases=new BossActionPlayer(),Staggers=new BossActionPlayer();
        public readonly BossLookAt Look=new BossLookAt{MaxYaw=1.1f,MaxUp=.5f,MaxDown=.7f,HalfLife=.18f,MaxRate=3.5f};
        public readonly BossLookAt LampAim=new BossLookAt{MaxYaw=.45f,MaxUp=.15f,MaxDown=.4f,HalfLife=.1f,MaxRate=3f};
        public readonly BossFlinch Flinch=new BossFlinch{PitchGain=.06f,BankGain=.05f,ShiftGain=.08f,Rise=.08f,Wobble=.3f,WobbleHz=1.6f};
        public readonly BossStagger Stagger=new BossStagger{Throw=.1f,Shift=.6f,PitchGain=.14f,BankGain=.12f,Crouch=.22f};
        public readonly BossInertializer Inertia=new BossInertializer(Count);
        public readonly BossChain Coupling=new BossChain(new BossChainDef{Count=NativeStationBossModel.ChainLinks+1,Length=.34f,Rest=Vector3.Normalize(new Vector3(0,-1,-.12f)),Stiffness=0,TipStiffness=0,
            Damping=.35f,Gravity=NativeStationBossMotion.ChainGravity,MinStretch=.95f,Radius=.05f,Friction=5,Iterations=NativeStationBossMotion.ChainIterations});
        public readonly BossChain CableA=new BossChain(CableDef()),CableB=new BossChain(CableDef());
        public readonly BossChain HornL=new BossChain(HornDef()),HornR=new BossChain(HornDef());
        public readonly BossChain TailL=new BossChain(TailDef()),TailR=new BossChain(TailDef());
        public readonly BossChain[] Arms={new BossChain(ArmDef(-1)),new BossChain(ArmDef(-1)),new BossChain(ArmDef(1)),new BossChain(ArmDef(1))};
        private static BossChainDef CableDef()=>new BossChainDef{Count=NativeStationBossModel.CableLinks+1,Length=.62f,Rest=Vector3.Normalize(new Vector3(0,-.8f,-.6f)),Stiffness=.9f,TipStiffness=.1f,Damping=.55f,Gravity=9.8f,Radius=.04f,Friction=4};
        private static BossChainDef HornDef()=>new BossChainDef{Count=2,Length=.55f,Rest=Vector3.Normalize(new Vector3(0,1,.12f)),Stiffness=26,TipStiffness=26,Damping=2.2f,Gravity=2.5f,Radius=.05f};
        private static BossChainDef TailDef()=>new BossChainDef{Count=NativeStationBossLook.TailLinks+1,Length=NativeStationBossLook.TailLength,Rest=Vector3.Normalize(new Vector3(0,-.4f,-.92f)),Pole=Vector3.UnitY,
            Stiffness=2.4f,TipStiffness=.5f,Damping=.9f,Gravity=9.8f,Radius=.025f,Friction=3};
        private static BossChainDef ArmDef(int side)=>new BossChainDef{Count=3,Length=.56f,Rest=Vector3.Normalize(new Vector3(side*.85f,-.5f,0)),Stiffness=5,TipStiffness=1.4f,Damping=1.1f,Gravity=9.8f,Radius=.06f,Friction=4};
        private readonly RoofGround roof=new RoofGround();
        private BossSecondOrder3 chestLag,hatLag;private BossSecondOrder panto,grabL,grabR,heave,roofPeel,rise;private bool configured;
        // ---- state ----
        private float time=float.NaN,wheel,lastYaw;private Vector3 lastVel;private bool started;
        private float seenSpawned=float.NaN,seenAt=float.NaN;private int actionSide=1,actionSideId;
        private float lampYaw,lampPitch;
        private bool hatLoose,pantoLoose;private Vector3 hatFrom,pantoFrom,hatFromVel;private Quaternion hatFromRot,pantoFromRot,pantoHeadFromRot;private Vector3 pantoHeadFrom;private float hatAt,pantoAt;
        private readonly float[] ch=new float[StationBossCh.Count],sample=new float[StationBossCh.Count],sample2=new float[StationBossCh.Count];
        private readonly Vector3[] legTarget=new Vector3[6],legPole=new Vector3[6];private readonly float[] legWeight=new float[6],legFlat=new float[6],slap=new float[6];
        private readonly Quaternion[] localRot=new Quaternion[Count];private readonly Vector3[] localMove=new Vector3[Count];
        // ---- outputs the effects read ----
        /// <summary>Whether the boss is shown (a view or the preview) and whether its arrival is playing (and how far in, s).</summary>
        public bool Visible,Arriving;public float ArrivalAge=float.NaN;
        /// <summary>The root as presented, its yaw (rad), ground speed; the hands' planted flags and this frame's landings.</summary>
        public Vector3 Root;public float Yaw,Speed;public int Landed;
        /// <summary>The headlamp's world position and beam direction; the lamp's lock (Departure's lane locked).</summary>
        public Vector3 LampPos,LampDir;public bool LampLocked;
        /// <summary>The action now (its sample) and the side a sided move plays to (+1 right, -1 left).</summary>
        public BossActionSample Act;public int ActMove=-1,ActSide=1;
        /// <summary>The last channel values (the effects read the doors, the pantograph, the horns, the lamp's flare).</summary>
        public float[] Channels=>ch;
        public float PantoRaise,GrabOutL,GrabOutR,RoofOpen,HeartPulse;
        public uint Seed=0x647u;

        public StationBossRig(){Legs=new BossGait(Gait);for(int i=0;i<Count;i++)localRot[i]=Quaternion.Identity;}
        private static BossGaitDef MakeGait()
        {
            var legs=new BossLegDef[6];
            for(int i=0;i<6;i++)
            {
                var hip=NativeStationBossModel.Rest[NativeStationBossModel.Hip(i)];var home=NativeStationBossModel.Stance(i);
                legs[i]=new BossLegDef{Hip=hip,Home=new Vector3(home.X,0,home.Z),Pole=StationBossCh.DefaultPole(i),Upper=NativeStationBossModel.Thigh(i),Lower=NativeStationBossModel.Shin(i),
                    Phase=Array.IndexOf(NativeStationBossMotion.TripodA,i)>=0?0:.5f,Side=NativeStationBossModel.Side(i),Ankle=NativeStationBossModel.PalmLift};
            }
            return new BossGaitDef{Legs=legs,WalkSpeed=NativeStationBossMotion.WalkSpeed,RunSpeed=14,WalkStride=NativeStationBossMotion.Stride,RunStride=6.6f,WalkDuty=NativeStationBossMotion.DutyWalk,RunDuty=NativeStationBossMotion.DutyCharge,
                WalkLift=NativeStationBossMotion.StepHeight,RunLift=NativeStationBossMotion.ChargeStepHeight,MinFrequency=.55f,MinSwing=.22f,MaxSwing=.9f,StartSpeed=.25f,StopSpeed=.1f,
                SettleDistance=.35f,SettleSwing=.36f,ReachMargin=.3f,MaxSink=.05f,BodyHeight=NativeStationBossModel.BodyLift,Bob=.05f,RunBob=.16f,Sway=.04f,Roll=2.5f*Deg,Twist=.03f,LandDip=.16f,
                SupportFrequency=1.15f,SupportDamping=.55f,LiftPeak=.42f,ToeOff=.25f,HeelStrike=.12f,FloorFollow=.5f,TurnRadius=4.5f};
        }
        /// <summary>The tripods' phase offsets and the bound's (front, mid, rear pairs a third apart): the gait morphs by the run blend.</summary>
        private static readonly float[] Tripod={0,.5f,.5f,0,0,.5f},Bound={0,0,.33f,.33f,.66f,.66f};

        /// <summary>A blow landed (its time, the push from the attacker into the boss, its size ~1 heavy): the body flinches, the
        /// windows flicker.</summary>
        public void Hit(double at,Vector3 push,float size){Flinch.Add(at,push,size,Follow.Yaw);LastHit=(float)at;}
        public float LastHit=float.NaN;

        /// <summary>
        /// Poses the boss at `t` (the arena's presentation clock, the view's timeline) after a frame of `dt`. `v` is the record;
        /// `velocity` the root's ground velocity when the caller has it (the preview's script; NaN-free zero otherwise: the follow
        /// derives it); `externalRoot` true when the caller has already stepped <see cref="Follow"/> from snapshots (a guest).
        /// `target` is who the Stationmaster and the headlamp look at.
        /// </summary>
        public void Update(float t,float dt,in StationBossView v,IBossGround ground,Vector3 velocity,bool externalRoot,bool hasTarget,Vector3 target)
        {
            Visible=v.live;if(!v.live){started=false;time=float.NaN;return;}
            if(!configured)Configure();dt=BossMath.Clamp(dt,0,.1f);
            var beats=NativeStationBossBeats.Current;
            // ---- the arrival (presentation, from `spawned`), late joiners skip it ----
            if(!seenSpawned.Equals(v.spawned)){seenSpawned=v.spawned;seenAt=t;}
            float age=StationBossView.At(v.spawned)?t-v.spawned:float.PositiveInfinity;
            Arriving=StationBossView.At(v.spawned)&&seenAt-v.spawned<=LateJoin&&age<beats.Hold&&age>=-1;ArrivalAge=Arriving?age:float.NaN;
            // A short tail (the song's drop on the hold's end): the root runs on to the live root for Tail after the hold.
            bool tail=!Arriving&&beats.Hold-beats.Drop<StationBossArrival.ShortTail&&StationBossView.At(v.spawned)&&seenAt-v.spawned<=LateJoin&&age>=beats.Hold&&age<beats.Hold+StationBossArrival.Tail;
            float vyaw=v.yaw*Deg;var vroot=new Vector3(v.x,v.y,v.z);
            if(Arriving||tail)
            {
                StationBossArrival.Root(age,beats,vroot,vyaw,out var ap,out var av,out float ay);
                Follow.MaxTurnRate=2.4f;Follow.MaxTurnAccel=9;Follow.Follow(ap,av,ay,dt,!started);
            }
            else if(!externalRoot)
            {
                Follow.MaxTurnRate=(v.phase>=3?NativeStationBossMotion.TurnRateTerminus:NativeStationBossMotion.TurnRate)*Deg;Follow.MaxTurnAccel=180*Deg;
                if(v.action=="departure")Follow.MaxTurnRate*=1.6f;
                Follow.Follow(vroot,velocity,vyaw,dt,!started);
            }
            bool first=!started;started=true;time=t;
            float yaw=Follow.Yaw;var facing=BossMath.Yaw(yaw);var fwd=BossMath.Forward(yaw);var right=BossMath.Right(yaw);Root=Follow.Position;Yaw=yaw;Speed=Follow.Speed;
            // ---- the record's move, phase change and stagger on their players ----
            int move=Arriving?-1:v.Move;
            if(move>=0&&v.actionId!=0&&StationBossView.At(v.actionAt))
            {
                if(actionSideId!=v.actionId){actionSideId=v.actionId;actionSide=SideOf(move,v,hasTarget,target);}
                float hit=StationBossView.At(v.hitAt)?v.hitAt-v.actionAt:float.NaN,dur=StationBossView.At(v.duration)?v.duration:float.NaN;
                Actions.Play(StationBossMoves.Defs[move],v.actionId,v.actionAt,hit,dur);if(StationBossView.At(v.cancelAt))Actions.Cancel(v.cancelAt);
            }
            if(StationBossView.At(v.phaseAt)&&v.phase>=2)
            {
                bool smash=v.phase==2;float contact=smash?(StationBossView.At(v.barrierDownAt)?v.barrierDownAt-v.phaseAt:2.2f):(StationBossView.At(v.boardDownAt)?v.boardDownAt-v.phaseAt:1.65f);
                contact=BossMath.Clamp(contact,.8f,NativeStationBoss.PhaseChangeSeconds-.4f);
                Phases.Play(smash?StationBossMoves.Smash:StationBossMoves.Tear,v.phase,v.phaseAt,contact,Math.Max(NativeStationBoss.PhaseChangeSeconds,contact+1.2f));
            }
            if(StationBossView.At(v.staggerAt)&&v.staggerSeconds>0)
            {
                bool part=v.staggerCause=="part";int key=v.staggerAt.GetHashCode();
                Staggers.Play(part?StationBossMoves.Flinch:StationBossMoves.Stagger,key,v.staggerAt,part?.1f:.14f,Math.Max(.6f,v.staggerSeconds));
                if(!part&&v.staggerCause!="phase"){var push=new Vector3(v.staggerPushX,0,v.staggerPushZ);if(push.LengthSquared()<1e-6f)push=-fwd;Stagger.Start(v.staggerAt,push,v.staggerCause=="express"?1.25f:.8f,v.staggerSeconds,yaw);}
            }
            Actions.Sample(t);Phases.Sample(t);Staggers.Sample(t);
            Act=Actions.Current;ActMove=Act.Active&&ReferenceEquals(Actions.CurrentDef,move>=0?StationBossMoves.Defs[move]:null)?move:-1;ActSide=actionSide;
            // ---- the channels: neutral, then each layer in priority (moves, phase changes, staggers, arrival, death) ----
            Array.Copy(StationBossCh.Neutral,ch,ch.Length);
            Layer(Actions,MoveKeysFn,true);Layer(Phases,PhaseKeysFn,false);Layer(Staggers,StaggerKeysFn,false);
            if(Arriving)ArrivalLayer(age,beats);
            if(v.Dead(t))DeathLayer(t-v.diedAt);
            // ---- persistent states: the roof (open from roofOpenAt, or already open in TERMINUS), the pantograph ----
            bool open=v.RoofOpen(t)||(v.phase>=3&&!StationBossView.At(v.roofOpenAt));float peelGoal=open?1:0;
            RoofOpen=roofPeel.Step(peelGoal,dt);float rose=rise.Step(open?.42f:0,dt);
            ch[StationBossCh.Roof]+= -115*RoofOpen;ch[StationBossCh.SRise]+=rose;
            bool pantoBroken=v.PantoBroken(t);PantoRaise=panto.Step(pantoBroken?0:ch[StationBossCh.Panto],dt);
            GrabOutL=grabL.Step(ch[StationBossCh.GrabL]*HoldEnvelope(v,t),dt);GrabOutR=grabR.Step(ch[StationBossCh.GrabR]*HoldEnvelope(v,t),dt);
            // ---- additive life: the chime's nods, the horn's shudder, Live Rail's tremble, the bell, breath, the flinch ----
            Additive(v,t);
            // ---- the body through the gait (planted hands stay planted) ----
            Stagger.Sample(t,out var sShift,out float sPitch,out float sBank,out float sCrouch,out _);
            Flinch.Sample(t,out float fPitch,out float fBank,out var fShift);
            var lean=BossMath.Euler(ch[StationBossCh.BP]*Deg,ch[StationBossCh.BW]*Deg,ch[StationBossCh.BR]*Deg);
            float lead=BossMath.Clamp(Follow.YawRate*.12f,-8*Deg,8*Deg);
            float heaveY=heave.Step(ch[StationBossCh.BY],dt);
            var offset=BossMath.Rotate(facing,new Vector3(ch[StationBossCh.BX],heaveY-sCrouch,ch[StationBossCh.BZ]))+BossMath.Rotate(facing,sShift+fShift);
            Legs.ExtraOffset=offset;Legs.ExtraRotation=Quaternion.Normalize(BossMath.AxisAngle(Vector3.UnitY,lead)*lean*BossMath.AxisAngle(Vector3.UnitX,sPitch+fPitch)*BossMath.AxisAngle(Vector3.UnitZ,-(sBank+fBank)));
            MorphGait(v,t);for(int i=0;i<6;i++)Legs.Free[i]=ch[StationBossCh.Leg(i,StationBossCh.W)]>=.98f;
            if(first||Follow.Jumped){Legs.Reset(Root,yaw,ground);Array.Clear(linked,0,linked.Length);}
            Legs.Step(Root,yaw,Follow.YawRate,Follow.Velocity,Follow.Body,dt,ground);Landed=Legs.Landed;
            var bodyPos=Legs.BodyPosition;var bodyRot=Legs.BodyRotation;
            roof.Set(bodyPos,bodyRot,RoofOpen);
            // ---- FK: every joint's local placement from the channels, then the world pose ----
            SetLocals(v,t,dt);
            Pose.RootPosition=Root;Pose.RootRotation=facing;
            Pose.Set(NativeStationBossModel.Root,Root,facing);Pose.Set(NativeStationBossModel.Body,bodyPos,bodyRot);
            for(int j=2;j<Count;j++)
            {
                if(j>=NativeStationBossModel.HipFirst&&j<NativeStationBossModel.FootFirst+6)continue;
                int p=NativeStationBossLook.Parent[j];if(p<0)continue;var pp=Pose.Position[p];var pr=Pose.Rotation[p];
                Pose.Set(j,pp+BossMath.Rotate(pr,NativeStationBossLook.Rest[j]+localMove[j]),Quaternion.Normalize(pr*localRot[j]));
            }
            SolveLegs(t,dt,ground,facing,yaw);
            Secondary(t,dt,ground,bodyPos,bodyRot,hasTarget,target,v);
            Loose(t,v);
            // ---- the pop killer: a jump in the source (a teleport, the arrival's hand-off, a late join) decays on springs ----
            if(!first&&(Follow.Jumped||ArrivalEnded(age,beats)))Inertia.Transition(Shown,Pose);
            Inertia.Apply(Pose,.16f,dt,Shown);last.CopyFrom(Shown);
        }
        /// <summary>A late joiner (more than this long after the arrival, s) sees the boss in place.</summary>
        public const float LateJoin=3;
        private bool arrivedBefore;
        private bool ArrivalEnded(float age,in StationBossBeats beats){bool now=Arriving;bool ended=arrivedBefore&&!now;arrivedBefore=now;return ended;}

        private void Configure()
        {
            var m=NativeStationBossMotion.Springs;float F(string id,bool hz){foreach(var c in m)if(c.Id==id)return hz?c.Hz:c.Damping;return 1;}
            chestLag.Configure(F("stationmaster",true),F("stationmaster",false),0);hatLag.Configure(F("hat",true),F("hat",false),0);
            panto.Configure(F("pantograph",true),F("pantograph",false),0);grabL.Configure(F("grab-arms",true),F("grab-arms",false),0);grabR.Configure(F("grab-arms",true),F("grab-arms",false),0);
            heave.Configure(F("body-heave",true),F("body-heave",false),1.6f);roofPeel.Configure(1.6f,.32f,0);rise.Configure(1.8f,.45f,0);configured=true;
        }
        // ---- layers ----
        private static readonly Func<BossActionDef,StationBossSnap[]> MoveKeysFn=MoveKeys,PhaseKeysFn=PhaseKeys,StaggerKeysFn=StaggerKeysOf;
        private static StationBossSnap[] MoveKeys(BossActionDef d){for(int i=0;i<StationBossMoves.Defs.Length;i++)if(ReferenceEquals(StationBossMoves.Defs[i],d))return StationBossMoves.Keys[i];return null;}
        private static StationBossSnap[] PhaseKeys(BossActionDef d)=>ReferenceEquals(d,StationBossMoves.Smash)?StationBossMoves.SmashKeys:ReferenceEquals(d,StationBossMoves.Tear)?StationBossMoves.TearKeys:null;
        private static StationBossSnap[] StaggerKeysOf(BossActionDef d)=>ReferenceEquals(d,StationBossMoves.Flinch)?StationBossMoves.FlinchKeys:StationBossMoves.StaggerKeys;
        private void Layer(BossActionPlayer p,Func<BossActionDef,StationBossSnap[]> keysOf,bool sided)
        {
            var a=p.Current;var b=p.Previous;float wa=a.Active?a.Weight:0,wb=b.Active?b.Weight:0;if(wa+wb<=1e-5f)return;
            var ka=wa>0?keysOf(p.CurrentDef):null;var kb=wb>0?keysOf(p.PreviousDef):null;if(ka==null)wa=0;if(kb==null)wb=0;if(wa+wb<=1e-5f)return;
            bool mirror=sided&&ka!=null&&Mirrored(p.CurrentDef);
            if(wa>0)StationBossMoves.Sample(ka,a.Progress,mirror,sample);if(wb>0)StationBossMoves.Sample(kb,b.Progress,sided&&Mirrored(p.PreviousDef),sample2);
            float w=Math.Min(1,wa+wb),na=wa/(wa+wb),nb=wb/(wa+wb);
            for(int c=0;c<ch.Length;c++){float s=(wa>0?sample[c]*na:0)+(wb>0?sample2[c]*nb:0);ch[c]+=(s-ch[c])*w;}
        }
        private bool Mirrored(BossActionDef d){for(int i=0;i<StationBossMoves.Defs.Length;i++)if(ReferenceEquals(StationBossMoves.Defs[i],d)){int s=StationBossMoves.Side[i];return s!=0&&s!=actionSide;}return false;}
        private int SideOf(int move,in StationBossView v,bool hasTarget,Vector3 target)
        {
            if(StationBossMoves.Side[move]==0)return 1;if(NativeStationBoss.Moves[move].Id=="doors"&&v.holdSide!=0)return v.holdSide;
            if(!hasTarget)return StationBossMoves.Side[move];var d=target-Follow.Position;return Vector3.Dot(d,BossMath.Right(Follow.Yaw))>=0?1:-1;
        }
        // All Aboard's arms withdraw once the hold ends (holdUntil moves forward on an early release).
        private static float HoldEnvelope(in StationBossView v,float t)
        {
            if(v.action!="doors"||!StationBossView.At(v.holdUntil)||!StationBossView.At(v.hitAt)||t<v.hitAt+.25f)return 1;
            return 1-BossMath.Smooth((t-v.holdUntil)/.35f)*.85f;
        }
        private void Blend(StationBossSnap s,float w){if(w<=0)return;w=Math.Min(1,w);var x=s.V;for(int c=0;c<ch.Length;c++)ch[c]+=(x[c]-ch[c])*w;}
        private void Blend2(StationBossSnap a,StationBossSnap b,float k,float w)
        {
            if(w<=0)return;w=Math.Min(1,w);var x=a.V;var y=b.V;
            for(int c=0;c<ch.Length;c++){float s=x[c]+(y[c]-x[c])*k;ch[c]+=(s-ch[c])*w;}
        }
        /// <summary>The arrival's pose over its beats: rolling in folded, the legs unfolding at the last accent (rear first), the
        /// rear through the gate on the drop, down onto its hands, the Stationmaster rising and raising the watch.</summary>
        private void ArrivalLayer(float a,in StationBossBeats b)
        {
            // With the song's drop on the hold's end (a short tail) the legs unfold quicker, before the run gathers speed.
            bool brief=b.Hold-b.Drop<StationBossArrival.ShortTail;
            float span=b.Drop-b.Accent2,roll=1-BossMath.Smoother((a-b.Accent2)/(span*(brief?.5f:.7f)));
            var rv=StationBossMoves.Rolling.V;
            // Folded on the rails until the fourth accent; the legs let go of the folded pose on their own beats (rear pair first)
            // while the body rises out of the pit.
            for(int c=0;c<StationBossCh.LegBase;c++)ch[c]+=(rv[c]-ch[c])*roll;
            for(int c=StationBossCh.SRise;c<StationBossCh.Count;c++)ch[c]+=(rv[c]-ch[c])*roll;
            for(int leg=0;leg<6;leg++)
            {
                int row=NativeStationBossModel.Row(leg),b0=StationBossCh.Leg(leg,0);float w=brief?1-BossMath.Smoother((a-b.Accent2-(2-row)*.05f)/(span*.3f)):1-BossMath.Smoother((a-b.Accent2-(2-row)*.09f)/(span*.5f));
                for(int f=0;f<StationBossCh.LegStride;f++)ch[b0+f]+=(rv[b0+f]-ch[b0+f])*w;
            }
            // Climbing out it pitches up; the drop rears it through the gate (up over .35 s), holds while the gate flies, and
            // brings it down onto its hands.
            ch[StationBossCh.BP]+=-8*BossMath.Smooth(1-roll)*(1-BossMath.Smooth((a-b.Drop+.3f)/.3f));
            // With the short tail it comes down while it runs on, and the Stationmaster rises as it lands; the watch is raised
            // only when there is time to hold it up.
            float up=BossMath.Smoother((a-(b.Drop-.35f))/.35f),down=brief?BossMath.Smoother((a-Math.Min(b.Drop+.6f,b.Hold-.45f))/.45f):BossMath.Smoother((a-(b.Drop+.6f))/.7f);
            Blend(StationBossMoves.Arrival,up*(1-down));
            // The Stationmaster stays down in the hatch until it has landed, then rises and raises the watch to the party.
            float stand=brief?BossMath.Smoother((a-b.Drop)/Math.Max(.3f,b.Hold-b.Drop)):BossMath.Smoother((a-(b.Drop+.8f))/Math.Max(.4f,(b.Hold-b.Drop-.8f)*.6f));
            ch[StationBossCh.SRise]=-.32f*(1-stand);if(!brief)Blend(StationBossMoves.Risen,stand*(1-BossMath.Smooth((a-(b.Hold-.6f))/.6f)));
            ch[StationBossCh.Lamp]=Math.Max(ch[StationBossCh.Lamp],1.4f+.6f*up*(1-down));
        }
        /// <summary>The death (inside the 4 s victory hold): the legs give rear to front, the body drops onto its wheels with a heave
        /// overshoot, the Stationmaster slumps late.</summary>
        private static readonly int[] DeathOrder={4,5,2,3,0,1},DeathBody={StationBossCh.BX,StationBossCh.BY,StationBossCh.BZ,StationBossCh.BP,StationBossCh.BW,StationBossCh.BR,StationBossCh.ChainLift};
        private void DeathLayer(float d)
        {
            var dv=StationBossMoves.Death.V;
            for(int k=0;k<6;k++)
            {
                int leg=DeathOrder[k];float w=BossMath.Smoother((d-k*.15f)/.38f);int b=StationBossCh.Leg(leg,0);
                for(int f=0;f<StationBossCh.LegStride;f++)ch[b+f]+=(dv[b+f]-ch[b+f])*w;
            }
            float drop=BossMath.Smoother((d-.25f)/.85f);
            foreach(int c in DeathBody)ch[c]+=(dv[c]-ch[c])*drop;
            float slump=BossMath.Smoother((d-2.5f)/.8f);
            for(int c=StationBossCh.SChP;c<=StationBossCh.SLfP;c++)ch[c]+=(dv[c]-ch[c])*slump;
            ch[StationBossCh.Look]*=1-BossMath.Smooth(d/1.2f);
        }
        private void Additive(in StationBossView v,float t)
        {
            // The PA chime's notes (Departure, Terminus): the nose nods on each, the clocks tick with them (the stage hook).
            if(ActMove>=0)
            {
                var m=NativeStationBoss.Moves[ActMove];float age=Act.Age;float scale=m.Anticipation>0?Act.HitAt/m.Anticipation:1;
                if(m.Chime>0)for(int n=0;n<NativeStationBoss.ChimeNotes;n++){float k=age-m.Note(n)*scale;if(k>=0&&k<.32f)ch[StationBossCh.BP]+=2.2f*(float)Math.Sin(Math.PI*k/.32f)*(m.Id=="departure"?1:.5f);}
                // The horn shakes the whole carriage (0.2 s at 3 Hz) at Alteration's hit.
                if(m.Id=="alteration"){float k=age-Act.HitAt;if(k>=0&&k<.6f){float e=(float)Math.Exp(-k*5);ch[StationBossCh.BR]+=1.4f*e*(float)Math.Sin(BossMath.Tau*3*k);ch[StationBossCh.BP]+=.8f*e*(float)Math.Sin(BossMath.Tau*3*k+1);}}
                // Live Rail: the body trembles on a 12 Hz noise through the strike, the Stationmaster's spread arms shake.
                if(m.Id=="live-rail"&&Act.Struck&&Act.Age<Act.Duration-.9f){float k=(float)Math.Min(1,(Act.Age-Act.HitAt)/.3f);
                    ch[StationBossCh.BR]+=.6f*k*BossBreath.Idle(t,Seed,40,12);ch[StationBossCh.BP]+=.4f*k*BossBreath.Idle(t,Seed,41,12);ch[StationBossCh.SRaY]+=6*k*BossBreath.Idle(t,Seed,42,9);ch[StationBossCh.SLaY]+=6*k*BossBreath.Idle(t,Seed,43,9);}
                // All Aboard and Next Stop: the leaves shudder at 2 Hz through the telegraph.
                float sh=ch[StationBossCh.Shudder];if(sh>0){float w=.03f*sh*(float)Math.Sin(BossMath.Tau*2*t)+.012f*sh*BossBreath.Idle(t,Seed,44,14);ch[StationBossCh.DoorL]+=w;ch[StationBossCh.DoorR]+=w;}
            }
            // The TERMINUS change: the Stationmaster rings the bell (2.5 s from the roof's peel, or the phase change).
            float bellAt=StationBossView.At(v.roofOpenAt)?v.roofOpenAt:StationBossView.At(v.phaseAt)&&v.phase>=3?v.phaseAt+1.8f:float.NaN;
            if(StationBossView.At(bellAt)){float k=t-bellAt;if(k>=0&&k<2.6f){float e=BossMath.Smooth(k/.2f)*(1-BossMath.Smooth((k-2.1f)/.5f));ch[StationBossCh.SLaP]+=(-120-ch[StationBossCh.SLaP])*e;ch[StationBossCh.SLaR]+=(-20-ch[StationBossCh.SLaR])*e;ch[StationBossCh.SLfP]+=(-40+25*(float)Math.Sin(BossMath.Tau*4.2f*k)-ch[StationBossCh.SLfP])*e;}}
            // Breath (0.25 Hz) and idle life on the Stationmaster; the watch checked now and then.
            float br=BossBreath.Sample(t,.25f,Seed);ch[StationBossCh.SChP]+=1.6f*br;ch[StationBossCh.SHdP]-=.8f*br;
            ch[StationBossCh.SHdY]+=4*BossBreath.Idle(t,Seed,1,.18f);ch[StationBossCh.SChR]+=1.5f*BossBreath.Idle(t,Seed,2,.15f);
            float check=BossBreath.Idle(t,Seed,3,.07f);if(check>.55f&&ActMove<0){float k=BossMath.Smooth((check-.55f)/.2f);ch[StationBossCh.SRaP]+=(-95-ch[StationBossCh.SRaP])*k;ch[StationBossCh.SRfP]+=(-110-ch[StationBossCh.SRfP])*k;ch[StationBossCh.SHdP]+=14*k;}
            // The Heart's pulse (faster each phase) and the boss's own idle sway.
            int ph=Math.Max(1,Math.Min(3,v.phase));HeartPulse=Math.Max(0,BossBreath.Sample(t,NativeStationBossMotion.HeartPulseHz[ph-1],Seed^7u));
            ch[StationBossCh.BR]+=.5f*BossBreath.Idle(t,Seed,5,.2f);ch[StationBossCh.BP]+=.35f*BossBreath.Idle(t,Seed,6,.17f);
        }
        private void MorphGait(in StationBossView v,float t)
        {
            float run=Legs.Run;for(int i=0;i<6;i++)Gait.Legs[i].Phase=Tripod[i]+(Bound[i]-Tripod[i])*run;
            // The arrival climbs out of the pit and over the barrier: higher steps.
            Gait.WalkLift=Arriving?.95f:NativeStationBossMotion.StepHeight;
        }
        // ---- the joints' local placements ----
        private void SetLocals(in StationBossView v,float t,float dt)
        {
            for(int i=0;i<Count;i++){localRot[i]=Quaternion.Identity;localMove[i]=Vector3.Zero;}
            // Wheels turn by distance (the follow's speed over the 0.52 m radius).
            wheel+=Follow.Speed*dt/.52f;wheel%=BossMath.Tau*64;
            for(int i=0;i<6;i++)localRot[NativeStationBossModel.Wheel(i)]=BossMath.AxisAngle(Vector3.UnitX,wheel);
            // The roof and the pantograph (rest -6/12/-6, raised -52/104/-52; broken: snapped back and gone, its loose piece falls).
            localRot[NativeStationBossModel.Roof]=BossMath.AxisAngle(Vector3.UnitX,ch[StationBossCh.Roof]*Deg);
            float r=PantoRaise;localRot[NativeStationBossModel.PantoLow]=BossMath.AxisAngle(Vector3.UnitX,(-6-46*r)*Deg);
            localRot[NativeStationBossModel.PantoHigh]=BossMath.AxisAngle(Vector3.UnitX,(12+92*r)*Deg);localRot[NativeStationBossModel.PantoHead]=BossMath.AxisAngle(Vector3.UnitX,(-6-46*r)*Deg);
            // The Stationmaster (his chest lags the body: thrown forward on the brake, back on the launch, overshooting).
            var accel=dt>0?(Follow.Velocity-lastVel)/dt:Vector3.Zero;lastVel=Follow.Velocity;float yawRate=dt>0?BossMath.WrapPi(Follow.Yaw-lastYaw)/dt:0;lastYaw=Follow.Yaw;
            float along=Vector3.Dot(accel,BossMath.Forward(Follow.Yaw)),side=Vector3.Dot(accel,BossMath.Right(Follow.Yaw));
            var lag=chestLag.Step(new Vector3(BossMath.Clamp(-along*.025f,-.45f,.45f),BossMath.Clamp(side*.02f+yawRate*.08f,-.3f,.3f),0),dt);
            localMove[NativeStationBossModel.SmHips]=new Vector3(0,ch[StationBossCh.SRise],0);
            localRot[NativeStationBossModel.SmChest]=BossMath.Euler(ch[StationBossCh.SChP]*Deg+lag.X,(ch[StationBossCh.SChY]+lookYaw*.3f)*Deg,ch[StationBossCh.SChR]*Deg-lag.Y);
            localRot[NativeStationBossModel.SmHead]=BossMath.Euler((ch[StationBossCh.SHdP]+lookPitch*.8f)*Deg-lag.X*.6f,(ch[StationBossCh.SHdY]+lookYaw*.7f)*Deg,ch[StationBossCh.SHdR]*Deg+lag.Y*.5f);
            var hat=hatLag.Step(new Vector3(lag.X*1.4f,lag.Y*1.2f,0),dt);
            localRot[NativeStationBossModel.SmHat]=BossMath.Euler(ch[StationBossCh.SHatP]*Deg+hat.X,0,ch[StationBossCh.SHatR]*Deg-hat.Y);localMove[NativeStationBossModel.SmHat]=new Vector3(0,Math.Max(0,ch[StationBossCh.SHatP])*.003f,0);
            localRot[NativeStationBossModel.SmRArm]=BossMath.Euler(ch[StationBossCh.SRaP]*Deg,ch[StationBossCh.SRaY]*Deg,ch[StationBossCh.SRaR]*Deg);localRot[NativeStationBossModel.SmRFore]=BossMath.AxisAngle(Vector3.UnitX,ch[StationBossCh.SRfP]*Deg);
            localRot[NativeStationBossModel.SmLArm]=BossMath.Euler(ch[StationBossCh.SLaP]*Deg,ch[StationBossCh.SLaY]*Deg,ch[StationBossCh.SLaR]*Deg);localRot[NativeStationBossModel.SmLFore]=BossMath.AxisAngle(Vector3.UnitX,ch[StationBossCh.SLfP]*Deg);
            // The doors slide (front leaf forward, back leaf back), the grab arms shoot out on their own dynamics.
            float dl=BossMath.Clamp(ch[StationBossCh.DoorL],0,1.05f),dr=BossMath.Clamp(ch[StationBossCh.DoorR],0,1.05f);
            localMove[NativeStationBossModel.DoorLF]=new Vector3(0,0,.6f*dl);localMove[NativeStationBossModel.DoorLB]=new Vector3(0,0,-.6f*dl);
            localMove[NativeStationBossModel.DoorRF]=new Vector3(0,0,.6f*dr);localMove[NativeStationBossModel.DoorRB]=new Vector3(0,0,-.6f*dr);
            Grab(NativeStationBossModel.GrabL0,NativeStationBossModel.GrabL1,-1,GrabOutL,t);Grab(NativeStationBossModel.GrabR0,NativeStationBossModel.GrabR1,1,GrabOutR,t);
            // The Heart breathes with its pulse.
            localMove[NativeStationBossModel.Heart]=new Vector3(0,0,-.04f*HeartPulse);
            // The lamp swivels toward its target (locked once Departure's lane is).
            localRot[NativeStationBossModel.Lamp]=BossMath.Euler(lampPitch,lampYaw,0);
        }
        private float lookYaw,lookPitch;
        private void Grab(int b0,int b1,int side,float e,float t)
        {
            float wob=e>.05f?.04f*BossBreath.Idle(t,Seed,50+side,5):0;
            localMove[b0]=new Vector3(side*1.45f*e,-.05f*e+wob,0);localRot[b0]=BossMath.Euler(0,0,side*-18*Deg*BossMath.Clamp01(e));
            localRot[b1]=BossMath.Euler(0,(side<0?180:-180)*Deg*(1-BossMath.Clamp01(e)),side*-8*Deg*BossMath.Clamp01(e)+wob);
        }
        // ---- the legs: the gait's planted hands or the channels' targets, two-bone IK, the hands ----
        private void SolveLegs(float t,float dt,IBossGround ground,Quaternion facing,float yaw)
        {
            var bodyRot=Legs.BodyRotation;
            for(int i=0;i<6;i++)
            {
                int b=StationBossCh.Leg(i,0);float w=ch[b];var hip=Legs.Hip[i];
                var gaitFoot=Legs.Foot[i];var gaitPole=BossMath.Rotate(bodyRot,Gait.Legs[i].Pole);
                var tl=new Vector3(ch[b+1],ch[b+2],ch[b+3]);var tw=Root+BossMath.Rotate(facing,tl);
                // A palm-down target on the floor (at the palm's lift) lands on the floor under it (the pit's bed, a platform).
                if(ch[b+4]>.5f&&Math.Abs(tl.Y-NativeStationBossModel.PalmLift)<.25f)tw.Y=ground.Height(tw.X,tw.Z)+tl.Y;
                var pole=Vector3.Normalize(Vector3.Lerp(gaitPole,BossMath.Rotate(facing,new Vector3(ch[b+5],ch[b+6],ch[b+7])),w));
                var target=Vector3.Lerp(gaitFoot,tw,w);legTarget[i]=target;legPole[i]=pole;legWeight[i]=w;legFlat[i]=ch[b+4];
                float soft=w>.01f?.04f*w:0;
                var knee=BossIk.TwoBone(hip,target,Gait.Legs[i].Upper,Gait.Legs[i].Lower,pole,out var end,soft);
                // The bones turn about the knee's own hinge (stable however the limb points; a pole-projected frame flips when the
                // limb passes along its pole, as a folded leg does).
                var hinge=Vector3.Cross(knee-hip,end-knee);if(hinge.LengthSquared()<1e-8f)hinge=Vector3.Cross(end-hip,pole);
                if(hingeKnown[i]&&Vector3.Dot(hinge,lastHinge[i])<0&&hinge.LengthSquared()<1e-4f)hinge=lastHinge[i];lastHinge[i]=hinge;hingeKnown[i]=true;
                var upper=Limb(hip,knee,hinge,pole);var lower=Limb(knee,end,hinge,pole);
                // The hand: palm down turned outward (the concept's splay), curling through the swing and slapping down; or, when a
                // channel reaches with it, the fingers carrying on along the shin.
                float splay=NativeStationBossModel.FootYaw(i)*Deg;
                if((Legs.Landed&(1<<i))!=0)slap[i]=1;slap[i]=Math.Max(0,slap[i]-dt/.12f);
                float curl=Legs.Swing[i]>=0?-.7f*(float)Math.Sin(Math.PI*Legs.Swing[i]):0;float slapPitch=.18f*slap[i]*(float)Math.Sin(Math.PI*(1-slap[i]));
                var gaitHand=Legs.FootRotation[i]*BossMath.AxisAngle(Vector3.UnitY,splay)*BossMath.AxisAngle(Vector3.UnitX,curl+slapPitch);
                var flatHand=BossMath.Yaw(yaw+splay);var alongHand=lower*BossMath.AxisAngle(Vector3.UnitX,90*Deg);
                var chanHand=BossMath.Slerp(alongHand,flatHand,legFlat[i]);var hand=BossMath.Slerp(gaitHand,chanHand,w);
                Pose.Set(NativeStationBossModel.Hip(i),hip,upper);Pose.Set(NativeStationBossModel.Knee(i),knee,lower);Pose.Set(NativeStationBossModel.Foot(i),end,hand);
            }
        }
        private readonly Vector3[] lastHinge=new Vector3[6];private readonly bool[] hingeKnown=new bool[6];
        /// <summary>A limb's rotation: local -Y along the bone, local +X on the knee's hinge (so +Z is the side it bends toward),
        /// falling back to the pole-projected frame when the hinge is degenerate.</summary>
        public static Quaternion Limb(Vector3 from,Vector3 to,Vector3 hinge,Vector3 pole)
        {
            var d=to-from;float l=d.Length();if(l<1e-6f)return Quaternion.Identity;var y=-d/l;var x=hinge-y*Vector3.Dot(hinge,y);float xl=x.Length();
            if(xl<1e-5f)return BossMath.Bone(from,to,pole);x/=xl;var z=Vector3.Cross(x,y);return BossMath.FromBasis(x,y,z);
        }
        /// <summary>Whether leg i's hand is planted by the gait and not taken by a channel (the planted-hand checks measure these).</summary>
        public bool PlantedFree(int i)=>Legs.Planted[i]&&legWeight[i]<1e-4f;
        public Vector3 Hand(int i)=>Pose.Position[NativeStationBossModel.Foot(i)];
        public Vector3 GaitFoot(int i)=>Legs.Foot[i];
        // ---- secondary motion: chains, look-at, the lamp ----
        private void Secondary(float t,float dt,IBossGround ground,Vector3 bodyPos,Quaternion bodyRot,bool hasTarget,Vector3 target,in StationBossView v)
        {
            // The Stationmaster's look (shared chest .3, head .7, applied next frame through the channels' lag: a smooth follow).
            var head=Pose.Position[NativeStationBossModel.SmHead];Look.Step(head,Follow.Yaw,hasTarget&&ch[StationBossCh.Look]>.2f,target,dt);
            lookYaw=Look.Yaw/Deg*ch[StationBossCh.Look];lookPitch=Look.Pitch/Deg*ch[StationBossCh.Look];
            // The headlamp: tracks the target, locks hard on Departure's lane at the fourth note (and cannot re-aim once broken).
            var lampWorld=Pose.Position[NativeStationBossModel.Lamp];LampLocked=false;
            if(ActMove>=0&&NativeStationBoss.Moves[ActMove].Id=="departure"&&StationBossView.At(v.laneX1))
            {
                var m=NativeStationBoss.Moves[ActMove];float lockAt=m.Note(NativeStationBoss.ChimeNotes-1)*(m.Anticipation>0?Act.HitAt/m.Anticipation:1);
                if(Act.Age>=lockAt){LampLocked=true;target=new Vector3(v.laneX1,1,v.laneZ1);hasTarget=true;}
            }
            if(!(LampLocked&&Act.Struck)&&!(v.LampBroken(t)&&ActMove>=0))
            {
                LampAim.Step(lampWorld,Follow.Yaw,hasTarget,target,dt);
                float pitchBody=0;lampYaw=LampAim.Yaw;lampPitch=LampAim.Pitch-pitchBody;
            }
            LampPos=Pose.Position[NativeStationBossModel.Lamp]+BossMath.Rotate(Pose.Rotation[NativeStationBossModel.Lamp],new Vector3(0,0,.22f));LampDir=BossMath.Rotate(Pose.Rotation[NativeStationBossModel.Lamp],Vector3.UnitZ);
            // The coupling chain (lifted on the rear-up), the cables (they spark at their ends), the horns' stalks, the window arms,
            // the coat tails over the roof.
            var chainFrame=Quaternion.Normalize(bodyRot*BossMath.AxisAngle(Vector3.UnitX,-ch[StationBossCh.ChainLift]*Deg*.4f));
            Coupling.Step(At(NativeStationBossModel.ChainFirst),chainFrame,dt,ground);Put(Coupling,NativeStationBossModel.ChainFirst,NativeStationBossModel.ChainLinks);
            CableA.Step(At(NativeStationBossModel.CableAFirst),bodyRot,dt,ground);Put(CableA,NativeStationBossModel.CableAFirst,NativeStationBossModel.CableLinks);
            CableB.Step(At(NativeStationBossModel.CableBFirst),bodyRot,dt,ground);Put(CableB,NativeStationBossModel.CableBFirst,NativeStationBossModel.CableLinks);
            float blast=ch[StationBossCh.Horn];var hornFrame=Quaternion.Normalize(bodyRot*BossMath.AxisAngle(Vector3.UnitX,.08f*blast*(float)Math.Sin(BossMath.Tau*9*t)));
            HornL.Step(At(NativeStationBossLook.HornL),hornFrame,dt);Stalk(HornL,NativeStationBossLook.HornL);HornR.Step(At(NativeStationBossLook.HornR),hornFrame,dt);Stalk(HornR,NativeStationBossLook.HornR);
            bool dead=v.Dead(t);
            for(int a=0;a<4;a++){int j=NativeStationBossModel.ArmFirst+a*2;var arm=Arms[a];arm.Def.Stiffness=dead?.6f:5;arm.Def.TipStiffness=dead?.1f:1.4f;arm.Step(At(j),bodyRot,dt,ground);
                PutArm(arm,j);}
            var hips=Pose.Rotation[NativeStationBossModel.SmHips];
            TailL.Step(At(NativeStationBossLook.TailL),hips,dt,roof);Put(TailL,NativeStationBossLook.TailL,NativeStationBossLook.TailLinks);
            TailR.Step(At(NativeStationBossLook.TailR),hips,dt,roof);Put(TailR,NativeStationBossLook.TailR,NativeStationBossLook.TailLinks);
        }
        private void PutArm(BossChain c,int j)
        {
            for(int k=0;k<2;k++){var dir=c.Point[k+1]-c.Point[k];if(!linked[j+k]){linkRot[j+k]=c.Rotation[k];linked[j+k]=true;}else{var was=BossMath.Rotate(linkRot[j+k],-Vector3.UnitY);linkRot[j+k]=Quaternion.Normalize(BossMath.FromTo(was,dir)*linkRot[j+k]);}Pose.Set(j+k,c.Point[k],linkRot[j+k]);}
        }
        // A joint's world origin from its parent as already posed (the chains' anchors).
        private Vector3 At(int j){int p=NativeStationBossLook.Parent[j];return Pose.Position[p]+BossMath.Rotate(Pose.Rotation[p],NativeStationBossLook.Rest[j]);}
        // A chain's links: each turned from its last frame's rotation by the shortest swing onto its new direction (parallel
        // transport), so a link lying along its pole (a chain on the floor, a tail along the roof) never flips about its own axis.
        private void Put(BossChain c,int first,int links)
        {
            for(int k=0;k<links&&k<c.Rotation.Length;k++)
            {
                int j=first+k;var dir=c.Point[k+1]-c.Point[k];
                if(!linked[j]||dir.LengthSquared()<1e-10f){linkRot[j]=c.Rotation[k];linked[j]=true;}
                else{var was=BossMath.Rotate(linkRot[j],-Vector3.UnitY);linkRot[j]=Quaternion.Normalize(BossMath.FromTo(was,dir)*linkRot[j]);}
                Pose.Set(j,c.Point[k],linkRot[j]);
            }
        }
        private readonly Quaternion[] linkRot=new Quaternion[Count];private readonly bool[] linked=new bool[Count];
        // A horn rides its stalk's tip turned with the stalk (its geometry hangs from the stalk's base in its rest frame).
        private void Stalk(BossChain c,int j){var body=Pose.Rotation[NativeStationBossModel.Body];var rest=BossMath.Rotate(body,c.Def.Rest);var now=c.Point[1]-c.Point[0];
            Pose.Set(j,c.Point[0],Quaternion.Normalize(BossMath.FromTo(rest,now)*body));}
        // ---- loose pieces: the hat in the death, the pantograph once snapped ----
        private void Loose(float t,in StationBossView v)
        {
            if(v.Dead(t)&&t-v.diedAt>=2.7f)
            {
                if(!hatLoose){hatLoose=true;hatAt=Math.Max(v.diedAt+2.7f,t-(t-v.diedAt>6?10:0));hatFrom=Pose.Position[NativeStationBossModel.SmHat];hatFromRot=Pose.Rotation[NativeStationBossModel.SmHat];
                    hatFromVel=BossMath.Rotate(BossMath.Yaw(Follow.Yaw),new Vector3(1.2f,1.6f,2.2f));}
                StationBossArrival.Hat(t-hatAt,hatFrom,hatFromRot,hatFromVel,Follow.Yaw,Root,out var hp,out var hr);Pose.Set(NativeStationBossModel.SmHat,hp,hr);
            }
            else hatLoose=false;
            if(v.PantoBroken(t))
            {
                if(!pantoLoose){pantoLoose=true;pantoAt=t-v.pantoBrokenAt>3?t-10:t;pantoFrom=Pose.Position[NativeStationBossModel.PantoHigh];pantoFromRot=Pose.Rotation[NativeStationBossModel.PantoHigh];pantoHeadFrom=Pose.Position[NativeStationBossModel.PantoHead];pantoHeadFromRot=Pose.Rotation[NativeStationBossModel.PantoHead];}
                float k=t-pantoAt;var side=BossMath.Right(Follow.Yaw);var drift=side*(1.8f*Math.Min(k,.7f))+Vector3.UnitY*(2.2f*Math.Min(k,.7f)-4.9f*Math.Min(k,.7f)*Math.Min(k,.7f));
                float fall=Math.Min(1,k/.7f);var floor=Math.Max(pantoFrom.Y+drift.Y,Root.Y+.1f);var spin=BossMath.AxisAngle(BossMath.Forward(Follow.Yaw),fall*1.4f);
                Pose.Set(NativeStationBossModel.PantoHigh,new Vector3(pantoFrom.X+drift.X,floor,pantoFrom.Z+drift.Z),Quaternion.Normalize(spin*pantoFromRot));
                Pose.Set(NativeStationBossModel.PantoHead,new Vector3(pantoHeadFrom.X+drift.X,Math.Max(pantoHeadFrom.Y+drift.Y,Root.Y+.15f),pantoHeadFrom.Z+drift.Z),Quaternion.Normalize(spin*pantoHeadFromRot));
            }
            else pantoLoose=false;
        }
        /// <summary>The roof as the coat tails' floor (a plane through the roof's top in the body's frame; the opening when peeled).</summary>
        private sealed class RoofGround:IBossGround
        {
            private Vector3 p,n;private Quaternion inv;private Vector3 origin;private float open;
            public void Set(Vector3 bodyPos,Quaternion bodyRot,float peel){origin=bodyPos;inv=BossMath.Inverse(bodyRot);open=peel;n=BossMath.Rotate(bodyRot,Vector3.UnitY);p=bodyPos+BossMath.Rotate(bodyRot,new Vector3(0,2.72f,0));}
            public float Height(float x,float z)
            {
                var l=BossMath.Rotate(inv,new Vector3(x,p.Y,z)-origin);if(Math.Abs(l.X)>1.45f||l.Z<NativeStationBossModel.RearZ||l.Z>NativeStationBossModel.CabZ)return -1000;
                float drop=l.Z<1.45f?open*1.5f:0;if(Math.Abs(n.Y)<1e-3f)return p.Y-drop;return p.Y-(n.X*(x-p.X)+n.Z*(z-p.Z))/n.Y-drop;
            }
        }
    }

    /// <summary>
    /// The arrival's presentation path (doc section 6, timed on <see cref="StationBossBeats"/>): out of the west tunnel on T1's
    /// rails braking hard (it stops at the crossing on the third accent), the legs unfolding on the fourth, heaving out of the pit
    /// onto P1 while it turns to face the concourse, the burst through the centre gate on the drop, walking on to the authority's
    /// spawn pose by the hold's end (so the fight starts exactly where the authority has it). Pure functions of the age.
    /// </summary>
    public static class StationBossArrival
    {
        /// <summary>Where the headlight first shows down the tunnel (x on T1), where the rolling body stops (the crossing), P1.</summary>
        public const float StartX=-76,StopX=0,ClimbZ=-6.6f,GateZ=-3.0f;
        /// <summary>A drop this close to the hold's end leaves no walk: the burst runs on to the live root, which the root reaches
        /// <see cref="Tail"/> after the hold's end.</summary>
        public const float ShortTail=1.8f,Tail=.7f;
        public static void Root(float a,in StationBossBeats b,Vector3 end,float endYaw,out Vector3 pos,out Vector3 vel,out float yaw)
        {
            float line=NativeStationBossArena.LineZ,east=90*BossMath.Deg;
            float t0=b.LightCut-.4f,stop=b.Accent1;
            if(a<stop)
            {
                // Running in down the tunnel, then braking at constant deceleration from the light cut to rest at the crossing on
                // the second accent (fastest as it bursts out of the mouth).
                float span=stop-t0,u=Math.Min(a,stop),rem=stop-u,dec=2*(StopX-StartX)/(span*span),v0=dec*span;
                float x=a<t0?StartX-v0*(t0-a):StopX-.5f*dec*rem*rem;pos=new Vector3(x,0,line);vel=new Vector3(a<t0?v0:dec*rem,0,0);yaw=east;return;
            }
            Path(a,b,end,out pos);Path(a+1/120f,b,end,out var q);vel=(q-pos)*120;
            yaw=east+BossMath.WrapPi(endYaw-east)*BossMath.Smoother((a-b.Accent2-.35f)/(b.Drop-b.Accent2-.35f));
        }
        // At rest, then (after the legs unfold on the third accent) the climb and the turn onto P1, the burst through the gate on
        // the drop, the walk on to the spawn pose.
        private static void Path(float a,in StationBossBeats b,Vector3 end,out Vector3 p)
        {
            float line=NativeStationBossArena.LineZ;
            var p0=new Vector3(StopX,0,line);var p1=new Vector3(StopX,0,ClimbZ);var p2=new Vector3(StopX,0,GateZ);
            float climb=BossMath.Smoother((a-b.Accent2-.35f)/(b.Drop-.15f-b.Accent2-.35f));var up=Vector3.Lerp(p0,p1,climb);
            // A drop close to the hold's end (the boss song's) leaves no time to stop and walk: the burst carries on through the
            // gate to the live root, landing on it Tail after the hold's end (the root alone; the fight's moves start on time).
            if(b.Hold-b.Drop<ShortTail){p=Vector3.Lerp(up,end,BossMath.Smoother((a-(b.Drop-.15f))/(b.Hold+Tail-(b.Drop-.15f))));return;}
            float burst=BossMath.Smoother((a-b.Drop+.15f)/.6f),walk=BossMath.Smoother((a-(b.Drop+.9f))/Math.Max(.5f,b.Hold-b.Drop-1.3f));
            p=Vector3.Lerp(Vector3.Lerp(up,p2,burst),end,walk);
        }
        /// <summary>The top hat's fall in the death: knocked off forward, a bounce, rolling on its brim to rest on its side.</summary>
        public static void Hat(float k,Vector3 from,Quaternion fromRot,Vector3 vel,float bodyYaw,Vector3 root,out Vector3 pos,out Quaternion rot)
        {
            float floor=root.Y+.42f*.3f+.12f;k=Math.Max(0,k);
            // Ballistic to the floor, then a damped bounce, then a roll that slows to rest.
            float tFall=(float)((vel.Y+Math.Sqrt(Math.Max(0,vel.Y*vel.Y+2*9.8f*(from.Y-floor))))/9.8f);
            var flat=new Vector3(vel.X,0,vel.Z);
            if(k<tFall){pos=from+flat*k+Vector3.UnitY*(vel.Y*k-4.9f*k*k);rot=Quaternion.Normalize(BossMath.AxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY,flat)),k*3.2f)*fromRot);return;}
            float r=k-tFall,roll=1-(float)Math.Exp(-r*1.6f),bounce=.25f*(float)Math.Exp(-r*6)*Math.Abs((float)Math.Sin(r*9));
            var dir=Vector3.Normalize(flat+new Vector3(.001f,0,0));var land=from+flat*tFall;pos=new Vector3(land.X,floor+bounce,land.Z)+dir*(1.4f*roll);
            var side=BossMath.AxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY,dir)),84*BossMath.Deg);var spin=BossMath.AxisAngle(Vector3.UnitY,bodyYaw+40*BossMath.Deg+roll*2.4f);
            rot=Quaternion.Normalize(spin*side);
        }
    }
}

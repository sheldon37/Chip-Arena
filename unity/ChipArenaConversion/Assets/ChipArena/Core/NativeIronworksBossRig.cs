using System;
using System.Numerics;
using M=ChipArena.UnityConversion.NativeIronworksBossModel;
using L=ChipArena.UnityConversion.NativeIronworksBossLook;
using Ch=ChipArena.UnityConversion.IronworksBossCh;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE CONVERTER's motion (docs/EXPEDITION-BOSS-CITY-IRONWORKS.md section 8 and "Presentation build"), on the shared boss
    /// motion library (docs/BOSS-MOTION.md): presentation only, a pure reader of the boss view and the clocks that writes a
    /// <see cref="BossPose"/> (one joint per <see cref="NativeIronworksBossLook"/> joint: world position and rotation; each joint's
    /// geometry is in its frame).
    /// <list type="bullet">
    /// <item>Root: <see cref="BossRootFollow"/> (the authority's root through its snapshots, or a continuous target), a heavy turn
    /// (35 degrees a second, 65 in phase 3) and lean.</item>
    /// <item>Gait: <see cref="BossGait"/> with four legs, the tong front feet and the crane-bogie rear feet, a diagonal trot that
    /// morphs into a transverse gallop on the charge; planted feet are locked (the gait never moves them) and solved by the
    /// concept's coxa and two-bone IK (softened at full reach); every body move goes through the gait's ExtraOffset and
    /// ExtraRotation so planted feet stay planted; a swinging foot accelerates into its plant (a thunk); the bogies' wheels roll
    /// with the distance the foot travels.</item>
    /// <item>The vessel on its trunnions: the action's tilt target plus a damped pendulum (3.2 rad/s, damping .25) driven by the
    /// body's acceleration and the target's own, so every stop, turn and slam swings it and lets it settle; the metal inside on a
    /// 2D spring (4 rad/s, .15) against the vessel, spilling past 12 degrees.</item>
    /// <item>Actions: one <see cref="BossActionPlayer"/> for the eight attacks (<see cref="IronworksBossMoves"/>: contact on
    /// actionAt + windup), one for the phase changes (shed, break) and one for the moments (choke, kneel, crash); the arrival and
    /// the death are tracks on their own clocks (the boss song's and the death's, which run through the holds).</item>
    /// <item>Secondary: <see cref="BossChain"/>s for the four yoke chains (hooks and shackles), the levers' pull chains and, in
    /// phase 3, the foreman's belt chain; the torn pulpit on a pendulum; the foreman's lean on second order dynamics (thrown by
    /// slams and stops), his breath, his head's look-at, his arms solved onto the lever knobs (the lever leads the tilt);
    /// <see cref="BossFlinch"/> and <see cref="BossStagger"/> on top; the phase 3 tremor; <see cref="BossInertializer"/> so a jump
    /// (a teleport, a late join, the arrival's hand-off) never pops.</item>
    /// </list>
    /// Fixed fields and arrays: no allocation per frame. Nothing here is read by the authority: hit timing, hitboxes, damage,
    /// cooldowns, movement and networking are unchanged (strikes only align their contact frame with the record's hit).
    /// </summary>
    public sealed class IronworksBossRig
    {
        public const int Count=L.Count;
        private const float Deg=BossMath.Deg;
        /// <summary>The pose written each frame and the inertialized one actually shown.</summary>
        public readonly BossPose Pose=new BossPose(Count),Shown=new BossPose(Count);
        public readonly BossRootFollow Follow=new BossRootFollow{PositionHalfLife=.16f,MaxExtrapolate=.3f,TurnHalfLife=.18f,MaxTurnRate=35*Deg,MaxTurnAccel=90*Deg,
            BankScale=.5f,MaxBank=3*Deg,PitchScale=.45f,SpeedPitch=.004f,MaxPitch=3*Deg,LeanFrequency=.9f,LeanDamping=.5f};
        public readonly BossGaitDef Gait=MakeGait();public readonly BossGait Legs;
        public readonly BossActionPlayer Actions=new BossActionPlayer(),Phases=new BossActionPlayer(),Moments=new BossActionPlayer();
        public readonly BossLookAt Look=new BossLookAt{MaxYaw=1.0f,MaxUp=.45f,MaxDown=.7f,HalfLife=.2f,MaxRate=3f};
        public readonly BossLookAt Eyes=new BossLookAt{MaxYaw=.4f,MaxUp=.15f,MaxDown=.35f,HalfLife=.3f,MaxRate=1.5f};
        public readonly BossFlinch Flinch=new BossFlinch{PitchGain=.035f,BankGain=.03f,ShiftGain=.06f,Rise=.09f,Wobble=.35f,WobbleHz=1.3f};
        public readonly BossStagger Stagger=new BossStagger{Throw=.12f,Shift=.7f,PitchGain=.12f,BankGain=.16f,Crouch=.35f};
        public readonly BossInertializer Inertia=new BossInertializer(Count);
        /// <summary>The four yoke chains (links + 1 points each).</summary>
        public readonly BossChain[] Chains=new BossChain[M.Chains];
        public readonly BossChain[] LeverChains={new BossChain(LeverDef()),new BossChain(LeverDef())};
        public readonly BossChain Belt=new BossChain(new BossChainDef{Count=4,Length=.32f,Rest=new Vector3(0,-1,0),Stiffness=0,TipStiffness=0,Damping=.5f,Gravity=9.8f,Radius=.05f,Friction=3,Iterations=4});
        private static BossChainDef LeverDef()=>new BossChainDef{Count=4,Length=.085f,Rest=new Vector3(0,-1,0),Stiffness=0,TipStiffness=0,Damping=.9f,Gravity=9.8f,Radius=.02f,Iterations=3};
        public static readonly int[] ChainLinks=new int[M.Chains];
        static IronworksBossRig(){for(int c=0;c<M.Chains;c++)ChainLinks[c]=(int)(M.ChainLength[c]/M.LinkPitch);}
        /// <summary>The links drawn for the torn pulpit's chains (each), by tier.</summary>
        public static int TearLinks(int tier)=>tier==2?5:9;
        /// <summary>The most chain links the presentation draws at a tier (the yoke chains, every third one, stretched, at low detail; the lever and
        /// belt chains above low detail; the torn pulpit's two chains): the model's triangle budget counts these.</summary>
        public static int LinksDrawn(int tier)
        {
            int n=0;for(int c=0;c<M.Chains;c++)n+=tier==2?(ChainLinks[c]+2)/3:ChainLinks[c];
            if(tier<2)n+=2*(LeverDef().Count-1)+3;return n+2*TearLinks(tier);
        }
        // ---- dynamics ----
        private float swing,swingVel,lastTiltTarget=float.NaN,lastTiltVel;private Vector3 lastBodyVel;private float lastYaw;
        private BossSecondOrder heave,lean,leverLag,pulpitSwing,beam;private BossSecondOrder3 slosh,headLag;private bool configured;
        // ---- state ----
        private bool started;private float seenSpawned=float.NaN;private int plantedKey=-1;private readonly int[] plantedBy=new int[4];
        private readonly float[] ch=new float[Ch.Count],sample=new float[Ch.Count],sample2=new float[Ch.Count];
        private readonly Vector3[] legTarget=new Vector3[4];private readonly float[] legWeight=new float[4];
        private float valve;private float arrivedBefore=float.NaN;
        // ---- outputs (the effects, the cues and the checks read them) ----
        /// <summary>Whether the boss shows; whether its arrival is playing and how far in (s, on the boss song clock); the death's age.</summary>
        public bool Visible,Arriving;public float ArrivalAge=float.NaN,DeathAge=float.NaN;
        /// <summary>The root as presented, its yaw (rad) and speed; this frame's landings (bit per leg) and how hard.</summary>
        public Vector3 Root;public float Yaw,Speed;public int Landed;public readonly float[] LandSpeed=new float[4];
        /// <summary>The action now: its sample, index (-1 none) and the side it plays to (+1 right, -1 left), the leg it uses.</summary>
        public BossActionSample Act;public int ActIndex=-1,ActSide=1,ActLeg=-1;
        /// <summary>The phase change (2 shed, 3 break) and the moment playing.</summary>
        public BossActionSample PhaseAct,MomentAct;public int PhaseKind,MomentIndex=-1;
        /// <summary>The channels as resolved this frame (the effects read the heat, the blow, the steam...).</summary>
        public float[] Channels=>ch;
        /// <summary>Per joint: emission scale and hidden flag.</summary>
        public readonly float[] Glow=new float[Count];public readonly bool[] Hidden=new bool[Count];
        /// <summary>The vessel's tilt as shown (degrees), its pendulum's speed, the metal's slosh (degrees about x and z), its spill (0..1).</summary>
        public float Tilt,TiltRate,SloshX,SloshZ,Spill;
        /// <summary>The mouth's centre and axis (world; the axis out of the mouth), the tuyère grate, the lamps' lenses and beam
        /// directions, the foreman's head, the whistle, each tong's tip.</summary>
        public Vector3 Mouth,MouthAxis,GrateAt,GrateAxis,HeadAt,WhistleAt,LampAtL,LampAtR,BeamL,BeamR;public readonly Vector3[] TongTip=new Vector3[2];
        /// <summary>Heat (mouth), blow (grate), seams (phase 3 white), cooling (the death), lamps, visor, the crust's crack glow.</summary>
        public float HeatLevel,BlowLevel,SeamLevel,CoolLevel,LampLevel,VisorLevel,CrackLevel;
        /// <summary>Each crust chunk's state: 0 on, 1 flying, 2 rubble; when it left (presentation clock).</summary>
        public readonly byte[] ChunkState=new byte[12];public readonly float[] ChunkAt=new float[12];
        /// <summary>The pulpit torn (0..1 of its swing down) and when.</summary>
        public float PulpitTear;public float PulpitAt=float.NaN;
        public uint Seed=0xC0FFEEu;public float LastHit=float.NaN;

        public IronworksBossRig()
        {
            Legs=new BossGait(Gait);for(int c=0;c<M.Chains;c++)Chains[c]=new BossChain(new BossChainDef{Count=ChainLinks[c]+1,Length=M.LinkPitch,Rest=new Vector3(0,-1,0),Stiffness=0,TipStiffness=0,
                Damping=.45f,Gravity=9.8f,MinStretch=.97f,Radius=.04f,Friction=6,Iterations=4});
            for(int i=0;i<4;i++)plantedBy[i]=-1;
        }
        private static BossGaitDef MakeGait()
        {
            var legs=new BossLegDef[4];
            for(int i=0;i<4;i++)
            {
                var home=Ch.Stance(i);var hip=M.Hip(i);int side=Ch.Side(i);
                // The femur's pivot sits the coxa's reach out from the hip, toward the foot: the gait's hip for its reach and sink.
                var flat=new Vector3(home.X-hip.X,0,home.Z-hip.Z);if(flat.LengthSquared()>1e-6f)flat=Vector3.Normalize(flat);var pivot=hip+flat*M.CoxaReach;
                legs[i]=new BossLegDef{Hip=pivot,Home=new Vector3(home.X,0,home.Z),Pole=Vector3.Normalize(new Vector3(side*.45f,1,0)),Upper=M.FemurLength,Lower=M.TibiaLength,
                    Phase=i==M.FL||i==M.RR?0:.5f,Side=side,Ankle=M.Ankle(i)};
            }
            return new BossGaitDef{Legs=legs,WalkSpeed=1.6f,RunSpeed=11.7f,WalkStride=2.2f,RunStride=4.4f,WalkDuty=.62f,RunDuty=.42f,WalkLift=.7f,RunLift=1.0f,MinFrequency=.42f,MinSwing=.22f,MaxSwing=.8f,
                StartSpeed=.2f,StopSpeed=.08f,SettleDistance=.35f,SettleSwing=.55f,ReachMargin=.25f,MaxSink=.05f,BodyHeight=M.StandHeight,Bob=.06f,RunBob=.2f,Sway=.06f,Roll=2.5f*Deg,Twist=.02f,LandDip=.12f,
                SupportFrequency=.9f,SupportDamping=.6f,LiftPeak=.4f,ToeOff=.2f,HeelStrike=.15f,FloorFollow=.5f,TurnRadius=4.6f};
        }
        /// <summary>The trot's phase offsets (FL with RR, FR with RL) and the gallop's (front pair, then the rear, a beat apart).</summary>
        private static readonly float[] Trot={0,.5f,.5f,0},Gallop={0,.12f,.5f,.62f};

        /// <summary>A blow landed (its time, the push from the attacker into the boss, its size ~1 heavy): the body flinches.</summary>
        public void Hit(double at,Vector3 push,float size){Flinch.Add(at,push,size,Follow.Yaw);LastHit=(float)at;}

        /// <summary>
        /// Poses the boss at `t` (the arena's presentation clock: actions are sampled on it) after a frame of `dt` (the frame's own
        /// time: the springs and chains integrate on it, so they keep moving through the holds while `t` stands still). `v` is the
        /// record, `velocity` the root's ground velocity when the caller has it, `externalRoot` true when the caller has stepped
        /// <see cref="Follow"/> from snapshots itself. `arrival` is the boss song clock (s since the arrival, NaN none: it runs through
        /// the intro hold), `death` the death's age (s, NaN alive: it runs through the victory hold). `target` is who the foreman and
        /// the lamps look at.
        /// </summary>
        public void Update(float t,float dt,in IronworksBossView v,IBossGround ground,Vector3 velocity,bool externalRoot,bool hasTarget,Vector3 target,double arrival,double death)
        {
            Visible=v.live;if(!v.live){started=false;return;}
            if(!configured)Configure();dt=BossMath.Clamp(dt,0,.1f);aimX=v.targetX;aimZ=v.targetZ;lookTarget=hasTarget;lookAt=target;
            var beats=NativeIronworksBossBeats.Current;
            // ---- the arrival on the song clock (a late joiner, more than LateJoin into it, sees the boss in place) ----
            if(!seenSpawned.Equals(v.spawned)){seenSpawned=v.spawned;ResetParts();lateJoin=!double.IsNaN(arrival)&&arrival>LateJoin;}
            float age=double.IsNaN(arrival)?float.PositiveInfinity:(float)arrival;
            Arriving=!lateJoin&&age>=0&&age<beats.Drop+ArrivalTail&&!v.Dead(t);ArrivalAge=Arriving?age:float.NaN;
            DeathAge=v.Dead(t)?(float)(double.IsNaN(death)?t-v.deathAt:death):float.NaN;
            float vyaw=v.yaw*Deg;var vroot=new Vector3(v.x,v.y,v.z);
            if(!externalRoot)
            {
                Follow.MaxTurnRate=(v.Phase>=3?65:35)*Deg;Follow.MaxTurnAccel=(v.Phase>=3?160:90)*Deg;
                Follow.Follow(vroot,Arriving?Vector3.Zero:velocity,vyaw,dt,!started);
            }
            bool first=!started;started=true;
            float yaw=Follow.Yaw;var facing=BossMath.Yaw(yaw);var fwd=BossMath.Forward(yaw);Root=Follow.Position;Yaw=yaw;Speed=Follow.Speed;
            // ---- the record's action, phase change and moment on their players ----
            int act=Arriving||v.Dead(t)?-1:v.Action;bool attack=act>=0&&NativeIronworksBoss.Actions[act].Code<NativeIronworksBoss.Arrive;
            if(attack)
            {
                var a=NativeIronworksBoss.Actions[act];int key=Key(v.actionAt,act);
                if(sideKey!=key){sideKey=key;ActSide=SideOf(act,v,hasTarget,target);ActLeg=LegOf(act,v);}
                Actions.Play(IronworksBossMoves.Defs[act],key,v.actionAt,a.Windup,float.NaN);
            }
            // The phase changes play from phaseAt (the record's shed/break action, when sent, is the same moment).
            if(BossAt(v.phaseAt)&&v.Phase>=2&&!Arriving)
            {
                int pi=IronworksBossMoves.Index(v.Phase==2?"shed":"break");var pa=NativeIronworksBoss.Actions[pi];
                Phases.Play(IronworksBossMoves.Defs[pi],v.Phase,v.phaseAt,pa.Windup,float.NaN);PhaseKind=v.Phase;
            }
            if(act>=0&&!attack)
            {
                string id=NativeIronworksBoss.Actions[act].Id;
                if(id=="choke"||id=="kneel"||id=="crash"){var a=NativeIronworksBoss.Actions[act];Moments.Play(IronworksBossMoves.Defs[act],Key(v.actionAt,act),v.actionAt,a.Windup,float.NaN);MomentIndex=act;}
            }
            // An attack the record no longer plays (interrupted by a moment, a phase change, the death) fades from where it stood.
            if(!attack)Actions.Cancel(t);
            Actions.Sample(t);Phases.Sample(t);Moments.Sample(t);
            Act=Actions.Current;ActIndex=Act.Active&&act>=0&&attack&&ReferenceEquals(Actions.CurrentDef,IronworksBossMoves.Defs[act])?act:-1;PhaseAct=Phases.Current;MomentAct=Moments.Current;
            if(!MomentAct.Active)MomentIndex=-1;
            // ---- the channels: neutral, then each layer in priority (attacks, moments, phase changes, the arrival, the death) ----
            Array.Copy(Ch.Neutral,ch,ch.Length);
            Layer(Actions,true);Layer(Moments,false);Layer(Phases,false);
            if(Arriving)ArrivalLayer(age,beats,v);
            if(v.Dead(t))DeathLayer(DeathAge);
            Additive(v,t,beats);
            // ---- staggers: the break throws it sideways, the crash back, the choke a little ----
            if(PhaseAct.Active&&PhaseKind==3)Stagger.Start(v.phaseAt,BossMath.Right(yaw),1.1f,2.4f,yaw);
            if(MomentIndex>=0){string id=NativeIronworksBoss.Actions[MomentIndex].Id;if(id=="crash")Stagger.Start(v.actionAt,-fwd,1.3f,2.0f,yaw);else if(id=="choke")Stagger.Start(v.actionAt,-fwd,.4f,1.2f,yaw);}
            // ---- the body through the gait (planted feet stay planted) ----
            Stagger.Sample(t,out var sShift,out float sPitch,out float sBank,out float sCrouch,out _);
            Flinch.Sample(t,out float fPitch,out float fBank,out var fShift);
            float heaveY=heave.Step(ch[Ch.BY],dt);
            var lean=BossMath.Euler(-ch[Ch.BP]*Deg,ch[Ch.BW]*Deg,ch[Ch.BR]*Deg);
            float lead=BossMath.Clamp(Follow.YawRate*.1f,-6*Deg,6*Deg);
            var offset=BossMath.Rotate(facing,new Vector3(ch[Ch.BX],heaveY-sCrouch,ch[Ch.BZ])+sShift+fShift);
            Legs.ExtraOffset=offset;Legs.ExtraRotation=Quaternion.Normalize(BossMath.AxisAngle(Vector3.UnitY,lead)*lean*BossMath.AxisAngle(Vector3.UnitX,-(sPitch+fPitch))*BossMath.AxisAngle(Vector3.UnitZ,-(sBank+fBank)));
            MorphGait(v,t);
            for(int i=0;i<4;i++)Legs.Free[i]=ch[Ch.Leg(i,Ch.W)]>=.98f;
            if(first||Follow.Jumped){Legs.Reset(Root,yaw,ground);for(int c=0;c<M.Chains;c++)chainReady=false;}
            Legs.Step(Root,yaw,Follow.YawRate,Arriving?Vector3.Zero:Follow.Velocity,Follow.Body,dt,ground);Landed=Legs.Landed;for(int i=0;i<4;i++)LandSpeed[i]=Legs.LandSpeed[i];
            var bodyPos=Legs.BodyPosition;var bodyRot=Legs.BodyRotation;
            // ---- the vessel on its trunnions and the metal in it ----
            if(first){lastBodyPos=bodyPos;lastBodyVel=Vector3.Zero;lastForeman=bodyPos;foremanVel=Vector3.Zero;lastYaw=yaw;lastTiltTarget=float.NaN;}
            Vessel(dt,bodyRot,v,t);
            // ---- every joint ----
            Pose.RootPosition=Root;Pose.RootRotation=facing;Pose.Set(M.Root,Root,facing);Pose.Set(M.Yoke,bodyPos,bodyRot);
            var vesselRot=Quaternion.Normalize(bodyRot*BossMath.AxisAngle(Vector3.UnitX,Tilt*Deg));
            foreach(int j in new[]{M.Vessel,M.VesselHeat,M.WindBox,M.Seams})Pose.Set(j,bodyPos,vesselRot);
            Pose.Set(L.Metal,bodyPos+BossMath.Rotate(vesselRot,L.MetalAt),Quaternion.Normalize(vesselRot*BossMath.Euler(SloshX*Deg,0,SloshZ*Deg)));
            Lamps(bodyPos,bodyRot,dt,hasTarget,target);
            Pulpit(t,dt,bodyPos,bodyRot,v,ground);
            SolveLegs(dt,ground,facing,yaw);
            Crust(t,v,bodyPos,vesselRot);
            Secondary(t,dt,ground,bodyPos,bodyRot);
            Outputs(v,t,bodyPos,bodyRot,vesselRot);
            // ---- the pop killer: a jump in the source (a teleport, the arrival's hand-off, a late join) decays on springs ----
            bool handOff=!float.IsNaN(arrivedBefore)&&!Arriving;arrivedBefore=Arriving?1:float.NaN;
            if(!first&&(Follow.Jumped||handOff))Inertia.Transition(Shown,Pose);
            Inertia.Apply(Pose,.18f,dt,Shown);
        }
        /// <summary>A late joiner (more than this far into the arrival on the song clock, s) sees the boss in place.</summary>
        public const float LateJoin=3;
        /// <summary>How long after the drop the arrival's first blow plays before the fight takes it over (s).</summary>
        public const float ArrivalTail=1.6f;
        private bool lateJoin,chainReady;private int sideKey=int.MinValue;
        private static bool BossAt(float t)=>!float.IsNaN(t);
        /// <summary>The player key of one action start (unique per start: the start time and the action).</summary>
        public static int Key(float at,int act)=>(int)Math.Round(at*240)*31+act;
        private void Configure()
        {
            heave.Configure(1.1f,.55f,1.4f);lean.Configure(1.4f,.38f,0);leverLag.Configure(2.6f,.5f,1.2f);pulpitSwing.Configure(.55f,.18f,0);beam.Configure(2.2f,.35f,0);
            slosh.Configure(4/BossMath.Tau,.15f,0);headLag.Configure(1.8f,.45f,0);configured=true;
        }
        private void ResetParts(){for(int k=0;k<12;k++){ChunkState[k]=0;ChunkAt[k]=float.NaN;}PulpitTear=0;PulpitAt=float.NaN;Stagger.Clear();Flinch.Clear();Actions.Clear();Phases.Clear();Moments.Clear();for(int i=0;i<4;i++)plantedBy[i]=-1;}

        // ---- layers ----
        private void Layer(BossActionPlayer p,bool sided)
        {
            var a=p.Current;var b=p.Previous;float wa=a.Active?a.Weight:0,wb=b.Active?b.Weight:0;if(wa+wb<=1e-5f)return;
            var ka=wa>0?KeysOf(p.CurrentDef,sided&&ActSide<0):null;var kb=wb>0?KeysOf(p.PreviousDef,false):null;if(ka==null)wa=0;if(kb==null)wb=0;if(wa+wb<=1e-5f)return;
            if(wa>0)IronworksBossMoves.Sample(ka,a.Progress,sample);if(wb>0)IronworksBossMoves.Sample(kb,b.Progress,sample2);
            float w=Math.Min(1,wa+wb),na=wa/(wa+wb),nb=wb/(wa+wb);
            for(int c=0;c<ch.Length;c++){float s=(wa>0?sample[c]*na:0)+(wb>0?sample2[c]*nb:0);ch[c]+=(s-ch[c])*w;}
            // A sided leg move (stomp, grab) on a rear leg or on the other front: the authored FR channel retargeted to the record's leg.
            if(sided&&a.Active&&ActLeg>=2)Retarget(ActLeg==M.RL?M.FL:M.FR,ActLeg);
            // Planted by the phrase: once a leg's landing key is reached, the gait owns the foot where it landed.
            if(a.Active)for(int leg=0;leg<4;leg++)
            {
                int f=Ch.Leg(leg,Ch.PL);if(ch[f]>.5f&&ch[Ch.Leg(leg,Ch.W)]>.5f&&plantedBy[leg]!=a.Id){plantedBy[leg]=a.Id;plantNow|=1<<leg;}
                if(plantedBy[leg]==a.Id&&!((plantNow&(1<<leg))!=0))ch[Ch.Leg(leg,Ch.W)]=0;
            }
        }
        private int plantNow;
        private static IronworksBossSnap[] KeysOf(BossActionDef d,bool mirror)
        {
            if(d==null)return null;var defs=IronworksBossMoves.Defs;for(int i=0;i<defs.Length;i++)if(ReferenceEquals(defs[i],d))return mirror?IronworksBossMoves.KeysMirrored[i]:IronworksBossMoves.Keys[i];return null;
        }
        // Moves leg `from`'s channels onto leg `to` (relative to each leg's stance), clearing `from`.
        private void Retarget(int from,int to)
        {
            var sf=Ch.Stance(from);var stt=Ch.Stance(to);int b0=Ch.Leg(from,0),b1=Ch.Leg(to,0);float w=ch[b0];if(w<=0)return;
            ch[b1]=w;ch[b1+Ch.X]=stt.X+(ch[b0+Ch.X]-sf.X)*(Ch.Side(to)==Ch.Side(from)?1:-1);ch[b1+Ch.Y]=ch[b0+Ch.Y];ch[b1+Ch.Z]=stt.Z+(ch[b0+Ch.Z]-sf.Z)*(Ch.Front(to)?1:-1);ch[b1+Ch.FP]=ch[b0+Ch.FP];ch[b1+Ch.PL]=ch[b0+Ch.PL];
            ch[b0]=0;ch[b0+Ch.PL]=0;
        }
        private int SideOf(int act,in IronworksBossView v,bool hasTarget,Vector3 target)
        {
            if(IronworksBossMoves.Side[act]==0)return 1;string id=NativeIronworksBoss.Actions[act].Id;
            if(id=="stomp"){int leg=Math.Max(0,Math.Min(3,v.variant));return Ch.Side(leg);}
            var p=!float.IsNaN(v.targetX)?new Vector3(v.targetX,0,v.targetZ):hasTarget?target:Follow.Position+BossMath.Forward(Follow.Yaw);
            return Vector3.Dot(p-Follow.Position,BossMath.Right(Follow.Yaw))>=0?1:-1;
        }
        private int LegOf(int act,in IronworksBossView v)
        {
            string id=NativeIronworksBoss.Actions[act].Id;if(id=="stomp")return Math.Max(0,Math.Min(3,v.variant));if(id=="grab")return ActSide>0?M.FR:M.FL;return -1;
        }
        // ---- the arrival: buried, the tongs punch up and grip the rim on the first hydraulic hit, the heave on the next downbeat,
        // the rear legs climb out on the second slam, standing by the furnace flare (the lamps click on), the first blow on the drop ----
        private void ArrivalLayer(float a,in IronworksBossBeats b,in IronworksBossView v)
        {
            var bur=IronworksBossMoves.Buried.V;var gri=IronworksBossMoves.Gripped.V;var hea=IronworksBossMoves.Heaved.V;var ris=IronworksBossMoves.Risen.V;
            // Body: buried until the hit, a lurch up as the tongs grip, the heave mouth first, the climb to standing.
            float grip=BossMath.Smoother((a-(b.Hit-.05f))/.32f),heave=BossMath.Smoother((a-b.Heave)/Math.Max(.3f,b.Climb-b.Heave)),climb=BossMath.Smoother((a-b.Climb)/Math.Max(.4f,(b.Flare-b.Climb)*.85f));
            for(int c=0;c<Ch.Count;c++)
            {
                if(c>=Ch.LegBase&&c<Ch.LegBase+4*Ch.LegStride)continue;
                float x=bur[c];x+=(gri[c]-x)*grip;x+=(hea[c]-x)*heave;x+=(ris[c]-x)*climb;float rest=Ch.Neutral[c];
                // Risen settles into the fight's neutral by the drop.
                float settle=BossMath.Smoother((a-b.Flare)/Math.Max(.3f,b.Drop-b.Flare));x+=(rest-x)*settle;ch[c]=x;
            }
            // Legs: the fronts punch up through the crust on the hit (from below to the rim, a fast In), grip there through the heave,
            // and step out to the stance as it stands; the rears stay folded in the pit until the climb, then step up behind.
            for(int leg=0;leg<4;leg++)
            {
                int bse=Ch.Leg(leg,0);bool front=Ch.Front(leg);var home=Ch.Stance(leg);
                var buried=new Vector3(bur[bse+Ch.X],bur[bse+Ch.Y],bur[bse+Ch.Z]);var rim=new Vector3(gri[bse+Ch.X],gri[bse+Ch.Y],gri[bse+Ch.Z]);var crouch=new Vector3(hea[bse+Ch.X],hea[bse+Ch.Y],hea[bse+Ch.Z]);
                Vector3 p;float pitch;
                if(front)
                {
                    float punch=BossEasing.Eval(BossEase.In,BossMath.Clamp01((a-(b.Hit-.12f))/.24f),2.2f);
                    var up=Vector3.Lerp(buried,rim,punch);up.Y+=.6f*(float)Math.Sin(Math.PI*punch);
                    // Out to the stance as the body stands: one stepped arc, front left first.
                    float s=BossMath.Clamp01((a-(b.Climb+.45f+(leg==M.FR?.38f:0)))/.6f);
                    p=Vector3.Lerp(up,home,BossMath.Smoother(s));p.Y+=1.0f*Thunk(s);pitch=gri[bse+Ch.FP]*(1-s);
                }
                else
                {
                    // Folded under until the climb, then up and out of the pit onto the floor behind (a high arc over the rim).
                    float rise=BossMath.Smoother((a-b.Heave)/Math.Max(.3f,b.Climb-b.Heave));var folded=Vector3.Lerp(buried,crouch,rise);
                    float s=BossMath.Clamp01((a-(b.Climb-.12f+(leg==M.RR?.3f:0)))/.62f);
                    p=Vector3.Lerp(folded,home,BossMath.Smoother(s));p.Y+=Math.Max(0,1.6f*Thunk(s));pitch=0;
                }
                ch[bse]=1;ch[bse+Ch.X]=p.X;ch[bse+Ch.Y]=p.Y;ch[bse+Ch.Z]=p.Z;ch[bse+Ch.FP]=pitch;ch[bse+Ch.PL]=0;
            }
            // Off the gait once every foot is at its stance (the gait takes them, planted, as the arrival ends).
            float handoff=BossMath.Smoother((a-(b.Flare-.4f))/.4f);for(int leg=0;leg<4;leg++)ch[Ch.Leg(leg,Ch.W)]=1-handoff*(a>b.Climb+1.5f?1:0);
            // The lamps click on and the visor lights on the furnace flare; the mouth flares with the furnaces.
            float lamps=a<b.Flare?0:a<b.Flare+.07f?1:a<b.Flare+.14f?.15f:a<b.Flare+.2f?1:1;ch[Ch.Lamp]=lamps;
            ch[Ch.Heat]+=1.4f*Math.Max(0,1-Math.Abs(a-b.Flare-.15f)/.6f)+.8f*BossMath.Smooth((a-b.Hit)/.2f)*(1-BossMath.Smooth((a-b.Heave)/.8f));
            ch[Ch.Look]=BossMath.Smoother((a-b.Flare)/.5f);
            // The first blow on the drop: the inhale from a beat before, the column from the drop (the effects play the column).
            float inhale=BossMath.Smoother((a-(b.Drop-.7f))/.5f)*(1-BossMath.Smoother((a-b.Drop)/.3f)),column=BossMath.Smooth((a-b.Drop)/.25f)*(1-BossMath.Smoother((a-(b.Drop+ArrivalTail-.5f))/.5f));
            ch[Ch.BY]+=-.25f*inhale+.35f*column;ch[Ch.Blow]=Math.Max(ch[Ch.Blow],1.0f*inhale+1.3f*column);ch[Ch.Heat]=Math.Max(ch[Ch.Heat],.85f+1.1f*column);ch[Ch.Tilt]+=-6*inhale-8*column;ch[Ch.Steam]=Math.Max(ch[Ch.Steam],inhale);
            ch[Ch.ChainLift]=Math.Max(ch[Ch.ChainLift],column);ch[Ch.Valve]=Math.Max(ch[Ch.Valve],2*inhale);
        }
        /// <summary>A step's lift (0..1) over its progress: up fast, then accelerating into the plant (a thunk: it lands moving).</summary>
        public static float Thunk(float s)
        {
            s=BossMath.Clamp01(s);const float peak=.38f;if(s<peak)return (float)Math.Sin(.5*Math.PI*s/peak);
            float u=(s-peak)/(1-peak);return 1-(float)Math.Pow(u,2.3);
        }
        // ---- the death (inside the 4 s victory hold): the front legs buckle (left, then right), the body drops onto the rim with a
        // bounce and the rear legs splay; it tips forward and pours its last; the foreman slumps late ----
        private void DeathLayer(float d)
        {
            var dv=IronworksBossMoves.Dead.V;
            float buckleL=BossMath.Smoother(d/.5f),buckleR=BossMath.Smoother((d-.35f)/.5f),drop=BossEasing.Eval(BossEase.Settle,BossMath.Clamp01((d-.15f)/1.1f),2.1f);
            for(int c=0;c<Ch.LegBase;c++)ch[c]+=(dv[c]-ch[c])*drop;
            ch[Ch.Tilt]=ch[Ch.Tilt]+(dv[Ch.Tilt]-ch[Ch.Tilt])*BossMath.Smoother((d-.7f)/.9f);
            for(int c=Ch.OpenL;c<Ch.Count;c++){if(c==Ch.Tilt)continue;ch[c]+=(dv[c]-ch[c])*BossMath.Smoother((d-1.0f)/1.6f);}
            ch[Ch.Lever]=BossMath.Lerp(ch[Ch.Lever],-10,BossMath.Smoother((d-1.9f)/.6f));ch[Ch.Lean]=BossMath.Lerp(ch[Ch.Lean],30,BossMath.Smoother((d-2.0f)/.7f));
            // The feet skid out as the legs give (the fronts first): splayed under the dropped body.
            for(int leg=0;leg<4;leg++)
            {
                var home=Ch.Stance(leg);float k=leg==M.FL?buckleL:leg==M.FR?buckleR:BossMath.Smoother((d-.6f)/.7f);int b=Ch.Leg(leg,0);
                var splay=new Vector3(home.X*(leg<2?1.07f:1.1f),0,home.Z*(leg<2?.9f:1.04f));
                ch[b]=1;ch[b+Ch.X]=BossMath.Lerp(home.X,splay.X,k);ch[b+Ch.Y]=0;ch[b+Ch.Z]=BossMath.Lerp(home.Z,splay.Z,k);ch[b+Ch.PL]=0;
            }
            // The heat drains from the bottom up over the hold (the effects and glows read CoolLevel); the lamps flicker twice and die.
            float lamp=d<1.9f?1:d<1.98f?.1f:d<2.1f?.8f:d<2.16f?0:d<2.26f?.5f:BossMath.Clamp01(1-(d-2.26f)/.25f)*.3f;ch[Ch.Lamp]=lamp;ch[Ch.Look]=Math.Max(0,1-d/1.2f);
        }
        private void Additive(in IronworksBossView v,float t,in IronworksBossBeats b)
        {
            // The idle burn: the heat pulses on a 2.4 s period, the yoke breathes 3 cm, the vessel ±1.5 degrees.
            float br=BossBreath.Sample(t,1/2.4f,Seed);ch[Ch.Heat]*=1+.08f*br;ch[Ch.BY]+=.03f*br;ch[Ch.Tilt]+=1.5f*BossBreath.Idle(t,Seed,1,.22f);
            ch[Ch.BR]+=.4f*BossBreath.Idle(t,Seed,2,.17f);ch[Ch.BP]+=.3f*BossBreath.Idle(t,Seed,3,.19f);
            // The blow's column shudders at 6 Hz; the deslag's three recoils; the call's bellow; the shed's shudder; the charge's gallop pitch.
            if(ActIndex>=0)
            {
                var a=NativeIronworksBoss.Actions[ActIndex];float age=Act.Age,hit=Act.HitAt;
                switch(a.Id)
                {
                    case "blow":if(age>hit&&age<hit+2.0f){float k=BossMath.Smooth((age-hit)/.2f)*(1-BossMath.Smooth((age-hit-1.7f)/.3f));ch[Ch.Tilt]+=1.2f*k*(float)Math.Sin(BossMath.Tau*6*age);ch[Ch.BY]+=.02f*k*(float)Math.Sin(BossMath.Tau*6*age+1);}break;
                    case "deslag":for(int i=0;i<3;i++){float k=age-hit-i*.4f;if(k>=0&&k<.5f){float e=(float)Math.Exp(-k*9)*BossMath.Smooth(k/.03f);ch[Ch.Tilt]-=6*e;ch[Ch.BP]+=1.5f*e;ch[Ch.BZ]-=.06f*e;}}break;
                    case "call":if(age>hit&&age<hit+.9f){float k=(float)Math.Exp(-(age-hit)*2.5f);ch[Ch.BR]+=.8f*k*(float)Math.Sin(BossMath.Tau*7*age);}break;
                    case "charge":if(Act.Struck&&age<hit+a.Strike){ch[Ch.BP]+=4*(float)Math.Sin(BossMath.Tau*Legs.Frequency*1*(age-hit)+.5f);}break;
                }
            }
            if(PhaseAct.Active&&PhaseKind==2&&PhaseAct.Age<.7f){float k=BossMath.Smooth(PhaseAct.Age/.15f);ch[Ch.BR]+=1.4f*k*BossBreath.Idle(t,Seed,9,14);ch[Ch.BP]+=1f*k*BossBreath.Idle(t,Seed,10,12);ch[Ch.Tilt]+=2*k*BossBreath.Idle(t,Seed,11,10);}
            // Phase 3: a 2.5 Hz tremor of ±1 degree on the vessel and the yoke.
            if(v.Phase>=3&&!v.Dead(t)){ch[Ch.Tilt]+=1.0f*BossBreath.Idle(t,Seed,12,2.5f);ch[Ch.BR]+=.6f*BossBreath.Idle(t,Seed,13,2.3f);ch[Ch.BP]+=.5f*BossBreath.Idle(t,Seed,14,2.7f);}
            // Broken crust: the shell's heat at the bands shows (the seams' glow rises with each group off).
            int off=0;for(int g=0;g<4;g++)if(!v.Intact(g))off++;ch[Ch.Crack]=Math.Max(ch[Ch.Crack],.25f);heatBands=.3f+.7f*off/4f;
        }
        private float heatBands;
        private void MorphGait(in IronworksBossView v,float t)
        {
            float run=Legs.Run;for(int i=0;i<4;i++)Gait.Legs[i].Phase=Trot[i]+(Gallop[i]-Trot[i])*run;
            // The stance's width and length (the gait steps the feet out to them: the pour's wide tongs, the charge's dug-in bogies).
            for(int i=0;i<4;i++){var home=Ch.Stance(i);Gait.Legs[i].Home=new Vector3(home.X*ch[Ch.StanceX],0,home.Z*(Ch.Front(i)?1:ch[Ch.StanceZ]));}
            Gait.WalkLift=.7f;
        }
        // ---- the vessel on its trunnions: the target plus a damped pendulum, the metal's slosh ----
        private void Vessel(float dt,Quaternion bodyRot,in IronworksBossView v,float t)
        {
            float target=ch[Ch.Tilt];if(float.IsNaN(lastTiltTarget)){lastTiltTarget=target;lastTiltVel=0;swing=swingVel=0;}
            float tv=dt>0?(target-lastTiltTarget)/dt:0,ta=dt>0?(tv-lastTiltVel)/dt:0;lastTiltTarget=target;lastTiltVel=tv;ta=BossMath.Clamp(ta,-4000,4000);
            var bodyVel=Legs.BodyPosition;var vel=dt>0?(bodyVel-lastBodyPos)/dt:Vector3.Zero;lastBodyPos=bodyVel;var acc=dt>0?(vel-lastBodyVel)/dt:Vector3.Zero;lastBodyVel=vel;acc=new Vector3(BossMath.Clamp(acc.X,-40,40),BossMath.Clamp(acc.Y,-40,40),BossMath.Clamp(acc.Z,-40,40));
            var fwd=BossMath.Rotate(bodyRot,Vector3.UnitZ);float along=Vector3.Dot(acc,fwd),up=acc.Y;
            // A pendulum hung on the trunnions: braking (along < 0) swings the mouth forward, a drop (up < 0) and the target's own
            // acceleration (its inertia) kick it; it rings at 3.2 rad/s and settles (damping .25). Sub-stepped: the same at any rate.
            const float w=3.2f,z=.25f;int n=Math.Max(1,(int)Math.Ceiling(dt/(1/240f)));float h=dt/n;
            float drive=-along*25f+up*3f-ta*.03f;
            for(int i=0;i<n;i++){float a=-w*w*swing-2*z*w*swingVel+drive;swingVel+=a*h;swing+=swingVel*h;}
            swing=BossMath.Clamp(swing,-24,24);Tilt=target+swing;TiltRate=tv+swingVel;
            // The metal: its surface lags the vessel's swing and the body's acceleration (a 2D spring), spilling past 12 degrees.
            float sx=BossMath.Clamp(-(swingVel*.06f+tv*.012f)-along*1.4f,-30,30),sz=BossMath.Clamp(Vector3.Dot(acc,BossMath.Rotate(bodyRot,Vector3.UnitX))*1.4f,-30,30);
            var s=slosh.Step(new Vector3(sx,0,sz),dt);SloshX=BossMath.Clamp(s.X,-25,25);SloshZ=BossMath.Clamp(s.Z,-25,25);
            float mag=(float)Math.Sqrt(SloshX*SloshX+SloshZ*SloshZ);Spill=BossMath.Clamp01((mag-12)/10)+BossMath.Clamp01((Tilt-55)/25);
        }
        private Vector3 lastBodyPos;
        // ---- the lamps: rigid in the yoke (their glow per joint), the beams aiming with a lag at the target ----
        private void Lamps(Vector3 bodyPos,Quaternion bodyRot,float dt,bool hasTarget,Vector3 target)
        {
            LampAtL=bodyPos+BossMath.Rotate(bodyRot,L.LampOrigin(L.LampL));LampAtR=bodyPos+BossMath.Rotate(bodyRot,L.LampOrigin(L.LampR));
            Pose.Set(L.LampL,LampAtL,bodyRot);Pose.Set(L.LampR,LampAtR,bodyRot);
            var mid=(LampAtL+LampAtR)*.5f;Eyes.Step(mid,Follow.Yaw,hasTarget&&ch[Ch.Look]>.3f,target,dt);
            float wob=beam.Step(Eyes.Yaw,dt);var aim=Quaternion.Normalize(bodyRot*BossMath.AxisAngle(Vector3.UnitY,wob)*BossMath.AxisAngle(Vector3.UnitX,.12f+Eyes.Pitch*.8f));
            BeamL=BeamR=BossMath.Rotate(aim,Vector3.UnitZ);
        }
        // ---- the pulpit, the levers, the foreman ----
        private void Pulpit(float t,float dt,Vector3 bodyPos,Quaternion bodyRot,in IronworksBossView v,IBossGround ground)
        {
            // Torn in phase 3 (its part's time, or the break's), swinging down behind on its chains: a pendulum that overshoots.
            bool torn=!v.Intact(IronworksBossView.PartPulpit)||v.Phase>=3;
            if(torn&&float.IsNaN(PulpitAt)){float at=v.PartAt(IronworksBossView.PartPulpit);PulpitAt=BossAt(at)?at:BossAt(v.phaseAt)&&v.Phase>=3?v.phaseAt+.9f:t;}
            if(!torn)PulpitAt=float.NaN;
            float tear=torn&&t>=PulpitAt?1:0;if(torn&&t-PulpitAt>6)pulpitSwing.Reset(1);PulpitTear=BossMath.Clamp(pulpitSwing.Step(tear,dt),-.2f,1.3f);
            // Torn off its front mounts it drops back and down behind the arch, hanging nose down on two chains from the arch's top:
            // the drop overshoots as the chains snap taut and it swings (the pendulum), rolling a little.
            var rest=M.PulpitRest;var rot=bodyRot;Vector3 pos=bodyPos+BossMath.Rotate(bodyRot,rest);float k=PulpitTear;
            if(Math.Abs(k)>1e-4f)
            {
                var q=Quaternion.Normalize(BossMath.AxisAngle(Vector3.UnitX,34*k*Deg)*BossMath.AxisAngle(Vector3.UnitZ,12*k*Deg));
                pos=bodyPos+BossMath.Rotate(bodyRot,rest+new Vector3(.12f*k,-1.6f*k,-.95f*k));rot=Quaternion.Normalize(bodyRot*q);
            }
            Pose.Set(M.Pulpit,pos,rot);
            for(int s=0;s<2;s++){float x=s==0?-.95f:.95f;TearTop[s]=bodyPos+BossMath.Rotate(bodyRot,new Vector3(x,2.12f,-3.35f));TearEnd[s]=pos+BossMath.Rotate(rot,new Vector3(x,1.04f,-.58f));}
            // The levers lead the tilt: hauled with the action's lever, plus the tilt's speed (he works it), on a quick lag.
            float leverGoal=ch[Ch.Lever]+BossMath.Clamp(TiltRate*.08f,-18,18);float lever=leverLag.Step(leverGoal,dt);
            var piv=M.LeverPivot;var lq=Quaternion.Normalize(rot*BossMath.AxisAngle(Vector3.UnitX,-lever*Deg));
            // The lever's joint frame is the pulpit's turned about the pivot: origin moves by the pivot's offset.
            var leverPos=pos+BossMath.Rotate(rot,piv)-BossMath.Rotate(lq,piv);Pose.Set(M.Lever,leverPos,lq);
            for(int s=0;s<2;s++)knob[s]=pos+BossMath.Rotate(rot,piv)+BossMath.Rotate(lq,new Vector3(s==0?-.2f:.2f,.9f,-.08f));
            // The foreman: thrown about by the body's acceleration (second order), leaning with the action, bracing.
            var accel=Legs.BodyPosition-lastForeman;lastForeman=Legs.BodyPosition;float jerk=dt>0?Vector3.Dot(accel/dt-foremanVel,BossMath.Rotate(bodyRot,Vector3.UnitZ)):0;foremanVel=dt>0?accel/dt:Vector3.Zero;
            float thrown=lean.Step(ch[Ch.Lean]-BossMath.Clamp(jerk*1.2f,-25,25),dt);
            Quaternion frot;Vector3 fpos;
            if(PulpitTear>.25f&&!v.Dead(t))
            {
                // Dangling from his belt chain off the torn pulpit's rail: hung below its end, swinging.
                var anchor=pos+BossMath.Rotate(rot,new Vector3(.3f,.9f,-.62f));Belt.Step(anchor,rot,dt,ground);var tip=Belt.Point[Belt.Point.Length-1];var dir=tip-anchor;
                var down=dir.LengthSquared()>1e-6f?Vector3.Normalize(dir):-Vector3.UnitY;frot=Quaternion.Normalize(BossMath.FromTo(Vector3.UnitY,-down)*BossMath.Yaw(Follow.Yaw+.4f));
                fpos=tip-BossMath.Rotate(frot,new Vector3(0,1.25f,0));
                float hang=BossMath.Smoother((PulpitTear-.25f)/.5f);var standPos=pos+BossMath.Rotate(rot,M.ForemanFeet);var standRot=Quaternion.Normalize(rot*BossMath.AxisAngle(Vector3.UnitX,thrown*Deg));
                fpos=Vector3.Lerp(standPos,fpos,hang);frot=BossMath.Slerp(standRot,frot,hang);
            }
            else{Belt.Reset(pos+BossMath.Rotate(rot,new Vector3(.3f,.9f,-.62f)),rot);fpos=pos+BossMath.Rotate(rot,M.ForemanFeet);frot=Quaternion.Normalize(rot*BossMath.AxisAngle(Vector3.UnitX,thrown*Deg));}
            Pose.Set(M.Foreman,fpos,frot);
            // Chest: his breath (a slow rise), bracing (a crouch); head: the look-at with a lag; the visor rides the head.
            float breath=BossBreath.Sample(t,.3f,Seed^5u);var chestRot=Quaternion.Normalize(frot*BossMath.AxisAngle(Vector3.UnitX,(3*breath+8*ch[Ch.Brace])*Deg));
            var chestPos=fpos+BossMath.Rotate(frot,L.Waist+new Vector3(0,.012f*breath-.08f*ch[Ch.Brace],0));Pose.Set(L.Chest,chestPos,chestRot);
            var headPos=chestPos+BossMath.Rotate(chestRot,L.Neck);Look.Step(headPos,Follow.Yaw,lookTarget&&ch[Ch.Look]>.2f,lookAt,dt);
            var lag=headLag.Step(new Vector3(Look.Pitch*ch[Ch.Look],Look.Yaw*ch[Ch.Look],0),dt);
            var headRot=Quaternion.Normalize(chestRot*BossMath.AxisAngle(Vector3.UnitY,lag.Y*.8f)*BossMath.AxisAngle(Vector3.UnitX,lag.X*.7f-1.5f*breath*Deg));
            if(v.Dead(t)){float slump=BossMath.Smoother((DeathAge-2.1f)/.8f);headRot=BossMath.Slerp(headRot,Quaternion.Normalize(chestRot*BossMath.AxisAngle(Vector3.UnitX,38*Deg)),slump);}
            Pose.Set(L.Head,headPos,headRot);Pose.Set(L.Visor,headPos+BossMath.Rotate(headRot,L.VisorAt),headRot);HeadAt=headPos;
            // The arms: shoulder to the lever's knob (down the rod when out of reach), the elbows out and back.
            for(int s=0;s<2;s++)
            {
                int side=s==0?-1:1;var sh=chestPos+BossMath.Rotate(chestRot,new Vector3(side*Math.Abs(L.Shoulder.X),L.Shoulder.Y,L.Shoulder.Z));
                var grip=knob[s];float reach=(L.UpperArm+L.Forearm)*.97f;var rod=pos+BossMath.Rotate(rot,piv);
                if(Vector3.Distance(sh,grip)>reach){for(int g=0;g<6;g++){grip=Vector3.Lerp(grip,rod,.12f);if(Vector3.Distance(sh,grip)<=reach)break;}}
                if(v.Dead(t)||PulpitTear>.25f){float limp=v.Dead(t)?BossMath.Smoother((DeathAge-2.0f)/.7f):BossMath.Smoother((PulpitTear-.25f)/.4f);grip=Vector3.Lerp(grip,sh+new Vector3(0,-.68f,0)+BossMath.Rotate(frot,new Vector3(side*.1f,0,.1f)),limp);}
                var pole=BossMath.Rotate(chestRot,Vector3.Normalize(new Vector3(side*.8f,-.4f,-.6f)));
                var elbow=BossIk.TwoBone(sh,grip,L.UpperArm,L.Forearm,pole,out var hand,.04f);
                Pose.Set(s==0?L.ArmUL:L.ArmUR,sh,BossMath.LookRotation(elbow-sh,BossMath.Rotate(chestRot,Vector3.UnitY)));
                Pose.Set(s==0?L.ArmFL:L.ArmFR,elbow,BossMath.LookRotation(hand-elbow,BossMath.Rotate(chestRot,Vector3.UnitY)));
            }
            // The blast valve spins with the channel (turns a second).
            valve+=ch[Ch.Valve]*BossMath.Tau*dt;valve%=BossMath.Tau*64;Pose.Set(L.Valve,pos+BossMath.Rotate(rot,L.ValveAt),Quaternion.Normalize(rot*BossMath.AxisAngle(Vector3.UnitZ,valve)));
            WhistleAt=pos+BossMath.Rotate(rot,L.Whistle);
            // The levers' pull chains hang off the knobs.
            for(int s=0;s<2;s++)LeverChains[s].Step(knob[s],lq,dt,ground);
            Pose.Set(M.ForemanArms,fpos,frot);
        }
        /// <summary>The torn pulpit's two chains: from the arch's top to its rear railing (world), drawn while torn.</summary>
        public readonly Vector3[] TearTop=new Vector3[2],TearEnd=new Vector3[2];
        private readonly Vector3[] knob=new Vector3[2];private Vector3 lastForeman,foremanVel;private bool lookTarget;private Vector3 lookAt;
        // ---- the legs: the gait's planted feet or the channels' targets, the concept's coxa and two-bone IK ----
        private void SolveLegs(float dt,IBossGround ground,Quaternion facing,float yaw)
        {
            var yokePos=Legs.BodyPosition;var yokeRot=Legs.BodyRotation;var up=BossMath.Rotate(yokeRot,Vector3.UnitY);var xAxis=BossMath.Rotate(yokeRot,Vector3.UnitX);
            for(int i=0;i<4;i++)
            {
                int b=Ch.Leg(i,0);float w=BossMath.Clamp01(ch[b]);var gait=Legs.Foot[i];
                // A swinging foot: the gait's path, its lift replaced by the thunk (accelerating into the plant).
                if(!Legs.Planted[i]&&Legs.Swing[i]>=0){float lift=BossMath.Lerp(Gait.WalkLift,Gait.RunLift,Legs.Run);gait.Y=ground.Height(gait.X,gait.Z)+M.Ankle(i)+lift*Thunk(Legs.Swing[i]);}
                var tl=new Vector3(ch[b+Ch.X],ch[b+Ch.Y],ch[b+Ch.Z]);var tw=Root+BossMath.Rotate(facing,tl);tw.Y=ground.Height(tw.X,tw.Z)+tl.Y+M.Ankle(i);
                // A record-aimed leg (the stomp's landing, the grab's reach and hold) bends its target toward the record's point.
                if(i==ActLeg&&ActIndex>=0)tw=AimLeg(i,tw,ground);
                var ankle=Vector3.Lerp(gait,tw,w);legTarget[i]=ankle;legWeight[i]=w;
                if((plantNow&(1<<i))!=0){var flat=new Vector3(ankle.X,ground.Height(ankle.X,ankle.Z)+M.Ankle(i),ankle.Z);Legs.Plant(i,flat,yaw);}
                var hip=yokePos+BossMath.Rotate(yokeRot,M.Hip(i));
                LegIk(hip,ankle,up,xAxis*Ch.Side(i),out var coxa,out var femur,out var tibia,out var foot,out var knee);
                Pose.Set(M.LegJoint(i,M.Coxa),hip,coxa);Pose.Set(M.LegJoint(i,M.Femur),femurPivot,femur);Pose.Set(M.LegJoint(i,M.Tibia),knee,tibia);
                // The foot: level, facing the gait's planted yaw (a planted tong keeps the heading it landed with), pitched by the channel.
                var gy=Legs.FootRotation[i];var fwdFoot=BossMath.Rotate(gy,Vector3.UnitZ);fwdFoot.Y=0;if(fwdFoot.LengthSquared()<1e-6f)fwdFoot=BossMath.Forward(yaw);
                var footYaw=BossMath.YawOf(fwdFoot);var chanYaw=yaw;float fy=w>0?BossMath.WrapPi(footYaw+BossMath.WrapPi(chanYaw-footYaw)*w):footYaw;
                var frot=Quaternion.Normalize(BossMath.Yaw(fy)*BossMath.AxisAngle(Vector3.UnitX,-ch[b+Ch.FP]*Deg));
                Pose.Set(M.LegJoint(i,M.Foot),foot,frot);
                // The tongs' jaws open about their hinge.
                if(i<2)
                {
                    float open=(i==M.FL?ch[Ch.OpenL]:ch[Ch.OpenR])*Deg;var hingeL=new Vector3(0,M.JawHinge,0);var hingeW=foot+BossMath.Rotate(frot,hingeL);
                    var q0=Quaternion.Normalize(frot*BossMath.AxisAngle(Vector3.UnitX,-open));var q1=Quaternion.Normalize(frot*BossMath.AxisAngle(Vector3.UnitX,open));
                    Pose.Set(M.Jaw(i,0),hingeW-BossMath.Rotate(q0,hingeL),q0);Pose.Set(M.Jaw(i,1),hingeW-BossMath.Rotate(q1,hingeL),q1);
                    TongTip[i]=foot+BossMath.Rotate(frot,new Vector3(0,M.JawHinge-2*M.JawRadius,0));
                }
            }
            plantNow=0;
        }
        private Vector3 femurPivot;
        // The record's point for the stomp's landing and the grab's reach: the target channel's point pulled toward it (within reach).
        private Vector3 AimLeg(int i,Vector3 tw,IBossGround ground)
        {
            if(float.IsNaN(aimX))return tw;var a=NativeIronworksBoss.Actions[ActIndex];var aim=new Vector3(aimX,0,aimZ);
            var hip=Legs.BodyPosition+BossMath.Rotate(Legs.BodyRotation,M.Hip(i));float reach=(M.FemurLength+M.TibiaLength)*.92f;
            if(a.Id=="stomp")
            {
                // The arc rides over the landing (the phrase's lift), the plant lands on it (within the leg's reach).
                var flat=new Vector3(aim.X,0,aim.Z);var hf=new Vector3(hip.X,0,hip.Z);var d=flat-hf;float r2=reach*.8f;if(d.Length()>r2)flat=hf+Vector3.Normalize(d)*r2;
                float lift=tw.Y-ground.Height(tw.X,tw.Z)-M.Ankle(i);float k=BossMath.Clamp01(Act.Age/.35f);var horiz=Vector3.Lerp(new Vector3(tw.X,0,tw.Z),flat,.85f*k);
                return new Vector3(horiz.X,ground.Height(horiz.X,horiz.Z)+M.Ankle(i)+lift,horiz.Z);
            }
            if(a.Id=="grab")
            {
                // Reach and snap at the target's point (1 m up: the pilot's chest), hold and shake it where the phrase lifts it.
                float age=Act.Age;float toward=BossMath.Smoother((age-.25f)/.6f)*(1-BossMath.Smoother((age-a.Windup-a.Strike-.3f)/.5f));
                var p=new Vector3(aim.X,ground.Height(aim.X,aim.Z)+1.0f,aim.Z);var d=p-hip;if(d.Length()>reach)p=hip+Vector3.Normalize(d)*reach;
                return Vector3.Lerp(tw,p,toward);
            }
            return tw;
        }
        private float aimX=float.NaN,aimZ=float.NaN;
        // The concept's leg (NativeIronworksBossModel.Solve's Leg): the coxa turned toward the ankle about the yoke's up, the femur and
        // tibia by the law of cosines with the knee bent up and out; the reach softened near full extension (no straightening pop).
        private void LegIk(Vector3 hip,Vector3 target,Vector3 up,Vector3 outward,out Quaternion coxa,out Quaternion femur,out Quaternion tibia,out Vector3 ankle,out Vector3 knee)
        {
            var d=target-hip;var o=d-up*Vector3.Dot(d,up);if(o.LengthSquared()<1e-6f)o=outward-up*Vector3.Dot(outward,up);o=Vector3.Normalize(o);
            coxa=BossMath.FromBasis(Vector3.Cross(up,o),up,o);
            var pivot=hip+o*M.CoxaReach;femurPivot=pivot;var c=target-pivot;float dist=c.Length();var cd=dist>1e-6f?c/dist:-up;
            float lo=Math.Abs(M.FemurLength-M.TibiaLength)+.01f,hi=M.FemurLength+M.TibiaLength-.01f,band=.06f*hi,start=hi-band;
            float dc=dist>start?start+band*(1-(float)Math.Exp(-(dist-start)/band)):dist;dc=Math.Max(lo,Math.Min(hi,dc));
            float cosA=(M.FemurLength*M.FemurLength+dc*dc-M.TibiaLength*M.TibiaLength)/(2*M.FemurLength*dc);cosA=Math.Max(-1,Math.Min(1,cosA));float sinA=(float)Math.Sqrt(1-cosA*cosA);
            var bend=up-cd*Vector3.Dot(up,cd);bend=bend.LengthSquared()>1e-8f?Vector3.Normalize(bend):o;
            knee=pivot+(cd*cosA+bend*sinA)*M.FemurLength;ankle=pivot+cd*dc;
            var nrm=Vector3.Normalize(Vector3.Cross(o,up));
            var fz=Vector3.Normalize(knee-pivot);var fy=Vector3.Normalize(Vector3.Cross(nrm,fz));femur=BossMath.FromBasis(Vector3.Cross(fy,fz),fy,fz);
            var tz=Vector3.Normalize(ankle-knee);var ty=Vector3.Normalize(Vector3.Cross(nrm,tz));tibia=BossMath.FromBasis(Vector3.Cross(ty,tz),ty,tz);
        }
        /// <summary>Whether leg i's foot is planted by the gait and not taken by a channel (the planted-foot checks measure these).</summary>
        public bool PlantedFree(int i)=>Legs.Planted[i]&&legWeight[i]<1e-4f&&!Arriving&&float.IsNaN(DeathAge);
        public Vector3 Foot(int i)=>Pose.Position[M.LegJoint(i,M.Foot)];
        public Vector3 GaitFoot(int i)=>Legs.Foot[i];
        // ---- the crust: on, flying on its own arc and tumble, lying as rubble ----
        private void Crust(float t,in IronworksBossView v,Vector3 bodyPos,Quaternion vesselRot)
        {
            for(int g=0;g<4;g++)
            {
                float at=v.PartAt(g);bool off=!v.Intact(g);if(!off)offSeen[g]=float.NaN;else if(float.IsNaN(offSeen[g]))offSeen[g]=t;
                if(off&&!BossAt(at))at=BossAt(v.phaseAt)&&v.Phase>=2?v.phaseAt+NativeIronworksBoss.Find("shed").Windup+.1f*g:offSeen[g];
                for(int c=0;c<L.Chunks;c++)
                {
                    int j=L.Crust(g,c),k=g*3+c;float start=at+.04f*c;
                    if(!off||t<start){ChunkState[k]=0;ChunkAt[k]=float.NaN;Pose.Set(j,bodyPos,vesselRot);continue;}
                    if(ChunkState[k]==0||!ChunkAt[k].Equals(start))
                    {
                        ChunkAt[k]=start;ChunkState[k]=1;
                        // Where it flies from: as drawn now, unless the fling is long past (a late joiner: it lies where it would have).
                        flingFrom[k]=bodyPos+BossMath.Rotate(vesselRot,L.ChunkCentre[k]);flingRot[k]=vesselRot;
                        var outDir=BossMath.Rotate(vesselRot,new Vector3(L.ChunkCentre[k].X,0,L.ChunkCentre[k].Z));outDir.Y=0;outDir=outDir.LengthSquared()>1e-6f?Vector3.Normalize(outDir):BossMath.Forward(Yaw);
                        float sp=3.0f+1.4f*BossMath.Hash(Seed,k*7+1);flingVel[k]=outDir*sp+new Vector3(0,2.2f+1.6f*BossMath.Hash(Seed,k*7+2)-.6f*c,0);
                        var axis=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,outDir)+new Vector3(0,.3f*(BossMath.Hash(Seed,k*7+3)-.5f),0));flingSpin[k]=axis*(1.6f+1.6f*BossMath.Hash(Seed,k*7+4));
                    }
                    Fling(k,t-start,out var p,out var q);if(t-start>2.5f)ChunkState[k]=2;
                    // The geometry is in the vessel frame: place the frame so the chunk's centre lands at p, turned by q.
                    var rot=Quaternion.Normalize(q*flingRot[k]);Pose.Set(j,p-BossMath.Rotate(rot,L.ChunkCentre[k]),rot);
                }
            }
        }
        private readonly float[] offSeen={float.NaN,float.NaN,float.NaN,float.NaN};
        private readonly Vector3[] flingFrom=new Vector3[12],flingVel=new Vector3[12],flingSpin=new Vector3[12];private readonly Quaternion[] flingRot=new Quaternion[12];
        /// <summary>Chunk k's flight `s` seconds after it left: ballistic and tumbling to the floor, a bounce, a short slide, at rest.</summary>
        public void Fling(int k,float s,out Vector3 pos,out Quaternion rot)
        {
            s=Math.Max(0,s);var p0=flingFrom[k];var v0=flingVel[k];float rest=L.ChunkRadius[k]*.32f;
            float g=9.8f,disc=v0.Y*v0.Y+2*g*Math.Max(0,p0.Y-rest),tl=(v0.Y+(float)Math.Sqrt(disc))/g;
            var flat=new Vector3(v0.X,0,v0.Z);
            if(s<tl){pos=p0+flat*s+new Vector3(0,v0.Y*s-.5f*g*s*s,0);rot=BossMath.Exp(flingSpin[k]*s);return;}
            float r=s-tl,bounceV=Math.Abs(v0.Y-g*tl)*.22f,tb=2*bounceV/g,slide=1-(float)Math.Exp(-r*3.2f);
            float hop=r<tb?bounceV*r-.5f*g*r*r:0;var land=p0+flat*tl;var dir=flat.LengthSquared()>1e-6f?Vector3.Normalize(flat):Vector3.UnitX;
            pos=new Vector3(land.X,rest+Math.Max(0,hop),land.Z)+dir*(.45f*slide);
            var spin=flingSpin[k]*(tl+.35f*(1-(float)Math.Exp(-r*4)));rot=BossMath.Exp(spin);
        }
        // ---- chains ----
        private void Secondary(float t,float dt,IBossGround ground,Vector3 bodyPos,Quaternion bodyRot)
        {
            float lift=ch[Ch.ChainLift];
            for(int c=0;c<M.Chains;c++)
            {
                var chain=Chains[c];chain.Def.Gravity=9.8f*(1-1.35f*lift);chain.Def.Damping=.45f+.6f*lift;
                var anchor=bodyPos+BossMath.Rotate(bodyRot,M.ChainAnchor[c]);
                if(!chainReady)chain.Reset(anchor,bodyRot);else chain.Step(anchor,bodyRot,dt,ground);
                // The updraft's flutter (the blow): a little noise into the tips.
                if(lift>.05f){int n=chain.Point.Length-1;chain.Point[n]+=new Vector3(BossBreath.Idle(t,Seed,30+c,7),0,BossBreath.Idle(t,Seed,40+c,6))*(.004f*lift);}
            }
            chainReady=true;
        }
        // ---- the glows, the hidden joints and the points the effects read ----
        private void Outputs(in IronworksBossView v,float t,Vector3 bodyPos,Quaternion bodyRot,Quaternion vesselRot)
        {
            float d=DeathAge;bool dead=!float.IsNaN(d);
            CoolLevel=dead?BossMath.Smoother((d-1.0f)/2.6f):0;float k=1-CoolLevel;
            HeatLevel=Math.Max(0,ch[Ch.Heat])*(1+.6f*Spill);BlowLevel=Math.Max(0,ch[Ch.Blow]);LampLevel=BossMath.Clamp01(ch[Ch.Lamp]);
            SeamLevel=v.Phase>=3&&!dead?BossMath.Smooth((t-(BossAt(v.phaseAt)?v.phaseAt+1.0f:t-10))/.8f):dead?Math.Max(0,1-CoolLevel*1.4f)*(v.Phase>=3?1:0):0;
            VisorLevel=Arriving?(ArrivalAge>=NativeIronworksBossBeats.Current.Flare?1:0):dead?Math.Max(0,1-BossMath.Smoother((d-2.2f)/.5f)):1;
            CrackLevel=ch[Ch.Crack];
            for(int j=0;j<Count;j++){Glow[j]=.25f+.75f*k;Hidden[j]=false;}
            Hidden[M.Root]=true;Hidden[M.ForemanArms]=true;Hidden[M.Seams]=SeamLevel<=1e-3f;
            Glow[M.VesselHeat]=HeatLevel*k*(.6f+.6f*heatBands);Glow[L.Metal]=HeatLevel*(1-CoolLevel*1.15f);if(Glow[L.Metal]<0)Glow[L.Metal]=0;
            Glow[M.WindBox]=BlowLevel*k;Glow[M.Seams]=SeamLevel*1.2f;Glow[L.LampL]=Glow[L.LampR]=LampLevel;Glow[L.Visor]=VisorLevel;
            for(int g=0;g<4;g++)for(int c=0;c<L.Chunks;c++){int j=L.Crust(g,c);Glow[j]=ChunkState[g*3+c]==2?.12f:CrackLevel*(.6f+.4f*HeatLevel)*k;}
            // The points.
            Mouth=bodyPos+BossMath.Rotate(vesselRot,new Vector3(0,M.MouthY,M.MouthZ));MouthAxis=BossMath.Rotate(vesselRot,Vector3.Normalize(new Vector3(0,1,.18f)));
            GrateAt=bodyPos+BossMath.Rotate(vesselRot,L.Grate);GrateAxis=BossMath.Rotate(vesselRot,-Vector3.UnitY);
        }
        /// <summary>A chain's link matrices (the runtime and the renders draw the link prop at each): its points and the end.</summary>
        public BossChain Chain(int c)=>Chains[c];
    }
}

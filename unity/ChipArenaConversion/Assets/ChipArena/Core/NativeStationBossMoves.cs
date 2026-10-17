using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE 6:47's pose channels (doc section 7): what an action, the arrival, a phase change, a stagger or the death sets over the
    /// gait. Body offsets are in the root's frame (x right, y up, z the nose) in metres, angles in degrees; each leg has an
    /// override weight (0: the gait plants it, 1: the channel's target), its target (root frame), a hand mode (1 palm down on the
    /// floor, 0 the fingers carrying on along the shin) and its knee's pole; the Stationmaster's joints are local Euler angles
    /// (the concept model's R(pitch, yaw, roll)); the parts are the roof (degrees), the pantograph's raise, the doors' and grab
    /// arms' opening, the lamp's flare, the horns' and the Heart's glow, the coupling chain's lift, the doors' shudder and the
    /// look-at's weight.
    /// </summary>
    public static class StationBossCh
    {
        public const int BX=0,BY=1,BZ=2,BP=3,BW=4,BR=5,LegBase=6,LegStride=8;
        public const int W=0,X=1,Y=2,Z=3,F=4,PX=5,PY=6,PZ=7;
        public static int Leg(int leg,int field)=>LegBase+leg*LegStride+field;
        public const int SRise=54,SChP=55,SChY=56,SChR=57,SHdP=58,SHdY=59,SHdR=60,SHatP=61,SHatR=62,SRaP=63,SRaY=64,SRaR=65,SRfP=66,SLaP=67,SLaY=68,SLaR=69,SLfP=70;
        public const int Roof=71,Panto=72,DoorL=73,DoorR=74,GrabL=75,GrabR=76,Lamp=77,Horn=78,Heart=79,ChainLift=80,Shudder=81,Look=82,Count=83;
        /// <summary>The neutral values: the body on its gait, the Stationmaster holding the watch and the bell (the concept's Neutral),
        /// everything folded and shut, the lamp at its steady level.</summary>
        public static readonly float[] Neutral=MakeNeutral();
        /// <summary>The mirror of each channel (the boss's left for its right) and whether its value flips sign.</summary>
        public static readonly int[] MirrorOf=new int[Count];public static readonly bool[] MirrorFlip=new bool[Count];
        static StationBossCh()
        {
            for(int c=0;c<Count;c++)MirrorOf[c]=c;
            for(int leg=0;leg<6;leg++){int o=leg^1;for(int f=0;f<LegStride;f++){MirrorOf[Leg(leg,f)]=Leg(o,f);MirrorFlip[Leg(leg,f)]=f==X||f==PX;}}
            foreach(int c in new[]{BX,BW,BR,SChY,SChR,SHdY,SHdR,SHatR})MirrorFlip[c]=true;
            MirrorOf[DoorL]=DoorR;MirrorOf[DoorR]=DoorL;MirrorOf[GrabL]=GrabR;MirrorOf[GrabR]=GrabL;
        }
        private static float[] MakeNeutral()
        {
            var n=new float[Count];
            for(int leg=0;leg<6;leg++){var p=DefaultPole(leg);var s=NativeStationBossModel.Stance(leg);n[Leg(leg,X)]=s.X;n[Leg(leg,Y)]=s.Y;n[Leg(leg,Z)]=s.Z;n[Leg(leg,F)]=1;n[Leg(leg,PX)]=p.X;n[Leg(leg,PY)]=p.Y;n[Leg(leg,PZ)]=p.Z;}
            n[SChP]=4;n[SHdP]=-4;n[SHatR]=4;n[SRaP]=-35;n[SRaR]=12;n[SRfP]=-75;n[SLaP]=-10;n[SLaR]=-28;n[SLfP]=-35;n[Lamp]=1;n[Look]=1;
            return n;
        }
        /// <summary>A leg's knee pole on the gait (the concept's: up and out; out and back for the front arms).</summary>
        public static Vector3 DefaultPole(int leg)
        {
            int s=NativeStationBossModel.Side(leg),r=NativeStationBossModel.Row(leg);
            return r==0?Vector3.Normalize(new Vector3(s*.85f,.6f,-.15f)):Vector3.Normalize(new Vector3(s*.45f,1,r==2?-.2f:0));
        }
    }

    /// <summary>One authored pose as channel values (copied from the neutral, then set).</summary>
    public sealed class StationBossSnap
    {
        public readonly float[] V=(float[])StationBossCh.Neutral.Clone();
        public StationBossSnap Set(int c,float v){V[c]=v;return this;}
        /// <summary>The body's lean (the concept's Lean: offset in the root's frame, pitch, yaw, roll in degrees).</summary>
        public StationBossSnap Lean(float x,float y,float z,float pitch,float yaw=0,float roll=0){V[StationBossCh.BX]=x;V[StationBossCh.BY]=y;V[StationBossCh.BZ]=z;V[StationBossCh.BP]=pitch;V[StationBossCh.BW]=yaw;V[StationBossCh.BR]=roll;return this;}
        /// <summary>A hand planted palm down at (x, z) on the floor (root frame), owned by the channel rather than the gait.</summary>
        public StationBossSnap Plant(int leg,float x,float z,float weight=1){int b=StationBossCh.Leg(leg,0);V[b]=weight;V[b+1]=x;V[b+2]=NativeStationBossModel.PalmLift;V[b+3]=z;V[b+4]=1;return this;}
        /// <summary>A limb reaching for (x, y, z) (root frame), the knee bending toward the pole, the fingers along the shin.</summary>
        public StationBossSnap Reach(int leg,float x,float y,float z,float px,float py,float pz,float weight=1)
        {int b=StationBossCh.Leg(leg,0);var p=Vector3.Normalize(new Vector3(px,py,pz));V[b]=weight;V[b+1]=x;V[b+2]=y;V[b+3]=z;V[b+4]=0;V[b+5]=p.X;V[b+6]=p.Y;V[b+7]=p.Z;return this;}
        /// <summary>A leg folded or splayed palm down at a point with its own pole (the rolling arrival, the death).</summary>
        public StationBossSnap Fold(int leg,float x,float y,float z,float px,float py,float pz)
        {int b=StationBossCh.Leg(leg,0);var p=Vector3.Normalize(new Vector3(px,py,pz));V[b]=1;V[b+1]=x;V[b+2]=y;V[b+3]=z;V[b+4]=1;V[b+5]=p.X;V[b+6]=p.Y;V[b+7]=p.Z;return this;}
        /// <summary>The Stationmaster: chest and head (pitch, yaw, roll), the watch arm (shoulder pitch, yaw, roll; forearm pitch),
        /// the bell arm.</summary>
        public StationBossSnap Chest(float p,float y=0,float r=0){V[StationBossCh.SChP]=p;V[StationBossCh.SChY]=y;V[StationBossCh.SChR]=r;return this;}
        public StationBossSnap Head(float p,float y=0,float r=0){V[StationBossCh.SHdP]=p;V[StationBossCh.SHdY]=y;V[StationBossCh.SHdR]=r;return this;}
        public StationBossSnap Watch(float p,float y,float r,float fore){V[StationBossCh.SRaP]=p;V[StationBossCh.SRaY]=y;V[StationBossCh.SRaR]=r;V[StationBossCh.SRfP]=fore;return this;}
        public StationBossSnap Bell(float p,float y,float r,float fore){V[StationBossCh.SLaP]=p;V[StationBossCh.SLaY]=y;V[StationBossCh.SLaR]=r;V[StationBossCh.SLfP]=fore;return this;}
        public StationBossSnap Copy(){var s=new StationBossSnap();Array.Copy(V,s.V,V.Length);return s;}
    }

    /// <summary>
    /// The moves as phrases on the shared action timeline (doc sections 5 and 7): one <see cref="BossActionDef"/> per move with
    /// its key poses. Each phrase's phases sum to the move table's timings (anticipation, strike, follow-through, recover), and the
    /// first contact phase starts at the move's anticipation: the authority's `hitAt`, so what is telegraphed is what hits; the
    /// timeline stretches the phases before contact to the record's hit and those after to its length. Key k of a phrase is the
    /// pose its progress reaches at k (0 the neutral; the last the neutral again). Moves that strike to one side are authored to
    /// the boss's right (Buffer Swipe) or left (All Aboard) and mirrored for the other.
    /// Also: the two phase changes (the barrier smash, the board torn down), the stagger, and the arrival's and the death's poses.
    /// </summary>
    public static class StationBossMoves
    {
        public static readonly BossActionDef[] Defs=new BossActionDef[NativeStationBoss.Moves.Length];
        public static readonly StationBossSnap[][] Keys=new StationBossSnap[NativeStationBoss.Moves.Length][];
        /// <summary>The side each move was authored to (+1 right, -1 left, 0 symmetric): it is mirrored toward its target's side.</summary>
        public static readonly int[] Side=new int[NativeStationBoss.Moves.Length];
        public static BossActionDef Smash,Tear,Stagger,Flinch;public static StationBossSnap[] SmashKeys,TearKeys,StaggerKeys,FlinchKeys;
        /// <summary>The arrival's poses: rolling on the rails with the legs folded, rearing through the gate, the plant.</summary>
        public static readonly StationBossSnap Stand=new StationBossSnap(),Rolling=MakeRolling(),Arrival=MakeArrival(),Risen=new StationBossSnap().Watch(-150,0,20,-15).Head(-14).Chest(-4);
        /// <summary>The death's pose (the concept's Death): the legs splayed, the body on its wheels, the Stationmaster slumped.</summary>
        public static readonly StationBossSnap Death=MakeDeath();
        private static BossPhase P(BossPhaseKind k,float s,float to,BossEase e,float shape=0)=>new BossPhase(k,s,to,e,shape);
        private const BossPhaseKind A=BossPhaseKind.Anticipation,S=BossPhaseKind.Strike,C=BossPhaseKind.Contact,F=BossPhaseKind.FollowThrough,R=BossPhaseKind.Recover;
        private static int Index(string id){var m=NativeStationBoss.Moves;for(int i=0;i<m.Length;i++)if(m[i].Id==id)return i;throw new InvalidOperationException(id);}
        private static void Def(string id,int side,BossActionDef def,params StationBossSnap[] keys){int i=Index(id);Defs[i]=def;Keys[i]=keys;Side[i]=side;}
        private static StationBossSnap N()=>new StationBossSnap();

        static StationBossMoves()
        {
            var mv=NativeStationBoss.Moves;
            // ---- Buffer Swipe: the right front arm draws back across the body (which yaws 10 degrees away and sinks), accelerates
            // into the sweep at the hit, sweeps 120 degrees across the front, carries 20 degrees past, the body over-rotates and
            // springs back while the hand replants ----
            var b=NativeStationBoss.Move("buffer");
            Def("buffer",1,new BossActionDef("buffer",.14f,.35f,P(A,b.Anticipation-.12f,1,BossEase.InOut),P(S,.12f,2,BossEase.In,2.4f),P(C,b.Strike,3,BossEase.Linear),P(F,b.Follow,4,BossEase.OutBack,1.4f),P(R,b.Recover,5,BossEase.Settle,1.5f)),
                N(),
                N().Lean(0,-.15f,0,3,10,-2).Plant(0,-3.3f,5.2f).Reach(1,3.9f,2.3f,4.2f,1,.25f,-.55f).Chest(12,14).Head(10).Watch(-50,0,12,-80),
                N().Lean(0,-.12f,0,2,2,3).Plant(0,-3.3f,5.0f).Reach(1,3.4f,1.7f,6.9f,1,-.1f,.5f).Chest(8,0).Head(4).Watch(-60,0,25,-40),
                N().Lean(0,-.1f,0,2,-16,6).Plant(0,-3.3f,4.6f).Reach(1,-1.9f,2.0f,6.7f,1,-.2f,.6f).Chest(6,-18).Watch(-70,0,40,-20),
                N().Lean(0,-.08f,0,1,-22,7).Plant(0,-3.3f,4.6f).Reach(1,-3.0f,2.2f,5.6f,1,-.1f,.4f).Chest(5,-24).Watch(-65,0,35,-25),
                N());
            // ---- All Aboard: the door's leaves shudder open 5 cm through the telegraph (light blades on the floor), snap open at
            // the hit and the arms shoot out (they overshoot and wobble on their own dynamics), hold and drag, withdraw ----
            var d=NativeStationBoss.Move("doors");
            Def("doors",-1,new BossActionDef("doors",.18f,.4f,P(A,d.Anticipation-.1f,1,BossEase.InOut),P(S,.1f,2,BossEase.In,3),P(C,d.Strike,3,BossEase.Out),P(F,d.Follow,4,BossEase.InOut),P(R,d.Recover,5,BossEase.Settle,1.8f)),
                N(),
                N().Lean(0,-.06f,0,0,0,-4).Set(StationBossCh.DoorL,.06f).Set(StationBossCh.Shudder,1).Head(-4,-35).Bell(-5,0,-88,-10),
                N().Lean(0,-.1f,0,0,0,-8).Set(StationBossCh.DoorL,1).Set(StationBossCh.GrabL,.7f).Head(-6,-38).Bell(-5,0,-88,-10),
                N().Lean(0,-.12f,0,0,0,-9).Set(StationBossCh.DoorL,1).Set(StationBossCh.GrabL,1).Head(-6,-35).Bell(-5,0,-80,-10),
                N().Lean(0,-.1f,0,0,0,-7).Set(StationBossCh.DoorL,1).Set(StationBossCh.GrabL,.75f).Head(-4,-25).Bell(-8,0,-60,-20),
                N());
            // ---- Departure: the chime crouch (body down 0.55, nose dipping, the front hands digging in, the watch pointed), the
            // launch on the fifth beat (tail-heavy), the bounding charge, the brake (hands skidding, yaw 14, roll 9, tail out),
            // the turn back ----
            var p=NativeStationBoss.Move("departure");
            Def("departure",0,new BossActionDef("departure",.2f,.45f,P(A,p.Anticipation-.15f,1,BossEase.InOut),P(S,.15f,2,BossEase.In,2.2f),P(C,p.Strike,3,BossEase.InOut),P(F,p.Follow,4,BossEase.Out,2),P(R,p.Recover,5,BossEase.Settle,1.4f)),
                N(),
                N().Lean(0,-.55f,.2f,6).Plant(0,-2.7f,6.6f).Plant(1,2.7f,6.6f).Chest(22).Head(-18).Watch(-88,0,6,-8).Bell(-140,0,-12,-70).Set(StationBossCh.Lamp,1.8f),
                N().Lean(0,-.35f,-.2f,2).Plant(0,-2.6f,6.9f,.4f).Plant(1,2.6f,6.9f,.4f).Chest(-8).Head(-12).Watch(-85,0,8,-10).Bell(-150,0,-12,-75).Set(StationBossCh.Lamp,2.2f),
                N().Lean(0,-.25f,0,9).Chest(28).Head(-25).Watch(-80,0,8,-10).Bell(-150,0,-12,-75).Set(StationBossCh.Lamp,2.0f),
                N().Lean(0,-.35f,.1f,4,14,-9).Plant(0,-2.4f,6.5f).Plant(1,3.6f,5.7f).Chest(18,0,14).Head(-6,0,-8).Set(StationBossCh.SHatP,15).Set(StationBossCh.SHatR,-25).Set(StationBossCh.Lamp,1.4f),
                N());
            // ---- Platform Alteration: the Stationmaster's watch arm rises to the vault (ease-in over 0.45 s), the body rears 8 and the
            // front arms lift; at the hit the horn blasts and the arm stands up for the 1.2 s strike; down ----
            var a=NativeStationBoss.Move("alteration");
            Def("alteration",0,new BossActionDef("alteration",.16f,.4f,P(A,a.Anticipation-.15f,1,BossEase.In,1.8f),P(S,.15f,2,BossEase.In),P(C,a.Strike,3,BossEase.InOut),P(F,a.Follow,4,BossEase.Out),P(R,a.Recover,5,BossEase.Settle,1.6f)),
                N(),
                N().Lean(0,.15f,0,-4).Reach(0,-3.1f,1.9f,6.3f,-1,-.3f,-.3f,.6f).Reach(1,3.1f,1.9f,6.3f,1,-.3f,-.3f,.6f).Head(-20).Watch(-120,0,8,-20),
                N().Lean(0,.3f,0,-8).Reach(0,-2.9f,2.9f,6.0f,-1,-.3f,-.3f).Reach(1,2.9f,2.9f,6.0f,1,-.3f,-.3f).Head(-30).Watch(-170,0,8,-5).Bell(-40,0,-60,-30).Set(StationBossCh.Horn,1).Set(StationBossCh.Lamp,1.3f),
                N().Lean(0,.32f,0,-8.5f).Reach(0,-2.9f,3.0f,6.0f,-1,-.3f,-.3f).Reach(1,2.9f,3.0f,6.0f,1,-.3f,-.3f).Head(-32).Watch(-172,0,8,-5).Bell(-40,0,-60,-30).Set(StationBossCh.Horn,.6f).Set(StationBossCh.Lamp,1.2f),
                N().Lean(0,.12f,0,-3).Reach(0,-3.2f,1.6f,6.2f,-1,-.3f,-.3f,.4f).Reach(1,3.2f,1.6f,6.2f,1,-.3f,-.3f,.4f).Head(-12).Watch(-110,0,8,-30),
                N());
            // ---- Shunt: brace (body back 0.3, nose down, palms up at chest height), the slam forward at the hit, over-travel 0.5,
            // settle ----
            var s=NativeStationBoss.Move("shunt");
            Def("shunt",0,new BossActionDef("shunt",.14f,.35f,P(A,s.Anticipation-.12f,1,BossEase.InOut),P(S,.12f,2,BossEase.In,2.6f),P(C,s.Strike,3,BossEase.Out),P(F,s.Follow,4,BossEase.InOut),P(R,s.Recover,5,BossEase.Settle,1.5f)),
                N(),
                N().Lean(0,-.15f,-.3f,6).Reach(0,-1.5f,2.2f,5.5f,-1,-.4f,-.4f).Reach(1,1.5f,2.2f,5.5f,1,-.4f,-.4f).Chest(-8).Bell(-140,0,-12,-70).Set(StationBossCh.Lamp,1.3f),
                N().Lean(0,-.15f,.3f,3).Reach(0,-1.0f,2.6f,7.2f,-1,-.5f,-.3f).Reach(1,1.0f,2.6f,7.2f,1,-.5f,-.3f).Chest(6).Bell(-140,0,-12,-70),
                N().Lean(0,-.15f,.6f,3).Reach(0,-1.0f,2.7f,7.5f,-1,-.5f,-.3f).Reach(1,1.0f,2.7f,7.5f,1,-.5f,-.3f).Chest(10).Bell(-130,0,-12,-60),
                N().Lean(0,-.05f,.2f,1).Reach(0,-1.8f,1.6f,6.8f,-1,-.4f,-.3f,.45f).Reach(1,1.8f,1.6f,6.8f,1,-.4f,-.3f,.45f).Chest(6),
                N());
            // ---- Live Rail: the pantograph springs up, the front hands kneel in two beats (left, right), the body braces at the hit
            // and trembles through the 4 s strike with its arms spread, the pantograph folds ----
            var l=NativeStationBoss.Move("live-rail");
            Def("live-rail",0,new BossActionDef("live-rail",.18f,.45f,P(A,(l.Anticipation-.2f)*.5f,1,BossEase.InOut),P(A,(l.Anticipation-.2f)*.5f,2,BossEase.InOut),P(S,.2f,3,BossEase.In),P(C,l.Strike,4,BossEase.InOut),P(F,l.Follow,5,BossEase.Out),P(R,l.Recover,6,BossEase.Settle,1.6f)),
                N(),
                N().Lean(0,-.2f,0,3).Plant(0,-1.85f,6.3f).Reach(1,2.6f,1.0f,6.6f,1,.4f,-.2f,.5f).Set(StationBossCh.Panto,1).Head(-14).Bell(-10,0,-60,-15),
                N().Lean(0,-.35f,0,5).Plant(0,-1.85f,6.3f).Plant(1,1.85f,6.3f).Set(StationBossCh.Panto,1).Head(-20).Bell(-10,0,-100,-15).Watch(-10,0,100,-15),
                N().Lean(0,-.4f,0,5.5f).Plant(0,-1.85f,6.3f).Plant(1,1.85f,6.3f).Set(StationBossCh.Panto,1).Head(-22).Bell(-12,0,-105,-12).Watch(-12,0,105,-12).Set(StationBossCh.Lamp,1.3f),
                N().Lean(0,-.4f,0,5.5f).Plant(0,-1.85f,6.3f).Plant(1,1.85f,6.3f).Set(StationBossCh.Panto,1).Head(-22).Bell(-12,0,-105,-12).Watch(-12,0,105,-12).Set(StationBossCh.Lamp,1.3f),
                N().Lean(0,-.15f,0,2).Plant(0,-2.6f,6.4f,.5f).Plant(1,2.6f,6.4f,.5f).Head(-8),
                N());
            // ---- Terminus: the rear-up over the four chime notes (body up 1.85, pitched 28 up, the front hands at 9.9 m), hang time
            // on the fourth note, the slam at the hit (fast), two heave bounces, the longest punish window ----
            var t=NativeStationBoss.Move("terminus");
            Def("terminus",0,new BossActionDef("terminus",.2f,.5f,P(A,t.Anticipation-.55f,.97f,BossEase.InOut),P(A,.2f,1,BossEase.Out,1.6f),P(S,.35f,2,BossEase.In,2.4f),P(C,t.Strike,3,BossEase.Out),P(F,t.Follow,4,BossEase.OutBack,1.6f),P(R,t.Recover,5,BossEase.Settle,1.3f)),
                N(),
                N().Lean(0,1.85f,-.6f,-28).Reach(0,-2.4f,8.3f,4.6f,-1,-.2f,-1).Reach(1,2.4f,8.3f,4.6f,1,-.2f,-1).Chest(-6).Head(-35).Watch(-175,0,10,0).Bell(-175,0,-10,0).Set(StationBossCh.ChainLift,30).Set(StationBossCh.Lamp,1.6f),
                N().Lean(0,-.3f,.6f,10).Plant(0,-1.9f,7.7f).Plant(1,1.9f,7.7f).Chest(32).Head(-10).Watch(-60,0,10,0).Bell(-60,0,-10,0).Set(StationBossCh.ChainLift,-15),
                N().Lean(0,-.48f,.62f,12).Plant(0,-1.9f,7.7f).Plant(1,1.9f,7.7f).Chest(36).Head(-6).Watch(-55,0,10,0).Bell(-55,0,-10,0),
                N().Lean(0,-.12f,.3f,4).Plant(0,-2.6f,7.2f,.6f).Plant(1,2.6f,7.2f,.6f).Chest(14).Head(-6),
                N());
            // ---- Next Stop: every door bangs (they shudder) and the amber light floods out, the doors bang open at the hit and stay
            // open for the 2 s the adds take to step out, shut ----
            var n=NativeStationBoss.Move("passengers");
            Def("passengers",0,new BossActionDef("passengers",.16f,.4f,P(A,n.Anticipation-.15f,1,BossEase.InOut),P(S,.15f,2,BossEase.In,2.5f),P(C,n.Strike,2,BossEase.Hold),P(R,n.Recover,3,BossEase.Settle,1.8f)),
                N(),
                N().Lean(0,-.1f,0,2).Set(StationBossCh.Shudder,1).Set(StationBossCh.DoorL,.1f).Set(StationBossCh.DoorR,.1f).Chest(10).Head(10),
                N().Lean(0,-.18f,0,1).Set(StationBossCh.Shudder,.25f).Set(StationBossCh.DoorL,1).Set(StationBossCh.DoorR,1).Chest(-4).Head(-10).Bell(-150,0,-15,-20),
                N());
            for(int i=0;i<mv.Length;i++){if(Defs[i]==null)throw new InvalidOperationException("no phrase for "+mv[i].Id);
                if(Math.Abs(Defs[i].Seconds-mv[i].Duration)>1e-3f||Math.Abs(Defs[i].HitAt-mv[i].Anticipation)>1e-3f)throw new InvalidOperationException(mv[i].Id+" phrase timing differs from the move table");
                if(Keys[i].Length!=(int)Math.Round(Defs[i].End)+1)throw new InvalidOperationException(mv[i].Id+" keys");}
            // ---- PLATFORM ALTERATION's change: brace, rear, slam the centre barrier (contact at barrierDownAt), recoil, settle ----
            Smash=new BossActionDef("smash",.25f,.5f,P(A,1.0f,1,BossEase.InOut),P(A,.9f,2,BossEase.InOut),P(S,.3f,3,BossEase.In,3),P(C,.2f,4,BossEase.Out),P(F,.5f,5,BossEase.InOut),P(R,.6f,6,BossEase.Settle,1.5f));
            SmashKeys=new[]{N(),
                N().Lean(0,-.15f,-.35f,6).Reach(0,-1.5f,2.2f,5.5f,-1,-.4f,-.4f).Reach(1,1.5f,2.2f,5.5f,1,-.4f,-.4f).Chest(-8).Set(StationBossCh.Lamp,1.4f),
                N().Lean(0,.7f,-.4f,-14).Reach(0,-1.7f,3.6f,6.2f,-1,-.2f,-.5f).Reach(1,1.7f,3.6f,6.2f,1,-.2f,-.5f).Chest(-10).Head(-20).Watch(-160,0,10,-10).Set(StationBossCh.Horn,1).Set(StationBossCh.Lamp,1.6f),
                N().Lean(0,-.3f,.9f,11).Reach(0,-1.6f,.9f,7.7f,-1,.2f,-.4f).Reach(1,1.6f,.9f,7.7f,1,.2f,-.4f).Chest(30).Head(-8).Set(StationBossCh.Lamp,1.3f),
                N().Lean(0,-.42f,1.0f,13).Reach(0,-1.7f,.7f,7.8f,-1,.2f,-.4f).Reach(1,1.7f,.7f,7.8f,1,.2f,-.4f).Chest(34),
                N().Lean(0,-.1f,.4f,4).Plant(0,-2.6f,7.0f,.6f).Plant(1,2.6f,7.0f,.6f).Chest(12),
                N()};
            // ---- TERMINUS's change: rear up and hook the departure board's chains, tug, tear it down (contact at boardDownAt), the
            // body drops with it; the roof peels and the Stationmaster rises ringing the bell (their own layer, from roofOpenAt) ----
            Tear=new BossActionDef("tear",.3f,.5f,P(A,1.1f,1,BossEase.InOut),P(A,.3f,2,BossEase.InOut),P(S,.25f,3,BossEase.In,2.6f),P(C,.3f,4,BossEase.Out),P(F,.9f,5,BossEase.InOut),P(R,.65f,6,BossEase.Settle,1.4f));
            TearKeys=new[]{N(),
                N().Lean(0,1.6f,-.5f,-24).Reach(0,-3.0f,7.6f,6.6f,-1,-.3f,-.8f).Reach(1,3.0f,7.6f,6.6f,1,-.3f,-.8f).Chest(-8).Head(-35).Set(StationBossCh.ChainLift,25).Set(StationBossCh.Lamp,1.6f),
                N().Lean(0,1.75f,-.55f,-26).Reach(0,-3.2f,7.9f,6.8f,-1,-.3f,-.8f).Reach(1,3.2f,7.9f,6.8f,1,-.3f,-.8f).Chest(-10).Head(-38).Set(StationBossCh.ChainLift,28),
                N().Lean(0,.4f,.2f,2).Reach(0,-3.0f,3.0f,7.2f,-1,-.1f,-.6f).Reach(1,3.0f,3.0f,7.2f,1,-.1f,-.6f).Chest(20).Head(-12),
                N().Lean(0,-.35f,.5f,9).Plant(0,-2.6f,7.4f).Plant(1,2.6f,7.4f).Chest(26).Head(-8).Set(StationBossCh.ChainLift,-12),
                N().Lean(0,-.1f,.2f,3).Plant(0,-3.0f,6.8f,.6f).Plant(1,3.0f,6.8f,.6f).Chest(6),
                N()};
            // ---- The stagger (crash, the express): thrown at once, the nose crumpling 11 down and rolling 11, the left front arm
            // buckling (its plant released), the Stationmaster slumped; a slow push back up ----
            Stagger=new BossActionDef("stagger",.06f,.6f,P(S,.14f,1,BossEase.Out,2),P(F,1,1.15f,BossEase.InOut),P(R,1,2,BossEase.Settle,1.2f));
            StaggerKeys=new[]{N(),
                N().Lean(0,.06f,0,11,0,-11).Fold(0,-2.0f,NativeStationBossModel.PalmLift,4.3f,-.4f,1,.9f).Chest(38,0,-12).Head(30).Watch(-10,0,5,0).Bell(-5,0,-5,0).Set(StationBossCh.Lamp,.5f),
                N()};
            // ---- A part's break (the lamp, the pantograph): a 0.8 s flinch ----
            Flinch=new BossActionDef("flinch",.05f,.3f,P(S,.1f,1,BossEase.Out),P(R,.7f,2,BossEase.Settle,1.4f));
            FlinchKeys=new[]{N(),N().Lean(0,-.12f,-.25f,5,0,4).Chest(22,0,-8).Head(18),N()};
        }
        private static StationBossSnap MakeRolling()
        {
            var r=new StationBossSnap().Lean(0,-2.25f,0,0).Set(StationBossCh.SRise,-.32f).Chest(22).Head(-20).Set(StationBossCh.ChainLift,40);
            for(int i=0;i<6;i++){int s=NativeStationBossModel.Side(i),w=NativeStationBossModel.Row(i);float z=NativeStationBossModel.RowZ[w]+(w==0?.4f:w==1?.1f:-.3f);
                r.Fold(i,s*2.35f,w==0?-.62f:-.92f,z,s*.15f,1,w==0?.25f:w==2?-.25f:0);}
            return r;
        }
        private static StationBossSnap MakeArrival()=>new StationBossSnap().Lean(0,.75f,-.2f,-16).Reach(0,-3.0f,1.7f,7.3f,-1,.4f,-.2f).Reach(1,3.2f,2.2f,7.1f,1,.4f,-.2f)
            .Set(StationBossCh.ChainLift,34).Chest(-12).Head(-16).Watch(-160,0,15,-10).Bell(-30,0,-70,-20).Set(StationBossCh.Lamp,1.6f);
        private static StationBossSnap MakeDeath()
        {
            var d=new StationBossSnap().Lean(0,-.74f,0,3,0,-4).Chest(75).Head(25).Watch(-60,0,0,0).Bell(-50,0,0,0).Set(StationBossCh.Lamp,0).Set(StationBossCh.Look,0).Set(StationBossCh.ChainLift,60);
            for(int i=0;i<6;i++){int s=NativeStationBossModel.Side(i),w=NativeStationBossModel.Row(i);var t=w==0?new Vector3(s*4.3f,NativeStationBossModel.PalmLift,6.2f):w==1?new Vector3(s*5.0f,NativeStationBossModel.PalmLift,.5f):new Vector3(s*4.6f,NativeStationBossModel.PalmLift,-4.6f);
                d.Fold(i,t.X,t.Y,t.Z,s*.7f,.6f,w==0?.2f:0);}
            return d;
        }
        /// <summary>A phrase's channel values at a progress (keys piecewise linear, extended past both ends the way they came, so an
        /// overshooting ease carries through), mirrored to the other side when asked, into `into`.</summary>
        public static void Sample(StationBossSnap[] keys,float progress,bool mirror,float[] into)
        {
            int n=keys.Length;int k=(int)Math.Floor(progress);if(k<0)k=0;if(k>n-2)k=n-2;float f=progress-k;var a=keys[k].V;var b=keys[k+1].V;
            for(int c=0;c<StationBossCh.Count;c++)
            {
                float v=a[c]+(b[c]-a[c])*f;
                // Leg override weights and the hand mode never overshoot (a weight past 1 would pull past the target).
                into[c]=v;
            }
            for(int leg=0;leg<6;leg++){int w=StationBossCh.Leg(leg,StationBossCh.W),h=StationBossCh.Leg(leg,StationBossCh.F);into[w]=BossMath.Clamp01(into[w]);into[h]=BossMath.Clamp01(into[h]);}
            if(mirror)Mirror(into);
        }
        private static readonly float[] mirrorScratch=new float[StationBossCh.Count];
        public static void Mirror(float[] v)
        {
            Array.Copy(v,mirrorScratch,StationBossCh.Count);
            for(int c=0;c<StationBossCh.Count;c++){int m=StationBossCh.MirrorOf[c];float x=mirrorScratch[m];v[c]=StationBossCh.MirrorFlip[c]?-x:x;}
        }
    }
}

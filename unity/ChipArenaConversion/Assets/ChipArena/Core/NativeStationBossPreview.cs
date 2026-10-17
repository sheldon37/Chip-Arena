using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE 6:47's DEV preview (DEV panel · Camera · "Station boss preview", on the station map): a scripted record that plays every
    /// part of the fight on the presentation clock, so the arrival, all eight moves, both phase changes, the staggers, the breaks
    /// and the death can be seen without Astra's rules. Presentation only: nothing is spawned, nothing collides or hurts, and the
    /// record never reaches the authority or the network. A pure function of the clock (two peers, or two frame rates, see the
    /// same record at the same time).
    /// The script (seconds from the start; it loops every <see cref="Loop"/> s): the arrival; a walk and turn; Buffer Swipe; All
    /// Aboard (left door, a 1.5 s hold); Departure (a 22 m charge); Platform Alteration; the turn to the barrier and the PLATFORM
    /// ALTERATION change (the barrier smashed); Shunt; Live Rail; Departure into the booth (the crash stagger); Mind the Gap (the
    /// express hit, the right flank torn); the headlamp shot out; the pantograph snapped; the TERMINUS change (the board torn down,
    /// the roof peeled); Terminus; Next Stop; the death (clocks to 6:48, DEPARTED).
    /// </summary>
    public static class NativeStationBossPreview
    {
        /// <summary>The DEV toggle and its start on the presentation clock (Runtime sets both).</summary>
        public static bool On;public static double Start=double.NaN;
        /// <summary>The record the preview drew last and on which clock (the DEV HUD's banner and plate read it).</summary>
        public static StationBossView Shown=StationBossView.None;public static double ShownClock;
        /// <summary>The banner's eyebrow as the expedition HUD names the area (BOSS ARRIVAL · CENTRAL STATION).</summary>
        public static readonly string BannerArea=ExpeditionBossModel.Eyebrow+" · "+NativeExpeditionStates.AreaName(NativeStationBoss.Area);
        public const float Loop=96,SpawnAt=.5f;
        /// <summary>The preview's pilots (x, z): the look-at targets, Alteration's rings and the HUD's sense of scale.</summary>
        public static readonly Vector2[] Pilots={new Vector2(3.5f,10.5f),new Vector2(-8.5f,9.5f),new Vector2(9.5f,6.0f),new Vector2(-4.0f,14.0f)};
        private struct Act{public string Id;public float At,Hit,Dur;public int Target,HoldSide;public float LaneX0,LaneZ0,LaneX1,LaneZ1;}
        private struct Key{public float At,X,Z,Yaw;public byte Kind;}
        private const byte Smooth=0,Charge=1,Knock=2;
        private static readonly Act[] acts;private static readonly Key[] keys;
        /// <summary>The script's moments the checks and the renders key on.</summary>
        public static float ArrivalEnd,BufferAt,DoorsAt,DepartureAt,AlterationAt,Phase2At,ShuntAt,LiveAt,CrashAt,ExpressAt,LampAt,PantoAt,Phase3At,TerminusAt,SpillAt,DeathAt;
        static NativeStationBossPreview()
        {
            var a=new System.Collections.Generic.List<Act>();var k=new System.Collections.Generic.List<Key>();
            var beats=StationBossBeats.Fallback;float t=SpawnAt+beats.Hold;ArrivalEnd=t;
            void K(float at,float x,float z,float yaw,byte kind=Smooth)=>k.Add(new Key{At=at,X=x,Z=z,Yaw=yaw,Kind=kind});
            float A(string id,float at,int target=0,int holdSide=0,float lx0=float.NaN,float lz0=float.NaN,float lx1=float.NaN,float lz1=float.NaN)
            {var m=NativeStationBoss.Move(id);a.Add(new Act{Id=id,At=at,Hit=at+m.Anticipation,Dur=m.Duration,Target=target,HoldSide=holdSide,LaneX0=lx0,LaneZ0=lz0,LaneX1=lx1,LaneZ1=lz1});return at+m.Duration;}
            K(0,0,1.5f,0);K(t,0,1.5f,0);
            // Walk forward-left and turn toward the far pilot: the gait, the turn on the shoulders.
            K(t+1.2f,0,1.5f,0);K(t+4.6f,-5.5f,4.2f,-28);K(t+5.4f,-5.5f,4.2f,8);
            BufferAt=t+5.6f;t=A("buffer",BufferAt,0);
            K(t+.3f,-5.5f,4.2f,8);K(t+1.6f,-5.5f,4.2f,40);
            DoorsAt=t+1.8f;t=A("doors",DoorsAt,1,-1);
            // Departure: from (-6, 4) east along z 4.2: 22 m.
            K(t+.4f,-5.5f,4.2f,40);K(t+1.5f,-8.0f,4.2f,90);
            DepartureAt=t+1.8f;var dm=NativeStationBoss.Move("departure");
            K(DepartureAt+dm.Anticipation,-8.0f,4.2f,90);K(DepartureAt+dm.Anticipation+dm.Strike+dm.Follow,14.0f,4.2f,98,Charge);
            t=A("departure",DepartureAt,2,0,-8.0f,4.2f,14.0f,4.2f);
            K(t+.4f,14.0f,4.2f,98);K(t+2.0f,14.0f,4.2f,-75);
            AlterationAt=t+2.2f;t=A("alteration",AlterationAt,0);
            // Back to the middle, facing the barrier: the PLATFORM ALTERATION change smashes its centre segments.
            K(t+.3f,14.0f,4.2f,-75);K(t+4.4f,0,1.2f,-150);K(t+5.4f,0,1.2f,-180);
            Phase2At=t+5.6f;t=Phase2At+NativeStationBoss.PhaseChangeSeconds;
            K(t+.2f,0,1.2f,-180);K(t+1.6f,0,1.2f,-200);
            ShuntAt=t+1.8f;t=A("shunt",ShuntAt,0);
            K(t+.3f,0,1.2f,-200);K(t+2.4f,-2.0f,3.0f,-300);
            LiveAt=t+2.6f;t=A("live-rail",LiveAt,1);
            // Departure north into the booth: the crash.
            K(t+.3f,-2.0f,3.0f,-300);K(t+1.9f,0,-1.5f,-360);
            CrashAt=t+2.1f;K(CrashAt+dm.Anticipation,0,-1.5f,-360);K(CrashAt+dm.Anticipation+.5f,0,3.3f,-360,Charge);
            a.Add(new Act{Id="departure",At=CrashAt,Hit=CrashAt+dm.Anticipation,Dur=dm.Anticipation+.5f+.05f,Target=0,LaneX0=0,LaneZ0=-1.5f,LaneX1=0,LaneZ1=8.5f});
            t=CrashAt+dm.Anticipation+.55f+2.2f;
            // Mind the Gap: to the edge of P1 over the pit, the express hits it east along the line.
            K(t+.3f,0,3.3f,-360);K(t+4.0f,-3.0f,-8.6f,-270);
            ExpressAt=t+4.6f;K(ExpressAt,-3.0f,-8.6f,-270);K(ExpressAt+.7f,1.0f,-8.4f,-262,Knock);t=ExpressAt+3.0f;
            K(t+.5f,1.0f,-8.4f,-262);K(t+3.6f,0,2.0f,-360);
            LampAt=t+3.2f;PantoAt=LampAt+2.2f;t=PantoAt+1.4f;
            K(t,0,2.0f,-360);K(t+1.6f,0,3.5f,-360);
            Phase3At=t+1.8f;t=Phase3At+NativeStationBoss.PhaseChangeSeconds;
            K(t+.3f,0,3.5f,-360);K(t+1.6f,0,2.5f,-350);
            TerminusAt=t+1.8f;t=A("terminus",TerminusAt,0);
            K(t+.2f,0,2.5f,-350);K(t+1.4f,0,2.5f,-370);
            SpillAt=t+1.6f;t=A("passengers",SpillAt,0);
            DeathAt=t+1.5f;K(DeathAt,0,2.5f,-370);K(Loop,0,2.5f,-370);
            acts=a.ToArray();keys=k.ToArray();
        }

        /// <summary>The preview's record at presentation time `t` (the clock the preview started on: <see cref="Start"/>),
        /// its root velocity, and the pilot it targets.</summary>
        public static void Fill(double t,ref StationBossView v,out Vector3 velocity,out Vector2 target)
        {
            double since=t-Start;double loops=Math.Floor(since/Loop);float p=(float)(since-loops*Loop);float baseAt=(float)(Start+loops*Loop);
            View(p,baseAt,ref v,out velocity,out target);
        }
        /// <summary>The record `p` seconds into the script, with the script's times offset to `baseAt` on the clock.</summary>
        public static void View(float p,float baseAt,ref StationBossView v,out Vector3 velocity,out Vector2 target)
        {
            v=StationBossView.None;v.live=true;v.spawned=baseAt+SpawnAt;v.maxHp=6000;
            Root(p,out float x,out float z,out float yaw);Root(p+1/240f,out float x2,out float z2,out _);Root(p-1/240f,out float x1,out float z1,out _);
            v.x=x;v.z=z;v.y=0;v.yaw=yaw;velocity=new Vector3((x2-x1)*120,0,(z2-z1)*120);
            // The phases and their arena changes.
            v.phase=p>=Phase3At?3:p>=Phase2At?2:1;
            if(p>=Phase2At){v.phaseAt=baseAt+(p>=Phase3At?Phase3At:Phase2At);v.barrierDownAt=baseAt+Phase2At+2.2f;}
            if(p>=Phase3At){v.boardDownAt=baseAt+Phase3At+1.65f;v.roofOpenAt=baseAt+Phase3At+2.2f;}
            v.hp=v.maxHp*(p<Phase2At?Math.Max(.66f,1-.34f*p/Phase2At):p<Phase3At?Math.Max(.31f,.65f-.34f*(p-Phase2At)/(Phase3At-Phase2At)):Math.Max(0,.3f-.3f*(p-Phase3At)/(DeathAt-Phase3At)));
            // The move playing (the latest started), its target, lane and hold.
            target=Pilots[0];
            for(int i=acts.Length-1;i>=0;i--)
            {
                var a=acts[i];if(p<a.At)continue;if(p>a.At+a.Dur+1.5f)break;
                v.action=a.Id;v.actionId=1000+i+(int)Math.Round(baseAt*4)%997*100;v.actionAt=baseAt+a.At;v.hitAt=baseAt+a.Hit;v.duration=a.Dur;v.actionTarget=a.Target;target=Pilots[a.Target];
                if(!float.IsNaN(a.LaneX0)){v.laneX0=a.LaneX0;v.laneZ0=a.LaneZ0;v.laneX1=a.LaneX1;v.laneZ1=a.LaneZ1;}
                if(a.Id=="doors"){v.holdTarget=a.Target;v.holdSide=a.HoldSide;v.holdUntil=baseAt+a.Hit+.25f+1.5f;}
                if(a.Id=="live-rail"){v.liveAt=baseAt+a.Hit;v.liveUntil=baseAt+a.Hit+NativeStationBoss.Move("live-rail").Strike;}
                if(a.Id=="passengers"){v.spillAt=baseAt+a.Hit;}
                if(a.Id=="shunt"||a.Id=="terminus"){v.laneX0=x+(float)Math.Sin(yaw*BossMath.Deg)*(a.Id=="shunt"?5.5f:6.6f);v.laneZ0=z+(float)Math.Cos(yaw*BossMath.Deg)*(a.Id=="shunt"?5.5f:6.6f);}
                break;
            }
            // The staggers: the crash into the booth, the express, the breaks' flinches, the phase changes.
            var dm=NativeStationBoss.Move("departure");
            if(p>=CrashAt+dm.Anticipation+.55f&&p<CrashAt+dm.Anticipation+.55f+2.6f){v.staggerAt=baseAt+CrashAt+dm.Anticipation+.55f;v.staggerSeconds=2.2f;v.staggerPushX=0;v.staggerPushZ=-1;v.staggerCause="crash";v.cancelAt=v.staggerAt;}
            if(p>=ExpressAt){v.expressHitAt=baseAt+ExpressAt;v.sideBroken=2;if(p<ExpressAt+3.4f){v.staggerAt=baseAt+ExpressAt;v.staggerSeconds=3;v.staggerPushX=1;v.staggerPushZ=0;v.staggerCause="express";}}
            if(p>=LampAt){v.lampBrokenAt=baseAt+LampAt;if(p<LampAt+1){v.staggerAt=v.lampBrokenAt;v.staggerSeconds=.8f;v.staggerCause="part";}}
            if(p>=PantoAt){v.pantoBrokenAt=baseAt+PantoAt;if(p<PantoAt+1){v.staggerAt=v.pantoBrokenAt;v.staggerSeconds=.8f;v.staggerCause="part";}}
            if(p>=DeathAt){v.diedAt=baseAt+DeathAt;v.hp=0;}
        }
        /// <summary>The scripted root at `p`: eased between keys (zero speed at each), the charge's launch-cruise-skid, the express's
        /// throw (fast, easing out).</summary>
        public static void Root(float p,out float x,out float z,out float yaw)
        {
            int i=0;while(i<keys.Length-1&&keys[i+1].At<=p)i++;
            if(i>=keys.Length-1){var e=keys[keys.Length-1];x=e.X;z=e.Z;yaw=e.Yaw;return;}
            var a=keys[i];var b=keys[i+1];float u=b.At>a.At?BossMath.Clamp01((p-a.At)/(b.At-a.At)):1,f;
            switch(b.Kind)
            {
                case Charge:{float T=b.At-a.At,acc=Math.Min(.3f,T*.25f),skid=Math.Min(.7f,T*.3f),cruise=T-acc-skid,s=p-a.At;float vmax=1/(acc*.5f+cruise+skid*.5f);
                    f=s<acc?.5f*vmax*s*s/acc:s<acc+cruise?vmax*(acc*.5f+(s-acc)):s<T?vmax*(acc*.5f+cruise+(s-acc-cruise)-.5f*(s-acc-cruise)*(s-acc-cruise)/skid):1;break;}
                case Knock:f=1-(1-u)*(1-u)*(1-u);break;
                default:f=BossMath.Smoother(u);break;
            }
            x=a.X+(b.X-a.X)*f;z=a.Z+(b.Z-a.Z)*f;yaw=a.Yaw+(b.Yaw-a.Yaw)*BossMath.Smoother(u);
        }
        /// <summary>Alteration's targets: one ring under each pilot (up to four), landing 1.8 s after the move starts.</summary>
        public static int Glass(in StationBossView v,float[] xz)
        {
            if(v.action!="alteration"||!StationBossView.At(v.actionAt))return 0;int n=Math.Min(Pilots.Length,xz.Length/2);
            for(int i=0;i<n;i++){xz[i*2]=Pilots[i].X;xz[i*2+1]=Pilots[i].Y;}return n;
        }
        /// <summary>The preview's blows (time, push, size) for the flinch: a burst of hits from the front pilots in the opening walk.</summary>
        public static int Blows(float p,float baseAt,float[] at,Vector3[] push,float[] size)
        {
            int n=0;for(int k=0;k<6&&n<at.Length;k++){float b=ArrivalEnd+1.6f+k*.42f;if(p<b)break;at[n]=baseAt+b;push[n]=Vector3.Normalize(new Vector3(k%2==0?.3f:-.4f,0,-1));size[n]=k==4?1.2f:.6f;n++;}
            return n;
        }
    }
}

using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE DEEP END's DEV preview (DEV panel · Camera · "Poolrooms boss preview"): a scripted record on the presentation clock, nothing spawned: the arrival, every move, both phase changes, a lamp's break and
    /// its stagger, and the death in a loop, with a directed camera. A pure function of the clock. The capture tool reads <see cref="On"/>, <see cref="Start"/> and <see cref="ShownClock"/> by name. Everything
    /// inside is on the script's own clock p (seconds from <see cref="Start"/>, looping every <see cref="Loop"/>).
    /// </summary>
    public static class NativeDeepEndBossPreview
    {
        public static bool On;public static double Start=double.NaN,ShownClock;
        /// <summary>How fast the script runs on the clock (1 as it plays; DEV: slower to study it frame by frame).</summary>
        public static double Rate=RateDefault;private const double RateDefault=1;
        /// <summary>DEV: where in the script it starts.</summary>
        public static float Skip=SkipDefault;private const float SkipDefault=0;
        /// <summary>The record drawn last (the DEV HUD's plate reads it), on the script's clock.</summary>
        public static DeepEndBossView Shown=DeepEndBossView.None;public static float ShownP;
        public const float SpawnAt=.5f;
        /// <summary>Stand-in pilots (room frame x, z): look-at targets, the Lull's victim, the sense of scale.</summary>
        public static readonly Vector2[] Pilots={new Vector2(-6.5f,-13.5f),new Vector2(14,3),new Vector2(-14,7),new Vector2(6.5f,-7)};
        public struct Act{public string Id;public float At,Hit,Dur;public int Target;public float Tx,Tz,Ax,Az;public int Seed;}
        public static readonly DeepEndRoute Route=new DeepEndRoute(1024);private static readonly Act[] acts;
        public static float ArrivalEnd,BreachAt,JetAt,FlopAt,ThrashAt,DrainAt,LullAt,LampsAt,Phase2At,SeicheAt,Breach2At,WhistleAt,Jet2At,Flop2At,Phase3At,LapAt,Drain2At,Breach3At,DeathAt,Loop;
        private const float TideP2=.5f,TideP3=1.1f;
        /// <summary>Where the DEV preview carves the room on a layout: its middle.</summary>
        public static void CarveAt(PoolroomsLayout l,out int cx,out int cz){cx=l.Width/2;cz=l.Depth/2;}
        public static float TideAt(float p){return p<Phase2At+.7f?PoolroomsLayout.WaterY:p<Phase2At+3.3f?PoolroomsLayout.WaterY+(TideP2-PoolroomsLayout.WaterY)*BossMath.Smooth((p-Phase2At-.7f)/2.6f):p<Phase3At+.8f?TideP2:p<Phase3At+3.4f?TideP2+(TideP3-TideP2)*BossMath.Smooth((p-Phase3At-.8f)/2.6f):TideP3;}
        static NativeDeepEndBossPreview()
        {
            var a=new System.Collections.Generic.List<Act>();var beats=DeepEndBossBeats.Fallback;
            DeepEndBossPaths.Arrival(Route,SpawnAt,beats,0);ArrivalEnd=SpawnAt+beats.Hold;float t=ArrivalEnd+3.0f;
            float At(string id,float at,int target,float tx,float tz,float ax=float.NaN,float az=float.NaN,float? dur=null)
            {
                var m=NativeDeepEndBoss.Move(id);var act=new Act{Id=id,At=at,Hit=at+m.Anticipation,Dur=dur??m.Duration,Target=target,Tx=tx,Tz=tz,Ax=ax,Az=az,Seed=a.Count+1};a.Add(act);
                var v=DeepEndBossView.None;v.live=true;v.action=id;v.actionId=act.Seed;v.actionAt=at;v.hitAt=act.Hit;v.duration=act.Dur;v.targetX=tx;v.targetZ=tz;v.aimX=ax;v.aimZ=az;v.yaw=act.Seed*67;v.x=Route.EndP.X;v.z=Route.EndP.Z;
                DeepEndBossPaths.Append(Route,v,TideAt(at+m.Anticipation));return at+act.Dur;
            }
            void Cruise(float until,float x,float z,float depth=4.6f){DeepEndBossPaths.Cruise(Route,until,new Vector3(x,Math.Min(TideAt(until)-depth,-2.8f),z),(uint)(until*11));}
            float s=PoolroomsLayout.WaterY;
            // ---- THE SHALLOW END ----
            Cruise(t,3,-9);BreachAt=t+.15f;
            t=At("breach",BreachAt,3,6.5f,-6.5f);t=Math.Max(t,Route.EndTime)+.4f;
            Cruise(t+2.2f,7,3);t+=2.4f;JetAt=t;t=At("jet",JetAt,2,Pilots[2].X,Pilots[2].Y,Pilots[1].X,Pilots[1].Y);t=Math.Max(t,Route.EndTime)+.5f;
            FlopAt=t+1.6f;Cruise(t+1.5f,-14,-7.5f,4.8f);t=At("flop",FlopAt,0,-12,-7.5f,12,-7.5f);t=Math.Max(t,Route.EndTime)+.5f;
            Cruise(t+1.4f,3,6,3.2f);t+=1.6f;ThrashAt=t;var thrash=NativeDeepEndBoss.Move("thrash");
            // (Thrash needs it up beside the pilots: it rises first.)
            Route.To(t-.15f,new Vector3(3,s+.6f,6),new Vector3(0,3,1));t=At("thrash",ThrashAt,1,6,8);t=Math.Max(t,Route.EndTime)+.8f;
            Cruise(t+1.2f,0,0,8);t+=1.4f;DrainAt=t;t=At("drain",DrainAt,0,0,0);t=Math.Max(t,Route.EndTime)+.6f;
            LullAt=t+1.0f;t=At("lull",LullAt,1,Pilots[1].X,Pilots[1].Y);t+=.6f;
            // A lamp shot out, a second, third, fourth: the stagger, on the surface, on its side.
            Cruise(t+1.1f,-6,4,3.2f);t+=1.3f;LampsAt=t;Route.To(t+.9f,new Vector3(-6,s+.5f,5.6f),new Vector3(0,.9f,2.6f));Route.To(t+2.6f,new Vector3(-6,s-.2f,9),new Vector3(0,-.2f,1.4f));Route.To(t+5.8f,new Vector3(-6,s-.15f,10.2f),new Vector3(0,0,.3f));Route.To(t+7f,new Vector3(-5,s-3.4f,8),new Vector3(.6f,-1.4f,-1));t+=7.4f;
            // ---- HIGH TIDE: the drain reverses; water wells up the steps and over every deck ----
            Phase2At=t;Cruise(t-.1f,0,0,8);DeepEndBossPaths.PhaseChange(Route,t,s+.6f,2);t=Math.Max(t+NativeDeepEndBoss.PhaseChangeSeconds,Route.EndTime)+.4f;
            SeicheAt=t+1.0f;t=At("seiche",SeicheAt,0,0,0,1,0);t=Math.Max(t,Route.EndTime)+.6f;
            Cruise(t+1.2f,-12,-4);t+=1.4f;Breach2At=t;t=At("breach",Breach2At,2,Pilots[2].X,Pilots[2].Y);t=Math.Max(t,Route.EndTime)+.6f;
            WhistleAt=t+.4f;t=At("whistle",WhistleAt,0,0,0);t+=.2f;
            Cruise(t+1.2f,10,8);t+=1.4f;Jet2At=t;t=At("jet",Jet2At,0,Pilots[0].X,Pilots[0].Y,Pilots[2].X,Pilots[2].Y);t=Math.Max(t,Route.EndTime)+.5f;
            Flop2At=t+1.5f;Cruise(t+1.4f,-4,-16,4.8f);t=At("flop",Flop2At,1,-4,-16,14,4);t=Math.Max(t,Route.EndTime)+.6f;
            // ---- THE DEEP END: the lights in the ceiling go out; the tide comes up to the chest ----
            Phase3At=t;Cruise(t-.1f,0,0,8);DeepEndBossPaths.PhaseChange(Route,t,TideP2,3);t=Math.Max(t+NativeDeepEndBoss.PhaseChangeSeconds,Route.EndTime)+.4f;
            LapAt=t+.8f;t=At("lap",LapAt,0,0,0);t=Math.Max(t,Route.EndTime)+.5f;
            Cruise(t+1.1f,0,0,8);t+=1.3f;Drain2At=t;t=At("drain",Drain2At,0,0,0);t=Math.Max(t,Route.EndTime)+.5f;
            Cruise(t+1.1f,8,-11);t+=1.3f;Breach3At=t;t=At("breach",Breach3At,0,Pilots[0].X,Pilots[0].Y);t=Math.Max(t,Route.EndTime)+.8f;
            // ---- the death: up out of the shaft, straight, so the tail goes first ----
            Route.To(t+.9f,new Vector3(0,-46,0),new Vector3(0,12,0));Route.To(t+3.4f,new Vector3(0,TideP3-1.5f,0),new Vector3(0,4.4f,0));DeathAt=t+2.9f;DeepEndBossPaths.Death(Route,DeathAt,TideP3,DeepEndBossRig.DeathSettle);
            Loop=DeathAt+14;acts=a.ToArray();
        }
        public static float Clock(double t){if(double.IsNaN(Start))return 0;double since=(t-Start)*Rate+Skip;return (float)(since-Math.Floor(since/Loop)*Loop);}
        /// <summary>The script's record at `p`: its move, its target, the tide, the phase.</summary>
        public static void View(float p,ref DeepEndBossView v,out Vector2 target)
        {
            v=DeepEndBossView.None;v.live=true;v.spawned=SpawnAt;v.maxHp=9000;v.pathSeed=47;
            v.phase=p>=Phase3At?3:p>=Phase2At?2:1;if(p>=Phase2At)v.phaseAt=p>=Phase3At?Phase3At:Phase2At;
            v.tideY=TideAt(p);v.tideFrom=v.tideY;
            if(p>=Phase2At&&p<Phase3At){v.tideFrom=PoolroomsLayout.WaterY;v.tideY=TideP2;v.tideAt=Phase2At+.7f;v.tideUntil=Phase2At+3.3f;}
            if(p>=Phase3At){v.tideFrom=TideP2;v.tideY=TideP3;v.tideAt=Phase3At+.8f;v.tideUntil=Phase3At+3.4f;}
            v.hp=v.maxHp*(p<Phase2At?Math.Max(.66f,1-.34f*p/Phase2At):p<Phase3At?Math.Max(.31f,.65f-.34f*(p-Phase2At)/(Phase3At-Phase2At)):Math.Max(0,.3f-.3f*(p-Phase3At)/(DeathAt-Phase3At)));
            target=Pilots[0];Route.At(p,out var h,out _);v.x=h.X;v.z=h.Z;v.y=h.Y;
            for(int i=acts.Length-1;i>=0;i--)
            {
                var a=acts[i];if(p<a.At)continue;if(p>a.At+a.Dur+1.5f)break;
                v.action=a.Id;v.actionId=a.Seed;v.actionAt=a.At;v.hitAt=a.Hit;v.duration=a.Dur;v.actionTarget=a.Target;v.targetX=a.Tx;v.targetZ=a.Tz;v.aimX=a.Ax;v.aimZ=a.Az;target=new Vector2(a.Tx,a.Tz);v.yaw=a.Seed*67;
                if(a.Id=="drain"){v.whirlAt=a.At+.2f;v.whirlUntil=a.Hit+NativeDeepEndBoss.Move("drain").Strike;}
                if(a.Id=="seiche")v.seicheAt=a.Hit;
                if(a.Id=="lull"){v.lull=Math.Min(1,(p-a.At+1.6f)/2.4f);if(p>a.Hit+NativeDeepEndBoss.Move("lull").Strike+NativeDeepEndBoss.Move("lull").Follow)v.lull=0;}
                break;
            }
            // The Lull's warning builds before its record starts.
            if(p>=LullAt-2.6f&&p<LullAt)v.lull=(p-LullAt+2.6f)/2.6f;
            // The whirlpool lingers after the move.
            // The lamps: four shot out one after another, the stagger on the fourth.
            int parts=0;if(p>=LampsAt+.5f)parts|=1;if(p>=LampsAt+1.0f)parts|=4;if(p>=LampsAt+1.5f)parts|=2;if(p>=LampsAt+2.0f)parts|=8;v.parts=parts;v.partAt=LampTimes;
            if(p>=LampsAt+2&&p<LampsAt+7){v.staggerAt=LampsAt+2;v.staggerSeconds=NativeDeepEndBoss.StaggerSeconds;v.staggerCause="lamps";}
            if(p>=DeathAt){v.diedAt=DeathAt;v.hp=0;}
        }
        private static readonly float[] LampTimes=MakeLampTimes();
        private static float[] MakeLampTimes(){return new float[12];}
        /// <summary>The time each lamp broke (the script's clock): the runtime keeps the array filled from the script.</summary>
        public static void FillLampTimes(){LampTimes[0]=LampsAt+.5f;LampTimes[2]=LampsAt+1f;LampTimes[1]=LampsAt+1.5f;LampTimes[3]=LampsAt+2f;}
        /// <summary>The act playing at p (its index in <see cref="ActCount"/> order, its start), for the camera's cut: -1 between.</summary>
        public static int ActCount=>acts.Length;public static Act ActAt(int i)=>acts[i];

        // ---- the directed camera: a slow orbit, reframed for each move ----
        private struct Frame{public float Yaw,Dist,Height,Fov;public Vector3 Focus;public float Follow;}
        private static Frame FrameAt(float p,int act,in Vector3 mouth,in Vector3 mid)
        {
            float orbit=p*.035f;var f=new Frame{Yaw=orbit+.6f,Dist=19,Height=5,Fov=56,Focus=new Vector3(0,1.5f,0),Follow=.2f};
            if(act<0)return f;var a=acts[act];float side=a.Seed%2==0?1:-1;
            switch(a.Id)
            {
                case "breach":f.Focus=new Vector3(a.Tx,2.6f,a.Tz);f.Yaw=(float)Math.Atan2(-a.Tx,-a.Tz)+side*1.0f+orbit*.2f;f.Dist=14.5f;f.Height=2.2f;f.Fov=60;f.Follow=.45f;break;
                case "jet":{var d=new Vector3(a.Tx-mouth.X,0,a.Tz-mouth.Z);float y=(float)Math.Atan2(d.X,d.Z);f.Focus=new Vector3((a.Tx+mouth.X)*.5f,2.1f,(a.Tz+mouth.Z)*.5f);f.Yaw=y+side*1.45f;f.Dist=17;f.Height=4.4f;f.Fov=66;f.Follow=.12f;break;}
                case "flop":{var d=new Vector3(a.Ax-a.Tx,0,a.Az-a.Tz);float y=(float)Math.Atan2(d.X,d.Z);f.Focus=new Vector3((a.Tx+a.Ax)*.5f,1.2f,(a.Tz+a.Az)*.5f);f.Yaw=y+1.57f*side;f.Dist=19;f.Height=4.2f;f.Fov=60;f.Follow=.1f;break;}
                case "thrash":f.Focus=new Vector3(2,1.4f,8);f.Yaw=2.6f;f.Dist=13;f.Height=3.2f;f.Fov=62;f.Follow=.3f;break;
                case "drain":f.Focus=new Vector3(0,1.2f,0);f.Yaw=orbit*2.4f+1.2f;f.Dist=16;f.Height=7f;f.Fov=66;f.Follow=.25f;break;
                case "lull":f.Focus=new Vector3(a.Tx,.9f,a.Tz);f.Yaw=-1.57f+.4f;f.Dist=5.5f;f.Height=2.1f;f.Fov=56;f.Follow=0;break;
                case "seiche":f.Focus=new Vector3(0,1,0);f.Yaw=0;f.Dist=18.5f;f.Height=4.6f;f.Fov=68;f.Follow=.05f;break;
                case "whistle":f.Focus=new Vector3(0,1.4f,0);f.Yaw=orbit+2;f.Dist=17;f.Height=4.2f;f.Fov=60;f.Follow=0;break;
                case "lap":f.Focus=new Vector3(0,.8f,0);f.Yaw=orbit*3.8f;f.Dist=4f;f.Height=5.6f;f.Fov=76;f.Follow=.85f;break;
            }
            return f;
        }
        private static int ActOf(float p){for(int i=acts.Length-1;i>=0;i--){var a=acts[i];if(p>=a.At-1.1f)return p<=a.At+a.Dur+1.2f?i:-1;}return -1;}
        /// <summary>The camera at p: the intro's shots through the arrival, then a slow orbit reframing itself for each move (blended over a second and more, so it never cuts), looking between the move's place and the boss's body.</summary>
        public static bool Camera(float p,in Vector3 origin,in Vector3 headRoom,in Vector3 midRoom,out Vector3 eye,out Vector3 look,out float fov)
        {
            eye=look=default;fov=60;
            if(p<ArrivalEnd+.4f)
            {
                if(!NativeBackroomsBossIntro.Sample(NativeDeepEndBoss.Shots,p-SpawnAt<0?0:p-SpawnAt,origin,0,out eye,out look,out fov))return false;
                // The arrival's mouth shot looks at the mouth itself.
                var b=DeepEndBossBeats.Fallback;float a=p-SpawnAt;if(a>=b.MouthShot)look=Vector3.Lerp(look,origin+headRoom,.65f);else if(a>=b.BreakShot)look=Vector3.Lerp(look,origin+headRoom,.35f);
                return true;
            }
            int act=ActOf(p);var f0=FrameAt(p,act,headRoom,midRoom);var f=f0;
            // Blend from the frame before and toward the frame after near the act's edges.
            if(act>=0){var a=acts[act];float inU=BossMath.Smoother((p-(a.At-1.1f))/1.1f);if(inU<1){f=Mix(FrameAt(p,-1,headRoom,midRoom),f0,inU);}float outU=BossMath.Smoother((p-(a.At+a.Dur))/1.2f);if(outU>0)f=Mix(f,FrameAt(p,-1,headRoom,midRoom),outU);}
            var focus=Vector3.Lerp(f.Focus,headRoom,f.Follow);focus.Y=Math.Max(focus.Y,.6f);
            float tide=TideAt(p);float h=Math.Max(f.Height,tide+1.2f);
            var at=focus+new Vector3((float)Math.Sin(f.Yaw)*f.Dist,0,(float)Math.Cos(f.Yaw)*f.Dist);at.Y=h;const float wall=NativeDeepEndArena.Half-.9f;at.X=Math.Max(-wall,Math.Min(wall,at.X));at.Z=Math.Max(-wall,Math.Min(wall,at.Z));at.Y=Math.Min(at.Y,NativeDeepEndArena.Ceiling-.5f);
            eye=origin+at;look=origin+focus;fov=f.Fov;return true;
        }
        private static Frame Mix(in Frame a,in Frame b,float u){return new Frame{Yaw=a.Yaw+BossMath.WrapPi(b.Yaw-a.Yaw)*u,Dist=a.Dist+(b.Dist-a.Dist)*u,Height=a.Height+(b.Height-a.Height)*u,Fov=a.Fov+(b.Fov-a.Fov)*u,Focus=Vector3.Lerp(a.Focus,b.Focus,u),Follow=a.Follow+(b.Follow-a.Follow)*u};}
        /// <summary>The preview's blows (time, push, size) for the flash: a burst from the pilots after the first Breach.</summary>
        public static int Blows(float p,float[] at,float[] size){int n=0;for(int k=0;k<5&&n<at.Length;k++){float b=BreachAt+1.4f+k*.36f;if(p<b)break;at[n]=b;size[n]=k==3?.6f:.3f;n++;}return n;}
    }
}

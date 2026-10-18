using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One boss cue: its key (an index into <see cref="NativeIronworksBossCues.Keys"/>), the clock time it falls on, where it
    /// plays and its gain.</summary>
    public struct IronworksBossCue{public byte Key;public float At,Gain;public Vector3 At3;}

    /// <summary>
    /// THE CONVERTER's sounds (doc section 9's table; the owner generates the clips): the `boss.ironworks.*` keys and the moments they
    /// fall on, read from the view and the rig as drawn. <see cref="Collect"/> lists the one-shot cues whose moment falls in (from,
    /// to] on the clocks; the runtime plays them through NativeAudio's existing bank path (Cue with no stand-in clip), so a key with
    /// no clips is silent. The five loops (idle burn, pour, rain, gallop, cooling) are levels per frame (<see cref="Loops"/>), played
    /// by the runtime on one bank voice each while their level is up. The arrival's cues fall on the boss song clock (it runs through
    /// the intro hold), the death's on the death's clock. Pure and allocation free.
    /// </summary>
    public static class NativeIronworksBossCues
    {
        public static readonly string[] Keys=
        {
            "boss.ironworks.arrive.rumble","boss.ironworks.arrive.crack","boss.ironworks.arrive.emerge","boss.ironworks.arrive.lamps","boss.ironworks.arrive.blow","boss.ironworks.idle.burn",
            "boss.ironworks.step.tong","boss.ironworks.step.bogie","boss.ironworks.gear.tilt","boss.ironworks.slosh","boss.ironworks.foreman.lever","boss.ironworks.foreman.valve","boss.ironworks.foreman.groan",
            "boss.ironworks.pour.windup","boss.ironworks.pour.loop","boss.ironworks.pour.splash","boss.ironworks.blow.inhale","boss.ironworks.blow.roar","boss.ironworks.blow.rain","boss.ironworks.blow.land",
            "boss.ironworks.blow.choke","boss.ironworks.stomp.lift","boss.ironworks.stomp.impact","boss.ironworks.grab.reach","boss.ironworks.grab.snap","boss.ironworks.grab.squeeze","boss.ironworks.grab.throw",
            "boss.ironworks.rear.creak","boss.ironworks.slam.impact","boss.ironworks.slam.wave","boss.ironworks.deslag.roll","boss.ironworks.deslag.lob","boss.ironworks.deslag.land","boss.ironworks.call.bellow",
            "boss.ironworks.charge.windup","boss.ironworks.charge.gallop","boss.ironworks.charge.skid","boss.ironworks.charge.crash","boss.ironworks.crust.crack","boss.ironworks.crust.break","boss.ironworks.shed.burst",
            "boss.ironworks.break.tear","boss.ironworks.break.split","boss.ironworks.pit.overflow","boss.ironworks.knee.buckle","boss.ironworks.hurt.clang","boss.ironworks.death.buckle","boss.ironworks.death.pour",
            "boss.ironworks.death.cool","boss.ironworks.death.whistle",
        };
        public const byte Rumble=0,Crack=1,Emerge=2,Lamps=3,ArriveBlow=4,IdleBurn=5,StepTong=6,StepBogie=7,GearTilt=8,Slosh=9,Lever=10,Valve=11,Groan=12,PourWindup=13,PourLoop=14,PourSplash=15,
            Inhale=16,Roar=17,Rain=18,Land=19,Choke=20,StompLift=21,StompImpact=22,GrabReach=23,GrabSnap=24,GrabSqueeze=25,GrabThrow=26,RearCreak=27,SlamImpact=28,SlamWave=29,DeslagRoll=30,DeslagLob=31,
            DeslagLand=32,Bellow=33,ChargeWindup=34,Gallop=35,Skid=36,CrashHit=37,CrustCrack=38,CrustBreak=39,ShedBurst=40,BreakTear=41,BreakSplit=42,PitOverflow=43,KneeBuckle=44,Hurt=45,DeathBuckle=46,
            DeathPour=47,DeathCool=48,Whistle=49;
        /// <summary>The loops (each a level per frame) and their keys.</summary>
        public static readonly byte[] LoopKeys={IdleBurn,PourLoop,Rain,Gallop,DeathCool};
        /// <summary>The warnings, mixed above the furnace bed (doc section 9).</summary>
        public static bool Warning(byte key)=>key==Inhale||key==RearCreak||key==GrabReach||key==ChargeWindup||key==DeslagRoll;
        public const int QueueSize=48;private static readonly IronworksBossCue[] queue=new IronworksBossCue[QueueSize];private static int queued;
        /// <summary>What the presentation asks the audio for this frame (it sets them; NativeAudio reads them the next frame): the loops'
        /// levels and places (<see cref="LoopKeys"/> order), the music's level (the duck), and the frame it asked on.</summary>
        public static readonly float[] LoopLevel=new float[5];public static readonly Vector3[] LoopAt=new Vector3[5];public static float MusicDuck=1;public static int Frame=int.MinValue;
        private static readonly IronworksBossCue[] scratch=new IronworksBossCue[QueueSize];
        public static void Queue(in IronworksBossView v,IronworksBossRig rig,NativeIronworksBossFx fx,float from,float to,float arriveFrom,float arriveTo,float deathFrom,float deathTo)
        {int k=Collect(v,rig,fx,from,to,arriveFrom,arriveTo,deathFrom,deathTo,scratch);for(int i=0;i<k&&queued<QueueSize;i++)queue[queued++]=scratch[i];}
        public static int Drain(IronworksBossCue[] into){int k=Math.Min(queued,into.Length);Array.Copy(queue,into,k);queued=0;return k;}
        private static IronworksBossCue[] into;private static int n;private static float lo,hi;
        private static void Add(byte key,float at,Vector3 place,float gain=1){if(!(at>lo&&at<=hi)||into==null||n>=into.Length)return;into[n++]=new IronworksBossCue{Key=key,At=at,At3=place,Gain=gain};}
        /// <summary>The one-shot cues falling in (from, to] on the presentation clock, (arriveFrom, arriveTo] on the boss song clock and
        /// (deathFrom, deathTo] on the death's; returns how many were written.</summary>
        public static int Collect(in IronworksBossView v,IronworksBossRig rig,NativeIronworksBossFx fx,float from,float to,float arriveFrom,float arriveTo,float deathFrom,float deathTo,IronworksBossCue[] buffer)
        {
            into=buffer;n=0;if(!v.live||rig==null){into=null;return 0;}
            var body=rig.Shown.Position[NativeIronworksBossModel.Vessel];var mouth=rig.Mouth;var head=rig.HeadAt;
            // ---- the arrival (on the song clock: only when this peer saw it) ----
            if(rig.Arriving&&arriveTo>arriveFrom)
            {
                lo=arriveFrom;hi=arriveTo;var b=NativeIronworksBossBeats.Current;var pit=new Vector3(NativeIronworksBossArena.PitX,0,NativeIronworksBossArena.PitZ);
                Add(Rumble,0.001f,pit,1.1f);Add(Crack,b.Hit,pit,1.2f);Add(StepTong,b.Hit+.08f,rig.Foot(0));Add(StepTong,b.Hit+.12f,rig.Foot(1));
                Add(Emerge,b.Heave,body,1.1f);Add(StepBogie,b.Climb+.5f,rig.Foot(2));Add(StepBogie,b.Climb+.8f,rig.Foot(3));Add(Slosh,b.Climb+1.1f,mouth,.8f);
                Add(Lamps,b.Flare,rig.LampAtL,1.0f);Add(ArriveBlow,b.Drop,mouth,1.3f);
            }
            // ---- the death (on its own clock) ----
            if(!float.IsNaN(rig.DeathAge)&&deathTo>deathFrom)
            {
                lo=deathFrom;hi=deathTo;Add(DeathBuckle,0.001f,body,1.2f);Add(DeathPour,1.0f,mouth,1.0f);Add(Lamps,1.9f,rig.LampAtL,.6f);Add(Whistle,NativeIronworksBossStage.WhistleAt,rig.WhistleAt,1.3f);
            }
            lo=from;hi=to;if(!(hi>lo)){into=null;return n;}
            if(v.hazards!=null&&float.IsNaN(rig.DeathAge))foreach(var h in v.hazards)
                if(h.kind=="blow"||h.kind=="deslag")Add(h.kind=="blow"?Land:DeslagLand,h.hitAt,new Vector3(h.x,h.y,h.z),h.kind=="blow"?.6f:1);
            // ---- the action playing (its record) ----
            int ai=rig.ActIndex;
            if(ai>=0&&IronworksBossView.At(v.actionAt))
            {
                var a=NativeIronworksBoss.Actions[ai];float s=v.actionAt,hit=s+a.Windup;
                switch(a.Id)
                {
                    case "pour":Add(Lever,s,head);Add(GearTilt,s+.1f,body);Add(PourWindup,s+.2f,mouth);Add(PourSplash,hit+.3f,mouth,1.1f);Add(Slosh,hit+a.Strike+a.Follow+.2f,mouth,.7f);break;
                    case "blow":Add(Inhale,s,rig.GrateAt,1.1f);Add(Valve,s+.15f,head,.7f);Add(Roar,hit,mouth,1.3f);
                        if(v.hazards==null&&fx!=null)for(int i=0;i<fx.RainCount;i++)Add(Land,hit+2.0f+fx.Rain[i*4+3],new Vector3(fx.Rain[i*4],0,fx.Rain[i*4+1]),.6f);break;
                    case "stomp":Add(StompLift,s,rig.Foot(Math.Max(0,Math.Min(3,v.variant))));Add(StompImpact,hit,rig.Foot(Math.Max(0,Math.Min(3,v.variant))),1.3f);break;
                    case "grab":Add(GrabReach,s,rig.TongTip[rig.ActLeg==0?0:1],1.0f);Add(GrabSnap,hit,rig.TongTip[rig.ActLeg==0?0:1],1.2f);Add(GrabSqueeze,hit+.4f,rig.TongTip[rig.ActLeg==0?0:1]);
                        if(v.targetSeat>=0)Add(GrabThrow,hit+a.Strike+a.Follow-.6f,rig.TongTip[rig.ActLeg==0?0:1]);break;
                    case "slam":Add(RearCreak,s,body,1.1f);Add(SlamImpact,hit,body,1.4f);Add(SlamWave,hit+.05f,body,1.1f);Add(Slosh,hit+.3f,mouth);break;
                    case "deslag":Add(Lever,s,head);Add(DeslagRoll,s+.1f,body,1.0f);for(int i=0;i<3;i++){Add(DeslagLob,hit+i*.4f,mouth);if(v.hazards==null&&fx!=null)Add(DeslagLand,hit+i*.4f+.9f,new Vector3(fx.Lumps[i*3],0,fx.Lumps[i*3+1]));}break;
                    case "call":Add(Bellow,hit,mouth,1.3f);break;
                    case "charge":Add(ChargeWindup,s,body,1.1f);Add(Skid,hit+a.Strike,body,1.1f);break;
                }
            }
            // ---- the moments, the phase changes, the parts ----
            if(rig.MomentIndex>=0&&IronworksBossView.At(v.actionAt))
            {
                string id=NativeIronworksBoss.Actions[rig.MomentIndex].Id;
                if(id=="choke")Add(Choke,v.actionAt,mouth,1.1f);else if(id=="kneel")Add(KneeBuckle,v.actionAt,body,1.2f);else if(id=="crash")Add(CrashHit,v.actionAt,body,1.4f);
            }
            if(IronworksBossView.At(v.phaseAt)&&v.phase>=2)
            {
                var sh=NativeIronworksBoss.Find("shed");
                if(v.phase==2){Add(CrustCrack,v.phaseAt,body);Add(ShedBurst,v.phaseAt+sh.Windup,body,1.3f);Add(Bellow,v.phaseAt+1.55f,mouth,1.2f);}
                else{Add(BreakSplit,v.phaseAt+1.0f,body,1.2f);Add(PitOverflow,v.phaseAt+1.2f,new Vector3(NativeIronworksBossArena.PitX,0,NativeIronworksBossArena.PitZ));}
            }
            if(v.phase<2&&IronworksBossView.At(v.partAt0))Add(CrustBreak,v.partAt0,body);
            for(int g=1;g<4;g++){float at=v.PartAt(g);if(v.phase<2&&IronworksBossView.At(at))Add(CrustBreak,at,body);}
            if(IronworksBossView.At(rig.PulpitAt))Add(BreakTear,rig.PulpitAt,rig.WhistleAt,1.2f);
            // ---- life: footfalls (the feet that landed this frame), a blow's clang, the foreman's groan now and then ----
            if(rig.Landed!=0&&float.IsNaN(rig.DeathAge)&&!rig.Arriving)for(int i=0;i<4;i++)if((rig.Landed&(1<<i))!=0){Add(i<2?StepTong:StepBogie,to,rig.Foot(i),.6f+.25f*Math.Min(1,rig.LandSpeed[i]/3));}
            if(IronworksBossView.At(rig.LastHit))Add(Hurt,rig.LastHit,body,.8f);
            if(float.IsNaN(rig.DeathAge)){const float period=9.5f;float k=(float)Math.Floor(to/period)*period+2.7f;Add(Groan,k,head,.5f);}
            into=null;return n;
        }
        /// <summary>The loops' levels this frame (0 off .. 1), in <see cref="LoopKeys"/> order: the idle burn while it lives, the pour's
        /// stream while it falls, the rain while it falls, the gallop while it charges, the cooling through the death's ticking.</summary>
        public static void Loops(in IronworksBossView v,IronworksBossRig rig,float t,float[] level)
        {
            for(int i=0;i<level.Length;i++)level[i]=0;if(!v.live||rig==null||!rig.Visible)return;bool dead=!float.IsNaN(rig.DeathAge);
            level[0]=dead?0:rig.Arriving?BossMath.Clamp01((rig.ArrivalAge-NativeIronworksBossBeats.Current.Heave)/2):.55f+.3f*Math.Min(1,rig.HeatLevel);
            if(rig.ActIndex>=0)
            {
                var a=NativeIronworksBoss.Actions[rig.ActIndex];float age=rig.Act.Age;
                if(a.Id=="pour"&&age>=a.Windup&&age<a.Windup+a.Strike+a.Follow)level[1]=age<a.Windup+a.Strike?1:1-(age-a.Windup-a.Strike)/a.Follow;
                if(a.Id=="blow"&&age>=a.Windup+2.0f&&age<a.Windup+a.Strike)level[2]=1;
                if(a.Id=="charge"&&age>=a.Windup&&age<a.Windup+a.Strike)level[3]=1;
            }
            if(dead)level[4]=BossMath.Clamp01((rig.DeathAge-1.6f)/1)*(1-BossMath.Clamp01((rig.DeathAge-14)/4));
        }
    }
}

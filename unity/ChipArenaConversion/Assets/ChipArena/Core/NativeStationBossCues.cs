using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One boss cue at a moment: its key (an index into <see cref="NativeStationBossCues.Keys"/>), the clock time it falls on,
    /// where it plays (unused for a 2D cue) and its gain.</summary>
    public struct StationBossCue{public byte Key;public float At,Gain;public Vector3 At3;}

    /// <summary>
    /// THE 6:47's sounds (doc section 8's table, the owner generates the clips): the `boss.station.*` keys and the moments they fall
    /// on, read from the view and the rig the picture drew. <see cref="Collect"/> lists the cues whose moment falls in (from, to]
    /// on the presentation clock; Runtime routes them through NativeAudio's existing bank path (Cue with no stand-in), so a key with
    /// no clips is silent. 2D (F) keys play flat; the rest at their place. Pure and allocation free.
    /// </summary>
    public static class NativeStationBossCues
    {
        public static readonly string[] Keys={"boss.station.horn","boss.station.chime","boss.station.announce","boss.station.doors-warn","boss.station.doors","boss.station.grab","boss.station.unfold",
            "boss.station.wheels","boss.station.charge","boss.station.brakes","boss.station.impact","boss.station.step","boss.station.swipe-windup","boss.station.swipe","boss.station.brace","boss.station.panto",
            "boss.station.arc","boss.station.rear","boss.station.slam","boss.station.shockwave","boss.station.bell","boss.station.tick","boss.station.spill","boss.station.voice","boss.station.hurt",
            "boss.station.lamp-break","boss.station.panto-break","boss.station.express-hit","boss.station.barrier","boss.station.board","boss.station.roof","boss.station.death","boss.station.clocks","boss.station.departed"};
        public const byte Horn=0,Chime=1,Announce=2,DoorsWarn=3,Doors=4,Grab=5,Unfold=6,Wheels=7,Charge=8,Brakes=9,Impact=10,Step=11,SwipeWindup=12,Swipe=13,Brace=14,Panto=15,
            Arc=16,Rear=17,Slam=18,Shockwave=19,Bell=20,Tick=21,Spill=22,Voice=23,Hurt=24,LampBreak=25,PantoBreak=26,ExpressHit=27,Barrier=28,Board=29,Roof=30,Death=31,Clocks=32,Departed=33;
        /// <summary>The 2D (station-wide) keys: the chime, the announcer, the doors' warning, the clocks' tick, the departure line.</summary>
        public static bool Flat(byte key)=>key==Chime||key==Announce||key==DoorsWarn||key==Tick||key==Clocks||key==Departed;
        /// <summary>The cues the presentation found this frame, waiting for the audio (one frame at most).</summary>
        public const int QueueSize=48;private static readonly StationBossCue[] queue=new StationBossCue[QueueSize];private static int queued;
        /// <summary>Collects this frame's cues (from, to] into the queue the audio drains.</summary>
        public static void Queue(in StationBossView v,StationBossRig rig,float fromTime,float toTime){int k=Collect(v,rig,fromTime,toTime,scratch);for(int i=0;i<k&&queued<QueueSize;i++)queue[queued++]=scratch[i];}
        /// <summary>Moves the queued cues into `into` (the audio's own buffer) and empties the queue; returns how many.</summary>
        public static int Drain(StationBossCue[] into){int k=Math.Min(queued,into.Length);Array.Copy(queue,into,k);queued=0;return k;}
        private static readonly StationBossCue[] scratch=new StationBossCue[QueueSize];
        private static StationBossCue[] into;private static int n;private static float from,to;
        private static void Add(byte key,float at,Vector3 place,float gain=1){if(!(at>from&&at<=to)||into==null||n>=into.Length)return;into[n++]=new StationBossCue{Key=key,At=at,At3=place,Gain=gain};}
        /// <summary>The cues falling in (from, to] for this record and the rig as drawn; returns how many were written.</summary>
        public static int Collect(in StationBossView v,StationBossRig rig,float fromTime,float toTime,StationBossCue[] buffer)
        {
            into=buffer;n=0;from=fromTime;to=toTime;if(!v.live||rig==null||!(to>from))return 0;
            var body=rig.Shown.Position[NativeStationBossModel.Body];var lamp=rig.LampPos;var hat=rig.Shown.Position[NativeStationBossModel.SmHead];
            // ---- the arrival (only when this peer saw it; the song carries its own chimes when it plays) ----
            if(rig.Arriving)
            {
                var b=NativeStationBossBeats.Current;float s=v.spawned;
                if(!b.Song)Add(Chime,s+b.ChimeHit,body);Add(Horn,s+b.LightCut,lamp,1.2f);Add(Brakes,s+b.Accent0,body);Add(Brakes,s+b.Accent1,body,1.1f);Add(Unfold,s+b.Accent2,body);Add(Step,s+b.Accent3,body,1.2f);
                Add(Barrier,s+b.Drop,new Vector3(rig.Root.X,1,NativeStationBossArena.BarrierZ),1.2f);Add(Bell,s+b.Drop+1.2f,hat,.8f);
            }
            // ---- the move playing (its record) ----
            int m=v.Move;
            if(m>=0&&StationBossView.At(v.actionAt))
            {
                var mv=NativeStationBoss.Moves[m];float a=v.actionAt,hit=StationBossView.At(v.hitAt)?v.hitAt:a+mv.Anticipation,s=mv.Anticipation>0?(hit-a)/mv.Anticipation:1;
                switch(mv.Id)
                {
                    case "buffer":Add(SwipeWindup,a,body);Add(Swipe,hit,rig.Hand(rig.ActSide>0?1:0));break;
                    case "doors":Add(DoorsWarn,a,body);Add(Doors,hit,body);Add(Grab,hit+.05f,body);break;
                    case "departure":Add(Announce,a,body);Add(Wheels,a,body);for(int i=0;i<NativeStationBoss.ChimeNotes;i++)Add(Chime,a+mv.Note(i)*s,body);Add(Charge,hit,body,1.2f);
                        if(v.phase>=3)
                        {
                            // TERMINUS's double charge: the turn's two-note chime, the second charge, its brake.
                            var dd=StationBossMoves.DepartureDouble;float after=dd.Seconds-dd.HitAt,k=StationBossView.At(v.duration)&&after>0?Math.Max(.3f,(v.duration-(hit-a))/after):1;
                            Add(Chime,hit+(mv.Strike+.1f)*k,body);Add(Chime,hit+(mv.Strike+.35f)*k,body);Add(Charge,hit+StationBossMoves.SecondCharge*k,body,1.2f);Add(Brakes,hit+(StationBossMoves.SecondCharge+mv.Strike)*k,body);
                        }
                        else Add(Brakes,hit+mv.Strike,body);
                        break;
                    case "alteration":Add(Horn,a,lamp,1.1f);break;
                    case "shunt":Add(Brace,a,body);Add(Impact,hit,lamp,.7f);break;
                    case "live-rail":Add(Panto,a,rig.Shown.Position[NativeStationBossModel.PantoHead]);for(float k=hit;k<hit+mv.Strike;k+=.8f)Add(Arc,k,rig.Shown.Position[NativeStationBossModel.PantoHead],.9f);break;
                    case "terminus":Add(Rear,a,body);for(int i=0;i<NativeStationBoss.ChimeNotes;i++){Add(Chime,a+mv.Note(i)*s,body);Add(Tick,a+mv.Note(i)*s,body,.8f);}Add(Slam,hit,body,1.3f);Add(Shockwave,hit+.05f,body);break;
                    case "passengers":Add(Spill,a,body);Add(Doors,hit,body);break;
                }
            }
            // ---- the phase changes, the breaks, the staggers ----
            if(StationBossView.At(v.phaseAt)&&v.phase>=2){Add(Horn,v.phaseAt,lamp,1.2f);if(v.phase>=3)Add(Clocks,v.phaseAt+.5f,body);}
            if(StationBossView.At(v.barrierDownAt))Add(Barrier,v.barrierDownAt,new Vector3(rig.Root.X,1,NativeStationBossArena.BarrierZ),1.2f);
            if(StationBossView.At(v.boardDownAt)){Add(Board,v.boardDownAt,new Vector3(NativeStationBossArena.BoardX,4,NativeStationBossArena.BoardZ),1.3f);}
            if(StationBossView.At(v.roofOpenAt)){Add(Roof,v.roofOpenAt,body);Add(Bell,v.roofOpenAt+.2f,hat);}
            if(StationBossView.At(v.lampBrokenAt))Add(LampBreak,v.lampBrokenAt,lamp);if(StationBossView.At(v.pantoBrokenAt))Add(PantoBreak,v.pantoBrokenAt,rig.Shown.Position[NativeStationBossModel.PantoLow]);
            if(StationBossView.At(v.expressHitAt))Add(ExpressHit,v.expressHitAt,body,1.3f);
            if(StationBossView.At(v.staggerAt)&&v.staggerCause=="crash")Add(Impact,v.staggerAt,lamp,1.2f);
            // ---- the death: the long brake release, the clocks to 6:48, the departure line ----
            if(StationBossView.At(v.diedAt)){Add(Death,v.diedAt,body,1.2f);Add(Clocks,v.diedAt+NativeStationBossStage.DeathClocks,body);Add(Departed,v.diedAt+NativeStationBossStage.DeathBoard,body);}
            // ---- life: a hand's footfall (the hands that landed this frame), a blow's groan, the passengers' moan every few seconds ----
            if(rig.Landed!=0&&!v.Dead(to))for(int i=0;i<6;i++)if((rig.Landed&(1<<i))!=0){Add(Step,to,rig.Hand(i),.55f+.1f*(i>>1));break;}
            if(StationBossView.At(rig.LastHit))Add(Hurt,rig.LastHit,body,.7f);
            if(!v.Dead(to)){const float period=6.5f;float k=(float)Math.Floor(to/period)*period+1.3f;Add(Voice,k,body,.5f);}
            into=null;return n;
        }
    }
}

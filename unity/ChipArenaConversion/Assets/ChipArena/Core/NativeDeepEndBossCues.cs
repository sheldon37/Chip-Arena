using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One boss cue at a moment: its key (an index into <see cref="NativeDeepEndBossCues.Keys"/>), the clock time it falls on, where it plays and its gain.</summary>
    public struct DeepEndBossCue{public byte Key;public float At,Gain;public Vector3 Place;}

    /// <summary>
    /// THE DEEP END's sounds: the `boss.deepend.*` keys and the moments they fall on, read from the record and the rig the picture drew (docs/EXPEDITION-BOSS-BACKROOMS.md section 4 lists each with its brief). <see cref="Collect"/>
    /// lists the cues whose moment falls in (from, to] on the clock; Runtime routes them through NativeAudio's bank path, so a key with no clips is silent. 2D keys play flat. Pure and allocation free.
    /// </summary>
    public static class NativeDeepEndBossCues
    {
        public static readonly string[] Keys={"boss.deepend.drip","boss.deepend.ripple","boss.deepend.lamp-out","boss.deepend.pressure","boss.deepend.break","boss.deepend.pour","boss.deepend.iris","boss.deepend.roar","boss.deepend.wave",
            "boss.deepend.quiet","boss.deepend.bubbles","boss.deepend.breach","boss.deepend.jet-charge","boss.deepend.jet","boss.deepend.shadow","boss.deepend.flop","boss.deepend.thrash","boss.deepend.drain-turn","boss.deepend.drain-roar","boss.deepend.lull","boss.deepend.hands",
            "boss.deepend.seiche-draw","boss.deepend.seiche","boss.deepend.seiche-wall","boss.deepend.whistle","boss.deepend.lap","boss.deepend.lamp-break","boss.deepend.stagger","boss.deepend.tide","boss.deepend.lights-die","boss.deepend.hurt","boss.deepend.death","boss.deepend.drain-away"};
        public const byte Drip=0,Ripple=1,LampOut=2,Pressure=3,Break=4,Pour=5,Iris=6,Roar=7,Wave=8,Quiet=9,Bubbles=10,Breach=11,JetCharge=12,Jet=13,Shadow=14,Flop=15,Thrash=16,DrainTurn=17,DrainRoar=18,Lull=19,Hands=20,SeicheDraw=21,Seiche=22,SeicheWall=23,Whistle=24,Lap=25,LampBreak=26,Stagger=27,Tide=28,LightsDie=29,Hurt=30,Death=31,DrainAway=32;
        /// <summary>The 2D (room-wide) keys.</summary>
        public static bool Flat(byte key)=>key==Drip||key==Pressure||key==Quiet||key==Whistle||key==LightsDie||key==Tide||key==DrainAway||key==SeicheDraw;
        public const int QueueSize=32;private static readonly DeepEndBossCue[] queue=new DeepEndBossCue[QueueSize];private static int queued;private static readonly DeepEndBossCue[] scratch=new DeepEndBossCue[QueueSize];
        public static void Queue(in DeepEndBossView v,DeepEndBossRig rig,float fromTime,float toTime){int k=Collect(v,rig,fromTime,toTime,scratch);for(int i=0;i<k&&queued<QueueSize;i++)queue[queued++]=scratch[i];}
        public static int Drain(DeepEndBossCue[] into){int k=Math.Min(queued,into.Length);Array.Copy(queue,into,k);queued=0;return k;}
        private static DeepEndBossCue[] into;private static int n;private static float from,to;
        private static void Add(byte key,float at,Vector3 place,float gain=1){if(!(at>from&&at<=to)||into==null||n>=into.Length)return;into[n++]=new DeepEndBossCue{Key=key,At=at,Place=place,Gain=gain};}
        public static int Collect(in DeepEndBossView v,DeepEndBossRig rig,float fromTime,float toTime,DeepEndBossCue[] buffer)
        {
            into=buffer;n=0;from=fromTime;to=toTime;if(!v.live||rig==null||!(to>from))return 0;
            var o=rig.Origin;var mouth=rig.MouthAt;var mid=rig.Shown.Position[8];
            if(DeepEndBossView.At(v.spawned))
            {
                var b=NativeDeepEndBossBeats.Current;float s=v.spawned;
                Add(Drip,s+b.Ripple-.3f,o);Add(Ripple,s+b.Ripple,o+new Vector3(5.2f,0,-2.8f));
                for(int i=0;i<6;i++)Add(LampOut,s+b.LightsOut0+(b.LightsOut1-b.LightsOut0)*i/6f,o,.7f);
                Add(Pressure,s+b.DownShot,o);Add(Break,s+b.Break,o,1.2f);Add(Pour,s+b.Break+.3f,o);Add(Iris,s+b.IrisOpen,mouth);Add(Roar,s+b.Roar,mouth,1.2f);Add(Wave,s+b.WaveAt,o);
            }
            if(v.Moving(to,out var m,out _,out float hit))
            {
                float a=v.actionAt,at=a+hit;var t=o+new Vector3(v.targetX,0,v.targetZ);if(float.IsNaN(v.targetX))t=mouth;Add(Quiet,a,o,.8f);
                switch(m.Id)
                {
                    case "breach":Add(Bubbles,a,t);Add(Breach,at,t,1.2f);Add(Pour,at+.7f,mouth);break;
                    case "jet":Add(Iris,a,mouth);Add(JetCharge,a+.15f,mouth);Add(Jet,at,mouth,1.2f);break;
                    case "flop":Add(Shadow,a,t);Add(Flop,at,mid,1.3f);Add(Pour,at+.5f,mid);break;
                    case "thrash":Add(Thrash,a+hit*.5f,mid);break;
                    case "drain":Add(DrainTurn,a,o);Add(DrainRoar,at,o,1.2f);Add(Iris,a,mouth);break;
                    case "lull":Add(Lull,a,t);Add(Hands,at,t);break;
                    case "seiche":Add(SeicheDraw,a,o);Add(Seiche,at,o,1.2f);Add(SeicheWall,at+2*NativeDeepEndArena.Half/m.Speed,o);break;
                    case "whistle":Add(Whistle,a,o,1.1f);break;
                    case "lap":Add(Lap,at,mouth,1.2f);break;
                }
            }
            if(v.parts!=0&&v.partAt!=null)for(int i=0;i<NativeDeepEndBossLook.Lamps&&i<v.partAt.Length;i++)if((v.parts>>i&1)!=0)Add(LampBreak,v.partAt[i],mid);
            if(DeepEndBossView.At(v.staggerAt))Add(Stagger,v.staggerAt,mid);
            if(DeepEndBossView.At(v.phaseAt)){Add(Tide,v.phaseAt+.4f,o);if(v.phase>=3)Add(LightsDie,v.phaseAt+.2f,o);}
            if(DeepEndBossView.At(v.diedAt)){Add(Death,v.diedAt,mouth);Add(DrainAway,v.diedAt+3.2f,o);}
            return n;
        }
    }
}

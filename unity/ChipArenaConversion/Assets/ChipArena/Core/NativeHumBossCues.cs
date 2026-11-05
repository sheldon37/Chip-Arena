using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One boss cue at a moment.</summary>
    public struct HumBossCue{public byte Key;public float At,Gain;public Vector3 At3;}

    /// <summary>
    /// THE HUM's sounds (docs/EXPEDITION-BOSS-BACKROOMS.md "The cue keys", the owner generates the clips): the `boss.hum.*` keys and the moments they fall on, read from the view and the rig the picture drew. <see cref="Collect"/> lists the cues whose moment falls in (from, to] on the
    /// presentation clock; Runtime routes them through NativeAudio's bank path (no stand-in), so a key with no clips is silent. 2D (<see cref="Flat"/>) keys play flat; the rest at their place. Pure and allocation free.
    /// </summary>
    public static class NativeHumBossCues
    {
        public static readonly string[] Keys={"boss.hum.hum.loop.rise","boss.hum.silence","boss.hum.row.out","boss.hum.scratch","boss.hum.tear","boss.hum.starter","boss.hum.tube.strike","boss.hum.head.catch","boss.hum.jaw","boss.hum.ceiling","boss.hum.slam.on",
            "boss.hum.reach.windup","boss.hum.reach","boss.hum.stoop.rise","boss.hum.stoop","boss.hum.row.buzz","boss.hum.row.burst","boss.hum.glass","boss.hum.noclip.sink","boss.hum.noclip.walls","boss.hum.noclip","boss.hum.brake","boss.hum.carpet","boss.hum.fall",
            "boss.hum.shift","boss.hum.charge","boss.hum.pulse","boss.hum.cut","boss.hum.strike.dark","boss.hum.hand.scratch","boss.hum.hand","boss.hum.voices","boss.hum.scream","boss.hum.die.row","boss.hum.rear","boss.hum.tile.rain","boss.hum.step","boss.hum.knuckle","boss.hum.scratch.idle",
            "boss.hum.hurt","boss.hum.tube.break","boss.hum.head.break","boss.hum.choke","boss.hum.clip","boss.hum.death","boss.hum.tube.pop","boss.hum.fold","boss.hum.lamp.back"};
        public const byte HumRise=0,Silence=1,RowOut=2,ScratchIn=3,TearKey=4,Starter=5,TubeStrike=6,HeadCatch=7,JawKey=8,CeilingKey=9,SlamOn=10,ReachWindup=11,ReachKey=12,StoopRise=13,StoopKey=14,RowBuzz=15,RowBurst=16,Glass=17,NoclipSink=18,NoclipWalls=19,NoclipKey=20,Brake=21,Carpet=22,Fall=23,
            Shift=24,Charge=25,Pulse=26,Cut=27,StrikeDark=28,HandScratch=29,HandKey=30,VoicesKey=31,Scream=32,DieRow=33,RearKey=34,TileRain=35,Step=36,Knuckle=37,ScratchIdle=38,Hurt=39,TubeBreak=40,HeadBreak=41,ChokeKey=42,ClipKey=43,DeathKey=44,TubePop=45,Fold=46,LampBack=47;
        /// <summary>The one-line brief for each key (doc section 3's table reads from this).</summary>
        public static readonly string[] Briefs={"the fluorescent hum-buzz climbing alone in an empty room; loopable, pitched by the stage's Pitch","a low shelf cut: the room going quiet; used before Lights Out","a row of fixtures clicking off, far away, with a sag in the hum","fingernails on the back of wallpaper, close and dry","wallpaper tearing slowly, plaster dust, then a wet rip","a tube starter ticking: small, each one louder","a tube striking on, one rising buzz","the head's two tubes catching: a heavy flicker then a steady bar","the diffuser splitting: plastic flexing, glass grinding","ceiling grid wrenching and tiles breaking over its shoulders","every lamp slamming on: one huge thunk of relays on the song's drop",
            "a long arm drawing back through wet carpet","a long whip of air and a wet heavy slap","the back pressing the ceiling tiles, creaking","a double impact on wet carpet, the room's lamps jumping","fixtures brightening and buzzing louder over a second","a row of fixtures exploding in order down the room","safety glass raining onto carpet","something heavy pressing into plaster, scratching inside it","scratching running fast along the inside of the walls","a torn rush of air and plaster: it comes through at speed","hands dragging on carpet; a skid","carpet backing tearing inward, slow and close","a body falling a long way, then a drop out of the ceiling",
            "a flicker's click and, somewhere, a heavy wall set down","the hum rising to a strain, ribs creaking open, a transformer loading up","a ring of noise: the hum's whole spectrum sweeping past, wall to wall","every sound stopping dead","a tube flash, a whip, a heavy blow from behind you in the dark","nails inside the wall right by the ear","an arm bursting through wallpaper and gripping","many muffled familiar voices inside the walls","the hum climbing to a scream, then stopping","one row of fixtures dying with a ballast's thud","its back going up through the ceiling: grid, plaster, a groan of steel","tiles raining and breaking on carpet","one heavy wet footfall","a knuckle of baseboard and rail planting on carpet","a fingernail drumming now and then",
            "a wet thud and the tubes stuttering","a tube bursting and its wires spitting","the head's panel cracking","the Ballast choking: a pitch collapse, a stumble","stuck in a wall: paper stretching, a glitching buzz","the hum falling in pitch and stopping","a tube popping, glass","its arms giving way: wet carpet folding, rails bending","one lamp coming back on, ordinary and quiet"};
        /// <summary>The 2D (room-wide) keys: the hum, the silence, the lamps' slam, the Ballast's ring.</summary>
        public static bool Flat(byte key)=>key==HumRise||key==Silence||key==SlamOn||key==Pulse||key==Cut||key==DeathKey||key==NoclipWalls;
        public const int QueueSize=48;private static readonly HumBossCue[] queue=new HumBossCue[QueueSize];private static int queued;
        public static void Queue(in HumBossView v,HumBossRig rig,HumRoom room,float fromTime,float toTime){int k=Collect(v,rig,room,fromTime,toTime,scratch);for(int i=0;i<k&&queued<QueueSize;i++)queue[queued++]=scratch[i];}
        public static int Drain(HumBossCue[] into){int k=Math.Min(queued,into.Length);Array.Copy(queue,into,k);queued=0;return k;}
        private static readonly HumBossCue[] scratch=new HumBossCue[QueueSize];
        private static HumBossCue[] into;private static int n;private static float from,to;
        private static void Add(byte key,float at,Vector3 place,float gain=1){if(!(at>from&&at<=to)||into==null||n>=into.Length)return;into[n++]=new HumBossCue{Key=key,At=at,At3=place,Gain=gain};}
        public static int Collect(in HumBossView v,HumBossRig rig,HumRoom room,float fromTime,float toTime,HumBossCue[] buffer)
        {
            into=buffer;n=0;from=fromTime;to=toTime;if(!v.live||rig==null||!(to>from))return 0;
            var chest=rig.Shown.Position[NativeHumBossLook.Chest];var head=rig.HeadPos;
            // ---- the arrival ----
            if(rig.Arriving)
            {
                var b=HumBossBeats.Current;float s=v.spawned;
                Add(HumRise,s,head,1);for(int i=0;i<4;i++)Add(RowOut,s+b.Row(i),head,.8f);
                Add(ScratchIn,s+b.Bulge,head);Add(TearKey,s+b.TearOpen,head);
                for(int i=0;i<7;i++){Add(Starter,s+b.Tube(i)-.12f,chest,.6f+.06f*i);Add(TubeStrike,s+b.Tube(i),chest,.7f+.06f*i);}
                Add(HeadCatch,s+b.HeadCatch,head);Add(JawKey,s+b.JawSplit,head);Add(CeilingKey,s+b.Straighten+.2f,chest);
            }
            if(HumBossView.At(v.spawned))Add(SlamOn,v.spawned+HumBossBeats.Current.Hold,head,1.2f);
            // ---- the move playing ----
            int m=v.Move;
            if(m>=0&&HumBossView.At(v.actionAt))
            {
                var mv=NativeHumBoss.Moves[m];float a=v.actionAt,hit=HumBossView.At(v.hitAt)?v.hitAt:a+mv.Wind;
                switch(mv.Id)
                {
                    case "reach":Add(ReachWindup,a,chest);Add(ReachKey,hit,rig.Limb(rig.ActSide>0?3:2));break;
                    case "stoop":Add(StoopRise,a,chest);Add(StoopKey,hit,chest,1.2f);break;
                    case "row":Add(RowBuzz,a,head);Add(RowBurst,hit,head,1.2f);Add(Glass,hit+.15f,head);break;
                    case "lunge":Add(NoclipSink,a,chest);Add(NoclipWalls,a+.2f,chest);Add(NoclipKey,hit,chest,1.2f);Add(Brake,hit+mv.Strike,chest);break;
                    case "shift":Add(Shift,a,chest);break;
                    case "pulse":Add(Charge,a,chest);Add(Pulse,hit,chest,1.2f);break;
                    case "cut":Add(Cut,a,head);Add(Silence,a,head);Add(StrikeDark,hit,chest,1.2f);break;
                    case "hand":Add(HandScratch,a,new Vector3(v.handX,v.handY,v.handZ));Add(HandKey,hit,new Vector3(v.handX,v.handY,v.handZ));break;
                    case "voices":Add(VoicesKey,a,head);break;
                    case "tear":if(HumBossView.At(v.tearX))Add(Carpet,a,new Vector3(v.tearX,.1f,v.tearZ));if(HumBossView.At(v.tearX))Add(Fall,hit,new Vector3(v.tearX,.1f,v.tearZ));break;
                }
            }
            // ---- the phase changes, the breaks, the staggers ----
            if(HumBossView.At(v.phaseAt)&&v.phase==2){Add(Scream,v.phaseAt,head,1.2f);Add(Silence,v.phaseAt+2.25f,head);for(int r=0;r<3;r++)Add(DieRow,v.phaseAt+2.25f+r*.26f,head,.9f);}
            if(HumBossView.At(v.phaseAt)&&v.phase>=3){Add(RearKey,v.phaseAt+.7f,chest,1.2f);Add(TileRain,v.phaseAt+1.4f,chest);}
            for(int i=0;i<7;i++){float at=v.PartAt(i);if(HumBossView.At(at))Add(TubeBreak,at,rig.Shown.Position[NativeHumBossLook.Tube(i)]);}
            {float at=v.PartAt(7);if(HumBossView.At(at))Add(HeadBreak,at,head);}
            if(HumBossView.At(v.staggerAt)&&v.staggerCause=="choke")Add(ChokeKey,v.staggerAt,chest);
            if(HumBossView.At(v.clipAt))Add(ClipKey,v.clipAt,chest);
            // ---- the death: tubes, the fold, the hum falling, silence, one lamp after another ----
            if(HumBossView.At(v.diedAt))
            {
                Add(DeathKey,v.diedAt,head,1.2f);for(int i=0;i<7;i++)Add(TubePop,v.diedAt+.18f+(6-i)*.17f,rig.Shown.Position[NativeHumBossLook.Tube(i)],.9f);Add(Fold,v.diedAt+.6f,chest);
                if(room!=null)for(int i=0;i<NativeHumArena.FixtureCount;i+=3)Add(LampBack,v.diedAt+HumBossStage.DeathReturn+BossMath.Hash(0x71u,i)*1.2f,room.FixtureAt[i],.5f);
            }
            // ---- life: a foot or a knuckle landing; a blow; idle drumming every so often ----
            if(rig.Landed!=0&&!v.Dead(to)&&rig.Flip<=0)for(int i=0;i<4;i++)if((rig.Landed&(1<<i))!=0)Add(i<2?Step:Knuckle,to,rig.LimbAt[i],i<2?.7f:.55f);
            if(HumBossView.At(rig.LastHit))Add(Hurt,rig.LastHit,chest,.7f);
            if(!v.Dead(to)){const float period=4.3f;float k=(float)Math.Floor(to/period)*period+1.3f;Add(ScratchIdle,k,rig.Limb(2),.5f);}
            into=null;return n;
        }
    }
}

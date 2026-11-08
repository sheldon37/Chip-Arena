#if UNITY_EDITOR
using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        /// <summary>
        /// Vote to skip on a real boss intro (Core NativeCutsceneSkip; the boss build checks run it on every boss): each connected
        /// human must vote, once; the ballot crosses the real codec; when all agree the intro ends on the next tick without the arena
        /// clock moving, released as at its natural end, the boss song carrying on from the fight's start.
        /// </summary>
        public void ValidateCutsceneSkip(Action<bool,string> check)
        {
            check(Pve.Holding&&Pve.Cinematic=="intro","an intro to skip");
            var humans=Pilots.Where(p=>!p.Seat.AuthorityBoss&&!p.Seat.devDummy&&!p.Seat.bot&&p.Seat.connected).ToArray();if(humans.Length==0)return;
            StepCutsceneSkip();check(CutsceneSkip.Key!=null&&CutsceneSkip.Required==humans.Length&&!VoteSkip(999),"the intro takes a vote from each human, and only them");
            for(int i=0;i<humans.Length;i++)
            {
                StepCutsceneSkip();check(Pve.Holding&&!CutsceneSkip.Due,"one short keeps the intro");
                check(VoteSkip(humans[i].Seat.id)&&!VoteSkip(humans[i].Seat.id),"one vote each");
                if(i==0){var wire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=NetworkFrame()})).frame.cutsceneSkip;
                    check(wire!=null&&wire.key==CutsceneSkip.Key&&wire.need==humans.Length&&wire.voters.Length==1&&wire.voters[0]==humans[0].Seat.id,"the ballot crosses the real codec");}
            }
            float clock=Time;int serial=CutsceneSkip.Serial;float duration=Pve.HoldDuration;
            StepCutsceneSkip();PveHoldTick(1f/60);
            check(!Pve.Holding&&Pve.Stage=="boss"&&Time==clock&&CutsceneSkip.Serial==serial+1&&CutsceneSkip.Key==null,"all agree: the intro ends at once, the clock unmoved");
            check(Math.Abs(BossSongTime-duration)<.02,"the boss song plays on from the fight's start ("+BossSongTime.ToString("0.00")+" of "+duration.ToString("0.00")+")");
            if(ZoneBoss!=null)check(ZoneBoss.action==null||ZoneBoss.action!="arrive","the boss's arrival is over");
            if(ZoneBoss?.fallen!=null){var song=FallenSongClock();check(Math.Abs(song.clip-NativeFallenSong.Drop)<.02,"NOCTIEL's song from the drop ("+song.clip.ToString("0.00")+")");}
        }
    }
}
#endif

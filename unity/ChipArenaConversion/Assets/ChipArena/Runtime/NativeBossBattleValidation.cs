#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidatePracticeBookmark()
        {
            if(Pve.Holding && Pve.Cinematic=="reveal")PveHoldTick(Pve.HoldLeft+.01f);
            if(Pve.Phase!=PracticePhase || Match.CanEarnProgression(LocalId) || BossOpening ||
                Pve.Rows.Values.Any(r=>r.Picks.Any(p=>p.Source==NativeBossPicks.Source)))
                throw new Exception("Practice opening mismatch "+PracticeBoss+" phase "+PracticePhase+" actual "+Pve.Phase+
                    " actor phase "+ZoneBoss?.phase+" stage "+Pve.Stage+" match "+Match.Phase+" hold "+Pve.Cinematic+
                    " hp "+zoneBossActor?.Hp+"/"+zoneBossActor?.MaxHp+" progression "+Match.CanEarnProgression(LocalId));
            if(PracticeBoss=="siren" && PracticePhase==2 && (!Pve.Hidden || Pve.Possessed==0))
                throw new Exception("Siren practice missing possessed add");
            if(ZoneBoss!=null && ZoneBoss.phase!=PracticePhase)
                throw new Exception("Practice actor phase mismatch "+PracticeBoss+" phase "+PracticePhase+" actual "+ZoneBoss.phase);
            if(PracticeBoss=="station" && PracticePhase>=2 && ZoneBoss.barrierDownAt<0)
                throw new Exception("Station practice missing barrier transition");
            if(PracticeBoss=="station" && PracticePhase==3 && (ZoneBoss.boardDownAt<0 || ZoneBoss.roofOpenAt<0))
                throw new Exception("Station phase three missing arena changes");
            if(PracticeBoss=="noctiel" && PracticePhase>1 && (ZoneBoss.fallen.draftAt<0 || ZoneBoss.fallen.chipOffer.Length!=3))
                throw new Exception("NOCTIEL practice missing phase draft");
        }
        public void ValidateBossOpening()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Boss opening: "+why);}
            Check(BossOpening&&!Pve.Holding&&BossSongAt<0,"selection before music/intro");
            float clock=Time;var members=Members().ToArray();
            foreach(var p in members)Check(Pve.Rows[p.Seat.id].Picks.Count==5&&Pve.Rows[p.Seat.id].Picks.All(pick=>pick.Tier=="common"&&pick.Source==NativeBossPicks.Source),"five normal-rarity choices per pilot, marked as the opening's");
            for(int i=0;i<120;i++)BossOpeningTick();
            Check(Time==clock&&BossOpening&&!Pve.Holding,"combat and clock frozen while choosing");
            Check(Math.Abs(Pve.PickLeft-(NativeBossPicks.Seconds-120/60f))<.05f,"the shared countdown runs while they choose");
            var packet=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=NetworkFrame()})).frame;
            Check(packet.Valid(Content)&&packet.pve.rows.All(r=>r.picks.Length==5&&r.open>0)&&Math.Abs(packet.pve.pickLeft-Pve.PickLeft)<.001f,"opening picks and the countdown survive real codec");
            if(members.Length>1)
            {
                var other=members[1];other.Seat.connected=false;
                BossOpeningTick();Check(BossOpening,"remaining connected player still choosing");
                other.Seat.connected=true;Check(Pve.Rows[other.Seat.id].Picks.Count==5,"reconnect retains choices");
            }
            foreach(var p in members)
            {
                var row=Pve.Rows[p.Seat.id];int before=p.Seat.chips.Values.Sum();
                ClosePvePick(p);BossOpeningTick();Check(BossOpening,"closing cannot skip selections");OpenPvePick(p);
                for(int i=0;i<5;i++)
                {
                    var pick=row.Open;Check(pick?.Offers?.Count==3,"three eligible offers");
                    string chip=pick.Offers[0];Check(ChoosePve(p,pick.Id,chip),"install choice");
                    Check(!ChoosePve(p,pick.Id,chip),"duplicate request cannot install again");
                    if(i<4){BossOpeningTick();Check(BossOpening&&!Pve.Holding,"fewer than five keeps intro locked");}
                }
                Check(p.Seat.chips.Values.Sum()==before+5&&row.Picks.Count==0,"exactly five installed");
            }
            BossOpeningTick();Check(!BossOpening&&Pve.Holding&&Pve.Cinematic=="intro"&&BossSongAt==Time,"intro after final player");
            // Existing boss combat fixtures require their original unmodified stats.
            foreach(var p in members){p.Seat.chips.Clear();RefreshLiveStats(p);}
            Debug.Log("PASS native boss opening: five selections, duplicate refusal, clock hold, codec and intro barrier");
        }
        public void ValidateBossBattle(Action<NativeGameFrame> replicaCheck)
        {
            int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("Direct boss: "+why);}
            string id=NativeBossBattle.Id(MapId);
            Check(Expedition==null&&Pve.Started&&Pve.Holding&&Pve.Cinematic=="intro","direct intro without run");
            Check(ZoneBoss.archetype==NativeZoneBossRules.Archetype(MapId)&&ZoneBoss.Valid(),"selected boss only");
            Check(Pve.Rows.Count==PartySize&&Pve.Rows.Values.All(r=>r.Open==null&&r.Picks.Count==0)&&Pve.Content.machines.Length==0,"opening draft consumed; no shops");
            Check(Pve.MaxHp==NativeBossBattle.Health(id,PartySize)&&(float)Pve.Boss.Stats["damage"]==NativeBossBattle.Damage(PartySize),"party scaling");
            foreach(var p in Members())Check(ExpeditionSupported(Vec(p.Body.position)-new V3(0,NativeLocomotionRules.HalfHeight,0),NativeLocomotionRules.Radius,NativeLocomotionRules.HalfHeight*2),"supported party landing "+p.Seat.id);
            var packet=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=NetworkFrame()})).frame;
            Check(packet.Valid(Content)&&packet.areaIdentity==null,"normal co-op wire frame");replicaCheck(packet);
            ValidateCutsceneSkip((ok,why)=>Check(ok,"skip vote: "+why));
            PveHoldTick(Pve.HoldDuration+.01f);Match.SynchronizeExpeditionClock(4);StepZoneBoss();
            Check(!Pve.Holding&&Pve.Stage=="boss"&&ZoneBoss.action!="arrive","normal intro released");
            zoneBossActor.Hp=zoneBossActor.MaxHp*.25f;StepZoneBoss();Check(ZoneBoss.phase>=2,"normal phase authority");
            zoneBossActor.Hp=0;StepBossBattle();Check(ZoneBoss.deathAt>=0&&Match.Phase!=NativePhase.Match,"death scene before completion");
            Match.SynchronizeExpeditionClock(Time+8);StepBossBattle();Check(Match.Phase==NativePhase.Match&&Pve.Outcome=="victory","win ends fight");
            Debug.Log("PASS native direct boss: "+id+" / "+PartySize+" pilots / "+n+" checks");
        }
        public void ValidateBossBattleWipe()
        {
            PveHoldTick(Pve.HoldDuration+.01f);Match.SynchronizeExpeditionClock(4);
            foreach(var p in Members()){p.Hp=0;p.Seat.alive=false;}
            StepBossBattle();if(!Pve.Holding||Pve.Cinematic!="defeat")throw new Exception("Direct boss wipe did not end encounter");
            PveHoldTick(4);if(Match.Phase!=NativePhase.Match||Pve.Outcome!="defeat")throw new Exception("Direct boss wipe did not finish");
        }
    }
}
#endif

using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private int launchingPractice;
        private bool mockPractice;
        private string mockOwner;
        private NativeLabBuild practiceBuild;

        private bool CanPractice(string boss)
        {
            if(!string.Equals(mockOwner,account?.Account?.id,StringComparison.Ordinal)){mockOwner=account?.Account?.id;mockPractice=false;}
            return !Online && account?.Account!=null && NativePracticeRules.Available(boss,account.EarnedBosses,account.Developer,mockPractice);
        }

        private void RequestPractice(string boss,int phase,NativeLabBuild build)
        {
            if(phase<1 || phase>NativePracticeRules.Phases(boss) || !CanPractice(boss))return;
            RequestStage(StageLoadKind.Boss,()=>{
                if(!CanPractice(boss))return;
                var valid=NativeLabBuild.Decode(build.Code,content,account.Access);
                if(valid==null || valid.Weapon=="siren"){Notice("Choose a pilot weapon in Chip Lab first.");return;}
                bool wasPve=prefs.pve,wasExp=prefs.expedition,wasSkip=prefs.skipWaves;
                int bots=prefs.bots;string map=prefs.map;
                try
                {
                    launchingPractice=phase;practiceBuild=valid;bossBattlePick=boss;
                    prefs.pve=true;prefs.expedition=false;prefs.skipWaves=true;prefs.bots=0;
                    StartMatch();
                }
                finally { launchingPractice=0;prefs.bots=bots;prefs.pve=wasPve;prefs.expedition=wasExp;prefs.skipWaves=wasSkip;prefs.map=map; }
            });
        }
        private void RetryPractice()
        {
            if(arena?.PracticePhase>0)RequestPractice(arena.PracticeBoss,arena.PracticePhase,practiceBuild);
        }

        private string PracticeResult
        {
            get
            {
                if(arena==null || arena.PracticePhase==0 || arena.Pve==null)return null;
                var v=arena.Pve;
                if(arena.Local?.Hp<=0 || v.Cinematic=="defeat")return "Pilot down";
                if(v.Outcome=="victory" || v.Stage=="victory" || arena.ZoneBoss?.deathAt>=0 ||
                    v.Phase>arena.PracticePhase || v.Holding&&v.Cinematic=="reveal"&&v.Reveal>arena.PracticePhase)return "Phase cleared";
                return null;
            }
        }
        private bool PracticeResultGui(float width,float height)
        {
            string result=PracticeResult;
            if(result==null)return false;
            var panel=new Rect((width-360)/2,(height-140)/2,360,140);
            GUI.Box(panel,"");
            GUILayout.BeginArea(new Rect(panel.x+16,panel.y+12,328,116));
            GUILayout.Label(result+" · Phase "+arena.PracticePhase);
            if(GUILayout.Button("Retry phase"))RetryPractice();
            if(GUILayout.Button("Back to Boss Battle")){ReturnToLobby();Go(MenuScreen.Boss);}
            GUILayout.EndArea();return true;
        }

        private void PracticeMenu(Rect d,string boss,float alpha)
        {
            bool unlocked=CanPractice(boss),live=ExpLive(alpha);
            int bit=NativePracticeRules.Bit(boss);
            bool earned=((account?.EarnedBosses??0)&bit)!=0;
            const float h=21;
            string status=earned?"Defeated":unlocked?"DEV access":"Defeat this boss to unlock";
            GUI.Label(new Rect(d.x,d.y,d.width,h),"Phase Practice · "+status);
            float y=d.y+h;
            if(account?.Developer==true){
                bool enabled=GUI.enabled;GUI.enabled=enabled&&live;
                mockPractice=GUI.Toggle(new Rect(d.x,y,d.width,h),mockPractice,"Preview earned clears only");
                GUI.enabled=enabled;y+=h;
            }
            int count=NativePracticeRules.Phases(boss);
            for(int phase=1;phase<=count;phase++){
                float w=d.width/count;
                if(ExpChipButton(new Rect(d.x+(phase-1)*w,y,w-3,h),null,"P"+phase,16000+phase,alpha,live,false,CanPractice(boss)))
                    RequestPractice(boss,phase,SelectedLabBuild());
            }
        }
    }

    public sealed partial class NativeArena
    {
        public int PracticePhase;
        public string PracticeBoss;

        private void OpenPracticePhase()
        {
            var v=Pve;
            v.HoldLeft=0;
            PveHoldTick(0); // Use the live intro release and its music origin.
            if(PracticePhase==1)return;
            if(ZoneBoss==null)
            {
                // Phase two possesses a horde actor. Three enlarges the Siren, including in phase four.
                if(PracticePhase==4){BeginPveHold("reveal",3);v.HoldLeft=0;PveHoldTick(0);}
                v.Boss.Hp=v.MaxHp*(1-(PracticePhase-1)*.25f);
                BeginPveHold("reveal",PracticePhase);
                return;
            }
            if(ZoneBoss.fallen!=null)
            {
                zoneBossActor.Hp=v.Boss.Hp=zoneBossActor.MaxHp*NativeFallenBoss.PhaseFrom[PracticePhase-1];
                ZoneBoss.phase=PracticePhase-1;
                ZoneBoss.fallen.phaseChangeAt=Time;
                FallenPhase(); // The actual phase draft and music break.
                return;
            }
            for(int phase=2;phase<=PracticePhase;phase++)
            {
                float fraction=NativeZoneBossRules.PhaseStart(ZoneBoss.archetype,phase);
                zoneBossActor.Hp=v.Boss.Hp=zoneBossActor.MaxHp*fraction;
                // Enter the requested transition directly. Combat ordinarily detects it after a hit.
                if(ZoneBoss.backrooms!=null)
                {
                    NativeBackroomsBossRules.AdvancePhase(ZoneBoss,Time);
                    v.Phase=ZoneBoss.phase;
                }
                else AdvanceIndustrialBossPhase();
                SyncZoneBossArena();
                if(phase<PracticePhase)
                {
                    // Let the previous authored transition expire before sampling tide/arena timestamps for the next.
                    Match.SynchronizeExpeditionClock(Time+Math.Max(6,ZoneBoss.duration)+.01f);
                }
            }
        }
    }
}

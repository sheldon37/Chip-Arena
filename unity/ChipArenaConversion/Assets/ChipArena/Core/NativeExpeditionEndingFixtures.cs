using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// MOCK views for the quota module and the end sequence (Claude, 2026-10-20): every state of both, for the DEV preview (DEV panel ·
    /// Previews · EXPEDITION QUOTA & ENDING), the managed checks and the offline renders, without playing a run. Hand-built
    /// ExpeditionViews in the replicated contract's own shape (the live zones' ids and the 36-kill quota), labelled MOCK: no value here is
    /// a game rule, a reward, a price or a proposed number, and nothing is sent, saved or bound.
    /// </summary>
    public static class NativeExpeditionEndingFixtures
    {
        public enum Quota { Off, Running, Closing, Last, Complete, Boss, Cleared, LoopTwo }
        public static readonly string[] QuotaNames={"OFF","RUNNING 12/36","CLOSING 28/36","LAST 3","QUOTA MET","BOSS","CLEARED","LOOP 2"};
        public enum End { Off, Victory, VictoryDown, VictoryGuest, Extracted, ExtractedSaved, RunEnded, Wiped, Failed, HostChange, LateJoiner, Minimal }
        public static readonly string[] EndNames={"OFF","VICTORY","VICTORY · DOWN","VICTORY · GUEST","EXTRACTED · SAVING","EXTRACTED · SAVED","RUN ENDED","WIPED","FAILED (SOLO)","HOST CHANGE","LATE JOINER","MISSING DATA"};
        public const string Run="mock-run",World="exp-city",Ward=NativeExpeditionJoinZones.WardZero,Station=NativeExpeditionJoinZones.Station,Ironworks=NativeExpeditionJoinZones.Ironworks;
        public const string Me="sheldon",Banner="DEV PREVIEW · EXPEDITION QUOTA & ENDING · MOCK VALUES";
        /// <summary>A late joiner sees the ending settled (the model's first sight of the run).</summary>
        public static bool FirstSight(End e)=>e==End.LateJoiner;

        private static ExpeditionViewMember M(string account,string name,string weapon,bool host=false,bool alive=true,bool connected=true,bool extracted=false,string area=World)
            =>new ExpeditionViewMember{account=account,name=name,weapon=weapon,host=host,alive=alive,connected=connected,extracted=extracted,area=area,seat=1};
        private static ExpeditionViewInstance I(string id,int progress,bool cleared=false,bool entered=true,bool current=false,string[] bosses=null,string[] defeated=null)
            =>new ExpeditionViewInstance{id=id,target=36,progress=progress,cleared=cleared,entered=entered||cleared,current=current,bosses=bosses??Array.Empty<string>(),defeated=defeated??Array.Empty<string>()};
        private static ChipCount[] Build()=>new[]{new ChipCount{id="vital",count=2},new ChipCount{id="overclock",count=1},new ChipCount{id="ricochet",count=1},new ChipCount{id="piercing",count=1},
            new ChipCount{id="ember",count=1},new ChipCount{id="static",count=1},new ChipCount{id="multishot",count=1},new ChipCount{id="splitter",count=1},new ChipCount{id="homing",count=1},new ChipCount{id="shrapnel",count=1}};
        private static ExpeditionView Base(string viewer,string area,ExpeditionPhase phase,int loop=0)
        {
            var v=new ExpeditionView{run=Run,world=World,area=area,host=Me,viewer=viewer,phase=phase,loop=loop,elapsed=1694.4,areaIdentity=new ExpeditionCohortTicket{run=Run,area=area},
                members=new[]{M(Me,"Sheldon","pistol",host:true,area:area),M("nova","Nova","rifle",area:area),M("kestrel","Kestrel","greatsword",area:area)},
                instances=new[]{I(Ward,36,cleared:true),I(Station,36,cleared:true),I(Ironworks,36,cleared:true)}};
            v.mine=new ExpeditionViewPersonal{account=viewer,connected=true,alive=true,wallet=1240,chips=Build()};return v;
        }
        private static void Completion(ExpeditionView v,bool won,int seconds,int cleared){foreach(var m in v.members)if(m.connected&&!m.extracted||m.extracted)m.xpCompletion=new AccountXpCompletion{completionId="exp_"+Run+"_"+v.loop,mode="expedition",won=won,seconds=seconds,cleared=cleared};}

        /// <summary>The quota fixture's view (the viewer in the zone with a party of three; MOCK values).</summary>
        public static ExpeditionView QuotaView(Quota q,int progressOverride=-1)
        {
            if(q==Quota.Off)return null;
            string area=q==Quota.Closing||q==Quota.Boss?Station:q==Quota.Last||q==Quota.Complete?Ironworks:Ward;
            int progress=q==Quota.Running?12:q==Quota.Closing?28:q==Quota.Last?33:q==Quota.LoopTwo?4:36;if(progressOverride>=0)progress=Math.Min(36,progressOverride);
            var v=Base(Me,area,ExpeditionPhase.Instance,q==Quota.LoopTwo?1:0);
            v.instances=new[]{I(Ward,area==Ward?progress:36,cleared:area!=Ward,current:area==Ward),I(Station,area==Station?progress:area==Ironworks?36:0,cleared:area==Ironworks,entered:area!=Ward,current:area==Station),I(Ironworks,area==Ironworks?progress:0,entered:area==Ironworks,current:area==Ironworks)};
            var cur=v.CurrentBuilding();
            if(q==Quota.Boss){cur.bosses=new[]{"station-boss"};v.bossArrivals=new[]{new ExpeditionBossArrivalView{id="station-boss",area=area,archetype="station-boss",hp=6000,maxHp=9000}};}
            if(q==Quota.Cleared){cur.cleared=true;cur.bosses=new[]{"m1-ward-siren"};cur.defeated=new[]{"m1-ward-siren"};}
            if(q==Quota.Closing)v.members[2].alive=false;
            return v;
        }

        /// <summary>The ending fixture's view (MOCK values).</summary>
        public static ExpeditionView EndView(End e)
        {
            switch(e)
            {
                case End.Off:return null;
                case End.Victory:case End.LateJoiner:case End.VictoryDown:case End.VictoryGuest:case End.HostChange:
                {
                    string viewer=e==End.VictoryGuest?"nova":Me;var v=Base(viewer,World,ExpeditionPhase.Victory);v.finalUnlocked=true;Completion(v,true,1694,3);
                    foreach(var i in v.instances){i.bosses=new[]{"boss"};i.defeated=new[]{"boss"};}
                    v.continueVotes=new[]{"kestrel"};v.members[1].extractVote=e!=End.VictoryGuest;
                    bool down=e==End.VictoryDown;v.members[0].alive=!down;v.mine.alive=!down;
                    string[] eligible=down?new[]{"kestrel","nova"}:new[]{"kestrel","nova",Me};
                    v.dice=new[]{new ExpeditionDice{boss="0:"+World+":final:boss",each=1,eligible=eligible,rolls=down?new[]{4,17}:new[]{4,17,12},winners=new[]{"nova"}}};
                    v.loot=down?Array.Empty<ExpeditionViewLoot>():new[]{new ExpeditionViewLoot{id="0:"+World+":final:boss:1",source="boss",owner=viewer,boss="0:"+World+":final:boss"}};
                    v.mine.rewards=down?Array.Empty<ExpeditionViewReward>():new[]{new ExpeditionViewReward{id="0:"+World+":final:boss:1",source="boss",boss="0:"+World+":final:boss"}};
                    if(e==End.VictoryGuest){v.mine.wallet=860;v.mine.chips=new[]{new ChipCount{id="frame",count=1},new ChipCount{id="dash",count=1},new ChipCount{id="ricochet",count=2},new ChipCount{id="echo-strike",count=1}};}
                    if(e==End.HostChange){v.migrating=true;v.transfer=new ExpeditionViewTransfer{active=true,stage=ExpeditionTransferStage.Frozen,oldHost=Me,nextHost="nova",nextEpoch=1};}
                    return v;
                }
                case End.Extracted:case End.ExtractedSaved:
                {
                    var v=Base(Me,World,ExpeditionPhase.Town);v.instances=new[]{I(Ward,36,cleared:true),I(Station,36,cleared:true),I(Ironworks,14,current:false)};v.instances[0].defeated=new[]{"m1-ward-siren"};v.instances[1].defeated=new[]{"station-boss"};
                    v.members[0].extracted=true;v.members[0].alive=v.members[0].connected=false;v.members[0].host=false;v.members[1].host=true;v.host="nova";v.mine.extracted=true;v.mine.alive=false;v.mine.extractionSaved=e==End.ExtractedSaved;
                    v.members[0].xpCompletion=new AccountXpCompletion{completionId="exp_"+Run+"_0",mode="expedition",won=false,seconds=1388,cleared=2};v.elapsed=1460;return v;
                }
                case End.RunEnded:
                {
                    var v=Base(Me,World,ExpeditionPhase.Ended);v.instances[2].cleared=false;v.instances[2].progress=20;foreach(var m in v.members){m.extracted=true;m.alive=m.connected=false;}
                    v.mine.extracted=true;v.mine.alive=false;v.mine.extractionSaved=true;Completion(v,false,2102,2);v.elapsed=2150;return v;
                }
                case End.Wiped:
                {
                    var v=Base(Me,Station,ExpeditionPhase.Instance);v.members=new[]{M(Me,"Sheldon","pistol",host:true,alive:false,area:Station),M("nova","Nova","rifle",alive:false,area:Station),M("kestrel","Kestrel","greatsword",alive:false,area:Station),M("rook","Rook","daggers",alive:false,area:Station)};
                    v.instances=new[]{I(Ward,36,cleared:true,defeated:new[]{"m1-ward-siren"}),I(Station,36,current:true,bosses:new[]{"station-boss"}),I(Ironworks,0,entered:false)};
                    v.bossArrivals=new[]{new ExpeditionBossArrivalView{id="station-boss",area=Station,archetype="station-boss",hp=2210,maxHp=9000}};
                    v.mine.alive=false;v.mine.wallet=410;Completion(v,false,1127,1);v.elapsed=1127.6;return v;
                }
                case End.Failed:
                {
                    var v=Base(Me,Ward,ExpeditionPhase.Instance);v.members=new[]{M(Me,"Sheldon","pistol",host:true,alive:false,area:Ward)};
                    v.instances=new[]{I(Ward,31,current:true),I(Station,0,entered:false),I(Ironworks,0,entered:false)};v.mine.alive=false;v.mine.wallet=180;
                    v.mine.chips=new[]{new ChipCount{id="vital",count=1},new ChipCount{id="overclock",count=1}};Completion(v,false,402,0);v.elapsed=402.9;return v;
                }
                default:
                {
                    // Missing optional data: no account XP completion, no names or weapons, no chips, an empty wallet, no dice.
                    var v=Base(Me,Ward,ExpeditionPhase.Instance);v.members=new[]{new ExpeditionViewMember{account=Me,host=true,connected=true,alive=false,area=Ward},new ExpeditionViewMember{account="pilot-2",connected=false,alive=true,area=Ward}};
                    v.instances=new[]{I(Ward,5,current:true)};v.mine=new ExpeditionViewPersonal{account=Me,connected=true,alive=false};v.elapsed=75.2;return v;
                }
            }
        }
    }
}

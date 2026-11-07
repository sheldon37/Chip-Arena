using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private void SelectBossBattle(string id)
        {
            if(server!=null||arena!=null||bossBattlePick==id)return;
            var o=CaptureRoomOptions();o.boss=id;SetRoomOptions(o);
            foreach(var peer in peers)peer.Ready=false;
            if(Hosting)LobbyBroadcast();
        }
        private void StartBossBattle(string id)
        {
            if(server!=null||!NativeBossBattle.Zone(id))return;
            // Picking has already invalidated ready votes; pressing FIGHT must not invalidate them again.
            if(bossBattlePick!=id)SelectBossBattle(id);
            prefs.pve=true;prefs.expedition=false;prefs.skipWaves=true;prefs.bots=0;sandboxWanted=false;
            RequestStage(StageLoadKind.Boss,StartMatch);
        }
        private ConversionContent bossBattleBaseContent;
        private string bossBattlePrepared;
        private void ResetBossBattleContent(){if(bossBattleBaseContent!=null)content=bossBattleBaseContent;bossBattleBaseContent=null;bossBattleBundle=null;bossBattlePrepared=null;}
        private void PrepareBossBattle(string id)
        {
            if(bossBattlePrepared==id&&bossBattleBundle?.Content==content)return;
            var original=bossBattleBaseContent??content;
            var bundle=ExpeditionRunBundle.BossBattle(id,original,DeploymentAuthoredJson);
            bossBattleBaseContent=original;bossBattlePrepared=id;bossBattleBundle=bundle;content=bundle.Content;
        }
        private void BossBattleEndTick()
        {
            if(arena==null||arena.Replica||arena.Expedition!=null||arena.ZoneBoss==null||match.Phase!=NativePhase.Match)return;
            // Completion is authoritative. The newer lobby epoch returns every guest without leaving the room.
            afterMatch=MenuScreen.Boss;ReturnToLobby();Go(Hosting?MenuScreen.Lobby:MenuScreen.Boss);
        }
    }
    public sealed partial class NativeArena
    {
        public void ConfigureBossBattle(string id,ExpeditionRunBundle bundle,NativePveContent source)
        {
            if(Replica||!NativeBossBattle.Zone(id)||MapId!=NativeBossBattle.Map(id)||Pilots.Any(p=>p.Seat.bot&&!p.Seat.AuthorityBoss))throw new InvalidOperationException("Direct boss fight requires human party seats.");
            // The boss arrival is this mode's countdown; do not play another countdown after it.
            if(Match.Phase==NativePhase.Countdown){Match.SynchronizeExpeditionClock(Match.PhaseEnd);Match.Tick(0);}
            GeneratedRun=bundle;var area=bundle.Layout.areas.Single(a=>a.map==MapId);
            var data=JsonUtility.FromJson<NativePveContent>(JsonUtility.ToJson(source));NativeExpeditionLayout.BindContent(area,data);
            // Test encounters have no shops, recruits, quota or self-revive delay.
            data.machines=Array.Empty<PveMachine>();data.machineBlocks=Array.Empty<ContentBlock>();
            ConfigurePve(data,true,NativeBossBattle.Seed);Pve.Started=true;Pve.Stage="boss";Pve.Wave=1;
            foreach(var p in Members())Pve.Rows[p.Seat.id]=new NativePveRow{Pilot=p};
            Pve.MaxHp=NativeBossBattle.Health(id,PartySize);var boss=Pve.Boss;boss.Stats["hp"]=boss.Hp=Pve.MaxHp;boss.Stats["damage"]=NativeBossBattle.Damage(PartySize);boss.Seat.alive=true;
            SetBossHidden(false);var point=area.bossPoints[0];boss.Body.position=Vec(point.Position)+Vector3.up*NativeLocomotionRules.HalfHeight;
            string kind=NativeZoneBossRules.Archetype(MapId);SpawnZoneBoss(kind,kind==NativeZoneBossRules.Station?new Vector3(0,0,1.5f):Vec(point.Position),kind==NativeZoneBossRules.Station?0:90);
            if(ZoneBoss.backrooms!=null)
            {var b=ZoneBoss.backrooms;int i=0;foreach(var p in Members()){p.Body.position=new Vector3(b.cx+(i%4-1.5f)*1.8f,.85f,b.cz-14+i/4*1.8f);p.Body.linearVelocity=Vector3.zero;i++;}BackroomGeometry();PoseBackroomBody();}
            else
            {int i=0;foreach(var p in Members()){var slots=area.doors[0].entries;var at=slots[i++%slots.Length];p.Body.position=Vec(at.Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);p.Yaw=at.yaw;p.Body.linearVelocity=Vector3.zero;}}
            foreach(var p in Pilots)p.Body.transform.position=p.Body.position;
            Physics.SyncTransforms();if(id=="noctiel")ConfigureFallenBattle();BeginBossOpening();
        }
        // Stage is replicated; the simulation clock stays frozen until connected pilots finish.
        public bool BossOpening=>Pve!=null&&Expedition==null&&Pve.Stage=="boss-picks";
        private void BeginBossOpening()
        {
            Pve.Stage="boss-picks";Pve.Holding=false;Pve.Cinematic=null;
            if(ZoneBoss!=null){ZoneBoss.action=null;ZoneBoss.actionAt=ZoneBoss.hitAt=-100;ZoneBoss.duration=0;}
            foreach(var p in Members())
            {
                for(int i=0;i<5;i++)GrantPvePick(p,"rainbow",false);
                OpenPvePick(p);
            }
        }
        private bool BossOpeningTick()
        {
            if(Replica||Pve==null||Expedition!=null||!Pve.Skip||Match.Phase!=NativePhase.Fight)return false;
            if(!Pve.Started)StartPve();
            if(!BossOpening)return false;
            foreach(var row in Pve.Rows.Values)
            {
                var p=row.Pilot;
                if(!p.Body.isKinematic)p.Body.linearVelocity=Vector3.zero;
                // Bots choose immediately; a disconnected human cannot hold the whole party.
                if(p.Seat.bot)while(row.Picks.Count>0)
                {if(row.Open==null)OpenPvePick(p);if(row.Open?.Offers?.Count>0&&ChoosePve(p,row.Open.Id,row.Open.Offers[0]))continue;break;}
            }
            var connected=Pve.Rows.Values.Where(r=>r.Pilot.Seat.connected&&!r.Pilot.Seat.bot).ToArray();
            if(connected.Any(r=>r.Picks.Count>0))return true;
            foreach(var row in Pve.Rows.Values){row.Open=null;row.Picks.Clear();}
            if(connected.Length==0){BeginPveHold("defeat");Pve.Stage="ended";return true;}
            Pve.Stage="boss";
            if(ZoneBoss==null)
            {
                Pve.MaxHp=NativePveRules.BossHp(PartySize);Pve.Boss.Stats["hp"]=Pve.Boss.Hp=Pve.MaxHp;
                Pve.Boss.Stats["damage"]=NativePveRules.BossDamage(PartySize);SetBossHidden(false);
                PlacePveBoss(BossSpawn);Pve.Boss.Yaw=Pve.Content.boss.yaw;
                Pve.Boss.Input=new InputFrame{Yaw=Pve.Boss.Yaw};Pve.Floor=Pve.MaxHp*.75f;
            }
            else
            {
                var at=zoneBossActor.Body.position;
                NativeZoneBossRules.Start(ZoneBoss,"arrive",Time,-1,at.x,at.z,zoneBossActor.Yaw);
            }
            BeginPveHold("intro");
            if(ZoneBoss?.archetype==NativeFallenBoss.Archetype)ConfigureFallenBattle();
            return true;
        }
        private void StepBossBattle()
        {
            if(ZoneBoss?.fallen!=null){StepFallenBattle();return;}
            StepZoneBoss();
            if(ZoneBoss.deathAt>=0)
            {Pve.Rate=0;if(Time>=ZoneBoss.deathAt+(ZoneBoss.archetype==NativeZoneBossRules.Station?4:7)){Pve.Outcome="victory";Pve.Stage="ended";KillPveHorde();Match.FinishCooperative(true);}return;}
            if(!Members().Any(p=>p.Hp>0&&p.Seat.connected)){BeginPveHold("defeat");return;}
            StepZoneHazards();StepZoneEvents();StepPveServices();StepPveHorde();StepPveAcid();
        }
    }
}

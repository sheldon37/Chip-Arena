using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateFollowupParity()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Follow-up parity: "+why);}
            Reset();var a=Pilots[0];var b=Pilots[1];a.Body.position=new Vector3(0,2,0);b.Body.position=new Vector3(0,2,3);Physics.SyncTransforms();
            var original=new NativeBullet{Zigzag=true,MotionScale=.45f};var copy=CopyBullet(original);Check(copy.Zigzag&&ReferenceEquals(copy.Motion,original.Motion)&&copy.MotionScale==.45f,"split/extras preserve source shared acceleration and zigzag state");
            a.Input=new InputFrame{Shoot=true};for(int i=0;i<100;i++)Bullets.Add(new NativeBullet());int ammo=a.Ammo;StepPilot(a);Check(a.Ammo==ammo&&a.Ready>Time,"projectile cap preserves ammo and sets firing interval");Bullets.Clear();
            b.Seat.chips["revenge-mail"]=1;Damage(b,a,1,Vector3.zero,0,false,primary:false);Check(!ChipFields.Any(f=>f.Effect=="revenge-mail"),"ability-like damage cannot trigger Return Address");Damage(b,a,1,Vector3.zero,0,false,weaponHit:true);Check(ChipFields.Any(f=>f.Effect=="revenge-mail"),"direct weapon damage triggers Return Address");
            a.Seat.chips["sticky-bomb"]=1;Summon(b,1,"toy",b.Body.position,10);var toy=Minions.Last();ImpactChips(a,null,null,toy.Body.position,Vector3.forward,99,99,toy);Check(ChipFields.Any(f=>f.Minion==toy),"sticky bomb attaches to a summon");
            var frame=NativeFrameCodec.Clone(NetworkFrame());frame.pilots[0].moveVelocity=new Vector3(float.NaN,0,0);Check(!frame.Valid(Content),"prediction velocity rejects NaN");frame=NativeFrameCodec.Clone(NetworkFrame());frame.lobbyReturnSeconds=float.NaN;Check(!frame.Valid(Content),"return timer rejects NaN");
            SetPilotConnected(b.Seat.id,false);Check(!b.Collider.enabled&&b.Body.isKinematic,"disconnected pilots cannot invisibly block bullets");SetPilotConnected(b.Seat.id,true);
            a.Stats["critical"]=1;Damage(b,a,1,Vector3.zero,0,false);Check(Events.Any(e=>e.Kind==EventKind.Critical),"critical outcome reaches event stream");ChipBlast(a,a.Body.position,1,5,0);Check(Events.Any(e=>e.Kind==EventKind.Explosion&&e.Radius==1),"blast event carries gameplay radius");
            // Ability ceiling impacts are absorbed silently: unlike bullets and thrown melee,
            // the original ability-system.ts does not create a ceiling-hit hologram.
            int ceilingHitsBefore=Events.Count(e=>e.Kind==EventKind.CeilingHit);
            var ability=Content.abilities.First(d=>d.id=="rocket");AbilityShots.Clear();AbilityShots.Add(new NativeAbilityShot{Owner=a,Definition=ability,Position=new Vector3(0,23.9f,0),Velocity=Vector3.up*60,Radius=.1f,Life=2,MaxTravel=100});StepAbilityWorld();
            Check(AbilityShots.Count==0&&Events.Count(e=>e.Kind==EventKind.CeilingHit)==ceilingHitsBefore,"ability ceiling absorbs without hologram");
            b.Seat.AuthorityBoss=true;ConfigurePve(JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text),false);StartPve();
            // Death.position is the immutable kill pose. The dead body carries the current
            // authority-selected ragdoll anchor (or the kill location before an anchor update).
            var deathAt=new Vector3(5,2,5);var corpseAt=new Vector3(7,2,5);var padAt=new Vector3(-5,2,5);
            foreach(var currentAt in new[]{deathAt,corpseAt})
            {
                a.Hp=0;a.Death=new NativeDeath{position=deathAt};a.Body.position=currentAt;
                a.Mana=3;a.Stamina=4;a.Ammo=2;RevivePve(a);
                Check(a.Body.position==currentAt,"paid/self revive preserves current body anchor");
                Check(a.Death.position==deathAt,"revive preserves immutable death pose");
                Check(a.Mana==3&&a.Stamina==4&&a.Ammo==2,"paid/self revive without resource refill");
            }
            a.Hp=0;a.Body.position=corpseAt;RevivePve(a,padAt);
            Check(a.Body.position==padAt&&a.Death.position==deathAt,"explicit pad respawn overrides corpse anchor without changing death pose");
            Check(a.Mana==3&&a.Stamina==4&&a.Ammo==2,"pad revive without resource refill");
            float hp=b.Hp;bool aWasKinematic=a.Body.isKinematic,bWasKinematic=b.Body.isKinematic;var rescuedAt=new Vector3(0,.9f,24.2f);var placementErrors=new System.Collections.Generic.List<string>();
            void CapturePlacement(string message,string trace,LogType type){if(type==LogType.Warning||type==LogType.Error||type==LogType.Exception||type==LogType.Assert)placementErrors.Add(message);}
            Application.logMessageReceived+=CapturePlacement;
            try
            {
                foreach(bool kinematic in new[]{true,false})
                {
                    a.Body.isKinematic=b.Body.isKinematic=kinematic;a.Body.position=rescuedAt;
                    a.Knockback=b.Knockback=a.MoveVelocity=b.MoveVelocity=Vector3.one;
                    if(!kinematic)a.Body.linearVelocity=b.Body.linearVelocity=Vector3.one;
                    PlacePveBoss(rescuedAt);
                    Check(b.Hp==hp&&b.Body.position==rescuedAt,"boss fall rescue cannot skip an HP phase");
                    Check(Vector3.Distance(a.Body.position,rescuedAt)>=3.99f&&a.Knockback==Vector3.zero&&b.Knockback==Vector3.zero&&a.MoveVelocity==Vector3.zero&&b.MoveVelocity==Vector3.zero,"rescue clears motion and pushes party off spawn");
                    Check(a.Body.isKinematic==kinematic&&b.Body.isKinematic==kinematic,"rescue preserves the body's physics mode");
                    if(!kinematic)Check(a.Body.linearVelocity==Vector3.zero&&b.Body.linearVelocity==Vector3.zero,"dynamic rescue clears velocity");
                }
            }
            finally{Application.logMessageReceived-=CapturePlacement;a.Body.isKinematic=aWasKinematic;b.Body.isKinematic=bWasKinematic;}
            Check(placementErrors.Count==0,"boss/party placement logged Unity warnings or errors: "+string.Join("; ",placementErrors));
            var add=SpawnPveAdd("shambler",true);add.Pve.ExposedAt=Time;KillPveHorde();Check(!Minions.Any(m=>m.Pve!=null)&&MinionDeaths.Any(d=>d.id==add.Id)&&Events.Any(e=>e.Kind==EventKind.Death&&e.Target==add.Id),"wave/outro cleanup records exposed add deaths");
            var row=Pve.Rows[a.Seat.id];row.Notice="Only A";row.NoticeUntil=Time+1;Check(PveNotice(a.Seat.id)=="Only A"&&PveNotice(b.Seat.id)==null,"machine denial isolated to affected pilot");row.NoticeUntil=Time;Check(PveNotice(a.Seat.id)==null,"machine denial expires");
            var entry=Pve.Content.entries.First();Entries.Add(new NativeEntryView{entry=entry.e,kind=entry.kind,closeAt=Time-.1f});var planned=PlanEntry(entry);var follower=PlanEntry(entry);Check(!follower.first&&follower.spawn>planned.spawn,"entry reopening keeps follower spacing");
            var nav=new NativePveNavigation(this);float firstDistance=nav.Distance(new Vector3(0,1,0),new Vector3(5,1,5),1);for(int i=0;i<20;i++)Check(nav.Distance(new Vector3(0,1,0),new Vector3(5,1,5),1)==firstDistance,"navigation cache stable within a tick");
            var victoryAdd=SpawnPveAdd("shambler",true);victoryAdd.Pve.ExposedAt=Time;
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=Time,cooperative=true});
            BeginPveHold("victory");float until=Pve.StageUntil;
            Check(BossVictoryWalk&&!Pve.Holding&&!Minions.Any(m=>m.Pve!=null)&&MinionDeaths.Any(d=>d.id==victoryAdd.Id),"victory immediately clears adds without freezing movement");
            Check(Math.Abs(until-Time-5)<.001f&&Match.Phase==NativePhase.Fight,"five-second live victory window");
            var victoryFrame=NativeFrameCodec.Clone(NetworkFrame());Check(victoryFrame.pve.stage=="victory"&&!victoryFrame.pve.holding&&victoryFrame.pve.stageUntil==until,"guest victory window is replicated");
            for(int i=0;i<299;i++)BossVictoryTick(new InputFrame{X=1,Yaw=90});
            Check(Match.Phase==NativePhase.Fight,"results do not show before five seconds");
            for(int i=0;i<3;i++)BossVictoryTick(default);
            Check(Match.Phase==NativePhase.Match&&Pve.Stage=="ended"&&Pve.Outcome=="victory","results follow victory walk");
            Sandbox=true;Check(!SandboxConfigure(weapon:"sword")&&!SandboxLaunchDebris(40,20,"metal"),"sandbox commands cannot bypass signed developer authorization");
        }
    }
}

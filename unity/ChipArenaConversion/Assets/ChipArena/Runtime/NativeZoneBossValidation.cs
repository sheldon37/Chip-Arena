#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateZoneBossArrivalCamera()
        {
            if(ZoneBoss==null)return;
            var go=new GameObject("Zone arrival camera fixture");var camera=go.AddComponent<Camera>();
            var presenter=go.AddComponent<ArenaPresentation>();var director=new NativeCinematicCamera();
            bool held=Pve.Holding;string cinematic=Pve.Cinematic;float songAt=BossSongAt,lead=BossSongLead;
            float duration=Pve.HoldDuration,left=Pve.HoldLeft;
            try
            {
                Pve.Holding=true;Pve.Cinematic="intro";Pve.HoldDuration=Pve.HoldLeft=9.0678f;BossSongAt=Time;BossSongLead=0;
                camera.fieldOfView=91;presenter.FirstPerson=true;presenter.AimHeld=true;
                director.BeginFrame();
                if(!director.Direct(this,presenter,camera,true)||!director.ZoneArrival||presenter.FirstPerson||presenter.AimHeld)
                    throw new Exception("Zone arrival must direct camera and hide first-person weapon");
                Pve.HoldLeft-=4;director.BeginFrame();
                if(Math.Abs(camera.fieldOfView-91)>.001f||!director.Direct(this,presenter,camera,true)||camera.transform.forward.sqrMagnitude<.99f)
                    throw new Exception("Zone arrival camera restores FOV and remains valid mid-hold");
                Pve.Holding=false;director.BeginFrame();
                if(director.ZoneArrival||director.Directing||Math.Abs(camera.fieldOfView-91)>.001f)
                    throw new Exception("Zone arrival camera must release without leaking FOV");
            }
            finally{director.BeginFrame();Pve.Holding=held;Pve.Cinematic=cinematic;Pve.HoldDuration=duration;Pve.HoldLeft=left;BossSongAt=songAt;BossSongLead=lead;UnityEngine.Object.DestroyImmediate(go);}
        }
        // Runs in real Unity/PhysX on the Windows builder. The runtime feature gate does not bypass this fixture.
        public void ValidateZoneBossAuthority(Action<NativeGameFrame> replicaCheck=null)
        {
            if(!NativeZoneCombat.Active(MapId))return;
            void Check(bool ok,string why){if(!ok)throw new Exception("Native zone boss "+MapId+": "+why);}
            foreach(var old in Minions.ToArray())RemoveMinion(old,false);ResetZoneBoss();
            var p=Pilots.First(x=>!x.Seat.AuthorityBoss);Pve.Holding=false;Pve.Stage="boss";Pve.Rate=0;Pve.MaxHp=6000;Pve.Boss.Hp=6000;
            p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=1;p.Seat.connected=true;p.DevGod=false;p.Roll=null;p.Down=null;p.Death=null;p.BlockEnd=p.ShieldUntil=p.ScareUntil=0;p.Collider.enabled=true;p.Body.isKinematic=false;
            Match.SynchronizeExpeditionClock(Math.Max(Time+1,200));
            SpawnZoneBoss(NativeZoneBossRules.Archetype(MapId),new Vector3(1000,100,1000),0);var m=zoneBossActor;var z=ZoneBoss;
            Check(m!=null&&m.Pve==null&&!Pve.Boss.Seat.connected&&!Pve.Boss.Collider.enabled,"separate minion actor, reserved pilot has no collision or AI");
            Check(m.Body.GetComponentsInChildren<Collider>().Length>=6,"compound body and parts");
            Check(z.archetype==NativeZoneBossRules.Station?StationZoneBossView().live&&!IronworksZoneBossView().live:IronworksZoneBossView().live&&!StationZoneBossView().live,"only matching actor presentation binds");
            var arrival=NetworkFrame();Check(arrival.Valid(Content),"authority arrival frame valid before codec");
            var arrivalWire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=arrival})).frame;
            Check(!NativeIntroRules.Staged(false,true,"intro",arrivalWire.pve.zoneBoss.archetype),"replicated zone boss selects live arena entrance instead of Siren stage");
            Check(arrivalWire.minions.Single(x=>x.id==m.Id).addKind==null,"boss stays a non-horde actor through Unity codec");
            if(!arrivalWire.Valid(Content))Debug.Log("Zone boss codec diagnostic: record="+JsonUtility.ToJson(arrivalWire.pve.zoneBoss)+", actor="+JsonUtility.ToJson(arrivalWire.minions.Single(x=>x.id==m.Id)));
            Check(arrivalWire.Valid(Content),"Unity codec accepts arrival with absent stagger");
            var idle=NetworkFrame();idle.pve.zoneBoss.action=null;idle.pve.zoneBoss.actionAt=idle.pve.zoneBoss.hitAt=-100;idle.pve.zoneBoss.duration=0;
            var idleWire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",epoch=7,frame=idle})).frame;
            Check(idleWire.Valid(Content),"Unity codec accepts idle with absent action and stagger");replicaCheck?.Invoke(idleWire);
            ValidateZoneBossArrivalCamera();
            foreach(var surface in StaticBlocks.Where(b=>b!=null&&b.GetComponent<Collider>()!=null).Take(3))
            {
                var shot=new NativeBullet{Spec="pistol",Bounces=1,Damage=10,BounceRetention=.8f,Velocity=Vector3.right*20,Radius=.1f,Mods=new System.Collections.Generic.Dictionary<string,int>()};
                var contact=new NativeContact{Collider=surface.GetComponent<Collider>(),Point=surface.transform.position,Normal=Vector3.left};
                Check(!BulletImpact(shot,p,contact)&&shot.Bounces==0&&shot.Velocity.x<0&&Math.Abs(shot.Damage-8)<.001,"bounce chip reflects static stage geometry once with retention");
                Check(BulletImpact(shot,p,contact),"exhausted bounce is absorbed");
            }
            if(z.archetype==NativeZoneBossRules.Station)
            {
                var savedPosition=m.Body.position;var pilotPosition=p.Body.position;float savedYaw=m.Yaw;int savedPhase=z.phase;
                try
                {
                    z.phase=1;
                    Check(StationPoseClear(new Vector3(0,0,5),90)&&!StationTurnClear(new Vector3(0,0,5),90,0),"booth blocks a turning hull before penetration");
                    m.Body.position=new Vector3(-12,0,5);m.Yaw=0;m.Body.rotation=Quaternion.identity;
                    p.Body.position=new Vector3(16,.9f,5);stationRoute.Clear();stationRouteAt=0;Physics.SyncTransforms();
                    for(int tick=0;tick<900;tick++)
                    {
                        Match.SynchronizeExpeditionClock(Time+1f/60);StationBossWalk(p);
                        Check(StationPoseClear(m.Body.position,m.Yaw),"wall route never embeds hull at tick "+tick);
                    }
                    Check(m.Body.position.x>0,"boss crosses concourse instead of retrying obstructed waypoint");
                }
                finally{m.Body.position=savedPosition;m.Yaw=savedYaw;m.Body.rotation=Quaternion.Euler(0,savedYaw,0);p.Body.position=pilotPosition;z.phase=savedPhase;stationRoute.Clear();stationRouteAt=0;Physics.SyncTransforms();}
            }
            if(z.archetype==NativeZoneBossRules.Ironworks)
            {
                // Actual opening and catwalk clearance, with full boss sweep and the boss gate open.
                var gates=root.GetComponentsInChildren<Collider>().Where(c=>c.name.Contains("gate:converter")&&c.enabled).ToArray();
                float savedYaw=m.Yaw;
                try
                {
                    foreach(var c in gates)c.enabled=false;m.Yaw=90;Physics.SyncTransforms();
                    for(float x=-35;x< -13;x+=.25f)Check(!BossPathBlocked(new Vector3(x,0,0),new Vector3(x+.25f,0,0),false),"Converter passage clears body, foreman and tongs at x="+x);
                }
                finally{m.Yaw=savedYaw;foreach(var c in gates)c.enabled=true;Physics.SyncTransforms();}
            }
            Pve.Holding=true;Check(HitMinion(m,p,100,Vector3.forward,0)==0,"intro cannot damage boss");Pve.Holding=false;
            p.Body.position=m.Body.position+new Vector3(0,.9f,2);p.Body.transform.position=p.Body.position;Physics.SyncTransforms();
            string attack=z.archetype==NativeZoneBossRules.Station?"buffer":"stomp";BossStart(attack,p);float hit=z.hitAt;
            Match.SynchronizeExpeditionClock(hit-.01f);StepZoneBoss();Check(p.Hp==1000,"windup harmless");
            Match.SynchronizeExpeditionClock(hit+.01f);StepZoneBoss();Check(p.Hp<1000,"strike damages at authority contact time");float after=p.Hp;
            Match.SynchronizeExpeditionClock(hit+.02f);StepZoneBoss();Check(p.Hp==after,"one hit per swing");
            float hp=m.Hp;HitMinion(m,p,100,Vector3.forward,0,m.Body.position+new Vector3(0,2.8f,-5),"projectile");Check(m.Hp<hp&&Pve.Boss.Hp==m.Hp,"real weapon path updates boss health");
            m.Hp=m.MaxHp*.6f;StepZoneBoss();Check(z.phase==2&&!Pve.Holding&&z.phaseAt==Time,"phase changes without Siren reveal hold");
            var wire=NativeFrameCodec.Clone(NetworkFrame());Check(wire.Valid(Content)&&wire.pve.zoneBoss?.actor==m.Id,"finite actor and unified record round trip");replicaCheck?.Invoke(wire);
            wire.pve.zoneBoss.hitAt=float.NaN;Check(!wire.Valid(Content),"nonfinite action time rejected");
            z.holdTarget=p.Seat.id;z.holdUntil=Time+1;BossStagger("part",.8f);Check(z.holdTarget<0&&z.holdUntil==Time&&z.cancelAt==Time,"stagger cancels move and releases victim");
            m.Hp=0;StepZoneBoss();Check(z.deathAt==Time&&m.Body.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"death releases collision and has authority time");
            RemoveMinion(m,false);ResetZoneBoss();
            Debug.Log("PASS native zone boss "+MapId+": separate actor, intro guard, windup/contact/single-hit, damage route, phase transition, wire/guest and death. Automated fixture.");
        }
    }
}
#endif

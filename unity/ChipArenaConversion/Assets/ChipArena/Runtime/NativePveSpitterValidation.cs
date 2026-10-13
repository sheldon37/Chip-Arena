#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidatePveSpitters()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native spitter gate: "+why);}
            void Clock(double t)=>Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=t,fightAt=10,round=Match.Round,set=Match.Set,cooperative=true});
            var bounds=Map.bounds;var entries=Pve.Content.entries;var p=Members().First();
            try
            {
                foreach(var m in Minions.ToArray())RemoveMinion(m,false);pveEntries.Clear();pveAcid.Clear();Clock(400);
                Map.bounds=new MapBounds{minX=-20,maxX=20,minZ=980,maxZ=1020,ceiling=20,killY=-6};
                AddProp(new ContentBlock{id="acid-check-floor",material="foundation",role="static",pos=new V3(0,-.5f,1000),size=new V3(30,1,30),rotation=new ContentRotation()});
                Pve.Holding=false;Pve.Stage="wave";Pve.Wave=2;Pve.Cap=8;Pve.Rate=0;Pve.SpawnBudget=0;
                Pve.Content.entries=new[]{new PveEntry{e=0,id="acid-check-entry",kind="door",from=new PvePoint{z=1000},@exit=new PvePoint{z=1000}}};
                p.Seat.chips.Clear();p.Seat.spec="pistol";p.Weapon=Content.Weapon("pistol");p.Stats=NativeContentRules.Stats(Content,"pistol",p.Seat.chips);p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=1;
                p.Seat.alive=p.Seat.connected=true;p.Down=null;p.Juggle=null;p.Kernel=null;p.Roll=null;p.ShieldUntil=p.RollEnd=p.BlockEnd=0;p.Dots.Clear();p.Body.position=new Vector3(0,p.Collider.height/2+.025f,1006);p.Body.linearVelocity=Vector3.zero;
                foreach(var q in Pilots.Where(q=>q!=p))q.Body.position=new Vector3(15,1,1010);
                var spit=SpawnPveAdd("spitter",true,0);Check(spit!=null,"spitter row spawns");spit.Body.position=new Vector3(0,.875f,1000);spit.Pve.SpawnAt=spit.Pve.WalkAt=spit.Pve.ActiveAt=spit.Pve.ExposedAt=Time-1;spit.Pve.Target=p;spit.Pve.DamageScale=1;spit.Pve.ThinkAt=Time+20;spit.Yaw=0;spit.Collider.enabled=true;Physics.SyncTransforms();
                LaunchPveSpit(spit);Check(pveAcid.Count==1&&Events.Any(e=>e.Detail=="minion-spit"),"clear mouth arc launches once");
                var record=pveAcid[0].Record;float hp=p.Hp;StepPveAcid();Check(p.Hp==hp,"no damage during flight");
                Clock(record.at+record.flight+.002);StepPveAcid();Check(Math.Abs(p.Hp-(hp-7))<.002f,"7 direct landing damage");StepPveAcid();Check(Math.Abs(p.Hp-(hp-7))<.002f,"direct hit once");
                RefreshPvePoison(p,4,.5);RefreshPvePoison(p,3,.5);Check(p.Dots.Count==1&&p.Dots["pve:poison"].Dps==4,"overlapping puddles use one strongest poison");
                Clock(Time+.251);AdvanceDots(p);Check(Math.Abs(p.Hp-(hp-8))<.002f&&Events.Any(e=>e.Kind==EventKind.PoisonTick),"existing poison damage/event path");
                var material=Content.materials.First(m=>!m.indestructible).id;
                var prop=AddProp(new ContentBlock{id="acid-check-prop",material=material,pos=new V3(.5f,.3f,1006),size=new V3(.2f,.6f,.2f),hp=100,rotation=new ContentRotation()});prop.LocalFixture=true;Physics.SyncTransforms();
                CorrodePve(record,.6f,10);Check(Math.Abs(prop.Health-90)<.002f,"direct corrosion uses destructible damage");CorrodePve(record,1.3f,6);Check(Math.Abs(prop.Health-84)<.002f,"puddle corrosion");prop.Indestructible=true;CorrodePve(record,1.3f,6);Check(Math.Abs(prop.Health-84)<.002f,"protected geometry is not corroded");
                spit.LastAttack=Time-.2f;spit.StrikeAt=spit.LastAttack+.8f;spit.Pve.Struck=false;spit.Hp=spit.MaxHp=1000;spit.Pve.Parts.Clear();
                HitPveAdd(spit,p,1,Vector3.forward,0,null,"projectile",0,false);Check(spit.Pve.Struck&&spit.Pve.SpitCancelAt==Time&&Math.Abs(spit.Pve.RecoverAt-Time-1.56f)<.002f,"small damaging hit cancels with partial cooldown");
                // Skip the entrance with a coherent completed timeline, then validate before serialization.
                var source=NetworkFrame();Check(source.Valid(Content),"source frame is valid before JsonUtility");
                var frame=NativeFrameCodec.Clone(source);Check(frame.Valid(Content),"JsonUtility clone remains valid");
                Check(frame.pve.acid.Length==1,"real JsonUtility carries acid");
                Check(frame.minions.Single(m=>m.id==spit.Id).spitCancelAt==Time,"real JsonUtility carries cancellation");
                var guest=new NativeArena(Content,new NativeMatch(Content),MapId){Replica=true,ReplicaScene=root.scene};
                try{guest.ApplyFrame(frame,LocalId);Check(guest.PveAcidCount==1&&guest.PveAcidAt(0).to.z==record.to.z,"guest / late join restores live puddle");frame.pve.acid=Array.Empty<NativeAcidRecord>();guest.ApplyFrame(frame,LocalId);Check(guest.PveAcidCount==0,"absent records clear on rewind/transition");}finally{guest.Dispose();}
                Debug.Log("PASS native spitter arc/landing/DoT/nonstacking/corrosion/interrupt/JsonUtility/guest state");
            }
            finally{Pve.Content.entries=entries;Map.bounds=bounds;Reset();}
        }
    }
}
#endif

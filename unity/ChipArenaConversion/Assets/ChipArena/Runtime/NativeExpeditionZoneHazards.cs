using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public NativeZoneHazardFrame ZoneHazards {get;private set;}
        private readonly NativeStationView zoneStation=new NativeStationView();
        private readonly Dictionary<long,float> zoneContact=new Dictionary<long,float>();
        private float zoneGlassAt,zoneLeverReady,zoneLastAt=-100;
        private void ResetZoneCombat(){ZoneHazards=null;zoneContact.Clear();zoneGlassAt=zoneLeverReady=0;zoneLastAt=-100;zonePops.Clear();}
        public void BindZonePresentation()
        {
            NativeIronworksLive.Bound=NativeZoneCombat.Ironworks(MapId)&&ZoneHazards!=null;
            NativeIronworksLive.Authority=ZoneHazards?.Ironworks??default;
            NativeStationLive.Authority=NativeZoneCombat.Station(MapId)&&ZoneHazards!=null;
            if(NativeStationLive.Authority)ZoneHazards.Fill(NativeStationLive.View,Time);
        }
        private bool ZoneLever(NativePilot p)
        {
            if(!NativeZoneCombat.Station(MapId)||ZoneHazards==null||!ZoneHazards.running||Time<ZoneHazards.leverUntil||Time<zoneLeverReady||Time>=ZoneHazards.trainAt&&Time<ZoneHazards.trainAt+NativeExpeditionStation.PassSeconds)return false;
            if((p.Body.position-new Vector3(36,6.9f,-54.4f)).magnitude>2.2f||!MeleeVisible(p.Body.position,new Vector3(36,6.9f,-54.4f),p,null,true))return false;
            ZoneHazards.leverAt=Time;ZoneHazards.leverUntil=Time+60;
            ZoneHazards.trainAt=Math.Max(ZoneHazards.trainAt,Time+64);zoneLeverReady=ZoneHazards.trainAt+2.2f;return true;
        }
        private static bool ZoneBox(Vector3 at,float radius,float half,float x,float y,float z,float sx,float sy,float sz)
            =>Math.Abs(at.x-x)<=sx*.5f+radius&&Math.Abs(at.y-y)<=sy*.5f+half&&Math.Abs(at.z-z)<=sz*.5f+radius;
        private bool ZoneTouch(int actor,int hazard,float interval)
        {
            long key=((long)actor<<32)|(uint)hazard;
            if(zoneContact.TryGetValue(key,out var until)&&Time<until)return false;
            zoneContact[key]=Time+interval;return true;
        }
        private void ZoneHazardPilot(NativePilot p,int id,float amount,float interval,string kind,Vector3 push=default,float stun=0,bool lethal=false)
        {
            if(!ZoneTouch(p.Seat.id,id,interval)||!lethal&&Invulnerable(p))return;
            float actual=Damage(p,null,lethal?p.Hp:amount,push.normalized,0,false,forced:lethal,direct:false,primary:false,stage:true,rollAvoidable:!lethal,damageKind:kind,stageHazard:"zone-"+id,stageLethal:lethal);
            if(actual>0){p.Knockback+=push;p.StunEnd=Math.Max(p.StunEnd,Time+stun);}
        }
        private void ZoneHazardMinion(NativeMinion m,int id,float amount,float interval,bool fire=false)
        {
            if(m.Pve==null||Time<m.Pve.ActiveAt||m.Hp<=0||fire&&m.Pve.Stats.id=="cinderling"||!ZoneTouch(m.Id,id,interval))return;
            m.Hp-=Math.Min(m.Hp,amount);m.LastHitAt=Time;
            if(m.Hp<=0){var source=Time-m.Pve.LastDamage<=5?m.Pve.LastHit:null;ZoneEnemyDeath(m,source,false);PveAddKilled(m,source);RemoveMinion(m,false);}
        }
        private void StepZoneHazards()
        {
            if(!NativeZoneCombat.Active(MapId)||Pve==null)return;
            if(ZoneHazards==null)ZoneHazards=new NativeZoneHazardFrame{start=Time,trainAt=Time+60};
            var h=ZoneHazards;h.running=Pve.Stage=="wave"||Pve.Stage=="boss";int wave=Math.Max(1,Math.Min(4,Pve.Wave));if(h.wave!=wave){h.wave=wave;h.clock=0;}
            if(!h.running||Pve.Holding)return;if(Time-zoneLastAt>.1f)h.trainAt=Math.Max(h.trainAt,Time+4);zoneLastAt=Time;h.clock+=1f/60;
            if(!h.boss&&Pve.Stage=="boss"){h.boss=true;h.bossAt=Time;h.clock=0;h.trainAt=Math.Max(h.trainAt,Time+4);}
            // A bounded ledger prevents contact rate from depending on physics or frame rate.
            if(zoneContact.Count>4096)foreach(var key in zoneContact.Where(p=>p.Value<Time-2).Select(p=>p.Key).ToArray())zoneContact.Remove(key);
            if(NativeZoneCombat.Ironworks(MapId)){StepIronworksHazards(h);return;}
            if(Time>h.trainAt+5)h.trainAt=Time+70+(float)Pve.SpawnRandom.Next()*40;
            if(h.boss&&Time>=zoneGlassAt)
            {
                zoneGlassAt=Time+4;var targets=Pilots.Where(p=>ZonePilot(p)&&p.Body.position.y<3&&Math.Abs(p.Body.position.x)<30&&p.Body.position.z>=-1&&p.Body.position.z<=17).Take(4).ToArray();
                h.glassX=targets.Select(p=>p.Body.position.x).ToArray();h.glassZ=targets.Select(p=>p.Body.position.z).ToArray();h.glassAt=targets.Select(p=>Time+1.2f).ToArray();
            }
            h.floodGrounded=Minions.Any(m=>m.Pve?.Stats.id=="linesman"&&m.Hp>0&&StationFlood(m.Body.position));h.Fill(zoneStation,Time);
            bool swept=NativeZoneHazardFrame.TrainSweep(h.trainAt,Time-1d/60,Time,out var trainLeft,out var trainRight);
            bool train=Time>=h.leverUntil&&swept;
            foreach(var p in Pilots){if(!ZonePilot(p))continue;var at=p.Body.position;
                if(train&&at.y<3.9f&&at.y> -2&&at.x+p.Collider.radius>=trainLeft&&at.x-p.Collider.radius<=trainRight)
                {float edge=Math.Abs(at.z+12.5f);if(edge<2.5f)ZoneHazardPilot(p,100,p.Hp,1,"hazard",lethal:true);else if(edge<3.1f)ZoneHazardPilot(p,101,1,1,"hazard",new Vector3(0,0,Math.Sign(at.z+12.5f)*6));}
                if(Time>=h.leverUntil&&StationRail(at,p.Collider.height*.5f))ZoneHazardPilot(p,102,25,1,"grid",stun:.6f);
                if(StationFlood(at)){p.SlowEnd=Math.Max(p.SlowEnd,Time+.1f);p.SlowFactor=Math.Min(p.SlowFactor,.8f);if(zoneStation.FloodLive)ZoneHazardPilot(p,103,2,.25f,"grid");}
                for(int i=0;i<2;i++)if(zoneStation.Cabinets[i].Phase==NativeHazardPhase.Active&&Math.Abs(at.y-1)<2&&Vector2.Distance(new Vector2(at.x,at.z),i==0?new Vector2(-13.4f,-15.8f):new Vector2(30.6f,-32.8f))<2)ZoneHazardPilot(p,104+i,12,1,"grid");
                if(zoneStation.Steam.Phase==NativeHazardPhase.Active&&ZoneBox(at,p.Collider.radius,p.Collider.height/2,-6,1.5f,-57,10,3,4))ZoneHazardPilot(p,106,2,.25f,"burner",Vector3.forward*.25f);
                for(int i=0;i<h.glassAt.Length;i++)if(Time>=h.glassAt[i]&&Time<h.glassAt[i]+.1f&&at.y<2&&Vector2.Distance(new Vector2(at.x,at.z),new Vector2(h.glassX[i],h.glassZ[i]))<1.4f)ZoneHazardPilot(p,110+i,18,2,"hazard");
            }
            foreach(var m in Minions.ToArray())if(m.Pve!=null&&m.Hp>0)
            {var at=m.Body.position;if(train&&at.y<3.9f&&at.y> -2&&at.x+m.Collider.radius>=trainLeft&&at.x-m.Collider.radius<=trainRight&&Math.Abs(at.z+12.5f)<2.5f)ZoneHazardMinion(m,100,m.Hp,1);else if(Time>=h.leverUntil&&StationRail(at,m.Collider.height/2)&&m.Pve.Stats.id!="linesman")ZoneHazardMinion(m,102,25,1);}
        }
        private void StepIronworksHazards(NativeZoneHazardFrame h)
        {
            var input=h.Ironworks;
            for(int i=0;i<NativeIronworksHazards.Count;i++)
            {
                var state=NativeIronworksHazards.At(i,input);if(!state.Live)continue;var d=NativeIronworksHazards.Defs[i];
                bool crush=d.Kind==NativeIronworksHazards.KindPress||d.Kind==NativeIronworksHazards.KindPusher,cut=d.Kind==NativeIronworksHazards.KindShear;
                float damage=crush?35:cut?22:2,interval=crush||cut?1:.25f;
                // Slab and pusher damage follow the moving object, not the whole swept path.
                float x=d.X,z=d.Z,sx=d.Sx,sz=d.Sz;
                if(i==NativeIronworksHazards.Slab){x=NativeIronworksKit.FrameOrigin[NativeIronworksKit.FrameSlab*3]-(float)NativeIronworksKit.SlabRunOf(state);sx=6;}
                if(i==NativeIronworksHazards.Slag)sx*=Mathf.Clamp01((float)state.Since/3);
                foreach(var p in Pilots)if(ZonePilot(p)&&ZoneBox(p.Body.position,p.Collider.radius,p.Collider.height/2,x,d.Y,z,sx,d.Sy,sz))ZoneHazardPilot(p,i,damage,interval,crush?"hazard":cut?"hazard":"burner",i==NativeIronworksHazards.Steam?Vector3.up*.5f:i==NativeIronworksHazards.Slab?Vector3.left*.3f:Vector3.zero);
                foreach(var m in Minions.ToArray())if(m.Pve!=null&&ZoneBox(m.Body.position,m.Collider.radius,m.Collider.height/2,x,d.Y,z,sx,d.Sy,sz))ZoneHazardMinion(m,i,damage,interval,!crush&&!cut);
            }
        }
        private bool ZonePilot(NativePilot p)=>p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss&&!p.ExpeditionTransferHeld;
        private static bool StationFlood(Vector3 p)=>p.y<1&&p.y> -2&&(p.x>=-44&&p.x<=-20&&Math.Abs(p.z+44.5f)<2.5f||Math.Abs(p.x+41)<3&&Math.Abs(p.z+36)<11);
        private static bool StationRail(Vector3 p,float half)=>p.y-half<-.55f&&p.y> -2&&(Math.Abs(p.z+12.5f)<.8f&&Math.Abs(p.x)>2&&Math.Abs(p.x)<44||Math.Abs(p.z+29.5f)<.8f&&p.x>=-44&&p.x<=-30||Math.Abs(p.z+44.5f)<.8f&&(p.x>=-20&&p.x<=-2||p.x>=2&&p.x<=44));
    }
}

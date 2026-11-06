using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
 public static partial class NativeZoneBossViews
 {
 public static float BackroomsArrival(NativeZoneBossFrame f)=>f.backrooms.started-NativeZoneBossRules.IntroSeconds;
 public static HumBossView Hum(NativeZoneBossFrame f,float x,float y,float z,float yaw,float hp,float maxHp,float spawned,int localSeat=-1)
 {
 if(f?.archetype!=NativeHumBoss.Archetype||f.backrooms==null)return HumBossView.None;
 var b=f.backrooms;var m=b.meters.FirstOrDefault(a=>a.seat==localSeat);float T(float t)=>HumBossView.Time(t);
 return new HumBossView{live=true,x=x,y=y,z=z,yaw=yaw,hp=hp,maxHp=maxHp,spawned=spawned,phase=f.phase,phaseAt=T(f.phaseAt),action=f.action,actionId=f.actionId,actionAt=T(f.actionAt),hitAt=T(f.hitAt),duration=f.duration,cancelAt=T(f.cancelAt),actionTarget=f.targetSeat,parts=f.parts,partAt=f.partAt.Select(T).ToArray(),staggerAt=T(f.staggerAt),staggerSeconds=f.staggerSeconds,staggerCause=f.staggerCause,diedAt=T(f.deathAt),laneX0=f.laneX0,laneZ0=f.laneZ0,laneX1=f.laneX1,laneZ1=f.laneZ1,clipSeconds=2.2f,staggerPushX=f.staggerPushX,staggerPushZ=f.staggerPushZ,handY=1,tornCount=b.torn.Length/3,torn=b.torn,voiceCount=b.voices.Length/6,voices=b.voices,heard=m?.value??0,heardX=m?.x??0,heardZ=m?.z??0,rowAxis=b.rowAxis,rowMask=b.rowMask,row2Axis=b.row2Axis,row2Mask=b.row2Mask,tearX=b.tearX,tearZ=b.tearZ,ringX=b.ringX,ringZ=b.ringZ,handX=b.handX,handZ=b.handZ,handNx=b.handNx,handNz=b.handNz,shiftMask=b.shiftMask,shiftFrom=b.shiftFrom,circuitMask=b.circuitMask,rowAt=T(b.rowAt),row2At=T(b.row2At),tearAt=T(b.tearAt),ringAt=T(b.ringAt),handAt=T(b.handAt),handUntil=T(b.handUntil),shiftAt=T(b.shiftAt),circuitAt=T(b.circuitAt),clipAt=T(b.clipAt),ceilingAt=T(b.ceilingAt),floorAt=T(b.floorAt)};
 }
 public static DeepEndBossView DeepEnd(NativeZoneBossFrame f,float x,float y,float z,float yaw,float hp,float maxHp,float spawned,int localSeat=-1)
 {
 if(f?.archetype!=NativeDeepEndBoss.Archetype||f.backrooms==null)return DeepEndBossView.None;
 var b=f.backrooms;var m=b.meters.FirstOrDefault(a=>a.seat==localSeat);float T(float t)=>DeepEndBossView.Time(t);
 return new DeepEndBossView{live=true,x=(f.actionAt>=0?f.laneX0:x)-b.cx,y=y,z=(f.actionAt>=0?f.laneZ0:z)-b.cz,yaw=yaw,hp=hp,maxHp=maxHp,spawned=spawned,phase=f.phase,phaseAt=T(f.phaseAt),action=f.action,actionId=f.actionId,actionAt=T(f.actionAt),hitAt=T(f.hitAt),duration=f.duration,cancelAt=T(f.cancelAt),actionTarget=f.targetSeat,parts=f.parts,partAt=f.partAt.Select(T).ToArray(),staggerAt=T(f.staggerAt),staggerSeconds=f.staggerSeconds,staggerCause=f.staggerCause,diedAt=T(f.deathAt),targetX=(f.action=="flop"?f.laneX0:f.targetX)-b.cx,targetZ=(f.action=="flop"?f.laneZ0:f.targetZ)-b.cz,lull=m?.value??0,aimX=b.aimX,aimZ=b.aimZ,surfaceX=b.surfaceX,surfaceZ=b.surfaceZ,pathSeed=b.pathSeed,tideY=b.tideY,tideFrom=b.tideFrom,diveAt=T(b.diveAt),surfaceAt=T(b.surfaceAt),tideAt=T(b.tideAt),tideUntil=T(b.tideUntil),whirlAt=T(b.whirlAt),whirlUntil=T(b.whirlUntil),seicheAt=T(b.seicheAt)};
 }
 }
}

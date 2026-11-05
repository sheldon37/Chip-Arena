using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeBossMeter
    {
        public int seat,lastRing;public float x,z,since,highAt=-100,value;
        public NativeBossMeter Copy()=>(NativeBossMeter)MemberwiseClone();
    }
    [Serializable] public sealed class NativeBackroomsBossFrame
    {
        public float cx,cz,x,z,yaw,started,next,checkedAt,shiftDue,enrageDue;
        public float shiftAt=-100,circuitAt=-100,clipAt=-100,ceilingAt=-100,floorAt=-100;
        public int ringSerial;public int shiftMask,shiftFrom,circuitMask,rowAxis,rowMask,row2Axis,row2Mask,pathSeed,lampsPhase;
        public float rowAt=-100,row2At=-100,tearX,tearZ,tearAt=-100,ringX,ringZ,ringAt=-100;
        public float handX,handZ,handNx,handNz,handAt=-100,handUntil=-100;
        public float tideY=PoolroomsLayout.WaterY,tideFrom=PoolroomsLayout.WaterY,tideAt=-100,tideUntil=-100;
        public float immuneSince=-100,exposedUntil=-100;
        public float diveAt=-100,surfaceAt=-100,surfaceX,surfaceZ,aimX,aimZ,whirlAt=-100,whirlUntil=-100,seicheAt=-100;
        public float[] torn=Array.Empty<float>(),voices=Array.Empty<float>(),ready=new float[10];
        public NativeBossMeter[] meters=Array.Empty<NativeBossMeter>();public int[] hits=Array.Empty<int>();public bool fired;
        public NativeBackroomsBossFrame Copy(){var c=(NativeBackroomsBossFrame)MemberwiseClone();c.torn=(float[])torn.Clone();c.voices=(float[])voices.Clone();c.ready=(float[])ready.Clone();c.meters=meters.Select(m=>m.Copy()).ToArray();c.hits=(int[])hits.Clone();return c;}
        public bool Valid()=>new[]{cx,cz,x,z,yaw,started,next,checkedAt,shiftDue,enrageDue,shiftAt,circuitAt,clipAt,ceilingAt,floorAt,rowAt,row2At,tearX,tearZ,tearAt,ringX,ringZ,ringAt,handX,handZ,handNx,handNz,handAt,handUntil,tideY,tideFrom,tideAt,tideUntil,diveAt,surfaceAt,surfaceX,surfaceZ,aimX,aimZ,whirlAt,whirlUntil,seicheAt,immuneSince,exposedUntil}.All(NativeZoneCombat.Finite)&&shiftMask>=0&&shiftMask<=255&&shiftFrom>=0&&shiftFrom<=255&&circuitMask>=0&&circuitMask<=255&&rowAxis>=0&&rowAxis<=1&&row2Axis>=0&&row2Axis<=1&&rowMask>=0&&rowMask<=63&&row2Mask>=0&&row2Mask<=63&&tideY>=-.15f&&tideY<=7.2f&&tideFrom>=-.15f&&tideFrom<=7.2f&&ready!=null&&ready.Length==10&&ready.All(NativeZoneCombat.Finite)&&torn!=null&&torn.Length<=96&&torn.Length%3==0&&torn.All(NativeZoneCombat.Finite)&&voices!=null&&voices.Length<=48&&voices.Length%6==0&&voices.All(NativeZoneCombat.Finite)&&meters!=null&&meters.Length<=8&&meters.All(m=>m!=null&&m.seat>=0&&m.seat<=10000&&new[]{m.x,m.z,m.since,m.highAt,m.value}.All(NativeZoneCombat.Finite)&&m.value>=0&&m.value<=1)&&meters.Select(m=>m.seat).Distinct().Count()==meters.Length&&hits!=null&&hits.Length<=512;
    }
    public struct BackroomsBossPilot{public int Seat;public Vector3 Feet;public float Yaw,Noise;}
    public struct BackroomsBossContact{public int Seat;public float Damage,Stun,Pull;public bool Jump,Return;public Vector3 Point;public string Add;}
    // Production scheduler and geometry; the native authority and the managed grid call the same Step.
    public static class NativeBackroomsBossRules
    {
        public static bool Is(string id)=>id==NativeHumBoss.Archetype||id==NativeDeepEndBoss.Archetype;
        public static float Tide(NativeZoneBossFrame z,float now){var b=z?.backrooms;if(b==null)return PoolroomsLayout.WaterY;return b.tideUntil<=b.tideAt?b.tideY:b.tideFrom+(b.tideY-b.tideFrom)*Clamp((now-b.tideAt)/(b.tideUntil-b.tideAt));}
        public static NativeZoneBossFrame Create(string id,int actor,float cx,float cz,float now)
        {
            bool hum=id==NativeHumBoss.Archetype;var z=new NativeZoneBossFrame{archetype=id,actor=actor,phaseAt=now,partAt=Enumerable.Repeat(-100f,hum?8:12).ToArray(),backrooms=new NativeBackroomsBossFrame{cx=cx,cz=cz,x=cx,z=cz+(hum?NativeHumArena.Size/2-7:0),yaw=180,started=now,next=now,shiftDue=now+18,enrageDue=now+360}};
            z.laneX0=z.backrooms.x;z.laneZ0=z.backrooms.z;NativeZoneBossRules.Start(z,"arrive",now,-1,cx,cz,180);return z;
        }
        public static HumRoom Room(NativeBackroomsBossFrame b)=>new HumRoom(0,0,new Vector3(b.cx-NativeHumArena.Size/2,0,b.cz-NativeHumArena.Size/2));
        public static bool Inside(NativeZoneBossFrame z,Vector3 p,float margin=0){var b=z.backrooms;float h=z.archetype==NativeHumBoss.Archetype?NativeHumArena.Size/2:20;return Math.Abs(p.X-b.cx)<h-margin&&Math.Abs(p.Z-b.cz)<h-margin&&p.Y>=-1.5f&&p.Y<(z.archetype==NativeHumBoss.Archetype?4:7.2f);}
        private static float Clamp(float x)=>Math.Max(0,Math.Min(1,x));
        private static Vector3 Flat(Vector3 p)=>new Vector3(p.X,0,p.Z);
        private static float Dist(float x,float z,float a,float b)=>new Vector2(x-a,z-b).Length();
        private static Vector3 Dir(float yaw)=>new Vector3((float)Math.Sin(yaw*Math.PI/180),0,(float)Math.Cos(yaw*Math.PI/180));
        private static float Angle(Vector3 v)=>(float)(Math.Atan2(v.X,v.Z)*180/Math.PI);
        public static bool Arc(Vector3 p,Vector3 o,float yaw,float reach,float arc){var d=Flat(p-o);return d.Length()<=reach&&(d.Length()<.001f||Vector3.Dot(Vector3.Normalize(d),Dir(yaw))>=Math.Cos(arc*Math.PI/360));}
        public static bool Line(Vector3 p,Vector3 a,Vector3 b,float width){p=Flat(p);a=Flat(a);b=Flat(b);var d=b-a;float t=d.LengthSquared()<.001f?0:Clamp(Vector3.Dot(p-a,d)/d.LengthSquared());return Vector3.Distance(p,a+d*t)<=width/2;}
        private static void Hit(NativeZoneBossFrame z,List<BackroomsBossContact> output,BackroomsBossPilot p,float damage,Vector3 point,int strike=0,bool jump=false,float stun=0,bool move=false,float pull=0)
        {
            int key=strike*10001+p.Seat;var b=z.backrooms;if(b.hits.Contains(key)||b.hits.Length>=512)return;b.hits=b.hits.Concat(new[]{key}).ToArray();output.Add(new BackroomsBossContact{Seat=p.Seat,Damage=damage,Point=point,Jump=jump,Stun=stun,Return=move,Pull=pull});
        }
        public static void Start(NativeZoneBossFrame z,string id,BackroomsBossPilot p,float now)
        {
            var b=z.backrooms;bool hum=z.archetype==NativeHumBoss.Archetype;int index=hum?NativeHumBoss.MoveIndex(id):NativeDeepEndBoss.Index(id);
            NativeZoneBossRules.Start(z,id,now,p.Seat,p.Feet.X,p.Feet.Z,Angle(p.Feet-new Vector3(b.x,0,b.z)));NativeZoneBossRules.Action(z.archetype,id,out var m);
            b.next=now+z.duration+1;b.hits=Array.Empty<int>();b.fired=false;if(index>=0)b.ready[index]=now+m.Cooldown*(z.phase==3?.8f:1);
            z.laneX0=b.x;z.laneZ0=b.z;var end=new Vector3(b.x,0,b.z)+Dir(z.actionYaw)*(hum?26:24);float half=hum?NativeHumArena.Size/2:20;end.X=Math.Max(b.cx-half+.4f,Math.Min(b.cx+half-.4f,end.X));end.Z=Math.Max(b.cz-half+.4f,Math.Min(b.cz+half-.4f,end.Z));z.laneX1=end.X;z.laneZ1=end.Z;b.yaw=z.actionYaw;
            if(id=="row"){b.rowAxis=z.actionId%2;b.rowMask=z.actionId%3==0?42:21;b.rowAt=z.hitAt;b.row2Axis=1-b.rowAxis;b.row2Mask=b.rowMask;b.row2At=z.phase==3?z.hitAt+1.1f:-100;if(z.phase==3){z.duration+=1.1f;b.next+=1.1f;}}
            if(id=="tear"){b.tearX=p.Feet.X;b.tearZ=p.Feet.Z;b.tearAt=z.hitAt;}
            if(id=="pulse"){b.ringX=b.x;b.ringZ=b.z;b.ringAt=z.hitAt;z.duration=m.Wind+2.6f+m.Follow+m.Recover;b.next=now+z.duration+1;}
            if(id=="hand"){NativeHumArena.NearestWall(Room(b),b.shiftMask,p.Feet.X,p.Feet.Z,out b.handX,out b.handZ,out b.handNx,out b.handNz);InsetWall(b);b.handAt=now;b.handUntil=z.hitAt+1.5f;}
            if(id=="cut"){var behind=Flat(p.Feet)-Dir(p.Yaw)*2;b.x=behind.X;b.z=behind.Z;b.yaw=p.Yaw;z.actionYaw=p.Yaw;}
            if(id=="lunge"&&z.phase==3){z.duration+=2.4f;b.next+=2.4f;}
            if(!hum){b.aimX=end.X-b.cx;b.aimZ=end.Z-b.cz;b.surfaceAt=now;b.surfaceX=b.x-b.cx;b.surfaceZ=b.z-b.cz;
                if(id=="breach"){b.surfaceX=p.Feet.X-b.cx;b.surfaceZ=p.Feet.Z-b.cz;}
                if(id=="jet"){b.aimX=p.Feet.X-b.cx;b.aimZ=p.Feet.Z-b.cz;}
                if(id=="seiche"){b.aimX=Dir(z.actionYaw).X;b.aimZ=Dir(z.actionYaw).Z;b.seicheAt=z.hitAt;}
                if(id=="drain"){b.whirlAt=z.hitAt;b.whirlUntil=z.hitAt+m.Strike;}}
        }
        private static void InsetWall(NativeBackroomsBossFrame b)
        {
            var room=Room(b);for(int i=0;i<8;i++){room.Partition(i,(b.shiftMask>>i)&1,out var c,out bool along);float half=NativeHumArena.PartitionCells*BackroomsLayout.Cell/2-1;
                if(along&&Math.Abs(b.handZ-c.Z)<.2f)b.handX=Math.Max(c.X-half,Math.Min(c.X+half,b.handX));if(!along&&Math.Abs(b.handX-c.X)<.2f)b.handZ=Math.Max(c.Z-half,Math.Min(c.Z+half,b.handZ));}
        }
        public static void Step(NativeZoneBossFrame z,float hp,float maxHp,float now,float dt,BackroomsBossPilot[] pilots,List<BackroomsBossContact> output,DeepEndBossRig rig=null)
        {
            output.Clear();if(z.deathAt>=0||hp<=0||pilots.Length==0)return;var b=z.backrooms;bool hum=z.archetype==NativeHumBoss.Archetype;
            int phase=NativeZoneBossRules.Phase(z.archetype,hp,maxHp);
            if(phase>z.phase){z.phase++;z.phaseAt=now;z.holdTarget=-1;NativeZoneBossRules.Start(z,"phase",now,-1,b.x,b.z,b.yaw);b.next=now+3.5f;b.hits=Array.Empty<int>();b.tideFrom=Tide(z,now);b.tideY=z.phase==2?.5f:1.1f;b.tideAt=now;b.tideUntil=now+3.5f;if(hum){b.circuitMask=255;b.circuitAt=now;}return;}
            if(z.action=="phase"&&now<z.actionAt+3.5f)return;
            if(now>=b.checkedAt){b.checkedAt=now+1;if(hum&&z.phase==3)foreach(var p in pilots)for(int i=0;i<b.torn.Length;i+=3)if(Dist(p.Feet.X,p.Feet.Z,b.torn[i],b.torn[i+1])<=2.4f&&now>b.torn[i+2]+.5f){output.Add(new BackroomsBossContact{Seat=p.Seat,Damage=12,Return=true,Stun=.6f,Point=new Vector3(b.x,2,b.z)});break;}foreach(var p in pilots)if(!Inside(z,p.Feet,.3f))output.Add(new BackroomsBossContact{Seat=p.Seat,Damage=12,Return=true,Stun=.6f,Point=new Vector3(b.cx,hum?2:0,b.cz+(hum?0:14))});}
            b.meters=b.meters.Where(m=>pilots.Any(p=>p.Seat==m.seat)).ToArray();
            foreach(var p in pilots){var meter=b.meters.FirstOrDefault(m=>m.seat==p.Seat);if(meter==null){meter=new NativeBossMeter{seat=p.Seat,x=p.Feet.X,z=p.Feet.Z,since=now};b.meters=b.meters.Concat(new[]{meter}).Take(8).ToArray();}
                if(Dist(p.Feet.X,p.Feet.Z,meter.x,meter.z)>3){meter.x=p.Feet.X;meter.z=p.Feet.Z;meter.since=now;}
                if(p.Feet.Y>1.6f){if(meter.highAt<0)meter.highAt=now;}else meter.highAt=-100;
                meter.value=Clamp(Math.Max((now-meter.since)/(z.phase==3?4:6),meter.highAt<0?0:(now-meter.highAt)/2));}
            if(hum&&now>=b.shiftDue){b.shiftDue=now+NativeHumBoss.ShiftEvery[z.phase-1];var room=Room(b);int mask=b.shiftMask;
                for(int i=0;i<8;i++){room.Partition(i,(mask>>i)&1,out var from,out _);room.Partition(i,1-((mask>>i)&1),out var to,out _);
                    bool safe=pilots.All(p=>Vector3.Distance(Flat(p.Feet),from)>5&&Vector3.Distance(Flat(p.Feet),to)>5&&Vector3.Dot(Dir(p.Yaw),Vector3.Normalize(from-Flat(p.Feet)))<0&&Vector3.Dot(Dir(p.Yaw),Vector3.Normalize(to-Flat(p.Feet)))<0);if(safe)mask^=1<<i;}
                if(mask!=b.shiftMask){b.shiftFrom=b.shiftMask;b.shiftMask=mask;b.shiftAt=now;}if(z.phase==3){if(b.ceilingAt>b.floorAt)b.floorAt=now;else b.ceilingAt=now;}}
            if(now-b.started>=360){if(!hum){if(b.tideY<7.2f){b.tideFrom=Tide(z,now);b.tideY=7.2f;b.tideAt=now;b.tideUntil=now+2;}if(now>=b.tideUntil)foreach(var p in pilots)output.Add(new BackroomsBossContact{Seat=p.Seat,Damage=100000,Point=p.Feet});return;}
                if(now>=b.enrageDue){b.enrageDue=now+5;if(z.phase<2)z.phase=2;b.x=b.cx;b.z=b.cz;Start(z,"pulse",pilots[0],now);z.hazards=z.hazards.Where(h=>h.until>=now).Concat(new[]{new NativeZoneBossHazardFrame{id=++b.ringSerial,kind="ring",x=b.x,z=b.z,radius=36.4f,hitAt=now+1.4f,until=now+4}}).ToArray();}
                foreach(var ring in z.hazards)if(now>=ring.hitAt&&now<=ring.until)foreach(var p in pilots){var meter=b.meters.First(m=>m.seat==p.Seat);if(meter.lastRing<ring.id&&Math.Abs(Dist(p.Feet.X,p.Feet.Z,ring.x,ring.z)-(now-ring.hitAt)*14)<=.9f){meter.lastRing=ring.id;output.Add(new BackroomsBossContact{Seat=p.Seat,Damage=14,Jump=true,Stun=.5f,Point=new Vector3(ring.x,0,ring.z)});}}return;}
            if(!hum){if(rig==null)throw new ArgumentNullException(nameof(rig));var view=NativeZoneBossViews.DeepEnd(z,b.x,0,b.z,b.yaw,hp,maxHp,b.started-NativeZoneBossRules.IntroSeconds);rig.Update(now,dt,view,null,new Vector3(b.cx,0,b.cz),Tide(z,now),false,Vector3.Zero);
                b.x=rig.Root.X;b.z=rig.Root.Z;b.yaw=rig.Yaw*180f/(float)Math.PI;
                if(rig.OutOfWater>0)b.immuneSince=-100;
                else{if(b.immuneSince<0){b.immuneSince=now;b.diveAt=now;}if(now-b.immuneSince>=4.8f&&z.action!="breach"&&now>=z.staggerAt+z.staggerSeconds){var near=pilots.OrderBy(p=>Dist(p.Feet.X,p.Feet.Z,b.x,b.z)).First();if(z.phase==1){near.Feet.X=Math.Max(b.cx-9,Math.Min(b.cx+9,near.Feet.X));near.Feet.Z=Math.Max(b.cz-9,Math.Min(b.cz+9,near.Feet.Z));}Start(z,"breach",near,now);}if(now-b.immuneSince>=6)b.exposedUntil=Math.Max(b.exposedUntil,now+3);}}

            if(z.cancelAt<0&&z.action!=null&&now>=z.hitAt&&now<=z.actionAt+z.duration)Contacts(z,now,pilots,output,rig);
            if(z.cancelAt>=0&&now<z.staggerAt+z.staggerSeconds&&!b.meters.Any(m=>m.value>=1))return;
            bool campActive=(z.action=="tear"||z.action=="lull")&&now<z.actionAt+z.duration;
            if(now<b.next&&(campActive||!b.meters.Any(m=>m.value>=1)))return;
            var target=pilots.OrderByDescending(p=>hum?p.Noise*100+Dist(p.Feet.X,p.Feet.Z,b.x,b.z):Dist(p.Feet.X,p.Feet.Z,b.x,b.z)).First();
            var still=b.meters.Where(m=>m.value>=1).OrderBy(m=>m.since).FirstOrDefault();
            if(still!=null){target=pilots.First(p=>p.Seat==still.seat);Start(z,hum?"tear":"lull",target,now);still.since=now;still.highAt=target.Feet.Y>1.6f?now:-100;return;}
            int count=hum?NativeHumBoss.Moves.Length:NativeDeepEndBoss.Moves.Length;
            foreach(int i in Enumerable.Range(0,count).OrderBy(i=>b.ready[i])){string id=hum?NativeHumBoss.Moves[i].Id:NativeDeepEndBoss.Moves[i].Id;
                if(id=="tear"||id=="shift"||id=="lull"||b.ready[i]>now||!NativeZoneBossRules.Action(z.archetype,id,out var a)||a.Phase>z.phase)continue;
                float d=Dist(target.Feet.X,target.Feet.Z,b.x,b.z);if(id=="reach"&&d>6||id=="stoop"&&d>4.5f||id=="thrash"&&d>5)continue;
                if(id=="hand"&&NativeHumArena.NearestWall(Room(b),b.shiftMask,target.Feet.X,target.Feet.Z,out _,out _,out _,out _)>2.5f)continue;
                if(id=="breach"&&z.phase==1&&!NativeDeepEndArena.InPool(target.Feet.X-b.cx,target.Feet.Z-b.cz))continue;Start(z,id,target,now);return;}
            var delta=Flat(target.Feet)-new Vector3(b.x,0,b.z);if(delta.Length()>3){var next=new Vector3(b.x,0,b.z)+Vector3.Normalize(delta)*Math.Min(delta.Length(),dt*(hum?2.3f:6));b.x=next.X;b.z=next.Z;b.yaw=Angle(delta);}
            if(!hum&&now-b.surfaceAt>=6){b.surfaceAt=now;b.surfaceX=b.x-b.cx;b.surfaceZ=b.z-b.cz;b.pathSeed++;}
        }
        private static void Contacts(NativeZoneBossFrame z,float now,BackroomsBossPilot[] pilots,List<BackroomsBossContact> output,DeepEndBossRig rig)
        {
            var b=z.backrooms;bool hum=z.archetype==NativeHumBoss.Archetype;string id=z.action;float t=now-z.hitAt;NativeZoneBossRules.Action(z.archetype,id,out var m);
            var origin=new Vector3(z.laneX0,0,z.laneZ0);var target=new Vector3(z.targetX,0,z.targetZ);var end=new Vector3(z.laneX1,0,z.laneZ1);
            if(!b.fired){b.fired=true;
                if(id=="voices"||id=="whistle"){var voices=new List<float>();foreach(var p in pilots){Vector3 spot;
                    if(hum){NativeHumArena.NearestWall(Room(b),b.shiftMask,p.Feet.X,p.Feet.Z,out b.handX,out b.handZ,out b.handNx,out b.handNz);InsetWall(b);spot=new Vector3(b.handX+b.handNx,0,b.handZ+b.handNz);voices.AddRange(new[]{b.handX,1f,b.handZ,b.handNx,b.handNz,now});}
                    else spot=new Vector3(Math.Max(b.cx-17,Math.Min(b.cx+17,p.Feet.X+3)),0,Math.Max(b.cz-17,Math.Min(b.cz+17,p.Feet.Z+3)));
                    output.Add(new BackroomsBossContact{Seat=p.Seat,Add=hum?"faceling":"drowned",Point=spot});if(!hum)output.Add(new BackroomsBossContact{Seat=p.Seat,Add="tiler",Point=new Vector3(b.cx+Math.Sign(p.Feet.X-b.cx)*18,0,p.Feet.Z)});
                }b.voices=voices.ToArray();}
                if(id=="tear"&&z.phase==3)b.torn=b.torn.Concat(new[]{b.tearX,b.tearZ,now}).Skip(Math.Max(0,b.torn.Length+3-96)).ToArray();}
            if(id=="lunge"){bool second=z.phase==3&&t>=2.4f;float age=second?t-2.4f:t;if(second){var swap=origin;origin=end;end=swap;}
                if(age>=0&&age<=1.2f){var q=Vector3.Lerp(origin,end,Clamp(age/1.2f));var previous=new Vector3(b.x,0,b.z);b.x=q.X;b.z=q.Z;foreach(var p in pilots)if(Line(p.Feet,previous,q,3))Hit(z,output,p,34,q,second?1:0);}
                if(t>=(z.phase==3?3.6f:1.2f)&&b.clipAt<z.actionAt&&NativeHumArena.NearestWall(Room(b),b.shiftMask,b.x,b.z,out _,out _,out _,out _)<=1){b.clipAt=now;z.staggerAt=now;z.staggerSeconds=2.2f;z.staggerCause="clip";z.cancelAt=now;b.next=now+2.2f;}return;}
            foreach(var p in pilots){if(hum){
                if(id=="reach"&&t<=m.Strike&&Arc(p.Feet,origin,z.actionYaw,6,130))Hit(z,output,p,22,origin);
                if(id=="stoop"&&t<=m.Strike&&Vector3.Distance(Flat(p.Feet),origin)<=4.5f)Hit(z,output,p,30,origin);
                if(id=="cut"&&t<=m.Strike&&Arc(p.Feet,new Vector3(b.x,0,b.z),b.yaw,3.5f,180))Hit(z,output,p,38,new Vector3(b.x,0,b.z));
                if(id=="row")for(int pass=0;pass<(z.phase==3?2:1);pass++){float at=pass==0?b.rowAt:b.row2At;int axis=pass==0?b.rowAxis:b.row2Axis,mask=pass==0?b.rowMask:b.row2Mask;if(now<at||now>at+.1f)continue;for(int row=0;row<6;row++)if((mask&(1<<row))!=0&&Math.Abs((axis==0?p.Feet.Z:p.Feet.X)-NativeHumArena.RowLine(Room(b),axis,row))<=.7f)Hit(z,output,p,16,p.Feet,pass);}
                if(id=="tear"&&t<=.2f&&Vector3.Distance(Flat(p.Feet),target)<=2.4f)Hit(z,output,p,12,new Vector3(b.x,2,b.z),0,false,.6f,true);
                if(id=="pulse"&&t<=2.6f&&Math.Abs(Vector3.Distance(Flat(p.Feet),origin)-t*14)<=.9f)Hit(z,output,p,14,origin,0,true,.5f);
                if(id=="hand"&&t<=1.5f&&Vector3.Distance(Flat(p.Feet),new Vector3(b.handX,0,b.handZ))<=2.5f){Hit(z,output,p,t<.25f?8:2,new Vector3(b.handX,0,b.handZ),(int)(t/.25f),false,0,false,2.6f);z.holdTarget=p.Seat;z.holdUntil=z.hitAt+1.5f;}
            }else{var centre=new Vector3(b.cx,0,b.cz);
                if(id=="breach"&&t<=.25f&&Vector3.Distance(Flat(p.Feet),target)<=3.2f)Hit(z,output,p,28,target,0,false,.5f);
                if(id=="flop"&&t<=.2f&&Line(p.Feet,origin,end,4))Hit(z,output,p,30,target);
                if(id=="thrash"&&t<=.3f&&Vector3.Distance(Flat(p.Feet),origin)<=5)Hit(z,output,p,18,origin);
                if(id=="jet"&&t<=2.2f){var view=NativeZoneBossViews.DeepEnd(z,b.x,0,b.z,b.yaw,1,1,b.started-NativeZoneBossRules.IntroSeconds);var head=rig.MouthAt;var dir=DeepEndBossRig.JetDir(view,now-z.actionAt,z.hitAt-z.actionAt,NativeDeepEndBoss.Move("jet"),head-centre,Tide(z,now));if(Line(p.Feet,head,head+dir*30,2))Hit(z,output,p,10,head,(int)(t/.25f));}
                if(id=="lull"&&t<=1.8f&&Vector3.Distance(Flat(p.Feet),target)<=1.8f)Hit(z,output,p,t<.3f?8:2,target,t<.3f?0:1+(int)((t-.3f)/.25f),false,.3f);
                if(id=="drain"&&t<=4){output.Add(new BackroomsBossContact{Seat=p.Seat,Point=centre,Pull=5+4*(1-Clamp(Vector3.Distance(Flat(p.Feet),centre)/28))});if(Vector3.Distance(Flat(p.Feet),centre)<=2.6f)Hit(z,output,p,40,centre);}
                if(id=="seiche"&&t<=7){float travel=t*11.4f,along=Vector3.Dot(Flat(p.Feet)-centre,Dir(z.actionYaw));bool back=travel>40;float wave=back?60-travel:travel-20;if(Math.Abs(along-wave)<=1.3f)Hit(z,output,p,18,centre,back?1:0,true);}
                if(id=="lap"&&t<=6)for(int j=1;j<NativeDeepEndBossLook.Spine;j++){var a=rig.Shown.Position[j-1];var q=rig.Shown.Position[j];if(Math.Max(a.Y,q.Y)+NativeDeepEndBossLook.Radius(j*NativeDeepEndBossLook.Seg)>=p.Feet.Y&&Line(p.Feet,a,q,3.2f))Hit(z,output,p,24,q);}
            }}
            if(!hum&&id=="flop"&&t>=.2f&&z.phase==1&&!NativeDeepEndArena.InPool(end.X-b.cx,end.Z-b.cz)){z.staggerAt=now;z.staggerSeconds=2.5f;z.staggerCause="stranded";z.cancelAt=now;b.next=now+2.5f;}
        }
    }
}

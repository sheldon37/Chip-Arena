using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeZoneBossHazardFrame
    {
        public int id;public string kind;public float x,y,z,radius,hitAt,until;
        public NativeZoneBossHazardFrame Copy()=>(NativeZoneBossHazardFrame)MemberwiseClone();
        public bool Valid()=>id>=0&&(kind=="blow"||kind=="deslag"||kind=="pour"||kind=="charge")&&new[]{x,y,z,radius,hitAt,until}.All(NativeZoneCombat.Finite)&&radius>0&&radius<=4&&hitAt>=0&&until>=hitAt&&until-hitAt<=7;
    }
    // Authority clocks only. Optional times are -100 on the wire; no pose data is replicated.
    [Serializable] public sealed class NativeZoneBossFrame
    {
        public string archetype,action,staggerCause;
        public int actor,phase=1,actionId,targetSeat=-1,variant,holdTarget=-1,holdSide=1,parts,sideBroken;
        public float phaseAt,actionAt=-100,hitAt=-100,duration,cancelAt=-100,actionYaw,targetX,targetZ;
        public float laneX0,laneZ0,laneX1,laneZ1,holdUntil=-100,staggerAt=-100,staggerSeconds,staggerPushX,staggerPushZ,stagger;
        public float lampBrokenAt=-100,pantoBrokenAt=-100,expressHitAt=-100,liveAt=-100,liveUntil=-100;
        public float barrierDownAt=-100,boardDownAt=-100,roofOpenAt=-100,spillAt=-100,deathAt=-100;
        public NativeZoneBossHazardFrame[] hazards=Array.Empty<NativeZoneBossHazardFrame>();
        public float[] partAt={-100,-100,-100,-100,-100};public int[] spillIds=Array.Empty<int>();
        public NativeZoneBossFrame Copy(){var f=(NativeZoneBossFrame)MemberwiseClone();f.partAt=(float[])partAt.Clone();f.spillIds=(int[])spillIds.Clone();f.hazards=hazards.Select(h=>h.Copy()).ToArray();return f;}
        public bool Valid()
        {
            if(!NativeZoneBossRules.Known(archetype)||actor<10000||phase<1||phase>3||actionId<0||targetSeat< -1||targetSeat>10000||holdTarget< -1||holdTarget>10000||Math.Abs(holdSide)!=1||variant<0||variant>3||parts<0||parts>31||sideBroken<0||sideBroken>3||stagger<0||stagger>1)return false;
            if(action!=null&&!NativeZoneBossRules.Action(archetype,action,out _))return false;
            if(staggerCause!=null&&!new[]{"crash","express","part","phase","choke","kneel"}.Contains(staggerCause))return false;
            if(hazards==null||hazards.Length>64||hazards.Any(h=>h==null||!h.Valid())||hazards.Select(h=>h.id).Distinct().Count()!=hazards.Length)return false;
            if(partAt==null||partAt.Length!=5||spillIds==null||spillIds.Length>6||spillIds.Any(i=>i<10000)||spillIds.Distinct().Count()!=spillIds.Length)return false;
            if(!new[]{phaseAt,actionAt,hitAt,duration,cancelAt,actionYaw,targetX,targetZ,laneX0,laneZ0,laneX1,laneZ1,holdUntil,staggerAt,staggerSeconds,staggerPushX,staggerPushZ,stagger,lampBrokenAt,pantoBrokenAt,expressHitAt,liveAt,liveUntil,barrierDownAt,boardDownAt,roofOpenAt,spillAt,deathAt}.All(NativeZoneCombat.Finite)||!partAt.All(NativeZoneCombat.Finite))return false;
            return phaseAt>=0&&duration>=0&&duration<=30&&staggerSeconds>=0&&staggerSeconds<=10&&(action==null||actionAt>=0&&hitAt>=actionAt&&hitAt<=actionAt+duration+.001f)&&(cancelAt<0||cancelAt>=actionAt)&&partAt.All(t=>t==-100||t>=0);
        }
    }
    public readonly struct ZoneBossAction
    {
        public readonly string Id;public readonly int Phase;public readonly float Wind,Strike,Follow,Recover,Cooldown;
        public float Duration=>Wind+Strike+Follow+Recover;
        public ZoneBossAction(string id,int phase,float wind,float strike,float follow,float recover,float cooldown){Id=id;Phase=phase;Wind=wind;Strike=strike;Follow=follow;Recover=recover;Cooldown=cooldown;}
    }
    public static class NativeZoneBossRules
    {
        public const string Station="station-647",Ironworks="ironworks-converter";
        public const float IntroSeconds=9.0678f;
        public static bool RuntimeEnabled=>true; // Native acceptance is a required build/publish gate.
        public static int StompLeg(float x,float z)=>(x<0?0:1)+(z<0?2:0);
        public static int DepartureLeg(int phase,float sinceHit,float strike)=>sinceHit<0?-1:sinceHit<=strike?0:phase>=3&&sinceHit>=2&&sinceHit<=2+strike?1:-1;
        public static bool Known(string id)=>id==Station||id==Ironworks;
        public static string Archetype(string map)=>NativeZoneCombat.Station(map)?Station:NativeZoneCombat.Ironworks(map)?Ironworks:null;
        public static int Phase(string id,float hp,float maxHp)=>hp<=maxHp*(id==Station?.30f:.35f)?3:hp<=maxHp*(id==Station?.65f:.70f)?2:1;
        public static bool Action(string boss,string id,out ZoneBossAction value)
        {
            value=default;
            if(boss==Station){foreach(var m in NativeStationBoss.Moves)if(m.Id==id){value=new ZoneBossAction(id,m.Phase,m.Anticipation,m.Strike,m.Follow,m.Recover,m.Cooldown);return true;}}
            else if(boss==Ironworks){foreach(var a in NativeIronworksBoss.Actions)if(a.Id==id){value=new ZoneBossAction(id,a.From,a.Windup,a.Strike,a.Follow,a.Recover,a.Cooldown);return true;}}
            if(id=="arrive"||id=="phase"||id=="crash"||id=="die"){value=new ZoneBossAction(id,1,id=="arrive"?IntroSeconds:id=="phase"?3.5f:id=="crash"?2.2f:4,0,0,0,0);return Known(boss);}return false;
        }
        public static void Start(NativeZoneBossFrame f,string action,float now,int seat,float x,float z,float yaw)
        {
            if(!Action(f.archetype,action,out var a)||a.Phase>f.phase)throw new ArgumentException("Unavailable boss action");
            f.action=action;f.actionId++;f.actionAt=now;f.hitAt=now+a.Wind;f.duration=a.Duration;f.cancelAt=-100;f.targetSeat=seat;f.targetX=x;f.targetZ=z;f.actionYaw=yaw;
            if(action=="alteration")f.hitAt=now+1.8f; // Pane contact follows the 0.6 s gesture and 1.2 s warning.
            if(action=="arrive"){f.hitAt=now+IntroSeconds;f.duration=IntroSeconds;}
            if(action=="departure"&&f.phase==3)f.duration=6.1f;
            if(action=="departure"&&f.lampBrokenAt>=0){f.hitAt+=.4f;if(f.phase<3)f.duration+=.4f;}
        }
        // Stable seeds avoid sending droplets as per-frame poses. Both authority and presentation use this layout.
        public static void Droplet(int actionId,int index,int phase,out float x,out float z,out float delay)
        {
            uint seed=NativePveRandom.Hash((uint)actionId,index);double angle=(seed%100000)/100000d*Math.PI*2;
            double r=4+(seed/100000%6500)/1000d;x=(float)(Math.Sin(angle)*r);z=(float)(Math.Cos(angle)*r);
            delay=2+1.8f*index/(phase==3?28:14);
        }
    }
    // Pure adapters shared by native host, replicas and managed contract tests.
    public static class NativeZoneBossViews
    {
        public static StationBossView Station(NativeZoneBossFrame f,float x,float y,float z,float yaw,float hp,float maxHp,float spawned)
        {
            if(f==null||f.archetype!=NativeZoneBossRules.Station)return StationBossView.None;
            return new StationBossView{live=true,x=x,y=y,z=z,yaw=yaw,hp=hp,maxHp=maxHp,spawned=spawned,phase=f.phase,phaseAt=StationBossView.Time(f.phaseAt),action=f.action,actionId=f.actionId,actionAt=StationBossView.Time(f.actionAt),hitAt=StationBossView.Time(f.hitAt),duration=f.duration,cancelAt=StationBossView.Time(f.cancelAt),laneX0=f.laneX0,laneZ0=f.laneZ0,laneX1=f.laneX1,laneZ1=f.laneZ1,holdTarget=f.holdTarget,holdSide=f.holdSide,holdUntil=StationBossView.Time(f.holdUntil),staggerAt=StationBossView.Time(f.staggerAt),staggerSeconds=f.staggerSeconds,staggerPushX=f.staggerPushX,staggerPushZ=f.staggerPushZ,staggerCause=f.staggerCause,lampBrokenAt=StationBossView.Time(f.lampBrokenAt),pantoBrokenAt=StationBossView.Time(f.pantoBrokenAt),expressHitAt=StationBossView.Time(f.expressHitAt),sideBroken=f.sideBroken,liveAt=StationBossView.Time(f.liveAt),liveUntil=StationBossView.Time(f.liveUntil),barrierDownAt=StationBossView.Time(f.barrierDownAt),boardDownAt=StationBossView.Time(f.boardDownAt),roofOpenAt=StationBossView.Time(f.roofOpenAt),spillAt=StationBossView.Time(f.spillAt),spillIds=f.spillIds,actionTarget=f.targetSeat,diedAt=StationBossView.Time(f.deathAt)};
        }
        public static IronworksBossView Ironworks(NativeZoneBossFrame f,float x,float y,float z,float yaw,float hp,float maxHp,float spawned)
        {
            if(f==null||f.archetype!=NativeZoneBossRules.Ironworks)return IronworksBossView.None;
            string action=f.cancelAt>=0?null:f.action;float at=f.actionAt;
            if(f.cancelAt>=0&&(f.staggerCause=="choke"||f.staggerCause=="kneel"||f.staggerCause=="crash")){action=f.staggerCause;at=f.staggerAt;}
            return new IronworksBossView{live=true,hazards=f.hazards,x=x,y=y,z=z,yaw=yaw,hp=hp,maxHp=maxHp,spawned=spawned,action=action,actionAt=IronworksBossView.Time(at),actionYaw=f.actionYaw,targetX=f.targetX,targetZ=f.targetZ,targetSeat=f.action=="grab"?f.holdTarget:f.targetSeat,variant=f.variant,phase=f.phase,phaseAt=IronworksBossView.Time(f.phaseAt),parts=f.parts,partAt0=IronworksBossView.Time(f.partAt[0]),partAt1=IronworksBossView.Time(f.partAt[1]),partAt2=IronworksBossView.Time(f.partAt[2]),partAt3=IronworksBossView.Time(f.partAt[3]),partAt4=IronworksBossView.Time(f.partAt[4]),stagger=f.stagger,deathAt=IronworksBossView.Time(f.deathAt)};
        }
    }

}

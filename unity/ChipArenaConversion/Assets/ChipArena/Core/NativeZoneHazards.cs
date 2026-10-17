using System;
using System.Linq;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeZoneHazardFrame
    {
        public bool running,boss;public int wave=1;public float clock,start=-100,trainAt=-100,leverAt=-100,leverUntil=-100,lastCallAt=-100,bossAt=-100;
        public float[] glassX=Array.Empty<float>(),glassZ=Array.Empty<float>(),glassAt=Array.Empty<float>();
        public bool floodGrounded;
        public NativeZoneHazardFrame Copy(){var f=(NativeZoneHazardFrame)MemberwiseClone();f.glassX=(float[])glassX.Clone();f.glassZ=(float[])glassZ.Clone();f.glassAt=(float[])glassAt.Clone();return f;}
        public bool Valid()=>wave>=1&&wave<=4&&new[]{clock,start,trainAt,leverAt,leverUntil,lastCallAt,bossAt}.All(NativeZoneCombat.Finite)&&clock>=0&&glassX!=null&&glassZ!=null&&glassAt!=null&&glassAt.Length<=8&&glassX.Length==glassAt.Length&&glassZ.Length==glassAt.Length&&glassX.All(NativeZoneCombat.Finite)&&glassZ.All(NativeZoneCombat.Finite)&&glassAt.All(NativeZoneCombat.Finite);
        public IronworksHazardInput Ironworks=>new IronworksHazardInput{Running=running,Boss=boss,Wave=wave,Clock=clock};
        public static NativeHazardState Cycle(double t,double period,double warn,double live)
        {
            if(t<0)return default;double c=t-Math.Floor(t/period)*period;
            return c<warn?new NativeHazardState{Phase=NativeHazardPhase.Warn,Cycle=(int)(t/period),Since=c,Until=warn-c,Level=(float)(c/warn)}:c<warn+live?new NativeHazardState{Phase=NativeHazardPhase.Active,Cycle=(int)(t/period),Since=c-warn,Until=warn+live-c,Level=1}:new NativeHazardState{Phase=NativeHazardPhase.Idle,Cycle=(int)(t/period),Since=c-warn-live,Until=period-c};
        }
        public void Fill(NativeStationView v,float now)
        {
            v.Reset();if(!running){v.RailLive=0;return;}
            v.TrainAt=trainAt<0?double.NaN:trainAt;v.TrainLine=1;v.Levers=leverAt>=0&&now<leverUntil;v.LeversAt=NativeZoneCombat.View(leverAt);v.LeversUntil=NativeZoneCombat.View(leverUntil);
            v.RailLive=v.Levers?0:NativeExpeditionStation.AllRails;v.RailChangedAt=leverAt<0?start:v.Levers?leverAt:leverUntil;v.FloodLive=!v.Levers||floodGrounded;
            v.LastCallAt=NativeZoneCombat.View(lastCallAt);v.Boss=boss;v.BossAt=NativeZoneCombat.View(bossAt);
            for(int i=0;i<2;i++)v.Cabinets[i]=Cycle(clock+i*4.3,11.5,1,1);
            v.Steam=Cycle(clock+2,15,2,3);v.GlassCount=glassAt.Length;
            for(int i=0;i<glassAt.Length;i++)v.Glass[i]=new NativeStationGlass{X=glassX[i],Z=glassZ[i],At=glassAt[i]};
        }
    }
}

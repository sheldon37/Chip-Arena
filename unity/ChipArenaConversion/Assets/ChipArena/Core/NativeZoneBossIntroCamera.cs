using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>Frames the existing in-world arrival during the replicated intro hold.</summary>
    public static class NativeZoneBossIntroCamera
    {
        public static bool Sample(string kind,bool holding,string cinematic,double age,Vector3 root,float yaw,
            out Vector3 eye,out Vector3 look,out float fov)
        {
            eye=look=default;fov=65;
            if(!holding||cinematic!="intro"||!NativeZoneBossRules.Known(kind)||double.IsNaN(age)||double.IsInfinity(age)||age<0)return false;
            if(kind==NativeZoneBossRules.Station)
            {
                // Look down T1 from the concourse, then follow the authored climb to the gate.
                StationBossArrival.Root((float)age,NativeStationBossBeats.Current,root,yaw*BossMath.Deg,out var arrival,out _,out _);
                look=new Vector3(Math.Max(-28,arrival.X),2.8f,arrival.Z);
                eye=new Vector3(-8,8,13);
            }
            else
            {
                // Inside the converter hall (x -50..-24, z -12..12), above the pit rim.
                look=root+new Vector3(0,3.5f,0);eye=root+new Vector3(6,7,10);fov=70;
            }
            return true;
        }
    }
}

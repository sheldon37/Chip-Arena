using System;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativePodRecord
    {
        public int seat,order;public string name,bodyColor,accentColor,look;
        public float x,y,z,yaw;public double end,launchAt=-1;public bool departed;
        public NativePodRecord Copy()=>(NativePodRecord)MemberwiseClone();
    }
    // Authority state for a run's first landing and individual extraction. No visual timings are authored here.
    public sealed class NativePodRun
    {
        public const int LookLimit=256;
        public const float UseRadius=1.5f;
        public static string Look(string value)=>value!=null&&value.Length<=LookLimit?NativePodDrop.Normalize(value):"";
        public bool Started{get;private set;}
        private readonly NativePodRecord[] records;
        public NativePodRun(NativePodRecord[] source)
        {records=(source??Array.Empty<NativePodRecord>()).Select(r=>r.Copy()).ToArray();if(!Valid(records,double.MaxValue))throw new ArgumentException("Invalid pod records");}
        public NativePodRecord[] Snapshot()=>Started?records.Select(r=>r.Copy()).ToArray():Array.Empty<NativePodRecord>();
        public bool Start(NativeMatch match)
        {
            if(Started||records.Length==0||!match.BeginExpeditionCountdown())return false;
            Started=true;foreach(var r in records){r.end=match.PhaseEnd;r.launchAt=-1;r.departed=false;}return true;
        }
        public void CancelDepartures(){foreach(var r in records)if(!r.departed)r.launchAt=-1;}
        public bool Departing(int seat)=>records.Any(r=>r.seat==seat&&r.launchAt>=0&&!r.departed);
        public static bool InReach(NativePodRecord r,Vector3 feet)
        {
            if(r==null||r.departed||r.launchAt>=0||!Finite(feet.X)||!Finite(feet.Y)||!Finite(feet.Z))return false;
            float dx=feet.X-r.x,dz=feet.Z-r.z;
            return Math.Abs(feet.Y-r.y)<=1.7f&&dx*dx+dz*dz<=UseRadius*UseRadius&&dx*Math.Sin(r.yaw)+dz*Math.Cos(r.yaw)>=-.2f;
        }
        public bool Board(int seat,Vector3 feet,double now,bool eligible)
        {
            var r=records.FirstOrDefault(p=>p.seat==seat);
            if(!Started||!eligible||!Finite(now)||r==null||now<r.end||!InReach(r,feet))return false;
            r.launchAt=now;return true;
        }
        public void Tick(double now,Func<int,bool> eligible,Func<int,bool> extract)
        {
            if(!Started||!Finite(now))return;
            foreach(var r in records)if(r.launchAt>=0&&!r.departed)
            {
                if(!eligible(r.seat)){r.launchAt=-1;continue;}
                if(now-r.launchAt+1e-6<NativePodDrop.LaunchGone)continue;
                if(extract(r.seat))r.departed=true;else r.launchAt=-1;
            }
        }
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        public static bool Valid(NativePodRecord[] rows,double now)=>rows==null||rows.Length<=8&&rows.All(r=>r!=null&&r.seat>0&&r.order>=0&&r.order<8&&
            r.name!=null&&r.name.Length<=40&&r.look!=null&&r.look.Length<=LookLimit&&r.look==Look(r.look)&&
            r.bodyColor!=null&&r.bodyColor.Length<=7&&r.accentColor!=null&&r.accentColor.Length<=7&&
            Finite(r.x)&&Finite(r.y)&&Finite(r.z)&&Finite(r.yaw)&&Finite(r.end)&&r.end>=0&&Finite(r.launchAt)&&r.launchAt>=-1&&r.launchAt<=now+.01&&
            (!r.departed||r.launchAt>=0&&now-r.launchAt+1e-5>=NativePodDrop.LaunchGone))&&rows.Select(r=>r.seat).Distinct().Count()==rows.Length;
    }
}

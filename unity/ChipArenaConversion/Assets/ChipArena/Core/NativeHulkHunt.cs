using System;
namespace ChipArena.UnityConversion
{
    // Authority state. Public presentation fields are copied explicitly into NativeMinionFrame.
    public class NativeHulkHunt
    {
        public int Move,ChargeEnd,ChargeTarget;
        public float ChargeAt=-100,ChargeEndAt=-100;
        public float ChargeReadyAt,ChargeProbeAt,ChargeSlowUntil,SeenAt=-100;
        public int SeenSeat;
        public V3 SeenPoint,ChargePoint,ChargeHeading,ChargeOrigin;
        public bool ChargeLocked;
        public bool Charging=>ChargeAt>=0&&ChargeEndAt<0;
        public void BeginCharge(float now,int seat,V3 origin,V3 point)
        {
            ChargeAt=now;ChargeEndAt=-100;ChargeEnd=0;ChargeTarget=seat;ChargeOrigin=origin;
            ChargeLocked=false;ChargeSlowUntil=0;TrackCharge(point);
        }
        public void TrackCharge(V3 point)
        {
            if(ChargeLocked)return;ChargePoint=point;var d=point-ChargeOrigin;d.y=0;
            if(d.Length>.001f)ChargeHeading=d.Normalized;
        }
        public void LockCharge(float now){if(Charging&&now>=ChargeAt+NativeHulkMoves.ChargeWind)ChargeLocked=true;}
        public bool Spent(V3 at,float now)=>now>=ChargeAt+NativeHulkMoves.ChargeWind+NativeHulkMoves.ChargeRun||
            V3.Dot(at-ChargeOrigin,ChargeHeading)>=V3.Dot(ChargePoint-ChargeOrigin,ChargeHeading)+NativeHulkMoves.ChargeOvershoot;
        public void EndCharge(float now,int how)
        {ChargeEndAt=now;ChargeEnd=how;ChargeReadyAt=now+NativeHulkMoves.ChargeCooldown;}
        public bool CancelCharge(float now,float damage,float maxHp)
        {
            if(!Charging||now>=ChargeAt+NativeHulkMoves.ChargeWind||damage<maxHp*.25f)return false;
            ChargeAt=ChargeEndAt=-100;ChargeEnd=ChargeTarget=0;ChargeLocked=false;ChargeReadyAt=now+4;return true;
        }
        // Earliest contact of the horizontal swept capsule with a pilot, not just its end position.
        public static bool Sweep(V3 from,V3 to,V3 pilot,float radius,out float fraction)
        {
            fraction=0;if(Math.Abs(pilot.y-from.y)>=2)return false;
            var d=to-from;d.y=0;var o=from-pilot;o.y=0;
            float c=V3.Dot(o,o)-radius*radius;if(c<=0)return true;
            float a=V3.Dot(d,d),b=V3.Dot(o,d),disc=b*b-a*c;
            if(a<1e-10f||disc<0)return false;fraction=(-b-(float)Math.Sqrt(disc))/a;
            return fraction>=0&&fraction<=1;
        }
        public static bool Inside(ContentMap map,V3 point,float pad=0)
        {var b=map?.bounds;return b!=null&&point.x-pad>b.minX&&point.x+pad<b.maxX&&point.z-pad>b.minZ&&point.z+pad<b.maxZ;}
        private static readonly string[] protectedNames={"shell","bounds","floor","ceiling","stair","ramp","hazard","door-frame","doorframe","machine"};
        public static bool Protected(ContentMap map,ContentBlock b)
        {
            if(b==null||map?.bounds==null)return true;var bounds=map.bounds;
            if(b.pos.x-b.size.x*.5f<=bounds.minX||b.pos.x+b.size.x*.5f>=bounds.maxX||b.pos.z-b.size.z*.5f<=bounds.minZ||b.pos.z+b.size.z*.5f>=bounds.maxZ)return true;
            string id=(b.id??"").ToLowerInvariant();
            foreach(var token in protectedNames)if(id.Contains(token))return true;
            foreach(var h in map.hazards??Array.Empty<MapHazard>())if(id==h.id||id.StartsWith(h.id+"-",StringComparison.Ordinal))return true;
            return false;
        }
    }
}

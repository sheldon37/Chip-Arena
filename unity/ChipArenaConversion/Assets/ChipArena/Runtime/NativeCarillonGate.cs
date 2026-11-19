using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Both the moving leaves and the physical edge sample the same clock and obstruction rule.
        // Park the lower edge over an occupant until they clear the sweep. No crush damage or teleport.
        internal float CarillonGateBottom(MapHazard h,double now)
        {
            var s=NativeWardZeroHazards.At(h,Match.Phase,StageRound,Match.FightAt,now);
            float bottom=(1-NativeWardZeroHazards.Closure(s))*5.15f;
            float Clearance(Vector3 p,float radius,float half)
            {
                if(Mathf.Abs(p.x-h.center.x)>h.size.x*.5f+radius||Mathf.Abs(p.z-h.center.z)>h.size.z*.5f+radius||p.y-half>5.12f)return 0;
                return Mathf.Min(5.15f,p.y+half+.04f);
            }
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected)bottom=Mathf.Max(bottom,Clearance(p.Body.position,p.Collider.radius,p.Collider.height*.5f));
            foreach(var m in Minions)if(m.Hp>0)bottom=Mathf.Max(bottom,Clearance(m.Body.position,m.Collider.radius,m.Collider.height*.5f));
            return bottom;
        }
        private void PoseCarillonGate(MapHazard h,BoxCollider box,double now)
        {
            float y=CarillonGateBottom(h,now);
            float height=5.35f-y;
            box.size=new Vector3(h.size.x,height,h.size.z);
            box.transform.position=new Vector3(h.center.x,y+height*.5f,h.center.z);
            box.enabled=true;
        }
    }
}

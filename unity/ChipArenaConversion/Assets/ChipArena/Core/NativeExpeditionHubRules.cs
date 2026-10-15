using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    // Host-owned state; no timers or decisions are accepted from a client.
    public sealed class ExpeditionHubAuthority
    {
        public readonly ExpeditionHubLayout Layout;
        private readonly ExpeditionGateState[] gates;
        private readonly double[] clearSince;
        public ExpeditionHubAuthority(ExpeditionHubLayout layout,double now)
        {
            Layout=layout;gates=new ExpeditionGateState[layout.Gates.Length];clearSince=new double[gates.Length];
            for(int i=0;i<gates.Length;i++){gates[i]=new ExpeditionGateState{Id=layout.Gates[i].Id,ChangedAt=now-2};clearSince[i]=-1;}
        }
        public ExpeditionHubView View()=>new ExpeditionHubView{Gates=(ExpeditionGateState[])gates.Clone(),Count=gates.Length,Holster=true,Ward=true};
        public bool Interact(V3 feet,double now,Func<ExpeditionVolume,bool> occupied)
        {
            for(int i=0;i<gates.Length;i++)if(Layout.Gates[i].Interact.Contains(feet))
            {if(gates[i].Open&&occupied(Layout.Gates[i].Closure))return true;Set(i,!gates[i].Open,now);return true;}return false;
        }
        private void Set(int i,bool open,double now){gates[i].Open=open;gates[i].ChangedAt=now;clearSince[i]=-1;}
        public void Step(double now,Func<ExpeditionVolume,bool> occupied)
        {
            for(int i=0;i<gates.Length;i++)
            {
                var g=Layout.Gates[i];
                if(occupied(g.Closure)){clearSince[i]=-1;if(!gates[i].Open)Set(i,true,now);continue;}
                if(!gates[i].Open)continue;
                if(occupied(g.Sensor)){clearSince[i]=-1;continue;}
                if(clearSince[i]<0)clearSince[i]=now;
                if(now-clearSince[i]>=Layout.AutoClose)Set(i,false,now);
            }
        }
        public bool Blocking(int i,double now)=>gates[i].Open?now-gates[i].ChangedAt<Layout.Release:now-gates[i].ChangedAt>=Layout.Block;
    }
    public static class ExpeditionHubRules
    {
        public const double HolsterDelay=.25,AggroDelay=1.5,SpawnInterval=3,FarSeconds=15,IdleSeconds=180,LostSeconds=30;
        public static int Cap(int alive)=>alive<=0?0:Math.Min(16,2+2*alive);
        public static float Reward(string ring)=>ring=="near"?.4f:ring=="mid"?.6f:ring=="far"?.8f:0;
        public static float Weight(string ring)=>ring=="near"?.35f:ring=="mid"?.7f:ring=="far"?1:0;
        public static bool Kind(string ring,string kind,double elapsed)=>kind=="shambler"||kind=="runner"||(ring=="mid"||ring=="far")&&kind=="spitter"||ring=="far"&&kind=="brute"&&elapsed>=360;
        // Horizontal slab intersection: the ward cannot be bypassed by an arcing projectile above the fence.
        public static bool Intersects(ExpeditionVolume v,V3 a,V3 b,float padding=0)
        {
            double r=v.yaw*Math.PI/180,c=Math.Cos(r),s=Math.Sin(r);
            double ax=c*(a.x-v.x)-s*(a.z-v.z),az=s*(a.x-v.x)+c*(a.z-v.z);
            double bx=c*(b.x-v.x)-s*(b.z-v.z),bz=s*(b.x-v.x)+c*(b.z-v.z),lo=0,hi=1;
            bool Slab(double p,double d,double h){if(Math.Abs(d)<1e-10)return Math.Abs(p)<=h;double t0=(-h-p)/d,t1=(h-p)/d;if(t0>t1){double t=t0;t0=t1;t1=t;}lo=Math.Max(lo,t0);hi=Math.Min(hi,t1);return lo<=hi;}
            return Slab(ax,bx-ax,v.sx/2+padding)&&Slab(az,bz-az,v.sz/2+padding)&&hi>=0&&lo<=1;
        }
        public static bool Inside(ExpeditionVolume[] volumes,V3 p)
        {foreach(var v in volumes)if(v.Contains(new V3(p.x,v.y,p.z)))return true;return false;}
        public static bool Crosses(ExpeditionVolume[] volumes,V3 a,V3 b,float radius=0)
        {
            bool ia=Inside(volumes,a),ib=Inside(volumes,b);if(ia!=ib)return true;if(ia)return false;
            foreach(var v in volumes)if(Intersects(v,a,b,radius))return true;return false;
        }
        public static InputFrame Holster(InputFrame i)
        {i.Attack=i.Shoot=i.Block=i.AbilityQ=i.AbilityE=i.AbilityEHeld=i.AbilityEReleased=i.Hadoken=i.HadokenHeld=i.Uppercut=false;i.SirenAbility=0;i.CancelAbilities=true;return i;}
    }
}

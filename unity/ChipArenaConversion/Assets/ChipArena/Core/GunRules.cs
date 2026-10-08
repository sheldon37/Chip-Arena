using System;

namespace ChipArena.UnityConversion
{
    public enum WeaponId : byte { Sword, Pistol, Rifle, MachineGun }
    public sealed class GunDefinition
    {
        public readonly string Name;
        public readonly int Magazine;
        public readonly float Damage,Interval,Speed,Gravity,Range,Impulse,Reload,Spread,Fov,AimSpread,Settle,AimMove,Sensitivity,AimInterval,Muzzle;
        public GunDefinition(string name,int magazine,float damage,float interval,float speed,float gravity,float range,float impulse,float reload,float spread,
            float fov,float aimSpread,float settle,float aimMove,float sensitivity,float aimInterval,float muzzle)
        { Name=name;Magazine=magazine;Damage=damage;Interval=interval;Speed=speed;Gravity=gravity;Range=range;Impulse=impulse;Reload=reload;Spread=spread;
          Fov=fov;AimSpread=aimSpread;Settle=settle;AimMove=aimMove;Sensitivity=sensitivity;AimInterval=aimInterval;Muzzle=muzzle; }
    }
    // Primary and ADS values copied from src/shared/gun-kit.ts at 05950ea. No Chip modifiers or E abilities in this test.
    public static class GunRules
    {
        public static readonly GunDefinition[] All={null,
            new GunDefinition("Pistol",7,23,.42f,52,32,55,175,1.1f,.008f,55,.25f,.15f,.75f,.68f,1.12f,.51f),
            new GunDefinition("Rifle",3,58,1.15f,150,12,140,350,2.6f,.0015f,28,0,.3f,.5f,.36f,1,1.22f),
            new GunDefinition("Machine Gun",48,8,.085f,49,36,48,65,2.4f,.025f,52,.4f,.2f,.55f,.63f,1,.91f)};
        public static GunDefinition Get(WeaponId id) => All[(int)id];
        public static bool Valid(WeaponId id) => (int)id>=0&&(int)id<All.Length;
        public static string Name(WeaponId id) => id==WeaponId.Sword?"Sword":Get(id).Name;
        public static bool Ranged(WeaponId id) => id!=WeaponId.Sword;
        public static V3 Direction(float yaw,float pitch)
        {
            double y=yaw*Math.PI/180,p=pitch*Math.PI/180;
            return new V3((float)(Math.Sin(y)*Math.Cos(p)),(float)Math.Sin(p),(float)(Math.Cos(y)*Math.Cos(p)));
        }
        public static float BallisticPitch(float distance,float height,float speed,float gravity)
        {
            if(distance<.001f||gravity<=0||speed<=0) return (float)(Math.Atan2(height,distance)*180/Math.PI);
            double squared=speed*speed,disc=squared*squared-gravity*(gravity*distance*distance+2*height*squared);
            return disc<0?45:(float)(Math.Atan((squared-Math.Sqrt(disc))/(gravity*distance))*180/Math.PI);
        }
        public static float Spread(Actor a,float time)
        {
            var gun=Get(a.Weapon); if(!a.Aiming) return gun.Spread;
            float settle=MathUtil.Clamp((time-a.AimAt)/gun.Settle,0,1),spread=gun.Spread*(1+(gun.AimSpread-1)*settle);
            if(a.Weapon==WeaponId.MachineGun&&time-a.LastShot<=.25f) spread=Math.Min(gun.Spread,spread+.002f*a.AimShots);
            return spread;
        }
        public static V3 RotateYaw(V3 v,float yaw)
        {
            double r=yaw*Math.PI/180; float c=(float)Math.Cos(r),s=(float)Math.Sin(r);
            return new V3(v.x*c+v.z*s,v.y,-v.x*s+v.z*c);
        }
        // A fixed gameplay muzzle (source also separates it from cosmetic recoil). Do not let barrel length bypass cover.
        public static V3 Muzzle(Actor a) => a.Position+new V3(0,1.18f,0)+RotateYaw(new V3(.2f,0,.48f),a.Yaw);
        public static bool SegmentActor(V3 start,V3 end,Actor a,float radius,out float t,out bool head)
        {
            t=2;head=false;
            foreach(float height in BodyHeights)
            {
                float hit; if(MathUtil.SegmentSphere(start,end,a.Position+new V3(0,height,0),.34f+radius,out hit)&&hit<t) t=hit;
            }
            float h;
            if(MathUtil.SegmentSphere(start,end,a.Position+new V3(0,1.51f,0),.19f+Math.Min(.06f,radius),out h)&&h<t) { t=h;head=true; }
            return t<=1;
        }
        private static readonly float[] BodyHeights={.35f,.7f,1.05f};
        // Aim point is traced from the camera crosshair; shots still sweep from the authoritative muzzle.
        public static V3 AimPoint(Snapshot snapshot,int owner,V3 origin,V3 direction,float distance)
        {
            V3 end=origin+direction*distance; float nearest=1;
            foreach(var c in snapshot.Covers) { float t;if(c.Hp>0&&MathUtil.SegmentBox(origin,end,c,0,out t)&&t<nearest)nearest=t; }
            foreach(var a in snapshot.Actors) { float t;bool h;if(a.Id!=owner&&a.Alive&&SegmentActor(origin,end,a,0,out t,out h)&&t<nearest)nearest=t; }
            if(end.y<.03f&&origin.y>.03f) nearest=Math.Min(nearest,(origin.y-.03f)/(origin.y-end.y));
            return origin+(end-origin)*nearest;
        }
    }
}

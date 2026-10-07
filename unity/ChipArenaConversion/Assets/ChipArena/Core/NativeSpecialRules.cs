using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public sealed class NativeMeleeAction
    {
        public string Kind;public float At,Release,Until,Yaw,Landed=-1;public bool Released;public int Shot;
        public readonly HashSet<string> Hit=new HashSet<string>();
    }
    [Serializable] public sealed class NativeRecovery
    {
        public float At,Until,Landed=-1,PickupUntil,RollAt=-1,RollUntil,JuggleUntil;public bool PickupUsed,HasDirection,Ragdoll;
        public int JuggleHits,Source;public V3 Direction;
    }
    public static class NativeSpecialRules
    {
        public static bool CanBegin(string spec,bool grounded,bool busy,bool committed,bool rolling,int projectiles,float stamina,float cost)
            =>NativeMeleeRules.Standard(spec)&&!busy&&!committed&&!rolling&&(spec!="greatsword"||grounded)&&((spec!="shield"&&spec!="daggers")||projectiles<100)&&stamina>=cost;
        public static float Windup(string spec)=>spec=="sword"?.26f:spec=="daggers"?.19f:spec=="greatsword"?.28f:.30f;
        public static float Recovery(string spec)=>spec=="sword"?.50f:spec=="daggers"?.28f:spec=="greatsword"?.55f:.40f;
        public static string Kind(string spec)=>spec=="sword"?"sword-launch":spec=="daggers"?"dagger-throw":spec=="greatsword"?"wolf-spin":"shield-throw";
        public static NativeMeleeAction Begin(string spec,float rate,float time,float yaw)=>new NativeMeleeAction{Kind=Kind(spec),At=time,Release=time+Windup(spec)/rate,Until=time+(spec=="greatsword"?3.5f:(Windup(spec)+Recovery(spec))/rate),Yaw=yaw};
        public static bool Strike(V3 delta,float range,float extra,float yaw,string kind)
        {
            if(delta.Length>range+extra||Math.Abs(delta.y)>(kind=="launch"?2.5f:1.8f)+extra)return false;
            return kind=="slam"||V3.Dot(new V3(delta.x,0,delta.z).Normalized,new V3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw)))>=(kind=="launch"?.45f:.15f);
        }
        public static bool CanLand(NativeMeleeAction a,float time,bool grounded,float vy)=>a.Kind=="wolf-spin"&&a.Released&&a.Landed<0&&time>a.Release+.20f&&grounded&&vy<1;
        public static bool Pickup(NativeRecovery k,float time)=>k!=null&&k.Landed>=0&&!k.PickupUsed&&time<k.PickupUntil;
        public static bool Protected(NativeRecovery k,float time)=>k!=null&&k.Landed>=0&&time<k.Until&&!Pickup(k,time);
        public static void Land(NativeRecovery k,float time){k.Landed=time;k.PickupUntil=k.PickupUsed?time:time+.22f;k.RollAt=time+.4f;k.RollUntil=k.RollAt+.4f;k.Until=time+1;}
        public static V3 RecoveryMotion(NativeRecovery k,float time,V3 move,float yaw)
        {
            if(k==null||k.RollAt<0||time<k.RollAt||time>=k.RollUntil)return new V3();
            if(!k.HasDirection){k.Direction=new V3(move.x,0,move.z).Length>.1f?new V3(move.x,0,move.z).Normalized:V3.Forward(yaw)*-1;k.HasDirection=true;}
            return k.Direction*6;
        }
        public static string Phase(float landed,float pickup,float roll,float rollEnd,bool used,float time)
        {if(landed<0||time<landed)return "air";if(rollEnd>=0&&time>=rollEnd)return "rise";if(roll>=0&&time>=roll)return "roll";if(!used&&time<pickup)return "legs-up";return time<landed+.2f?"land":"flat";}
        public static bool Ceiling(V3 position,V3 delta,float radius,out float fraction,out V3 center)
        {
            fraction=0;center=new V3();float y=24-radius;
            if(delta.y<=0&&position.y<y)return false;fraction=position.y>=y?0:(y-position.y)/delta.y;
            if(!MathUtil.Finite(fraction)||fraction<0||fraction>1)return false;center=position+delta*fraction;
            if(Math.Abs(center.x)>32+radius||Math.Abs(center.z)>28+radius)return false;center.y=y-.015f;return true;
        }
        public static V3 Ballistic(ref V3 velocity,float gravity,float dt){var delta=velocity*dt+new V3(0,-.5f*gravity*dt*dt,0);velocity.y-=gravity*dt;return delta;}
    }
}

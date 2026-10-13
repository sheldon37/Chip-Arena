using System;
namespace ChipArena.UnityConversion
{
    // All positions are Rigidbody centres in native metres. Clients never resolve their own waypoints.
    [Serializable] public sealed class NativeKernelPanic
    {
        public bool WallSplat;
        public int Id,Caster,Victim,Seed,Next,Outcome; // 0 windup/lunge, 1 connect, 2 whiff, 3 interrupted, 4 complete
        public float At,ConnectedAt,Until,Ready,DamageDebt;
        public V3 Anchor,Facing,CasterStart,VictimStart,CasterExit,VictimExit;
        public V3[] CasterPoints=Array.Empty<V3>(),VictimPoints=Array.Empty<V3>();
        public bool Active=>Id>0&&Outcome==1;
        public bool Busy(float now)=>Id>0&&(Outcome==0||Outcome==1||Outcome==2&&now<Until);
        public NativeKernelPanic ViewCopy(){var c=(NativeKernelPanic)MemberwiseClone();c.CasterPoints=CasterPoints==null?null:(V3[])CasterPoints.Clone();c.VictimPoints=VictimPoints==null?null:(V3[])VictimPoints.Clone();return c;}
        public bool Valid()
        {
            bool F(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
            bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<10000;
            if(Id<=0||Caster<=0||Victim<0||Victim==Caster||Outcome<0||Outcome>4||Next<0||Next>5||!F(At)||!F(ConnectedAt)||!F(Until)||!F(Ready)||!F(DamageDebt)||DamageDebt<0||!V(Anchor)||!V(Facing)||Math.Abs(Facing.Length-1)>.01f||!V(CasterStart)||!V(VictimStart)||!V(CasterExit)||!V(VictimExit))return false;
            if(CasterPoints==null||VictimPoints==null||CasterPoints.Length!=VictimPoints.Length||CasterPoints.Length!=0&&CasterPoints.Length!=6)return false;
            if((Outcome==1||Outcome==4)&&(Victim<=0||CasterPoints.Length!=6||ConnectedAt<At||Until<ConnectedAt))return false;
            foreach(var p in CasterPoints)if(!V(p))return false;foreach(var p in VictimPoints)if(!V(p))return false;return true;
        }
    }
    public static class NativeKernelPanicRules
    {
        public const float Windup=.25f,CutIn=.6f,Duration=3.4f,RecoveryAt=2.6f,WhiffRecovery=.6f,Cooldown=20,Mana=60,Range=12,Radius=1.2f;
        public const float LungeSpeed=7f/.22f; // Sword Flash Step speed; 12 m takes about .38 s.
        public static readonly float[] Steps={0,.45f,.95f,1.35f,1.9f};
        public static readonly float[] Damage={12,10,10,6,25};
        public static float HitAt(NativeKernelPanic s,int step)=>s.ConnectedAt+CutIn+Steps[step];
        public static bool Due(NativeKernelPanic s,float now)=>s.Active&&s.Next<5&&now+1e-5f>=HitAt(s,s.Next);
        public static void Sample(NativeKernelPanic s,float now,out V3 caster,out V3 victim)
        {
            caster=s.CasterStart;victim=s.VictimStart;if(s.VictimPoints.Length!=6||now<s.ConnectedAt+CutIn)return;
            float age=now-s.ConnectedAt-CutIn;int i=0;while(i<4&&age>=Steps[i+1])i++;
            float end=i==4?RecoveryAt:Steps[i+1],u=MathUtil.Clamp((age-Steps[i])/(end-Steps[i]),0,1);u=u*u*(3-2*u);
            caster=s.CasterPoints[i];victim=s.VictimPoints[i]+(s.VictimPoints[i+1]-s.VictimPoints[i])*u;
            if(age>=RecoveryAt){caster=s.CasterPoints[5];victim=s.VictimPoints[5];}
        }
        // Called after ordinary damage mitigation, before saves and HP mutation. No damage is lost to the floor.
        public static float HoldLethal(NativeKernelPanic s,float hp,float damage,int step)
        {
            if(step==5){float total=damage+s.DamageDebt;s.DamageDebt=0;return total;}
            float paid=Math.Min(damage,Math.Max(0,hp-1));s.DamageDebt+=damage-paid;return paid;
        }
        public static bool AllowsDamage(NativeKernelPanic target,int targetId,NativeKernelPanic source,int sourceId,int step)
        {
            if(target?.Active==true)return step>0&&target.Victim==targetId&&target.Caster==sourceId&&ReferenceEquals(target,source);
            return true; // Existing outgoing chip/projectile effects keep their normal rules against outsiders.
        }
    }
}

using System;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeCyberKit
    {
        // Unity expands null inline records; retain their actual presence across network/replay JSON.
        public bool HasKit2,HasKit2R;
        public int Version;public CyberKit2MeleeRecord Kit2;public CyberKit2RangedRecord Kit2R;
        public NativeCyberRangedState Ranged;
        public string Kind="";public float At,Release,Until,CutAt=-1,LastComboEnd=-100;public int Combo=-1;public bool Resolved;
        public float PacketAt,PacketRelease,PacketUntil,PacketCutAt=-1;
        public float QReady,EReady,PacketReady,LinkReady,CrashReady;
        public float DashAt,DashUntil,TrailUntil,WallAt,WallUntil,WallYaw,LinkAt,LinkUntil,CrashAt,CrashUntil;
        public int LinkTarget;public V3 Facing,DashFrom,DashTo,WallPoint,CrashPoint;
        public void CapturePresence(){HasKit2=Kit2!=null;HasKit2R=Kit2R!=null;}
        public void RestorePresence(){if(!HasKit2)Kit2=null;if(!HasKit2R)Kit2R=null;}
        public NativeCyberKit ViewCopy(){var c=(NativeCyberKit)MemberwiseClone();c.Ranged=Ranged?.ViewCopy();c.Kit2=Kit2?.ViewCopy();c.Kit2R=Kit2R?.ViewCopy();return c;}
        public bool Valid()=>NativeCyberKit2Rules.Valid(this)&&(Ranged==null||Ranged.Valid())&&Finite(CutAt)&&Finite(PacketAt)&&Finite(PacketRelease)&&Finite(PacketUntil)&&Finite(PacketCutAt)&&Finite(At)&&Finite(Release)&&Finite(Until)&&Finite(LastComboEnd)&&Combo>=-1&&Combo<=(Version==2?4:2)&&Finite(QReady)&&Finite(EReady)&&Finite(PacketReady)&&Finite(LinkReady)&&Finite(CrashReady)&&Finite(DashAt)&&Finite(DashUntil)&&Finite(TrailUntil)&&Finite(WallAt)&&Finite(WallUntil)&&Finite(WallYaw)&&Finite(LinkAt)&&Finite(LinkUntil)&&Finite(CrashAt)&&Finite(CrashUntil)&&Vec(Facing)&&Vec(DashFrom)&&Vec(DashTo)&&Vec(WallPoint)&&Vec(CrashPoint)&&LinkTarget>=0&&(Kind==""||Kind=="cyber-jab"||Kind=="cyber-cross"||Kind=="cyber-palm"||Kind=="data-dash"||Kind=="firewall"||Kind=="packet-burst"||Kind=="siphon-link"||Kind=="system-crash"||Kind=="mode-switch"||Version==2&&(CyberKit2Spec.Slot(Kind)>=0||Kind=="cyber-spin"||Kind=="cyber-rise")||CyberRangedSpec.Slot(Kind)>=0);
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&Math.Abs(n)<1e8;
        static bool Vec(V3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
    }
    public static class NativeCyberRules
    {
        public const float DashCooldown=0;
        // Preserve the first cancellation time; repeated blocked/stunned ticks must not move it.
        public static void Interrupt(NativeCyberKit s,float time)
        {
            if(s.Version==2)NativeCyberKit2Rules.Interrupt(s,time);
            if(s.Kind!=""&&time>=s.At&&time<s.Until){s.CutAt=time;
                int slot=CyberRangedSpec.Slot(s.Kind);
                if(slot>=0&&s.Ranged!=null)
                {
                    if(time<s.Ranged.Release[slot]){if(slot==CyberRangedSpec.Field)s.Ranged.FieldLand=s.Ranged.FieldUntil=-100;else if(slot==CyberRangedSpec.Swap)s.Ranged.MineAt=-100;}
                    s.Ranged.Until[slot]=time;s.Ranged.Release[slot]=Math.Min(time,s.Ranged.Release[slot]);s.Release=Math.Min(time,s.Release);
                }
                s.Until=time;s.Resolved=true;
                if(s.Kind=="packet-burst"){s.PacketUntil=time;s.PacketCutAt=time;}
                if(s.Kind=="system-crash")s.CrashUntil=time;}
            if(time>=s.DashAt&&time<s.DashUntil)s.DashUntil=time;
            if(time>=s.LinkAt&&time<s.LinkUntil)s.LinkUntil=time;
        }
        public static void Predict(NativeCyberKit s,InputFrame input,float time,V3 position,ref float mana,float rate,float costScale,float cooldownScale,Func<V3,V3,V3> constrain,float ultimateCostScale=1)
        {
            if(s.Version==2){NativeCyberKit2Rules.Predict(s,input,time,position,ref mana,rate,costScale,cooldownScale,ultimateCostScale);return;}
            if(input.Block||input.Roll||input.CancelAbilities){Interrupt(s,time);return;}
            if(time<s.Until)return;
            if(input.SirenAbility==5){NativeCyberRangedRules.Switch(s,time,false);return;}
            bool dash=input.AbilityQ&&time>=s.QReady&&mana>=20*costScale,wall=input.AbilityE&&time>=s.EReady&&mana>=35*costScale;
            if(s.Ranged?.Ranged==true&&!dash&&!wall)
            {
                int slot=NativeCyberRangedRules.Slot(input);
                if(NativeCyberRangedRules.Begin(s,slot,time,ref mana,rate,slot==4?ultimateCostScale:costScale,cooldownScale))
                {
                    var r=s.Ranged;var from=position+new V3(0,.3f,0);var aim=GunRules.Direction(input.Yaw,input.Pitch);
                    if(slot==1){r.LanceFrom=from;r.LanceTo=from+aim*CyberRangedSpec.LanceRange;}
                    if(slot==4){r.RootFrom=from;r.RootTo=from+aim*CyberRangedSpec.RootRange;}
                    if(slot==2){r.FieldFrom=from;var delta=input.AbilityTarget-from;r.FieldPoint=from+delta.Normalized*Math.Min(delta.Length,CyberRangedSpec.FieldRange);}
                }
                return;
            }
            string kind="";float cost=0,duration=0,windup=0;
            if(dash){kind="data-dash";cost=20;duration=.2f;}
            else if(wall){kind="firewall";cost=35;duration=.2f;}
            else if(input.SirenAbility==1&&time>=s.PacketReady){kind="packet-burst";cost=15;duration=.25f;windup=.08f;}
            else if(input.SirenAbility==3&&time>=s.CrashReady){kind="system-crash";cost=40;duration=.8f;windup=.5f;}
            else if(input.Attack||input.Shoot){if(time>s.LastComboEnd+.6f)s.Combo=-1;s.Combo=(s.Combo+1)%3;kind=new[]{"cyber-jab","cyber-cross","cyber-palm"}[s.Combo];duration=ComboSeconds[s.Combo]/rate;windup=(s.Combo==2?.18f:.1f)/rate;s.LastComboEnd=time+duration;}
            if(kind==""||mana<cost*costScale)return;mana-=cost*costScale;s.Kind=kind;s.CutAt=-1;s.At=time;s.Release=time+windup;s.Until=time+duration;s.Facing=V3.Forward(input.Yaw);s.Resolved=false;
            if(kind=="data-dash"){s.QReady=time+DashCooldown*cooldownScale;s.DashAt=time;s.DashUntil=s.Until;s.DashFrom=position;s.DashTo=constrain(position,position+s.Facing*7);s.TrailUntil=s.Until+1.2f;}
            if(kind=="firewall"){s.EReady=time+12*cooldownScale;s.WallAt=time;s.WallUntil=time+4;s.WallYaw=input.Yaw;s.WallPoint=position-new V3(0,.865f,0)+s.Facing*.8f;}
            if(kind=="packet-burst"){s.PacketReady=time+4*cooldownScale;s.PacketAt=s.At;s.PacketRelease=s.Release;s.PacketUntil=s.Until;s.PacketCutAt=-1;}
            if(kind=="system-crash"){s.CrashReady=time+14*cooldownScale;s.CrashAt=time;s.CrashUntil=s.Until;s.CrashPoint=position;}
        }
        public static readonly float[] ComboDamage={12,14,22},ComboSeconds={.28f,.30f,.45f};
        public static bool Cone(V3 delta,V3 facing,float range,float arc)
        {if(delta.Length>range)return false;var flat=new V3(delta.x,0,delta.z);return flat.Length<.0001f||V3.Dot(flat.Normalized,facing.Normalized)>=(float)Math.Cos(arc*.5f);}
        public static float SegmentDistance(V3 point,V3 start,V3 end)
        {var d=end-start;float squared=V3.Dot(d,d);return(point-(start+d*(squared<1e-8?0:MathUtil.Clamp(V3.Dot(point-start,d)/squared,0,1)))).Length;}
        public static bool WallCross(V3 from,V3 to,V3 foot,float yaw,float radius,out float t)
        {
            var normal=V3.Forward(yaw);var side=new V3(normal.z,0,-normal.x);float a=V3.Dot(from-foot,normal),b=V3.Dot(to-foot,normal);t=0;
            if(Math.Abs(a)<=radius)t=0;else{float denominator=b-a;if(Math.Abs(denominator)<1e-7f)return false;t=((a>0?radius:-radius)-a)/denominator;if(t<0||t>1)return false;}
            var contact=from+(to-from)*t-foot;return Math.Abs(V3.Dot(contact,side))<=2.5f+radius&&contact.y>=-radius&&contact.y<=2.5f+radius;
        }
        public static V3 DashPosition(NativeCyberKit s,float time)=>s.DashFrom+(s.DashTo-s.DashFrom)*MathUtil.Clamp((time-s.DashAt)/.2f,0,1);
    }
}

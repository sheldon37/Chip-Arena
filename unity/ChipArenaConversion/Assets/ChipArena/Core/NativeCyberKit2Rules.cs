using System;
namespace ChipArena.UnityConversion
{
    public static class NativeCyberKit2Rules
    {
        public static void Ensure(NativeCyberKit s){s.Version=2;s.Kit2??=new CyberKit2MeleeRecord{Version=2};s.Kit2R??=new CyberKit2RangedRecord{Version=2};s.Ranged??=new NativeCyberRangedState();}
        public static int Slot(NativeCyberKit s,InputFrame i)=>i.AbilityQ?0:i.AbilityE?1:i.SirenAbility>=1&&i.SirenAbility<=4?(s.Ranged?.Ranged==true?7:2)+i.SirenAbility:i.Attack||i.Shoot?(s.Ranged?.Ranged==true?7:2):-1;
        public static int Current(NativeCyberKit s)=>s.Kind.StartsWith("cyber-",StringComparison.Ordinal)?2:CyberKit2Spec.Slot(s.Kind);
        public static float Move(NativeCyberKit s,float time)
        {
            if(s==null||time>=s.Until||s.Kind=="mode-switch")return 1;
            if(s.Version!=2)return 0;
            int slot=Current(s);return slot>=0?CyberKit2Spec.MoveScale[slot]:0;
        }
        public static bool Overclock(NativeCyberKit s,float time)=>s?.Version==2&&s.Kit2!=null&&time>=s.Kit2.OverclockAt&&time<s.Kit2.OverclockUntil;
        public static bool Begin(NativeCyberKit s,int slot,float time,ref float mana,float rate,float cost,float cooldown,float ultimateCost)
        {
            Ensure(s);if(slot<0||slot>=12||slot==6||time<s.Until)return false;
            var m=s.Kit2;var r=s.Kit2R;int n=slot-7;float ready=n>=0?r.Ready[n]:m.Ready[slot];
            float price=CyberKit2Spec.Costs[slot]*(slot==11?ultimateCost:cost);if(time<ready||mana<price)return false;
            rate=Math.Max(.01f,rate)*(Overclock(s,time)?1+CyberKit2Spec.OverclockRate:1);float duration=CyberKit2Spec.Seconds[slot],windup=CyberKit2Spec.Windup[slot];
            if(slot==2){s.Combo=time>s.LastComboEnd+CyberKit2Spec.ComboReset?0:(s.Combo+1)%5;m.Combo=s.Combo;duration=CyberKit2Spec.ComboSeconds[s.Combo]/rate;windup=CyberKit2Spec.ComboWindups[s.Combo]/rate;}
            if(slot==7){r.PlasmaShot=time>r.Until[0]+CyberKit2Spec.ComboReset?0:r.PlasmaShot+1;duration/=rate;windup/=rate;}
            mana-=price;s.Kind=slot==2?CyberKit2Spec.ComboKinds[s.Combo]:CyberKit2Spec.Ids[slot];s.At=time;s.Release=time+windup;s.Until=time+duration;s.CutAt=-1;s.Resolved=false;
            if(slot==2)s.LastComboEnd=s.Until;
            if(slot==1){m.OverclockAt=s.Release;m.OverclockUntil=s.Release+CyberKit2Spec.OverclockSeconds;}
            float next=time+(slot==2?duration:slot==7?duration:CyberKit2Spec.Cooldowns[slot]*(slot==11?1:cooldown));
            if(n>=0){r.At[n]=time;r.Release[n]=s.Release;r.Until[n]=s.Until;r.Ready[n]=next;r.CutAt[n]=-100;}
            else{m.At[slot]=time;m.Release[slot]=s.Release;m.Until[slot]=s.Until;m.Ready[slot]=next;m.CutAt[slot]=-100;}
            return true;
        }
        public static void Recover(NativeCyberKit s,float time,float dt)
        {
            if(!Overclock(s,time))return;float extra=dt*(CyberKit2Spec.OverclockCooldownRate-1);
            for(int n=0;n<12;n++)if(s.Kit2.Ready[n]>time)s.Kit2.Ready[n]=Math.Max(time,s.Kit2.Ready[n]-extra);
            for(int n=1;n<5;n++)if(s.Kit2R.Ready[n]>time)s.Kit2R.Ready[n]=Math.Max(time,s.Kit2R.Ready[n]-extra);
        }
        public static void Predict(NativeCyberKit s,InputFrame i,float t,V3 pos,ref float mana,float rate,float cost,float cooldown,float ultimateCost)
        {
            if(i.Block||i.Roll||i.CancelAbilities){NativeCyberRules.Interrupt(s,t);return;}
            if(i.SirenAbility==5){NativeCyberRangedRules.Switch(s,t,false);return;}
            if(Begin(s,Slot(s,i),t,ref mana,rate,cost,cooldown,ultimateCost))s.Facing=V3.Forward(i.Yaw);
        }
        public static void Interrupt(NativeCyberKit s,float t)
        {
            if(s.Kit2==null||s.Kit2R==null)return;
            int slot=Current(s);if(slot<0||t<s.At||t>=s.Until)return;
            if(slot==1&&!s.Resolved)s.Kit2.OverclockAt=s.Kit2.OverclockUntil=-100;
            if(slot>=7){int n=slot-7;s.Kit2R.CutAt[n]=t;s.Kit2R.Until[n]=t;s.Kit2R.Release[n]=Math.Min(t,s.Kit2R.Release[n]);if(!s.Resolved&&slot==9)s.Kit2R.OrbLand=s.Kit2R.Implode=-100;}
            else{s.Kit2.CutAt[slot]=t;s.Kit2.Until[slot]=t;s.Kit2.Release[slot]=Math.Min(t,s.Kit2.Release[slot]);}
        }
        public static bool Valid(NativeCyberKit s)
        {
            if(s.Version==0)return s.Kit2==null&&s.Kit2R==null;
            if(s.Version!=2||s.Kit2==null||s.Kit2R==null||s.Kit2.Version!=2||s.Kit2R.Version!=2||!s.Kit2R.Valid())return false;
            var m=s.Kit2;bool F(float x)=>MathUtil.Finite(x)&&Math.Abs(x)<1e8;bool P(V3 p)=>F(p.x)&&F(p.y)&&F(p.z)&&Math.Abs(p.x)<1e4&&Math.Abs(p.y)<1e4&&Math.Abs(p.z)<1e4;
            foreach(var a in new[]{m.At,m.Release,m.Until,m.Ready,m.CutAt}){if(a==null||a.Length!=12)return false;foreach(float x in a)if(!F(x))return false;}
            for(int i=0;i<12;i++)if(m.Release[i]<m.At[i]||m.Until[i]<m.Release[i])return false;
            if(m.Combo< -1||m.Combo>4||m.UplinkTarget<0||m.UplinkOutcome<0||m.UplinkOutcome>3||m.MarkedCount<0||m.MarkedCount>8||m.Marked==null||m.Marked.Length!=8)return false;
            if(m.ClonePos?.Length!=3||m.CloneYaw?.Length!=3||m.CloneStrikeAt?.Length!=3||m.CloneBurstAt?.Length!=3||m.CloneTarget?.Length!=3||m.CloneBurst?.Length!=3)return false;
            for(int i=0;i<3;i++)if(!P(m.ClonePos[i])||!P(m.CloneBurst[i])||!F(m.CloneYaw[i])||!F(m.CloneStrikeAt[i])||!F(m.CloneBurstAt[i])||m.CloneTarget[i]<0)return false;
            return P(m.UplinkFrom)&&P(m.UplinkAnchor)&&P(m.CutFrom)&&P(m.CutTo)&&P(m.MeteorFrom)&&P(m.MeteorTarget)&&F(m.UplinkLatchAt)&&F(m.UplinkArriveAt)&&F(m.OverclockAt)&&F(m.OverclockUntil)&&F(m.ForkAt)&&F(m.ForkUntil)&&F(m.DetonateAt)&&F(m.LandAt);
        }
    }
}

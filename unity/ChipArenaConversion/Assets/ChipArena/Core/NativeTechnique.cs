using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A weapon's technique, an optional Q/E replacement (owner 2026-11-03, docs/WEAPON-TECHNIQUES.md). The contract between the rules
    /// (Astra's) and the presentation: everything the pose, the effects, the sound and the HUD read. Authority-owned in play; <see cref="NativeTechniquePreview"/> supplies optional Dev Sandbox previews. Times are on the presentation clock.
    /// </summary>
    [Serializable] public sealed class NativeTechnique
    {
        /// <summary>The technique's id and its phase (docs/WEAPON-TECHNIQUES.md).</summary>
        public string Kind,Phase;
        /// <summary>The press; when the move leaves the pilot; the effect's end; the pilot's own lock's end.</summary>
        public float At=-100,Release,Until,RecoverUntil;
        /// <summary>The triggered moment and the recast (-100: not yet); a time of the kind's own (armed, stuck, burst), or a share.</summary>
        public float HitAt=-100,RecastAt=-100,Arm;
        public V3 From,Point,To,Dir;
        public int Target;
        public float End=>Math.Max(Until,RecoverUntil);
        public bool Hit(float time)=>HitAt>-50&&time>=HitAt;
        public bool Recast(float time)=>RecastAt>-50&&time>=RecastAt;
        public NativeTechnique ViewCopy()=>(NativeTechnique)MemberwiseClone();
        private static readonly string[] Phases=new[]{"windup","active","guard","counter","recovery","throw","cloud","plant","wall","recall","flight","stuck","leap","return","flick","air","drop","armed","fire","reel","draw","burst","crouch","cast","pool","set","bolt","swap"};
        public bool Valid()
        {
            bool F(float v)=>MathUtil.Finite(v)&&Math.Abs(v)<10000000;bool V(V3 v)=>F(v.x)&&F(v.y)&&F(v.z)&&v.Length<100000;bool S(string v)=>v==null||v.Length<=32;
            return (Kind==null||NativeTechniques.Find(Kind)!=null)&&(Phase==null||Array.IndexOf(Phases,Phase)>=0)&&Target>=0&&Target<1000000&&S(Kind)&&S(Phase)&&F(At)&&F(Release)&&F(Until)&&F(RecoverUntil)&&F(HitAt)&&F(RecastAt)&&F(Arm)&&V(From)&&V(Point)&&V(To)&&V(Dir)&&(Kind==null||Release>=At&&Until>=Release&&Until-At<=60&&RecoverUntil-At<=60);
        }
    }
    /// <summary>Where the presentation reads an actor's technique: the snapshot's, once the rules replicate it, or the preview's.</summary>
    public interface ITechniqueSource{NativeTechnique Of(int actor);}

    /// <summary>The twelve techniques, a weapon each (docs/WEAPON-TECHNIQUES.md: the implemented base numbers).</summary>
    public static class NativeTechniques
    {
        public sealed class Def{public string Id,Spec,Name,Icon;public float Cooldown,Cost;public int Accent;}
        public const string Riposte="riposte",SmokeVeil="smoke-veil",Bastion="bastion",AnchorThrow="anchor-throw",RicochetCoin="ricochet-coin",SentryDrop="sentry-drop",GrappleLine="grapple-line",GaleArrow="gale-arrow",IgnitionLeap="ignition-leap",HealingSpring="healing-spring",StaticRecall="static-recall",ShadeSwap="shade-swap";
        private static Def D(string id,string spec,string name,string icon,float cooldown,float cost,int accent)=>new Def{Id=id,Spec=spec,Name=name,Icon=icon,Cooldown=cooldown,Cost=cost,Accent=accent};
        public static readonly Def[] All=
        {
            D(Riposte,"sword","Riposte","shield-check",12,20,0x8fe8ff),D(SmokeVeil,"daggers","Smoke Veil","cloud",16,25,0xb48cff),D(Bastion,"shield","Bastion","shield",14,25,0x4fa8ff),D(AnchorThrow,"greatsword","Anchor Throw","anchor",14,30,0xffa23a),
            D(RicochetCoin,"pistol","Ricochet Coin","coins",9,0,0xffcf5a),D(SentryDrop,"machinegun","Sentry Drop","crosshair",20,0,0xff5a4a),D(GrappleLine,"rifle","Grapple Line","spline",12,0,0x4fe6ff),D(GaleArrow,"bow","Gale Arrow","wind",10,0,0xbff5e4),
            D(IgnitionLeap,"fire","Ignition Leap","flame",11,22,0xff7a22),D(HealingSpring,"water","Healing Spring","droplets",18,30,0x4ff0c8),D(StaticRecall,"lightning","Static Recall","zap",13,20,0x9ec4ff),D(ShadeSwap,"darkness","Shade Swap","repeat",15,24,0xa050ff),
        };
        /// <summary>The weapon's technique (null: a DEV class, or none).</summary>
        public static Def For(string spec){foreach(var d in All)if(d.Spec==spec)return d;return null;}
        public static Def Find(string id){foreach(var d in All)if(d.Id==id)return d;return null;}
        /// <summary>Who holds the records (null: no active snapshot). Set from the rendered snapshot; the sandbox preview may override it.</summary>
        public static ITechniqueSource Source;
        public static NativeTechnique Of(Actor a)=>a==null||Source==null?null:Source.Of(a.Id);
        /// <summary>The weapon out of the pilot's hand this frame: the planted shield, the thrown greatsword until it is caught.</summary>
        public static bool WeaponAway(NativeTechnique k,float time)=>k?.Kind!=null&&time>=k.Release&&time<k.Until&&(k.Kind==Bastion||k.Kind==AnchorThrow);
        /// <summary>The coin in the air at `time`.</summary>
        public static Vector3 CoinAt(NativeTechnique k,float time)
        {
            float t=Math.Max(0,time-k.Release);return new Vector3(k.From.x+k.Dir.x*CoinDrift*t,k.From.y+CoinLift*t-.5f*CoinFall*t*t,k.From.z+k.Dir.z*CoinDrift*t);
        }
        public const float CoinDrift=2.2f,CoinLift=6.1f,CoinFall=9.8f,CoinAir=1.4f;
        /// <summary>The thrown greatsword's place at `time`, and how far through its flight or its return (0..1).</summary>
        public static Vector3 BladeAt(NativeTechnique k,float time,Vector3 catcher,out float flight)
        {
            var from=new Vector3(k.From.x,k.From.y,k.From.z);var to=new Vector3(k.Point.x,k.Point.y,k.Point.z);
            if(time<k.Arm){flight=Math.Max(0,Math.Min(1,(time-k.Release)/Math.Max(.05f,k.Arm-k.Release)));return Vector3.Lerp(from,to,flight);}
            if(k.Recast(time)){flight=1;return to;}
            float back=(time-(k.Until-BladeReturn))/BladeReturn;if(back<=0){flight=1;return to;}
            flight=Math.Min(1,back);return Vector3.Lerp(to,catcher,flight*flight);
        }
        public const float BladeReturn=.32f;
    }

    /// <summary>
    /// The Dev Sandbox's "Preview Techniques": the local pilot's weapon's technique on a loop, each cast written as the proposed host would
    /// write the record (docs/WEAPON-TECHNIQUES.md), so the pose, the effects, the sound and the HUD all show it. It reaches nothing else: not
    /// the authority, the actor, the network frame, damage or movement, so a move that travels draws only its effects here.
    /// </summary>
    public sealed class NativeTechniquePreview:ITechniqueSource
    {
        private readonly Dictionary<int,NativeTechnique> records=new Dictionary<int,NativeTechnique>();private float next=-100,clock;private bool odd;
        public NativeTechnique Of(int actor)=>records.TryGetValue(actor,out var r)?r:null;
        public void Clear(){records.Clear();next=-100;}
        public bool Any=>records.Count>0;
        /// <summary>Seconds a kind's loop takes, its effect with a breath after.</summary>
        public static float Period(string id)
        {
            switch(id)
            {
                case NativeTechniques.Riposte:return 2.6f;case NativeTechniques.SmokeVeil:return 6.4f;case NativeTechniques.Bastion:return 6.2f;case NativeTechniques.AnchorThrow:return 4.4f;
                case NativeTechniques.RicochetCoin:return 3.4f;case NativeTechniques.SentryDrop:return 9.2f;case NativeTechniques.GrappleLine:return 3.2f;case NativeTechniques.GaleArrow:return 3.2f;
                case NativeTechniques.IgnitionLeap:return 3.6f;case NativeTechniques.HealingSpring:return 7.2f;case NativeTechniques.StaticRecall:return 4.2f;default:return 3.4f;
            }
        }
        /// <summary>Advances the loop for the pilot at `feet` facing `yaw` (radians).</summary>
        public void Step(Actor a,Vector3 feet,float yaw,float time)
        {
            var d=NativeTechniques.For(a?.SourceSpec);if(d==null){records.Clear();next=-100;return;}
            if(time<clock-.25f)next=-100;clock=time;
            if(next<-50)next=(float)Math.Ceiling(time+.3f);
            if(!records.TryGetValue(a.Id,out var k)){k=new NativeTechnique();records[a.Id]=k;}
            if(time>=next){odd=!odd;Write(k,d.Id,feet,yaw,time,odd);next=time+Period(d.Id);}
            k.Phase=Phase(k,time);
        }
        /// <summary>Fills the record as the proposed host would for a cast at `t0`. `first`: the sword's parry lands, the other loop it does not.</summary>
        public static void Write(NativeTechnique k,string id,Vector3 feet,float yaw,float t0,bool first)
        {
            var f=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));var r=new Vector3(f.Z,0,-f.X);var up=Vector3.UnitY;Vector3 hand=feet+up*1.25f+f*.35f+r*.1f;
            k.Kind=id;k.At=t0;k.HitAt=-100;k.RecastAt=-100;k.Arm=0;k.Target=0;k.Dir=V(f);k.From=V(hand);k.Point=V(feet);k.To=V(feet);k.Release=t0+.1f;k.Until=t0+1;k.RecoverUntil=0;
            switch(id)
            {
                case NativeTechniques.Riposte:k.Release=t0+.06f;k.Until=t0+.5f;k.Point=V(feet+up*1.25f+f*.55f);k.Dir=V(-f);if(first){k.HitAt=t0+.26f;k.RecoverUntil=k.HitAt+.45f;}else k.RecoverUntil=k.Until+.45f;break;
                case NativeTechniques.SmokeVeil:k.Release=t0+.24f;k.Until=k.Release+4;k.Point=V(feet+f*.6f);k.RecoverUntil=k.Release+.18f;break;
                case NativeTechniques.Bastion:k.Release=t0+.42f;k.Until=k.Release+4;k.Point=V(feet+f*1.3f);k.Arm=1;k.HitAt=k.Release+1.5f;k.To=V(feet+f*1.3f+up*1.05f+r*.55f);k.RecoverUntil=k.Release+.25f;break;
                case NativeTechniques.AnchorThrow:k.Release=t0+.32f;{var to=feet+f*4.2f+r*1.6f;k.Point=V(to);k.Dir=V(Vector3.Normalize(to-feet));}k.Arm=k.Release+4.4f/26f;if(first){k.RecastAt=k.Arm+1.1f;k.Until=k.RecastAt+.45f;}else k.Until=k.Arm+1.4f;k.RecoverUntil=k.Release+.3f;break;
                case NativeTechniques.RicochetCoin:k.Release=t0+.16f;k.HitAt=k.Release+.56f;k.Until=k.Release+NativeTechniques.CoinAir;k.Point=V(NativeTechniques.CoinAt(k,k.HitAt));k.To=V(feet+f*9+r*5+up*1.15f);k.RecoverUntil=k.Release+.2f;break;
                case NativeTechniques.SentryDrop:k.Release=t0+.34f;k.Point=V(feet+f*1.5f+r*1);k.Arm=k.Release+.6f;k.Until=k.Arm+6;k.To=V(feet+f*12+r*2+up*1.1f);k.RecoverUntil=k.Release+.25f;break;
                case NativeTechniques.GrappleLine:k.Release=t0+.14f;k.Point=V(feet+f*14+up*5);k.Arm=k.Release+.24f;k.Until=k.Arm+.95f;k.RecoverUntil=k.Until;break;
                case NativeTechniques.GaleArrow:k.Release=t0+.34f;k.From=V(feet+up*1.35f+f*.6f);k.Point=V(feet+f*6.5f);k.Arm=k.Release+.26f;k.Until=k.Arm+.9f;k.RecoverUntil=k.Release+.3f;break;
                case NativeTechniques.IgnitionLeap:k.Release=t0+.16f;k.Until=k.Release+1.35f;k.RecoverUntil=k.Release+.5f;break;
                case NativeTechniques.HealingSpring:k.Release=t0+.38f;k.Point=V(feet+f*3.5f);k.Until=k.Release+5;k.RecoverUntil=k.Release+.3f;break;
                case NativeTechniques.StaticRecall:k.Release=t0+.12f;k.Point=V(feet+f*5.5f+r*2.4f);k.RecastAt=t0+2.5f;k.From=V(feet);k.Until=k.RecastAt+.3f;k.RecoverUntil=k.Release+.2f;break;
                case NativeTechniques.ShadeSwap:k.Release=t0+.22f;k.Point=V(feet+f*11);k.HitAt=k.Release+.5f;k.Until=k.HitAt+.28f;k.From=V(feet+up*1.2f+f*.5f);k.RecoverUntil=k.Until;break;
            }
        }
        private static V3 V(Vector3 v)=>new V3(v.X,v.Y,v.Z);
        private static string Phase(NativeTechnique k,float t)
        {
            if(t<k.Release)return "windup";return t<k.Until?"active":"recovery";
        }
    }
}

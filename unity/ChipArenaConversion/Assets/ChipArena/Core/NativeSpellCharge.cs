using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The right-hand charge socket this frame: shown, its uniform scale and rotation, and the two halos' rotations.</summary>
    public struct SpellChargePose
    {
        public bool Visible;public float Scale;public Quaternion Rotation,Halo0,Halo1;
    }
    /// <summary>
    /// client/ranged-vfx.ts spellCharge at 05950ea: a weapon-coloured orb with two counter-rotating halos in the right hand. On a
    /// magic-class pilot it grows with the cast's progress, flashes for 60 ms at the authoritative release and is gone by
    /// release + .08 s; the Ansatsuken shows it while charging a Hadoken, sized by the charge. Both rigs show it (bodyModifiers
    /// runs on every pilot rig and the first-person rig). Joint-frame numbers are the source's (+x the rig's left).
    /// </summary>
    public static class NativeSpellCharge
    {
        /// <summary>The 'spell-charge' socket in the right-hand joint, the orb's radius and the halos' scale (halo.scale.z=.5).</summary>
        public static readonly Vector3 Socket=new Vector3(0,-.09f,.02f),HaloScale=new Vector3(.15f,.15f,.5f);
        public const float OrbScale=.1f,ChargeSeconds=1.4f;
        private static float Smooth01(float x){x=Math.Min(1,Math.Max(0,x));return x*x*(3-2*x);}
        /// <summary>content.ts castWindup: the primary's release after lastAttack (attackRate already clamped by the stats).</summary>
        public static float CastWindup(int school,float rate)=>(school==MagicPose.Leet?.18f:school==1?.16f:school==2?.12f:school==3?.065f:school==4?.22f:0)/rate;
        /// <summary>
        /// The phrase spellCharge reads: a Q/E phrase newer than the primary (ranged-motion.ts abilityPhrase: Actor.AbilityAt
        /// within 3 s, its release AbilityRelease, as NativeAbilities stamps them), else character-animation.ts spellPose on the
        /// primary (Actor.AttackAt, live until release + .07/rate hold + .26/rate recovery). `age` and `release` are seconds.
        /// </summary>
        public static bool Phrase(Actor a,float time,out float age,out float release)
        {
            int school=MagicPose.School(a);age=0;release=0;if(school==0)return false;
            float rate=a.CastRate>0?a.CastRate:1;
            if(!string.IsNullOrEmpty(a.AbilityId)&&a.AbilityAt>a.AttackAt){float ab=time-a.AbilityAt;if(ab>=0&&ab<=3){age=ab;release=a.AbilityRelease-a.AbilityAt;return true;}}
            age=Math.Max(0,time-a.AttackAt);release=CastWindup(school,rate);return age<release+.07f/rate+.26f/rate;
        }
        /// <summary>spellPose(p,time).weight, the cast layer weight the hand motes test (coil in, hold, settle out).</summary>
        public static float CastWeight(Actor a,float time)
        {
            int school=MagicPose.School(a);if(school==0)return 0;float rate=a.CastRate>0?a.CastRate:1;
            float age=Math.Max(0,time-a.AttackAt),release=CastWindup(school,rate),windup=release*.6f,hold=.07f/rate,recover=.26f/rate;
            if(age>=release+hold+recover)return 0;if(age<windup)return Smooth01(age/Math.Max(.01f,windup));
            return age>release+hold?1-Smooth01((age-release-hold)/recover):1;
        }
        /// <summary>
        /// ranged-vfx.ts:19-31 for one frame. `charging`/`charge`: the Ansatsuken's replicated Hadoken charge (arcade action
        /// 'charge'; charge 0..1). Halo spin and the Ansatsuken's turn are on the arena clock `time`.
        /// </summary>
        public static SpellChargePose Sample(Actor a,float time,bool charging=false,float charge=0)
        {
            var o=new SpellChargePose{Rotation=Quaternion.Identity,Halo0=Quaternion.Identity,Halo1=CapturedRig.Euler((float)Math.PI/2),Scale=1};
            if(a==null)return o;
            if(a.SourceSpec=="ansatsuken"){o.Visible=a.Alive&&charging;o.Rotation=CapturedRig.Euler(0,time*8);o.Scale=.5f+charge*1.2f;return o;}
            if(!Phrase(a,time,out float age,out float release)||!a.Alive||!(age<release+.08f)){o.Visible=false;return o;}
            float progress=Math.Min(1,age/Math.Max(.01f,release)),grow=(.35f+.65f*progress)*Smooth01(age/.05f);
            float flash=age>=release?1+.35f*(1-Math.Min(1,(age-release)/.06f)):1,fade=age>release+.06f?1-(age-release-.06f)/.02f:1;
            o.Visible=true;o.Scale=Math.Max(.01f,grow*flash*Math.Max(0,fade));
            o.Halo0=CapturedRig.Euler(0,9*time);o.Halo1=CapturedRig.Euler((float)Math.PI/2,-7*time);return o;
        }
        /// <summary>
        /// The Ansatsuken's arcade charge from the replicated fighter action (arcade.ts:82: clamp((time - at)/chargeSeconds,
        /// 0, 1), started when the action became 'charge').
        /// </summary>
        public static bool Charging(Actor a,float time,out float charge)
        {
            charge=0;if(a==null||a.SourceSpec!="ansatsuken"||a.SourceAction!="charge")return false;
            charge=Math.Min(1,Math.Max(0,(time-a.SourceAt)/ChargeSeconds));return true;
        }
    }
    /// <summary>One hand mote: its position in the right-hand joint (source frame), the world-space jitter added to every axis,
    /// and the burst it spawns.</summary>
    public struct SpellMote
    {
        public Vector3 Local;public float Jitter,Power;public int Color,Count;
    }
    /// <summary>
    /// client/ranged-vfx.ts spellMotes: while a magic-class pilot casts (spellPose weight above .3, or inside a Q/E windup) its
    /// right hand emits element motes at a fixed rate (lightning 30/s, water 18/s, others 22/s), positions hashed from the
    /// emission index and the cast time (no randomness). The clock restarts whenever the pilot stops casting. One instance per
    /// pilot rig; Step writes into a caller buffer and allocates nothing.
    /// </summary>
    public sealed class NativeSpellMotes
    {
        private double clock;private int index;private bool live;
        public void Reset(){clock=0;index=0;live=false;}
        private static double Hash(double x){double v=Math.Sin(x*91.37+17.1)*43758.5453;return v-Math.Floor(v);}
        /// <summary>
        /// One renderer frame of `dt` seconds (clamped to [0, .05] as the source does). `quality` is effectsQuality (0 turns the
        /// motes off). Returns how many motes were written to `o`.
        /// </summary>
        /// <summary>Whether the hand emits: a live magic-class pilot past .3 cast weight, or inside a Q/E windup.</summary>
        public static bool Casting(Actor a,float time)
        {
            if(MagicPose.School(a)==0||!a.Alive)return false;
            if(!string.IsNullOrEmpty(a.AbilityId)){float ab=time-a.AbilityAt;if(ab>=0&&ab<=3&&ab<a.AbilityRelease-a.AbilityAt)return true;}
            return NativeSpellCharge.CastWeight(a,time)>.3f;
        }
        public int Step(Actor a,float time,double dt,float quality,int color,SpellMote[] o)
        {
            int school=MagicPose.School(a);
            if(!Casting(a,time)||!(quality>0)){Reset();return 0;}
            if(!live){clock=0;index=0;live=true;}
            bool lightning=school==3,water=school==2,fire=school==1,darkness=school==4;
            double step=1.0/(lightning?30:water?18:22);clock+=Math.Min(Math.Max(dt,0),.05);int n=0;
            while(clock>=step)
            {
                clock-=step;int k=++index;double h=Hash(k+(double)a.AttackAt),h2=Hash(k*1.7+3.1);
                double angle=k*(water?.9:darkness?-1.1:2.399),radius=darkness?.14*(1-(k%6)/6.0):water?.09:.05*h;
                if(o==null||n>=o.Length)continue;
                o[n++]=new SpellMote{Local=new Vector3((float)(Math.Cos(angle)*radius),(float)(-.1+(fire?.04*h2:0)),(float)(.05+Math.Sin(angle)*radius)),Jitter=lightning?(float)((h-.5)*.06):0,
                    Color=color,Count=lightning?1:2,Power=fire?.7f:darkness?.2f:.45f};
            }
            return n;
        }
    }
}

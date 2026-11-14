using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Kit v2's melee and shared poses (docs/CYBER-LORD.md "Kit v2"), authored pose to pose like the hardlight string
    /// (Core/CyberLordCombo.cs, the same channels, curves, rows and feet) and placed on the replicated windows of the v2 record
    /// (CyberLordView.Kit2): Q Uplink's cable shot, the zip (pulled along the cable, legs tucked) and its landing, the yank;
    /// E Overclock's power-up; 1 Fork Bomb's hand sign flung open; 2 Glitch Cut's low dash-slash and the "already cut" hold
    /// until the marks detonate; 3 Meteor Byte's crouch and leap, the hover with the fist raised and the other hand aiming, the
    /// dive and the ground punch, and the rise. The casts that let the pilot move (Q's shot, E, 1) pose the upper body over the
    /// gait's legs; the others stand on planted feet (CyberComboFeet) or author the legs in the air (Air). Pure.
    /// </summary>
    public static class CyberKit2MeleePoses
    {
        public const int UplinkShot=CyberCombo.Kinds,UplinkZip=UplinkShot+1,UplinkYank=UplinkShot+2,UplinkLand=UplinkShot+3,Overclock=UplinkShot+4,ForkCast=UplinkShot+5,
            GlitchCut=UplinkShot+6,GlitchHold=UplinkShot+7,MeteorLeap=UplinkShot+8,MeteorHover=UplinkShot+9,MeteorDive=UplinkShot+10,MeteorLand=UplinkShot+11,End=UplinkShot+12;
        public static readonly string[] States={"cyber-uplink","cyber-uplink-zip","cyber-uplink-yank","cyber-uplink-land","cyber-overclock","cyber-fork-bomb",
            "cyber-glitch-cut","cyber-glitch-hold","cyber-meteor-leap","cyber-meteor-hover","cyber-meteor-dive","cyber-meteor-land"};
        public static string State(int kind)=>kind>=UplinkShot&&kind<End?States[kind-UplinkShot]:null;
        /// <summary>Kinds that stand on planted feet.</summary>
        public static bool Plants(int kind)=>kind==UplinkYank||kind==UplinkLand||kind==GlitchHold||kind==MeteorLeap||kind==MeteorDive||kind==MeteorLand;
        private static readonly CyberCombo.Key[][] keys=Build();
        private static readonly CyberCombo.FootMove[] none=new CyberCombo.FootMove[0];
        private static readonly CyberCombo.FootMove[][] feet=
        {
            none,none,
            // the yank: the rear foot braces back on its ball as the cable is hauled in
            new[]{CyberCombo.MakePivot(CyberCombo.RearFoot,0,0,1,.7f,.2f,.35f)},
            none,none,none,none,none,none,none,none,none,
        };
        public static CyberCombo.Key[] Keys(int kind)=>keys[Math.Max(0,Math.Min(keys.Length-1,kind-UplinkShot))];
        public static CyberCombo.FootMove[] Feet(int kind)=>feet[Math.Max(0,Math.Min(feet.Length-1,kind-UplinkShot))];
        private static CyberCombo.Key[][] Build()
        {
            var k=new CyberCombo.Key[End-UplinkShot][];var g=CyberCombo.Guard();
            const byte S=CyberCombo.Stop,I=CyberCombo.Impact;
            // Q Uplink: the rear fist thrusts the hardlight cable out along the aim, the right shoulder driving, and holds it taut.
            var shot=new CyberCombo.B(g).Hips(.15f,.06f,0).Spine(.06f,.14f,0).Chest(.18f,.12f,0).Head(-.35f,-.32f).Right(-.05f,.38f,.84f).RFist(0,.12f,1,0,-1,0)
                .Left(.12f,.30f,.26f).Poles(.35f,-1,-.1f,-.7f,-.6f,0).Reach(0,.35f).Body(-.02f,0,.03f);
            k[UplinkShot-UplinkShot]=new[]{g.At(0,0,S),shot.At(0,1,I),new CyberCombo.B(shot).Right(-.05f,.37f,.82f).At(1,.3f,S),new CyberCombo.B(shot).Right(-.05f,.37f,.82f).At(1,1,S)};
            // The zip: hauled along the cable at 30 m/s, leaning hard into it, the cable fist leading, the other arm swept back,
            // the knees tucked and the rear leg trailing.
            var zip=new CyberCombo.B(shot).Lean(.25f,.30f,.20f).Head(-.25f,-.62f).Right(-.05f,.42f,.86f).Left(.24f,-.12f,-.26f).LFist(0,-.6f,-1,0,-1,0).Poles(.4f,0,-1,-.7f,-.6f,0)
                .Legs(.9f,-.3f,1.6f,1.2f);
            var zip2=new CyberCombo.B(zip).Legs(.65f,-.5f,1.35f,.9f).Left(.26f,-.16f,-.3f);
            k[UplinkZip-UplinkShot]=new[]{shot.At(0,0,S),zip.At(0,1,I),zip2.At(1,.5f),zip2.At(1,1,S)};
            // The landing: the knees soak it, the cable fist drawn back to strike, then the guard.
            var land=new CyberCombo.B(g).Lean(.15f,.25f,.15f).Body(-.13f,0,.04f).Right(-.13f,.22f,.42f).RFist(0,.5f,1,.8f,-.5f,0).Head(.42f,-.4f).Ground();
            k[UplinkLand-UplinkShot]=new[]{new CyberCombo.B(zip2).Ground().At(0,0,S),land.At(0,1,I),g.At(1,1,S)};
            // The yank: the cable hauled in, the fist ripped back to the hip and the torso twisting away, leaning back on the
            // braced rear foot; the lead fist cocked for the pilot arriving in front.
            var yank=new CyberCombo.B(g).Hips(-.5f,-.04f,0).Spine(-.12f,-.06f,0).Chest(-.3f,-.02f,0).Head(.85f,-.1f).Body(-.08f,-.01f,-.04f)
                .Right(-.24f,.02f,-.04f).RFist(-.3f,-.5f,-.8f,0,-1,0).Poles(.35f,-1,-.1f,-.4f,-.2f,-1).Left(.15f,.31f,.42f);
            var cock=new CyberCombo.B(g).Hips(-.4f,.06f,0).Chest(-.18f,.14f,0).Head(.6f,-.3f).Body(-.05f,0,0).Left(.15f,.30f,.34f).Right(-.12f,.25f,.22f);
            k[UplinkYank-UplinkShot]=new[]{shot.At(0,0,S),yank.At(0,1,I),cock.At(1,.6f),g.At(1,1,S)};
            // E Overclock: the fists drawn together at the chest as the body gathers, then flung down and out, chest up, head back.
            var gather=new CyberCombo.B(g).Hips(0,.1f,0).Spine(0,.22f,0).Chest(0,.18f,0).Head(0,-.25f).Body(-.1f,0,0).Left(.07f,.10f,.24f).LFist(-1,0,.2f,0,-1,0).Right(-.07f,.10f,.24f).RFist(1,0,.2f,0,-1,0).Poles(.8f,-.3f,0,-.8f,-.3f,0);
            var flex=new CyberCombo.B(g).Hips(0,0,0).Spine(0,-.12f,0).Chest(0,-.12f,0).Head(0,-.25f).Body(.02f,0,0).Left(.44f,-.16f,.12f).LFist(.35f,-1,0,0,0,1).Right(-.44f,-.16f,.12f).RFist(-.35f,-1,0,0,0,1).Poles(.6f,-.6f,-.4f,-.6f,-.6f,-.4f);
            k[Overclock-UplinkShot]=new[]{g.At(0,0,S),gather.At(0,.7f),flex.At(0,1,I),new CyberCombo.B(flex).Body(.01f,0,0).At(1,.5f,S),g.At(1,1,S)};
            // 1 Fork Bomb: a hand sign (the fists knuckle to knuckle before the face), then both arms flung wide as the clones split off.
            var sign=new CyberCombo.B(g).Hips(0,.06f,0).Spine(0,.18f,0).Chest(0,.12f,0).Head(0,-.3f).Body(-.04f,0,0).Left(.04f,.30f,.32f).LFist(0,1,.2f,-1,0,0).Right(-.04f,.30f,.32f).RFist(0,1,.2f,1,0,0).Poles(.8f,-.4f,0,-.8f,-.4f,0);
            var wide=new CyberCombo.B(g).Hips(0,.04f,0).Spine(0,.04f,0).Chest(0,.02f,0).Head(0,-.25f).Body(-.02f,0,0).Left(.52f,.22f,.02f).LFist(1,.2f,0,0,0,1).Right(-.52f,.22f,.02f).RFist(-1,.2f,0,0,0,1).Poles(.2f,-1,-.4f,-.2f,-1,-.4f).Reach(.15f,.15f);
            k[ForkCast-UplinkShot]=new[]{g.At(0,0,S),sign.At(0,.75f),wide.At(0,1,I),new CyberCombo.B(wide).Left(.5f,.2f,.06f).Right(-.5f,.2f,.06f).At(1,.5f,S),g.At(1,1,S)};
            // 2 Glitch Cut: coiled low with the cutting hand at the far hip, then the dash: low and flat, the hand slashing across
            // and out behind, the legs in a long lunge; then the "already cut" hold, the head turned back to the marks, and a
            // flick of the hand as they detonate.
            var coil=new CyberCombo.B(g).Hips(.35f,.18f,0).Spine(.1f,.25f,0).Chest(.25f,.15f,0).Head(-.6f,-.45f).Body(-.14f,0,0).Right(.16f,-.12f,.2f).RFist(1,-.2f,.3f,0,-1,0).Left(.2f,.16f,.36f).Poles(.35f,-1,-.1f,-.2f,-.6f,.8f);
            var slash=new CyberCombo.B(g).Hips(-.45f,.25f,0).Spine(-.15f,.3f,0).Chest(-.3f,.15f,0).Head(.85f,-.55f).Body(-.16f,0,0).Right(-.55f,.1f,.32f).RFist(-1,0,.3f,0,-1,0).Left(.22f,-.1f,-.3f).Reach(0,.3f)
                .Poles(.4f,0,-1,-.3f,-.8f,0).Legs(.7f,-.6f,.9f,.3f);
            var through=new CyberCombo.B(slash).Hips(-.6f,.18f,0).Chest(-.4f,.1f,0).Head(1.0f,-.45f).Right(-.46f,.05f,-.16f).RFist(-.8f,0,-.6f,0,-1,0).Legs(.5f,-.4f,1.1f,.6f);
            k[GlitchCut-UplinkShot]=new[]{g.At(0,0,S),coil.At(0,.7f),slash.At(0,1,I),through.At(1,1,S)};
            var hold=new CyberCombo.B(through).Ground().Body(-.1f,0,0).Lean(.1f,.12f,.08f).Head(.2f,-.25f).Hips(-.6f,.1f,0).Chest(-.5f,.08f,0);
            var flick=new CyberCombo.B(hold).Right(-.38f,.2f,-.06f).RFist(-.3f,.9f,-.3f,0,0,1).Head(.1f,-.2f);
            k[GlitchHold-UplinkShot]=new[]{new CyberCombo.B(through).Ground().At(0,0,S),hold.At(0,.6f),flick.At(0,1,I),g.At(1,1,S)};
            // 3 Meteor Byte: the crouch (both fists low), the launch (arms swung up, the body stretched), the tuck to the apex;
            // the hover (the fist raised overhead for the giant hardlight fist, the other arm aiming down at the reticle, the
            // body arched); the dive (pitching over, the fist leading) into the ground punch, low in a squat; the rise.
            var crouch=new CyberCombo.B(g).Hips(0,.22f,0).Spine(0,.25f,0).Chest(0,.12f,0).Head(0,-.62f).Body(-.2f,0,0).Left(.22f,-.26f,.12f).LFist(0,-1,.2f,0,0,1).Right(-.22f,-.26f,.12f).RFist(0,-1,.2f,0,0,1).Poles(.5f,-.2f,-1,-.5f,-.2f,-1);
            var launch=new CyberCombo.B(g).Hips(0,-.05f,0).Spine(0,-.05f,0).Chest(0,-.05f,0).Head(0,-.15f).Body(.04f,0,0).Left(.15f,.78f,.12f).LFist(0,1,.1f,-1,0,0).Right(-.15f,.78f,.12f).RFist(0,1,.1f,1,0,0).Poles(.6f,0,-1,-.6f,0,-1).Legs(-.05f,-.15f,.2f,.3f);
            var tuck=new CyberCombo.B(g).Hips(0,.1f,0).Spine(0,.15f,0).Chest(0,.1f,0).Head(0,-.3f).Body(.05f,0,0).Left(.14f,.2f,.3f).Right(-.14f,.2f,.3f).Legs(.9f,.7f,1.8f,1.7f);
            k[MeteorLeap-UplinkShot]=new[]{g.At(0,0,S),crouch.At(0,1,I),launch.At(1,.3f),tuck.At(1,1,S)};
            var raise=new CyberCombo.B(tuck).Hips(.2f,-.08f,0).Spine(.08f,-.1f,0).Chest(.1f,-.05f,0).Head(-.2f,.55f).Right(-.12f,.88f,-.04f).RFist(0,1,0,0,0,1).Left(.16f,-.14f,.55f).LFist(0,-1,.6f,0,0,1).Poles(.4f,-1,0,-.5f,0,-1).Reach(.2f,.15f).Legs(.6f,.4f,1.5f,1.3f);
            k[MeteorHover-UplinkShot]=new[]{tuck.At(0,0,S),raise.At(0,1,I),new CyberCombo.B(raise).Right(-.12f,.9f,-.06f).At(1,1,S)};
            var pitch=new CyberCombo.B(raise).Lean(.45f,.3f,.2f).Head(0,-.5f).Right(-.05f,.25f,.62f).RFist(0,-.4f,1,0,-1,0).Left(.25f,.2f,-.35f).LFist(0,-.3f,-1,0,-1,0).Legs(-.3f,-.4f,.5f,.7f);
            var punch=new CyberCombo.B(g).Hips(-.1f,.45f,0).Spine(0,.35f,0).Chest(0,.25f,0).Head(.1f,-.75f).Body(-.28f,0,.02f).Right(-.08f,-.6f,.55f).RFist(0,-1,.2f,0,0,1).Left(.32f,.18f,-.3f).LFist(.3f,-.3f,-1,0,-1,0).Poles(.4f,0,-1,-.6f,-.3f,-.6f).Reach(0,.3f).Ground();
            k[MeteorDive-UplinkShot]=new[]{raise.At(0,0,S),pitch.At(0,.45f),punch.At(0,1,I),new CyberCombo.B(punch).Right(-.08f,-.58f,.55f).At(1,1,S)};
            var rise=new CyberCombo.B(g).Lean(.15f,.25f,.15f).Body(-.12f,0,0).Right(-.12f,.12f,.32f).Head(.3f,-.4f);
            k[MeteorLand-UplinkShot]=new[]{punch.At(0,0,S),new CyberCombo.B(punch).Right(-.08f,-.55f,.53f).At(0,1,S),rise.At(1,.5f),g.At(1,1,S)};
            return k;
        }
    }

    /// <summary>
    /// The v2 move at a moment (CyberLordMotion.Of, once Kernel Panic's own have passed): the newest live ability's pose, the
    /// combo's strike, or the guard; each a strike (Move.Strike) on its replicated window, faded out over the slot's settle.
    /// </summary>
    public static class CyberKit2MeleeMotion
    {
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Ramp(float t,float a,float b)=>Clamp01((t-a)/Math.Max(1e-4f,b-a));
        private static CyberLordMotion.Move Strike(int kind,float at,float hit,float until,float time,float fade,string state)
        {
            var m=CyberLordMotion.Move.None;m.Strike=kind+1;m.Swing=new CyberCombo.Swing(kind,at,hit,until);m.State=state??CyberKit2MeleePoses.State(kind)??CyberCombo.States[Math.Min(kind,CyberCombo.Kinds-1)];
            m.Root=kind==CyberKit2MeleePoses.UplinkShot||kind==CyberKit2MeleePoses.Overclock||kind==CyberKit2MeleePoses.ForkCast?.5f:1;m.Time=time;
            m.FadeOut=1-MeleeMath.Ease(Ramp(time,until,until+fade));m.Weight=CyberLordMotion.StrikeIn(m.Swing,time)*m.FadeOut;return m;
        }
        /// <summary>The v2 move at `time`, or Move.None (the stance; a block's guard is the caller's).</summary>
        public static CyberLordMotion.Move Of(CyberLordView view,float time)
        {
            var v=view.Kit2;
            int newest=-1;float at=-100;
            foreach(int s in Slots){float end=End(v,s);if(v.At[s]>-90&&time>=v.At[s]&&time<end&&v.At[s]>at){newest=s;at=v.At[s];}}
            bool combo=v.ComboAt>-90&&time>=v.ComboAt&&time<v.ComboUntil+CyberCombo.Settle;
            if(newest>=0&&!(combo&&v.ComboAt>at))return Ability(v,newest,time);
            if(combo)
            {
                int kind=CyberCombo.V2Kind(v.ComboStep);var m=Strike(kind,v.ComboAt,Math.Max(v.ComboAt+.02f,Math.Min(v.ComboUntil-.02f,v.ComboHit)),v.ComboUntil,time,CyberCombo.Settle,CyberCombo.States[kind]);
                // An interrupted step (CutAt) keeps its own motion on its nominal windows (the rate it began at), fading out from
                // the cut over the settle: a kick's leg comes down on its own path instead of snapping back in a frame.
                if(v.Cut[CyberKit2Spec.MeleeLmb])
                {
                    float rate=v.OverclockAt>-90&&v.ComboAt>=v.OverclockAt&&v.ComboAt<v.OverclockUntil?1+CyberKit2Spec.OverclockRate:1;
                    m.Swing=new CyberCombo.Swing(kind,v.ComboAt,v.ComboAt+CyberCombo.Windup(kind)/rate,v.ComboAt+CyberCombo.Seconds(kind)/rate);m.Weight=CyberLordMotion.StrikeIn(m.Swing,time)*m.FadeOut;
                }
                // the simulation's step, when the authority moves the pilot through the string (else the in-capsule step)
                m.Swing=m.Swing.Stepped(v);
                m.StrikeFrom=v.ComboStep>0?CyberCombo.Previous(kind,true):-1;if(m.StrikeFrom>=0)m.Weight=m.FadeOut;return m;
            }
            return CyberLordMotion.Move.None;
        }
        private static readonly int[] Slots={CyberKit2Spec.Q,CyberKit2Spec.E,CyberKit2Spec.Fork,CyberKit2Spec.Cut,CyberKit2Spec.Meteor};
        /// <summary>The end of a slot's poses (its settle included): the Uplink's zip, landing or yank, the Glitch Cut's hold to
        /// the detonation, the Meteor's rise.</summary>
        public static float End(CyberKit2MeleeView v,int s)
        {
            float settle=CyberKit2Spec.Settle[s],end=Math.Max(v.Until[s],v.RecoverUntil[s]);
            switch(s)
            {
                case CyberKit2Spec.Q:if(v.Cut[s])return end;return v.UplinkOutcome==CyberKit2Melee.Zip?Math.Max(end,v.UplinkArriveAt+LandSeconds+settle):v.UplinkOutcome==CyberKit2Melee.Yank?Math.Max(end,v.UplinkArriveAt+YankHold+settle):v.UplinkOutcome==CyberKit2Melee.Miss?Math.Max(end,v.Release[s]+CyberKit2Melee.MissSeconds*.5f+settle):Math.Max(end,v.UplinkLatchAt+settle);
                case CyberKit2Spec.Cut:return v.Cut[s]?end:Math.Max(end,v.DetonateAt+HoldAfter+settle);
                case CyberKit2Spec.Meteor:return v.Cut[s]?end:Math.Max(end,v.LandAt+RiseSeconds+settle);
                default:return end;
            }
        }
        /// <summary>The zip's landing, the yank's follow-on, the hold past the detonation and the meteor's rise (s).</summary>
        public const float LandSeconds=.3f,YankHold=.18f,HoldAfter=.22f,RiseSeconds=.4f;
        private static CyberLordMotion.Move Ability(CyberKit2MeleeView v,int s,float time)
        {
            float at=v.At[s],rel=Math.Max(at+.01f,v.Release[s]),until=Math.Max(rel+.01f,v.Until[s]),settle=CyberKit2Spec.Settle[s];
            switch(s)
            {
                case CyberKit2Spec.Q:
                {
                    // the shot holds the cable out until it latches (or, missing, until it is half way back)
                    float hold=v.UplinkOutcome==CyberKit2Melee.Miss?rel+CyberKit2Melee.MissSeconds*.5f:v.UplinkLatchAt>-90?Math.Max(until,v.UplinkLatchAt):until;
                    if(v.Cut[s]||time<hold||v.UplinkOutcome==CyberKit2Melee.Miss||v.UplinkOutcome==CyberKit2Melee.Flying)return Strike(CyberKit2MeleePoses.UplinkShot,at,rel,Math.Max(until,hold),time,settle,null);
                    float latch=v.UplinkLatchAt,arrive=Math.Max(latch+.02f,v.UplinkArriveAt);
                    if(v.UplinkOutcome==CyberKit2Melee.Zip)
                    {
                        if(time<arrive)return Strike(CyberKit2MeleePoses.UplinkZip,latch,Math.Min(arrive-.01f,latch+.06f),arrive,time,settle,null).Held();
                        return Strike(CyberKit2MeleePoses.UplinkLand,arrive,arrive+.06f,arrive+LandSeconds,time,settle,null).Held();
                    }
                    return Strike(CyberKit2MeleePoses.UplinkYank,latch,latch+.08f,arrive+YankHold,time,settle,null).Held();
                }
                case CyberKit2Spec.E:return Strike(CyberKit2MeleePoses.Overclock,at,rel,until,time,settle,null);
                case CyberKit2Spec.Fork:return Strike(CyberKit2MeleePoses.ForkCast,at,rel,until,time,settle,null);
                case CyberKit2Spec.Cut:
                {
                    if(time<until||v.Cut[s])return Strike(CyberKit2MeleePoses.GlitchCut,at,rel+(until-rel)*.5f,until,time,settle,null);
                    float det=Math.Max(until+.05f,v.DetonateAt);return Strike(CyberKit2MeleePoses.GlitchHold,until,det,det+HoldAfter,time,settle,null).Held();
                }
                case CyberKit2Spec.Meteor:
                {
                    float leap=CyberKit2Melee.LeapAt(at),hover=CyberKit2Melee.HoverAt(at),dive=CyberKit2Melee.DiveAt(at),land=Math.Max(dive+.02f,v.LandAt);
                    if(v.Cut[s]&&time>=until)return Strike(CyberKit2MeleePoses.MeteorLand,until,until+.05f,until+RiseSeconds,time,settle,null).Held();
                    if(time<hover)return Strike(CyberKit2MeleePoses.MeteorLeap,at,leap,hover,time,settle,null);
                    if(time<dive)return Strike(CyberKit2MeleePoses.MeteorHover,hover,hover+.1f,dive,time,settle,null).Held();
                    if(time<land)return Strike(CyberKit2MeleePoses.MeteorDive,dive,land,land+.06f,time,settle,null).Held();
                    return Strike(CyberKit2MeleePoses.MeteorLand,land,land+.05f,land+RiseSeconds,time,settle,null).Held();
                }
            }
            return CyberLordMotion.Move.None;
        }
        /// <summary>A pose carried on from the one before (no blend in from the stance: the rig's last row is the start).</summary>
        private static CyberLordMotion.Move Held(this CyberLordMotion.Move m){m.StrikeFrom=-2;m.Weight=m.FadeOut;return m;}
    }
}

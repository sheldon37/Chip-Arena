using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV fighting styles (Emperor, Ansatsuken) in third person, in character-animation.ts sampleCharacter's order: the
    /// gait's locomotion (Core/MeleeGait with the styles' bladed stances, stances.ts), then a juggle, a knockdown, the hit stun,
    /// the Emperor's dodge roll, or the style's own layer (emperor-animation.ts emperorLayers, ansatsuken-animation.ts
    /// ansatsukenLayers) from its exported clips (tools/export_motion.mjs: guard idle, block, actions, Demon Step). A fighter
    /// never plays the sword set's idle, walk, run, cut, block or stagger. Presentation only.
    /// </summary>
    public static class NativeFighterPose
    {
        public static bool Handles(string spec)=>spec=="emperor"||spec=="ansatsuken";
        private static readonly Dictionary<(string,string,int,int),string> keys=new Dictionary<(string,string,int,int),string>();
        /// <summary>
        /// The style clip for this frame (a SourceMotion key), or null when no style clip plays (the Emperor's dodge roll is
        /// dodgeRollMotion, before the style layers). The Ansatsuken's Shift is its Demon Step, part of its own layer.
        /// `action` is the replicated SourceAction while it lasts. Keys are cached: no allocation once seen.
        /// </summary>
        public static string Clip(string spec,string action,int total,int index,bool rolling,bool blocking)
        {
            if(!Handles(spec))return null;
            if(rolling)return spec=="ansatsuken"?"ansatsuken-step":null;
            if(blocking)action="block";else if(string.IsNullOrEmpty(action))action="idle";
            bool single=action=="charge"||action=="wave"||action=="hadoken"||action=="shinku"||action=="electric";
            if(action=="idle"||action=="block"){total=index=0;}else{if(single)total=1;if(action!="jab"&&action!="combo")index=0;}
            var k=(spec,action,total,index);if(keys.TryGetValue(k,out var key))return key;
            key=action=="idle"||action=="block"?spec+"-"+action:spec+"-"+action+"-"+total+"-"+index;keys[k]=key;return key;
        }
        /// <summary>Whether the replicated action still plays (the authority clears it at its end; a charge lasts until release).</summary>
        public static bool Acting(string action,float until,float time)=>!string.IsNullOrEmpty(action)&&(action=="charge"||time<until);

        private static readonly int[] Upper={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,
            CapturedRig.RClav,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHand,CapturedRig.RHand};
        private static readonly Quaternion[] upperBase=new Quaternion[14];
        /// <summary>Whether a sampled style pose authors the legs (a kick); otherwise the gait keeps them (the clips were exported
        /// without a gait, so an unauthored leg is the identity).</summary>
        public static bool AuthorsLegs(CapturedRig.Pose clip)
        {
            for(int b=CapturedRig.LHip;b<=CapturedRig.RToe;b++)if(1-Math.Abs(clip.Rotation[b].W)>1e-5f)return true;
            return false;
        }
        /// <summary>
        /// emperorLayers / ansatsukenLayers over the gait rig `r`: the style clip owns the upper body (the legs only where it
        /// authors them); on the ground the hips keep travel*.8 (Ansatsuken .85) of the gait's body and twist with the stride
        /// (not during a wavedash); with no action, not blocking, the travel carries the upper body back to the gait's (its arm
        /// pump) by ease((motion-.10)/.65), so a running fighter runs with its own guard melting into the gait, not a sword carry.
        /// </summary>
        public static void Layer(MeleeRig r,CapturedRig.Pose clip,bool emperor,bool grounded,bool wave,bool acting,bool blocking)
        {
            for(int i=0;i<Upper.Length;i++)upperBase[i]=r.Q[Upper[i]];
            Vector3 body=r.P[CapturedRig.Body];float travel=MeleeMath.Clamp(r.Motion*1.4f,0,1);bool legs=AuthorsLegs(clip);
            for(int b=0;b<MeleeRig.N;b++)if(legs||b<CapturedRig.LHip)r.SetQ(b,clip.Rotation[b]);
            r.P[CapturedRig.Body]=clip.Position[CapturedRig.Body];if(legs)r.LegsAuthored=true;
            if(grounded&&!(emperor&&wave))
            {
                float s=(float)Math.Sin(r.Stride);r.P[CapturedRig.Body]=Vector3.Lerp(r.P[CapturedRig.Body],body,travel*(emperor?.8f:.85f));
                r.AddE(CapturedRig.Pelvis,0,s*.07f*travel,0);r.AddE(CapturedRig.Chest,0,-s*(emperor?.05f:.1f)*travel,0);
            }
            if(acting||blocking||!grounded)return;
            float carry=MeleeMath.Ease((r.Motion-.10f)/.65f);if(carry<=0)return;
            for(int i=0;i<Upper.Length;i++)r.Slerp(Upper[i],upperBase[i],carry);
            r.P[CapturedRig.Body]=Vector3.Lerp(r.P[CapturedRig.Body],body,carry);
        }
    }

    /// <summary>
    /// The juggle a pilot shows, observed from the replicated Actor.JuggleUntil (each juggle hit sets it to the hit + 1.25 s,
    /// a bound or wall splat to + .85 s, the Siren's launch to + 2 s; 0 once the juggle ends). Keeps the start and the last two
    /// hits as emperor-animation.ts previousHit does; a time step back (a replay) starts over.
    /// </summary>
    public sealed class NativeJuggleTrack
    {
        public float At=-100,LastHit=-100,Previous=float.NegativeInfinity,Until;public int Hits;private float seen=-100;
        /// <summary>When a newly seen JuggleUntil was set: never after the frame that saw it.</summary>
        public static float HitOf(float until,float time)=>Math.Min(time,until-(until-time>1.05f?1.25f:.85f));
        public bool Observe(float until,float time)
        {
            if(time<seen-.1f)Until=0;seen=time;
            if(!(until>0)){Until=0;return false;}
            if(!(Until>0)){At=LastHit=HitOf(until,time);Previous=float.NegativeInfinity;Hits=1;}
            else if(until>Until+.001f){Previous=LastHit;LastHit=HitOf(until,time);Hits++;}
            Until=until;return true;
        }
    }

    /// <summary>
    /// emperor-animation.ts animateJuggle for any juggled pilot (an Emperor's launch or the Siren's): absolute hit times give
    /// every observer the same launch. Each hit kicks the body with an impulse rising over 65 ms (adding to the previous
    /// hit's), the limbs trail the vertical velocity and flail with decaying, staggered phases, and the launch grows out of the
    /// pose the hit caught over .16 s. A Mishima-screw spin needs the replicated spin end (spinUntil 0 shows none).
    /// </summary>
    public static class NativeJugglePose
    {
        public const float ScrewSpin=.65f,Grace=3;
        /// <summary>The juggle lasts until the pilot lands (the source extends `until` while airborne), capped for safety.</summary>
        public static bool Active(float until,float time)=>until>0&&time<until+Grace;
        private static readonly int[] Bones={CapturedRig.Body,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Head,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,
            CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee,CapturedRig.LFoot,CapturedRig.RFoot};
        private static readonly Quaternion[] held=new Quaternion[14];
        private static float Pulse(float a)=>a<=0||a>2?0:a/.065f*(float)Math.Exp(1-a/.065f);
        private static void Set(CapturedRig.Pose p,int b,float x,float y,float z)=>p.Rotation[b]=MeleeMath.Euler(x,y,z);
        /// <summary>Only the X of a joint's (XYZ) Euler, as `node.rotation.x=` does.</summary>
        private static void SetX(CapturedRig.Pose p,int b,float x){var e=MeleeMath.ToEuler(p.Rotation[b]);p.Rotation[b]=MeleeMath.Euler(x,e.Y,e.Z);}
        public static void Apply(CapturedRig.Pose p,float time,NativeJuggleTrack j,float vy,float spinUntil=0)
        {
            float age=Math.Max(0,time-j.LastHit),kick=Pulse(age)+Pulse(time-j.Previous),start=spinUntil-ScrewSpin;
            float spinU=spinUntil>time&&time>=start?MeleeMath.Clamp((time-start)/Math.Max(.05f,spinUntil-start),0,1):0,spin=MeleeMath.TAU*MeleeMath.Ease(spinU);
            float air=time-j.At,lift=MeleeMath.Clamp(-vy*.06f,-.35f,.45f),decay=.05f+(float)Math.Exp(-air*1.5f)*.17f;
            float Flail(float phase)=>(float)Math.Sin(air*9+phase)*decay;
            for(int i=0;i<Bones.Length;i++)held[i]=p.Rotation[Bones[i]];
            float launch=MeleeMath.Ease((time-j.At)/.16f),rise=p.Position[CapturedRig.Body].Y;
            p.Position[CapturedRig.Body].Y=.03f;
            Set(p,CapturedRig.Body,-.7f-kick*.45f,(float)Math.Sin((time-j.At)*4)*.18f+(spinU>0?spin:0),(float)Math.Sin(time*8)*.10f);
            Set(p,CapturedRig.Spine,.24f-.08f*kick,0,0);Set(p,CapturedRig.Chest,.22f,0,.13f*kick);Set(p,CapturedRig.Head,-.32f+.3f*kick*(float)Math.Exp(-age*6),0,.08f);
            Set(p,CapturedRig.LArm,-.45f-.3f*lift+Flail(0),0,.65f+.2f*kick+.2f*lift+Flail(.6f)*.6f);Set(p,CapturedRig.RArm,.25f-.25f*lift+Flail(.55f),0,-.75f-.15f*lift-Flail(1.2f)*.6f);
            SetX(p,CapturedRig.LElbow,-.75f+Flail(1.6f)*.5f);SetX(p,CapturedRig.RElbow,-.4f+Flail(2.1f)*.5f);
            Set(p,CapturedRig.LHip,-.85f-.25f*kick-.2f*lift+Flail(.8f),0,.22f);Set(p,CapturedRig.RHip,-.4f-.15f*lift-Flail(1.4f),0,-.25f);
            SetX(p,CapturedRig.LKnee,1.3f+.2f*lift+Flail(2.4f)*.4f);SetX(p,CapturedRig.RKnee,.85f+.2f*(float)Math.Sin(time*9)+.25f*lift);
            SetX(p,CapturedRig.LFoot,-.2f);SetX(p,CapturedRig.RFoot,-.2f);
            if(launch<1)
            {
                for(int i=0;i<Bones.Length;i++)p.Rotation[Bones[i]]=MeleeMath.Slerp(held[i],p.Rotation[Bones[i]],launch);
                p.Position[CapturedRig.Body].Y=rise+(.03f-rise)*launch;
            }
        }
    }

    /// <summary>
    /// client/emperor-effects.ts triggers. The Electric Hook fist: an Emperor whose action is 'electric' (electricAt, .35 s).
    /// A juggled pilot: .28 s from its last juggle hit (only an Emperor's confirms and the Siren's launch juggle). Impacts:
    /// only emperor-hit / emperor-bound, i.e. hits from an Emperor's punch (Electric Hook 1.3, jab .65), its Twin echo (1.3,
    /// .13 s after an Electric Hook hit), its storm-trail SHOCK (.75) and its bound or wall splat (1.8, the victim's juggle reset
    /// to + .85 s). Nothing an Ansatsuken does (combo, tatsu, shoryuken, hadoken, Demon Step) and no other Emperor hit spawns a bolt.
    /// </summary>
    public static class NativeEmperorTriggers
    {
        public const float EchoDelay=.13f;
        public static bool Fist(string spec,string action)=>spec=="emperor"&&action=="electric";
        /// <summary>The impact radius for a Hit event from `sourceSpec` with `detail` (direct/secondary), 0 for none. `punch`: the
        /// hit matches a tracked strike (LastAttack), `electricPunch`: that strike was the Electric Hook's, `sinceElectric`: seconds
        /// since the last Electric Hook strike, `storm`: an owned storm trail lies near the target, `splat`: the target's juggle
        /// was just reset to the hit + .85 s.</summary>
        /// <summary>Whether a juggle hit at `hit` shows the juggle bolt: not when it was a basic (non-electric) Emperor jab, per the
        /// owner direction above. `jabAt`/`prevJabAt` are an Emperor's last two non-electric strike times (-100 for none).</summary>
        public static bool JuggleBolt(float hit,float jabAt,float prevJabAt)=>Math.Abs(hit-jabAt)>=.05f&&Math.Abs(hit-prevJabAt)>=.05f;
        public static double Impact(string sourceSpec,string detail,bool punch,bool electricPunch,float sinceElectric,bool storm,bool splat)
        {
            if(sourceSpec!="emperor")return 0;
            // Owner direction (2026-10-09): basic attacks show no lightning, so the source's .65 jab impact is not drawn.
            if(detail=="direct")return punch?(electricPunch?1.3:0):storm?NativeEmperorBolts.DefaultRadius:sinceElectric>=EchoDelay-.04f&&sinceElectric<=EchoDelay+.08f?1.3:0;
            return detail=="secondary"&&splat?1.8:0;
        }
    }
}

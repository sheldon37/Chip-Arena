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
        /// <summary>
        /// The two baked frames and the blend for a clip sample at `fraction` (NativeSourceMotion.Sample's frame math). The
        /// exporter bakes an action's last frame at its until, where emperorLayers has already returned to the guard; the
        /// wavedash is closed (its dip, sin(pi u), is 0 at both ends), so its last segment eases into its first frame instead
        /// of the guard and a chained wavedash (E held) carries on without a twitch.
        /// </summary>
        public static void Frames(int frames,float fraction,bool closed,out int a,out int b,out float t)
        {
            float index=fraction*(frames-1);a=Math.Min(frames-1,(int)index);b=Math.Min(frames-1,a+1);t=index-a;
            if(closed){if(a==frames-1)a=0;if(b==frames-1)b=0;}
        }
        /// <summary>Whether a style clip is closed (ends on its first pose): the Emperor's wavedash.</summary>
        public static bool Closed(string key)=>key=="emperor-wave-1-0";

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
        /// The Emperor's guard writes only the hips' height (emperorGuard), so they keep the gait's sideways sway and forward lean
        /// offset (the clips were baked with the body at the origin) and only the height takes the travel blend; `pinned` (its
        /// Electric Hook and wavedash, which write the hips' height and depth after that blend) keeps the clip's height and depth.
        /// </summary>
        public static void Layer(MeleeRig r,CapturedRig.Pose clip,bool emperor,bool grounded,bool wave,bool acting,bool blocking,bool pinned=false)
        {
            for(int i=0;i<Upper.Length;i++)upperBase[i]=r.Q[Upper[i]];
            Vector3 body=r.P[CapturedRig.Body],c=clip.Position[CapturedRig.Body];float travel=MeleeMath.Clamp(r.Motion*1.4f,0,1);bool legs=AuthorsLegs(clip);
            for(int b=0;b<MeleeRig.N;b++)if(legs||b<CapturedRig.LHip)r.SetQ(b,clip.Rotation[b]);
            r.P[CapturedRig.Body]=emperor?new Vector3(body.X+c.X,c.Y,pinned?c.Z:body.Z+c.Z):c;if(legs)r.LegsAuthored=true;
            if(grounded&&!(emperor&&wave))
            {
                float s=(float)Math.Sin(r.Stride);
                if(!emperor)r.P[CapturedRig.Body]=Vector3.Lerp(r.P[CapturedRig.Body],body,travel*.85f);else if(!pinned)r.P[CapturedRig.Body].Y+=(body.Y-c.Y)*travel*.8f;
                r.AddE(CapturedRig.Pelvis,0,s*.07f*travel,0);r.AddE(CapturedRig.Chest,0,-s*(emperor?.05f:.1f)*travel,0);
            }
            if(acting||blocking||!grounded)return;
            float carry=MeleeMath.Ease((r.Motion-.10f)/.65f);if(carry<=0)return;
            for(int i=0;i<Upper.Length;i++)r.Slerp(Upper[i],upperBase[i],carry);
            r.P[CapturedRig.Body]=Vector3.Lerp(r.P[CapturedRig.Body],body,carry);
        }

        /// <summary>
        /// CapturedAnimation FighterPose's view of the replicated actor: the style action is the pose transition's key
        /// (pose-transitions.ts transitionAction keys a fighter by its emperor/arcade action), and an Emperor's wavedash is the
        /// gait's dash (movement.ts: each wave sets dashTime .18, which runs out on its own through a cancel), so the legs leave
        /// the stride like a push-off instead of running at the dash speed. A hit, juggle, knockdown or dodge roll ends the dash
        /// (emperor.ts confirm and beginRoll clear it). Call after the view's juggle, down and hit stun are set.
        /// </summary>
        public static void View(MeleeView v,Actor a,NativeWaveDash wave,float time)
        {
            v.ActionKind=string.IsNullOrEmpty(a.SourceAction)?null:a.SourceAction;v.ActionAt=a.SourceAt;v.ActionUntil=a.SourceUntil;
            if(v.Juggle||v.Down||v.HitStunUntil>time||a.Rolling(time))wave.Stop(a.SourceAction,a.SourceAt);
            v.DashTime=a.SourceSpec=="emperor"?wave.DashTime(a.SourceAction,a.SourceAt,a.SourceUntil,time):0;
        }
        /// <summary>
        /// The style over the gait for a frame that plays a style clip (between MeleeMotion.FighterBase and FighterFinish): the
        /// clip layer, then an acting Emperor's per-foot edits (emperorLayers then applyFighterLegs, as animateEmperor runs them).
        /// </summary>
        public static void Style(MeleeMotion m,CapturedRig.Pose clip,Actor a,bool stun,float time)
        {
            bool emperor=a.SourceSpec=="emperor",acting=!stun&&Acting(a.SourceAction,a.SourceUntil,time),blocking=!stun&&time<a.BlockUntil;
            bool wave=acting&&a.SourceAction=="wave";
            Layer(m.Rig,clip,emperor,a.Grounded,wave,acting||!stun&&a.Rolling(time),blocking,emperor&&(wave||acting&&a.SourceAction=="electric"));
            if(emperor&&acting&&!blocking)Feet(m,a.SourceAction,time-a.SourceAt,a.SourceUntil-a.SourceAt);
        }
        /// <summary>emperorLayers' wavedash length (u = age / .18) and fighter-motion.ts's ball-of-foot offset.</summary>
        public const float WaveSeconds=.18f,Ball=.09f;
        private static readonly Vector3 WaveLead=new Vector3(.16f,-.772f,.24f),WaveRear=new Vector3(-.14f,-.765f,-.30f);
        /// <summary>
        /// emperorLayers' foot edits, solved over the gait as fighter-motion.ts applyFighterLegs does (the clips were exported
        /// without a gait, so these never reached them). The wavedash skims the floor: the lead (left) ankle reaches to
        /// (.16,-.772,.24) and the rear trails to (-.14,-.765,-.30) on its toes (heel +.55), both by sqrt(sin(pi u)). The
        /// Electric Hook's rear heel drives up by .3 power (1 - travel) into the hook. Each ankle starts from its gait target
        /// (a raised heel lifts it by Ball sin(heel), pitches the foot and bends the toe -.45 heel), blends to the edit's target
        /// and stays above the floor; the leg is solved as layerLeg does, so a zero edit is exactly the gait leg. False (legs
        /// left to the gait) for other actions or before the gait has run.
        /// </summary>
        public static bool Feet(MeleeMotion m,string action,float age,float duration)
        {
            float reach,heel;
            if(action=="wave"){float dip=(float)Math.Sin(MeleeMath.Clamp(age/WaveSeconds,0,1)*MeleeMath.PI);reach=(float)Math.Sqrt(Math.Max(0,dip));heel=.55f*reach;}
            else if(action=="electric")
            {
                float u=MeleeMath.Clamp(age/Math.Max(1e-4f,duration),0,1),power=MeleeMath.Ease((u-.115f)/.135f)*(1-MeleeMath.Ease((u-.48f)/.52f));
                reach=0;heel=.3f*power*(1-MeleeMath.Clamp(m.Rig.Motion*1.4f,0,1));
            }
            else return false;
            for(int i=0;i<2;i++)
            {
                bool left=i==0;if(!m.GaitFoot(left,out Vector3 g,out float yaw,out float pitch,out float toe))return false;
                float h=left?0:heel;Vector3 target=new Vector3(g.X,g.Y+Ball*(float)Math.Sin(h),g.Z);
                if(reach>0)target=Vector3.Lerp(target,left?WaveLead:WaveRear,Math.Min(1,reach));target.Y=Math.Max(MeleeRig.FLOOR,target.Y);
                m.Rig.LayerLeg(left,target,yaw,pitch+h,toe-.45f*h);
            }
            return true;
        }
    }

    /// <summary>
    /// The Emperor's wavedash push as the gait sees it (movement.ts: E sets dashTime .18 with the push; Q's Electric Hook
    /// starting mid-wave clears waveTime but not dashTime). Observed from the replicated SourceAction/SourceAt: the latest
    /// wave's window is kept when the action changes, so a cancel keeps the push-off legs until the dash runs out.
    /// </summary>
    public sealed class NativeWaveDash
    {
        public const float Seconds=.18f;
        private float at=-100,until=-100,stopped=-100;
        /// <summary>Seconds of dash left at `time` (0 outside the latest wave's window, also after a replay step back).</summary>
        public float DashTime(string action,float sourceAt,float sourceUntil,float time)
        {
            if(action=="wave"&&time<sourceUntil&&time>=sourceAt&&sourceAt!=stopped){at=sourceAt;until=sourceAt+Seconds;}
            return time>=at&&time<until?until-time:0;
        }
        /// <summary>End the dash now; the stopped wave (a replicated action that still reads 'wave') does not start it again.</summary>
        public void Stop(string action,float sourceAt){until=-100;if(action=="wave")stopped=sourceAt;}
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

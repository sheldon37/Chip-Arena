using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Cyber Lord's pilot motion (owner 2026-10-12: "an unarmed class, using cyber themed abilities ... a melee
    /// spellcasting hybrid", the "hardlight brawler" direction), on the DEV fighters' route (CapturedAnimation FighterPose:
    /// the gait's locomotion and procedural feet, this layer, then the hit reactions, jump layer and ground pass) in third
    /// person, and on the first-person view model. Every move is one of the owner's Explosive pack unarmed takes
    /// (tools/export_pack_cyberlord.mjs, NativePackCyberLord.Generated) retimed onto the replicated windows read through
    /// CyberLordFeed: the bladed fighting stance with the pack's run and sprint carry over the gait, the hardlight guard,
    /// the Jab / Cross / Pulse Palm combo (each take's impact pinned to its hit), the six abilities on their cast / active
    /// / recovery windows, the idle stance after the Sheldon Intro's ending once the pilot stands still
    /// (Core/CyberLordIdle.cs), the Kernel Panic opener and its five-step choreography, and the victim's tumble (any pilot,
    /// layered over its own route). Poses are pure functions of the replicated view and the clock (the gait under them
    /// keeps its own travel state, and the idle when the pilot last moved); nothing here reaches the authority, hit
    /// timing, hitboxes, damage, cooldowns or movement. No allocation per frame.
    /// </summary>
    public sealed class CyberLordMotion
    {
        public static bool Handles(string spec)=>CyberLordFeed.Handles(spec);
        /// <summary>
        /// Whether the simulation is stepping the pilot through a string step (docs/CYBER-LORD.md "Melee step contract": the
        /// actor's step fields; never under today's rules). The fighter route hides that travel from the gait's locomotion, as
        /// a weapon's committed swing does: the strike's own feet (CyberComboFeet) plant for it, and the gait must not start a
        /// stride under them.
        /// </summary>
        public static bool Stepping(Actor a,float time)=>a!=null&&a.StepDistance>0&&a.StepUntil>a.StepAt&&time>=a.StepAt-.02f&&time<a.StepUntil+.1f;
        // ---- the takes (NativePackCyberLord order) ----
        public const int Stance=0,Guard=1,Run=2,Sprint=3,Jab=4,Cross=5,Palm=6,Dash=7,Firewall=8,Burst=9,Siphon=10,Crash=11,
            Lunge=12,Breaker=13,Kick=14,Hammer=15,Slam=16,Launched=17,Knockdown=18,Stunned=19,Boost=20,Clips=21;
        public static CapturedClip Clip(int i)=>i>=0&&i<NativePackCyberLord.Clips.Length?NativePackCyberLord.Clips[i]:null;
        /// <summary>A take's impact (its second mark), else its end.</summary>
        public static float Impact(int i){var c=Clip(i);return c==null?0:c.Marks.Length>2?c.Marks[1]:c.Duration;}
        /// <summary>The empty hands' capture set: one-handed, no blade, no clearance (the fists are the hands).</summary>
        public static readonly MeleeSet Unarmed=new MeleeSet{Spec=CyberLordSpec.Id,Index=-1,Fit=new MeleeFit{OneHand=true,Clear=MeleeClear.None},Tip=new Vector3(0,0,.08f),BladeEnd=0,Cuts=Array.Empty<CapturedClip>(),Anticipation=new[]{1f}};
        /// <summary>The combo's takes by step.</summary>
        public static int ComboClip(int step)=>step==1?Cross:step==2?Palm:Jab;
        /// <summary>Blend in / out (s) of a move over the stance and carry.</summary>
        public const float BlendIn=.06f,BlendOut=.14f,ChainBlend=.07f;
        /// <summary>A strike's ease in from the stance: over most of its windup (at least BlendIn, at most .14 s), so the hands rise
        /// straight into the blow and the blow lands at full weight.</summary>
        public static float StrikeIn(in CyberCombo.Swing s,float time)=>MeleeMath.Smooth(Ramp(time,s.At,s.At+Math.Min(.14f,Math.Max(BlendIn,(s.Hit-s.At)*.9f))));

        /// <summary>One frame's move: the take and its clip time, its weight over the base, and the legs and root shares it owns.</summary>
        public struct Move
        {
            public int Clip;public float Time,Weight,Legs,Root;public string State;
            /// <summary>A second take cross-faded under it (a chained combo step's previous swing held at its end), and its share.</summary>
            public int From;public float FromTime,FromWeight;
            /// <summary>Kernel Panic victim tumble: body pitch (rad, + forward), roll and spin about +y.</summary>
            public float Pitch,Roll,Spin;
            /// <summary>A hardlight combo strike (Core/CyberLordCombo.cs: authored pose to pose, no take): its kind + 1 (0 none), its
            /// swing, and the kind a chained strike comes from (-1: from the stance).</summary>
            public int Strike,StrikeFrom;public CyberCombo.Swing Swing;public float FadeOut;
            /// <summary>Whether anything plays (a take or a strike).</summary>
            public bool Active=>Clip>=0||Strike>0;
            public static Move None=>new Move{Clip=-1,From=-1,State=null,StrikeFrom=-1};
        }
        private static readonly float[] knots=new float[6],marks=new float[6];
        private static readonly float[][] At={null,null,new float[2],new float[3],new float[4]},Marks={null,null,new float[2],new float[3],new float[4]};
        // The clip time at `t` through the first `n` knots (monotone C1, CapturedClip.Retime); knots closer than .1 ms are spread.
        private static float Retime(int n,float t)
        {
            var a=At[n];var m=Marks[n];for(int i=0;i<n;i++){a[i]=knots[i];m[i]=marks[i];if(i>0&&a[i]<a[i-1]+1e-4f)a[i]=a[i-1]+1e-4f;}
            return CapturedClip.Retime(a,m,t);
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Ramp(float t,float a,float b)=>Clamp01((t-a)/Math.Max(1e-4f,b-a));
        private static float Ease(float x)=>MeleeMath.Ease(x);

        /// <summary>
        /// The Cyber Lord's move at `time` from its view (caster role), or Move.None for the stance / carry. Order: the Kernel
        /// Panic sequence, the Kernel Panic opener, the newest live ability, the combo swing, then the guard.
        /// </summary>
        /// <summary>Kernel Panic's fork-bomb clone stand-ins (NativeCyberLordFx.CloneActor) and the entrance keep the v1 takes for
        /// their strikes: Kernel Panic and the Hardlight Boot are unchanged.</summary>
        public static bool LegacyCombo(int id)=>id<=-1000&&id>-2000;
        public static Move Of(CyberLordView v,float time,bool blocking,float blockAt)=>Of(v,time,blocking,blockAt,LegacyCombo(v.Id));
        public static Move Of(CyberLordView v,float time,bool blocking,float blockAt,bool legacy)
        {
            var m=Move.None;
            // 4 Kernel Panic: the cut-in freeze and the five steps, then the recovery.
            if(v.PanicLive(time)||v.PanicStart>-90&&time>=v.PanicStart&&time<v.PanicStart+KernelPanicPath.End+BlendOut)
            {
                var pm=Move.None;if(Panic(v,time,ref pm))return pm;
            }
            var op=v.Phase(CyberLordSpec.Four,time);
            if(op.Live&&!(v.PanicConnect>-90&&time>=v.PanicConnect&&v.PanicConnect>=op.At-.01f))return Opener(v,op,time);
            // Kit v2 (Core/CyberKit2MeleeMotion.cs): its abilities and five-step string replace v1's Q to 3 and combo.
            if(v.Kit2.Live&&!legacy){var k2=CyberKit2MeleeMotion.Of(v,time);float k2End=k2.Strike>0&&k2.Strike-1<CyberCombo.Kinds?Math.Min(k2.Swing.Until,v.Kit2.ComboUntil):k2.Swing.Until;if(k2.Active&&!(blocking&&time>=k2End))return k2;if(blocking)return new Move{Clip=Guard,Time=time,Weight=Ease((time-blockAt)/BlendIn),State="cyber-guard",From=-1,StrikeFrom=-1};return m;}
            // The newest live ability (to the end of its recovery and blend out), unless a combo swing started after it.
            int newest=-1;float at=-100;
            for(int s=0;s<CyberLordSpec.Four;s++){float start=v.At[s],end=Math.Max(v.Until[s],v.RecoverUntil[s]);if(start>-90&&time>=start&&time<end+BlendOut&&start>at){newest=s;at=start;}}
            bool combo=v.ComboAt>-90&&time>=v.ComboAt&&time<v.ComboUntil+(legacy?BlendOut:CyberCombo.Settle);
            // (A strike's settle yields to a raised guard at once.)
            if(combo&&!legacy&&blocking&&time>=v.ComboUntil)combo=false;
            if(newest>=0&&!(combo&&v.ComboAt>at))return Ability(v,newest,time);
            if(combo)return legacy?ComboMove(v,time):StrikeMove(v,time);
            if(blocking){var g=new Move{Clip=Guard,Time=time,Weight=Ease((time-blockAt)/BlendIn),State="cyber-guard",From=-1};return g;}
            return m;
        }
        // The combo: the take's impact on the replicated hit, the start of the take on the swing's start (a chained step blends
        // from the previous step's end), the follow-through over the rest of the swing, then out to the stance.
        private static Move ComboMove(CyberLordView v,float time)
        {
            int step=v.ComboStep,clip=ComboClip(step);var c=Clip(clip);float dur=Math.Max(.05f,v.ComboUntil-v.ComboAt),hit=Math.Max(v.ComboAt+.02f,Math.Min(v.ComboUntil-.02f,v.ComboHit));
            float lead=Impact(clip)*(step==2?.55f:.45f);
            knots[0]=v.ComboAt;knots[1]=hit;knots[2]=v.ComboUntil;marks[0]=lead;marks[1]=Impact(clip);marks[2]=Math.Min(c.Duration,Impact(clip)+(c.Duration-Impact(clip))*.75f);
            var m=new Move{Clip=clip,Time=Retime(3,time),Legs=0,Root=step==2?.8f:.6f,State=step==0?"cyber-jab":step==1?"cyber-cross":"cyber-palm",From=-1};
            m.Weight=Ease(Ramp(time,v.ComboAt,v.ComboAt+BlendIn))*(1-Ease(Ramp(time,v.ComboUntil,v.ComboUntil+BlendOut)));
            // A chained step starts from the previous step's last pose, not the stance.
            if(step>0&&time<v.ComboAt+ChainBlend){int prev=ComboClip(step-1);var pc=Clip(prev);m.From=prev;m.FromTime=Math.Min(pc.Duration,Impact(prev)+(pc.Duration-Impact(prev))*.75f);m.FromWeight=1-Ease(Ramp(time,v.ComboAt,v.ComboAt+ChainBlend));m.Weight=1;}
            return m;
        }
        // The hardlight string (Core/CyberLordCombo.cs): the step's authored strike on its replicated swing, eased in from the
        // stance (a first step) or carried on from the step before (a chained one: the caller blends from where the rig was),
        // and back out to the stance over CyberCombo.Settle after the swing.
        private static Move StrikeMove(CyberLordView v,float time)
        {
            int kind=CyberCombo.V1Kind(v.ComboStep);var m=Move.None;m.Strike=kind+1;m.Swing=new CyberCombo.Swing(kind,v.ComboAt,Math.Max(v.ComboAt+.02f,Math.Min(v.ComboUntil-.02f,v.ComboHit)),v.ComboUntil);
            m.StrikeFrom=v.ComboStep>0?CyberCombo.Previous(kind,false):-1;m.State=CyberCombo.States[kind];m.Legs=0;m.Root=1;m.Time=time;
            m.FadeOut=1-Ease(Ramp(time,v.ComboUntil,v.ComboUntil+CyberCombo.Settle));m.Weight=(m.StrikeFrom>=0?1:StrikeIn(m.Swing,time))*m.FadeOut;
            return m;
        }
        // Each ability's take on its cast (windup to the take's impact), active and recovery windows.
        private static readonly int[] AbilityClip={Dash,Firewall,Burst,Siphon,Crash,Lunge};
        private static readonly string[] AbilityState={"cyber-data-dash","cyber-firewall","cyber-packet-burst","cyber-siphon-link","cyber-system-crash","cyber-kernel-panic"};
        private static Move Ability(CyberLordView v,int slot,float time)
        {
            int clip=AbilityClip[slot];var c=Clip(clip);float impact=Impact(clip),end=c.Duration;var p=v.Phase(slot,time);
            float at=v.At[slot],rel=v.Release[slot]>at?v.Release[slot]:at+.001f,until=Math.Max(rel+.001f,v.Until[slot]),fin=Math.Max(until+.001f,v.RecoverUntil[slot]);
            float lead=0,legs=0,root=.7f;
            switch(slot)
            {
                // The dash: a low push-off over the windup, the glide through the active, the stand up in the recovery.
                case CyberLordSpec.Q:lead=.05f;knots[0]=at;knots[1]=rel;knots[2]=until;knots[3]=fin;marks[0]=lead;marks[1]=impact;marks[2]=Math.Min(end,impact+.25f);marks[3]=end;root=.9f;break;
                // The wall: both hands raised and thrust as it forms (the take's release on the replicated release), held.
                case CyberLordSpec.E:knots[0]=at;knots[1]=rel;knots[2]=until;knots[3]=fin;marks[0]=0;marks[1]=impact;marks[2]=end;marks[3]=end;break;
                // The shards from both palms on the release.
                case CyberLordSpec.One:knots[0]=at;knots[1]=rel;knots[2]=until;knots[3]=fin;marks[0]=impact*.2f;marks[1]=impact;marks[2]=end;marks[3]=end;break;
                // The link: the reach to the release, held through the drain.
                case CyberLordSpec.Two:knots[0]=at;knots[1]=rel;knots[2]=until;knots[3]=fin;marks[0]=0;marks[1]=end;marks[2]=end;marks[3]=end;break;
                // The slam: the windup gathers (the take's raise), the impact on the release, a low hold.
                case CyberLordSpec.Three:knots[0]=at;knots[1]=rel;knots[2]=until;knots[3]=fin;marks[0]=0;marks[1]=impact;marks[2]=end;marks[3]=end;legs=.55f;root=1;break;
                default:knots[0]=at;knots[1]=fin;marks[0]=0;marks[1]=end;break;
            }
            // A cast with no windup (the dash, the wall) is already at its take's impact as it starts (the blend in eases it in).
            if(slot<CyberLordSpec.Four&&!(v.Release[slot]>at+1e-4f))marks[0]=marks[1];
            var m=new Move{Clip=clip,Time=Retime(slot>=CyberLordSpec.Four?2:4,time),Legs=legs,Root=root,State=AbilityState[slot],From=-1};
            m.Weight=Ease(Ramp(time,at,at+BlendIn*1.4f))*(1-Ease(Ramp(time,fin,fin+BlendOut)));
            // The link's held reach pulls back in small tugs as the packets stream home.
            if(slot==CyberLordSpec.Two&&time>=rel&&time<until)m.Time=end-.04f*(.5f+.5f*(float)Math.Sin((time-rel)*14));
            return m;
        }
        // Kernel Panic's opener: the cocked windup, the lunge to its punch on the active's end, and on a whiff the stumble out.
        private static Move Opener(CyberLordView v,CyberPhase p,float time)
        {
            int clip=Lunge;var c=Clip(clip);float impact=Impact(clip);
            float at=p.At,rel=p.Release>at?p.Release:at+.001f,until=Math.Max(rel+.001f,p.Until),fin=Math.Max(until+.001f,p.End);
            knots[0]=at;knots[1]=rel;knots[2]=until;knots[3]=fin;marks[0]=0;marks[1]=impact*.45f;marks[2]=impact;marks[3]=c.Duration;
            var m=new Move{Clip=clip,Time=Retime(4,time),Legs=.35f,Root=.9f,State=v.PanicOutcome==CyberLordSpec.Whiff&&time>=until?"cyber-kernel-whiff":"cyber-kernel-lunge",From=-1};
            m.Weight=Ease(Ramp(time,at,at+BlendIn))*(1-Ease(Ramp(time,fin,fin+BlendOut)));return m;
        }
        /// <summary>The caster through the cut-in and the sequence (KernelPanicPath.Steps): the lunge frozen on its punch (a
        /// hardlight tremor), the Breaker, the blink-behind rising kick, the blink-above hammer-fist, out of sight through the
        /// fork bomb, the slam into the code column, the recovery.</summary>
        public const float KickLead=.1f,HammerLead=.08f;
        private static bool Panic(CyberLordView v,float time,ref Move m)
        {
            float age=v.PanicAge(time);if(float.IsNaN(age))return false;var S=KernelPanicPath.Steps;m.From=-1;m.Weight=1;
            // Ended early (a disconnect, the round's end): the pose eases back to the stance from wherever it was.
            if(v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90&&time>=v.PanicEndedAt){m.Clip=Breaker;m.Time=Clip(Breaker).Duration;m.Weight=1-Ease(Ramp(time,v.PanicEndedAt,v.PanicEndedAt+BlendOut));m.State="cyber-kernel-ended";return m.Weight>0;}
            if(age<-.12f){m.Clip=Lunge;m.Time=Impact(Lunge)+.012f*(float)Math.Sin(time*90);m.Legs=.35f;m.Root=.9f;m.State="cyber-kernel-cut-in";return true;}
            // Astra's hits land on the step starts (KernelPanicPath.HitAge): each later take winds up in the last moment of the
            // previous step and strikes as the host teleports the body in.
            if(age<S[1]-KickLead){knots[0]=-.12f;knots[1]=0;knots[2]=S[1]-KickLead;marks[0]=Impact(Breaker)*.4f;marks[1]=Impact(Breaker);marks[2]=Clip(Breaker).Duration*.85f;m.Clip=Breaker;m.Time=Retime(3,age);m.Legs=.3f;m.Root=.9f;m.State="cyber-kernel-breaker";
                if(age<-.12f+ChainBlend){m.From=Lunge;m.FromTime=Impact(Lunge);m.FromWeight=1-Ease(Ramp(age,-.12f,-.12f+ChainBlend));}return true;}
            if(age<S[2]-HammerLead){knots[0]=S[1]-KickLead;knots[1]=KernelPanicPath.HitAge(2);knots[2]=S[2]-HammerLead;marks[0]=Impact(Kick)*.6f;marks[1]=Impact(Kick);marks[2]=Clip(Kick).Duration;m.Clip=Kick;m.Time=Retime(3,age);m.Legs=1;m.Root=1;m.State="cyber-kernel-kick";return true;}
            if(age<S[3]){knots[0]=S[2]-HammerLead;knots[1]=KernelPanicPath.HitAge(3);knots[2]=S[3];marks[0]=Impact(Hammer)*.3f;marks[1]=Impact(Hammer);marks[2]=Clip(Hammer).Duration;m.Clip=Hammer;m.Time=Retime(3,age);m.Legs=1;m.Root=1;m.State="cyber-kernel-hammer";return true;}
            if(age<S[4]-.08f){m.Clip=Hammer;m.Time=Clip(Hammer).Duration;m.Legs=1;m.Root=1;m.State="cyber-kernel-fork-bomb";return true;}
            if(age<KernelPanicPath.Recovery){knots[0]=S[4]-.08f;knots[1]=S[4];knots[2]=KernelPanicPath.Recovery;marks[0]=Impact(Slam)*.55f;marks[1]=Impact(Slam);marks[2]=Clip(Slam).Duration;m.Clip=Slam;m.Time=Retime(3,age);m.Legs=.6f;m.Root=1;m.State="cyber-kernel-slam";return true;}
            if(age<KernelPanicPath.End+BlendOut){m.Clip=Slam;m.Time=Clip(Slam).Duration;m.Legs=.6f*(1-Ramp(age,KernelPanicPath.Recovery,KernelPanicPath.End));m.Root=1;m.Weight=1-Ease(Ramp(age,KernelPanicPath.Recovery+.2f,KernelPanicPath.End+BlendOut));m.State="cyber-kernel-recovery";return true;}
            return false;
        }

        /// <summary>
        /// The Kernel Panic victim's tumble `age` s into the caster's sequence (any pilot class), for VictimPose: recoiling
        /// through the cut-in, launched back by the Breaker (flattened on a wall-splat), kicked up spinning, hammered head
        /// down, jolted by each clone's strike, slammed flat into the column, lying glitched through the recovery.
        /// </summary>
        public static Move Victim(CyberLordView v,float time)
        {
            var m=Move.None;if(!v.PanicLive(time)&&!(v.PanicStart>-90&&time>=v.PanicStart&&time<v.PanicStart+KernelPanicPath.End+.35f))return m;
            float age=v.PanicAge(time);if(float.IsNaN(age))return m;var S=KernelPanicPath.Steps;m.Weight=1;m.Legs=1;m.Root=1;m.From=-1;
            if(v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90&&time>=v.PanicEndedAt){m.Clip=Knockdown;m.Time=Clip(Knockdown).Duration;m.Weight=1-Ease(Ramp(time,v.PanicEndedAt,v.PanicEndedAt+.3f));m.State="kernel-victim-ended";return m;}
            var launched=Clip(Launched);var down=Clip(Knockdown);
            if(age<0){m.Clip=Launched;m.Time=.06f;m.Pitch=-.12f;m.State="kernel-victim-held";return m;}
            if(age<S[1])
            {
                float u=age/S[1];m.Clip=Launched;m.Time=.06f+.55f*Ease(u);m.Pitch=-.25f-.65f*Ease(u);m.State="kernel-victim-launched";
                // A wall stops the flight: flat against it from the splat, arms flung.
                if(v.PanicWall&&u>.62f){float k=Ease(Ramp(u,.62f,.7f));m.Pitch=-.25f-.65f*Ease(.62f)+(.08f+.65f*Ease(.62f))*k;m.Time=.06f+.55f*Ease(.62f);m.Roll=.06f*k;m.State="kernel-victim-wall-splat";}
                return m;
            }
            if(age<S[2]){float u=(age-S[1])/(S[2]-S[1]);m.Clip=Launched;m.Time=.62f+.3f*u;m.Pitch=-.9f-MeleeMath.TAU*.9f*Ease(Ramp(u,.08f,1));m.State="kernel-victim-kicked";return m;}
            if(age<S[3]){float u=(age-S[2])/(S[3]-S[2]);m.Clip=Knockdown;m.Time=.1f+.25f*u;m.Pitch=MeleeMath.Lerp(-.4f,1.15f,Ease(Ramp(u,0,.35f)));m.Spin=.4f*u;m.State="kernel-victim-hammered";return m;}
            if(age<S[4])
            {
                float u=(age-S[3])/(S[4]-S[3]);m.Clip=Knockdown;m.Time=.35f+.3f*u;m.Pitch=1.15f-.35f*u;m.Spin=.4f;
                // Each clone's strike jolts the falling body (its own side: a roll toward it).
                for(int i=0;i<KernelPanicPath.Clones.Length;i++){float s=age-KernelPanicPath.Clones[i];if(s>=0&&s<.16f){float k=(float)Math.Sin(s/.16f*Math.PI);m.Roll+=(i==1?-1:1)*.35f*k;m.Pitch+=.15f*k;m.Spin+=.25f*k*(i==2?-1:1);}}
                m.State="kernel-victim-fork-bomb";return m;
            }
            // Slammed flat into the column, then lying (the take's floor frames) until the sequence ends.
            {float u=Ramp(age,S[4],S[4]+.12f);m.Clip=Knockdown;m.Time=MeleeMath.Lerp(.65f,down.Duration*.72f,Ease(u))+Math.Max(0,age-(S[4]+.12f))*.15f;m.Time=Math.Min(m.Time,down.Duration);m.Pitch=.8f*(1-Ease(u));m.Spin=.4f;
             m.Weight=1-Ease(Ramp(age,KernelPanicPath.End,KernelPanicPath.End+.35f));m.State="kernel-victim-slammed";}
            return m;
        }

        // ---- the hardlight strikes on a rig (Core/CyberLordCombo.cs) ----
        /// <summary>
        /// Two rows blended for a hardlight strike: as MeleeCapture.BlendRowsTurning, but each hand's whole frame (the right's
        /// blade and edge, the left's forward and up) turns as one rotation and each elbow's pole along the great circle, so a fist
        /// rising from the hanging idle into the guard (frames half a turn apart) never flips its wrist or elbow mid-blend.
        /// `o` may be `a` or `b`.
        /// </summary>
        public static void StrikeBlend(float[] a,float[] b,float k,float[] o)
        {
            if(k<=0){if(o!=a)Array.Copy(a,o,o.Length);return;}if(k>=1){if(o!=b)Array.Copy(b,o,o.Length);return;}
            Quaternion ra=HandFrame(a,MeleeCapture.Blade,MeleeCapture.Edge,true),rb=HandFrame(b,MeleeCapture.Blade,MeleeCapture.Edge,true);
            Quaternion la=HandFrame(a,MeleeCapture.LFwd,MeleeCapture.LUp,false),lb=HandFrame(b,MeleeCapture.LFwd,MeleeCapture.LUp,false);
            Vector3 rpa=Ch(a,MeleeCapture.RPole),rpb=Ch(b,MeleeCapture.RPole),lpa=Ch(a,MeleeCapture.LPole),lpb=Ch(b,MeleeCapture.LPole);
            Vector3 rha=Ch(a,MeleeCapture.Grip),rhb=Ch(b,MeleeCapture.Grip),lha=Ch(a,MeleeCapture.LHand),lhb=Ch(b,MeleeCapture.LHand);
            MeleeCapture.BlendRows(a,b,k,o);
            // the fists swing round their shoulders (direction turned, distance lerped), not straight through them
            Put(o,MeleeCapture.Grip,Swing(rha,rhb,k,RShoulder));Put(o,MeleeCapture.LHand,Swing(lha,lhb,k,LShoulder));
            var r=MeleeMath.Slerp(ra,rb,k);Put(o,MeleeCapture.Blade,Vector3.Transform(Vector3.UnitZ,r));Put(o,MeleeCapture.Edge,Vector3.Transform(Vector3.UnitX,r));
            var l=MeleeMath.Slerp(la,lb,k);Put(o,MeleeCapture.LFwd,Vector3.Transform(Vector3.UnitZ,l));Put(o,MeleeCapture.LUp,Vector3.Transform(Vector3.UnitY,l));
            Put(o,MeleeCapture.RPole,TurnPole(rpa,rpb,k));Put(o,MeleeCapture.LPole,TurnPole(lpa,lpb,k));
        }
        private static Vector3 Ch(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static void Put(float[] f,int i,Vector3 v){f[i]=v.X;f[i+1]=v.Y;f[i+2]=v.Z;}
        // A hand's frame from its main axis (z) and its second (the right's edge: x; the left's up: y).
        private static Quaternion HandFrame(float[] f,int main,int second,bool secondIsX)
        {
            var z=Ch(f,main);if(z.LengthSquared()<1e-10f)z=Vector3.UnitZ;z=Vector3.Normalize(z);var s=Ch(f,second);s-=z*Vector3.Dot(s,z);
            if(s.LengthSquared()<1e-8f){s=Math.Abs(z.Y)<.9f?Vector3.UnitY:Vector3.UnitX;s-=z*Vector3.Dot(s,z);}s=Vector3.Normalize(s);
            return secondIsX?MeleeMath.Basis(s,Vector3.Cross(z,s),z):MeleeMath.Basis(Vector3.Cross(s,z),s,z);
        }
        // The shoulders in chest space (as MeleeCapture's poles read them; +x is the pilot's left).
        private static readonly Vector3 LShoulder=new Vector3(.345f,.18f,0),RShoulder=new Vector3(-.345f,.18f,0);
        private static Vector3 Swing(Vector3 a,Vector3 b,float k,Vector3 shoulder)
        {
            Vector3 da=a-shoulder,db=b-shoulder;float la=da.Length(),lb=db.Length();if(la<1e-3f||lb<1e-3f)return Vector3.Lerp(a,b,k);
            var d=Vector3.Transform(da/la,MeleeMath.Slerp(Quaternion.Identity,MeleeMath.FromTo(da/la,db/lb),k));return shoulder+d*(la+(lb-la)*k);
        }
        // A pole hint along the great circle (its length lerped); a missing one takes the other's direction.
        private static Vector3 TurnPole(Vector3 a,Vector3 b,float k)
        {
            float la=a.Length(),lb=b.Length();if(la<1e-4f||lb<1e-4f)return Vector3.Lerp(a,b,k);
            var d=Vector3.Transform(a/la,MeleeMath.Slerp(Quaternion.Identity,MeleeMath.FromTo(a/la,b/lb),k));return d*(la+(lb-la)*k);
        }
        /// <summary>A rig's memory of the row it showed last frame, so a strike carries on from wherever the body was as it began
        /// (a chained step, a guard, a cast, the run); without one a chained step starts from its predecessor's ready pose.
        /// Presentation state only: a rewound clock starts the strike over.</summary>
        private sealed class StrikeMemory
        {
            private readonly float[] last=new float[CapturedClip.Channels],from=new float[CapturedClip.Channels],pose=new float[CyberCombo.N],scratch=new float[CyberCombo.N];
            private float lastAt=-100,fromAt=-100;private bool hasFrom,fromIdle;private Vector2 lastShift,fromShift;
            /// <summary>The strike's hips' drop under the guard this frame, weighted (the first-person view's dip on a plant).</summary>
            public float Drop;
            public void Note(float[] row,float time,Vector2 shift){Array.Copy(row,last,last.Length);lastAt=time;lastShift=shift;}
            /// <summary>The strike's row at `time` (its torso turn at `torso`; `view`: the first-person view model) and the body's
            /// shift; returns its weight over the base. `stutter`: the windup runs on the frame-skip clock (CyberCombo.Stutter).
            /// `idle`: the idle stance's weight under it (a strike begun out of the low idle spreads its arms' rise over the windup).</summary>
            public float Row(in Move m,float time,float torso,float[] into,out Vector2 shift,out float air,bool view=false,bool stutter=false,float idle=0)
            {
                var sw=m.Swing;
                if(Math.Abs(sw.At-fromAt)>1e-4f||time<fromAt-1e-3f){fromAt=sw.At;hasFrom=lastAt>-90&&time>=lastAt-1e-4f&&time-lastAt<.1f;if(hasFrom){Array.Copy(last,from,from.Length);fromShift=lastShift;}fromIdle=idle>IdleSmooth;}
                bool canonical=!hasFrom&&m.StrikeFrom>=0;float clock=stutter?CyberCombo.Stutter(sw,time):time;
                CyberCombo.Pose(sw,clock,pose,canonical?sw.Before(m.StrikeFrom):sw,canonical,scratch);CyberCombo.Row(pose,torso,into,view);
                // (a first step's ease in from the stance steps on the same frame-skip clock, so its holds hold; out of the low idle
                // the arms have half a metre to rise, so the ease spreads evenly over the whole windup: the snaps stay, a third each)
                float w=hasFrom?m.FadeOut:clock!=time&&m.StrikeFrom<0?StrikeIn(sw,clock)*m.FadeOut:m.Weight;shift=new Vector2(pose[CyberCombo.ShX],pose[CyberCombo.ShZ]);air=Clamp01(pose[CyberCombo.Air]);
                if(hasFrom){float u=(clock-sw.At)/Math.Max(1e-4f,(sw.Hit-sw.At)*(fromIdle?1:CyberCombo.ChainShare)),k=fromIdle?Clamp01(u):Ease(u);if(k<1){StrikeBlend(from,into,k,into);shift=Vector2.Lerp(fromShift,shift,k);}}
                shift*=w;Drop=pose[CyberCombo.Drop]*w;return w;
            }
        }
        private readonly StrikeMemory third=new StrikeMemory(),first=new StrikeMemory();
        /// <summary>The idle stance's weight above which a strike begun from it spreads its arms' rise evenly over the windup.</summary>
        public const float IdleSmooth=.3f;
        /// <summary>First person: the share of the strike's body drive (along the facing) and of its drop that the view model's
        /// fists take (chest space; the camera never moves).</summary>
        public const float ViewDrive=.7f,ViewDip=.35f;
        /// <summary>The strikes' feet on this rig.</summary>
        public readonly CyberComboFeet ComboFeet=new CyberComboFeet();

        // ---- applying a move to the gait rig ----
        private readonly float[] row=new float[CapturedClip.Channels],baseRow=new float[CapturedClip.Channels],fromRow=new float[CapturedClip.Channels],runRow=new float[CapturedClip.Channels],heldRow=new float[CapturedClip.Channels];
        private readonly CyberLordView view=new CyberLordView();
        public CyberLordView View=>view;
        public string State{get;private set;}="cyber-stance";
        /// <summary>
        /// The voxel body's posture over the pack's hunched combat takes: the spine's and chest's forward bend kept at
        /// SpineKeep / ChestKeep and the neck's and head's at HeadKeep (the voxel head is a big block: a human's chin-down guard
        /// reads as staring at the floor). The arms are chest-space targets, so the fists keep their captured line.
        /// </summary>
        public const float SpineKeep=.6f,ChestKeep=.75f,HeadKeep=.35f;
        public static void Posture(float[] row)
        {
            row[MeleeCapture.Spine]*=SpineKeep;row[MeleeCapture.Chest]*=ChestKeep;row[MeleeCapture.Neck]*=HeadKeep;row[MeleeCapture.Head]*=HeadKeep;
        }
        /// <summary>
        /// The head back onto the pilot's facing: the pack's bladed takes turn the chest toward the rear hand while the head
        /// watches the opponent, and the stance's heading carried by the torso swung the head 55 degrees off the facing. The
        /// neck turns the head's heading `weight` of the way back to the rig's forward (group space), about the vertical.
        /// </summary>
        public static void FaceForward(MeleeRig r,float weight)
        {
            var head=r.ChainQuat(CapturedRig.Head,-1);var f=Vector3.Transform(Vector3.UnitZ,head);if(f.X*f.X+f.Z*f.Z<1e-6f)return;
            float yaw=(float)Math.Atan2(f.X,f.Z)*weight;var parent=r.ChainQuat(CapturedRig.Chest,-1);
            r.SetQ(CapturedRig.Neck,MeleeMath.Inverse(parent)*MeleeMath.AxisAngle(MeleeMath.Y,-yaw)*parent*r.Q[CapturedRig.Neck]);
        }
        public const float FaceWeight=.85f;
        /// <summary>First person: the fists raised and brought in so the guard sits in the lower view (chest space, m).</summary>
        public static readonly Vector3 ViewLift=new Vector3(0,.08f,.22f);
        /// <summary>How much of ViewLift's push toward the view a step of the hardlight string takes off (first person).</summary>
        public const float StrikePush=.9f;private float viewStrike,viewDrive,viewDip;
        /// <summary>The stance row at `time`: the alert take played forward and back (it is not a loop), its middle held longest.</summary>
        public static void StanceRow(float time,float[] into)
        {
            var c=Clip(Stance);float d=c.Duration,u=CapturedClip.Wrap(time*.55f,2*d);float t=u<d?u:2*d-u;
            // The take's opening settles into its guard: play its settled stretch only (.35 .. .95 of it).
            c.Sample(d*(.35f+.6f*(t/d)),into);GuardUp(into,1);
        }
        /// <summary>The fists raised from the take's low alert guard to cheek and chin height, the lead fist brought in toward the
        /// centre line (chest space, m): a brawler's high guard. `k` is the share (the run carry keeps less of it).</summary>
        public static readonly Vector3 LeadLift=new Vector3(-.09f,.17f,.04f),RearLift=new Vector3(.04f,.14f,.02f);
        public static void GuardUp(float[] row,float k)
        {
            row[MeleeCapture.LHand]+=LeadLift.X*k;row[MeleeCapture.LHand+1]+=LeadLift.Y*k;row[MeleeCapture.LHand+2]+=LeadLift.Z*k;
            row[MeleeCapture.Grip]+=RearLift.X*k;row[MeleeCapture.Grip+1]+=RearLift.Y*k;row[MeleeCapture.Grip+2]+=RearLift.Z*k;
        }
        /// <summary>The base (no move): the stance, carried by the Runner pack's run and sprint (Core/CyberLordRun.cs) at the gait's
        /// stride as the pilot travels.</summary>
        private readonly CyberLordRunCarry carry=new CyberLordRunCarry();
        /// <summary>The travel carry on this rig (its direction, bracketing takes and sprint weight, as the last frame left them).</summary>
        public CyberLordRunCarry Carry=>carry;
        public void BaseRow(MeleeRig r,Actor a,float time,float[] into,out float travel)
        {
            StanceRow(time,into);travel=Ease((r.Motion-.10f)/.65f);carry.Weight=travel;if(travel<=1e-4f)return;
            carry.Row(r,a,time,runRow);MeleeCapture.BlendRowsTurning(into,runRow,travel,into);
        }
        /// <summary>
        /// The style layer on the fighter route (between MeleeMotion.FighterBase and FighterFinish): the base, the move over it,
        /// applied to the gait rig (the legs the gait's except where a move owns them). Returns the presented state.
        /// </summary>
        /// <summary>The idle stance (Core/CyberLordIdle.cs): its tracker on this rig, this frame's weight and glitch, and whether
        /// effects are reduced (the runtime sets it before posing: the glitch's and the twitches' softer versions).</summary>
        public readonly CyberLordIdle Idle=new CyberLordIdle();public float IdleWeight{get;private set;}public CyberIdleGlitch IdleGlitch;public bool Reduced;
        private float idleUnder,idleTime,idlePitch;private int idleId;private MeleeRig idleRig;private readonly Action idleLayer;
        public CyberLordMotion(){idleLayer=()=>CyberLordIdlePose.Layer(idleRig,idleUnder,idleTime,idleId,Reduced,idlePitch,IdleGlitch);}
        /// <summary>The second jump's hardlight boost (Core/CyberLordRun.cs CyberLordAirBoost): this frame's weight, the Dev Sandbox
        /// preview's stand-in air-jump time (-100: none; the runtime sets it for the local pilot only), and the jump track the
        /// boost reads when the route has no jump layer (the first-person view model).</summary>
        public float BoostWeight{get;private set;}public float PreviewAirJumpAt=-100;private readonly NativeJumpTrack ownTrack=new NativeJumpTrack();private readonly float[] boostRow=new float[CapturedClip.Channels];
        /// <summary>The boost's take over `row` at the boost's weight on the track (the torso at CyberLordAirBoost.Torso, the fists at
        /// Arms; `arms` alone: the first-person view keeps only the fists' share).</summary>
        private float BoostRow(NativeJumpTrack t,float time,bool blocked,float[] row,bool armsOnly,float share)
        {
            float age=CyberLordAirBoost.Age(t,time),w=blocked||float.IsNaN(age)?0:CyberLordAirBoost.Weight(age)*share;if(w<=1e-4f)return 0;
            CyberLordAirBoost.Clip.Sample(CyberLordAirBoost.TakeTime(age),boostRow);
            for(int i=armsOnly?MeleeCapture.Grip:0;i<CapturedClip.Channels;i++){float k=i<MeleeCapture.Grip?CyberLordAirBoost.Torso:CyberLordAirBoost.Arms;boostRow[i]=row[i]+(boostRow[i]-row[i])*k;}
            MeleeCapture.BlendRowsTurning(row,boostRow,w,row);return w;
        }
        /// <summary>The ranged layer's state this frame (null in melee mode).</summary>
        public string RangedState{get;private set;}
        public string Style(MeleeMotion motion,Actor a,float time,bool stun)
        {
            var r=motion.Rig;CyberLordFeed.Read(a,view);motion.FighterSeat=carry;ComboFeet.Before(r,time);
            // The idle stance (CyberLordIdle): its weight first, so the base under it is the sword idle (no alert bob in the hips).
            float idle=Idle.Update(a,view,time,Ease((r.Motion-.10f)/.65f),stun);
            // The ranged mode holds its emitters up instead (Core/CyberLordRangedMotion.cs): no idle stance there.
            idle*=1-view.ModeWeight(time);
            BaseRow(r,a,time,baseRow,out float travel);if(idle>1e-4f){CyberLordIdlePose.Base(time,heldRow);MeleeCapture.BlendRowsTurning(baseRow,heldRow,idle,baseRow);}
            // The second jump's boost over the base: the rig's own jump track (the jump layer's, observed here first with the same
            // replicated signal, so the torso and the legs start on the same frame).
            bool airBlocked=stun||a.Hp<=0||a.Rolling(time)||a.JuggleUntil>time||NativeSpecialPose.Active(a,time)||NativeSpecialPose.Down(a,time);
            var track=motion.JumpLayer?.Track??ownTrack;track.Explicit=true;track.Observe(time,a.Grounded,r.GroupPosition.Y,airBlocked);track.AirJumpSignal(CyberLordAirBoost.Signal(a,PreviewAirJumpAt));
            BoostWeight=BoostRow(track,time,airBlocked,baseRow,false,1);
            bool blocking=!stun&&time<a.BlockUntil;var m=stun?Move.None:Of(view,time,blocking,a.BlockAt>-90?a.BlockAt:a.BlockUntil-Rules.BlockDuration,LegacyCombo(a.Id));
            float legs=0,root=1-travel*.85f,strike=0,air=0;var shift=Vector2.Zero;
            if(m.Strike>0&&m.Weight>1e-4f)
            {
                // A hardlight strike is authored on the voxel body already (no posture), over the postured base.
                baseRow[MeleeCapture.Yaw]=0;Posture(baseRow);strike=third.Row(m,time,1,row,out shift,out air,false,!Reduced,idle);
                StrikeBlend(baseRow,row,strike,row);if(m.Root>=1)root=MeleeMath.Lerp(root,1,strike);legs=air*strike;
            }
            else
            {
                if(m.Clip>=0&&m.Weight>1e-4f)
                {
                    Clip(m.Clip).Sample(m.Time,row);
                    if(m.From>=0&&m.FromWeight>1e-4f){Clip(m.From).Sample(m.FromTime,fromRow);MeleeCapture.BlendRowsTurning(row,fromRow,m.FromWeight,row);}
                    MeleeCapture.BlendRowsTurning(baseRow,row,m.Weight,row);legs=m.Legs*m.Weight;root=MeleeMath.Lerp(root,m.Root,m.Weight);
                }
                else Array.Copy(baseRow,row,CapturedClip.Channels);
                // The capture's heading is the stance's (carried by the torso): the pelvis keeps the gait's facing.
                row[MeleeCapture.Yaw]=0;Posture(row);
            }
            third.Note(row,time,shift);
            if(!a.Grounded&&!m.Active)root*=.3f;
            MeleeCapture.Apply(r,Unarmed,row,CaptureOptions.Of(1,Unarmed.Fit,MeleeClear.None,legs:legs,root:Math.Max(0,root)));FaceForward(r,FaceWeight*(1-.85f*strike));
            if(legs>.05f)r.LegsAuthored=true;
            // The strike's weight shift and its footwork (Core/CyberLordCombo.cs CyberComboFeet): the feet step, pivot and hop
            // under it at full weight through its settle (a fading foot would slide), and the gait keeps them exactly where they
            // were left, stepping them back to its stance on its own.
            if(strike>1e-4f){var b=r.P[CapturedRig.Body];b.X+=shift.X;b.Z+=shift.Y;r.P[CapturedRig.Body]=b;}
            ComboFeet.Stutter=!Reduced;ComboFeet.Pose(r,m.Swing,time,strike>1e-4f&&a.Grounded&&CyberCombo.PlantsFeet(m.Swing.Kind)?1-air:0);motion.Gait.SyncTolerance=ComboFeet.Active?0:MeleeGait.SyncNear;
            // The idle's twitching over the sword idle once the pilot has stood still (CyberLordIdle), under any move still blending out.
            float under=idle*(m.Clip>=0?1-Clamp01(m.Weight):1);IdleWeight=under;
            IdleGlitch=CyberLordIdlePose.Glitch(a.Id,time,under,Reduced);
            if(under>1e-4f){var tw=CyberLordIdlePose.Twitch(a.Id,time,Reduced);IdleGlitch.LClench=tw.LClench*under;IdleGlitch.RClench=tw.RClench*under;}
            // (the idle's body twitches under a strike that has planted its feet keep those feet where they stand)
            if(ComboFeet.Active&&under>1e-4f){idleUnder=under;idleTime=time;idleId=a.Id;idlePitch=a.Pitch*(float)Math.PI/180;idleRig=r;r.HoldAnkles(idleLayer,1,true);idleRig=null;}
            else CyberLordIdlePose.Layer(r,under,time,a.Id,Reduced,a.Pitch*(float)Math.PI/180,IdleGlitch);
            // The ranged mode (Core/CyberLordRangedMotion.cs): the cyber mage's stance, throws, casts and glide over the posed rig
            // (the legs stay the gait's); a melee take (the guard, Q, E) owns the frame through its weight.
            string ranged=stun?null:CyberRangedPose.Layer(r,view,time,a.Pitch*(float)Math.PI/180,m.Clip>=0?Clamp01(m.Weight):0,new CyberRangedPose.Gait{Travel=travel,Stride=r.Stride,Sprint=carry.Sprint*travel},false,Reduced);
            RangedState=ranged;
            State=stun?"hit reaction":m.State??(ranged!=null&&ranged!="cyber-ranged-aim"?ranged:null)??(blocking?"cyber-guard":BoostWeight>.5f?"cyber-air-boost":travel>.5f?(a.Sprinting?"cyber-sprint":ranged!=null?"cyber-ranged-run":"cyber-run"):ranged??(under>.5f?"cyber-idle":"cyber-stance"));return State;
        }
        /// <summary>
        /// The first-person view model (the generic route's view rig): the same base and move with the legs and root left out,
        /// the chest bowed so the fists sit in view, low and centred (NativeViewBase.Fighter), and the torso settled.
        /// </summary>
        private readonly MeleeRig viewRig=new MeleeRig();
        public string FirstPerson(CapturedRig.Pose pose,Actor a,float time)
        {
            CyberLordFeed.Read(a,view);var r=viewRig;r.Reset();StanceRow(time,baseRow);
            bool blocking=time<a.BlockUntil,stun=a.StunUntil>time;
            // The second jump's boost: a gentler share of its fists (the view's own hop carries the body; no camera motion).
            bool airBlocked=stun||a.Hp<=0||a.Rolling(time);ownTrack.Explicit=true;ownTrack.Observe(time,a.Grounded,a.Position.y,airBlocked);ownTrack.AirJumpSignal(CyberLordAirBoost.Signal(a,PreviewAirJumpAt));
            BoostWeight=BoostRow(ownTrack,time,airBlocked,baseRow,true,CyberLordAirBoost.FirstPerson);var m=stun?Move.None:Of(view,time,blocking,a.BlockAt>-90?a.BlockAt:a.BlockUntil-Rules.BlockDuration,LegacyCombo(a.Id));
            if(m.Strike>0&&m.Weight>1e-4f)
            {
                // A strike keeps a third of its torso turn in view (built in), over the postured base kept the same way.
                baseRow[MeleeCapture.Yaw]=0;Posture(baseRow);for(int i=MeleeCapture.Pelvis;i<MeleeCapture.Neck;i++)baseRow[i]*=.35f;
                float w=first.Row(m,time,.35f,row,out var drive,out _,true);StrikeBlend(baseRow,row,w,row);viewStrike=m.Strike-1<CyberCombo.Kinds?w:0;
                viewDrive=m.Strike-1<CyberCombo.Kinds?drive.Y:0;viewDip=m.Strike-1<CyberCombo.Kinds?first.Drop:0;
            }
            else
            {
                if(m.Clip>=0&&m.Weight>1e-4f)
                {
                    Clip(m.Clip).Sample(m.Time,row);
                    if(m.From>=0&&m.FromWeight>1e-4f){Clip(m.From).Sample(m.FromTime,fromRow);MeleeCapture.BlendRowsTurning(row,fromRow,m.FromWeight,row);}
                    MeleeCapture.BlendRowsTurning(baseRow,row,m.Weight,row);
                }
                else Array.Copy(baseRow,row,CapturedClip.Channels);
                row[MeleeCapture.Yaw]=0;Posture(row);
                // First person: the torso's capture twist would swing the fists out of view; keep a third of it.
                for(int i=MeleeCapture.Pelvis;i<MeleeCapture.Neck;i++)row[i]*=.35f;
            }
            first.Note(row,time,Vector2.Zero);
            // (a strike keeps its own reach: the stance's push of the fists toward the view eases off under it, so a punch or a palm
            // drives out from a bent arm instead of an already straight one)
            float push=1-StrikePush*viewStrike;viewStrike=0;
            // (the body's drive into a strike: both fists carried forward with it and dipped as it sinks onto the plant)
            float dz=ViewDrive*viewDrive,dy=ViewDip*viewDip;viewDrive=viewDip=0;
            for(int k=0;k<3;k++){row[MeleeCapture.Grip+k]+=k==0?ViewLift.X:k==1?ViewLift.Y+dy:ViewLift.Z*push+dz;row[MeleeCapture.LHand+k]+=k==0?-ViewLift.X:k==1?ViewLift.Y+dy:ViewLift.Z*push+dz;}
            MeleeCapture.Apply(r,Unarmed,row,CaptureOptions.Of(1,Unarmed.Fit,MeleeClear.None,legs:0,root:0));
            // The ranged mode's emitters in the lower view (the camera carries the aim: no pitch here).
            string ranged=stun?null:CyberRangedPose.Layer(r,view,time,0,m.Clip>=0?Clamp01(m.Weight):0,0,true,Reduced);RangedState=ranged;
            r.WritePose(pose);NativeFirstPersonPose.SettleTorso(pose);
            State=stun?"hit reaction":m.State??ranged??(blocking?"cyber-guard":"cyber-stance");return State;
        }
        /// <summary>The upper body alone (the dev-cyberlord entrance over its keyed legs): the stance and the view's move on `r`.</summary>
        public string Upper(CyberLordView v,float time,MeleeRig r)
        {
            r.Reset();StanceRow(time,baseRow);var m=Of(v,time,false,0,true);
            if(m.Clip>=0&&m.Weight>1e-4f)
            {
                Clip(m.Clip).Sample(m.Time,row);
                if(m.From>=0&&m.FromWeight>1e-4f){Clip(m.From).Sample(m.FromTime,fromRow);MeleeCapture.BlendRowsTurning(row,fromRow,m.FromWeight,row);}
                MeleeCapture.BlendRowsTurning(baseRow,row,m.Weight,row);
            }
            else Array.Copy(baseRow,row,CapturedClip.Channels);
            row[MeleeCapture.Yaw]=0;Posture(row);MeleeCapture.Apply(r,Unarmed,row,CaptureOptions.Of(1,Unarmed.Fit,MeleeClear.None,legs:0,root:0));FaceForward(r,FaceWeight);
            return State=m.State??"cyber-stance";
        }
        /// <summary>
        /// The Kernel Panic victim layered over its own route: the tumble take's full body on a scratch rig, the tumble's
        /// pitch, roll and spin on the body, blended over the route's pose by the move's weight. False when `caster` holds no
        /// live sequence on `victim`.
        /// </summary>
        private readonly MeleeRig victimRig=new MeleeRig();private readonly CapturedRig.Pose victimPose=new CapturedRig.Pose();
        public bool VictimPose(CapturedRig.Pose pose,CyberLordView caster,int victim,float time)
        {
            if(caster==null||caster.PanicVictim!=victim||victim==0)return false;var m=Victim(caster,time);if(m.Clip<0||m.Weight<=1e-4f)return false;
            Clip(m.Clip).Sample(m.Time,row);row[MeleeCapture.Yaw]=0;Posture(row);var r=victimRig;r.Reset();
            MeleeCapture.Apply(r,Unarmed,row,CaptureOptions.Of(1,Unarmed.Fit,MeleeClear.None,legs:1,root:1));
            var tumble=MeleeMath.AxisAngle(MeleeMath.Y,m.Spin)*MeleeMath.AxisAngle(MeleeMath.X,m.Pitch)*MeleeMath.AxisAngle(MeleeMath.Z,m.Roll);
            r.SetQ(CapturedRig.Body,tumble*r.Q[CapturedRig.Body]);r.WritePose(victimPose);
            float w=Clamp01(m.Weight);
            for(int b=0;b<CapturedRig.Bones.Length;b++){pose.Rotation[b]=MeleeMath.Slerp(pose.Rotation[b],victimPose.Rotation[b],w);pose.Position[b]=Vector3.Lerp(pose.Position[b],victimPose.Position[b],w);}
            State=m.State;return true;
        }
    }

    /// <summary>The fixture stance for MeleeGait (Core/MeleeGait MeleeStance.For "cyberlord"): bladed, lead foot left, soft knees.</summary>
    public static class CyberLordStance
    {
        public static readonly MeleeStance Stance=new MeleeStance{LeadX=.16f,LeadZ=.12f,LeadToe=-.22f,RearX=-.15f,RearZ=-.18f,RearToe=.55f,Crouch=-.035f,PelvisYaw=0,Lean=.03f,KneeSoft=.1f};
    }
}

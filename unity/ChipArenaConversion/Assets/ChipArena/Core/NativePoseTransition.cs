using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// pose-transitions.ts finishPose for the gait pilots posed on a MeleeRig over the native overlays (the magic gait,
    /// MagicMotion, and the gun gait, GunMotion; captured melee keeps its own in MeleeMotion). Presentation only.
    /// The key is the pose family (transitionAction: juggle, knockdown phase, flinch, roll, guard, an E special, else base; a
    /// cast or a shot never changes it) and the live hit reaction's impact. A key change blends from the pose shown last frame
    /// over transitionDuration (a juggle or knockdown continues its joint velocities first), so leaving a juggle, roll, guard
    /// or flinch never snaps. No allocation.
    /// </summary>
    public sealed class NativePoseTransition
    {
        private bool trHas,trActive,trHasPrev,trParry;private int trId;private string trAction,trPhase;
        private float trReaction=float.NaN,trTime,trPrevTime,trAt,trDuration;
        private Quaternion[] trLast=new Quaternion[MeleeRig.N],trPrev=new Quaternion[MeleeRig.N];
        private readonly Quaternion[] trOffset=new Quaternion[MeleeRig.N],trProjected=new Quaternion[MeleeRig.N];
        private Vector3 trLastP,trPrevP,trOffsetP;
        public static string Action(Actor a,float time,bool juggled,out string phase)
        {
            phase=null;
            if(juggled)return MagicMotion.Juggle;
            if(NativeSpecialPose.Down(a,time)){phase=NativeSpecialRules.Phase(a.DownLanded,a.DownPickup,a.DownRoll,a.DownRollEnd,a.DownUsed,time);return "down";}
            if(a.StunUntil>time)return "flinch";
            if(a.Rolling(time))return "roll";
            if(time<a.BlockUntil)return "guard";
            if(NativeSpecialPose.Active(a,time))return a.SpecialKind;
            return "base";
        }
        private static bool ReactionFamily(string action)=>action==MagicMotion.Juggle||action=="down";
        public static float Duration(Actor a,float time,string action,string phase,string previous,bool reaction)
        {
            if(reaction)return Math.Min(.075f,Math.Max(.02f,(Math.Max(a.ReactionUntil,a.StunUntil)-time)*.28f));
            if(ReactionFamily(action))
            {
                float d=action!=MagicMotion.Juggle&&phase!="air"&&ReactionFamily(previous)?.08f:.09f;
                // The lying body is exact from landedAt+.25.
                if(phase=="flat"&&a.DownLanded>=0)d=Math.Min(d,a.DownLanded+.25f-time);
                return d;
            }
            if(action=="base")return .13f;
            if(action=="guard")return .08f;
            return .055f;
        }
        private static void Capture(MeleeRig rig,Quaternion[] q,out Vector3 body){Array.Copy(rig.Q,q,MeleeRig.N);body=rig.P[CapturedRig.Body];}
        public void Apply(MeleeRig r,Actor a,float time,bool juggled)
        {
            string action=Action(a,time,juggled,out string phase);
            bool reaction=a.ReactionAt>-90&&time>=a.ReactionAt&&time<Math.Max(a.ReactionUntil,a.StunUntil);
            float reactionAt=reaction?a.ReactionAt:float.NaN;bool parry=reaction&&a.ReactionParry;
            bool same=trAction==action&&trPhase==phase&&(trReaction==reactionAt||float.IsNaN(trReaction)&&float.IsNaN(reactionAt))&&trParry==parry;
            if(!trHas||time<trTime||time-trTime>.35f||a.Id!=trId)
            {
                trHas=true;trId=a.Id;trAction=action;trPhase=phase;trReaction=reactionAt;trParry=parry;
                trTime=trPrevTime=trAt=time;trDuration=0;trActive=false;trHasPrev=false;Capture(r,trLast,out trLastP);return;
            }
            if(!same)
            {
                float duration=Duration(a,time,action,phase,trAction,reaction);var incoming=trLast;Vector3 incomingP=trLastP;
                if(!reaction&&ReactionFamily(trAction)&&trHasPrev&&trTime>trPrevTime)
                {
                    // Continue the last frame's joint velocities to this frame's time, then keep only the remaining discontinuity.
                    float ratio=MeleeMath.Clamp((time-trTime)/(trTime-trPrevTime),0,2);
                    for(int i=0;i<MeleeRig.N;i++)trProjected[i]=MeleeMath.Slerp(trLast[i]*MeleeMath.Inverse(trPrev[i]),Quaternion.Identity,1-ratio)*trLast[i];
                    incoming=trProjected;incomingP=(trLastP-trPrevP)*ratio+trLastP;
                }
                trAction=action;trPhase=phase;trReaction=reactionAt;trParry=parry;trAt=time;trDuration=duration;trActive=duration>1e-4f;
                if(trActive){for(int i=0;i<MeleeRig.N;i++)trOffset[i]=MeleeMath.Inverse(r.Q[i])*incoming[i];trOffsetP=incomingP-r.P[CapturedRig.Body];}
            }
            if(trActive)
            {
                float u=MeleeMath.Ease((time-trAt)/trDuration);
                if(u>=1)trActive=false;
                else{for(int i=0;i<MeleeRig.N;i++)r.Multiply(i,MeleeMath.Slerp(trOffset[i],Quaternion.Identity,u));r.P[CapturedRig.Body]+=trOffsetP*(1-u);}
            }
            var spare=trPrev;trPrev=trLast;trLast=spare;trPrevP=trLastP;Capture(r,trLast,out trLastP);trHasPrev=true;trPrevTime=trTime;trTime=time;
        }
    }
}

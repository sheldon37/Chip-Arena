using UnityEngine;
using NV=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    // NOCTIEL in a live fight (docs/EXPEDITION-BOSS-FINAL.md, "Live motion"): Core NativeFallenLiveMotion fed from the replicated
    // actor after the binding (FallenBossViewOf) has filled the frame, and its soles kept where they stand on the water. Pure
    // presentation: no rule, hit, hitbox, damage, cooldown, movement or wire value is read back from here.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeFallenLiveMotion fbLive=new NativeFallenLiveMotion();private bool fbStandSought;
        private string fbShownClip;private float fbShownTime;private bool fbLiveNow;
        /// <summary>The live director's pose over the binding's (the fight and the draft; the intro and the death keep the script's).</summary>
        private void FallenLiveMotion(NativeArena a,FallenBossFrame f,float now)
        {
            fbLiveNow=false;f.Stand=0;var z=a?.ZoneBoss;
            bool fight=z?.fallen!=null&&!(a.Pve.Holding&&a.Pve.Cinematic=="intro")&&z.deathAt<0;
            var m=fight?a.Minions.Find(x=>x.Id==z.actor):null;
            if(m==null){fbLive.Reset();return;}
            var target=a.Pilots.Find(p=>p.Seat.id==z.targetSeat);
            var facts=new NativeFallenLiveMotion.Facts{Now=now,At=ToNV(m.Body.position),Yaw=m.Yaw,Target=target!=null?ToNV(target.Body.position):ToNV(m.Body.position)+new NV(0,0,1),
                Action=z.action,ActionAt=z.actionAt,HitAt=z.hitAt,Duration=z.duration,ActionYaw=z.actionYaw,From=new NV(z.laneX0,0,z.laneZ0),To=new NV(z.laneX1,0,z.laneZ1),
                SecondWing=NativeFallenRules.Chip(z.fallen,"second-wing")};
            // Its idle is the slow walk held with both feet down (the owner's picture): found once in the bank from the knees.
            if(float.IsNaN(fbLive.StandAt)&&VisualMotionBank!=null&&!fbStandSought){fbStandSought=true;var muscle=HumanTrait.MuscleName;fbLive.StandAt=NativeFallenLiveMotion.BothFeetDown(VisualMotionBank,System.Array.IndexOf(muscle,"Left Lower Leg Stretch"),System.Array.IndexOf(muscle,"Right Lower Leg Stretch"));}
            fbLive.Step(f,facts,VisualMotionBank,fbLive.Active?null:fbShownClip,fbShownTime);fbLiveNow=true;
        }
        /// <summary>What the body showed this frame (the director starts from it after the intro).</summary>
        private void FallenShown(FallenBossFrame f){bool to=f.Mix>=.5f;fbShownClip=to?f.ClipTo:f.ClipFrom;fbShownTime=to?f.TimeTo:f.TimeFrom;}

        // ---- the feet: a sole the clip puts on the water stays where it landed ----
        // While it stands (live, not gliding, hovering, leaping or kneeling), each foot the clip has down is held where it came
        // down (two-bone leg IK, the knee bending its own way). It lets go when the clip lifts it, when the body has moved or turned
        // far enough from it (then it steps: a short lifted arc to where the clip has it, one foot at a time), or when it leaves the
        // water. What the clip does with a foot in the air is untouched.
        private struct FbFoot{public bool Locked;public Vector3 At;public Quaternion Turn;public float Yaw,StepAt;public Vector3 From;}
        private FbFoot fbPlantL,fbPlantR;
        private const float FbStepTime=.2f;
        private void FallenFeetPlant(FallenBossFrame f,float now,float hover)
        {
            if(fbThighL==null||fbThighR==null||fbCalfL==null||fbCalfR==null)return;
            bool stand=fbLiveNow&&f.Air<.04f&&hover<=0&&f.Kneel<.3f&&!f.Gliding&&f.Plant<.5f;
            if(!fbLiveNow){fbPlantL=fbPlantR=default;fbPlantL.StepAt=fbPlantR.StepAt=-1;return;}
            FallenFootPlant(ref fbPlantL,stand,fbPlantR.StepAt>=0,fbThighL,fbCalfL,fbFootL,f,now);
            FallenFootPlant(ref fbPlantR,stand,fbPlantL.StepAt>=0,fbThighR,fbCalfR,fbFootR,f,now);
        }
        private void FallenFootPlant(ref FbFoot p,bool stand,bool otherStepping,Transform thigh,Transform calf,Transform foot,FallenBossFrame f,float now)
        {
            float s=NativeFallenBoss.Scale;var raw=foot.position;var rawTurn=foot.rotation;
            // The clip's sole on the water (the root stands the lowest sole on it, FallenPose).
            float lowest=Mathf.Min(fbFootL.position.y,fbFootR.position.y);bool down=stand&&raw.y-lowest<.05f*s;
            // Letting go is never a jump: the foot travels from where it stood to the clip's in a short lifted arc.
            if(p.Locked)
            {
                var off=raw-p.At;off.y=0;float turned=Mathf.Abs(Mathf.DeltaAngle(p.Yaw,f.Yaw));
                bool far=off.magnitude>.3f*s||turned>70,due=off.magnitude>.15f*s||turned>32;
                if(!down||far||due&&!otherStepping){p.Locked=false;p.From=p.At;p.StepAt=now;}
            }
            Vector3 goal;Quaternion turn;
            if(p.Locked){goal=p.At;turn=p.Turn;}
            else if(p.StepAt>=0)
            {
                float k=(now-p.StepAt)/FbStepTime;
                if(k>=1||k<0){p.StepAt=-1;if(down){p.Locked=true;p.At=raw;p.Turn=rawTurn;p.Yaw=f.Yaw;}return;}
                float e=k*k*(3-2*k);goal=Vector3.Lerp(p.From,raw,e)+Vector3.up*(Mathf.Sin(k*Mathf.PI)*.09f*s);turn=Quaternion.Slerp(p.Turn,rawTurn,e);
            }
            else{if(down){p.Locked=true;p.At=raw;p.Turn=rawTurn;p.Yaw=f.Yaw;}return;}
            if((goal-raw).sqrMagnitude<1e-8f)return;
            FallenLegTo(thigh,calf,foot,goal,turn);
        }
        // Two-bone leg IK: the hip stays, the knee keeps the side it bends to (forward when straight), the foot keeps `turn`.
        private void FallenLegTo(Transform thigh,Transform calf,Transform foot,Vector3 goal,Quaternion turn)
        {
            Vector3 a=thigh.position,b=calf.position,c=foot.position;float la=(b-a).magnitude,lb=(c-b).magnitude;if(la<1e-4f||lb<1e-4f)return;
            var d=goal-a;float l=Mathf.Clamp(d.magnitude,Mathf.Abs(la-lb)+.001f,la+lb-.001f);var dir=d.normalized;
            var bend=Vector3.ProjectOnPlane(b-a,dir)+Vector3.ProjectOnPlane(fbRoot.transform.forward,dir)*.05f*la;if(bend.sqrMagnitude<1e-8f)return;bend.Normalize();
            float cos=(la*la+l*l-lb*lb)/(2*la*l),sin=Mathf.Sqrt(Mathf.Max(0,1-cos*cos));var knee=a+dir*(la*cos)+bend*(la*sin);
            thigh.rotation=Quaternion.FromToRotation(b-a,knee-a)*thigh.rotation;
            calf.rotation=Quaternion.FromToRotation(foot.position-calf.position,a+dir*l-calf.position)*calf.rotation;
            foot.rotation=turn;
        }
    }
}

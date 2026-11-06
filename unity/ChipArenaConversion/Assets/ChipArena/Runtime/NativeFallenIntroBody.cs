using UnityEngine;
namespace ChipArena.UnityConversion
{
    // NOCTIEL's intro choreography on the rig (Core NativeFallenIntroMotion; docs/EXPEDITION-BOSS-FINAL.md section 18): the hips'
    // travel, the spine's lean and twist and breath, the head, both feet held by two-bone IK (the front foot never moves through the
    // rise; the back foot steps up beside it and later into the lunge), the claymore's keyed path in the right hand through the
    // existing arm IK, and the free arm thrown back for balance. Everything is in the boss's own frame, so when the cutscene ends the
    // layer holds the cut's impact a beat and then hands back to the fight's pose (the feet stepping home, not sliding), whichever
    // way the live boss then stands. Presentation only.
    public sealed partial class ArenaPresentation
    {
        private Transform fbSpine1,fbSpine3;
        private struct FbIntroFoot{public bool Held;public Vector3 Local;public Quaternion Turn;}
        private FbIntroFoot fbIntroL,fbIntroR;private FallenIntroPose fbIntroLast;private float fbIntroEnd=-1;
        private const float FbIntroHold=.25f,FbIntroRelease=.7f;

        /// <summary>The layer this frame and its weight: the cutscene's own, or the last one held then let go after it ends.</summary>
        private bool FallenIntroNow(FallenBossFrame f,float now,out FallenIntroPose pose,out float weight,out float age)
        {
            age=0;
            if(f.Intro.On){if(fbIntroEnd>=0||!fbIntroLast.On){fbIntroL=default;fbIntroR=default;}pose=f.Intro;weight=1;fbIntroLast=f.Intro;fbIntroEnd=-1;return true;}
            pose=fbIntroLast;weight=0;if(!fbIntroLast.On)return false;
            if(fbIntroEnd<0)fbIntroEnd=now;age=now-fbIntroEnd;
            if(age<0||age>FbIntroHold+FbIntroRelease){fbIntroLast.On=false;fbIntroL=default;fbIntroR=default;fbPlantL=fbPlantR=default;fbPlantL.StepAt=fbPlantR.StepAt=-1;return false;}
            float k=Mathf.Clamp01((age-FbIntroHold)/FbIntroRelease);weight=1-k*k*(3-2*k);return true;
        }
        private Vector3 FbBossLocal(Vector3 world,Vector3 at,Quaternion yawRot)=>Quaternion.Inverse(yawRot)*(world-at);

        // The hips, the back and the head: on top of the posed clip, before the feet and the arms are solved.
        private void FallenIntroBody(in FallenIntroPose ip,float w,Quaternion yawRot)
        {
            if(w<=0)return;var root=fbRoot.transform;root.position+=yawRot*(NativeBossPoseApply.ToUnity(ip.Root)*w);
            var right=yawRot*Vector3.right;float lean=ip.Lean*w,twist=ip.Twist*w;
            void Turn(Transform b,float share){if(b!=null)b.rotation=Quaternion.AngleAxis(lean*share,right)*Quaternion.AngleAxis(twist*share,Vector3.up)*b.rotation;}
            Turn(fbSpine1,.35f);Turn(fbSpine3,.35f);Turn(fbSpine,.3f);
            if(fbSpine!=null)fbSpine.rotation=Quaternion.AngleAxis(-1.2f*ip.Breath*w,right)*fbSpine.rotation;
            float head=ip.Head*w;if(fbNeck!=null)fbNeck.rotation=Quaternion.AngleAxis(head*.4f,right)*fbNeck.rotation;fbHead.rotation=Quaternion.AngleAxis(head*.6f,right)*fbHead.rotation;
        }

        // The feet: held where they stood as the rise began; the left steps to its spots; after the cutscene each steps home in turn.
        private void FallenIntroFeet(in FallenIntroPose ip,float w,float age,Vector3 at,Quaternion yawRot)
        {
            if(fbThighL==null||fbThighR==null||fbCalfL==null||fbCalfR==null)return;float s=NativeFallenBoss.Scale;
            if(ip.FootR>0&&!fbIntroR.Held)fbIntroR=new FbIntroFoot{Held=true,Local=FbBossLocal(fbFootR.position,at,yawRot),Turn=Quaternion.Inverse(yawRot)*fbFootR.rotation};
            if(ip.FootL>0&&!fbIntroL.Held)fbIntroL=new FbIntroFoot{Held=true,Local=FbBossLocal(fbFootL.position,at,yawRot),Turn=Quaternion.Inverse(yawRot)*fbFootL.rotation};
            if(!fbIntroR.Held||!fbIntroL.Held)return;
            var r=fbIntroR.Local;var spot=new Vector3(r.x+ip.LeftSpot.X,r.y,r.z+ip.LeftSpot.Z);
            var left=Vector3.Lerp(fbIntroL.Local,spot,ip.LeftBlend)+Vector3.up*ip.LeftLift;
            var goalR=at+yawRot*r;var goalL=at+yawRot*left;var turnR=yawRot*fbIntroR.Turn;var turnL=Quaternion.Slerp(yawRot*fbIntroL.Turn,fbFootL.rotation,ip.LeftBlend);
            if(w<1)
            {
                // Home: the left foot first, then the right, each a short lifted step from where the cut left it to the fight pose's foot.
                float kl=Mathf.Clamp01((age-FbIntroHold)/.35f),kr=Mathf.Clamp01((age-FbIntroHold-.3f)/.35f);kl=kl*kl*(3-2*kl);kr=kr*kr*(3-2*kr);
                goalL=Vector3.Lerp(at+yawRot*left,fbFootL.position,kl)+Vector3.up*(Mathf.Sin(kl*Mathf.PI)*.1f*s);goalR=Vector3.Lerp(at+yawRot*r,fbFootR.position,kr)+Vector3.up*(Mathf.Sin(kr*Mathf.PI)*.1f*s);
                turnL=Quaternion.Slerp(turnL,fbFootL.rotation,kl);turnR=Quaternion.Slerp(turnR,fbFootR.rotation,kr);
            }
            FallenLegTo(fbThighR,fbCalfR,fbFootR,goalR,turnR);FallenLegTo(fbThighL,fbCalfL,fbFootL,goalL,turnL);
        }

        // The claymore's keyed fist and blade (boss frame), blended with the plant or the carry by the key's weight.
        private bool FallenIntroSword(in FallenIntroPose ip,float w,Vector3 at,Quaternion yawRot,out Vector3 grip,out Quaternion world,out Vector3 pole,out float weight)
        {
            weight=ip.Sword*w;grip=default;world=Quaternion.identity;pole=default;if(weight<=0)return false;
            grip=at+yawRot*NativeBossPoseApply.ToUnity(ip.Grip);world=Quaternion.LookRotation(yawRot*NativeBossPoseApply.ToUnity(ip.Blade),yawRot*NativeBossPoseApply.ToUnity(ip.Flat));pole=yawRot*new Vector3(1f,-.2f,-.4f);
            return true;
        }

        // The free hand thrown back behind the hips for balance through the cut.
        private void FallenIntroLeftHand(in FallenIntroPose ip,float w,Vector3 at,Quaternion yawRot)
        {
            float k=ip.LeftHand*w;if(k<=0||fbUpperL==null||fbLowerL==null||fbHandL==null)return;
            var goal=at+yawRot*(NativeBossPoseApply.ToUnity(ip.Root)+NativeBossPoseApply.ToUnity(ip.LeftHandAt));
            FallenLegTo(fbUpperL,fbLowerL,fbHandL,Vector3.Lerp(fbHandL.position,goal,k),fbHandL.rotation);
        }
    }
}

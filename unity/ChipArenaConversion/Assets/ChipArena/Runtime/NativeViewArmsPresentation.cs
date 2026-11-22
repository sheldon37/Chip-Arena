using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The pilot's own arms in first person for every weapon and class the tactical view does not hold (Core/NativeViewArms.cs;
    // owner 2026-11-22: "Go ahead and fix"). The old view model poses and places the view body as before (its clips, its grasp,
    // its weapon on the view rig's hands, the effects there); then the body turns with the eye, its shoulders go to the eye-
    // anchored place, each arm reaches its hand where the old view put it by two-bone IK (a pole down and out), the wrists' roll
    // goes into the forearms' twist bones, and the hands keep their frames (so the weapon, its trails and every effect stay put).
    // The tactical view's cut at the clavicles, its first-person girth, its proxy rule and its fill light come with it
    // (NativeTacticalViewPresentation.cs). Third person never reads any of it.
    public sealed partial class ArenaPresentation
    {
        private int viewArmsFrame=-10;
        /// <summary>Whether the eye-anchored arms drew the local pilot's first person this frame.</summary>
        public bool ViewArmsDrawn=>viewArmsFrame==Time.frameCount;

        /// <summary>RenderFirstPerson, before the view rig is placed: the class's hold raised into view (camera frame) while the
        /// eye-anchored arms draw it (as they did last frame; none before).</summary>
        private Vector3 ViewArmsLift(Actor a)=>viewArmsFrame>=Time.frameCount-1&&!ScopeActive?CapturedAnimation.Vec(NativeViewArms.Lift(a.SourceSpec)):Vector3.zero;

        /// <summary>TacticalViewPose, when the tactical view did not draw: the eye-anchored arms on the old view model's hands, or
        /// false (the old view stands as it was: a scope, the chip select's raised wrist, a downed or grabbed pilot).</summary>
        private bool ViewArmsPose(Actor a,float time)
        {
            if(ScopeActive||ViewCamera==null||viewRig==null||!VisualViewDrawn(viewRig)||!NativeViewArms.Eligible(a.SourceSpec))return false;
            if(chipView.Frame==Time.frameCount||NativeSpecialPose.Down(a,time)||a.GrabOwner!=0&&time<a.GrabUntil||a.Swimming)return false;
            if(tacticalArms==null||tacticalArms.Body!=viewBody)tacticalArms=TacticalArmsOf(viewBody);var arms=tacticalArms;if(arms==null)return false;
            var root=viewBody.Root.transform;
            // The hands where the old view put them, on the weapon, and each wrist's roll as the grasp left it.
            Vector3 hl=arms.Hand[0].position,hr=arms.Hand[1].position;Quaternion ql=arms.Hand[0].rotation,qr=arms.Hand[1].rotation;
            float twistL=TacticalTwist(arms.Lower[0],arms.Hand[0]),twistR=TacticalTwist(arms.Lower[1],arms.Hand[1]);
            // The body turned with the eye, its shoulders on the eye-anchored place (at the old view's scale).
            // At true size (the old view enlarges the whole body about the weapon frame, so its arms filled the lower view): the
            // hands stay where the old view put them, on the weapon, a little smaller.
            var cam=ViewCamera.transform;Vector3 eye=cam.position;Quaternion eyeRot=cam.rotation;
            var parent=root.parent;float ps=parent!=null?parent.lossyScale.x:1;root.localScale=Vector3.one/Mathf.Max(1e-4f,ps);const float s=1;
            root.rotation=eyeRot;
            Vector3 ours=(arms.Upper[0].position+arms.Upper[1].position)*.5f,want=eye+eyeRot*(CapturedAnimation.Vec(NativeViewArms.ShoulderAnchor)*s);
            root.position+=want-ours;
            // The arms back to their hands.
            TacticalReach(arms.Upper[1],arms.Lower[1],arms.Hand[1],hr,eye+eyeRot*(CapturedAnimation.Vec(NativeViewArms.PoleRight)*s));arms.Hand[1].SetPositionAndRotation(hr,qr);
            TacticalReach(arms.Upper[0],arms.Lower[0],arms.Hand[0],hl,eye+eyeRot*(CapturedAnimation.Vec(NativeViewArms.PoleLeft)*s));arms.Hand[0].SetPositionAndRotation(hl,ql);
            for(int side=0;side<2;side++)
            {
                float delta=TacticalTwist(arms.Lower[side],arms.Hand[side])-(side==0?twistL:twistR);if(Mathf.Abs(delta)<1e-4f)continue;
                Vector3 axis=(arms.Hand[side].position-arms.Lower[side].position).normalized;
                for(int i=0;i<2;i++){var t=arms.Twist[side][i];if(t!=null&&arms.Share[side][i]>0)t.rotation=Quaternion.AngleAxis(delta*arms.Share[side][i]*Mathf.Rad2Deg,axis)*t.rotation;}
            }
            TacticalSlim(arms);
            viewArmsFrame=Time.frameCount;return true;
        }
    }
}

using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The pilot's own arms in first person for every weapon and class the tactical view does not hold (Core/NativeViewArms.cs;
    // owner 2026-11-22: "Go ahead and fix"). The old view model poses and places the view body as before (its clips, its grasp,
    // its weapon on the view rig's hands, the effects there); then the body turns with the eye, its shoulders go to the class's
    // eye-anchored place, each arm reaches its hand where the old view put it by two-bone IK (the class's poles), the wrists' roll
    // goes into the forearms' twist bones, and the hands keep their frames (so the weapon, its trails and every effect stay put).
    // The tactical view's cut at the clavicles, its first-person girth, its proxy rule and its fill light come with it
    // (NativeTacticalViewPresentation.cs), and the view body's near-black cloth is lifted so it takes that light (ViewArmsCloth).
    // Third person never reads any of it.
    public sealed partial class ArenaPresentation
    {
        private int viewArmsFrame=-10;
        /// <summary>Whether the eye-anchored arms drew the local pilot's first person this frame.</summary>
        public bool ViewArmsDrawn=>viewArmsFrame==Time.frameCount;

        /// <summary>RenderFirstPerson, before the view rig is placed: the class's hold raised into view (camera frame) while the
        /// eye-anchored arms draw it (as they did last frame; none before).</summary>
        private Vector3 ViewArmsLift(Actor a,float time)=>viewArmsFrame>=Time.frameCount-1&&!ScopeActive?CapturedAnimation.Vec(NativeViewArms.LiftOf(NativeViewArms.Of(a.SourceSpec),ViewArmsRaised(a,time))):Vector3.zero;
        // How far a bow is raised onto the arrow line (1 for every other class): its lowered carry comes into view, its string hand tucks.
        private static float ViewArmsRaised(Actor a,float time)=>NativeBowWire.Is(a.SourceSpec)?NativeBow.At(NativeBowWire.Of(a),time).Raised:1;

        /// <summary>TacticalViewPose, when the tactical view did not draw: the eye-anchored arms on the old view model's hands, or
        /// false (the old view stands as it was: a scope, the chip select's raised wrist, a downed or grabbed pilot).</summary>
        private bool ViewArmsPose(Actor a,float time)
        {
            if(ScopeActive||ViewCamera==null||viewRig==null||!VisualViewDrawn(viewRig)||!NativeViewArms.Eligible(a.SourceSpec))return false;
            if(chipView.Frame==Time.frameCount||NativeSpecialPose.Down(a,time)||a.GrabOwner!=0&&time<a.GrabUntil||a.Swimming)return false;
            if(tacticalArms==null||tacticalArms.Body!=viewBody)tacticalArms=TacticalArmsOf(viewBody);var arms=tacticalArms;if(arms==null)return false;
            var hold=NativeViewArms.Of(a.SourceSpec);
            var root=viewBody.Root.transform;
            // The hands where the old view put them, on the weapon, and each wrist's roll as the grasp left it.
            Vector3 hl=arms.Hand[0].position,hr=arms.Hand[1].position;Quaternion ql=arms.Hand[0].rotation,qr=arms.Hand[1].rotation;
            float twistL=TacticalTwist(arms.Lower[0],arms.Hand[0]),twistR=TacticalTwist(arms.Lower[1],arms.Hand[1]);
            var cam=ViewCamera.transform;Vector3 eye=cam.position;Quaternion eyeRot=cam.rotation;
            // The free hand (the one-hand blades' left, the staffs' right, a lowered bow's string hand) tucked under the frame while
            // it hangs low; it comes back as it rises into a cast or a draw. It holds nothing, so nothing moves with it.
            if(hold.Free!=0)
            {
                int side=hold.Free<0?0:1;Vector3 h=side==0?hl:hr;
                float w=NativeBowWire.Is(a.SourceSpec)?1-ViewArmsRaised(a,time):NativeViewArms.TuckWeight(CapturedAnimation.Num(Quaternion.Inverse(eyeRot)*(h-eye)));
                h+=eyeRot*(CapturedAnimation.Vec(hold.Tuck)*w);if(side==0)hl=h;else hr=h;
            }
            // The body turned with the eye, its shoulders on the class's eye-anchored place, at true size (the old view enlarges the
            // whole body about the weapon frame, so its arms filled the lower view): the hands stay where the old view put them, on
            // the weapon, a little smaller.
            var parent=root.parent;float ps=parent!=null?parent.lossyScale.x:1;root.localScale=Vector3.one/Mathf.Max(1e-4f,ps);
            root.rotation=eyeRot;
            Vector3 ours=(arms.Upper[0].position+arms.Upper[1].position)*.5f,want=eye+eyeRot*CapturedAnimation.Vec(hold.Shoulder);
            root.position+=want-ours;
            // The arms back to their hands.
            ViewArmsReach(arms.Upper[1],arms.Lower[1],arms.Hand[1],hr,eye+eyeRot*CapturedAnimation.Vec(hold.PoleRight));arms.Hand[1].SetPositionAndRotation(hr,qr);
            ViewArmsReach(arms.Upper[0],arms.Lower[0],arms.Hand[0],hl,eye+eyeRot*CapturedAnimation.Vec(hold.PoleLeft));arms.Hand[0].SetPositionAndRotation(hl,ql);
            for(int side=0;side<2;side++)
            {
                float delta=TacticalTwist(arms.Lower[side],arms.Hand[side])-(side==0?twistL:twistR);if(Mathf.Abs(delta)<1e-4f)continue;
                Vector3 axis=(arms.Hand[side].position-arms.Lower[side].position).normalized;
                for(int i=0;i<2;i++){var t=arms.Twist[side][i];if(t!=null&&arms.Share[side][i]>0)t.rotation=Quaternion.AngleAxis(delta*arms.Share[side][i]*Mathf.Rad2Deg,axis)*t.rotation;}
            }
            TacticalSlim(arms);
            viewArmsFrame=Time.frameCount;return true;
        }

        /// <summary>VisualViewPose, while the eye-anchored arms draw (as they did last frame): the left fist closed on what the
        /// left hand carries, as third person's VisualLeftSeat closes it (a staff's shaft, the bow's riser) and a held off-hand blade
        /// seats (the Fangs' left blade): with the hands in view, an open palm under the riser or beside the blade read as dropped.
        /// The item moves onto the fist; the arm keeps the old view's place.</summary>
        private void ViewArmsSeat(ref NativeVisualRig.Grasp g,Actor a)
        {
            if(viewArmsFrame<Time.frameCount-1||ScopeActive||a.WeaponAway||chipView.Frame==Time.frameCount||!NativeViewArms.Eligible(a.SourceSpec))return;
            if(NativeLobbyPoses.Magic(a.SourceSpec)){g.LeftGrip=1;g.Left=NativeVisualRig.StaffShaft;g.Support=0;}
            else if(NativeBowWire.Is(a.SourceSpec)){g.LeftGrip=1;g.Left=NativeVisualRig.BowRiser;g.Support=0;}
            else if(a.SourceSpec==NativeFangs.Spec&&!g.Left.Seated){g.LeftGrip=1;g.Left=NativeVisualRig.Blade(System.Numerics.Quaternion.Identity);}
        }

        // Two-bone IK as the tactical view's TacticalReach (the elbow on the side of the pole, the hand on the target; an arm too
        // short reaches from its shoulder first), but with the elbow read again once the shoulder has moved: read before it, the
        // elbow was turned toward its old place and the forearm fell short, the hand left floating up to 30 cm off its wrist
        // (the greatsword, the shield; measured 2026-11-22 on the view-body probe). TacticalReach is the guns' and stays as it is.
        private static void ViewArmsReach(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole)
        {
            Vector3 a=upper.position,b=lower.position,c=hand.position;float la=Vector3.Distance(a,b),lb=Vector3.Distance(b,c);if(la<1e-4f||lb<1e-4f)return;
            Vector3 to=target-a;float d=to.magnitude,reach=(la+lb)*.999f;
            if(d>reach){upper.position=a+to/d*(d-reach);a=upper.position;b=lower.position;to=target-a;d=to.magnitude;}
            if(d<1e-4f)return;d=Mathf.Clamp(d,Mathf.Abs(la-lb)+1e-4f,reach);Vector3 dir=to.normalized;
            Vector3 bend=pole-a;bend-=Vector3.Dot(bend,dir)*dir;if(bend.sqrMagnitude<1e-8f){bend=b-a;bend-=Vector3.Dot(bend,dir)*dir;}if(bend.sqrMagnitude<1e-8f)bend=Vector3.down;bend.Normalize();
            float cosA=Mathf.Clamp((la*la+d*d-lb*lb)/(2*la*d),-1,1),sinA=Mathf.Sqrt(1-cosA*cosA);
            Vector3 elbow=a+dir*(la*cosA)+bend*(la*sinA);
            upper.rotation=Quaternion.FromToRotation(b-a,elbow-a)*upper.rotation;
            b=lower.position;c=hand.position;lower.rotation=Quaternion.FromToRotation(c-b,target-b)*lower.rotation;
        }

        // ---- The view body's dark cloth (owner 2026-11-22 review: the default suit's sleeves read as flat black silhouettes at
        // night, the fill light lost on a near-black colour): while the eye-anchored arms draw, every view-body renderer takes
        // NativeViewArms.Look.Cloth (NativeVisualBodyFx.cginc ViewCloth: a dark colour lifted by how dark it is, so a black sleeve reads
        // as dark cloth in the view layer's light; skin, gloves and trims unchanged); put back (0) for the tactical view and the
        // old view model. Written only when it changes or the body is made again.
        private static readonly int ViewClothId=Shader.PropertyToID("_ViewCloth");
        private VisualBody viewClothBody;private Vector4 viewClothShown;
        private void ViewArmsCloth(bool on)
        {
            if(viewBody==null||viewBody.Root==null)return;
            var c=NativeViewArms.Look.Cloth;Vector4 want=on?new Vector4(c.X,c.Y,c.Z,1):Vector4.zero;
            if(viewClothBody==viewBody&&viewClothShown==want)return;
            viewClothBody=viewBody;viewClothShown=want;
            foreach(var r in viewBody.All){if(r==null)continue;r.GetPropertyBlock(viewBody.Block);viewBody.Block.SetVector(ViewClothId,want);r.SetPropertyBlock(viewBody.Block);}
        }
    }
}

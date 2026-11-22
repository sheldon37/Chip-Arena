using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The first-person tactical view on the view body (Core/NativeTacticalView.cs; docs/FPS-TACTICAL.md; owner 2026-11-21:
    // "Utilize new FPS system / animations from tactical pack"). With the local view-motion bank installed, the pistol, rifle and
    // machine gun in first person (chosen in Settings; aiming never forces it) are held by the pilot model's own forearms and
    // hands on the game's own weapon models, posed by the pack's clips: the body is set at true size with its shoulders on the
    // clip's, each arm reaches its hand by two-bone IK (the clip's elbow as the pole), the wrists' roll goes into the forearms'
    // twist bones and the clip's fingers close the hands while an action moves them. The view rig's right hand takes the model's
    // weapon frame (fitted, as VisualViewPose leaves it), so the muzzle flash, the rounds' origin and the kit pieces stay on the
    // model. Without the bank, for another weapon or class, the chip select's raised wrist, a scope or a downed pilot, the old
    // view model draws as before. Third person never reads any of it. Presentation only: aim, muzzle, shots and rules are the game's.
    public sealed partial class ArenaPresentation
    {
        private static NativeTacticalBank tacticalBank;private static bool tacticalTried;
        /// <summary>The installed view-motion bank (null without one, or when it fails to read).</summary>
        public static NativeTacticalBank TacticalBank{get{TacticalLoad();return tacticalBank;}}
        private static void TacticalLoad()
        {
            if(tacticalTried)return;tacticalTried=true;
            try{var asset=Resources.Load<TextAsset>(NativeTacticalBank.Resource);if(asset!=null)tacticalBank=NativeTacticalBank.Parse(asset.bytes);}
            catch(System.Exception e){tacticalBank=null;Debug.LogWarning("View-motion bank not read: "+e.Message);}
        }
        private NativeTacticalView tacticalView;private readonly NativeTacticalSolution tacticalSolve=new NativeTacticalSolution();
        private NativeTacticalCalibration tacticalCal;private VisualBody tacticalCalBody;private VisualWeapon tacticalCalHeld;private WeaponId tacticalCalGun;private bool tacticalCalCalm;
        private int tacticalFrame=-10,tacticalScopedFrame=-10,tacticalKickFrame=-1;
        private Quaternion tacticalCamera=Quaternion.identity,tacticalKick=Quaternion.identity;
        private TacticalArms tacticalArms;
        /// <summary>Whether the tactical view drew the local pilot's first person this frame.</summary>
        public bool TacticalViewDrawn=>tacticalFrame==Time.frameCount;
        // The walk share of the view's gait (NativeViewSway.Motion, speed over 6.8 m/s) at which the pack's walk is whole.
        private const float TacticalWalkMotion=.6f;

        // The view body's arm, hand, finger and forearm twist bones (by the pack-style names the BoZo skeleton shares), each finger
        // bone's length axis in its own frame, each twist bone's share of the wrist's roll. Null: the body lacks one.
        private sealed class TacticalArms
        {
            public VisualBody Body;public readonly Transform[] Upper=new Transform[2],Lower=new Transform[2],Hand=new Transform[2];
            public readonly Transform[][] Fingers={new Transform[15],new Transform[15]};public readonly Vector3[][] Axis={new Vector3[15],new Vector3[15]};
            public readonly Transform[][] Twist={new Transform[2],new Transform[2]};public readonly float[][] Share={new float[2],new float[2]};
            // The places the binding never writes (it turns bones only): put back every frame before the arms are posed again.
            public readonly Vector3[] UpperAt=new Vector3[2],HandAt=new Vector3[2];
            public void Rest(){for(int i=0;i<2;i++){if(Upper[i]!=null)Upper[i].localPosition=UpperAt[i];if(Hand[i]!=null)Hand[i].localPosition=HandAt[i];}}
        }
        private static readonly string[] TacticalDigits={"thumb","index","middle","ring","pinky"};
        private static TacticalArms TacticalArmsOf(VisualBody body)
        {
            var bones=body?.Binding?.Bones;if(bones==null)return null;
            Transform Find(string n){foreach(var t in bones)if(t!=null&&t.name==n)return t;return null;}
            var arms=new TacticalArms{Body=body};
            for(int side=0;side<2;side++)
            {
                string s=side==0?"_l":"_r";
                arms.Upper[side]=Find("upperarm"+s);arms.Lower[side]=Find("lowerarm"+s);arms.Hand[side]=Find("hand"+s);
                if(arms.Upper[side]==null||arms.Lower[side]==null||arms.Hand[side]==null)return null;
                arms.UpperAt[side]=arms.Upper[side].localPosition;arms.HandAt[side]=arms.Hand[side].localPosition;
                for(int d=0;d<5;d++)for(int k=0;k<3;k++)
                {
                    var b=Find(TacticalDigits[d]+"_0"+(k+1)+s);if(b==null)return null;arms.Fingers[side][d*3+k]=b;
                }
                for(int d=0;d<5;d++)for(int k=0;k<3;k++)
                {
                    // The direction to its child in its own frame; the last segment keeps the axis it was offset along (as the bake reads it).
                    var b=arms.Fingers[side][d*3+k];var child=k<2?arms.Fingers[side][d*3+k+1]:null;
                    var axis=child!=null&&child.parent==b?child.localPosition:b.localPosition;arms.Axis[side][d*3+k]=axis.sqrMagnitude>1e-10f?axis.normalized:Vector3.forward;
                }
                float length=Vector3.Distance(arms.Lower[side].position,arms.Hand[side].position);
                for(int i=0;i<2;i++)
                {
                    var t=Find("lowerarm_twist_0"+(i+1)+s);arms.Twist[side][i]=t;
                    if(t!=null&&length>1e-4f)arms.Share[side][i]=Mathf.Clamp01(Vector3.Dot(t.position-arms.Lower[side].position,(arms.Hand[side].position-arms.Lower[side].position).normalized)/length);
                }
            }
            return arms;
        }

        // The palm frame the bake reads (forward wrist to middle_01, across index_01 to pinky_01, up their cross).
        private static Quaternion TacticalPalm(TacticalArms arms,int side)
        {
            var f=arms.Fingers[side];var hand=arms.Hand[side];Transform index=f[3],middle=f[6],pinky=f[12];
            Vector3 fwd=(middle.position-hand.position).normalized,across=index.position-pinky.position;across=(across-Vector3.Dot(across,fwd)*fwd).normalized;
            return fwd.sqrMagnitude>.5f&&across.sqrMagnitude>.5f?Quaternion.LookRotation(fwd,Vector3.Cross(fwd,across)):hand.rotation;
        }
        // The wrist's roll about the forearm (radians, signed).
        private static float TacticalTwist(Transform lower,Transform hand)
        {
            Vector3 axis=Quaternion.Inverse(lower.rotation)*(hand.position-lower.position);if(axis.sqrMagnitude<1e-10f)return 0;axis.Normalize();
            var rel=Quaternion.Inverse(lower.rotation)*hand.rotation;float proj=rel.x*axis.x+rel.y*axis.y+rel.z*axis.z;
            float a=2*Mathf.Atan2(proj,rel.w);return a>Mathf.PI?a-2*Mathf.PI:a<-Mathf.PI?a+2*Mathf.PI:a;
        }
        // Two-bone IK: the elbow on the side of the pole, the hand on the target; an arm too short reaches from its shoulder first.
        private static void TacticalReach(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole)
        {
            Vector3 a=upper.position,b=lower.position,c=hand.position;float la=Vector3.Distance(a,b),lb=Vector3.Distance(b,c);if(la<1e-4f||lb<1e-4f)return;
            Vector3 to=target-a;float d=to.magnitude,reach=(la+lb)*.999f;
            if(d>reach){upper.position=a+to/d*(d-reach);a=upper.position;to=target-a;d=to.magnitude;}
            if(d<1e-4f)return;d=Mathf.Clamp(d,Mathf.Abs(la-lb)+1e-4f,reach);Vector3 dir=to.normalized;
            Vector3 bend=pole-a;bend-=Vector3.Dot(bend,dir)*dir;if(bend.sqrMagnitude<1e-8f){bend=b-a;bend-=Vector3.Dot(bend,dir)*dir;}if(bend.sqrMagnitude<1e-8f)bend=Vector3.down;bend.Normalize();
            float cosA=Mathf.Clamp((la*la+d*d-lb*lb)/(2*la*d),-1,1),sinA=Mathf.Sqrt(1-cosA*cosA);
            Vector3 elbow=a+dir*(la*cosA)+bend*(la*sinA);
            upper.rotation=Quaternion.FromToRotation(b-a,elbow-a)*upper.rotation;
            b=lower.position;c=hand.position;lower.rotation=Quaternion.FromToRotation(c-b,target-b)*lower.rotation;
        }

        /// <summary>RenderFirstPerson, once the view body is posed (VisualViewPose): the tactical view on it, or false (the old view
        /// model stands).</summary>
        private bool TacticalViewPose(Actor a,float time)
        {
            if(tacticalArms!=null&&tacticalArms.Body==viewBody)tacticalArms.Rest();
            if(ScopeActive){tacticalScopedFrame=Time.frameCount;return false;}
            var bank=TacticalBank;var g=NativeTacticalGun.For(a.Weapon);if(bank==null||g==null||!bank.Covers(g)||ViewCamera==null||viewRig==null)return false;
            if(!VisualViewDrawn(viewRig)||viewBody.Held==null)return false;
            string spec=HeldSpec(a);if(spec!="pistol"&&spec!="rifle"&&spec!="machinegun")return false;
            if(NativeBowWire.Is(a.SourceSpec)||a.SourceSpec==NativeBombs.Spec||a.SourceSpec=="siren"||chipView.Frame==Time.frameCount)return false;
            if(a.GrabOwner!=0&&time<a.GrabUntil||NativeSpecialPose.Down(a,time)||a.Swimming)return false;
            if(tacticalArms==null||tacticalArms.Body!=viewBody)tacticalArms=TacticalArmsOf(viewBody);var arms=tacticalArms;if(arms==null)return false;
            tacticalView??=new NativeTacticalView(bank);
            var root=viewBody.Root.transform;var held=viewBody.Held;
            // Calibration: the body's right hand bone in its weapon frame as its grasp holds the model (VisualViewPose's Apply, this
            // frame), taken again once the pilot is calm (not shooting, reloading, sprinting, rolling or airborne).
            bool calm=time-a.LastShot>.35f&&!NativeTacticalTiming.Reloading(time,a.ReloadAt,a.ReloadUntil)&&!a.Sprinting&&a.Grounded&&!a.Rolling(time);
            if(!tacticalCal.Set||tacticalCalBody!=viewBody||tacticalCalHeld!=held||tacticalCalGun!=a.Weapon||calm&&!tacticalCalCalm)
            {
                var pose=viewRig.Pose;viewBody.Binding.WeaponFrame(pose,out var rp,out var rq);
                Vector3 hp=root.InverseTransformPoint(arms.Hand[1].position);Quaternion hq=Quaternion.Inverse(root.rotation)*arms.Hand[1].rotation;
                var iq=Quaternion.Inverse(rq);
                tacticalCal=tacticalView.Calibrate(g,CapturedAnimation.Num(iq*(hp-rp)),CapturedAnimation.Num(iq*hq),CapturedAnimation.Num(TacticalSight(a,held)),held.Supported?CapturedAnimation.Num(held.Support):default);
                tacticalCalBody=viewBody;tacticalCalHeld=held;tacticalCalGun=a.Weapon;tacticalCalCalm=calm;
            }
            bool fresh=tacticalFrame<Time.frameCount-1&&tacticalScopedFrame<Time.frameCount-1;
            var input=new NativeTacticalInput{Time=time,Weapon=a.Weapon,LastShot=a.LastShot,ShootReady=a.ShootReady,ReloadAt=a.ReloadAt,ReloadUntil=a.ReloadUntil,Aim=AimHeld,
                Sprinting=a.Sprinting,Grounded=a.Grounded,Blocked=a.Rolling(time)||time<a.StunUntil,Walk=Mathf.Clamp01(viewSway.Motion/TacticalWalkMotion),Sprint=viewSway.Sprint,
                Stride=viewSway.Stride/(2*Mathf.PI),SwayX=viewSway.SwayX,SwayY=viewSway.SwayY,SwayRoll=viewSway.SwayRoll,Hop=viewSway.Hop,Land=viewSway.Land,Shake=CameraShake?viewShake:0,Fresh=fresh};
            var s=tacticalSolve;if(!tacticalView.Solve(input,tacticalCal,s))return false;
            // The eye: the camera without the clips' own motion (TacticalCameraKick turned it), as the pack's arms are on the body.
            var cam=ViewCamera.transform;Quaternion kick=tacticalKickFrame==Time.frameCount?tacticalKick:Quaternion.identity;
            Vector3 eye=cam.position;Quaternion eyeRot=cam.rotation*Quaternion.Inverse(kick);
            Vector3 W(System.Numerics.Vector3 v)=>eye+eyeRot*CapturedAnimation.Vec(v);Quaternion R(System.Numerics.Quaternion q)=>eyeRot*CapturedAnimation.Quat(q);
            // What rides the left hand (its item frame, VisualViewPose's) and each wrist's roll as the grasp left it.
            var lh=viewRig.Bones[CapturedRig.LHand];Matrix4x4 leftItem=arms.Hand[0].worldToLocalMatrix*lh.localToWorldMatrix;
            float twistL=TacticalTwist(arms.Lower[0],arms.Hand[0]),twistR=TacticalTwist(arms.Lower[1],arms.Hand[1]);
            Quaternion palmInHandL=Quaternion.Inverse(arms.Hand[0].rotation)*TacticalPalm(arms,0);
            // The body at true size, turned with the eye, its shoulders' midpoint on the clip's.
            var parent=root.parent;float ps=parent!=null?parent.lossyScale.x:1;root.localScale=Vector3.one/Mathf.Max(1e-4f,ps);root.rotation=eyeRot;
            Vector3 ours=(arms.Upper[0].position+arms.Upper[1].position)*.5f,clip=(W(s.LeftShoulder)+W(s.RightShoulder))*.5f;root.position+=clip-ours;
            // The arms: the right hand on the model's grip (its grasp's frame, carried by the clip), the left on the clip's place and palm.
            Vector3 rt=W(s.Right),lt=W(s.Left);Quaternion rr=R(s.RightTurn),lr=R(s.LeftPalm)*Quaternion.Inverse(palmInHandL);
            TacticalReach(arms.Upper[1],arms.Lower[1],arms.Hand[1],rt,W(s.RightElbow));arms.Hand[1].SetPositionAndRotation(rt,rr);
            TacticalReach(arms.Upper[0],arms.Lower[0],arms.Hand[0],lt,W(s.LeftElbow));arms.Hand[0].SetPositionAndRotation(lt,lr);
            // The wrists' new roll, shared along the forearms' twist bones as the grasp shared it.
            for(int side=0;side<2;side++)
            {
                float delta=TacticalTwist(arms.Lower[side],arms.Hand[side])-(side==0?twistL:twistR);if(Mathf.Abs(delta)<1e-4f)continue;
                Vector3 axis=(arms.Hand[side].position-arms.Lower[side].position).normalized;
                for(int i=0;i<2;i++){var t=arms.Twist[side][i];if(t!=null&&arms.Share[side][i]>0)t.rotation=Quaternion.AngleAxis(delta*arms.Share[side][i]*Mathf.Rad2Deg,axis)*t.rotation;}
                // The hand keeps its target turn (the twist bones are its siblings, not its parents).
            }
            // The fingers: each segment turned onto the clip's direction in the palm, by the clip's weight over the grasp's.
            for(int side=0;side<2;side++)
            {
                float w=side==0?s.LeftFingerWeight:s.RightFingerWeight;if(w<=0)continue;var dirs=side==0?s.LeftFingers:s.RightFingers;
                for(int d=0;d<5;d++)for(int k=0;k<3;k++)
                {
                    var palm=TacticalPalm(arms,side);var b=arms.Fingers[side][d*3+k];
                    Vector3 now=b.rotation*arms.Axis[side][d*3+k],want=palm*CapturedAnimation.Vec(dirs[d*3+k]);
                    b.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(now,want),w)*b.rotation;
                }
            }
            // The view rig's hands: the right on the model's weapon frame at true size, fitted (the effects' frame); the left's item
            // back on the new left hand.
            var rh=viewRig.Bones[CapturedRig.RHand];rh.SetPositionAndRotation(W(s.Weapon),R(s.WeaponTurn));
            rh.localScale=viewBody.RightScale;float rs=rh.lossyScale.x;if(rs>1e-5f)rh.localScale=viewBody.RightScale/rs;VisualWeaponFit(rh,held);
            var lm=arms.Hand[0].localToWorldMatrix*leftItem;lh.SetPositionAndRotation(lm.GetColumn(3),lm.rotation);
            // The view model's lens: the pack's own framing (its first-person camera's 90 degrees, its aim's 70-80) instead of the
            // world's, eased with the aim; the sights stay on the axis whatever the lens.
            ViewCamera.fieldOfView=Mathf.Lerp(g.HipFov,g.AimFov,s.Ads);
            tacticalCamera=CapturedAnimation.Quat(s.Camera);tacticalFrame=Time.frameCount;
            return true;
        }
        // The model's rear sight in its weapon frame at true size: the bake's Sight, else the gun's ModelSight, else the Standard
        // gun's through the fit.
        private static Vector3 TacticalSight(Actor a,VisualWeapon w)
        {
            float y=a.Weapon==WeaponId.Pistol?.105f:a.Weapon==WeaponId.Rifle?.175f:.25f,z=a.Weapon==WeaponId.Pistol?-.03f:a.Weapon==WeaponId.Rifle?-.075f:-.02f;
            var rear=new Vector3(0,y,z);if(w==null)return rear;if(w.Sighted)return w.Sight;
            var own=NativeTacticalGun.For(a.Weapon)?.ModelSight??System.Numerics.Vector3.Zero;if(own!=System.Numerics.Vector3.Zero)return CapturedAnimation.Vec(own);
            return w.Scale!=1?w.Centre+w.Scale*(rear-w.Centre):rear;
        }
        /// <summary>The clips' own camera motion (a reload's look down at the magazine, the bolt's nod), first person with camera shake
        /// on, applied after the aim point like HitKick (the view is rebuilt from the look angles every frame, so it never reaches aim);
        /// `scale` is the setting's share. The arms stay on the body: TacticalViewPose places them in the camera without it.</summary>
        public void TacticalCameraKick(Transform view,float scale)
        {
            tacticalKickFrame=-1;if(view==null||!(scale>0)||!FirstPerson||tacticalFrame<Time.frameCount-2)return;
            var q=Quaternion.Slerp(Quaternion.identity,tacticalCamera,Mathf.Clamp01(scale));if(Quaternion.Angle(q,Quaternion.identity)<.01f)return;
            view.rotation*=q;tacticalKick=q;tacticalKickFrame=Time.frameCount;
        }
    }
}

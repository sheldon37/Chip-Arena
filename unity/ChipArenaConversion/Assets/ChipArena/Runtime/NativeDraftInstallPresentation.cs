using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuat=System.Numerics.Quaternion;
namespace ChipArena.UnityConversion
{
    // The chip install's draw on the draft stage (Core NativeInstallShot / NativeDraftInstall; owner 2026-11-18: "improve drastically";
    // presentation only). The stage plans the cartridge on its rig; here, on the model, the drawn left arm is solved so the drawn
    // fingers' pads hold the cartridge on its planned path relative to the drawn bay (VisualDraftContact: the pinch, then the two
    // fingers pressing it in), the cartridge is drawn in the fingers, its core latched into the bay and seated there face out as the
    // shell bursts into lit shards, the device runs on the install's clock (its lid, its bay's pulse, its lens), the chest core
    // ignites and the light runs up the arm through the suit's trims to the visor; the install's lights take the muzzle slots (the
    // model, the gear and the voxel body take them as plain emission), and its effects (the catch's motes and glint, the line-up's
    // brackets, the clack's flash, ring and sparks, the surge's packet, the power-up's motes and the crest's flourish) go through the
    // shared effects renderer. The voxel body and the Siren drafter take their own versions. Allocation-free per frame once warm.
    public sealed partial class ArenaPresentation
    {
        // ---- conversions between the stage's root frame (source axes) and Unity's world ----
        private static Vector3 StageDir(NVector v)=>new Vector3(-v.X,v.Y,v.Z);
        private static Quaternion StageTurn(NQuat q)=>CapturedAnimation.Quat(CapturedRig.Mirror(q));
        private static NVector FromStage(Vector3 w)=>new NVector(-w.x,w.y-.88f,w.z);
        private static NVector FromStageDir(Vector3 d)=>new NVector(-d.x,d.y,d.z);

        // ---- this frame's install (RenderDraft) ----
        private NativeInstallShot.Profile diProfile=NativeInstallShot.Of("common",null);private float diA=-1,diP=-1;private bool diModel,diSiren,diDog,diReduced,diShort;
        // The chip's colour, the pilot's accent tint and the clack's rim (the chip's; a common chip's grey takes the pilot's accent instead).
        private Color diChip=Color.white,diAccent=Color.white,diRim=Color.white;private string diTier="common";private float diTurn=NativeInstallShot.TurnSeconds;
        // The accent trims' tints (ratios over the model's baked accent, as AccentTint is: not colours) that make them glow in the chip's
        // colour and white-hot (the visor's beat): a colour multiplied onto the baked teal read green for gold.
        private Color diChipAccent=Color.white,diWhiteAccent=Color.white;private int diTintsOf=-1;
        private void DraftTints(Color chip)
        {
            int hex=(Mathf.RoundToInt(Mathf.Clamp01(chip.r)*255)<<16)|(Mathf.RoundToInt(Mathf.Clamp01(chip.g)*255)<<8)|Mathf.RoundToInt(Mathf.Clamp01(chip.b)*255);if(hex==diTintsOf)return;diTintsOf=hex;
            var c=NativePortraitRules.GlowAccentTint(hex);diChipAccent=new Color(c.X,c.Y,c.Z);var w=NativePortraitRules.GlowAccentTint(0xffffff);diWhiteAccent=new Color(w.X,w.Y,w.Z);
        }
        // The drawn bay (seat, outward normal, forearm toward the hand) and the cartridge drawn this frame (centre, turn, scale).
        private Vector3 diBayAt,diBayN,diBayF;private bool diBay;private Vector3 diCartAt,diPinch;private Quaternion diCartTurn=Quaternion.identity;private float diCartScale;
        // The grasp the draft actor wears (NativeHandShapes; VisualBodyBind takes it for draftActor while diGrasp is set).
        private bool diGraspOn;private NativeVisualRig.HandShape diLeftShape,diRightShape;private float diRightGrip,diRightClench;
        private Func<int,bool> diShell,diCore;private Func<int,Matrix4x4> diCartBone;private Func<int,Color> diCartPart;private Matrix4x4 diCartM,diCartOrbit;private Color diCartTint;
        private readonly NativeMuzzleLightPicker diLights=new NativeMuzzleLightPicker();private DraftFxSink diFx;

        /// <summary>The stage's grasp for the draft actor (its hands' shapes and the receiving hand's grip and fist).</summary>
        private void DraftGrasp(ref NativeVisualRig.Grasp g)
        {
            if(!diGraspOn)return;g.LeftShape=diLeftShape;g.RightShape=diRightShape;g.RightGrip=Mathf.Max(g.RightGrip,diRightGrip);g.Clench=Mathf.Max(g.Clench,diRightClench);
        }
        /// <summary>The drawn bay's frame on the draft body: its seat, outward normal and the forearm's run toward the hand (world).</summary>
        private bool VisualDraftBayFrame(out Vector3 at,out Vector3 outward,out Vector3 along)
        {
            along=default;if(!VisualDraftBay(out at,out outward))return false;var dev=draftBody.GearDevice;along=-dev.TransformDirection(Vector3.up).normalized;along=Vector3.ProjectOnPlane(along,outward).normalized;return along.sqrMagnitude>.5f;
        }

        // ---- the contact pass (the model) ----
        private sealed class DraftArm{public VisualBody Body;public string Side;public Transform Upper,Lower,Hand,Thumb2,Thumb3,Index1,Index2,Index3,Middle1,Middle2,Middle3,Pinky1,OtherLower,OtherHand,OtherUpper,OtherClav,Chest,Neck,Head;public float Glove=.09f;public Vector3 UpperBase=Vector3.one,HandBase=Vector3.one;public bool Scaled;}
        private DraftArm draftArm;
        private DraftArm DraftArmFor(VisualBody body,bool left)
        {
            string side=left?"_l":"_r",other=left?"_r":"_l";if(draftArm!=null&&draftArm.Body==body&&draftArm.Side==side&&draftArm.Hand!=null)return draftArm;
            var r=new DraftArm{Body=body,Side=side};var rig=body.Binding.Rig;var bones=body.Binding.Bones;Transform B(string n){int i=rig.Find(n);return i>=0&&i<bones.Length?bones[i]:null;}
            r.Upper=B("upperarm"+side);r.Lower=B("lowerarm"+side);r.Hand=B("hand"+side);r.Thumb2=B("thumb_02"+side);r.Thumb3=B("thumb_03"+side);r.Index1=B("index_01"+side);r.Index2=B("index_02"+side);r.Index3=B("index_03"+side);
            r.Middle1=B("middle_01"+side);r.Middle2=B("middle_02"+side);r.Middle3=B("middle_03"+side);r.Pinky1=B("pinky_01"+side);r.OtherLower=B("lowerarm"+other);r.OtherHand=B("hand"+other);r.OtherUpper=B("upperarm"+other);r.OtherClav=B("clavicle"+other);
            r.Chest=B("spine_05");r.Neck=B("neck_01");r.Head=B("head");if(r.Upper!=null)r.UpperBase=r.Upper.localScale;if(r.Hand!=null)r.HandBase=r.Hand.localScale;
            if(r.Index1!=null&&r.Pinky1!=null)r.Glove=Mathf.Clamp((r.Index1.position-r.Pinky1.position).magnitude*1.3f,.05f,.14f);
            return draftArm=r;
        }
        // A finger's pad: its last joint plus .6 of its last segment (the rig has no tip bones).
        private static Vector3 Pad(Transform b2,Transform b3)=>b3.position+.6f*(b3.position-b2.position);
        private static Vector3 PalmOf(DraftArm r)
        {
            var palm=Vector3.Cross(r.Index1.position-r.Pinky1.position,r.Middle1.position-r.Hand.position);if(palm.sqrMagnitude<1e-10f)return Vector3.down;palm.Normalize();
            return Vector3.Dot(r.Thumb3.position-r.Hand.position,palm)<0?-palm:palm;
        }
        /// <summary>
        /// The drawn taking hand onto the cartridge by weight `w`: its pads' frame turned onto the cartridge's (the pinch: the index pad
        /// over the thumb's along the back face's normal, the index along the cartridge toward its connector; the press: the palm side
        /// onto the back face, kept through the lift off the shell), and the arm reaching (a two-bone solve, at most 3% stretch) so the
        /// pinch, then the two fingers' pads, land on `pinchAt` / `pressAt`. Before the catch `cart` is the cartridge's turn at the catch
        /// and the targets the stage hand's own pinch on its reach, so the drawn hand is already open and turned to take it as it flies
        /// in. Returns the drawn pinch (world).
        /// </summary>
        private Vector3 VisualDraftContact(bool left,float p,Vector3 pinchAt,Vector3 pressAt,Quaternion cart,float w)
        {
            var body=draftBody;if(body==null)return pinchAt;var r=DraftArmFor(body,left);if(r.Upper==null||r.Lower==null||r.Hand==null||r.Index1==null||r.Index2==null||r.Index3==null||r.Thumb2==null||r.Thumb3==null||r.Pinky1==null||r.Middle1==null)return pinchAt;
            if(r.Scaled){r.Upper.localScale=r.UpperBase;r.Hand.localScale=r.HandBase;r.Scaled=false;}
            float press=NativeInstallShot.Ramp(p,NativeInstallShot.ThumbOff,NativeInstallShot.PressShaped);
            var n=cart*Vector3.up;var along=cart*Vector3.back;var hand=r.Hand;
            Vector3 Hold(){var t=Pad(r.Thumb2,r.Thumb3);var i=Pad(r.Index2,r.Index3);var m=r.Middle2!=null&&r.Middle3!=null?Pad(r.Middle2,r.Middle3):i;return Vector3.Lerp((t+i)*.5f,(i+m)*.5f,press);}
            if(w<=1e-4f)return Hold();
            // The pads' frame now and the turn that lays it on the cartridge's.
            var a=Vector3.Lerp((Pad(r.Index2,r.Index3)-Pad(r.Thumb2,r.Thumb3)).normalized,-PalmOf(r),press);var b=(r.Index3.position-r.Index2.position).normalized;
            if(a.sqrMagnitude<1e-6f||b.sqrMagnitude<1e-6f)return Hold();
            var turn=Quaternion.LookRotation(along,n)*Quaternion.Inverse(Quaternion.LookRotation(b,a))*hand.rotation;
            var keep=hand.rotation;hand.rotation=turn;var off=Hold()-hand.position;hand.rotation=keep;
            var target=Vector3.Lerp(pinchAt,pressAt,press)-off;var pole=new Vector3(left?-1:1,-.8f,-.35f);
            r.Scaled=DogReach(r.Upper,r.Lower,hand,target,pole,w,1.03f,r.UpperBase,r.HandBase);hand.rotation=Quaternion.Slerp(hand.rotation,turn,w);
            return Hold();
        }
        /// <summary>Where the two pressing fingers lift to off the shell (world): from the press point on the drawn bay, up along its normal and
        /// back toward the taking hand's own shoulder in the bay's plane, never along the forearm toward its elbow (NativeDraftInstall.Lift:
        /// about 4 cm by ReleaseEnd), so the bay stays clear for the white-hot core and the lid's slam.</summary>
        private Vector3 DraftLiftAt()
        {
            float s=diCartScale;var press=diBayAt+diBayN*(NativePilotGear.FrameHeight+2*NativeDraftInstall.FaceDepth*s);
            var shoulder=draftArm?.Upper!=null?draftArm.Upper.position:press+Vector3.up;var away=Vector3.ProjectOnPlane(shoulder-press,diBayN);away=away.sqrMagnitude>1e-8f?away.normalized:Vector3.up;
            float along=Vector3.Dot(away,diBayF);if(along<0){var cut=away-diBayF*along;if(cut.sqrMagnitude>1e-6f)away=cut.normalized;}
            float lift=NativeDraftInstall.Lift(diP);return press+diBayN*(NativeDraftInstall.FlickUp*lift)+away*(NativeDraftInstall.FlickOver*lift);
        }
        /// <summary>The readout's leader starts beside the pilot at the device's height, never across the body: the device's screen point
        /// (pixels) moved out past the pilot's side toward the readout (the taking arm's joints and the receiving hand, projected, with a
        /// margin), or the device itself where it stands clear of them.</summary>
        private Vector2 DraftLeaderFrom(Camera camera,Vector2 device,NativeDraftStage stage)
        {
            if(device==Vector2.zero)return Vector2.zero;float edge=device.x,margin=.035f*Screen.width;
            void Out(Vector3 w){var s=camera.WorldToScreenPoint(w);if(s.z>0)edge=Mathf.Max(edge,s.x+margin);}
            if(diModel&&draftArm?.Upper!=null&&draftArm.Lower!=null&&draftArm.Hand!=null){Out(draftArm.Upper.position);Out(draftArm.Lower.position);Out(draftArm.Hand.position);if(draftArm.OtherHand!=null)Out(draftArm.OtherHand.position);}
            else{var pose=stage.Pose;int arm=stage.Left?CapturedRig.LArm:CapturedRig.RArm;for(int i=0;i<3;i++)Out(Stage(pose.WorldPosition[arm+i]));Out(Stage(pose.WorldPosition[stage.Left?CapturedRig.RHand:CapturedRig.LHand]));}
            return new Vector2(edge,device.y);
        }
        // The present's slide along the lens's line, its miss off the designed seat and the contact's weight this frame (read by the local
        // capture tool's log).
        private float diPresentSlide,diPresentMiss,diContactW;
        /// <summary>
        /// The model's receiving arm onto the stage's designed present (NativeDraftInstall.PresentAt, its normal and its forearm) by the arm's
        /// rise into it (ForeWeight): the device rides the model's forearm (its twist bone), which the stage hand's frame cannot place, so the
        /// bay is solved on the drawn bones. The forearm is turned so its axis runs along the designed forearm (across the body, square to
        /// the lens) and the bay's face along the designed normal (the device's own turn on the forearm as the bind left it, measured fresh
        /// on each frame's bound pose, so nothing accumulates); the elbow goes where that puts the bay on its seat, slid along the lens's
        /// line (which barely moves it on the screen) so the upper arm keeps its length, else onto the upper arm's reach; the upper arm turns
        /// to the elbow. The hand rides the forearm. Before the suit, the gear and the effects read the arm.
        /// </summary>
        private void VisualDraftPresent(NativeDraftStage stage,Camera camera)
        {
            diPresentSlide=diPresentMiss=0;var plan=stage.Plant;var body=draftBody;float k=plan.ForeWeight;if(!plan.ForeAiming||k<=1e-4f||body?.GearDevice==null)return;
            var r=DraftArmFor(body,stage.Left);var upper=r.OtherUpper;var lower=r.OtherLower;var hand=r.OtherHand;if(upper==null||lower==null||hand==null)return;
            if(!VisualDraftBay(out var b0,out var n0))return;
            var s=upper.position;var e0=lower.position;var w0=hand.position;float lu=(e0-s).magnitude,lf=(w0-e0).magnitude;if(lu<1e-3f||lf<1e-3f)return;
            var inv=Quaternion.Inverse(lower.rotation);var axisL=inv*((w0-e0)/lf);var bayL=inv*(b0-e0);var nL=Vector3.ProjectOnPlane(inv*n0,axisL);if(nL.sqrMagnitude<1e-6f)return;
            var bt=Stage(plan.PresentAt);var ft=StageDir(plan.PresentForearm).normalized;var nt=Vector3.ProjectOnPlane(StageDir(plan.PresentNormal),ft);if(nt.sqrMagnitude<1e-6f||ft.sqrMagnitude<.5f)return;
            var turn=Quaternion.LookRotation(ft,nt)*Quaternion.Inverse(Quaternion.LookRotation(axisL,nL));var et=bt-turn*bayL;
            // The upper arm's length kept: slid along the lens's line (at most PresentSlide), then onto its reach.
            var v=(camera.transform.position-bt).normalized;var d=et-s;float bq=Vector3.Dot(v,d),disc=bq*bq-(d.sqrMagnitude-lu*lu),slide=0;
            if(disc>=0){float q=Mathf.Sqrt(disc),s1=-bq+q,s2=-bq-q;slide=Mathf.Abs(s1)<Mathf.Abs(s2)?s1:s2;}
            slide=Mathf.Clamp(slide,-NativeDraftInstall.PresentSlide,NativeDraftInstall.PresentSlide);et+=v*slide;var reach=et-s;if(reach.sqrMagnitude>1e-8f)et=s+reach.normalized*lu;
            diPresentSlide=slide;diPresentMiss=(et+turn*bayL-bt).magnitude;
            upper.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(e0-s,et-s),k)*upper.rotation;
            lower.rotation=Quaternion.Slerp(lower.rotation,turn,k);
        }

        // ---- the cartridge, the core and the shell ----
        /// <summary>
        /// The cartridge's frame this frame in Unity's world: the stage's plan, and from the catch (by its BayShare) the same plan relative
        /// to the drawn bay (the stage's bay and the drawn one differ by the model's proportions), clear of the receiving forearm.
        /// </summary>
        private void DraftCartridgeFrame(NativeDraftStage stage)
        {
            var plan=stage.Plant;var c=Stage(plan.Chip);var q=StageTurn(plan.ChipRotation);diCartScale=plan.Scale;
            if(diBay&&plan.BayShare>0)
            {
                var sa=Stage(plan.At);var sn=StageDir(plan.Normal);var sf=StageDir(plan.Forearm);var sb=Quaternion.LookRotation(sf,sn);var db=Quaternion.LookRotation(diBayF,diBayN);var inv=Quaternion.Inverse(sb);
                var dc=diBayAt+db*(inv*(c-sa));var dq=db*(inv*q);float k=plan.BayShare;c=Vector3.Lerp(c,dc,k);q=Quaternion.Slerp(q,dq,k);
            }
            // The guard: kept outside the receiving forearm's capsule (its sleeve and 1.5 cm) through the carry.
            if(diModel&&draftArm?.OtherLower!=null&&draftArm.OtherHand!=null&&diP<NativeInstallShot.CarryEnd)
            {
                var e=draftArm.OtherLower.position;var h=draftArm.OtherHand.position;var seg=h-e;float t=Mathf.Clamp01(Vector3.Dot(c-e,seg)/Mathf.Max(1e-6f,seg.sqrMagnitude));var near=e+seg*t;
                float radius=(draftBody!=null&&draftBody.GearRadius>.01f?draftBody.GearRadius:.04f)+.015f+.06f*diCartScale,d=(c-near).magnitude;
                if(d<radius){var outw=diBay?diBayN:(c-near).normalized;c+=outw*(radius-d);}
            }
            diCartAt=c;diCartTurn=q;
        }
        /// <summary>The cartridge in the air from the burst into the hand (Burst..Catch on the pose clock): from where the 2D card handed over
        /// into the drawn thumb and index pads as they reach for it (`pinch`, this frame's, after the contact pass), slowing, its bank
        /// settling to the catch's turn, so it visibly travels its last centimetres into the fingers.</summary>
        private void DraftCartridgeFlight(NativeDraftStage stage,Vector3 pinch)
        {
            var plan=stage.Plant;diCartScale=plan.Scale;diCartTurn=StageTurn(plan.ChipRotation);var crest=diCartTurn*Vector3.forward;
            float u=Mathf.Clamp01((diP-NativeInstallShot.Burst)/(NativeInstallShot.Catch-NativeInstallShot.Burst));
            var to=pinch-crest*((NativeDraftInstall.CoreZ(plan.Tier)+NativeDraftInstall.CrestEdge)*diCartScale);diCartAt=Vector3.Lerp(Stage(plan.BurstAt),to,NativeInstallShot.EaseOut(u));
        }
        /// <summary>The cartridge's drawn pinch and press points (world) on its frame this frame.</summary>
        private Vector3 CartPoint(NativeDraftStage stage,bool pinch)
        {
            float s=diCartScale;float along=(NativeDraftInstall.CoreZ(stage.Plant.Tier)+(pinch?NativeDraftInstall.CrestEdge:0))*s;
            return diCartAt+diCartTurn*(Vector3.forward*along+(pinch?Vector3.zero:Vector3.up*(NativeDraftInstall.FaceDepth*s)));
        }
        /// <summary>The held cartridge (the shell and, before the latch, its core), the latched core sinking into the seat, then the seated
        /// core face out in the bay (white-hot as the shell breaks), and the shell's shards. Every tier holds a built cartridge with the core
        /// module (NativeMachineModels.HeldPickup: a class kit or a boss relic without its own takes the common's in its colours); should a
        /// model without one be all there is, the device's own chip (GearChip) seats in the bay from the latch instead, as before the
        /// install, so the bay is never empty at the clack.</summary>
        private void DraftCartridgeDraw(NativeDraftStage stage,float time,bool reduced)
        {
            var plan=stage.Plant;string model=sourceModels.MachineModel(NativeMachineModels.HeldPickup(diTier),"pve-pickup");if(!sourceModels.Has(model))return;
            bool hasCore=sourceModels.PickupCore(model);
            if(!hasCore&&diBay&&diP>=NativeInstallShot.Contact&&draftBody?.GearDevice!=null)
            {
                gearBone??=GearBoneOf;float gr=Mathf.Round(Mathf.Clamp(draftBody.GearRadius,.02f,.08f)*1000)/1000;var seat=NativePilotGear.Seat(gr);var chip=diChip.linear*(1.6f+2*Mathf.Clamp01(1-(diA-NativeInstallShot.Release)/.25f));
                gearMatrix=DraftDeviceMatrix(draftBody)*Matrix4x4.Translate(new Vector3(-seat.X,seat.Y,seat.Z));sourceModels.Queue(NativeSourceModels.GearChip,gearMatrix,gearBone,null,0,null,Color.white,diAccent,1,null,null,new Vector4(chip.r,chip.g,chip.b,1),null,0);
            }
            diShell??=b=>b!=NativeMachineModels.CoreBody&&b!=NativeMachineModels.CoreLights;diCore??=b=>b==NativeMachineModels.CoreBody||b==NativeMachineModels.CoreLights;
            diCartBone??=b=>b==NativeMachineModels.Orbit?diCartOrbit:diCartM;diCartPart??=b=>b==1||b==NativeMachineModels.CoreLights||b==NativeMachineModels.Orbit?diCartTint:Color.white;
            float lights=plan.Lights;var lit=diChip.linear*lights*(.9f+.2f*NativeChipCartridge.Breath(diProfile.Look,time,reduced));var s=Vector3.one*diCartScale;
            if(plan.ChipShown)
            {
                diCartM=Matrix4x4.TRS(diCartAt,diCartTurn,s);diCartOrbit=diCartM*sourceModels.PickupOrbit(model,reduced?0:time);draftChip=diCartM;
                // The shell cracks under the fingers at the latch (its lights flare), the core snaps down into the seat.
                float crack=plan.Latch;diCartTint=lit*(1+1.2f*crack);sourceModels.Queue(model,diCartM,diCartBone,diShell,partTint:diCartPart);
                var coreM=Matrix4x4.TRS(diCartAt-(diBay?diBayN:diCartTurn*Vector3.up)*(NativeDraftInstall.LatchDepth*crack),diCartTurn,s);var keep=diCartM;diCartM=coreM;
                diCartTint=lit*(1+2*crack);sourceModels.Queue(model,coreM,diCartBone,diCore,partTint:diCartPart);diCartM=keep;
            }
            else if(plan.CoreSeated&&!diSiren)
            {
                // Face out along the bay's normal, its crest toward the hand, sunk to the floor; white-hot (3 to 1) as the shell breaks.
                var n=diBay?diBayN:StageDir(plan.Normal);var f=diBay?diBayF:StageDir(plan.Forearm);var at=diBay?diBayAt:Stage(plan.At);var turn=Quaternion.LookRotation(f,-n);
                float z=NativeDraftInstall.CoreZ(diTier)*diCartScale,hot=1+2*Mathf.Clamp01(1-(diA-NativeInstallShot.Release)/.25f);diCartTint=lit*hot;
                diCartM=Matrix4x4.TRS(at-n*.002f-turn*new Vector3(0,0,z),turn,s);sourceModels.Queue(model,diCartM,diCartBone,diCore,partTint:diCartPart);
            }
            {var n=diBay?diBayN:StageDir(plan.Normal);DraftShards(diBay?diBayAt+n*.009f:Stage(plan.At),n,diBay?diBayF:StageDir(plan.Forearm),NativeInstallShot.Release,reduced);}
            // Epic and up: the ring and its satellites leave the shell as it breaks, circle the forearm once (6 cm out) and shrink into the device.
            if(diProfile.Look.Ring&&diBay&&diA>=NativeInstallShot.Release&&diA<1.45f)
            {
                float u=(diA-NativeInstallShot.Release)/(1.45f-NativeInstallShot.Release),ang=u*Mathf.PI*2;var side=Vector3.Cross(diBayN,diBayF).normalized;
                var at=diBayAt+(diBayN*Mathf.Cos(ang)+side*Mathf.Sin(ang))*(.06f*(1-u*u))+diBayF*(-.03f*Mathf.Sin(ang*.5f));var turn=Quaternion.LookRotation(diBayF,diBayN*Mathf.Cos(ang)+side*Mathf.Sin(ang));
                diOrbitOnly??=b=>b==NativeMachineModels.Orbit;var m=Matrix4x4.TRS(at,turn,Vector3.one*diCartScale*(1-.85f*u*u))*Matrix4x4.Translate(-new Vector3(0,0,NativeDraftInstall.CoreZ(diTier)));
                diCartOrbit=m*sourceModels.PickupOrbit(model,reduced?0:time*3);diCartTint=lit*(1.6f-.6f*u);sourceModels.Queue(model,m,diCartBone,diOrbitOnly,partTint:diCartPart);
            }
        }
        private Func<int,bool> diOrbitOnly;
        // The shell's shards: thin slabs of the cartridge's metal with lit edges, flung out in its plane and toward the lens from under the
        // fingers at the release, spinning and slowing, gone by ShardsGone (the boss relics' burn to embers instead: DraftInstallFx).
        private void DraftShards(Vector3 origin,Vector3 n,Vector3 f,float born,bool reduced)
        {
            float t=diA-born;if(t<0||diA>=NativeInstallShot.ShardsGone||diProfile.Boss)return;var across=Vector3.Cross(n,f).normalized;
            var metal=NativeUi.Hex(diProfile.Look.MetalMid);var edge=diChip.linear*2.4f;int count=reduced?6:10+Mathf.Clamp(diProfile.Grade-2,0,4);float life=NativeInstallShot.ShardsGone-born;
            for(int i=0;i<count;i++)
            {
                float r1=NativeEntranceMath.Rand(7301,i),r2=NativeEntranceMath.Rand(7303,i),r3=NativeEntranceMath.Rand(7307,i),ang=(i+r1*.6f)/count*Mathf.PI*2;
                var radial=across*Mathf.Cos(ang)+f*Mathf.Sin(ang);var v=(radial*(.9f+.8f*r2)+n*(.35f+.4f*r3))*(1.2f);var p0=origin+radial*(.006f+.012f*r3);
                float k=3.5f,e=Mathf.Exp(-k*t),drift=(1-e)/k;var at=p0+v*drift+Vector3.down*(2f*t*t);float u=t/life,fade=1-u*u;if(fade<=.02f)continue;
                var spin=Quaternion.AngleAxis((300+400*r2)*t*(r1>.5f?1:-1),Vector3.Cross(radial,n).normalized)*Quaternion.LookRotation(radial,n);
                var size=new Vector3(.011f+.01f*r1,.0025f,.008f+.008f*r2)*(.35f+.65f*fade)*(diCartScale/.075f);
                sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(at,spin,size),metal);
                sourceModels.QueuePiece(WeaponShape.Cube,true,Matrix4x4.TRS(at+spin*new Vector3(0,0,size.z*.5f),spin,new Vector3(size.x,size.y*1.4f,size.z*.18f)),edge*fade);
            }
        }

        // ---- the device, the chest core, the surge ----
        /// <summary>The device on the draft body on the install's clock (NativeInstallShot.Device): the strips' boot and flare, the lid open
        /// with its overshoot and shut with its bounce, the bay's pulse and the chip's colour, the lens. The Hot Dog's draws a third larger.</summary>
        private void VisualDraftDevice(NativeSeat seat,float p,float time,Color chip)
        {
            var body=draftBody;if(body?.GearDevice==null||!sourceModels.PilotGear(body.GearRadius>.01f?body.GearRadius:.04f))return;
            gearBone??=GearBoneOf;var accent=AccentTint(seat.accentColor);var dev=NativeInstallShot.Device(p,diTurn,diShort,diProfile);
            var m=DraftDeviceMatrix(body);float r=Mathf.Round(Mathf.Clamp(body.GearRadius,.02f,.08f)*1000)/1000,strips=dev.Strips;
            gearMatrix=m;sourceModels.Queue(sourceModels.GearDeviceFor(body.GearRadius),m,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(strips,strips,strips,1),null,0);
            if(dev.Lid>.02f){var bay=(dev.BayChip?chip:body.AccentLinear)*dev.Bay;sourceModels.Queue(sourceModels.GearBayFor(body.GearRadius),m,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(bay.r,bay.g,bay.b,1),null,0);}
            var h=NativePilotGear.Hinge(r);var lens=(dev.LensChip?chip:body.AccentLinear)*dev.Lens;
            gearMatrix=m*Matrix4x4.TRS(new Vector3(-h.X,h.Y,h.Z),Quaternion.AngleAxis(dev.Lid*NativePilotGear.LidOpenDegrees,Vector3.forward),Vector3.one);
            sourceModels.Queue(NativeSourceModels.GearLid,gearMatrix,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(lens.r,lens.g,lens.b,1),null,0);
        }
        // The device's frame on the draft body: the Hot Dog's a third larger about its anchor (its noodle arm's device is tiny).
        private Matrix4x4 DraftDeviceMatrix(VisualBody body)=>diDog?body.GearDevice.localToWorldMatrix*Matrix4x4.Scale(Vector3.one*1.3f):body.GearDevice.localToWorldMatrix;
        /// <summary>The chest core on the draft body (never on the Hot Dog), as VisualGear draws it, igniting in the chip's colour at CoreAt.</summary>
        private void DraftCore(NativeSeat seat,float time)
        {
            var body=draftBody;if(body?.GearCore==null||diDog||!sourceModels.PilotGear(body.GearRadius>.01f?body.GearRadius:.04f))return;
            gearBone??=GearBoneOf;float since=diA-NativeInstallShot.CoreAt,ignite=since>=0?Mathf.Exp(-since/.35f):0,held=diA>=NativeInstallShot.CoreAt&&!diShort?.3f*(1-NativeInstallShot.Ramp(diA,NativeInstallShot.SettleStart(diTurn),NativeInstallShot.SettleEnd(diTurn))):diShort&&diA>=NativeInstallShot.CoreAt?.3f:0;
            float g=NativePilotGear.CoreGlow(time,1,-1,seat.id)*(1+2.5f*ignite+held);var accent=Color.Lerp(AccentTint(seat.accentColor),diChipAccent,Mathf.Clamp01(ignite*1.5f+held*2));
            gearMatrix=body.GearCore.localToWorldMatrix;sourceModels.Queue(NativeSourceModels.GearCore,gearMatrix,gearBone,null,0,null,Color.white,accent,1,null,null,new Vector4(g,g,g,1),null,0);
        }
        /// <summary>The surge's light along the receiving arm to the chest and the head at age `a` (0..1 per link: forearm, upper arm, chest,
        /// neck, head), the robot suit's per-bone band.</summary>
        private static float SurgeBand(float a,int link)
        {
            float at=NativeInstallShot.SurgeStart+.03f+.055f*link;if(a<at)return 0;float t=a-at;return Mathf.Clamp01(t/.04f)*Mathf.Exp(-Mathf.Max(0,t-.04f)/.45f);
        }
        // The surge's path on the drawn body (world): the bay, the elbow, the shoulder, the chest core, the visor; false without the bones.
        private readonly Vector3[] diPath=new Vector3[5];
        private bool DraftSurgePath(NativeDraftStage stage)
        {
            if(diModel&&draftArm?.OtherLower!=null&&draftArm.OtherUpper!=null&&draftArm.Chest!=null)
            {
                diPath[0]=diBay?diBayAt:draftArm.OtherHand.position;diPath[1]=draftArm.OtherLower.position;diPath[2]=draftArm.OtherUpper.position;
                diPath[3]=draftBody?.GearCore!=null?draftBody.GearCore.position:draftArm.Chest.position;diPath[4]=draftArm.Head!=null?draftArm.Head.position+draftArm.Head.TransformDirection(Vector3.forward)*.09f+Vector3.up*.06f:diPath[3]+Vector3.up*.3f;return true;
            }
            var pose=stage.Pose;bool left=!stage.Left;int h=left?CapturedRig.LHand:CapturedRig.RHand;
            diPath[0]=Stage(stage.Plant.At);diPath[1]=Stage(pose.WorldPosition[h-1]);diPath[2]=Stage(pose.WorldPosition[h-2]);diPath[3]=Stage(pose.WorldPosition[CapturedRig.Chest]+new NVector(0,.13f,.16f));diPath[4]=Stage(pose.WorldPosition[CapturedRig.Head]+new NVector(0,.19f,.16f));return true;
        }
        private Vector3 PathAt(float s)
        {
            // Lengths along the path, s 0..1 by distance.
            float total=0;for(int i=0;i<4;i++)total+=(diPath[i+1]-diPath[i]).magnitude;float want=Mathf.Clamp01(s)*total;
            for(int i=0;i<4;i++){float l=(diPath[i+1]-diPath[i]).magnitude;if(want<=l||i==3)return Vector3.Lerp(diPath[i],diPath[i+1],l>1e-5f?Mathf.Clamp01(want/l):0);want-=l;}
            return diPath[4];
        }

        // The surge's links through the robot suit (NativeRobotSuit.Bones, in its order up the receiving arm to the head).
        private static readonly int[] SurgeSuit={NativeRobotSuit.ForearmR,NativeRobotSuit.ArmR,NativeRobotSuit.Chest,NativeRobotSuit.Neck,NativeRobotSuit.Head};
        /// <summary>The suit on the draft body: the trims of each surge link lit in the chip's colour as the light passes (a band at about
        /// 55 ms a link, glowing up to four times), decaying to a sheen held to the settle.</summary>
        private void DraftSuitSurge(VisualBody body,NativeSeat seat)
        {
            if(diA<NativeInstallShot.SurgeStart||diSiren){VisualSuit(body.Root,seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0);return;}
            int chased=0;foreach(int b in SurgeSuit)chased|=1<<b;VisualSuit(body.Root,seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0,~chased);
            float sheen=(.1f+.1f*diProfile.Level)*NativeInstallShot.Ramp(diA,NativeInstallShot.SurgeEnd,NativeInstallShot.SurgeEnd+.3f)*(diShort?1:1-NativeInstallShot.Ramp(diA,NativeInstallShot.SettleStart(diTurn),NativeInstallShot.SettleEnd(diTurn)));
            for(int i=0;i<SurgeSuit.Length;i++){float band=Mathf.Clamp01(SurgeBand(diA,i)+sheen);VisualSuit(body.Root,seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0,1<<SurgeSuit[i],Color.Lerp(diAccent,diChipAccent,Mathf.Clamp01(band*2)),1+3*band);}
        }
        /// <summary>The face gear on the draft body: the visor's beat (VisorAt) flashes white-hot by the tier's visor share (toward white, never
        /// toward the chip's hue: the visor glows near saturation already, and gold over the teal visor read green) and decays back to the
        /// accent over .5 s; the chip's colour rims it as light round the visor (DraftInstallFx). A wild chip's visor flickers off and on twice.</summary>
        private void DraftFaceGear(VisualBody body,NativeSeat seat)
        {
            float t=diA-NativeInstallShot.VisorAt;if(diSiren||t<0){VisualFaceGear(VisualHeadOf(body),seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0);return;}
            float flash=VisorFlash(t)*(.65f+.35f*diProfile.Visor),glow=1+.6f*flash;if(diProfile.Wild&&(t>=.05f&&t<.09f||t>=.16f&&t<.2f))glow=.1f;
            VisualFaceGear(VisualHeadOf(body),seat.id,seat.bodyColor,seat.accentColor,0,null,null,null,0,Color.Lerp(diAccent,diWhiteAccent,.95f*flash),glow);
        }
        /// <summary>The visor beat's flash (0..1) `t` seconds after VisorAt: up in two frames, falling away over .5 s.</summary>
        private static float VisorFlash(float t){if(t<0||t>=.5f)return 0;float up=Mathf.Clamp01(t/.03f),fall=1-t/.5f;return up*fall*fall;}
        /// <summary>A cloth outfit's accent trims flaring from the pilot's accent to the chip's colour at the core's light (CoreAt), settling
        /// back over 1.5 s (the next bind writes the pilot's accent again).</summary>
        private void DraftAccentFlare(VisualBody body,NativeSeat seat)
        {
            float t=diA-NativeInstallShot.CoreAt;if(diSiren||t<0||t>1.5f||body.Cloth==null)return;float k=NativeInstallShot.Bump(t/1.5f*.5f+.5f)*(t<.1f?t/.1f:1);
            if(!ColorUtility.TryParseHtmlString(seat.accentColor??"#70e1d0",out var accent))accent=Color.cyan;var c=Color.Lerp(accent,diChip,k);
            foreach(var r in body.Cloth){if(r==null||Array.IndexOf(body.Sheldon,r)>=0)continue;r.GetPropertyBlock(body.Block);foreach(int id in AccentChannels)body.Block.SetColor(id,c);r.SetPropertyBlock(body.Block);}
            body.Accent=null;
        }

        // ---- the lights ----
        /// <summary>The install's lights in the muzzle slots (after NativeLighting.Draft, which empties them): the cartridge's from the burst,
        /// the bay's flash at the clack held as a glow, the chest core's at its ignite, the face's at the visor beat.</summary>
        private void DraftInstallLights(Camera camera,bool reduced)
        {
            diLights.Begin();if(diA<0){diLights.Resolve(System.Numerics.Vector3.Zero,0,1);NativeMuzzleLights.Upload(diLights);return;}float L=diProfile.Level;int hex=diProfile.Look.Core;
            NVector N(Vector3 v)=>new NVector(v.x,v.y,v.z);
            if(diA>=NativeInstallShot.Burst)
            {
                // (Held a little toward the lens and under the cartridge, so it lights the camera's side of the fingers holding it and the
                // black glove reads off the black suit, brighter as the hand lines it up and presses; the clack's at the bay.)
                var toLens=(camera.transform.position-diCartAt).normalized;
                float seat=diA-NativeInstallShot.Seat;bool seated=seat>=0;var at=seated&&diBay?diBayAt+diBayN*.03f+toLens*.02f:diCartAt+toLens*.045f-Vector3.up*.015f;
                float level=seated?(.4f+(2.5f+2.5f*L-.4f)*Mathf.Exp(-seat/.18f))*(diShort?1:1-NativeInstallShot.Ramp(diA,NativeInstallShot.SettleStart(diTurn),NativeInstallShot.SettleEnd(diTurn))):(1+L)*(1+.7f*NativeInstallShot.Ramp(diP,NativeInstallShot.CarryEnd-.04f,NativeInstallShot.Contact));
                diLights.Offer(1,N(at),new MuzzleLightLook{Color=hex,Peak=reduced?.6f:1,Range=seated?.6f:.45f},level,false);
            }
            float core=diA-NativeInstallShot.CoreAt;if(core>=0&&core<.3f&&draftBody?.GearCore!=null&&!diDog)diLights.Offer(2,N(draftBody.GearCore.position+draftBody.GearCore.forward*.06f),new MuzzleLightLook{Color=hex,Peak=1,Range=.5f},(1.5f+L)*(1-core/.3f),false);
            float face=diA-NativeInstallShot.VisorAt;if(face>=0&&face<.25f&&diPath[4]!=Vector3.zero)diLights.Offer(3,N(diPath[4]+Vector3.forward*.08f),new MuzzleLightLook{Color=hex,Peak=1,Range=.4f},(1+diProfile.Visor)*(1-face/.25f),false);
            diLights.Resolve(N(camera.transform.position),3,1);for(int k=0;k<diLights.Count;k++)diLights.Rgb[k*4+3]=.025f;NativeMuzzleLights.Upload(diLights);
        }

        // ---- the effects ----
        /// <summary>The effects sink in Unity's world (the entrance emitters take source axes: mirrored here).</summary>
        private sealed class DraftFxSink:INativeEntranceFx
        {
            public NativeVfxDraw Vfx;private static Vector3 U(NVector v)=>new Vector3(-v.X,v.Y,v.Z);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>Vfx.Glow(U(at),new Color(color.X,color.Y,color.Z,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>Vfx.Streak(U(head),U(velocity),new Color(color.X,color.Y,color.Z,0),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>Vfx.Smoke(U(at),new Color(albedo.X,albedo.Y,albedo.Z,0),opacity,size,u,seed,floor);
        }
        private static NVector S(Vector3 w)=>new NVector(-w.x,w.y,w.z);
        private static NVector Lin(Color c,float k=1)=>new NVector(c.r*k,c.g*k,c.b*k);
        /// <summary>
        /// The install's effects through the shared renderer (closed form in the ages; thinned by the effects quality): the motes into the
        /// fingertips and the glint at the catch, the cartridge's soft bloom, the hinge's vapour, the line-up's brackets, the clack's flash
        /// (white-cored; a void for the boss relics), its ring and sparks, the shell's prism sparks (the boss relics' embers), the lid's sheet
        /// of light, the surge's packet (a second for rare and up), the chest core's ring, the power-up's motes (epic and up), the nova and
        /// rays (legendary and up) and the crest's flourish. Reduced effects: half the flash, one ring, no sparks, motes or rays.
        /// </summary>
        private void DraftInstallFx(NativeDraftStage stage,Camera camera,float quality,bool reduced)
        {
            if(vfx==null||diA<0)return;diFx??=new DraftFxSink();diFx.Vfx=vfx;var fx=diFx;float a=diA,p=diP,L=diProfile.Level;var plan=stage.Plant;
            var rare=diChip.linear;var core=NativeUi.Hex(diProfile.Look.Core).linear;var accentC=NativeUi.Hex(diProfile.Look.Accent).linear;var white=new NVector(1,1,1);
            bool wild=diProfile.Wild;var n=diBay?diBayN:StageDir(plan.Normal);var bay=diBay?diBayAt:Stage(plan.At);var forward=camera.transform.forward;
            // The catch: motes converging into the fingertips, a glint at the contact, a soft bloom on the cartridge.
            if(!reduced&&a>=.46f&&a<NativeInstallShot.Catch+.02f)for(int i=0;i<6;i++){float u=Mathf.Clamp01((a-.46f)/(NativeInstallShot.Catch-.46f));var d=NativeEntranceMath.Direction(7411,i);var at=diPinch+new Vector3(-d.X,d.Y,d.Z)*(.09f*(1-u*u));fx.Glow(S(at),Lin(rare,1.8f),0,.008f,0,u,i);}
            if(a>=NativeInstallShot.Catch&&a<NativeInstallShot.Catch+.12f){float u=(a-NativeInstallShot.Catch)/.12f;NativeEntranceFx.Glint(fx,S(diPinch),white*(2.2f*(1-u)),.015f*(1+u),new NVector(0,1,0),new NVector(1,0,0),7419);}
            if(plan.ChipShown&&a>=NativeInstallShot.Burst&&a<.80f){float u=(a-NativeInstallShot.Burst)/(.80f-NativeInstallShot.Burst);fx.Glow(S(diCartAt),Lin(rare,.55f*(1-u)),0,.09f,1,u,7421);}
            // The hinge's vapour of light as the lid swings open.
            if(!reduced&&p>=NativeInstallShot.LidOpen&&p<NativeInstallShot.LidOpen+.3f&&diBay)for(int i=0;i<3;i++){float u=(p-NativeInstallShot.LidOpen-.05f*i)/.25f;if(u<0||u>1)continue;fx.Glow(S(bay+diBayF*-.03f+n*(.01f+.025f*u)),Lin(accentC,.6f*(1-u)),.0f,.012f+.02f*u,0,u,7431+i);}
            // The line-up's brackets: four corners of light snapping in round the cartridge's footprint, the accent to the rarity.
            if(p>=.96f&&p<NativeInstallShot.Seat+.02f&&diBay)
            {
                var across=Vector3.Cross(diBayN,diBayF).normalized;float half=.6f*diCartScale,longHalf=.62f*diCartScale;
                for(int k=0;k<4;k++)
                {
                    float snap=NativeInstallShot.EaseOut((p-(.98f+.02f*k))/.05f),rise=NativeInstallShot.EaseOut((p-.96f)/.04f);if(rise<=0)continue;float grow=1.8f-.8f*snap;
                    float sx=(k&1)==0?-1:1,sy=(k&2)==0?-1:1;var c=bay+diBayN*(.004f+.026f*(1-snap))+(across*sx*half+diBayF*sy*longHalf)*grow;
                    var col=Color.Lerp(accentC,rare,snap)*(1.6f+1.4f*snap);var leg=.012f*diCartScale/.075f;
                    fx.Streak(S(c),S(-across*sx*leg/.05f),Lin(col),.0018f,leg,.05f,0,0,7441+k);fx.Streak(S(c),S(-diBayF*sy*leg/.05f),Lin(col),.0018f,leg,.05f,0,0,7451+k);
                }
            }
            // The line-up's backlight: a soft glow of the chip's light just behind the fingers that line the cartridge up and press it, so the
            // black glove reads off the black under-suit (the cartridge's own light, held toward the lens, lights their front).
            if(diModel&&p>=NativeInstallShot.CarryEnd-.06f&&p<NativeInstallShot.Release+.02f){float w=NativeInstallShot.Ramp(p,NativeInstallShot.CarryEnd-.06f,NativeInstallShot.CarryEnd+.04f)*(1-NativeInstallShot.Ramp(p,NativeInstallShot.Seat,NativeInstallShot.Release+.02f));fx.Glow(S(diPinch+forward*.03f),Lin(rare,.9f*w),0,.05f,1,0,8171);}
            // The clack (on `a`): the flash at the bay, white at its heart (a void with a rim in the set's colour for the boss relics), its ring
            // of light in the bay's plane (additive streaks: it thins and fades to nothing as it grows, never a solid hoop), the sparks off the
            // frame. A common chip's grey would read dull: its light is white-hot with the pilot's accent for its rim (diRim).
            float seat=a-NativeInstallShot.Seat;var rim=diRim.linear;
            if(seat>=0&&seat<.4f)
            {
                float u=Mathf.Clamp01(seat/.25f),fall=(1-u)*(1-u)*(1-u),size=diProfile.Flash*(reduced?.5f:1);var at=bay+n*.012f;
                if(diProfile.Boss){fx.Glow(S(at),new NVector(0,0,0),.85f*fall,size*.5f,1,u,7461);fx.Glow(S(at),Lin(rare,3*fall),0,size*.8f,0,u,7462);}
                else{fx.Glow(S(at),Lin(rim,2.6f*fall),0,size,1,u,7461);fx.Glow(S(at),white*(3.5f*fall),0,size*.3f,0,u,7462);}
                if(!reduced&&diProfile.Grade>=6&&!diProfile.Boss){fx.Glow(S(at+camera.transform.right*.006f),new NVector(1.6f*fall,0,0),0,size*.9f,1,u,7463);fx.Glow(S(at-camera.transform.right*.006f),new NVector(0,0,1.6f*fall),0,size*.9f,1,u,7464);}
                if(wild&&!reduced)for(int k=0;k<3;k++)fx.Glow(S(at+camera.transform.right*(.006f*(k-1))),new NVector(k==0?2*fall:0,k==1?2*fall:0,k==2?2*fall:0),0,size*.6f,1,u,7465+k);
                int rings=reduced?1:diProfile.Rings,segs=reduced?12:20;var f=diBay?diBayF:StageDir(plan.Forearm);var side=Vector3.Cross(n,f).normalized;f=Vector3.Cross(side,n).normalized;
                for(int k=0;k<rings;k++)
                {
                    float v=Mathf.Clamp01((seat-.04f*k)/.18f);if(v<=0||v>=1)continue;float r=.02f+.12f*NativeInstallShot.EaseOut(v),fade=(1-v)*(1-v),arc=r*Mathf.PI*2/segs;var col=Lin(Color.Lerp(rim,Color.white,.35f*(1-v)),2.6f*fade);
                    for(int j=0;j<segs;j++){float ang=(j+.5f*k)/segs*Mathf.PI*2;var dir=side*Mathf.Cos(ang)+f*Mathf.Sin(ang);var tan=Vector3.Cross(n,dir);fx.Streak(S(at+dir*r),S(tan*(arc/.05f)),col,.0022f*(1-.7f*v),arc,.05f,.2f,v,8101+k*37+j);}
                }
                if(!reduced)NativeEntranceFx.Sparks(fx,a,NativeInstallShot.Seat,S(at),S(n),.9f,diProfile.Sparks,.9f,.3f,4,.5f,Lin(diProfile.Grade<=1?rim:accentC,2.6f),Lin(NativeUi.Hex(diProfile.Look.Inlay).linear,1.4f),.0022f,7471,quality,-10);
            }
            // The shell breaking: three prism sparks off its crystal (the boss relics' shell burns to embers).
            if(!reduced&&a>=NativeInstallShot.Release&&a<NativeInstallShot.Release+.5f)
            {
                if(diProfile.Boss)NativeEntranceFx.Embers(fx,a,NativeInstallShot.Release,NativeInstallShot.Release+.25f,S(bay+n*.01f),.03f,40,.25f,.5f,Lin(rare,1.6f),.006f,7481,quality,.6f);
                else NativeEntranceFx.Sparks(fx,a,NativeInstallShot.Release,S(bay+n*.012f),S(n+forward*-.3f),1.1f,3,.7f,.35f,2,.5f,white*2.5f,Lin(rare,2),.003f,7483,quality,-10);
            }
            // Legendary and up: the shell's corner gems pop as gold sparks as it breaks.
            if(!reduced&&diProfile.Look.Gems&&diBay&&a>=NativeInstallShot.Release&&a<NativeInstallShot.Release+.45f)
            {
                var across=Vector3.Cross(diBayN,diBayF).normalized;
                for(int k=0;k<4;k++){var corner=diBayAt+diBayN*.01f+across*((k&1)==0?-.3f:.3f)*diCartScale+diBayF*((k&2)==0?-.4f:.4f)*diCartScale;NativeEntranceFx.Sparks(fx,a,NativeInstallShot.Release,S(corner),S(diBayN),1.2f,4,.8f,.35f,3,.5f,new NVector(2.6f,1.9f,.6f),new NVector(1.2f,.6f,.1f),.0022f,7491+k*7,quality,-10);}
            }
            // The lid's sheet of light squeezed out of the closing gap.
            if(p>=NativeInstallShot.ShutStart&&p<NativeInstallShot.ShutSettled&&diBay){float u=(p-NativeInstallShot.ShutStart)/(NativeInstallShot.ShutSettled-NativeInstallShot.ShutStart);var across=Vector3.Cross(diBayN,diBayF).normalized;fx.Streak(S(bay+n*.006f+across*.02f),S(-across*.04f/.05f),Lin(rare,2.2f*(1-u)),.0025f*(1-u)+.0005f,.04f,.05f,.2f,u,7491);}
            // The surge: a packet of the chip's light running up the arm to the chest core and the visor (a fainter one behind it for rare and
            // up; a burning fuse for the Bombadier, with arcs for the bolts' crests, as green dashes for the terminals').
            if(p>=NativeInstallShot.SurgeStart&&p<NativeInstallShot.SurgeEnd+.12f&&DraftSurgePath(stage))
            {
                for(int k=0;k<(diProfile.Grade>=3&&!reduced?2:1);k++)
                {
                    float u=NativeInstallShot.Smooth((p-NativeInstallShot.SurgeStart-.15f*k)/(NativeInstallShot.SurgeEnd-NativeInstallShot.SurgeStart));if(u<=0||u>=1)continue;
                    var head=PathAt(u);var back=PathAt(Mathf.Max(0,u-.08f));if(wild)head+=new Vector3(NativeEntranceMath.Signed(7501,(int)(p*60)),NativeEntranceMath.Signed(7503,(int)(p*60)),0)*.03f;
                    float level=k==0?1:.45f;var col=diProfile.Crest==NativeChipCartridge.Crest.Terminal?new Color(.3f,1,.5f):diProfile.Crest==NativeChipCartridge.Crest.Fuse?new Color(1,.6f,.2f):rare;
                    fx.Glow(S(head),Lin(col,3f*level),0,.02f,1,u,7511+k);fx.Streak(S(head),S((head-back)/.05f),Lin(col,2.2f*level),.006f,.04f,.05f,.85f,u,7521+k);
                    if(!reduced)for(int m=0;m<3;m++){var d=NativeEntranceMath.Direction(7531+k,m);fx.Glow(S(head+new Vector3(d.X,d.Y,d.Z)*.02f),Lin(col,1.4f*level),0,.006f,0,u,7541+m);}
                    if(diProfile.Crest==NativeChipCartridge.Crest.Bolts&&!reduced)NativeEntranceFx.Bolt(fx,p,S(back),S(head),5,.012f,.5f,Lin(Color.white,2),Lin(col,1.5f),.003f,7551+k,quality);
                    if(diProfile.Crest==NativeChipCartridge.Crest.Fuse&&!reduced)NativeEntranceFx.Embers(fx,p,NativeInstallShot.SurgeStart,NativeInstallShot.SurgeEnd,S(head),.01f,60,.2f,.4f,Lin(new Color(1,.5f,.1f),1.6f),.004f,7561,quality);
                }
            }
            // An ability chip: the light forks at the core and runs down both arms into the hands, which flare in its colour for .3 s.
            if(diProfile.Ability&&diModel&&draftArm?.Hand!=null&&draftArm.OtherHand!=null&&draftArm.Upper!=null&&draftArm.OtherUpper!=null&&diPath[3]!=Vector3.zero&&a>=NativeInstallShot.CoreAt&&a<NativeInstallShot.CoreAt+.5f)
            {
                float u=NativeInstallShot.Smooth((a-NativeInstallShot.CoreAt)/.22f);
                for(int side=0;side<2;side++)
                {
                    var sh=side==0?draftArm.Upper.position:draftArm.OtherUpper.position;var el=side==0?draftArm.Lower.position:draftArm.OtherLower.position;var hd=side==0?draftArm.Hand.position:draftArm.OtherHand.position;
                    var head=u<.33f?Vector3.Lerp(diPath[3],sh,u*3):u<.66f?Vector3.Lerp(sh,el,(u-.33f)*3):Vector3.Lerp(el,hd,Mathf.Clamp01((u-.66f)*3));
                    if(u<1){fx.Glow(S(head),Lin(rare,2.4f),0,.016f,1,u,7651+side);}
                    float flare=a-NativeInstallShot.CoreAt-.22f;if(flare>=0&&flare<.3f)fx.Glow(S(hd),Lin(rare,2.2f*(1-flare/.3f)),0,.07f,1,flare/.3f,7655+side);
                }
            }
            // The visor beat's rim: the chip's light along the white-hot band's top and bottom edges, never over the band (laid on it, a gold
            // glow tinted the white visor pink; DraftFaceGear flashes the band itself toward white). The visor point is the band's top edge.
            float visorT=a-NativeInstallShot.VisorAt;if(diModel&&visorT>=0&&visorT<.5f&&diPath[4]!=Vector3.zero)
            {
                float fl=VisorFlash(visorT)*(.65f+.35f*diProfile.Visor);var right=camera.transform.right;var up=camera.transform.up;var edge=Lin(rim,2.2f*fl);
                for(int k=0;k<2;k++){var head=diPath[4]-forward*.035f+up*(k==0?.006f:-.07f)-right*.12f;fx.Streak(S(head),S(right*(-.24f/.05f)),edge,.0035f,.24f,.05f,.35f,visorT/.5f,8161+k);}
                fx.Glow(S(diPath[4]-forward*.035f+right*.125f-up*.032f),Lin(rim,1.2f*fl),0,.03f,1,visorT/.5f,8163);fx.Glow(S(diPath[4]-forward*.035f-right*.125f-up*.032f),Lin(rim,1.2f*fl),0,.03f,1,visorT/.5f,8164);
            }
            // The chest core igniting: a soft ring of its light.
            float coreAt=a-NativeInstallShot.CoreAt;if(coreAt>=0&&coreAt<.5f&&diPath[3]!=Vector3.zero){float u=coreAt/.5f;fx.Glow(S(diPath[3]+forward*-.04f),Lin(rare,1.6f*(1-u)*(1-u)),0,.05f+.08f*u,2,u,7571);}
            if(reduced)return;
            if(diProfile.Boss&&a>=NativeInstallShot.Seat&&a<NativeInstallShot.Seat+1.2f)DraftBossMotif(fx,camera,a,quality);
            // The power-up: motes rising off the device (epic and up); the nova at the chest core with its rays and gold motes (legendary and up).
            if(diProfile.Grade>=4&&p>=NativeInstallShot.PowerStart&&p<NativeInstallShot.PowerEnd+.5f)NativeEntranceFx.Embers(fx,p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd,S(bay),.04f,30,.35f,.8f,Lin(rare,1.4f),.005f,7581,quality,.4f);
            if(diProfile.Grade>=5&&a>=NativeInstallShot.PowerStart&&a<NativeInstallShot.PowerStart+.6f&&diPath[3]!=Vector3.zero)
            {
                float u=(a-NativeInstallShot.PowerStart)/.25f,ray=Mathf.Clamp01(1-(a-NativeInstallShot.PowerStart)/.6f);var at=diPath[3]+forward*-.05f;
                if(u<1)fx.Glow(S(at),Lin(rare,2.4f*(1-u)),0,.12f+.3f*u,1,u,7591);
                for(int k=0;k<8;k++){float ang=k*Mathf.PI/4+.2f;var dir=camera.transform.right*Mathf.Cos(ang)+camera.transform.up*Mathf.Sin(ang);fx.Streak(S(at+dir*.4f*ray),S(dir*8),Lin(rare,1.2f*ray*ray),.012f*ray,.25f,.05f,.9f,1-ray,7601+k);}
                NativeEntranceFx.Embers(fx,a,NativeInstallShot.PowerStart,NativeInstallShot.PowerStart+.35f,S(at),.15f,57,.5f,1f,Lin(new Color(1,.8f,.3f),1.5f),.005f,7611,quality,.3f);
            }
            // The crest's flourish over the power-up.
            if(p>=NativeInstallShot.PowerStart&&p<NativeInstallShot.PowerEnd+.4f)DraftCrestFx(fx,camera,p,quality);
        }
        // A boss relic's motif at its clack (NativeBossChips' sets in order): the Siren's violet smoke from the closing lid, 6:47's clock hand
        // sweeping round the bay, the Converter's molten sparks, THE HUM's flicker of light, THE DEEP END's droplets and ripple, NOCTIEL's
        // drifting feather and a halo's flash over the visor.
        private void DraftBossMotif(INativeEntranceFx fx,Camera camera,float a,float quality)
        {
            int set=NativeBossChips.SetOf(diTier);float t=a-NativeInstallShot.Seat;var bay=diBay?diBayAt:diPath[0];var n=diBay?diBayN:Vector3.up;var rare=diChip.linear;var right=camera.transform.right;
            switch(set)
            {
                case 0:for(int k=0;k<6;k++){float u=Mathf.Repeat(t*1.2f+k/6f,1);if(t<.06f)break;fx.Smoke(S(bay+n*(.02f+.1f*u)+right*(.03f*Mathf.Sin(k*2.3f+t))),new NVector(.4f,.2f,.7f),.5f*(1-u),.03f+.05f*u,u,7901+k,-10);}break;
                case 1:if(t<.5f){float ang=t/.5f*Mathf.PI*2;var dir=Quaternion.AngleAxis(-ang*Mathf.Rad2Deg,n)*Vector3.ProjectOnPlane(camera.transform.up,n).normalized;fx.Streak(S(bay+n*.008f+dir*.035f),S(dir*.7f),Lin(rare,2.2f*(1-t/.5f)),.002f,.035f,.05f,0,t/.5f,7911);}break;
                case 2:NativeEntranceFx.Sparks(fx,a,NativeInstallShot.Seat,S(bay+n*.01f),S(n),1.3f,16,1.1f,.5f,6,.4f,new NVector(3,1.4f,.3f),new NVector(1.2f,.2f,.02f),.0025f,7921,quality,-10);break;
                case 3:if(t<.35f&&Mathf.Repeat(t*28,1)<.5f)fx.Glow(S(bay+n*.05f),Lin(rare,1.4f),0,.35f,1,t/.35f,7931);break;
                case 4:{float u=Mathf.Clamp01(t/.5f);var face=Quaternion.FromToRotation(Vector3.forward,n);if(u<1){float r=.03f+.16f*NativeInstallShot.EaseOut(u);sourceModels.QueuePiece(WeaponShape.Ring,true,Matrix4x4.TRS(bay+n*.006f,face,new Vector3(r,r,r*.1f)),rare*(1.6f*(1-u)));}
                    for(int k=0;k<5;k++){float d=t-.04f*k;if(d<0||d>.6f)continue;var at=bay+n*(.03f+.25f*d-1.2f*d*d)+right*(.03f*(k-2));fx.Glow(S(at),Lin(rare,1.6f*(1-d/.6f)),0,.006f,0,d/.6f,7941+k);}}break;
                case 5:{float u=Mathf.Clamp01(t/1.2f);var at=diPath[4]+Vector3.up*(.12f-.35f*u)+right*(.06f*Mathf.Sin(u*9));fx.Streak(S(at),S(new Vector3(Mathf.Cos(u*9),-.5f,0)*.3f),new NVector(1.6f,1.6f,1.8f)*(1-u),.006f,.03f,.05f,.6f,u,7951);
                    if(t<.3f)fx.Glow(S(diPath[4]+Vector3.up*.16f),new NVector(2.2f,2.1f,1.8f)*(1-t/.3f),0,.12f,2,t/.3f,7952);}break;
            }
        }
        // The crest's flourish (NativeChipCartridge.Look.Crest) at the power-up: the crown's gold rising off the shoulders and a crown of
        // glints over the visor, the shards' crystal rubble, the bolts' arcs between the knuckles, the knot's and the flames' ink fire, the
        // crescent's ring glint round the device, the prism's hues along the surge.
        private void DraftCrestFx(INativeEntranceFx fx,Camera camera,float p,float quality)
        {
            var rare=diChip.linear;var head=diPath[4];var bay=diBay?diBayAt:diPath[0];var right=camera.transform.right;
            switch(diProfile.Crest)
            {
                case NativeChipCartridge.Crest.Crown:
                    if(diPath[2]!=Vector3.zero){NativeEntranceFx.Embers(fx,p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd,S(diPath[2]),.05f,24,.4f,.9f,Lin(new Color(1,.8f,.3f),1.6f),.005f,7701,quality,.3f);NativeEntranceFx.Embers(fx,p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd,S(diPath[2]+right*.28f),.05f,24,.4f,.9f,Lin(new Color(1,.8f,.3f),1.6f),.005f,7703,quality,.3f);}
                    {float u=NativeInstallShot.Ramp(p,NativeInstallShot.PowerStart,NativeInstallShot.PowerStart+.15f)*(1-NativeInstallShot.Ramp(p,NativeInstallShot.PowerEnd,NativeInstallShot.PowerEnd+.3f));for(int k=0;k<4;k++)NativeEntranceFx.Glint(fx,S(head+Vector3.up*.08f+right*(.045f*(k-1.5f))),new NVector(2,1.6f,.6f)*u,.02f,new NVector(0,1,0),new NVector(1,0,0),7711+k);}
                    break;
                case NativeChipCartridge.Crest.Shards:NativeEntranceFx.Sparks(fx,p,NativeInstallShot.PowerStart,S(bay),new NVector(0,1,0),1.2f,10,.6f,.6f,3,.4f,Lin(rare,2.2f),Lin(rare,1),.003f,7721,quality,-10);break;
                case NativeChipCartridge.Crest.Bolts:if(diModel&&draftArm?.OtherHand!=null)NativeEntranceFx.Bolt(fx,p,S(draftArm.OtherHand.position+right*.02f),S(draftArm.OtherHand.position-right*.03f),4,.01f,.5f,new NVector(2,2,2),Lin(rare,1.6f),.0025f,7731,quality);break;
                case NativeChipCartridge.Crest.Knot:case NativeChipCartridge.Crest.Flames:
                    {var at=diProfile.Crest==NativeChipCartridge.Crest.Flames?diPath[3]:bay;for(int k=0;k<6;k++){float u=Mathf.Repeat((p-NativeInstallShot.PowerStart)*1.6f+k/6f,1);fx.Smoke(S(at+Vector3.up*(.08f*u)+right*(.02f*Mathf.Sin(k*2.1f))),diProfile.Crest==NativeChipCartridge.Crest.Flames?new NVector(.35f,.15f,.6f):new NVector(.15f,.05f,.08f),.5f*(1-u),.03f+.03f*u,u,7741+k,-10);}}
                    break;
                case NativeChipCartridge.Crest.Crescent:{float u=NativeInstallShot.Ramp(p,NativeInstallShot.PowerStart,NativeInstallShot.PowerEnd);float ang=u*Mathf.PI*2;var dir=(right*Mathf.Cos(ang)+Vector3.up*Mathf.Sin(ang))*.05f;NativeEntranceFx.Glint(fx,S(bay+dir),new NVector(.6f,2.2f,2.4f)*(1-u),.018f,new NVector(0,1,0),new NVector(1,0,0),7751);}break;
            }
        }
    }
    // The draft stage's install cues (NativeInstallShot.Cues; presentation only): the existing bank keys layered on the stage's clock, once
    // a pick, for every viewer of the stage; a generated ui.install.* key, once the bank has it, replaces the layers it stands for. With
    // no LocalAssets bank they are silent, as every bank cue is; the install route at the clack stays NativeInstallCue's.
    public sealed partial class NativeAudio
    {
        /// <summary>The pick the draft stage shows (set by ConversionGame each frame, read and cleared here): its chip and tier, its age on
        /// the stage's clock, the drafter's body (1 a pilot, 2 the Hot Dog, 3 the Siren), how many cards break, whether it is a short
        /// follow-through, Reduced effects, and a key for the pick (its pick time).</summary>
        [NonSerialized] public string StageChip,StageTier;[NonSerialized] public float StageAge=-1;[NonSerialized] public int StageBody=1,StageBreaks;[NonSerialized] public bool StageShort,StageReduced;[NonSerialized] public double StageKey;
        private double stageHeard=double.NaN;private float stageLastA=-1,stageLastP=-1;
        private void InstallStage()
        {
            string chip=StageChip;StageChip=null;float a=StageAge;if(chip==null||!(a>=0)){stageHeard=double.NaN;return;}
            var pr=NativeInstallShot.Cached(StageTier,chip);float p=NativeInstallShot.PoseAge(a,pr.Hold,StageReduced);
            if(!StageKey.Equals(stageHeard)){stageHeard=StageKey;stageLastA=stageLastP=a>.3f?a:-1;}
            foreach(var c in NativeInstallShot.Cues){float t=c.Pose?p:a,last=c.Pose?stageLastP:stageLastA;if(t>=c.At&&last<c.At&&t-c.At<.3f&&NativeInstallShot.Plays(c,StageBody,pr.Grade))InstallSound(c,pr);}
            for(int k=0;k<Math.Min(3,StageBreaks);k++){float at=NativeInstallShot.ShatterAt(k+1);if(a>=at&&stageLastA<at)InstallSound(NativeInstallShot.Shatter,pr);}
            if(StageShort&&a>=NativeInstallShot.BloomAt&&stageLastA<NativeInstallShot.BloomAt)InstallSound(NativeInstallShot.Out,pr);
            if(pr.Boss&&a>=NativeInstallShot.Seat&&stageLastA<NativeInstallShot.Seat){int set=NativeBossChips.SetOf(pr.Tier);if(set>=0&&set<NativeInstallShot.BossSeat.Length)Cue(NativeInstallShot.BossSeat[set],null,null,.4f,1,null,null,true);}
            stageLastA=a;stageLastP=p;
        }
        private void InstallSound(in NativeInstallShot.Cue c,in NativeInstallShot.Profile pr)
        {
            if(c.Body==2){Toon(c.Key,null,c.Gain,true,c.Rate);return;}
            if(c.Bespoke!=null&&bank.Has(c.Bespoke)){if(c.Lead)Cue(c.Bespoke,null,null,Math.Min(1,c.Gain*1.6f),1,null,null,true);return;}
            Cue(c.Key,null,null,c.Gain,NativeInstallShot.Rate(c,pr),null,null,true);
        }
    }
}

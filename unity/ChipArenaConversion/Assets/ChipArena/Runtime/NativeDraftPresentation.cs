using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // draftPalm / draftCatch: the reaching palm and, from the pick, where it meets the card at the contact (virtual px).
        private int draftHover=-1,draftViewSeat;private bool draftViewing;private Rect[] draftCards=Array.Empty<Rect>();private Vector2 draftPalm,draftCatch,draftDevice2d;private NVector2[] draftPoints=new NVector2[16];
        // The highlighted card held across the gaps between cards for the turn it was highlighted in (NativeDraftHover).
        private readonly NativeDraftHover draftHeld=new NativeDraftHover();private double draftHeldTurn=double.NaN;
        private readonly Dictionary<Renderer,bool> pickVisibility=new Dictionary<Renderer,bool>();
        // The Siren personal pick replaces the arena view for the local player only (pve/chip-screen.ts): the fight keeps
        // simulating, so the live renderers are hidden instead of deactivating the arena.
        private void HideLive(){foreach(var r in arena.Root.GetComponentsInChildren<Renderer>()){if(!pickVisibility.ContainsKey(r))pickVisibility[r]=r.enabled;r.enabled=false;}}
        private void RestoreLive(){foreach(var pair in pickVisibility)if(pair.Key!=null)pair.Key.enabled=pair.Value;pickVisibility.Clear();}
        private bool PvePickScreen=>!menu&&arena!=null&&(PveFollowing||PvePickOpen)&&arena.Pve?.Holding!=true&&arena.Local.Hp>0&&!ReplayActive&&match.Phase==NativePhase.Fight;
        private bool DraftFrame()
        {
            bool pve=PvePickScreen,active=!menu&&arena!=null&&match.Phase==NativePhase.Draft;
            // The Deathmatch's own chip picks on this stage, the local pilot on it (NativeDeathmatchPickStage.cs).
            if(!active&&!pve&&PickStageFrame())return true;
            if(!active&&!pve){if(draftViewing){draftViewing=false;RestoreLive();presentation.ResetPresentation();}return false;}
            var seat=pve?arena.Local.Seat:match.Seat(match.DraftSeat);if(seat==null)return false;
            // A new picker: the stage restarts (its springs snap) and frames once the tray has been laid out for them.
            if(!draftViewing||draftViewSeat!=seat.id){RestoreLive();presentation.ResetPresentation();draftHover=-1;draftFocus=-1;draftHeld.Reset();ResetHolo();draftViewSeat=seat.id;draftViewing=true;draftCards=Array.Empty<Rect>();draftTrayLine=draftTrayBottom=-1;}
            float scale=NativeUiMotion.Measure(Screen.width,Screen.height,prefs.uiScale).Scale,width=Screen.width/scale,height=Screen.height/scale;
            // The Siren pick's follow-through (NativeDraftFollow) plays the pick on its snapshot of the offers.
            bool follow=pve&&PveFollowing;var row=pve?arena.Pve.Rows[seat.id]:null;int count=follow?pveFollowChips.Count:pve?row.Open?.Offers?.Count??0:seat.offers.Count;
            float age=follow?PveFollowAge:pve||match.Chosen==null?-1:(float)(match.Time-match.ChosenAt);var chosenChip=follow?pveFollowChips[pveFollowIndex]:!pve&&match.Chosen!=null?content.Chip(match.Chosen):null;
            // The considered card (NativeDraftStage.Focus): the chosen one, the local picker's hovered or focused card (held
            // across the gaps between cards until another is highlighted or the turn ends; draftHover is only set on the local
            // picker's enabled turn), else the boot scan then a 1.8 s cycle. The arm lifts toward the held card while it is
            // being reached for. Each card's centre goes to the stage as screen fractions, for the gaze and the reach.
            double turnStart=pve?(follow?pveFollowOpened:pveOpenAt):DraftStarted(seat);float since=pve?Time.unscaledTime-(float)turnStart:(float)(match.Time-turnStart);int n=Math.Min(count,draftCards.Length);
            if(draftPoints.Length<n)draftPoints=new NVector2[Math.Max(n,draftPoints.Length*2)];for(int i=0;i<n;i++)draftPoints[i]=new NVector2(draftCards[i].center.x/width,draftCards[i].center.y/height);
            int chosen=follow?pveFollowIndex:!pve&&match.Chosen!=null?seat.offers.IndexOf(match.Chosen):-1;if(turnStart!=draftHeldTurn){draftHeldTurn=turnStart;draftHeld.Reset();ResetHolo();}
            if(chosen>=0)draftHeld.Reset();else draftHeld.Step(draftHover,Time.unscaledDeltaTime);int focus=NativeDraftStage.Focus(chosen,draftHeld.Card,since,n,prefs.reducedEffects);bool consider=chosen<0&&draftHeld.Reaching;
            if(pve)HideLive();else arena.Root.SetActive(false);viewModelCamera.enabled=false;
            // Stage lights take the picker's colour (the Siren pick: its tier); the install blends them to the chip's from the clack.
            string tier=follow?pveFollowTier:row?.Open?.Tier;Color chipTint=chosenChip!=null?RarityColor(chosenChip.rarity):default;
            Color tint=pve?(tier=="rainbow"?Color.white:RarityColor(tier??"common")):SeatColor(seat);
            // The backdrop's heading band (the heading's bottom row, NativeDraftScreen.DraftHeader) and its variant: the PvE / Siren
            // personal pick's ward, the rainbow pick's prism.
            presentation.DraftHeadLine=draftHeadLine;presentation.DraftWard=pve;presentation.DraftPrism=pve&&tier=="rainbow";
            presentation.DraftBandTeam=seat.spec=="siren"?-1:NativeStatusVisuals.PilotBand(match.Teams,match.Cooperative,seat.AuthorityBoss,seat.team,seat.id==arena.LocalId);draftPalm=presentation.RenderDraft(seat,age,focus,consider,draftPoints,n,draftTrayLine,draftTrayBottom,tint,cameraView,prefs.reducedEffects,out var catchAt,chipTint,chosenChip?.rarity,chosenChip?.id,pve,match.Set)/scale;draftPalm.y=height-draftPalm.y;
            draftCatch=catchAt/scale;draftCatch.y=height-draftCatch.y;draftDevice2d=presentation.DraftDeviceScreen/scale;draftDevice2d.y=height-draftDevice2d.y;
            // The install's cues on the stage's clock, for every viewer of the stage (NativeAudio.InstallStage, next frame).
            if(chosenChip!=null&&nativeAudio!=null){nativeAudio.StageChip=chosenChip.id;nativeAudio.StageTier=chosenChip.rarity;nativeAudio.StageAge=presentation.DraftAge;nativeAudio.StageBody=seat.spec=="siren"?3:presentation.DraftDog?2:1;nativeAudio.StageBreaks=Math.Max(0,n-1);nativeAudio.StageShort=pve;nativeAudio.StageReduced=prefs.reducedEffects;nativeAudio.StageKey=pve?pveFollowAt:match.ChosenAt;}
            return true;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player draftRig;
        // The stage motion (NativeDraftStage: the pilot close, from the solar plexus up, the bow and the gaze springs, the
        // considering lift, the slowed catch in front of the chest and the wrist plant) and the cached draw delegates of the
        // pilot and the held chip (no per-frame closures).
        private readonly NativeDraftStage draftStage=new NativeDraftStage();private Matrix4x4 draftChip,draftChipOrbit;private Color draftChipTint;
        private Func<int,Matrix4x4> draftBone,draftChipBone;private Func<int,Color> draftChipPart;
        // The pick's age on the stage's smooth clock (-1 before the pick), for the tray's fly, shatter and spotlight.
        public float DraftAge=>draftStage.Age;
        // A new picker on the stage without the rest of ResetPresentation (the Deathmatch's comeback pick, mid-fight): the draft rig
        // and the model it carries go (RenderDraft makes them again and restarts the stage, as after a reset); the arena's
        // corpses, blood, wounds and gibs are left as they are. DraftStageRoot: the stage's own scene objects, kept in view.
        public void RestartDraftStage(){draftRig?.Dispose();draftRig=null;}
        public Transform DraftStageRoot=>draftRig?.Root!=null?draftRig.Root.transform:null;
        // The tray's lower shade on the stage's framing (fractions from the top): its line for a tray whose first row's top is
        // `trayTop` and where it turns opaque (NativeDraftStage.ShadeLine / ShadeDeep).
        public void DraftShade(float trayTop,out float line,out float deep){line=draftStage.ShadeLine(trayTop);deep=draftStage.ShadeDeep(line);}
        // The worn head cosmetic's top above the head joint, for the stage's framing (NativeDraftStage.Frame), measured once per
        // outfit as the portraits' hatTop is: the hat model's head-space bounds (the meshes drawn), or the Auditor halo's
        // reach (NativeAuditorHalo.Top); 0 bare-headed, for an unknown hat (nothing is drawn) and for the Siren drafter.
        private string draftCrownHat;private float draftCrown;private bool draftDevice;
        private float DraftCrown(string hat,bool siren)
        {
            if(siren||string.IsNullOrEmpty(hat)||hat=="none")return 0;if(hat==draftCrownHat)return draftCrown;
            draftCrownHat=hat;draftCrown=0;if(hat==NativeAuditorHalo.Hat)return draftCrown=NativeAuditorHalo.Top;if(hat==NativeSheldonHead.Hat)return draftCrown=NativeSheldonHead.Top;
            string id="hat-"+hat;if(sourceModels!=null)for(int i=0,n=sourceModels.PortraitPartCount(id);i<n;i++){sourceModels.PortraitPart(id,i,out var mesh,out int bone,out _,out _);if(mesh!=null&&bone==CapturedRig.Head)draftCrown=Mathf.Max(draftCrown,mesh.bounds.max.y);}
            return draftCrown;
        }
        // `age` the authoritative seconds since the pick (-1 before it), `focus` the considered card, `consider` whether the arm
        // lifts toward it (the local picker's highlighted card), `cards` the card centres as screen fractions (the first
        // `count`), `trayLine` / `trayBottom` the tray's top and bottom edges (fractions from the top; negative until the tray is
        // laid out). `tint` the stage's light before the clack (the picker's colour, the PvE pick's tier); `chipTint` the chosen chip's
        // rarity (sRGB), `chipTier` its tier (the cartridge: NativeMachineModels.PickupModel; the install's profile, NativeInstallShot) and
        // `chipId` its id; `shortCut` the PvE / Deathmatch follow-through (it ends on the power pose); `set` the draft's set (the hero
        // hold's hash). Returns the taking hand's pinch in screen pixels; `catchAt` where the cartridge is caught (the 2D card flies at it,
        // re-projected each frame as the camera moves) and `deviceAt` the bay (the readout's leader and the bloom). No table is drawn:
        // the portrait ends at the solar plexus, under the tray's lower shade; the install's shot pushes in from it (NativeDraftStage.Shot).
        public Vector2 RenderDraft(NativeSeat seat,float age,int focus,bool consider,NVector2[] cards,int count,float trayLine,float trayBottom,Color tint,Camera camera,bool reduced,out Vector2 catchAt,Color chipTint=default,string chipTier=null,string chipId=null,bool shortCut=false,int set=0)
        {
            if(draftRig==null){draftRig=captured.Create(transform,-101);draftStage.Reset();}sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            if(draftBone==null){draftBone=b=>draftRig.Bones[b].localToWorldMatrix;draftChipBone=b=>b==NativeMachineModels.Orbit?draftChipOrbit:draftChip;draftChipPart=b=>b==1||b==NativeMachineModels.CoreLights||b==NativeMachineModels.Orbit?draftChipTint:Color.white;}
            // The stage runs on the frame clock: the idle (and the Siren's) on its own presentation clock, the pick's age eased onto the
            // authoritative one (simulation ticks and network frames would otherwise step it); the body on the install's pose clock (its
            // hold at the clack), the camera and the impact on the age.
            float dt=Time.unscaledDeltaTime;bool siren=seat.spec=="siren";var stage=draftStage;var draftPose=stage.Pose;float a=stage.SmoothAge(age,dt);
            var pr=NativeInstallShot.Cached(chipTier??"common",chipId);float p=a<0?-1:NativeInstallShot.PoseAge(a,pr.Hold,reduced),rate=a<0?1:NativeInstallShot.PoseRate(a,pr.Hold,reduced);
            bool model=!siren&&VisualDraftModel(seat),dog=model&&NativeHotDog.Worn(VisualOutfitOf(seat.id));var plan=stage.Plant;
            plan.Device=!siren&&draftDevice;plan.Profile=pr;plan.Tier=pr.Tier;plan.Short=shortCut;plan.TurnSeconds=shortCut?NativeInstallShot.ShortEnd:NativeInstallShot.Turn(chipId);plan.Toon=dog;plan.Hero=NativeInstallShot.Hero(seat.id,chipId,set,pr);
            plan.Width=siren?NativeDraftInstall.SirenWidth:!model?NativeDraftInstall.VoxelWidth:dog?NativeDraftInstall.HotDogWidth:Mathf.Min(NativeDraftInstall.HeldWidth,NativeDraftInstall.HandShare*(draftArm!=null&&draftArm.Body==draftBody?draftArm.Glove:.09f));
            diA=a;diP=p;diProfile=pr;diModel=model;diSiren=siren;diDog=dog;diReduced=reduced;diShort=shortCut;diTier=pr.Tier;diTurn=plan.TurnSeconds;diChip=chipTint.a>0?chipTint:tint;diAccent=AccentTint(seat.accentColor);
            stage.Begin(dt,reduced,NativeDraftStage.IdleHeld(p));double time=stage.Clock;
            if(siren)NativeSourceMotion.Sample("siren-idle-melee",reduced?0:(float)time,0,draftPose);
            stage.Frame(camera.aspect,trayLine,trayBottom,DraftCrown(seat.hat,siren),siren,dt,VisualDraftModel(seat)?NativeDraftStage.ModelHeadTop:NativeDraftStage.HeadTop);
            stage.Shot(a,pr,plan.TurnSeconds,shortCut,siren,reduced,NativeInstallShot.DetailsBeside);stage.Step(dt*rate,p,focus,consider,cards,count,siren,reduced);
            draftRig.Root.transform.SetPositionAndRotation(new Vector3(0,.88f,0),Quaternion.identity);
            NativePoseWriter.Write(draftRig.Bones,draftPose,"the draft rig");
            // The camera: the shot's, rolled by the clack's kick (Project ignores the roll).
            camera.fieldOfView=NativeDraftStage.Fov;camera.transform.position=Stage(stage.CameraPosition);camera.transform.LookAt(Stage(stage.CameraTarget));if(stage.Roll!=0)camera.transform.Rotate(0,0,stage.Roll,Space.Self);
            // The stage's light keeps the picker's colour until the clack, then takes the chip's by its tier's share over .3 s (the common
            // and uncommon chips half mixed with the pilot's accent), so a common chip never greys the stage; the rim flashes at the clack.
            var chipMix=pr.AccentMix>0?Color.Lerp(diChip,SeatColorOf(seat,tint),pr.AccentMix):diChip;
            var stageTint=a>=NativeInstallShot.Seat&&chipTint.a>0?Color.Lerp(tint,chipMix,pr.Tint*NativeInstallShot.Ramp(a,NativeInstallShot.Seat,NativeInstallShot.Seat+.3f)):tint;
            float flash=a>=NativeInstallShot.Seat?(.4f+.6f*pr.Level)*Mathf.Pow(1-Mathf.Clamp01((a-NativeInstallShot.Seat)/.45f),2)*(reduced?.5f:1):0;
            NativeLighting.Draft(camera,stageTint,flash);
            // The animated cyber corridor behind the pilot (NativeDraftBackdropPresentation.cs), on the stage's frame step; the clack re-fires
            // its power-on front in the chip's colour.
            DraftBackdrop(camera,stageTint,a,a>=NativeInstallShot.Seat&&!reduced?a-NativeInstallShot.Seat:-1,trayLine,reduced,siren,dt,pr.Level,pr.Grade>=5,DraftHallDim(pr,a));
            int hand=stage.Left?9:13,other=hand==CapturedRig.LHand?CapturedRig.RHand:CapturedRig.LHand;Matrix4x4 plantFrom=draftRig.Bones[other].localToWorldMatrix;
            // The pilot on its visual body (NativeVisualBodyPresentation.cs VisualDraftPose) wearing the install's hand shapes, else the
            // voxel body and hat; the Siren drafter animated on the match clock (its idle look, eye halos lit).
            diGraspOn=model&&a>=0;if(diGraspOn){diLeftShape=NativeHandShapes.Left(p);diRightShape=NativeHandShapes.Right(p,plan.Settle,out diRightGrip,out diRightClench);}
            bool asModel=!siren&&VisualDraftPose(draftRig,seat,draftPose,(float)time,camera);VisualBodiesShown();diGraspOn=false;
            if(asModel&&dog&&draftBody!=null)DraftDogJiggle(draftBody,p);
            // The device's bay on the model as drawn: back into the stage hand's frame (source axes) for the plan's next frame.
            diBay=asModel&&VisualDraftBayFrame(out diBayAt,out diBayN,out diBayF);draftDevice=diBay;
            if(diBay)
            {
                var plantTo=draftRig.Bones[other].localToWorldMatrix*plantFrom.inverse;var back=plantTo.inverse;var bp=back.MultiplyPoint3x4(diBayAt);var bn=back.MultiplyVector(diBayN);var turn=System.Numerics.Quaternion.Inverse(draftPose.WorldRotation[other]);
                plan.BayLocal=NVector.Transform(new NVector(-bp.x,bp.y-.88f,bp.z)-draftPose.WorldPosition[other],turn);plan.BayNormal=NVector.Transform(new NVector(-bn.x,bn.y,bn.z),turn);
            }
            // The cartridge on its plan (on the drawn bay), the drawn taking hand onto it, the device on the install's clock, the chest core.
            if(a>=NativeInstallShot.Burst)DraftCartridgeFrame(stage);
            float contact=asModel?ContactWeight(p):0;var pinchAt=CartPoint(stage,true);
            if(contact>0){diPinch=VisualDraftContact(stage.Left,p,pinchAt,CartPoint(stage,false),diCartTurn,contact);if(p<NativeInstallShot.AlignEnd)diCartAt+=(diPinch-pinchAt)*(1-NativeInstallShot.Ramp(p,NativeInstallShot.CarryEnd,NativeInstallShot.AlignEnd))*contact;}
            else diPinch=a>=NativeInstallShot.Burst?pinchAt:Stage(stage.Palm);
            if(diBay)VisualDraftDevice(seat,p,(float)time,diChip.linear);if(asModel)DraftCore(seat,(float)time);DraftSurgePath(stage);
            // The voxel pilot's surge: its tint running up the receiving arm into the chest and the head.
            diVoxelPart??=b=>DraftVoxelTint(b);var voxelPart=siren||a<NativeInstallShot.SurgeStart?null:diVoxelPart;
            var root=draftRig.Root.transform.localToWorldMatrix;var sirenFace=siren?SirenStill(reduced?0:(float)time):null;int bodySkin=NativeBodySkins.Of(seat.weaponSkins);if(!asModel){sourceModels.Queue(siren?"siren":BodyModel(bodySkin,BodyCloseTier),root,draftBone,body:BodyTint(seat.bodyColor),accent:BodyAccent(bodySkin,seat.accentColor,seat.hat,seat.hatColor),look:sirenFace,partTint:voxelPart);
                if(!siren&&!AuditorHalo(seat.hat,draftRig.Bones[CapturedRig.Head].localToWorldMatrix,(float)time,1,reduced,0,null,camera,seat.hatColor))sourceModels.Queue(HatModel(seat.hat,seat.hatColor),root,draftBone,body:HatBody(seat.hat,seat.hatColor));
                // The voxel cuff's hex socket ring round the seated core.
                if(!siren&&p>=NativeInstallShot.Contact){var n=StageDir(plan.Normal);sourceModels.QueuePiece(WeaponShape.Ring,true,Matrix4x4.TRS(Stage(plan.At)+n*.004f,Quaternion.FromToRotation(Vector3.forward,n),new Vector3(.03f,.03f,.006f)),diChip.linear*(1.2f+1.5f*Mathf.Exp(-Mathf.Max(0,a-NativeInstallShot.Seat)/.3f)));}
            }
            if(siren){sirenLookCamera=camera;SirenLookFx(draftRig,sirenFace,0,null,reduced?0:(float)time);}
            // The cartridge: in the fingers (the Siren's claw), the core latched and seated, the shell's shards.
            draftChipTint=diChip.linear;
            if(siren)DraftSirenChip(stage,(float)time,reduced);else if(a>=NativeInstallShot.Burst)DraftCartridgeDraw(stage,(float)time,reduced);
            TeamBands(draftRig,DraftBandTeam);DrawBatches(null,0,false);
            // The install's lights into the muzzle slots NativeLighting.Draft emptied, and its effects.
            DraftInstallLights(camera,reduced);
            if(vfx!=null){vfx.Low=LowGraphics;vfx.Begin();if(siren)DraftSirenFx(stage,camera,Mathf.Clamp01(WeaponEffectsQuality),reduced);else DraftInstallFx(stage,camera,Mathf.Clamp01(WeaponEffectsQuality),reduced);}
            sourceModels.Flush(UsesInstancing);vfx?.Submit(camera);
            catchAt=camera.WorldToScreenPoint(Stage(stage.CatchPalm));DraftDeviceScreen=camera.WorldToScreenPoint(diBay?diBayAt:Stage(plan.At));
            return camera.WorldToScreenPoint(diPinch);
        }
        // The hall's dip: legendary and up dim it a quarter over the line-up (released at the clack); a boss relic's dips 40% for .3 s after it.
        private static float DraftHallDim(in NativeInstallShot.Profile pr,float a)
        {
            float dim=pr.Grade>=5?.25f*NativeInstallShot.Ramp(a,NativeInstallShot.CarryEnd,NativeInstallShot.Seat)*(1-NativeInstallShot.Ramp(a,NativeInstallShot.Seat,NativeInstallShot.Seat+.06f)):0;
            if(pr.Boss)dim=Mathf.Max(dim,.4f*NativeInstallShot.Bump((a-NativeInstallShot.Seat)/.3f));return dim;
        }
        /// <summary>The bay on screen this frame (pixels, Unity's y up): the readout's leader and the short cut's bloom start there.</summary>
        public Vector2 DraftDeviceScreen;
        /// <summary>The shot's state for the 2D layer: the burst's hand-over (screen fractions and the cartridge's width there) and the weight.</summary>
        public bool DraftDog=>diDog;public bool DraftBursting=>draftStage.Bursting;public Vector2 DraftBurstScreen=>new Vector2(draftStage.BurstScreen.X,draftStage.BurstScreen.Y);public float DraftBurstWidth=>draftStage.BurstWidth;public float DraftShotWeight=>draftStage.ShotWeight;
        private static Color SeatColorOf(NativeSeat seat,Color fallback)=>ColorUtility.TryParseHtmlString(seat.accentColor??"",out var c)?c:fallback;
        // The voxel pilot's surge tint per part bone (the receiving hand, forearm, upper arm, chest, head in turn).
        private Func<int,Color> diVoxelPart;
        private Color DraftVoxelTint(int bone)
        {
            bool right=draftStage.Left;int link=bone==(right?CapturedRig.RHand:CapturedRig.LHand)?0:bone==(right?CapturedRig.RElbow:CapturedRig.LElbow)?1:bone==(right?CapturedRig.RArm:CapturedRig.LArm)?2:bone==CapturedRig.Chest?3:bone==CapturedRig.Head?4:-1;
            if(link<0)return Color.white;float band=SurgeBand(diA,link);return Color.Lerp(Color.white,diChip*1.8f,band);
        }
        // The Hot Dog's jiggle on the draft body: squashed at the clack, stretched at the core's light.
        private void DraftDogJiggle(VisualBody body,float p)
        {
            HotDogJiggle(body);var rig=body.Dog;if(rig==null)return;
            if(p>=NativeInstallShot.Seat&&p-NativeInstallShot.Seat<Time.unscaledDeltaTime*1.5f)NativeHotDog.Impulse(ref rig.Spring,new System.Numerics.Vector3(0,-.6f,0));
            if(p>=NativeInstallShot.CoreAt&&p-NativeInstallShot.CoreAt<Time.unscaledDeltaTime*1.5f)NativeHotDog.Impulse(ref rig.Spring,new System.Numerics.Vector3(0,.5f,0));
        }
        // The Siren's cartridge in her claw until the crush (.8 of her palm), then the shell's shards between the claws.
        private void DraftSirenChip(NativeDraftStage stage,float time,bool reduced)
        {
            var plan=stage.Plant;if(!plan.ChipShown){if(diA>=NativeInstallShot.Seat&&diA<NativeInstallShot.ShardsGone){var cam=sirenLookCamera!=null?sirenLookCamera.transform:null;DraftShards(Stage(plan.Chip),cam!=null?-cam.forward:Vector3.forward,cam!=null?cam.right:Vector3.right,NativeInstallShot.Seat,reduced);}return;}
            string model=sourceModels.MachineModel(NativeMachineModels.PickupModel(diTier),"pve-pickup");if(!sourceModels.Has(model))return;
            int h=stage.Left?CapturedRig.LHand:CapturedRig.RHand;diCartAt=Stage(plan.Chip);diCartTurn=StageTurn(plan.ChipRotation);diCartScale=plan.Scale;
            draftChip=Matrix4x4.TRS(diCartAt,diCartTurn,Vector3.one*plan.Scale);draftChipOrbit=draftChip*sourceModels.PickupOrbit(model,reduced?0:time);draftChipTint=diChip.linear*(1.3f+.6f*NativeInstallShot.Ramp(diA,1.0f,NativeInstallShot.Seat));
            sourceModels.Queue(model,draftChip,draftChipBone,partTint:draftChipPart);
        }
        // The Siren's devour: the light leaking between her claws, the crush's flash, the core's light streaming into her grin, the laugh's smoke.
        private void DraftSirenFx(NativeDraftStage stage,Camera camera,float quality,bool reduced)
        {
            if(diA<0)return;diFx??=new DraftFxSink();diFx.Vfx=vfx;var fx=diFx;var plan=stage.Plant;var claw=Stage(plan.Chip);var grin=Stage(stage.Pose.WorldPosition[CapturedRig.Head]+NVector.Transform(new NVector(0,.07f,.22f),stage.Pose.WorldRotation[CapturedRig.Head]));
            var rare=diChip.linear;var violet=new Color(.55f,.35f,1f);
            // The held cartridge's own light from the catch to the crush, beating faster as she lifts it, so it reads in the claw even
            // where her talons cover the model.
            if(diA>=NativeInstallShot.Catch&&diA<NativeInstallShot.Seat){float beat=.75f+.25f*Mathf.Sin(diA*(18+20*NativeInstallShot.Ramp(diA,NativeInstallShot.Catch,NativeInstallShot.Seat)));fx.Glow(S(claw),Lin(rare,(.9f+.5f*NativeInstallShot.Ramp(diA,.8f,NativeInstallShot.Seat))*beat),0,.07f,1,0,7841);}
            if(diA>=.8f&&diA<NativeInstallShot.Seat&&!reduced)for(int k=0;k<6;k++){float f=(.5f+.5f*Mathf.Sin(diA*40+k*1.7f))*NativeInstallShot.Ramp(diA,.8f,1.0f);var dir=Quaternion.AngleAxis(k*60+diA*90,camera.transform.forward)*camera.transform.up;fx.Streak(S(claw+dir*.06f),S(dir*1.2f),Lin(rare,1.6f*f),.004f,.05f,.05f,.8f,0,7801+k);}
            float seat=diA-NativeInstallShot.Seat;if(seat>=0&&seat<.35f){float u=seat/.25f,fall=Mathf.Pow(Mathf.Clamp01(1-u),3);fx.Glow(S(claw),Lin(rare,2.4f*fall),0,.25f,1,u,7811);fx.Glow(S(claw),new NVector(3,3,3)*fall,0,.07f,0,u,7812);}
            if(diA>=NativeInstallShot.Seat&&diA<1.34f&&!reduced)for(int k=0;k<10;k++){float u=Mathf.Repeat((diA-NativeInstallShot.Seat)*4+k/10f,1);var at=Vector3.Lerp(claw,grin,u)+Vector3.up*(.03f*Mathf.Sin(u*Mathf.PI));fx.Glow(S(at),Lin(rare,1.6f*(1-u)),0,.01f,0,u,7821+k);}
            if(diA>=1.34f&&diA<1.9f&&!reduced)for(int k=0;k<5;k++){float u=Mathf.Repeat((diA-1.34f)*1.4f+k/5f,1);fx.Smoke(S(grin+Vector3.up*(.1f*u)+camera.transform.right*(.05f*Mathf.Sin(k*1.9f))),Lin(violet,.6f),.4f*(1-u),.06f+.06f*u,u,7831+k,-10);}
        }
        // A stage rig root-frame point (source axes) in Unity world space (the rig root stands at y .88, unrotated).
        private static Vector3 Stage(NVector p)=>new Vector3(-p.X,p.Y+.88f,p.Z);
    }
}

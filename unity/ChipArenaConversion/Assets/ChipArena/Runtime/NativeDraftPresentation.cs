using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private int draftHover=-1,draftViewSeat;private bool draftViewing;private Rect[] draftCards=Array.Empty<Rect>();private Vector2 draftPalm;private NVector2[] draftPoints=new NVector2[16];
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
            if(!active&&!pve){if(draftViewing){draftViewing=false;RestoreLive();presentation.ResetPresentation();}return false;}
            var seat=pve?arena.Local.Seat:match.Seat(match.DraftSeat);if(seat==null)return false;
            // A new picker: the stage restarts (its springs snap) and frames once the tray has been laid out for them.
            if(!draftViewing||draftViewSeat!=seat.id){RestoreLive();presentation.ResetPresentation();draftHover=-1;draftFocus=-1;draftHeld.Reset();draftViewSeat=seat.id;draftViewing=true;draftCards=Array.Empty<Rect>();draftTrayLine=-1;}
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
            int chosen=follow?pveFollowIndex:!pve&&match.Chosen!=null?seat.offers.IndexOf(match.Chosen):-1;if(turnStart!=draftHeldTurn){draftHeldTurn=turnStart;draftHeld.Reset();}
            if(chosen>=0)draftHeld.Reset();else draftHeld.Step(draftHover,Time.unscaledDeltaTime);int focus=NativeDraftStage.Focus(chosen,draftHeld.Card,since,n,prefs.reducedEffects);bool consider=chosen<0&&draftHeld.Reaching;
            if(pve)HideLive();else arena.Root.SetActive(false);viewModelCamera.enabled=false;
            // Stage lights take the picker's colour, blending to the chip's rarity over .5 s after the pick (the Siren pick: its tier).
            string tier=follow?pveFollowTier:row?.Open?.Tier;Color chipTint=chosenChip!=null?RarityColor(chosenChip.rarity):default;
            Color tint=pve?(tier=="rainbow"?Color.white:RarityColor(tier??"common")):SeatColor(seat);if(age>=0&&chosenChip!=null)tint=Color.Lerp(tint,chipTint,Mathf.Clamp01(age/.5f));
            presentation.DraftBandTeam=seat.spec=="siren"?-1:NativeStatusVisuals.PilotBand(match.Teams,match.Cooperative,seat.AuthorityBoss,seat.team,seat.id==arena.LocalId);draftPalm=presentation.RenderDraft(seat,age,focus,consider,draftPoints,n,draftTrayLine,tint,cameraView,prefs.reducedEffects,chipTint)/scale;draftPalm.y=height-draftPalm.y;return true;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player draftRig;
        // The stage motion (NativeDraftStage: the pilot behind the tray, the bow and the gaze springs, the slowed reach and the
        // wrist plant) and the cached draw delegates of the pilot and the held chip (no per-frame closures).
        private readonly NativeDraftStage draftStage=new NativeDraftStage();private Matrix4x4 draftChip;private Color draftChipTint;
        private Func<int,Matrix4x4> draftBone,draftChipBone;private Func<int,Color> draftChipPart;
        // The pick's age on the stage's smooth clock (-1 before the pick), for the tray's fly, shatter and spotlight.
        public float DraftAge=>draftStage.Age;
        // `age` the authoritative seconds since the pick (-1 before it), `focus` the considered card, `consider` whether the arm
        // lifts toward it (the local picker's highlighted card), `cards` the card centres as screen fractions (the first
        // `count`), `trayLine` the tray's top edge (fraction from the top; negative until the tray is laid out). `chipTint` is the chosen chip's rarity (sRGB) for its die and the plant pulse (the stage tint when not
        // given). Returns the reaching palm in screen pixels (the chosen card flies there).
        public Vector2 RenderDraft(NativeSeat seat,float age,int focus,bool consider,NVector2[] cards,int count,float trayLine,Color tint,Camera camera,bool reduced,Color chipTint=default)
        {
            if(draftRig==null){draftRig=captured.Create(transform,-101);draftStage.Reset();}sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            if(draftBone==null){draftBone=b=>draftRig.Bones[b].localToWorldMatrix;draftChipBone=b=>draftChip;draftChipPart=b=>b==1?draftChipTint:Color.white;}
            // The stage runs on the frame clock: the idle (and the Siren's) on its own presentation clock, the pick's age eased
            // onto the authoritative one (simulation ticks and network frames would otherwise step it).
            float dt=Time.unscaledDeltaTime;bool siren=seat.spec=="siren";var stage=draftStage;var draftPose=stage.Pose;age=stage.SmoothAge(age,dt);stage.Begin(dt,reduced);double time=stage.Clock;
            if(siren)NativeSourceMotion.Sample("siren-idle-melee",reduced?0:(float)time,0,draftPose);
            stage.Frame(camera.aspect,trayLine,seat.hat!="none",siren,dt);stage.Step(dt,age,focus,consider,cards,count,siren,reduced);
            draftRig.Root.transform.SetPositionAndRotation(new Vector3(0,.88f,0),Quaternion.identity);
            for(int i=0;i<22;i++){draftRig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(draftPose.Position[i]));draftRig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(draftPose.Rotation[i]));}
            float pulse=age>=0&&!reduced?NativeDraftPlant.Pulse(age):-1,flash=pulse>=0?(1-pulse)*(1-pulse):0;
            camera.fieldOfView=NativeDraftStage.Fov;camera.transform.position=Stage(stage.CameraPosition);camera.transform.LookAt(Stage(stage.CameraTarget));NativeLighting.Draft(camera,tint,flash);
            var plant=stage.Plant;int hand=stage.Left?9:13;
            // draft-stage.ts animates the Siren drafter (animateSiren on the match clock): its idle look, eye halos lit.
            var root=draftRig.Root.transform.localToWorldMatrix;var sirenFace=siren?SirenStill(reduced?0:(float)time):null;sourceModels.Queue(siren?"siren":"pilot",root,draftBone,body:BodyTint(seat.bodyColor),accent:AccentTint(seat.accentColor),look:sirenFace);if(!siren)sourceModels.Queue("hat-"+seat.hat,root,draftBone);
            if(siren){sirenLookCamera=camera;SirenLookFx(draftRig,sirenFace,0,null,reduced?0:(float)time);}
            // The chip at its real size in the hand (never bigger than the hand), carried to the other wrist and pressed in.
            var palm=Stage(stage.Palm);draftChipTint=(chipTint.a>0?chipTint:tint).linear;
            if(age>=NativeDraftPlant.Contact&&(siren||plant.ChipShown))
            {
                float scale=NativeDraftPlant.HeldChipScale(age,(float)time,reduced);var at=siren?palm:Stage(plant.Chip);var turn=siren?draftRig.Bones[hand].rotation*Quaternion.Euler(90,0,0):CapturedAnimation.Quat(CapturedRig.Mirror(plant.ChipRotation));
                if(scale>.001f){draftChip=Matrix4x4.TRS(at,turn,Vector3.one*scale);sourceModels.Queue("pve-pickup",draftChip,draftChipBone,partTint:draftChipPart);}
            }
            // The plant pulse: two rarity rings expanding around the forearm, a spark at the cuff and the rim light's flash.
            if(pulse>=0)
            {
                var at=siren?palm:Stage(plant.At);var axis=Quaternion.FromToRotation(Vector3.forward,siren?Vector3.up:CapturedAnimation.Vec(CapturedRig.Mirror(plant.Forearm)));var rare=draftChipTint;
                for(int ring=0;ring<2;ring++){float u=Mathf.Clamp01(pulse*1.35f-ring*.3f),fade=(1-u)*(1-u);if(u<=0||fade<.03f)continue;float r=.06f+.14f*NativeUiMotion.Bezier(.2f,.8f,.2f,1,u);sourceModels.QueuePiece(WeaponShape.Ring,true,Matrix4x4.TRS(at,axis,new Vector3(r,r,r*.6f)),rare*(1.6f*fade));}
                float s=.03f*(1-pulse);if(s>.002f)sourceModels.QueuePiece(WeaponShape.Cube,true,Matrix4x4.TRS(at,Quaternion.identity,Vector3.one*s),rare*2);
            }
            TeamBands(draftRig,DraftBandTeam);DrawBatches(null,0,false);sourceModels.Flush(UsesInstancing);return camera.WorldToScreenPoint(palm);
        }
        // A stage rig root-frame point (source axes) in Unity world space (the rig root stands at y .88, unrotated).
        private static Vector3 Stage(NVector p)=>new Vector3(-p.X,p.Y+.88f,p.Z);
    }
}

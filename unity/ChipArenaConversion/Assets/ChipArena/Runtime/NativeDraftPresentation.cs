using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private int draftHover=-1,draftViewSeat;private bool draftViewing;private Rect[] draftCards=Array.Empty<Rect>();private Vector2 draftPalm;
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
            if(!draftViewing||draftViewSeat!=seat.id){RestoreLive();presentation.ResetPresentation();draftHover=-1;draftFocus=-1;draftViewSeat=seat.id;draftViewing=true;}
            float scale=NativeUiMotion.Measure(Screen.width,Screen.height,prefs.uiScale).Scale,width=Screen.width/scale,height=Screen.height/scale;
            // The Siren pick's follow-through (NativeDraftFollow) plays the pick on its snapshot of the offers.
            bool follow=pve&&PveFollowing;var row=pve?arena.Pve.Rows[seat.id]:null;int count=follow?pveFollowChips.Count:pve?row.Open?.Offers?.Count??0:seat.offers.Count;
            float age=follow?PveFollowAge:pve||match.Chosen==null?-1:(float)(match.Time-match.ChosenAt);var chosenChip=follow?pveFollowChips[pveFollowIndex]:!pve&&match.Chosen!=null?content.Chip(match.Chosen):null;
            // Look target: the chosen card, the local picker's hovered or focused card, else the boot scan then a 1.8 s cycle.
            float since=pve?Time.unscaledTime-(follow?pveFollowOpened:pveOpenAt):(float)(match.Time-DraftStarted(seat));int scan=since<count*.22f?Mathf.FloorToInt(Math.Max(0,since)/.22f):(int)(since/1.8f)%Math.Max(1,count);
            int index=follow?pveFollowIndex:!pve&&match.Chosen!=null?seat.offers.IndexOf(match.Chosen):draftHover>=0?draftHover:scan;index=Mathf.Clamp(index,0,Math.Max(0,draftCards.Length-1));
            Rect card=draftCards.Length>0?draftCards[index]:new Rect(width/2,180,1,1);float bottom=Mathf.Min(draftTrayBottom,.49f);
            if(pve)HideLive();else arena.Root.SetActive(false);viewModelCamera.enabled=false;
            // Stage lights take the picker's colour, blending to the chip's rarity over .5 s after the pick (the Siren pick: its tier).
            string tier=follow?pveFollowTier:row?.Open?.Tier;Color chipTint=chosenChip!=null?RarityColor(chosenChip.rarity):default;
            Color tint=pve?(tier=="rainbow"?Color.white:RarityColor(tier??"common")):SeatColor(seat);if(age>=0&&chosenChip!=null)tint=Color.Lerp(tint,chipTint,Mathf.Clamp01(age/.5f));
            presentation.DraftBandTeam=seat.spec=="siren"?-1:NativeStatusVisuals.PilotBand(match.Teams,match.Cooperative,seat.AuthorityBoss,seat.team,seat.id==arena.LocalId);draftPalm=presentation.RenderDraft(seat,pve?arena.Time:match.Time,age,new Vector2(card.center.x/width,1-card.center.y/height),bottom,tint,cameraView,draftHover>=0||age>=0,prefs.reducedEffects,chipTint)/scale;draftPalm.y=height-draftPalm.y;return true;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player draftRig;private readonly CapturedRig.Pose draftPose=new CapturedRig.Pose();private float draftWeight;
        // The wrist plant (NativeDraftPlant) and the cached draw delegates of the pilot and the held chip (no per-frame closures).
        private readonly NativeDraftPlant draftPlant=new NativeDraftPlant();private Matrix4x4 draftChip;private Color draftChipTint;
        private Func<int,Matrix4x4> draftBone,draftChipBone;private Func<int,Color> draftChipPart;
        // `chipTint` is the chosen chip's rarity (sRGB) for its die and the plant pulse (the stage tint when not given).
        public Vector2 RenderDraft(NativeSeat seat,double time,float age,Vector2 card,float trayBottom,Color tint,Camera camera,bool reach,bool reduced,Color chipTint=default)
        {
            if(draftRig==null){draftRig=captured.Create(transform,-101);draftWeight=0;}sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            if(draftBone==null){draftBone=b=>draftRig.Bones[b].localToWorldMatrix;draftChipBone=b=>draftChip;draftChipPart=b=>b==1?draftChipTint:Color.white;}
            bool siren=seat.spec=="siren";NativeDraftRules.Pose(draftPose,reduced?0:(float)time);if(siren)NativeSourceMotion.Sample("siren-idle-melee",(float)time,0,draftPose);
            void Apply(){for(int i=0;i<22;i++){draftRig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(draftPose.Position[i]));draftRig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(draftPose.Rotation[i]));}}
            draftRig.Root.transform.SetPositionAndRotation(new Vector3(0,.88f,0),Quaternion.identity);Apply();float top=draftRig.Bones[5].position.y+.28f+(seat.hat!="none"?.16f:0),bottom=draftRig.Bones[3].position.y-.13f,edge=Mathf.Clamp(trayBottom+.035f,.2f,.56f),height=Math.Max((top-bottom)/(1-edge),1.3f/Math.Max(.2f,camera.aspect)),centre=top+(edge-.5f)*height,dist=height*.5f/Mathf.Tan(12*Mathf.Deg2Rad);
            float pulse=age>=0&&!reduced?NativeDraftPlant.Pulse(age):-1,flash=pulse>=0?(1-pulse)*(1-pulse):0;
            camera.fieldOfView=24;camera.transform.position=new Vector3(0,centre+.22f,dist);camera.transform.LookAt(new Vector3(0,centre,0));NativeLighting.Draft(camera,tint,flash);
            var ray=camera.ViewportPointToRay(new Vector3(card.x,card.y,0));var plane=new Plane(Vector3.forward,new Vector3(0,0,.42f));plane.Raycast(ray,out float distance);Vector3 goal=ray.GetPoint(distance);bool left=goal.x<0;float target=reach?1:0;draftWeight=Mathf.MoveTowards(draftWeight,target,Time.unscaledDeltaTime*5);float weight=age>=0?Math.Max(draftWeight,NativeDraftRules.Reach(age)):draftWeight;
            draftPose.World();int hand=left?9:13,clav=left?6:10;var sourceTarget=new NVector(-goal.x,goal.y-.88f,goal.z);var shoulder=draftPose.WorldPosition[clav+1];var direction=NVector.Normalize(sourceTarget-shoulder);var point=shoulder+direction*(.495f+.017f*(age<0?0:NativeDraftRules.Reach(age)));point.X=left?Math.Max(.36f,point.X):Math.Min(-.36f,point.X);var rest=draftPose.WorldPosition[hand];var control=shoulder+new NVector(left?.06f:-.06f,-.12f,.34f);point=point*weight*weight+control*(2*weight*(1-weight))+rest*(1-weight)*(1-weight);
            // The reach (the tray's solve) up to the contact; from the pick the other wrist rises, and from the contact the chip
            // is carried there and planted (NativeDraftPlant). The Siren drafter keeps her idle and holds the chip.
            var local=NVector.Transform(point-draftPose.WorldPosition[3],NQuaternion.Inverse(draftPose.WorldRotation[3]));bool planting=age>=0&&!siren;
            if(weight>.001f&&!siren&&age<NativeDraftPlant.Contact)CapturedRig.Arm(draftPose,left,local,NativeDraftPlant.ContactWrist,new NVector(left?1:-1,-.8f,-.25f));
            if(planting)draftPlant.Pose(draftPose,left,local,age);
            var look=age<NativeDraftPlant.Contact?sourceTarget:planting?(draftPlant.ChipShown?draftPlant.Chip:draftPlant.At):point;draftPose.World();float eye=draftPose.WorldPosition[5].Y+.165f;look.Y=eye+(look.Y-eye)*(look.Y>eye?.55f:.5f);NativeDraftRules.Look(draftPose,look,1);Apply();
            if(planting)draftPlant.Place(draftPose,left,age);
            var root=draftRig.Root.transform.localToWorldMatrix;sourceModels.Queue(siren?"siren":"pilot",root,draftBone,body:BodyTint(seat.bodyColor),accent:AccentTint(seat.accentColor));if(!siren)sourceModels.Queue("hat-"+seat.hat,root,draftBone);
            // The chip at its real size in the hand (never bigger than the hand), carried to the other wrist and pressed in.
            var palm=draftRig.Bones[hand].TransformPoint(new Vector3(0,-.035f,.04f));draftChipTint=(chipTint.a>0?chipTint:tint).linear;
            if(age>=NativeDraftPlant.Contact&&(siren||draftPlant.ChipShown))
            {
                float scale=NativeDraftPlant.HeldChipScale(age,(float)time,reduced);var at=siren?palm:Stage(draftPlant.Chip);var turn=siren?draftRig.Bones[hand].rotation*Quaternion.Euler(90,0,0):CapturedAnimation.Quat(CapturedRig.Mirror(draftPlant.ChipRotation));
                if(scale>.001f){draftChip=Matrix4x4.TRS(at,turn,Vector3.one*scale);sourceModels.Queue("pve-pickup",draftChip,draftChipBone,partTint:draftChipPart);}
            }
            // The plant pulse: two rarity rings expanding around the forearm, a spark at the cuff and the rim light's flash.
            if(pulse>=0)
            {
                var at=siren?palm:Stage(draftPlant.At);var axis=Quaternion.FromToRotation(Vector3.forward,siren?Vector3.up:CapturedAnimation.Vec(CapturedRig.Mirror(draftPlant.Forearm)));var rare=draftChipTint;
                for(int ring=0;ring<2;ring++){float u=Mathf.Clamp01(pulse*1.35f-ring*.3f),fade=(1-u)*(1-u);if(u<=0||fade<.03f)continue;float r=.06f+.14f*NativeUiMotion.Bezier(.2f,.8f,.2f,1,u);sourceModels.QueuePiece(WeaponShape.Ring,true,Matrix4x4.TRS(at,axis,new Vector3(r,r,r*.6f)),rare*(1.6f*fade));}
                float s=.03f*(1-pulse);if(s>.002f)sourceModels.QueuePiece(WeaponShape.Cube,true,Matrix4x4.TRS(at,Quaternion.identity,Vector3.one*s),rare*2);
            }
            TeamBands(draftRig,DraftBandTeam);DrawBatches(null,0,false);sourceModels.Flush(UsesInstancing);return camera.WorldToScreenPoint(palm);
        }
        // A stage rig root-frame point (source axes) in Unity world space (the rig root stands at y .88, unrotated).
        private static Vector3 Stage(NVector p)=>new Vector3(-p.X,p.Y+.88f,p.Z);
    }
}

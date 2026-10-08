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
        private bool PvePickScreen=>!menu&&arena!=null&&PvePickOpen&&arena.Pve?.Holding!=true&&arena.Local.Hp>0&&!ReplayActive&&match.Phase==NativePhase.Fight;
        private bool DraftFrame()
        {
            bool pve=PvePickScreen,active=!menu&&arena!=null&&match.Phase==NativePhase.Draft;
            if(!active&&!pve){if(draftViewing){draftViewing=false;RestoreLive();presentation.ResetPresentation();}return false;}
            var seat=pve?arena.Local.Seat:match.Seat(match.DraftSeat);if(seat==null)return false;
            if(!draftViewing||draftViewSeat!=seat.id){RestoreLive();presentation.ResetPresentation();draftHover=-1;draftFocus=-1;draftViewSeat=seat.id;draftViewing=true;}
            float scale=NativeUiMotion.Measure(Screen.width,Screen.height,prefs.uiScale).Scale,width=Screen.width/scale,height=Screen.height/scale;
            var row=pve?arena.Pve.Rows[seat.id]:null;int count=pve?row.Open?.Offers?.Count??0:seat.offers.Count;float age=pve||match.Chosen==null?-1:(float)(match.Time-match.ChosenAt);
            // Look target: the chosen card, the local picker's hovered or focused card, else the boot scan then a 1.8 s cycle.
            float since=pve?Time.unscaledTime-pveOpenAt:(float)(match.Time-DraftStarted(seat));int scan=since<count*.22f?Mathf.FloorToInt(Math.Max(0,since)/.22f):(int)(since/1.8f)%Math.Max(1,count);
            int index=!pve&&match.Chosen!=null?seat.offers.IndexOf(match.Chosen):draftHover>=0?draftHover:scan;index=Mathf.Clamp(index,0,Math.Max(0,draftCards.Length-1));
            Rect card=draftCards.Length>0?draftCards[index]:new Rect(width/2,180,1,1);float bottom=Mathf.Min(draftTrayBottom,.49f);
            if(pve)HideLive();else arena.Root.SetActive(false);viewModelCamera.enabled=false;
            // Stage lights take the picker's colour, blending to the chip's rarity over .5 s after the pick (the Siren pick: its tier).
            Color tint=pve?(row.Open?.Tier=="rainbow"?Color.white:RarityColor(row.Open?.Tier??"common")):SeatColor(seat);if(age>=0)tint=Color.Lerp(tint,RarityColor(content.Chip(match.Chosen).rarity),Mathf.Clamp01(age/.5f));
            draftPalm=presentation.RenderDraft(seat,pve?arena.Time:match.Time,age,new Vector2(card.center.x/width,1-card.center.y/height),bottom,tint,cameraView,draftHover>=0||age>=0,prefs.reducedEffects)/scale;draftPalm.y=height-draftPalm.y;return true;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private CapturedAnimation.Player draftRig;private readonly CapturedRig.Pose draftPose=new CapturedRig.Pose();private float draftWeight;
        public Vector2 RenderDraft(NativeSeat seat,double time,float age,Vector2 card,float trayBottom,Color tint,Camera camera,bool reach,bool reduced)
        {
            if(draftRig==null){draftRig=captured.Create(transform,-101);draftWeight=0;}sourceModels.Begin();foreach(var b in batches)b.Clear();FirstPerson=false;combatGlow?.Clear();particles.Clear();
            NativeDraftRules.Pose(draftPose,reduced?0:(float)time);if(seat.spec=="siren")NativeSourceMotion.Sample("siren-idle-melee",(float)time,0,draftPose);
            void Apply(){for(int i=0;i<22;i++){draftRig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(draftPose.Position[i]));draftRig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(draftPose.Rotation[i]));}}
            draftRig.Root.transform.SetPositionAndRotation(new Vector3(0,.88f,0),Quaternion.identity);Apply();float top=draftRig.Bones[5].position.y+.28f+(seat.hat!="none"?.16f:0),bottom=draftRig.Bones[3].position.y-.13f,edge=Mathf.Clamp(trayBottom+.035f,.2f,.56f),height=Math.Max((top-bottom)/(1-edge),1.3f/Math.Max(.2f,camera.aspect)),centre=top+(edge-.5f)*height,dist=height*.5f/Mathf.Tan(12*Mathf.Deg2Rad);
            camera.fieldOfView=24;camera.transform.position=new Vector3(0,centre+.22f,dist);camera.transform.LookAt(new Vector3(0,centre,0));NativeLighting.Draft(camera,tint);
            var ray=camera.ViewportPointToRay(new Vector3(card.x,card.y,0));var plane=new Plane(Vector3.forward,new Vector3(0,0,.42f));plane.Raycast(ray,out float distance);Vector3 goal=ray.GetPoint(distance);bool left=goal.x<0;float target=reach?1:0;draftWeight=Mathf.MoveTowards(draftWeight,target,Time.unscaledDeltaTime*5);float weight=age>=0?Math.Max(draftWeight,NativeDraftRules.Reach(age)):draftWeight;
            draftPose.World();int hand=left?9:13,clav=left?6:10;float pull=age<0?0:NativeDraftRules.Pull(age);var sourceTarget=new NVector(-goal.x,goal.y-.88f,goal.z);var shoulder=draftPose.WorldPosition[clav+1];var direction=NVector.Normalize(sourceTarget-shoulder);var point=shoulder+direction*(.495f+.017f*(age<0?0:NativeDraftRules.Reach(age)));point.X=left?Math.Max(.36f,point.X):Math.Min(-.36f,point.X);var rest=draftPose.WorldPosition[hand];var control=shoulder+new NVector(left?.06f:-.06f,-.12f,.34f);point=point*weight*weight+control*(2*weight*(1-weight))+rest*(1-weight)*(1-weight);
            var hold=draftPose.WorldPosition[3]+NVector.Transform(new NVector(left?.15f:-.15f,.07f,.36f),draftPose.WorldRotation[3]);point=new NVector(point.X+(hold.X-point.X)*pull*pull,point.Y+(hold.Y-point.Y)*pull,point.Z+(hold.Z-point.Z)*pull);
            if(weight>.001f&&seat.spec!="siren"){var local=NVector.Transform(point-draftPose.WorldPosition[3],NQuaternion.Inverse(draftPose.WorldRotation[3]));var wrist=CapturedRig.Euler(-1.45f+.45f*pull,0,(left?-1:1)*.5f*pull);CapturedRig.Arm(draftPose,left,local,wrist,new NVector(left?1:-1,-.8f,-.25f));}
            var look=age>=.48f?point:sourceTarget;draftPose.World();float eye=draftPose.WorldPosition[5].Y+.165f;look.Y=eye+(look.Y-eye)*(look.Y>eye?.55f:.5f);NativeDraftRules.Look(draftPose,look,1);Apply();Matrix4x4 Bone(int b)=>draftRig.Bones[b].localToWorldMatrix;
            sourceModels.Queue(seat.spec=="siren"?"siren":"pilot",draftRig.Root.transform.localToWorldMatrix,Bone,body:BodyTint(seat.bodyColor),accent:AccentTint(seat.accentColor));if(seat.spec!="siren")sourceModels.Queue("hat-"+seat.hat,draftRig.Root.transform.localToWorldMatrix,Bone);
            var palm=draftRig.Bones[hand].TransformPoint(new Vector3(0,-.035f,.04f));if(age>=.48f){var m=Matrix4x4.TRS(palm,draftRig.Bones[hand].rotation*Quaternion.Euler(90,0,0),Vector3.one*(.5f+.5f*Mathf.Clamp01((age-.48f)/.12f)));sourceModels.Queue("pve-pickup",m,b=>m,partTint:b=>b==1?tint.linear:Color.white);}
            DrawBatches(null,0,false);sourceModels.Flush(UsesInstancing);return camera.WorldToScreenPoint(palm);
        }
    }
}

using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private int draftHover=-1,draftViewSeat;private bool draftViewing;private Rect[] draftCards=Array.Empty<Rect>();private Vector2 draftPalm;
        private bool DraftFrame()
        {
            bool active=!menu&&arena!=null&&match.Phase==NativePhase.Draft;
            if(!active){if(draftViewing){draftViewing=false;presentation.ResetPresentation();}return false;}
            var seat=match.Seat(match.DraftSeat);if(seat==null)return false;
            if(!draftViewing||draftViewSeat!=seat.id){presentation.ResetPresentation();draftHover=-1;draftViewSeat=seat.id;draftViewing=true;}
            float scale=Mathf.Max(.65f,Screen.height/900f),width=Screen.width/scale,height=Screen.height/scale;DraftLayout(width,height);
            int index=match.Chosen!=null?seat.offers.IndexOf(match.Chosen):draftHover>=0?draftHover:(int)(match.Time/1.8)%Math.Max(1,seat.offers.Count);index=Mathf.Clamp(index,0,Math.Max(0,draftCards.Length-1));
            Rect card=draftCards.Length>0?draftCards[index]:new Rect(width/2,180,1,1);float bottom=0;foreach(var r in draftCards)bottom=Math.Max(bottom,r.yMax);bottom=Mathf.Min(bottom,height*.49f);
            arena.Root.SetActive(false);viewModelCamera.enabled=false;float age=match.Chosen==null?-1:(float)(match.Time-match.ChosenAt);Color tint=match.Chosen==null?NativeArena.ColorOf(0x70e1d0):RarityColor(content.Chip(match.Chosen).rarity);
            draftPalm=presentation.RenderDraft(seat,match.Time,age,new Vector2((card.center.x-draftScroll.x)/width,1-card.center.y/height),bottom/height,tint,cameraView,draftHover>=0||age>=0,prefs.reducedEffects)/scale;draftPalm.y=height-draftPalm.y;return true;
        }
        private void DraftLayout(float width,float height)
        {
            int count=match.Seat(match.DraftSeat).offers.Count;draftCards=new Rect[count];int fit=width<=760?count:Mathf.Clamp(Mathf.FloorToInt((width-42)/164),2,6),columns=count<=fit?Math.Max(1,count):(count+1)/2;float w=Math.Max(150,Math.Min(260,(width-56-(columns-1)*14)/columns)),h=height<650?126:148;
            for(int i=0;i<count;i++){int row=i/columns,n=Math.Min(columns,count-row*columns);float left=Math.Max(28,(width-(n*w+(n-1)*14))/2);draftCards[i]=new Rect(left+i%columns*(w+14),118+row*(h+12),w,h);}
        }
        private void Draft(float width,float height)
        {
            var seat=match.Seat(match.DraftSeat);if(seat==null)return;DraftLayout(width,height);float age=match.Chosen==null?-1:(float)(match.Time-match.ChosenAt);
            GUI.Label(new Rect(28,63,width-56,45),seat.name+" chooses a Chip  ·  "+Math.Max(0,Math.Ceiling(match.PhaseEnd-match.Time))+"s",heading);
            float extent=width;foreach(var card in draftCards)extent=Math.Max(extent,card.xMax+28);draftScroll=GUI.BeginScrollView(new Rect(0,110,width,Math.Min(height*.4f,350)),draftScroll,new Rect(0,110,extent,Math.Min(height*.4f,350)),false,false);draftHover=-1;
            for(int i=0;i<draftCards.Length;i++)
            {
                var chip=content.Chip(seat.offers[i]);Rect rect=draftCards[i];bool selected=chip.id==match.Chosen;if(age>=0&&(!selected||age>=.48f))continue;if(rect.Contains(Event.current.mousePosition))draftHover=i;
                if(selected){float fly=NativeDraftRules.Fly(age);Vector2 centre=Vector2.Lerp(rect.center,draftPalm+new Vector2(draftScroll.x,0),fly);rect=new Rect(centre.x-rect.width*(1-fly*.82f)/2,centre.y-rect.height*(1-fly*.82f)/2,rect.width*(1-fly*.82f),rect.height*(1-fly*.82f));}
                GUI.color=RarityColor(chip.rarity);GUI.Box(rect,"");GUI.color=Color.white;if(selected&&age>.15f)continue;
                GUI.Label(new Rect(rect.x+9,rect.y+7,rect.width-18,40),chip.name,label);GUI.Label(new Rect(rect.x+9,rect.y+48,rect.width-18,rect.height-82),chip.description,small);
                GUI.enabled=!seat.bot&&seat.id==arena.Local.Seat.id&&match.Time>=match.ReadyAt&&match.Chosen==null;if(GUI.Button(new Rect(rect.x+9,rect.yMax-32,rect.width-18,27),"Install"))ChooseChip(seat.id,chip.id);GUI.enabled=true;
            }GUI.EndScrollView();
            if(age>=.85f){var c=content.Chip(match.Chosen);float w=Math.Min(380,width*.34f);GUI.Box(new Rect(width-w-28,height*.58f,w,height*.31f),"");GUI.color=RarityColor(c.rarity);GUI.Label(new Rect(width-w-12,height*.6f,w-32,60),c.name,heading);GUI.color=Color.white;GUI.Label(new Rect(width-w-12,height*.6f+62,w-32,height*.18f),c.description,label);GUI.Label(new Rect(28,height-48,width-56,40),"INSTALLED · "+seat.name,heading);}
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

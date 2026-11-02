using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    /// <summary>A party member's look on the Deployment Station's pads: the rooftop's pilot look (weapon spec, hat, colours, body
    /// skin, weapon skins), the lobby pose key (standing with weapon or arms crossed; the seated and sleeping poses stand) and
    /// the outfit (the visual body's look).</summary>
    public struct DeployPilot {public RooftopPilot Look;public string Pose,Outfit;}

    public sealed partial class ArenaPresentation
    {
        // The Deployment Station (Runtime/NativeDeployScene.cs; docs/EXPEDITION-DEPLOYMENT.md "Presentation"): the scene, built on
        // first use, and its cast: one pilot per occupied pad, posed by the lobby poses (NativeLobbyPoses: the hero stance or arms
        // crossed, each pad's breath offset), on the visual body like the rooftop's crowd (rigs DeployIdTop and down, the seat's
        // outfit and weapon skins) or the voxel body where the model does not draw; stage lit; lifted away at Deploy.
        private NativeDeployScene deployScene;private bool deployFailed;private DeployCast deployCast;private Transform deployHolder;
        public const int DeployIdTop=-500;
        /// <summary>Whether the Deployment Station can draw here (its shaders run, the model bank loaded, no failure this session).</summary>
        public bool DeploySupported=>!deployFailed&&sourceModels!=null&&NativeDeployScene.Supported;
        /// <summary>The station's stage this frame (null before its first draw): the console reads its looks and controls.</summary>
        public NativeDeployStage DeployStage=>deployScene?.Stage;
        private sealed class DeployCast
        {
            public const int Count=NativeDeployPresentation.Pads;
            public readonly NativeRooftopActors.Look[] Looks=new NativeRooftopActors.Look[Count];public readonly NativeRooftopActors.State[] States=new NativeRooftopActors.State[Count];
            public readonly Matrix4x4[][] Bones=new Matrix4x4[Count][];public readonly Func<int,Matrix4x4>[] Bone=new Func<int,Matrix4x4>[Count],Held=new Func<int,Matrix4x4>[Count],ModelHeld=new Func<int,Matrix4x4>[Count];
            public readonly Func<int,bool>[] HeldShown=new Func<int,bool>[Count],ModelHeldShown=new Func<int,bool>[Count];
            public readonly Color[] Body=new Color[Count],Accent=new Color[Count];public readonly int[] Pose=new int[Count],BodySkin=new int[Count];
            public readonly string[] Key=new string[Count],Weapon=new string[Count],Outfit=new string[Count],Skins=new string[Count],HatColor=new string[Count];
            public readonly CapturedAnimation.Player[] Rigs=new CapturedAnimation.Player[Count];public readonly Actor[] Actors=new Actor[Count];public readonly bool[] OwnWeapon=new bool[Count];
            public DeployCast()
            {
                for(int i=0;i<Count;i++)
                {
                    Looks[i]=new NativeRooftopActors.Look{Id="deploy-"+i,Script=-1,Spec="pistol",Hat="none",Body="#e5e7eb",Accent="#22e6ff"};
                    var s=States[i]=new NativeRooftopActors.State();var bones=Bones[i]=new Matrix4x4[CapturedRig.Bones.Length];int k=i;Actors[i]=new Actor();
                    Bone[i]=b=>bones[b];
                    Held[i]=b=>s.Weapon.Visible&&b==s.Weapon.Bone?bones[b]*RooftopCast.Offset(s.Weapon):s.Offhand.Visible&&b==s.Offhand.Bone?bones[b]*RooftopCast.Offset(s.Offhand):bones[b];
                    HeldShown[i]=b=>s.Weapon.Visible&&b==s.Weapon.Bone||s.Offhand.Visible&&b==s.Offhand.Bone;
                    ModelHeld[i]=b=>{var m=Rigs[k].Bones[b].localToWorldMatrix;return s.Weapon.Visible&&b==s.Weapon.Bone?m*RooftopCast.Offset(s.Weapon):s.Offhand.Visible&&b==s.Offhand.Bone?m*RooftopCast.Offset(s.Offhand):m;};
                    ModelHeldShown[i]=b=>s.Weapon.Visible&&b==s.Weapon.Bone&&!OwnWeapon[k]||s.Offhand.Visible&&b==s.Offhand.Bone;
                }
            }
        }
        // The deploy rigs' outfits and weapon skins (NativeVisualBodyPresentation VisualOutfitOf, NativeWeaponSkinPresentation
        // SeatWeaponSkins ask here first).
        private bool DeployOutfit(int id,out string look){look=null;int i=DeployIdTop-id;if(deployCast==null||i<0||i>=DeployCast.Count)return false;look=deployCast.Outfit[i]??"";return true;}
        private bool DeploySkins(int id,out string skins){skins=null;int i=DeployIdTop-id;if(deployCast==null||i<0||i>=DeployCast.Count)return false;skins=deployCast.Skins[i];return true;}

        /// <summary>
        /// Draws the Deployment Station for `camera` from `input` (the replicated state, this client's roll and deploy times, the
        /// room's readiness) with `party` on the pads (party[i] on pad i, the host first). `dt` eases the camera (negative cuts).
        /// False when it cannot draw here (the caller keeps its own backdrop); a failure is logged once and not retried.
        /// </summary>
        public bool RenderDeployStation(Camera camera,in DeployInput input,DeployPilot[] party,float dt,bool reduced)
        {
            if(!DeploySupported||camera==null)return false;
            try{deployScene??=new NativeDeployScene();deployScene.Draw(camera,input,dt);DeployCastFrame(camera,input.Time,party,reduced);return true;}
            catch(Exception e){Debug.LogException(e);deployFailed=true;deployScene?.Dispose();deployScene=null;return false;}
        }
        /// <summary>Leaves the station: its pilots' visual bodies hide and the scene is released (built again on the next visit).</summary>
        public void DeployLeave()
        {
            var cast=deployCast;if(cast!=null)foreach(var rig in cast.Rigs)if(rig!=null&&visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&body.Root.activeSelf){body.Root.SetActive(false);body.Seen=false;}
            deployScene?.Dispose();deployScene=null;
        }
        private void DeployCastFrame(Camera camera,float time,DeployPilot[] party,bool reduced)
        {
            var stage=deployScene.Stage;var cast=deployCast??=new DeployCast();sourceModels.Begin();
            for(int i=0;i<DeployCast.Count;i++)
            {
                if(i>=stage.Pilots||party==null||i>=party.Length||stage.PilotShown[i]<.5f)continue;
                var p=party[i];var look=cast.Looks[i];string spec=string.IsNullOrEmpty(p.Look.Spec)?"pistol":p.Look.Spec,hat=string.IsNullOrEmpty(p.Look.Hat)?"none":p.Look.Hat;
                if(look.Spec!=spec||look.Hat!=hat||look.Body!=p.Look.Body||look.Accent!=p.Look.Accent||cast.BodySkin[i]!=p.Look.BodySkin||cast.HatColor[i]!=p.Look.HatColor||cast.Key[i]!=p.Pose||cast.Weapon[i]==null)
                {
                    look.Spec=spec;look.Hat=hat;look.Body=p.Look.Body;look.Accent=p.Look.Accent;cast.BodySkin[i]=p.Look.BodySkin;cast.HatColor[i]=p.Look.HatColor;cast.Key[i]=p.Pose;cast.Weapon[i]="weapon-"+spec;
                    cast.Body[i]=BodyTint(p.Look.Body);cast.Accent[i]=BodyAccent(p.Look.BodySkin,p.Look.Accent,hat,p.Look.HatColor);
                    // the lobby pose: the chair and sleep need a set of their own, so they stand; idles the lobby samples elsewhere fold their arms
                    int pose=NativeLobbyPoses.Index(p.Pose);if(pose==NativeLobbyPoses.Chair||pose==NativeLobbyPoses.Sleep||pose==NativeLobbyPoses.SheldonIdle)pose=NativeLobbyPoses.Weapon;
                    if(NativeLobbyPoses.UsesIdle(spec,pose))pose=NativeLobbyPoses.Crossed;cast.Pose[i]=pose;
                }
                cast.Outfit[i]=p.Outfit;cast.Skins[i]=p.Look.Skins;
                var s=cast.States[i];s.Pose.Reset();s.Offset=NVector.Zero;s.Hidden=false;s.Rig.Reset();
                NativeLobbyPoses.Sample(s.Rig,spec,cast.Pose[i],reduced?0:time+i*1.37f,out s.Weapon,out s.Offhand);s.Rig.WritePose(s.Pose);s.Pose.World();
                var f=stage.PilotFeet[i];var face=stage.PilotFacing[i];
                var root=Matrix4x4.TRS(new Vector3(f.X,f.Y,f.Z),Quaternion.LookRotation(new Vector3(face.X,0,face.Z)),Vector3.one);var group=root*Matrix4x4.Translate(new Vector3(0,NativePortraitRules.GroupY,0));
                var bones=cast.Bones[i];var pp=s.Pose;for(int b=0;b<bones.Length;b++)bones[b]=group*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(pp.WorldPosition[b])),CapturedAnimation.Quat(CapturedRig.Mirror(pp.WorldRotation[b])),Vector3.one);
                bool siren=spec=="siren";string hatColor=p.Look.HatColor;
                if(siren||!DeployModel(cast,i,root,group,time,camera,reduced,hatColor))
                {
                    string body=siren?"siren":BodyModel(cast.BodySkin[i],NativeBodySkins.Tier(LowGraphics,reduced?.3f:1,Vector3.Distance(camera.transform.position,group.GetColumn(3)),false));
                    sourceModels.Queue(body,group,cast.Bone[i],null,0,camera,cast.Body[i],cast.Accent[i],look:siren?SirenStill(reduced?0:time):null);
                    if(!siren&&hat!="none"&&!AuditorHalo(hat,bones[CapturedRig.Head],time,1,reduced,0,camera,camera,hatColor)&&!SheldonStill(hat,hatColor,group,cast.Bone[i],bones[CapturedRig.Head],time,i,reduced,0,camera))sourceModels.Queue(HatModel(hat,hatColor),group,cast.Bone[i],null,0,camera,body:HatBody(hat,hatColor));
                    if(!siren&&(s.Weapon.Visible||s.Offhand.Visible))sourceModels.Queue(cast.Weapon[i],group,cast.Held[i],cast.HeldShown[i],0,camera,accent:cast.Accent[i],glow:HeldGlow);
                }
            }
            sourceModels.Stage=true;try{sourceModels.Flush(UsesInstancing);}finally{sourceModels.Stage=false;}
            VisualBodiesShown();
        }
        // A pad's pilot on the visual body (RooftopModel's path: its own rig, the pose written onto it, the body bound in the
        // seat's outfit, its hat, gear and held weapon); false where the model does not draw (Classic, no bake, a viewer).
        private bool DeployModel(DeployCast cast,int i,Matrix4x4 root,Matrix4x4 group,float time,Camera camera,bool reduced,string hatColor)
        {
            if(Viewer||ClassicBodies||!VisualBodiesOn)return false;
            var look=cast.Looks[i];var s=cast.States[i];int id=DeployIdTop-i;
            if(deployHolder==null){var go=new GameObject("Deployment Station pilots");go.transform.SetParent(transform,false);deployHolder=go.transform;}
            var rig=cast.Rigs[i]??=captured.Create(deployHolder,id);
            rig.Root.transform.SetPositionAndRotation(group.GetColumn(3),root.rotation);
            if(!NativePoseWriter.Write(rig.Bones,s.Pose,"a deployment station pilot"))return false;
            var a=cast.Actors[i];a.Id=id;a.SourceSpec=look.Spec;a.Hat=look.Hat;a.HatColor=hatColor;a.BodyColor=look.Body;a.AccentColor=look.Accent;a.WeaponAway=!s.Weapon.Visible;a.Grounded=true;VisualArmedAs(a,look.Spec);
            var body=VisualBodyOf(rig,id,VisualHooded(a));if(body==null)return false;
            body.Root.transform.localPosition=new Vector3(0,NativeVisualRig.Floor,0);
            var held=VisualBodyBind(body,rig,a,time,s.Pose,false,false,VisualArms.Still);
            var right=rig.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
            body.Moved=true;body.Seen=true;
            VisualHatHair(body,look.Hat);
            if(look.Hat!="none"&&look.Hat!=NativeSheldonHead.Hat)
            {
                var hb=VisualHatBone(rig,look.Hat);
                if(!AuditorHalo(look.Hat,hb(CapturedRig.Head),time,1,reduced,0,camera,camera,hatColor))sourceModels.Queue(VisualHatModel(look.Hat,hatColor),rig.Root.transform.localToWorldMatrix,hb,null,0,camera,body:HatBody(look.Hat,hatColor));
            }
            else SheldonHoodStill(body,look.Hat,hatColor,rig.Root.transform.localToWorldMatrix,VisualHatBone(rig,look.Hat),time,i,reduced,0,camera);
            VisualGear(body,a,time,null,null,1,false);
            if(s.Weapon.Visible||s.Offhand.Visible)
            {
                cast.OwnWeapon[i]=s.Weapon.Visible&&VisualWeaponDrawn(rig,rig.Bones[s.Weapon.Bone].localToWorldMatrix*RooftopCast.Offset(s.Weapon));
                sourceModels.Queue(cast.Weapon[i],rig.Root.transform.localToWorldMatrix,cast.ModelHeld[i],cast.ModelHeldShown[i],0,camera,accent:cast.Accent[i],glow:HeldGlow);
            }
            return true;
        }
    }
}

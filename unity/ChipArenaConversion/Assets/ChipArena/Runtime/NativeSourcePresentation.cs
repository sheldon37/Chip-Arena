using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private NativeSourceModels sourceModels;private readonly Dictionary<int,CapturedAnimation.Player> minionRigs=new Dictionary<int,CapturedAnimation.Player>();private readonly CapturedRig.Pose minionPose=new CapturedRig.Pose();
        private static Color BodyTint(string text){if(!ColorUtility.TryParseHtmlString(text??"#e5e7eb",out var color))return Color.white;Color source=new Color(229/255f,231/255f,235/255f).linear,target=color.linear;return new Color(target.r/source.r,target.g/source.g,target.b/source.b);}
        private static Color AccentTint(string text){if(!ColorUtility.TryParseHtmlString(text??"#e5e7eb",out var color))return Color.white;Color source=new Color(112/255f,225/255f,208/255f).linear,target=color.linear;return new Color(target.r/source.r,target.g/source.g,target.b/source.b);}
        private bool SourceCharacter(CapturedAnimation.Player rig,Actor a,float time,bool first=false)
        {
            if(sourceModels==null||string.IsNullOrEmpty(a.SourceSpec))return false;int layer=first?10:0;Camera camera=first?ViewCamera:null;
            Matrix4x4 Bone(int b)=>rig.Bones[b].localToWorldMatrix;bool Visible(int b)=>!first||NativeViewRules.ViewModelBone(b);
            sourceModels.Queue(a.SourceSpec=="siren"?"siren":"pilot",rig.Root.transform.localToWorldMatrix,Bone,Visible,layer,camera,BodyTint(a.BodyColor),AccentTint(a.AccentColor));
            if(!first&&!string.IsNullOrEmpty(a.Hat)&&a.SourceSpec!="siren")sourceModels.Queue("hat-"+a.Hat,rig.Root.transform.localToWorldMatrix,Bone,null,layer,camera);
            if(a.SourceSpec=="siren"||a.WeaponAway)return true;
            Matrix4x4 WeaponBone(int b)
            {if(a.MagicVisual>0||a.SourceSpec=="leet")return StaffMatrix(rig,first);if(a.Weapon==WeaponId.Sword&&a.SourceSpec!="emperor"&&a.SourceSpec!="ansatsuken")return MeleeHand(rig,a,time,b==CapturedRig.LHand);return Bone(b);}
            sourceModels.Queue("weapon-"+a.SourceSpec,rig.Root.transform.localToWorldMatrix,WeaponBone,null,layer,camera);return true;
        }
        private void SourceWorld(NativeArena arena)
        {
            if(sourceModels==null)return;sourceModels.Queue("map-"+arena.MapId,Matrix4x4.identity);if(arena.MapId=="siren")WardPresentation(arena);
            if(arena.Pve!=null)foreach(var m in arena.Pve.Content.machines)sourceModels.Queue("machine-"+m.kind,Matrix4x4.TRS(new Vector3(m.x,0,m.z),Quaternion.Euler(0,m.yaw*Mathf.Rad2Deg,0),Vector3.one));
            foreach(int id in minionRigs.Keys.Where(id=>!arena.Minions.Any(m=>m.Id==id)).ToArray()){minionRigs[id].Dispose();minionRigs.Remove(id);}
            foreach(var m in arena.Minions)
            {
                if(m.Hp<=0)continue;if(!minionRigs.TryGetValue(m.Id,out var rig)){rig=captured.Create(transform,m.Id);minionRigs.Add(m.Id,rig);}Vector3 pos=m.Body.transform.position;int variant=m.Id%6;string kind="idle";float age=arena.Time,duration=0;
                var add=m.Pve;
                if(add?.Travelling==true){if(arena.Time-add.DiveAt>=.9f)continue;kind="dive";age=arena.Time-add.DiveAt;duration=.9f;}
                else if(add!=null&&arena.Time<add.ActiveAt){if(arena.Time<add.SpawnAt)continue;kind=add.Entry.kind;var timeline=NativeEntryRules.Timeline(kind,true);duration=timeline.duration;age=duration-(add.ActiveAt-arena.Time);if(kind=="vent")pos=NativeArena.Vec(add.Entry.@exit.Position(.9f));}
                else if(arena.Time-m.LastAttack>=0&&arena.Time-m.LastAttack<.9f){kind=add?.Crawl==true?"lunge":add?.Stats.attack??(m.Kind=="toy"?"club":"swipe");age=arena.Time-m.LastAttack;duration=.9f;}
                else if(add?.Crawl==true)kind="crawl";else if(new Vector2((arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).x,(arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).z).magnitude>.2f)kind=(arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).magnitude>2.5f?"run":"walk";
                NativeSourceMotion.Sample("horde-"+variant+"-"+kind,age,duration,minionPose);
                for(int i=0;i<22;i++){rig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(minionPose.Position[i]));rig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(minionPose.Rotation[i]));}
                rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,m.Yaw,0));rig.Root.transform.localScale=Vector3.one*(m.Kind=="slender"?1.45f:m.Kind=="toy"?.65f:1);
                int lost=add?.Lost??0;bool Visible(int b){int[] bits={1,2,4,8,16,32,64,128,256},bones={5,7,11,8,12,14,18,15,19};for(int i=0;i<bits.Length;i++)if((lost&bits[i])!=0&&BoneWithin(b,bones[i]))return false;return true;}
                sourceModels.Queue(m.Kind=="toy"?"pilot":"horde-"+variant,rig.Root.transform.localToWorldMatrix,b=>rig.Bones[b].localToWorldMatrix,Visible,body:m.Kind=="slender"?new Color(.07f,.07f,.1f):(Color?)null);
            }
        }
        private void ClearSourceActors(){ClearCorpses();if(pitMaterial!=null){CapturedAnimation.Release(pitMaterial);pitMaterial=null;}introRig?.Dispose();introRig=null;foreach(var rig in minionRigs.Values)rig.Dispose();minionRigs.Clear();sourceModels?.Begin();}
        public void FinishSourceFrame()=>sourceModels?.Flush(UsesInstancing);
    }
}

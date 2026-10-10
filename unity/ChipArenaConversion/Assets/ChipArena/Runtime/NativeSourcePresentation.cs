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
        // Accent trims and armor strips carry ember('#70e1d0'); the pilot's ember(accent) replaces it (NativePortraitRules.GlowAccentTint).
        private static Color AccentTint(string text){var tint=NativePortraitRules.GlowAccentTint(text);return new Color(tint.X,tint.Y,tint.Z);}
        private bool SourceCharacter(CapturedAnimation.Player rig,Actor a,float time,bool first=false,bool weapon=true)
        {
            if(sourceModels==null||string.IsNullOrEmpty(a.SourceSpec))return false;int layer=first?10:0;Camera camera=first?ViewCamera:null;
            Matrix4x4 Bone(int b)=>rig.Bones[b].localToWorldMatrix;bool Visible(int b)=>!first||NativeViewRules.ViewModelBone(b);
            // The first-person Siren shows inked pilot arms (first-person.ts); a Last Laugh victim chars as it burns (C12).
            string model=a.SourceSpec=="siren"?first?sirenInk??(sirenInk=sourceModels.Ink("pilot")):"siren":sourceModels.WoundedPilot(a.Id,a.Wounds,a.BodyColor,ForceCompatibilityRendering);
            // The world Siren animates its look (claws, palm orbs, halos, eyes, grin, tongues, wisps: NativeSirenLookPresentation.cs).
            var look=a.SourceSpec=="siren"&&!first?SirenLook(rig,a,time):null;
            sourceModels.Queue(model,rig.Root.transform.localToWorldMatrix,Bone,Visible,layer,camera,BodyTint(a.BodyColor),AccentTint(a.AccentColor),partTint:first?null:SirenCharTint(a.Id),look:look);
            if(look!=null)SirenLookFx(rig,look,layer,camera,time);
            if(!first)LivingWounds(rig,a,time);
            if(!first&&!string.IsNullOrEmpty(a.Hat)&&a.SourceSpec!="siren")sourceModels.Queue("hat-"+a.Hat,rig.Root.transform.localToWorldMatrix,Bone,null,layer,camera);
            if(a.SourceSpec=="siren"||a.WeaponAway||!weapon)return true;
            float reach=WeaponReach(rig,a,time);Matrix4x4 WeaponBone(int b)=>SourceWeaponBone(rig,a,time,first,b,reach);
            sourceModels.Queue("weapon-"+a.SourceSpec,rig.Root.transform.localToWorldMatrix,WeaponBone,null,layer,camera);WeaponDressing(rig,a,time,first,layer,camera,reach);return true;
        }
        private void SourceWorld(NativeArena arena)
        {
            if(sourceModels==null)return;sourceModels.Queue("map-"+arena.MapId,Matrix4x4.identity);HazardPresentation(arena);SceneryMotion(arena);if(arena.MapId=="siren")WardPresentation(arena);
            PveMachinePresentation(arena);
            foreach(int id in minionRigs.Keys.Where(id=>!arena.Minions.Any(m=>m.Id==id)&&!arena.MinionDeaths.Any(d=>d.id==id&&arena.Time-d.at<2.3f)).ToArray()){minionRigs[id].Dispose();minionRigs.Remove(id);}
            foreach(var m in arena.Minions)
            {
                if(m.Hp<=0)continue;if(m.Kind=="slender"){SlenderModel(arena,m);continue;}if(!minionRigs.TryGetValue(m.Id,out var rig)){rig=captured.Create(transform,m.Id);minionRigs.Add(m.Id,rig);}Vector3 pos=m.Body.transform.position;int variant=m.Id%6;string kind="idle";float age=arena.Time,duration=0;
                var add=m.Pve;
                if(add?.Travelling==true){if(arena.Time-add.DiveAt>=.9f)continue;kind="dive";age=arena.Time-add.DiveAt;duration=.9f;}
                else if(add!=null&&arena.Time<add.ActiveAt){if(arena.Time<add.SpawnAt)continue;kind=add.Entry.kind;var timeline=NativeEntryRules.Timeline(kind,true);duration=timeline.duration;age=duration-(add.ActiveAt-arena.Time);if(kind=="vent")pos=NativeArena.Vec(add.Entry.@exit.Position(.9f));}
                else if(arena.Time-m.LastAttack>=0&&arena.Time-m.LastAttack<.9f){kind=add?.Crawl==true?"lunge":add?.Stats.attack??(m.Kind=="toy"?"club":"swipe");age=arena.Time-m.LastAttack;duration=.9f;}
                else if(add?.Crawl==true)kind="crawl";else if(new Vector2((arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).x,(arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).z).magnitude>.2f)kind=(arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).magnitude>2.5f?"run":"walk";
                if(m.Pve==null)NativeSummonPose.Sample(minionPose,m.Kind=="toy",arena.Time,m.Speed,arena.Time-m.LastAttack,arena.Time-m.LastHitAt,m.Id.ToString().Length);else NativeSourceMotion.Sample("horde-"+variant+"-"+kind,age,duration,minionPose);
                for(int i=0;i<22;i++){rig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(minionPose.Position[i]));rig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(minionPose.Rotation[i]));}
                rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,m.Yaw,0));rig.Root.transform.localScale=Vector3.one*(m.Pve!=null?1:m.Kind=="toy"?.6f:.85f);
                int lost=add?.Lost??0;bool Visible(int b){int[] bits={1,2,4,8,16,32,64,128,256},bones={5,7,11,8,12,14,18,15,19};for(int i=0;i<bits.Length;i++)if((lost&bits[i])!=0&&BoneWithin(b,bones[i]))return false;return true;}
                sourceModels.Queue(m.Pve==null?"summon-"+m.Kind:"horde-"+variant,rig.Root.transform.localToWorldMatrix,b=>rig.Bones[b].localToWorldMatrix,Visible);
            }
            // C17: severed limbs fly off as chunks (the rigs above are posed), and the downed get revive beacons.
            PveGore(arena);PveBeacons(arena);
        }
        private void ClearSourceActors(){ClearWardZero();ClearSirenLooks();ClearDevEffects();draftRig?.Dispose();draftRig=null;ClearBroadcasts();slenderHops.Clear();ClearSceneryMotion();ClearWoundSpray();sourceModels?.ClearWounds();ClearCorpses();if(pitMaterial!=null){CapturedAnimation.Release(pitMaterial);pitMaterial=null;}introRig?.Dispose();introRig=null;foreach(var rig in minionRigs.Values)rig.Dispose();minionRigs.Clear();sourceModels?.Begin();}
        public void FinishSourceFrame()=>sourceModels?.Flush(UsesInstancing);
    }
}

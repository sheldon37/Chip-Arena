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
        // `halo`: the clock the Auditor halo animates on (NaN: `time`; the intro rig passes its entrance age, as player-intros.ts does).
        // `reveal`: per-bone opacity (dithered) for a directed entrance that builds or projects the pilot in (null: solid).
        private bool SourceCharacter(CapturedAnimation.Player rig,Actor a,float time,bool first=false,bool weapon=true,float halo=float.NaN,Func<int,float> reveal=null)
        {
            if(sourceModels==null||string.IsNullOrEmpty(a.SourceSpec))return false;int layer=first?10:0;Camera camera=first?ViewCamera:null;
            Matrix4x4 Bone(int b)=>rig.Bones[b].localToWorldMatrix;bool Visible(int b)=>!first||NativeViewRules.ViewModelBone(b);
            // The first-person Siren shows inked pilot arms (first-person.ts); a Last Laugh victim chars as it burns (C12).
            // Every other pilot wears its body skin at this rig's tier (NativeBodySkinPresentation.cs); the Siren keeps its own.
            int bodySkin=NativeBodySkins.StandardIndex;string model=a.SourceSpec=="siren"?first?sirenInk??(sirenInk=sourceModels.Ink("pilot")):"siren":sourceModels.WoundedPilot(a.Id,a.Wounds,a.BodyColor,ForceCompatibilityRendering,RigBody(rig,a,first,out bodySkin));
            // The world Siren animates its look (claws, palm orbs, halos, eyes, grin, tongues, wisps: NativeSirenLookPresentation.cs).
            var look=a.SourceSpec=="siren"&&!first?SirenLook(rig,a,time):null;
            // Arena and first-person rigs wear their Chips and block state (NativeBodyChipPresentation.cs); entrance and viewer rigs stay bare.
            bool dressed=!Viewer&&rig!=introRig;Func<int,Color> tint;if(dressed)tint=RigTint(a,time,first);else{ClearRigTint();tint=first?null:SirenCharTint(a.Id);}
            // The hit flash (NativeHitFlash, weapons milestone) on a struck arena rig's body and hat; not the first-person arms.
            float flash=dressed&&!first?HitFlashCode(a.Id,time):1;
            sourceModels.Queue(model,rig.Root.transform.localToWorldMatrix,Bone,Visible,layer,camera,BodyTint(a.BodyColor),BodyAccent(bodySkin,a.AccentColor,a.Hat,a.HatColor),partOpacity:reveal,partTint:tint,look:look,hitFlash:flash);
            if(look!=null)SirenLookFx(rig,look,layer,camera,time);
            if(!first)LivingWounds(rig,a,time);
            // The hat rides the head (hidden on the first-person view model); the Auditor halo animates (NativeAuditorHaloPresentation.cs),
            // the Sheldon Head's face and ring too (NativeHatPresentation.cs); a picked hat colour tints the hat (NativeHatLooks).
            bool headShown=reveal==null||reveal(CapturedRig.Head)>.5f;
            if(headShown)if(!first&&!string.IsNullOrEmpty(a.Hat)&&a.SourceSpec!="siren"&&!AuditorHalo(a.Hat,rig.Bones[CapturedRig.Head].localToWorldMatrix,float.IsNaN(halo)?time:halo,1,HaloReduced,layer,camera,color:a.HatColor)&&!SheldonHead(a,rig.Root.transform.localToWorldMatrix,Bone,float.IsNaN(halo)?time:halo,layer,camera,dressed?tint:null,flash))sourceModels.Queue(HatModel(a.Hat,a.HatColor),rig.Root.transform.localToWorldMatrix,Bone,null,layer,camera,body:HatBody(a.Hat,a.HatColor),partTint:dressed?tint:null,hitFlash:flash);
            if(dressed)BodyDressing(rig,a,time,first,layer,camera);
            bool armed=NativeWeaponDressing.SocketShown(a.SourceSpec)&&!a.WeaponAway&&weapon&&!NativeSirenVictimPose.Active(a,time);float reach=armed?WeaponReach(rig,a,time):1;Matrix4x4 WeaponBone(int b)=>SourceWeaponBone(rig,a,time,first,b,reach);
            // The bow draws its live parts (Runtime/NativeBowPresentation.cs: the flexed limbs, the string to the nock, the arrows).
            if(armed&&BowHeld(rig,a,time,first,layer,camera,tint,flash))armed=false;
            if(armed){string held=HeldWeaponModel(rig,a,first);sourceModels.Queue(held,rig.Root.transform.localToWorldMatrix,WeaponBone,b=>!a.MainWeaponAway||a.SourceSpec!="daggers"||b!=CapturedRig.RHand,layer,camera,accent:AccentTint(a.AccentColor),hitFlash:StandardModel(held)?flash:1,partTint:tint,glow:HeldGlow);WeaponDressing(rig,a,time,first,layer,camera,reach);}
            if(dressed&&rigFlash>0)ParryFlash(rig,a,layer,camera,Bone,Visible,armed?WeaponBone:(Func<int,Matrix4x4>)null,first);
            ClearRigTint();return true;
        }
        private void SourceWorld(NativeArena arena)
        {
            if(sourceModels==null)return;sourceModels.Queue("map-"+arena.MapId,Matrix4x4.identity);HazardPresentation(arena);SceneryMotion(arena);if(arena.MapId=="siren")WardPresentation(arena);
            BrickfallScenery(arena);
            PveMachinePresentation(arena);
            // A dead add's rig stays posed until its death effects end (NativeMinionFxPresentation.cs: flames rise off its joints).
            foreach(int id in minionRigs.Keys.Where(id=>!arena.Minions.Any(m=>m.Id==id)&&!arena.MinionDeaths.Any(d=>d.id==id&&arena.Time-d.at<NativeMinionFx.DeathFxSeconds)).ToArray()){minionRigs[id].Dispose();minionRigs.Remove(id);}
            BeginHordeFx(arena);
            foreach(var m in arena.Minions)
            {
                if(m.Hp<=0)continue;if(m.Kind=="slender"){SlenderModel(arena,m);continue;}if(!minionRigs.TryGetValue(m.Id,out var rig)){rig=captured.Create(transform,m.Id);minionRigs.Add(m.Id,rig);}Vector3 pos=m.Body.transform.position;int variant=m.Id%6;string kind="idle";float age=arena.Time,duration=0;
                var add=m.Pve;
                if(add?.Travelling==true){if(arena.Time-add.DiveAt>=.9f)continue;kind="dive";age=arena.Time-add.DiveAt;duration=.9f;}
                else if(add!=null&&arena.Time<add.ActiveAt){if(arena.Time<add.SpawnAt)continue;kind=add.Entry.kind;var timeline=NativeEntryRules.Timeline(kind,true);duration=timeline.duration;age=duration-(add.ActiveAt-arena.Time);if(kind=="vent")pos=NativeArena.Vec(add.Entry.@exit.Position(.9f));}
                else if(arena.Time-m.LastAttack>=0&&arena.Time-m.LastAttack<.9f){kind=add?.Crawl==true?"lunge":add?.Stats.attack??(m.Kind=="toy"?"club":"swipe");age=arena.Time-m.LastAttack;duration=.9f;}
                else if(add?.Crawl==true)kind="crawl";else if(new Vector2((arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).x,(arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).z).magnitude>.2f)kind=(arena.Replica?m.NetworkVelocity:m.Body.linearVelocity).magnitude>2.5f?"run":"walk";
                if(m.Pve==null)NativeSummonPose.Sample(minionPose,m.Kind=="toy",arena.Time,m.Speed,arena.Time-m.LastAttack,arena.Time-m.LastHitAt,m.Id.ToString().Length);else NativeSourceMotion.Sample("horde-"+variant+"-"+kind,age,duration,minionPose);
                NativePoseWriter.Write(rig.Bones,minionPose,"a horde rig");
                rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,m.Yaw,0));rig.Root.transform.localScale=Vector3.one*(m.Pve!=null?1:m.Kind=="toy"?.6f:.85f);
                int lost=add?.Lost??0;bool Visible(int b){int[] bits={1,2,4,8,16,32,64,128,256},bones={5,7,11,8,12,14,18,15,19};for(int i=0;i<bits.Length;i++)if((lost&bits[i])!=0&&BoneWithin(b,bones[i]))return false;return true;}
                sourceModels.Queue(m.Pve==null?"summon-"+m.Kind:"horde-"+variant,rig.Root.transform.localToWorldMatrix,b=>rig.Bones[b].localToWorldMatrix,Visible,glow:add!=null?HordeGlow(m.Id):(Vector4?)null);
                // Siren minion effects (NativeMinionFxPresentation.cs): tongues and their licks, the crack haze, the possession hint
                // by its replicated start (grin and trail from level 2) and a vent climb's emergence pool (on the vent's floor; the
                // source counts its age to ActiveAt = 1.95 s).
                if(add!=null){bool vent=add.Entry.kind=="vent";HordeAdd(rig,m.Id,add.Travelling?float.NaN:(float)NativeMinionFx.VentAge(vent,add.ActiveAt,hordeClock),float.NaN,vent?NativeArena.Vec(add.Entry.@exit.Position(.9f)).y-.9f:pos.y-.85f,pos.y-(add.Crawl?.44f:.85f),add.Possessed?add.PossessedAt:float.NaN,arena.Replica?m.NetworkVelocity:m.Body.linearVelocity,lost,true);}
            }
            // C17: severed limbs fly off as chunks (the rigs above are posed), and the downed get revive beacons.
            PveGore(arena);PveBeacons(arena);
        }
        private void ClearSourceActors(){ClearRevives();ClearRigDressing();ClearWardZero();ClearSirenLooks();ClearDevEffects();draftRig?.Dispose();draftRig=null;ClearBroadcasts();slenderHops.Clear();ClearSceneryMotion();ClearWoundSpray();sourceModels?.ClearWounds();ClearCorpses();if(pitMaterial!=null){CapturedAnimation.Release(pitMaterial);pitMaterial=null;}introRig?.Dispose();introRig=null;foreach(var rig in minionRigs.Values)rig.Dispose();minionRigs.Clear();sourceModels?.Begin();}
        public void FinishSourceFrame()=>sourceModels?.Flush(UsesInstancing);
    }
}

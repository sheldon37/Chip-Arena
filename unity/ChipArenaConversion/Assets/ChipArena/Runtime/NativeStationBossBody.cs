using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NQuaternion=System.Numerics.Quaternion;

namespace ChipArena.UnityConversion
{
    // THE 6:47 as a real model (owner 2026-11-04, on the expedition enemies: "They still use voxel models"; docs/EXPEDITION-ENEMY-REWORK.md
    // section 2: the machines rebuilt in Blender on today's joints, the riders on the player's model). Presentation only: Core
    // StationBossRig's pose, the record and the effects are as they were, and nothing here is read back; the colliders stay
    // NativeZoneBossAuthority's, the moves Core NativeStationBoss's.
    //  - The machine: the bank Resources/ChipArenaPackBoss647 (tools/gear/station_boss.py), one mesh per joint and tier in the joint's own
    //    frame, drawn with the model's toon materials so it sits with the pilots and the zombies (each joint's light from the effects
    //    on its glowing sub-mesh, the struck flash, the station's practical lights). Built a few joints a frame while the map is up, so
    //    the arrival never waits for it.
    //  - The Stationmaster: the player's model in his frock coat (Core NativeZombieLooks.Stationmaster), its bones turned straight from
    //    the rig's joints (his chest, head and arms), waist deep in the cab roof, his legs folded away. His hat, watch and bell are
    //    the bank's, at his own hands and head; they fall off him through the rig's own loose pose.
    //  - Without the bank or the bake (CI, other checkouts, Settings · Player model Classic) the concept's sculpt draws as before.
    public sealed partial class ArenaPresentation
    {
        public const string StationBossBodyBank="ChipArenaPackBoss647";
        private struct StationBossPart{public Mesh Mesh;public int Joint;public bool Glows,Small;}
        private readonly List<StationBossPart>[] stationBossParts={new List<StationBossPart>(),new List<StationBossPart>(),new List<StationBossPart>()};
        private int stationBossPartsStep;private bool stationBossPartsAbsent;private MaterialPropertyBlock stationBossBodyBlock;
        /// <summary>Joints built a frame while the station map is up.</summary>
        private const int StationBossPartsPerFrame=6;
        private static int StationBossPartsTotal=>NativeStationBossLook.Count*3;
        private bool StationBossBodyDrawn=>!stationBossPartsAbsent&&ZombieModelsOn;
        // StationBossScenery, every frame on the station's map: the bank's meshes built a few at a time (all at once if the boss is up).
        private void StationBossPartsBuild(bool now)
        {
            if(stationBossPartsAbsent||stationBossPartsStep>=StationBossPartsTotal)return;
            var bank=NativePackBanks.Bank(StationBossBodyBank);if(bank==null||ZombieGearMaterials()==null){stationBossPartsAbsent=true;return;}
            int end=now?StationBossPartsTotal:Mathf.Min(StationBossPartsTotal,stationBossPartsStep+StationBossPartsPerFrame);
            for(;stationBossPartsStep<end;stationBossPartsStep++)
            {
                int joint=stationBossPartsStep%NativeStationBossLook.Count,tier=stationBossPartsStep/NativeStationBossLook.Count;var prop=bank.Find("647-"+NativeStationBossLook.Names[joint]+"-"+tier);if(prop==null||prop.Parts.Length==0)continue;
                try
                {
                    var built=ZombieGearBuild(bank,prop);if(built?.Mesh==null)continue;
                    bool small=joint>=NativeStationBossModel.ChainFirst&&joint!=NativeStationBossLook.BreachL&&joint!=NativeStationBossLook.BreachR||NativeStationBossLook.GlowJoint(joint);
                    stationBossParts[tier].Add(new StationBossPart{Mesh=built.Mesh,Joint=joint,Glows=built.Mesh.subMeshCount>1,Small=small});
                }
                catch(Exception e){Debug.LogWarning("THE 6:47 "+prop.Name+": "+e.Message);}
            }
            if(stationBossPartsStep>=StationBossPartsTotal&&stationBossParts[0].Count==0)stationBossPartsAbsent=true;
        }
        /// <summary>DrawStationBoss: the machine and the Stationmaster on this frame's pose; false leaves it to the concept's sculpt.</summary>
        private bool StationBossBodyDraw(BossPose pose,NativeStationBossFx fx,int tier,float flash)
        {
            if(!StationBossBodyDrawn)return false;StationBossPartsBuild(true);if(stationBossPartsAbsent)return false;
            var mats=ZombieGearMaterials();var block=stationBossBodyBlock??=new MaterialPropertyBlock();var parts=stationBossParts[Mathf.Clamp(tier,0,2)];
            StationBossRiderPose(pose,flash,fx.Hidden[NativeStationBossModel.SmHead]?0:fx.Glow[NativeStationBossModel.SmHead]);
            for(int i=0;i<parts.Count;i++)
            {
                var part=parts[i];int j=part.Joint;if(fx.Hidden[j])continue;
                var at=NativeBossPoseApply.ToUnity(pose.Position[j]);at+=StationBossRiderShift(pose,j);
                var m=Matrix4x4.TRS(at,NativeBossPoseApply.ToUnity(pose.Rotation[j]),Vector3.one);
                block.Clear();block.SetFloat(HitFlashId,flash);block.SetFloat(ActorLitId,1);
                var shadow=part.Small||tier==2?ShadowCastingMode.Off:ShadowCastingMode.On;
                Graphics.DrawMesh(part.Mesh,m,mats[0],0,null,0,block,shadow,true);StationBossDraws++;
                if(part.Glows){block.SetFloat(VertexAlbedoId,ZombieGearGlow*Mathf.Max(0,fx.Glow[j]));Graphics.DrawMesh(part.Mesh,m,mats[1],0,null,1,block,ShadowCastingMode.Off,true);StationBossDraws++;}
            }
            return true;
        }

        // ================================================================================================================
        // The Stationmaster on the player's model
        // ================================================================================================================
        private ZombieBody stationBossRider;private bool stationBossRiderTried;private Vector3 stationBossRiderHandL,stationBossRiderHandR,stationBossRiderHead;private bool stationBossRiderShown;
        private struct RiderBone{public Transform Bone;public Quaternion Rest;public int Joint,Toward;public float Share;}
        private RiderBone[] stationBossRiderBones;
        /// <summary>His size over the player's (the concept's: swollen, fused into the cab), and the pelvis's drop under the hips joint.</summary>
        public const float StationBossRiderScale=1.8f,StationBossRiderSink=.1f;
        // A prop on his own head or hands, not where the rig's proportions would have them (a loose hat falls on the rig's pose).
        private Vector3 StationBossRiderShift(BossPose pose,int j)
        {
            if(!stationBossRiderShown)return Vector3.zero;
            if(j==NativeStationBossModel.SmRHand||j==NativeStationBossLook.WatchAt47||j==NativeStationBossLook.WatchAt48)return stationBossRiderHandR-NativeBossPoseApply.ToUnity(pose.Position[NativeStationBossModel.SmRHand]);
            if(j==NativeStationBossModel.SmLHand)return stationBossRiderHandL-NativeBossPoseApply.ToUnity(pose.Position[NativeStationBossModel.SmLHand]);
            if(j==NativeStationBossModel.SmHat)
            {
                var head=NativeBossPoseApply.ToUnity(pose.Position[NativeStationBossModel.SmHead]);var hat=NativeBossPoseApply.ToUnity(pose.Position[NativeStationBossModel.SmHat]);
                if((hat-head).sqrMagnitude>1)return Vector3.zero;return stationBossRiderHead-head;
            }
            return Vector3.zero;
        }
        private void StationBossRiderPose(BossPose pose,float flash,float eyes)
        {
            stationBossRiderShown=false;
            if(stationBossRider!=null&&stationBossRider.Body.Root==null){stationBossRider=null;stationBossRiderTried=false;}
            if(stationBossRider==null&&!stationBossRiderTried)
            {
                if(zombiePool.Count==0)return;stationBossRiderTried=true;stationBossRider=ZombieModelMake(NativeZombieLooks.Stationmaster);if(stationBossRider==null)return;
                StationBossRiderBind(stationBossRider);
            }
            var z=stationBossRider;if(z?.Body?.Root==null||stationBossRiderBones==null)return;
            var root=z.Body.Root;if(!root.activeSelf)root.SetActive(true);var t=root.transform;
            int hips=NativeStationBossModel.SmHips;var hq=NativeBossPoseApply.ToUnity(pose.Rotation[hips]);t.rotation=hq;t.localScale=Vector3.one*StationBossRiderScale;
            // The pelvis on the hips joint (the model's root is the floor under it, so it hangs below the roof, unseen).
            var pelvis=stationBossRiderBones[0].Bone;var off=pelvis.position-t.position;t.position=NativeBossPoseApply.ToUnity(pose.Position[hips])+Vector3.down*StationBossRiderSink-off;
            foreach(var b in stationBossRiderBones)
            {
                if(b.Bone==null)continue;NQuaternion q=pose.Rotation[b.Joint];
                if(b.Toward>=0)q=NQuaternion.Slerp(q,pose.Rotation[b.Toward],b.Share);
                b.Bone.rotation=NativeBossPoseApply.ToUnity(q)*b.Rest;
            }
            stationBossRiderHandL=stationBossRiderBoneOf("hand_l",t);stationBossRiderHandR=stationBossRiderBoneOf("hand_r",t);stationBossRiderHead=stationBossRiderBoneOf("head",t);stationBossRiderShown=true;
            ZombieFlash(z,flash,eyes>0?1+eyes:0);
        }
        private readonly Dictionary<string,Transform> stationBossRiderNamed=new Dictionary<string,Transform>(StringComparer.Ordinal);
        private Vector3 stationBossRiderBoneOf(string name,Transform root)=>stationBossRiderNamed.TryGetValue(name,out var b)&&b!=null?b.position:root.position;
        // Once: each driven bone's rest turn in the model's frame, fitted so that a joint at rest (the rig's arms hang straight down)
        // gives the bone hanging down; the legs folded away inside the cab; the hands closed on the watch and the bell.
        private void StationBossRiderBind(ZombieBody z)
        {
            var root=z.Body.Root.transform;var skeleton=root.Find("VisualSkeleton");if(skeleton==null)return;stationBossRiderNamed.Clear();
            root.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.localScale=Vector3.one;
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))if(!stationBossRiderNamed.ContainsKey(t.name))stationBossRiderNamed[t.name]=t;
            Transform B(string n)=>stationBossRiderNamed.TryGetValue(n,out var t)?t:null;
            var list=new List<RiderBone>();int hips=NativeStationBossModel.SmHips,chest=NativeStationBossModel.SmChest,head=NativeStationBossModel.SmHead;
            void Trunk(string name,int joint,int toward,float share){var b=B(name);if(b!=null)list.Add(new RiderBone{Bone=b,Rest=b.rotation,Joint=joint,Toward=toward,Share=share});}
            // Toward straight down from the bone (to its child): where it points at rest, swung to hang down the rig's -y.
            void Limb(string name,string child,int joint){var b=B(name);var c=B(child);if(b==null||c==null)return;var hang=Quaternion.FromToRotation((c.position-b.position).normalized,Vector3.down);list.Add(new RiderBone{Bone=b,Rest=hang*b.rotation,Joint=joint,Toward=-1});}
            Trunk("pelvis",hips,-1,0);Trunk("spine_01",hips,chest,.1f);Trunk("spine_02",hips,chest,.3f);Trunk("spine_03",hips,chest,.55f);Trunk("spine_04",hips,chest,.8f);Trunk("spine_05",chest,-1,0);
            Trunk("clavicle_l",chest,-1,0);Trunk("clavicle_r",chest,-1,0);Trunk("neck_01",chest,head,.4f);Trunk("neck_02",chest,head,.75f);Trunk("head",head,-1,0);
            Limb("upperarm_l","lowerarm_l",NativeStationBossModel.SmLArm);Limb("lowerarm_l","hand_l",NativeStationBossModel.SmLFore);Limb("hand_l","middle_01_l",NativeStationBossModel.SmLHand);
            Limb("upperarm_r","lowerarm_r",NativeStationBossModel.SmRArm);Limb("lowerarm_r","hand_r",NativeStationBossModel.SmRFore);Limb("hand_r","middle_01_r",NativeStationBossModel.SmRHand);
            stationBossRiderBones=list.ToArray();
            // His legs go nowhere: he is grown into the cab.
            foreach(var leg in new[]{"thigh_l","thigh_r"}){var b=B(leg);if(b!=null)b.localScale=Vector3.one*1e-4f;}
            // The fingers curled a joint at a time toward the palm (the side the thumb's tip is on), as the fused arm's are.
            foreach(var side in new[]{"_l","_r"})
            {
                Transform hand=B("hand"+side),index=B("index_01"+side),pinky=B("pinky_01"+side),mid=B("middle_01"+side),thumb=B("thumb_03"+side);if(hand==null||index==null||pinky==null||mid==null)continue;
                var across=(index.position-pinky.position).normalized;var along=(mid.position-hand.position).normalized;var palm=Vector3.Cross(across,along).normalized;if(thumb!=null&&Vector3.Dot(thumb.position-hand.position,palm)<0)palm=-palm;
                foreach(var f in new[]{"index","middle","ring","pinky"})for(int k=1;k<=2;k++)
                {var j=B(f+"_0"+k+side);var c=B(f+"_0"+(k+1)+side);if(j==null||c==null)continue;var d=(c.position-j.position).normalized;j.rotation=Quaternion.FromToRotation(d,d*Mathf.Cos(.8f)+palm*Mathf.Sin(.8f))*j.rotation;}
            }
        }
        private void StationBossRiderHide(){var root=stationBossRider?.Body?.Root;if(root!=null&&root.activeSelf)root.SetActive(false);stationBossRiderShown=false;}
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeSlenderNode {public string name;public int parent,tentacle,segment;public float[] position,rotation,scale;}
    [Serializable] public sealed class NativeSlenderRig {public string commit;public NativeSlenderNode[] nodes;}
    public sealed partial class ArenaPresentation
    {
        private NativeSlenderRig slenderRig;private Matrix4x4[] slenderMatrices;private readonly Dictionary<int,(int hops,float at)> slenderHops=new Dictionary<int,(int,float)>();
        private void SlenderModel(NativeArena arena,NativeMinion minion)
        {
            var field=arena.LeetFields.FirstOrDefault(f=>f.Slender==minion);if(field==null)return;
            if(slenderRig==null){slenderRig=JsonUtility.FromJson<NativeSlenderRig>(Resources.Load<TextAsset>("SlenderRig").text);if(slenderRig.commit!=arena.Content.sourceCommit||slenderRig.nodes.Length>128)throw new InvalidOperationException("Unverified Slenderman model");slenderMatrices=new Matrix4x4[slenderRig.nodes.Length];}
            float time=arena.Time;if(!slenderHops.TryGetValue(field.Id,out var hop))hop=(field.Hops,-100);if(field.Hops>hop.hops)hop=(field.Hops,time);slenderHops[field.Id]=hop;
            float age=time-hop.at,reach=Mathf.Clamp01(field.Sanity*1.2f),sway=field.Seen?0:1,ext=Math.Max(Math.Max(0,1-age/.9f),Math.Max(reach*.8f,field.Seen?0:.15f)),glitch=age<.4f?1-age/.4f:0;if(glitch>0&&Mathf.Sin(age*140)>.35f)return;
            var root=Matrix4x4.TRS(minion.Body.transform.position-Vector3.up*1.23f,Quaternion.Euler(0,minion.Yaw,0),Vector3.one);
            for(int i=0;i<slenderRig.nodes.Length;i++)
            {
                var n=slenderRig.nodes[i];var pos=new Vector3(-n.position[0],n.position[1],n.position[2]);var q=new Quaternion(n.rotation[0],-n.rotation[1],-n.rotation[2],n.rotation[3]);var scale=new Vector3(n.scale[0],n.scale[1],n.scale[2]);Vector3? angles=null;
                if(n.name=="slender-body"){angles=new Vector3(0,0,Mathf.Sin(time*.7f)*.035f*sway);pos.x=-glitch*Mathf.Sin(age*97)*.18f;scale.x=1+glitch*.35f*Math.Abs(Mathf.Sin(age*61));}
                else if(n.name=="slender-torso")angles=new Vector3(.05f+Mathf.Sin(time*.45f)*.02f*sway,0,0);
                else if(n.name=="slender-head")angles=new Vector3(-.06f+(field.Seen?.1f:0),0,.28f+Mathf.Sin(time*.31f)*.08f*sway+(field.Seen?.12f:0));
                else if(n.name=="slender-arm-l"||n.name=="slender-arm-r"){float s=n.name.EndsWith("-l")?1:-1;angles=new Vector3(Mathf.Sin(time*.6f+s)*.05f*sway,0,s*.06f+reach*s*.25f);}
                else if(n.name=="slender-elbow")angles=new Vector3(-.08f-reach*.6f,0,0);
                else if(n.name=="slender-seg"){scale=Vector3.one*Math.Max(.001f,ext);angles=new Vector3(Mathf.Sin(time*2.3f+n.tentacle*1.7f+n.segment*.8f)*.35f*ext,Mathf.Cos(time*1.9f+n.tentacle+n.segment)*.3f*ext,0);}
                if(angles.HasValue){var a=angles.Value;q=CapturedAnimation.Quat(CapturedRig.Mirror(CapturedRig.Euler(a.x,a.y,a.z)));}
                slenderMatrices[i]=(n.parent<0?root:slenderMatrices[n.parent])*Matrix4x4.TRS(pos,q,scale);
            }
            sourceModels.Queue("slender",root,b=>slenderMatrices[b],b=>slenderRig.nodes[b].tentacle<0||ext>.02f);
        }
        private bool SourceMinionDeath(NativeDeath death,float age)
        {
            bool summon=death.model?.StartsWith("summon-")==true,horde=death.model?.StartsWith("horde-")==true;if(!summon&&!horde)return false;
            if(summon&&age>=.9f){if(age<1.6f)VoxelCloud(death.position,age-.75f,death.model=="summon-toy"?7:4,death.seed,24);return true;}if(horde&&age>=2.3f)return true;
            if(!minionRigs.TryGetValue(death.id,out var rig)){rig=captured.Create(transform,death.id);minionRigs.Add(death.id,rig);}
            if(summon)NativeSummonPose.Death(minionPose,death.model=="summon-toy",age);else NativeSourceMotion.Sample(death.model+"-death",age,2.3f,minionPose);
            for(int i=0;i<22;i++){rig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(minionPose.Position[i]));rig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(minionPose.Rotation[i]));}
            rig.Root.transform.SetPositionAndRotation(death.position,Quaternion.Euler(0,death.yaw,0));rig.Root.transform.localScale=Vector3.one*(summon?(death.model=="summon-toy"?.6f:.85f):1);
            sourceModels.Queue(death.model,rig.Root.transform.localToWorldMatrix,b=>rig.Bones[b].localToWorldMatrix,b=>MinionPartVisible(death.lost,b),opacity:summon?1:Mathf.Clamp01((2.3f-age)/.3f));return true;
        }
        private static bool MinionPartVisible(int lost,int b){int[] bits={1,2,4,8,16,32,64,128,256},bones={5,7,11,8,12,14,18,15,19};for(int i=0;i<bits.Length;i++)if((lost&bits[i])!=0&&BoneWithin(b,bones[i]))return false;return true;}
    }
}

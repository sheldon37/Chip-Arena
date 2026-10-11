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
        private readonly NativeSummonBreakups summonBreakups=new NativeSummonBreakups();
        private NativeArena summonEventArena;private int summonEventGeneration=-1,summonEvent;
        // chaos-visuals.ts summons(): a minionHit on a live summon puffs 3 voxels at the hit (Core/NativeSummonBreak.cs). Unity
        // emits a summon's hits as Hit events targeting its id (NativeImpactCues draws nothing for a non-pilot target). now:
        // FieldClock, the source's s.time.
        private void SummonHitPuffs(NativeArena arena,float now)
        {
            if(arena!=summonEventArena||arena.Generation!=summonEventGeneration){summonEventArena=arena;summonEventGeneration=arena.Generation;summonEvent=0;}
            var events=arena.Events;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=summonEvent)continue;summonEvent=e.Id;
                if(e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot||!NativeSummonBreak.Fresh(now,e.Time)||!SummonTarget(arena,e.Target,out bool toy))continue;
                SourceBurst(Vec(e.Position),NativeSummonBreak.HitColor(toy),NativeSummonBreak.HitCount,NativeSummonBreak.HitPower);
            }
        }
        // A live summon, or the one this hit just killed (gone from the list; the source still holds its rig for the event).
        private static bool SummonTarget(NativeArena arena,int id,out bool toy)
        {
            toy=false;if(id<=0)return false;
            foreach(var m in arena.Minions)if(m.Id==id){toy=m.Kind=="toy";return m.Pve==null&&NativeSummonBreak.Summon(m.Kind);}
            foreach(var d in arena.MinionDeaths)if(d.id==id)return NativeSummonBreak.SummonModel(d.model,out toy);
            return false;
        }
        private bool SourceMinionDeath(NativeDeath death,float age)
        {
            bool summon=NativeSummonBreak.SummonModel(death.model,out bool toy),horde=death.model?.StartsWith("horde-")==true;if(!summon&&!horde)return false;
            // At SUMMON_LOOK.death.breakAt the rig hides and three bursts fire up the body in the gib colours, once.
            if(summon&&summonBreakups.Due(death.id,death.seed,age))for(int i=0;i<NativeSummonBreak.Bursts;i++){NativeSummonBreak.Burst(toy,i,out float lift,out int color,out int count);SourceBurst(death.position+Vector3.up*lift,color,count,NativeSummonBreak.BurstPower);}
            if(summon&&age>=NativeSummonBreak.BreakAt)return true;if(horde&&age>=NativeMinionFx.DeathFxSeconds)return true;
            if(!minionRigs.TryGetValue(death.id,out var rig)){rig=captured.Create(transform,death.id);minionRigs.Add(death.id,rig);}
            if(summon)NativeSummonPose.Death(minionPose,toy,age);else NativeSourceMotion.Sample(death.model+"-death",Mathf.Min(age,2.3f),2.3f,minionPose);
            NativePoseWriter.Write(rig.Bones,minionPose,"a summon rig");
            // poseSummonDeath's jitter before the break-up moves the rig group's x (source axes; Unity's x is mirrored).
            Vector3 at=summon?death.position-Vector3.right*NativeSummonBreak.Shake(age):death.position;
            rig.Root.transform.SetPositionAndRotation(at,Quaternion.Euler(0,death.yaw,0));rig.Root.transform.localScale=Vector3.one*(summon?(toy?NativeSummonBreak.ToyScale:NativeSummonBreak.ZombieScale):1);
            // A horde add chars joint by joint and burns away by height (motion.ts dyingLook, replacing the first port's dithered
            // fade), then its flames, ash and scorch outlast the body to DEATH_FX_SECONDS (NativeMinionFxPresentation.cs), on the
            // presentation clock as the other death effects.
            if(horde){float fx=hordeClock-death.at;bool body=!NativeMinionFx.Gone(fx);if(body)HordeDeathBody(rig,death.model,death.id,death.lost,fx,b=>MinionPartVisible(death.lost,b));HordeAdd(rig,death.id,float.NaN,Mathf.Max(0,fx),death.position.y-.85f,false,Vector3.zero,death.lost,body);return true;}
            sourceModels.Queue(death.model,rig.Root.transform.localToWorldMatrix,b=>rig.Bones[b].localToWorldMatrix,b=>MinionPartVisible(death.lost,b));return true;
        }
        private static bool MinionPartVisible(int lost,int b){int[] bits={1,2,4,8,16,32,64,128,256},bones={5,7,11,8,12,14,18,15,19};for(int i=0;i<bits.Length;i++)if((lost&bits[i])!=0&&BoneWithin(b,bones[i]))return false;return true;}
    }
}

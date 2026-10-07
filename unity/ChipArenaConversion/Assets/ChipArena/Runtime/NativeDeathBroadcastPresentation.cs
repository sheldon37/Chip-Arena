using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        private Material hologramMaterial;
        public string Hologram(string model)
        {
            string id="hologram-"+model;if(models.ContainsKey(id))return id;if(!models.TryGetValue(model,out var source))return model;if(hologramMaterial==null){hologramMaterial=new Material(Resources.Load<Shader>("NativeHologram")){enableInstancing=true};materials.Add(hologramMaterial);}models[id]=source.Select(p=>new Part{Name=p.Name,Bone=p.Bone,Mesh=p.Mesh,Material=hologramMaterial}).ToArray();return id;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private readonly NativeDeathBroadcast broadcasts=new NativeDeathBroadcast();private readonly Dictionary<int,CapturedAnimation.Player> broadcastRigs=new Dictionary<int,CapturedAnimation.Player>();
        public IEnumerable<string> DeathMessages(float time)=>broadcasts.Items.Where(i=>time>=i.At+1.05f&&time<i.At+5.15f).Select(i=>i.Text);
        private void DeathBroadcasts(NativeArena arena)
        {
            float t=arena.Time;bool clear=arena.Match.Phase==NativePhase.Lobby||arena.Match.Phase==NativePhase.Intro;broadcasts.Tick(t,clear);if(!clear)foreach(var p in arena.Pilots.Where(p=>p.Hp<=0&&p.Death!=null).OrderBy(p=>p.Death.at).ThenBy(p=>p.Death.seed))broadcasts.Add(p.Death.seed,p.Seat.id,p.Death.owner,p.Death.at,t,p.Seat.name,arena.Match.Seat(p.Death.owner)?.name);
            foreach(int id in broadcastRigs.Keys.Where(id=>!broadcasts.Items.Any(a=>a.Seed==id)).ToArray()){broadcastRigs[id].Dispose();broadcastRigs.Remove(id);}
            foreach(var item in broadcasts.Items)
            {
                float age=t-item.At;if(age<0)continue;Vector3 center=new Vector3(new[]{0,-43,43}[item.Slot],72,-115);float u=Mathf.Clamp01(age/1.05f),burst=age-1.05f,fade=Mathf.Clamp01((5.6f-age)/.8f);
                if(burst<0){Vector3 at=center+new Vector3(Mathf.Sin(u*8)*.12f,-25*(1-u)*(1-u),0)*5.5f;Add(8,Matrix4x4.TRS(at,Quaternion.identity,Vector3.one*1.485f));Beam(at,at-Vector3.up*(6*(1-u*.5f)*5.5f),.16f,8);continue;}
                if(burst<2.2f)for(int i=0;i<72;i++){float y=1-2*(i+.5f)/72,phi=i*2.39996323f,r=Mathf.Sqrt(1-y*y),distance=2+i%7*.22f;Vector3 dir=new Vector3(Mathf.Cos(phi)*r,y,Mathf.Sin(phi)*r)*distance;Add(8,Matrix4x4.TRS(center+(dir*burst-Vector3.up*burst*burst*.4f)*5.5f,Quaternion.identity,Vector3.one*.6f*(1-burst/2.2f)));}
                if(!broadcastRigs.TryGetValue(item.Seed,out var rig)){rig=captured.Create(transform,-item.Seed);broadcastRigs[item.Seed]=rig;}
                float q=Mathf.Clamp01(burst/.32f)-1,reveal=1+2.3f*q*q*q+1.3f*q*q,show=MathUtil.Smooth(burst/.26f);var seat=arena.Match.Seat(item.Victim);var pose=new CapturedRig.Pose();pose.Rotation[5]=CapturedRig.Euler(Mathf.Sin(age*.85f)*.018f,Mathf.Sin(age*.6f)*.045f);
                for(int b=0;b<22;b++){rig.Bones[b].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(pose.Position[b]));rig.Bones[b].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(pose.Rotation[b]));}
                rig.Root.transform.SetPositionAndRotation(center-Vector3.up*7,Quaternion.identity);rig.Root.transform.localScale=new Vector3(28,28*Math.Max(.01f,reveal),28);
                bool Visible(int b)=>b==3||b==4||b==5||b==6||b==10;float alpha=.55f*fade*show*(.96f+.04f*Mathf.Sin(age*29));sourceModels.Queue(sourceModels.Hologram("pilot"),rig.Root.transform.localToWorldMatrix,b=>rig.Bones[b].localToWorldMatrix,Visible,opacity:alpha);
                if(seat?.hat!=null)sourceModels.Queue(sourceModels.Hologram("hat-"+seat.hat),rig.Root.transform.localToWorldMatrix,b=>rig.Bones[b].localToWorldMatrix,opacity:alpha);
                foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1}){Vector3 c=center+new Vector3(sx*16.5f,sy*20+1.375f,8);Beam(c,c+Vector3.right*(-sx*3.575f),.15f*fade,8);Beam(c,c+Vector3.up*(-sy*4.95f),.15f*fade,8);}
            }
        }
        private void ClearBroadcasts(){broadcasts.Tick(0,true);foreach(var rig in broadcastRigs.Values)rig.Dispose();broadcastRigs.Clear();}
    }
}

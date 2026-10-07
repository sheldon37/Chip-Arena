using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private void PveMachinePresentation(NativeArena arena)
        {
            if(arena.Pve==null)return;float t=arena.Time;var view=arena.Pve;
            foreach(var m in view.Content.machines)
            {
                var root=Matrix4x4.TRS(new Vector3(m.x,0,m.z),Quaternion.Euler(0,m.yaw*Mathf.Rad2Deg,0),Vector3.one);float flap=0,door=0,act=0,deny=0;
                foreach(var use in view.Machines.Where(u=>u.machine==m.m)){float age=t-use.at;if(use.result=="deny")deny=Math.Max(deny,NativeMachineRules.Deny(age));else act=Math.Max(act,NativeMachineRules.Activity(age,use.result=="med"));if(use.result=="chip")flap=Math.Max(flap,NativeMachineRules.Flap(age));if(use.result=="recruit")door=Math.Max(door,NativeMachineRules.Door(age));}
                float level=m.kind=="med"?.8f+.2f*Mathf.Sin(t*1.3f+m.m)+1.2f*act:.75f+.25f*Mathf.Sin(t*2.1f+m.m*1.7f)+1.1f*act;var tint=new Color(level,level*(1-.85f*deny),level*(1-.85f*deny));sourceModels.Queue("machine-"+m.kind,root,b=>root,partTint:b=>b==1?tint:Color.white);
                Vector3 pivot=m.kind=="chip"?new Vector3(0,1.12f,.27f):m.kind=="recruit"?new Vector3(.49f,0,.22f):new Vector3(0,1.55f,.2f);float k=act>0?Mathf.Repeat(t*1.4f,1):0,opacity=m.kind=="med"?act*.9f*(1-k):1;
                var moving=root*Matrix4x4.TRS(pivot,m.kind=="chip"?Quaternion.Euler(-1.25f*flap*Mathf.Rad2Deg,0,0):m.kind=="recruit"?Quaternion.Euler(0,1.85f*door*Mathf.Rad2Deg,0):Quaternion.identity,Vector3.one*(m.kind=="med"?1+2.2f*k:1));if(opacity>0)sourceModels.Queue("machine-moving-"+m.kind,moving,opacity:opacity);
            }
            foreach(var k in view.Pickups)
            {
                var machine=view.Content.machines.FirstOrDefault(m=>m.m==k.Machine)??view.Content.machines[0];var from=new V3(machine.x+Mathf.Sin(machine.yaw)*.27f,1.02f,machine.z+Mathf.Cos(machine.yaw)*.27f);var pose=NativeMachineRules.Pickup(from,NativeArena.Vec(k.Position),k.Born,t);if(!pose.visible)continue;
                var rarity=arena.Content.rarities.FirstOrDefault(r=>r.id==k.Tier);Color tint=Color.white;if(k.Tier=="rainbow")tint=Color.HSVToRGB(Mathf.Repeat(t*.25f,1),.74f,.981f);else if(rarity!=null)ColorUtility.TryParseHtmlString(rarity.color,out tint);tint=tint.linear;
                var root=Matrix4x4.TRS(NativeArena.Vec(pose.position),Quaternion.Euler(0,pose.spin*Mathf.Rad2Deg,0)*Quaternion.Euler(-1.25f*Mathf.Rad2Deg,0,0),Vector3.one*.62f);sourceModels.Queue("pve-pickup",root,b=>root,partTint:b=>b==1?tint:Color.white);
                if(pose.progress>=1&&(k.Tier=="epic"||k.Tier=="legendary"||k.Tier=="mythic"||k.Tier=="rainbow"))Beam(new Vector3(k.Position.x,.05f,k.Position.z),new Vector3(k.Position.x,7,k.Position.z),.025f,6);
            }
        }
    }
}

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
                float level=m.kind=="med"?.8f+.2f*Mathf.Sin(t*1.3f+m.m)+1.2f*act:.75f+.25f*Mathf.Sin(t*2.1f+m.m*1.7f)+1.1f*act;var tint=new Color(level,level*(1-.85f*deny),level*(1-.85f*deny));sourceModels.Queue(sourceModels.MachineModel(NativeMachineModels.BodyId(m.kind),"machine-"+m.kind),root,b=>root,partTint:b=>b==1?tint:Color.white);
                Vector3 pivot=m.kind=="chip"?new Vector3(0,1.12f,.27f):m.kind=="recruit"?new Vector3(.49f,0,.22f):new Vector3(0,1.55f,.2f);float k=act>0?Mathf.Repeat(t*1.4f,1):0,opacity=m.kind=="med"?act*.9f*(1-k):1;
                var moving=root*Matrix4x4.TRS(pivot,m.kind=="chip"?Quaternion.Euler(-1.25f*flap*Mathf.Rad2Deg,0,0):m.kind=="recruit"?Quaternion.Euler(0,1.85f*door*Mathf.Rad2Deg,0):Quaternion.identity,Vector3.one*(m.kind=="med"?1+2.2f*k:1));if(opacity>0)sourceModels.Queue(sourceModels.MachineModel(NativeMachineModels.MovingId(m.kind),"machine-moving-"+m.kind),moving,opacity:opacity);
            }
            // pickups.ts: the chip, its additive glow sprite (YOURS for the local seat's pickups) and, for epic and above once
            // landed, the translucent pulsing beam (Core/NativePickupFx.cs, NativePickupGlowPresentation.cs).
            var glow=PickupGlow();
            foreach(var k in view.Pickups)
            {
                var machine=view.Content.machines[0];foreach(var m in view.Content.machines)if(m.m==k.Machine){machine=m;break;}
                var from=new V3(machine.x+Mathf.Sin(machine.yaw)*.27f,1.02f,machine.z+Mathf.Cos(machine.yaw)*.27f);var pose=NativeMachineRules.Pickup(from,NativeArena.Vec(k.Position),k.Born,t);if(!pose.visible)continue;
                // tierColor: the rarity colour, or the rainbow hue (setHSL: linear components). tint is linear, shown is the screen colour.
                Color tint=Color.white,shown=Color.white;
                if(k.Tier=="rainbow"){NativePickupFx.Rainbow(t,out float r,out float g,out float b);tint=new Color(r,g,b);shown=tint.gamma;}
                else{foreach(var rarity in arena.Content.rarities)if(rarity.id==k.Tier){ColorUtility.TryParseHtmlString(rarity.color,out shown);break;}tint=shown.linear;}
                // The tier's cartridge (NativeChipCartridge): its lights breathing in the tint, its ring (Epic and up) orbiting the core.
                var root=Matrix4x4.TRS(NativeArena.Vec(pose.position),Quaternion.Euler(0,pose.spin*Mathf.Rad2Deg,0)*Quaternion.Euler(-1.25f*Mathf.Rad2Deg,0,0),Vector3.one*.62f);string model=sourceModels.MachineModel(NativeMachineModels.PickupModel(k.Tier),"pve-pickup");
                var orbit=root*sourceModels.PickupOrbit(model,t);var lit=tint*(.85f+.3f*NativeChipCartridge.Breath(NativeChipCartridge.Of(k.Tier),t,false));
                sourceModels.Queue(model,root,b=>b==NativeMachineModels.Orbit?orbit:root,partTint:b=>b==1||b==NativeMachineModels.Orbit?lit:Color.white);
                if(glow==null)continue;bool mine=k.Owner!=null&&k.Owner.Seat.id==arena.LocalId;Vector3 at=NativeArena.Vec(pose.position);
                glow.Glow(at+Vector3.up*NativePickupFx.GlowLift,new Color(shown.r,shown.g,shown.b,NativePickupFx.GlowOpacity(mine,t)),mine);
                if(NativePickupFx.BeamShown(k.Tier,pose.progress))glow.Beam(new Vector3(at.x,0,at.z),new Color(shown.r,shown.g,shown.b,NativePickupFx.BeamOpacity(mine,t)));
            }
            glow?.Draw(DeathFx()?.PopFont);
        }
    }
}

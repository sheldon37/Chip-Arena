using System;
using System.Collections.Generic;
using UnityEngine;
using NQuaternion=System.Numerics.Quaternion;
using NVector3=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        private Part killDust;
        /// <summary>A translucent lit cube (vent-fall.ts dust and voxel puff: MeshLambertMaterial, transparent).</summary>
        public void KillDust(Matrix4x4 matrix,Color albedo,float opacity)
        {
            if(killDust==null)killDust=KillPart("kill-dust",Cube().Mesh("Kill dust",Color.white,new Vector4(1,1,1,0)),true);
            if(killDust!=null&&opacity>.001f)Draw(killDust,matrix,new Vector4(1,1,1,opacity),albedo,NoGlow);
        }
    }
    public sealed partial class ArenaPresentation
    {
        /// <summary>A vent-fall / vent-splat victim (vent-fall.ts): a scripted rig posed by Core/NativeVentDeath.cs each frame,
        /// with no physics. Built once per death (seed); nothing per frame allocates.</summary>
        private sealed class VentBody
        {
            public int Seed;public bool Splat,Carried;public float Age;
            public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly NQuaternion[] From=new NQuaternion[22];public NVector3 FromBody;
            public readonly Matrix4x4[] Bones=new Matrix4x4[22];public Matrix4x4 Root;public bool Visible,Landed;public float Since;
            public NativeVentDeath.Dust[] Dust=Array.Empty<NativeVentDeath.Dust>();public int Band=-1;public bool Caster;
            public Func<int,Matrix4x4> Bone,WeaponBone;public Func<int,bool> WeaponShown;public string Hat,Weapon;
            public Matrix4x4 At(int b)=>Bones[b];
        }
        private readonly Dictionary<int,VentBody> ventBodies=new Dictionary<int,VentBody>();private readonly List<int> staleVents=new List<int>();
        private static bool VentStyle(string style)=>style=="vent-fall"||style=="vent-splat";
        private VentBody Vent(int id)=>ventBodies.TryGetValue(id,out var v)?v:null;
        private void ClearVents(){ventBodies.Clear();}
        /// <summary>Drops vent bodies whose death is gone or replaced (a vent-fall becomes a vent-splat with a new seed).</summary>
        private void PruneVents(NativeArena arena)
        {
            staleVents.Clear();foreach(var item in ventBodies)if(!DeathListed(arena,item.Key,item.Value.Seed))staleVents.Add(item.Key);foreach(int id in staleVents)ventBodies.Remove(id);
        }
        /// <summary>
        /// vent-fall.ts for one death at presentation-clock age <paramref name="fx"/>: poses the scripted rig (vent-fall: carried
        /// death pose, flail, drop, spin; vent-splat: fall, squash, wobble, held from VENT_FX.keepAge on a kept body) and draws
        /// it with its hat, weapon and team bands, plus the splat's dust puff and SPLAT! pop. Returns the body (a kept splat is what
        /// the PvE revive repair rebuilds from), or null when nothing is shown.
        /// </summary>
        private VentBody PresentVent(NativeArena arena,NativeDeath death,bool pilot,float fx,NativePilot live,bool draw=true)
        {
            if(!ventBodies.TryGetValue(death.id,out var v)||v.Seed!=death.seed)
            {
                v=new VentBody{Seed=death.seed,Splat=death.style=="vent-splat"};ventBodies[death.id]=v;var body=v;v.Bone=b=>body.At(b);
                string spec=death.actor?.SourceSpec;v.Caster=spec=="fire"||spec=="water"||spec=="lightning"||spec=="darkness"||spec=="leet";bool caster=v.Caster;
                // buildWeapon(victim.weapon, spec): one weapon in the right hand (a caster's staff too; no off-hand weapon).
                v.WeaponBone=b=>body.At(caster&&b==CapturedRig.LHand?CapturedRig.RHand:b);v.WeaponShown=b=>b==CapturedRig.RHand||caster&&b==CapturedRig.LHand;
                v.Weapon=!string.IsNullOrEmpty(spec)&&spec!="siren"&&sourceModels.Has("weapon-"+spec)?Skinned(death.actor,"weapon-"+spec):null;v.Hat=!string.IsNullOrEmpty(death.actor?.Hat)&&death.actor.Hat!="none"?death.actor.Hat:null;
                if(v.Splat){int n=NativeVentDeath.DustCount(Mathf.Clamp01(EffectsQuality));v.Dust=new NativeVentDeath.Dust[n];for(int i=0;i<n;i++)v.Dust[i]=NativeVentDeath.DustDirection(death.seed,i);}
                // DeathPoseCarry: the dead rig's last pose (joints matched by name), when this client still has it.
                else if(rigs.TryGetValue(death.id,out var rig)){for(int i=0;i<22;i++)v.From[i]=CapturedRig.Mirror(CapturedAnimation.Num(rig.Bones[i].localRotation));v.FromBody=CapturedRig.Mirror(CapturedAnimation.Num(rig.Bones[0].localPosition));v.Carried=true;}
                v.Band=pilot&&live!=null&&(death.model??"pilot")=="pilot"?NativeStatusVisuals.PilotBand(arena.Match.Teams,arena.Match.Cooperative,live.Seat.AuthorityBoss,live.Seat.team,live.Seat.id==arena.LocalId):-1;
            }
            // The presentation clock never runs the effect backwards.
            if(fx>v.Age)v.Age=fx;float age=v.Age;var yaw=Quaternion.Euler(0,death.yaw,0);
            if(!v.Splat)
            {
                NativeVentDeath.Fall(age,v.Pose,out float drop,out v.Visible,out float spin);if(v.Carried)NativeVentDeath.Carry(v.Pose,v.From,v.FromBody,age);
                v.Root=Matrix4x4.TRS(death.position+Vector3.down*drop,yaw,Vector3.one)*Matrix4x4.Rotate(CapturedAnimation.Quat(CapturedRig.Mirror(MeleeMath.Euler(0,spin,0))));
            }
            else
            {
                float shown=death.kept?Math.Min(age,NativeVentDeath.KeepAge):age;NativeVentDeath.Splat(shown,v.Pose,out float lift,out float sxz,out float sy,out v.Landed);v.Since=shown-NativeVentDeath.Drop;v.Visible=true;
                float ground=death.position.y-NativeVentDeath.FloorOffset+NativeVentDeath.Lie;
                v.Root=Matrix4x4.TRS(new Vector3(death.position.x,ground+lift,death.position.z),yaw,new Vector3(sxz,sy,sxz))*Matrix4x4.Rotate(CapturedAnimation.Quat(CapturedRig.Mirror(MeleeMath.Euler(NativeVentDeath.SplatTilt,0,0))));
            }
            v.Pose.World();for(int b=0;b<22;b++)v.Bones[b]=v.Root*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(v.Pose.WorldPosition[b])),CapturedAnimation.Quat(CapturedRig.Mirror(v.Pose.WorldRotation[b])),Vector3.one);
            if(!v.Visible)return null;
            if(draw)DrawVent(v,death,age);
            return v;
        }
        /// <summary>The scripted victim (cinematicVictim: the live rig's model, armour glow and team bands; no wounds), its hat and
        /// right-hand weapon, then the splat's puff and pop.</summary>
        private void DrawVent(VentBody v,NativeDeath death,float clock,bool extras=true)
        {
            var root=v.Root;string pilot=BodyAt(death.id,death.position,out int bodySkin);sourceModels.Queue(pilot,root,v.Bone,null,body:BodyTint(death.actor?.BodyColor),accent:BodyAccent(bodySkin,death.actor?.AccentColor,death.actor?.Hat,death.actor?.HatColor));
            if(v.Hat!=null&&!AuditorHalo(v.Hat,v.At(CapturedRig.Head),death.at+clock,1,HaloReduced,0,null,color:death.actor?.HatColor))sourceModels.Queue(HatModel(v.Hat,death.actor?.HatColor),root,v.Bone,body:HatBody(v.Hat,death.actor?.HatColor));
            if(v.Weapon!=null)sourceModels.Queue(v.Weapon,root,v.WeaponBone,v.WeaponShown);
            if(v.Band>=0&&StatusReady())for(int k=0;k<bandLocal.Length;k++)teamBands[v.Band].Add(v.At(NativeStatusVisuals.BandBones[k])*bandLocal[k]);
            if(!v.Splat||!extras||!v.Landed)return;
            // Dust and voxel puff along the floor: translucent Lambert cubes, every fifth in the player's colour.
            float floor=death.position.y-NativeVentDeath.FloorOffset,opacity=NativeVentDeath.DustOpacity(v.Since);
            NativeKillEffectRules.Linear(NativeVentDeath.DustColor,out double dr,out double dg,out double db);NativeKillEffectRules.Linear(NativeKillEffectRules.ParseHex(death.actor?.AccentColor,0x70e1d0),out double cr,out double cg,out double cb);
            Color dust=new Color((float)dr,(float)dg,(float)db,1),chip=new Color((float)cr,(float)cg,(float)cb,1);
            for(int i=0;i<v.Dust.Length;i++)
            {
                if(!NativeVentDeath.DustPose(v.Dust[i],v.Since,out float along,out float height,out float rx,out float ry,out float scale))continue;var d=v.Dust[i];
                var at=new Vector3(death.position.x-d.X*along,floor+height,death.position.z+d.Z*along);
                sourceModels.KillDust(Matrix4x4.TRS(at,CapturedAnimation.Quat(CapturedRig.Mirror(MeleeMath.Euler(rx,ry,0))),Vector3.one*scale),i%5==0?chip:dust,opacity);
            }
            // SPLAT! pops over the body: a camera-facing sprite, drawn over everything (depthTest false, renderOrder 6).
            var fx=DeathFx();var eye=WorldEye;
            if(fx!=null&&eye!=null&&NativeVentDeath.Pop(v.Since,out float width,out float rise,out float alpha))
            {
                var centre=new Vector3(death.position.x,floor+rise,death.position.z);float px=width/NativeVentDeath.PopWidth;
                Vector3 right=eye.transform.right*px,down=-eye.transform.up*px,origin=centre-right*(NativeVentDeath.PopWidth*.5f)-down*(NativeVentDeath.PopHeightPx*.5f);
                var font=fx.PopFont;float cx=NativeVentDeath.PopWidth*.5f,cy=NativeVentDeath.PopHeightPx*.5f,ink=NativeVentDeath.PopShadowWidth*.5f,line=NativeVentDeath.PopLineWidth*.5f;
                // ctx.strokeText (14 px, 4 px low), fillText, strokeText (3 px): strokes as the glyphs offset around their outline.
                var shadow=fx.Hex(NativeVentDeath.PopShadow,alpha);for(int k=0;k<16;k++){float a=k*Mathf.PI/8;fx.Text(fx.Pop,font,"SPLAT!",64,FontStyle.Bold,NativeVentDeath.PopFontPx,cx,cy,0,origin,right,down,shadow,Mathf.Cos(a)*ink,NativeVentDeath.PopShadowDrop+Mathf.Sin(a)*ink,NativeVentDeath.PopAngle);}
                var rim=fx.Hex(NativeVentDeath.PopLine,alpha);for(int k=0;k<8;k++){float a=k*Mathf.PI/4;fx.Text(fx.Pop,font,"SPLAT!",64,FontStyle.Bold,NativeVentDeath.PopFontPx,cx,cy,0,origin,right,down,rim,Mathf.Cos(a)*line,Mathf.Sin(a)*line,NativeVentDeath.PopAngle);}
                fx.Text(fx.Pop,font,"SPLAT!",64,FontStyle.Bold,NativeVentDeath.PopFontPx,cx,cy,0,origin,right,down,fx.Hex(NativeVentDeath.PopFill,alpha),0,0,NativeVentDeath.PopAngle);
            }
        }
    }
}

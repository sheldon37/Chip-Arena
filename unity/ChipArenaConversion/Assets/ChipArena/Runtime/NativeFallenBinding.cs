using System;
using System.Linq;
using UnityEngine;
using NV=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public (double clip,double epoch) FallenSongClock()
        {
            var z=ZoneBoss;var f=z.fallen;
            if(Pve.Holding&&Pve.Cinematic=="intro")return (NativeFallenSong.IntroFrom+Pve.HoldDuration-Pve.HoldLeft,f.started);
            if(z.deathAt>=0)return (Math.Min(NativeFallenSong.End,NativeFallenSong.DeathAt+Time-z.deathAt),z.deathAt+5000);
            if(f.draftAt>=0)return (NativeFallenSong.Break+(Time-f.draftAt)%(NativeFallenSong.P3-NativeFallenSong.Break),f.draftAt+3000);
            NativeFallenSong.Loop(z.phase,out float from,out float to);float age=Math.Max(0,Time-f.phaseStarted),length=to-from;return (from+age%length,f.phaseStarted+Math.Floor(age/length)*1000+z.phase*10000);
        }
    }
    public sealed partial class ArenaPresentation
    {
        // Binding only: uses Claude's existing intro/death shots, clips, shaders and effects.
        private void FallenBossViewOf(NativeArena a,FallenBossFrame f)
        {
            var z=a.ZoneBoss;var state=z.fallen;float now=a.Time;
            bool intro=a.Pve.Holding&&a.Pve.Cinematic=="intro",death=z.deathAt>=0;
            float script=intro?a.Pve.HoldDuration-a.Pve.HoldLeft:death?NativeFallenBossPreview.Sections[8].From+now-z.deathAt:NativeFallenBossPreview.Sections[z.phase==1?1:z.phase==2?3:z.phase==3?5:7].From+2;
            NativeFallenBossPreview.Eval(script,VisualMotionBank,f);f.Live=state;var song=a.FallenSongClock();f.SongClip=(float)song.clip;f.SongEpoch=(int)song.epoch;f.Actions=Array.Empty<NativeFallenBossPreview.Act>();f.LiveAdds=Array.Empty<FallenFxProp>();
            if(intro||death){f.Target=NV.Zero;return;}
            var m=a.Minions.FirstOrDefault(m=>m.Id==z.actor);if(m==null)return;
            f.P=now;f.Boss=ToNV(m.Body.position);f.Yaw=m.Yaw;f.Phase=z.phase;f.Kind=state.draftAt>=0?NativeFallenBossPreview.KindChips:NativeFallenBossPreview.KindFight;f.ChipAge=state.draftAt>=0?now-state.draftAt:-1;f.ChipPhase=z.phase;f.Black=0;f.SlashAge=-1;f.CardAge=-1;f.Plant=0;f.Kneel=0;f.Air=0;f.Trail=0;f.Flare=0;f.Carry=1;f.Track=0;f.ClipFrom=f.ClipTo="Unarmed-Idle";f.TimeFrom=f.TimeTo=now;f.Mix=1;f.BossShown=true;
            if(state.phaseChangeAt>=0){f.Kneel=1;f.SlashKind=NativeFallenSlash.Cut;f.SlashAge=Math.Max(0,1.7f-(state.phaseChangeAt-now));f.SlashAngle=-32;}
            if(state.draftAt>=0&&now-state.draftAt<1.7f){f.SlashKind=NativeFallenSlash.Cut;f.SlashAge=now-state.draftAt;f.SlashAngle=-32;}
            var target=a.Pilots.FirstOrDefault(p=>p.Seat.id==z.targetSeat);f.Target=target==null?f.Boss:ToNV(target.Body.position);
            if(z.action!=null&&z.action!="arrive"&&now<=z.actionAt+z.duration+2)
            {
                var move=NativeFallenBoss.Move(z.action);f.ClipFrom=f.ClipTo=move.Clip??"Armed-Activate-Left";var seg=new NativeFallenBossPreview.Seg{T0=z.actionAt,T1=z.actionAt+z.duration,Clip=f.ClipTo,Wind=z.hitAt-z.actionAt,Hit=Math.Max(.2f,move.Strike),Rate=1,Mode=2};if(z.action=="cleave"&&now>=z.hitAt){seg.T0+=Math.Min(2,(int)((now-z.hitAt)/.3f))*.3f;seg.T1=seg.T0+seg.Wind+.3f;}f.TimeFrom=f.TimeTo=NativeFallenBossPreview.ClipTime(seg,now,VisualMotionBank);f.Carry=0;f.Trail=now>=z.hitAt&&now<z.hitAt+.3f?1:0;f.Flare=f.Trail;
                var act=new NativeFallenBossPreview.Act{Id=z.action,At=z.actionAt,Hit=z.hitAt,End=z.actionAt+z.duration,Phase=z.phase,Seed=state.patternSeed,Target=z.targetSeat,From=new NV(z.laneX0,0,z.laneZ0),To=new NV(z.laneX1,0,z.laneZ1),Aim=z.actionYaw};
                if(z.action=="verdict"||z.action=="last")act.To=new NV(z.targetX,0,z.targetZ);
                if(z.action=="severance"&&NativeFallenRules.Chip(state,"second-wing")){var second=act;second.At+=.6f;second.Hit+=.6f;second.End+=.6f;f.Actions=new[]{act,second};}else f.Actions=new[]{act};
            }
            var acts=new System.Collections.Generic.List<NativeFallenBossPreview.Act>(f.Actions);var props=new System.Collections.Generic.List<FallenFxProp>();foreach(var add in a.Minions.Where(m=>m.Hp>0&&m.Kind.StartsWith("fallen-",StringComparison.Ordinal)))
            {string kind=add.Kind.Substring(7);var victim=a.Pilots.Where(p=>!p.Seat.AuthorityBoss&&p.Hp>0&&p.Seat.connected).OrderBy(p=>(p.Body.position-add.Body.position).sqrMagnitude).FirstOrDefault();if(victim!=null&&kind!="penitent")acts.Add(new NativeFallenBossPreview.Act{Id=kind+"-beam",At=now-1,Hit=now,End=now+1,From=ToNV(add.Body.position)+NV.UnitY,To=ToNV(victim.Body.position)});props.Add(new FallenFxProp{Id=kind=="cantor"?"cantor":kind=="penitent"?"penitent":"throne-ring",At=ToNV(add.Body.position),Turn=System.Numerics.Quaternion.CreateFromAxisAngle(NV.UnitY,add.Yaw*Mathf.Deg2Rad),Scale=1,Glow=1});if(kind=="throne")props.Add(new FallenFxProp{Id="throne-eye",At=ToNV(add.Body.position),Turn=System.Numerics.Quaternion.Identity,Scale=1,Glow=2});}f.LiveAdds=props.ToArray();f.Actions=acts.ToArray();
        }
    }
}

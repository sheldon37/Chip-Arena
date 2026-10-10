using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private void WorldEffects(NativeArena arena)
        {
            float time=arena.Time;
            // Chip fields and ability zones are never outlined (owner request 2026-10-04): NativeFieldPresentation draws their
            // ground glow, cores and domes, NativeBlastPresentation their volume. The Siren's fields are its scorch decals and
            // eruption warnings (C9) and black flames, eruption columns and rain (C12, ArenaPresentation.DevEffects). Leet fields
            // (nuke field, tree) and storm-step trails are C12's DevFighters. Marked's crosshair on the marked player is the
            // source's camera-facing warn-red reticle over everything (ArenaPresentation.SirenMarks), not a ring of cubes.
            foreach(var p in arena.Pilots)
            {
                if(p.Hp<=0)continue;Vector3 at=p.Body.position;if(time<p.FrozenUntil){Ring(at-Vector3.up*.75f,.65f,8,time*8);for(int j=0;j<6;j++){float a=j*Mathf.PI/3;Vector3 side=new Vector3(Mathf.Sin(a)*.5f,0,Mathf.Cos(a)*.5f);Beam(at+side-Vector3.up*.6f,at+side+Vector3.up*.8f,.055f,8);}}
                if(p.Controlling!=null)Beam(at+Vector3.up*.6f,p.Controlling.Body.position+Vector3.up*.6f,.025f,10);
                if(p.ShieldUntil>time)Ring(at-Vector3.up*.7f,.75f,0,time*90);

            }
            if(arena.MapId=="siren"&&arena.BossSongTime>=0)for(int j=0;j<NativeArena.StageVents.Length;j++)
            {var state=arena.BossTrack.Vent(arena.BossSongTime,j%2);Vector3 at=NativeArena.StageVents[j];if(state.warning>0)Ring(at+Vector3.up*.08f,1.3f,7,time*45);if(state.active){Ring(at+Vector3.up*.04f,1.4f,7,time*25);for(int k=0;k<12;k++){float a=k*2.399f,u=Mathf.Repeat(time*2+k*.13f,1);Vector3 p=at+new Vector3(Mathf.Sin(a)*.7f,u*2.2f,Mathf.Cos(a)*.7f);Add(7,Matrix4x4.TRS(p,Quaternion.Euler(0,45,0),new Vector3(.16f,.4f,.16f)*(1-u)));}}}
            // A minion death draws no burst here: a horde add keeps its corpse (pve/horde-view.ts; gore only on a gib) and a summon
            // breaks up at SUMMON_LOOK.death.breakAt (chaos-visuals.ts), both in SourceMinionDeath.
        }
    }
}

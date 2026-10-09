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
            // ground glow, cores and domes, NativeBlastPresentation their volume. The Siren's patch scorch decals and eruption
            // warning ring/fill are drawn there too; the flames, column and rain below stay until C12 ports siren/fx.ts.
            foreach(var f in arena.SirenFields)
            {
                float age=time-f.At;Vector3 at=f.Position;
                if(f.Kind=="rain")
                {Ring(at+Vector3.up*.025f,f.Radius,7,-age*25);Ring(at+Vector3.up*4.5f,f.Radius,10,age*16);for(int j=0;j<16;j++){float a=j*2.399f,r=f.Radius*Mathf.Sqrt((j+.5f)/16),y=4.5f*(1-Mathf.Repeat(age*.75f+j*.17f,1));Vector3 p=at+new Vector3(Mathf.Sin(a)*r,y,Mathf.Cos(a)*r);Beam(p,p+Vector3.up*.22f,.045f,7);}}
                else if(f.Kind=="eruption")
                {
                    // Stand-in column from the burst (At + 1 = the damage, where the warning ring and fill end) on the field
                    // clock of that warning; nothing during the warning itself.
                    float since=FieldClock(arena)-f.At-(float)NativeFieldRules.EruptionWarning;if(since<0)continue;float rise=Mathf.Clamp01(since*8);
                    for(int j=0;j<12;j++){float a=j*Mathf.PI/6;Vector3 p=at+new Vector3(Mathf.Sin(a)*f.Radius*.6f,0,Mathf.Cos(a)*f.Radius*.6f);Beam(p,p+Vector3.up*(.06f+rise*2.5f),.14f,7);}
                }
                else for(int j=0;j<10;j++){float a=j*2.399f,r=f.Radius*Mathf.Sqrt((j+.5f)/10),u=Mathf.Repeat(age+j*.13f,1);Add(7,Matrix4x4.TRS(at+new Vector3(Mathf.Sin(a)*r,.15f+u*.6f,Mathf.Cos(a)*r),Quaternion.Euler(0,45,0),new Vector3(.11f,.25f*(1-u)+.05f,.11f)));}
            }
            foreach(var f in arena.LeetFields)
            {
                float age=time-f.At;Vector3 at=f.Position;
                if(f.Kind=="tree")
                {Beam(at,at+Vector3.up*3.5f,.3f,3);for(int j=0;j<7;j++){float a=j*2.399f;Vector3 start=at+Vector3.up*(1+j*.28f),end=start+new Vector3(Mathf.Sin(a)*1.3f,.6f,Mathf.Cos(a)*1.3f);Beam(start,end,.11f,3);Beam(end,end+Vector3.up*.6f,.045f,3);}Ring(at+Vector3.up*.03f,1.1f,10,age*13);}
                else{Ring(at,.7f,10,age*120);VoxelCloud(at,Mathf.Repeat(age,1.5f),10,f.Id,12,true);}
            }
            foreach(var mark in arena.Marks()){var target=arena.Pilots.Find(p=>p.Seat.id==mark.target);if(target==null||time<mark.lockAt)continue;float u=Mathf.Clamp01((time-mark.lockAt)/Math.Max(.1f,mark.throwAt-mark.lockAt));Ring(target.Body.position+Vector3.up*.9f,1.6f-.75f*MathUtil.Smooth(u),7,time*60);}
            foreach(var s in arena.StormTrails){float u=Mathf.Clamp01((s.Until-time)/(s.Until-s.At));Ring(s.Position+Vector3.up*.04f,.65f*u,9,time*140);Beam(s.Position,s.Position+Vector3.up*(.25f+u),.035f,9);}
            foreach(var p in arena.Pilots)
            {
                if(p.Hp<=0)continue;Vector3 at=p.Body.position;if(time<p.FrozenUntil){Ring(at-Vector3.up*.75f,.65f,8,time*8);for(int j=0;j<6;j++){float a=j*Mathf.PI/3;Vector3 side=new Vector3(Mathf.Sin(a)*.5f,0,Mathf.Cos(a)*.5f);Beam(at+side-Vector3.up*.6f,at+side+Vector3.up*.8f,.055f,8);}}
                if(p.Controlling!=null)Beam(at+Vector3.up*.6f,p.Controlling.Body.position+Vector3.up*.6f,.025f,10);
                if(p.Siren?.Move?.Rule.Id=="mark"&&p.Siren.Move.Target!=null){Vector3 target=p.Siren.Move.Target.Body.position;Ring(target-Vector3.up*.75f,.7f,7,time*60);Beam(target+Vector3.up*1.1f,target+Vector3.up*1.6f,.07f,7);}
                if(p.ShieldUntil>time)Ring(at-Vector3.up*.7f,.75f,0,time*90);

            }
            if(arena.MapId=="siren"&&arena.BossSongTime>=0)for(int j=0;j<NativeArena.StageVents.Length;j++)
            {var state=arena.BossTrack.Vent(arena.BossSongTime,j%2);Vector3 at=NativeArena.StageVents[j];if(state.warning>0)Ring(at+Vector3.up*.08f,1.3f,7,time*45);if(state.active){Ring(at+Vector3.up*.04f,1.4f,7,time*25);for(int k=0;k<12;k++){float a=k*2.399f,u=Mathf.Repeat(time*2+k*.13f,1);Vector3 p=at+new Vector3(Mathf.Sin(a)*.7f,u*2.2f,Mathf.Cos(a)*.7f);Add(7,Matrix4x4.TRS(p,Quaternion.Euler(0,45,0),new Vector3(.16f,.4f,.16f)*(1-u)));}}}
            foreach(var d in arena.MinionDeaths){float age=time-d.at;if(age>=0&&age<.7f)VoxelCloud(d.position,age,4,d.seed,12);}
        }
    }
}

using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        private void WorldEffects(NativeArena arena)
        {
            float time=arena.Time;
            foreach(var f in arena.ChipFields)
            {
                float age=time-f.Born;Vector3 at=f.Position;int color=f.Kind=="fire"||f.Kind=="bomb"?7:f.Kind=="acid"||f.Kind=="heal"?0:f.Kind=="frost"?8:f.Kind=="storm"?9:10;
                bool bomb=f.Kind=="mine"||f.Kind=="bomb"||f.Kind=="implosion";
                Ring(at+Vector3.up*.06f,f.Radius,color,age*(f.Kind=="time"?8:22));
                if(bomb)
                {float pulse=.15f+.045f*Mathf.Sin(age*25);Add(color,Matrix4x4.TRS(at+Vector3.up*.16f,Quaternion.Euler(0,age*90,0),new Vector3(pulse,.14f,pulse)));float u=Mathf.Clamp01(age/Math.Max(.01f,f.Until-f.Born));Ring(at+Vector3.up*.03f,f.Radius*u,color,0);continue;}
                if(f.Kind=="black-hole"){Add(3,Matrix4x4.TRS(at+Vector3.up*.7f,Quaternion.Euler(age*80,age*120,0),Vector3.one*.5f));for(int j=0;j<16;j++){float u=Mathf.Repeat(age*.7f+j/16f,1),a=j*2.399f+age*3;Vector3 p=at+new Vector3(Mathf.Sin(a)*f.Radius*(1-u),u*.7f,Mathf.Cos(a)*f.Radius*(1-u));Add(color,Matrix4x4.TRS(p,Quaternion.identity,Vector3.one*.09f));}}
                else for(int j=0;j<20;j++)
                {float angle=j*2.399f,r=f.Radius*Mathf.Sqrt((j+.5f)/20),u=Mathf.Repeat(age*(f.Kind=="acid"?1.8f:.6f)+j*.17f,1);Vector3 p=at+new Vector3(Mathf.Sin(angle+age*.3f)*r,f.Kind=="acid"?(1-u)*2:f.Kind=="frost"?.05f:u*.8f,Mathf.Cos(angle+age*.3f)*r);Add(color,Matrix4x4.TRS(p,Quaternion.Euler(0,age*50,j*30),new Vector3(.055f,f.Kind=="acid"?.2f:f.Kind=="fire"?.35f:.08f,.055f)));}
                if(f.Kind=="storm"&&Mathf.Repeat(age,.33f)<.09f)Beam(at+Vector3.up*3,at,.035f,9);
            }
            foreach(var f in arena.SirenFields)
            {
                float age=time-f.At;Vector3 at=f.Position;Ring(at+Vector3.up*.025f,f.Radius,7,-age*25);
                if(f.Kind=="rain")
                {Ring(at+Vector3.up*4.5f,f.Radius,10,age*16);for(int j=0;j<16;j++){float a=j*2.399f,r=f.Radius*Mathf.Sqrt((j+.5f)/16),y=4.5f*(1-Mathf.Repeat(age*.75f+j*.17f,1));Vector3 p=at+new Vector3(Mathf.Sin(a)*r,y,Mathf.Cos(a)*r);Beam(p,p+Vector3.up*.22f,.045f,7);}}
                else if(f.Kind=="eruption")
                {float rise=Mathf.Clamp01((age-1.2f)*8);for(int j=0;j<12;j++){float a=j*Mathf.PI/6;Vector3 p=at+new Vector3(Mathf.Sin(a)*f.Radius*.6f,0,Mathf.Cos(a)*f.Radius*.6f);Beam(p,p+Vector3.up*(.06f+rise*2.5f),.14f,7);}Ring(at,f.Radius*Mathf.Clamp01(age/1.2f),10,age*80);}
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
                if(!FirstPerson||p.Seat.id!=LocalId)LivingWounds(p);
            }
            if(arena.MapId=="siren"&&arena.BossSongTime>=0)for(int j=0;j<NativeArena.StageVents.Length;j++)
            {var state=arena.BossTrack.Vent(arena.BossSongTime,j%2);Vector3 at=NativeArena.StageVents[j];if(state.warning>0)Ring(at+Vector3.up*.08f,1.3f,7,time*45);if(state.active){Ring(at+Vector3.up*.04f,1.4f,7,time*25);for(int k=0;k<12;k++){float a=k*2.399f,u=Mathf.Repeat(time*2+k*.13f,1);Vector3 p=at+new Vector3(Mathf.Sin(a)*.7f,u*2.2f,Mathf.Cos(a)*.7f);Add(7,Matrix4x4.TRS(p,Quaternion.Euler(0,45,0),new Vector3(.16f,.4f,.16f)*(1-u)));}}}
            foreach(var d in arena.MinionDeaths){float age=time-d.at;if(age>=0&&age<.7f)VoxelCloud(d.position,age,4,d.seed,12);}
        }
        private void LivingWounds(NativePilot pilot)
        {
            if(!rigs.TryGetValue(pilot.Seat.id,out var rig))return;
            foreach(var w in pilot.Wounds)
            {
                if(w.part<0||w.part>=22)continue;Transform bone=rig.Bones[w.part];Vector3 p=bone.position;Vector3 dir=NativeArena.Vec(w.direction);if(dir.sqrMagnitude<.001f)dir=-bone.forward;else dir.Normalize();p-=dir*.12f;
                int color=w.kind=="scorch"?3:w.kind=="bruise"?5:2;Add(color,Matrix4x4.TRS(p,Quaternion.LookRotation(dir),new Vector3(w.radius*1.5f,w.radius*.8f,.02f)));
            }
        }
    }
}

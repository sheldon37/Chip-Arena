using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using ChipArena.UnityConversion;
public static class BackroomsBossRuleChecks
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("Backrooms boss authority: "+why);}
        var output=new List<BackroomsBossContact>();
        foreach(string id in new[]{NativeHumBoss.Archetype,NativeDeepEndBoss.Archetype})
        {
            bool hum=id==NativeHumBoss.Archetype;var z=NativeBackroomsBossRules.Create(id,10001,40,40,0);var rig=new DeepEndBossRig();
            Check(z.Valid(),"new record "+id);Check(z.duration==9.0678f,"song hold");
            var p=new BackroomsBossPilot{Seat=0,Feet=new Vector3(45,0,45)};
            NativeBackroomsBossRules.Step(z,640,1000,1,1f/60,new[]{p},output,rig);Check(z.phase==2&&z.action=="phase","65% transition");
            NativeBackroomsBossRules.Step(z,290,1000,5,1f/60,new[]{p},output,rig);Check(z.phase==3,"30% transition");
            Check(z.Valid(),"phase state valid");var copy=z.Copy();copy.backrooms.ready[0]=999;Check(z.backrooms.ready[0]!=999,"copy cooldown isolation");copy.partAt[0]=9;Check(z.partAt[0]==-100,"copy parts isolation");
            z=NativeBackroomsBossRules.Create(id,10001,40,40,0);z.action=null;
            NativeBackroomsBossRules.Start(z,hum?"stoop":"breach",p,10);float hit=z.hitAt;
            var at=hum?new Vector3(z.laneX0,0,z.laneZ0):p.Feet;p.Feet=at;
            NativeBackroomsBossRules.Step(z,1000,1000,hit-.01f,1f/60,new[]{p},output,rig);Check(!output.Any(h=>h.Damage>0),"windup harmless");
            NativeBackroomsBossRules.Step(z,1000,1000,hit+.01f,1f/60,new[]{p},output,rig);Check(output.Count(h=>h.Damage>0)==1,"contact hits once");
            NativeBackroomsBossRules.Step(z,1000,1000,hit+.02f,1f/60,new[]{p},output,rig);Check(!output.Any(h=>h.Damage>0),"single swing cannot double hit");
            // Real scheduler, every 2m grid cell, all phases. Moving in the 3m radius must not erase stillness.
            for(int phase=1;phase<=3;phase++)for(int x=-18;x<=18;x+=2)for(int zz=-18;zz<=18;zz+=2)
            {
                z=NativeBackroomsBossRules.Create(id,10001,40,40,0);z.phase=phase;z.action=null;z.phaseAt=-100;rig.Clear();bool struck=false;
                for(int tick=1;tick<=(phase==3?390:510)&&!struck;tick++){float t=tick/60f;p.Feet=new Vector3(40+x+.2f*(float)Math.Sin(t),0,40+zz);NativeBackroomsBossRules.Step(z,phase==1?1000:phase==2?600:200,1000,t,1f/60,new[]{p},output,rig);struck=output.Any(h=>h.Seat==p.Seat&&h.Damage>0);}
                Check(struck,id+" still grid "+phase+" "+x+","+zz);
            }
            // Continuous circles exceed the stillness radius. Count actual contacts, currents and locally spawned adds as threats.
            for(int phase=1;phase<=3;phase++)for(int gx=-12;gx<=12;gx+=4)for(int gz=-12;gz<=12;gz+=4)
            {
                z=NativeBackroomsBossRules.Create(id,10001,40,40,0);z.phase=phase;z.action=null;z.phaseAt=-100;rig.Clear();bool threatened=false;
                for(int tick=1;tick<=1200&&!threatened;tick++){float t=tick/30f;p.Feet=new Vector3(40+gx+5.2f*(float)Math.Sin(t*.6f),0,40+gz+5.2f*(float)Math.Cos(t*.6f));NativeBackroomsBossRules.Step(z,phase==1?1000:phase==2?600:200,1000,t,1f/30,new[]{p},output,rig);threatened=output.Any(h=>h.Damage>0||h.Pull>0||h.Add!=null);}
                Check(threatened,id+" moving grid "+phase+" "+gx+","+gz);
            }
            z=NativeBackroomsBossRules.Create(id,10001,40,40,0);z.action=null;rig.Clear();p.Feet=new Vector3(200,0,200);NativeBackroomsBossRules.Step(z,1000,1000,1,1f/60,new[]{p},output,rig);Check(output.Any(h=>h.Return),"outside returned");
            if(!hum){z.phase=2;z.backrooms.tideY=.5f;z.backrooms.tideAt=1;z.backrooms.tideUntil=4.5f;Check(Math.Abs(NativeBackroomsBossRules.Tide(z,4.5f)-.5f)<.001f,"tide finishes");}
        }
        PoolroomsRulesChecks.Run();
        Console.WriteLine("PASS Backrooms boss production rules: "+n+" assertions");
    }
}

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
        // Match the native fixture: retain one boss/rig across successive locations,
        // and sample collision pose after every rule tick with no song clock.
        foreach(string id in new[]{NativeHumBoss.Archetype,NativeDeepEndBoss.Archetype})
        {
            bool hum=id==NativeHumBoss.Archetype;float now=300,half=hum?NativeHumArena.Size/2:20;
            var z=NativeBackroomsBossRules.Create(id,10001,40,40,now);var b=z.backrooms;var rig=new DeepEndBossRig();z.action=null;
            float arrival=NativeZoneBossViews.BackroomsArrival(z);
            var spots=new[]{new Vector3(-half+1,0,-half+1),new Vector3(half-1,0,-half+1),new Vector3(-half+1,0,half-1),new Vector3(half-1,0,half-1),new Vector3(0,0,-half+1),new Vector3(half-1,0,0),new Vector3(0,0,half-1),new Vector3(-half+1,0,0),new Vector3(0,0,-14)};
            foreach(var spot in spots)
            {
                var p=new BackroomsBossPilot{Seat=1,Feet=new Vector3(40,0,40)+spot};float began=now;
                b.next=now;b.meters=Array.Empty<NativeBossMeter>();b.hits=Array.Empty<int>();z.action=null;z.cancelAt=-100;bool hit=false;
                for(int tick=1;tick<=510&&!hit;tick++)
                {
                    now=began+tick/60f;NativeBackroomsBossRules.Step(z,6000,6000,now,1f/60,new[]{p},output,rig);
                    if(!hum){var view=NativeZoneBossViews.DeepEnd(z,b.x,rig.Root.Y,b.z,b.yaw,6000,6000,NativeZoneBossViews.BackroomsArrival(z));rig.Update(now,1f/60,view,null,new Vector3(b.cx,0,b.cz),NativeBackroomsBossRules.Tide(z,now),false,Vector3.Zero);}
                    hit=output.Any(h=>h.Seat==p.Seat&&h.Damage>0);
                }
                Check(NativeZoneBossViews.BackroomsArrival(z)==arrival,"arrival remains fixed across collision samples");
                Check(hit,id+" sequential native collision-pose deadline "+spot);
            }
        }
        // The owner's pass, 2026-11-07: Noclip faster, aimed through its wind and then held; NOCLIP's room taller; THE DEEP END's 12 m room; the Drain a current a run beats.
        {
            var lunge=NativeHumBoss.Move("lunge");Check(lunge.Wind<=.75f&&lunge.Strike<=.8f&&Math.Abs(lunge.Speed-lunge.Reach/lunge.Strike)<.6f,"Noclip faster, its speed its reach over its strike");
            var z=NativeBackroomsBossRules.Create(NativeHumBoss.Archetype,10001,40,40,0);z.action=null;var p=new BackroomsBossPilot{Seat=3,Feet=new Vector3(40,0,30)};
            NativeBackroomsBossRules.Start(z,"lunge",p,10);float x1=z.laneX1,z1=z.laneZ1;Check(Math.Abs(z.hitAt-10-lunge.Wind)<.001f,"its wind from the table");
            p.Feet=new Vector3(48,0,32);NativeBackroomsBossRules.Step(z,1000,1000,10.2f,1f/60,new[]{p},output);
            var dir=Vector3.Normalize(new Vector3(z.laneX1-z.laneX0,0,z.laneZ1-z.laneZ0));var want=Vector3.Normalize(new Vector3(p.Feet.X-z.laneX0,0,p.Feet.Z-z.laneZ0));
            Check(Vector3.Dot(dir,want)>.999f&&(z.laneX1!=x1||z.laneZ1!=z1)&&z.targetX==48,"Noclip follows its target through the wind");
            float lx=z.laneX1,lz=z.laneZ1;p.Feet=new Vector3(30,0,34);NativeBackroomsBossRules.Step(z,1000,1000,z.hitAt-NativeHumBoss.LungeLock+.05f,1f/60,new[]{p},output);Check(z.laneX1==lx&&z.laneZ1==lz,"and holds its line before it goes");
            Check(NativeBackroomsBossRules.Ceiling(z)==BackroomsLayout.Height&&!NativeBackroomsBossRules.Inside(z,new Vector3(40,5,40)),"the level's ceiling until NOCLIP");
            z.phase=3;Check(NativeBackroomsBossRules.Ceiling(z)==NativeHumBoss.HighCeiling&&NativeBackroomsBossRules.Inside(z,new Vector3(40,5,40)),"NOCLIP's room is taller");
            var d=NativeBackroomsBossRules.Create(NativeDeepEndBoss.Archetype,10001,40,40,0);Check(NativeBackroomsBossRules.Ceiling(d)==NativeDeepEndArena.Ceiling&&NativeBackroomsBossRules.Inside(d,new Vector3(40,10,40)),"THE DEEP END's 12 m room");
            d.phase=3;d.action=null;var rig=new DeepEndBossRig();var q=new BackroomsBossPilot{Seat=4,Feet=new Vector3(40,0,52)};NativeBackroomsBossRules.Start(d,"drain",q,10);
            NativeBackroomsBossRules.Step(d,200,1000,d.hitAt+.1f,1f/60,new[]{q},output,rig);float pull=output.Where(h=>h.Seat==4&&h.Pull>0).Select(h=>h.Pull).DefaultIfEmpty(0).Max();
            Check(pull>=NativeDeepEndBoss.DrainPullMin&&pull<=NativeDeepEndBoss.DrainPullMax&&NativeDeepEndBoss.DrainPullMax<6.8f,"the Drain is a current a run beats ("+pull+")");
            Check(NativeDeepEndBoss.DrainThroatShare>0&&NativeDeepEndBoss.DrainThroatShare<=.05f&&NativeDeepEndBoss.DrainStagger>0,"the throat shuts it");
            Check(NativeDeepEndBoss.DrainPull(2.6f)>3.5f&&NativeDeepEndBoss.DrainPull(10)>NativeDeepEndBoss.DrainPull(20)*1.8f&&NativeDeepEndBoss.DrainPull(27)==NativeDeepEndBoss.DrainPullMin&&NativeDeepEndBoss.DrainPull(0)==NativeDeepEndBoss.DrainPullMax,"weaker the further away (the owner)");
            // Carried by the current toward the pit without fighting it is still standing still: the Lull still finds them inside the build's deadline (8.5 s).
            {
                var e=NativeBackroomsBossRules.Create(NativeDeepEndBoss.Archetype,10001,40,40,0);e.phase=1;e.action=null;var er=new DeepEndBossRig();var c=new BackroomsBossPilot{Seat=5,Feet=new Vector3(21,0,59)};
                NativeBackroomsBossRules.Start(e,"drain",c,1);bool lulled=false;
                for(float t=1;t<=9.5f&&!lulled;t+=1f/60)
                {
                    NativeBackroomsBossRules.Step(e,6000,6000,t,1f/60,new[]{c},output,er);lulled|=e.action=="lull";
                    float carry=output.Where(h=>h.Seat==5&&h.Pull>0).Select(h=>h.Pull).DefaultIfEmpty(0).Max();c.Feet+=Vector3.Normalize(new Vector3(40,0,40)-c.Feet)*carry/60;
                }
                Check(lulled,"a pilot the Drain carries still gets the Lull");
            }
        }
        PoolroomsRulesChecks.Run();
        Console.WriteLine("PASS Backrooms boss production rules: "+n+" assertions");
    }
}

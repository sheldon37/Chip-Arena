#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class HordeDropChecks
    {
        public static void Run(string root)
        {
            int count=0;void Check(bool ok,string why){if(!ok)throw new Exception("Horde drop: "+why);count++;}
            bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
            var data=JsonSerializer.Deserialize<NativePveContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")),new JsonSerializerOptions{IncludeFields=true});
            var drop=data.entries.Single(NativeHordeDrop.Is);var door=data.entries.First(e=>e.kind=="door");
            Check(drop.id=="drop-centre"&&drop.height==6.5f,"authored landing entry and height");
            Check(NativeEntryRules.ValidIndex(drop.e,drop.kind)&&!NativeEntryRules.ValidIndex(16,"door")&&!NativeEntryRules.ValidIndex(-1,"drop")&&!NativeEntryRules.ValidIndex(256,"drop"),"network accepts authored drop beyond legacy indices, retains bounds");
            Check(NativeHordeDrop.HeightOf(new PveEntry())==6.5f&&NativeHordeDrop.HeightOf(new PveEntry{height=9})==9,"default and custom height");
            foreach(float h in new[]{0f,3f,6.5f,12f})foreach(bool first in new[]{false,true})
            {
                var e=new PveEntry{kind="drop",height=h};var timeline=NativeEntryRules.Timeline(e,first);float active=10+timeline.duration,land=NativeHordeDrop.LandAt(active);
                Check(timeline.walk==0&&Near(timeline.duration,NativeHordeDrop.Duration(h==0?6.5f:h)),"both queued and first drops use height duration");
                Check(Near(active-land,.9f)&&NativeHordeDropRules.Held(e,land-.001f,active)&&!NativeHordeDropRules.Held(e,land,active),"hold ends exactly at landing");
                Check(Near(NativeHordeDropRules.Exposed(e,10,active),land)&&Near(NativeHordeDropRules.TargetAt(e,active),land),"damage and locks open together");
            }
            Check(Near(NativeHordeDrop.Duration(6.5f),2.705535f),"6.5 m duration");
            Check(!NativeHordeDropRules.Held(door,0,10)&&NativeHordeDropRules.TargetAt(door,10)==10,"legacy entry behavior");
            Check(NativeHordeDropRules.UsesDrop("brute",data.entries)&&!NativeHordeDropRules.UsesDrop("runner",data.entries)&&!NativeHordeDropRules.UsesDrop("brute",new[]{door}),"brute-only drops and fallback without them");
            var second=new PveEntry{e=17,kind="drop"};var entries=new[]{door,drop,second};int cursor=0;
            Check(NativeHordeDropRules.Next(entries,ref cursor,e=>true)==drop&&NativeHordeDropRules.Next(entries,ref cursor,e=>true)==second&&NativeHordeDropRules.Next(entries,ref cursor,e=>true)==drop,"round robin excludes legacy");
            int before=cursor;Check(NativeHordeDropRules.Next(entries,ref cursor,e=>false)==null&&cursor==before,"full queues do not fall back or rotate");
            Check(NativeHordeDropRules.Next(entries,ref cursor,e=>e==drop)==drop,"skip unavailable drop");
            Check(NativeHordeDropRules.Touch(new V3(2.4f,1.49f,0))&&!NativeHordeDropRules.Touch(new V3(2.401f,0,0))&&!NativeHordeDropRules.Touch(new V3(0,1.5f,0))&&!NativeHordeDropRules.Touch(new V3(0,-1.5f,0)),"disc radius and strict vertical bound");
            Check(NativeHordeDropRules.Damage(0,false)==10&&NativeHordeDropRules.Damage(1.2f,false)==7.5f&&NativeHordeDropRules.Damage(2.4f,false)==5&&NativeHordeDropRules.Damage(0,true)==0,"falloff and block");
            var full=NativeHordeDropRules.Shove(new V3(1,0,0),new V3(0,0,1),380,false);var half=NativeHordeDropRules.Shove(new V3(1,0,0),new V3(0,0,1),380,true);
            Check(Near(full.x,380f/65)&&full.y==2.5f&&Near(half.x,full.x*.5f)&&half.y==1.25f,"drop shove and half blocked shove");
            var smash=NativeHordeDropRules.Shove(new V3(0,0,-.2f),new V3(0,0,1),420,false);
            Check(Near(smash.z,-420f/65)&&smash.y==2.5f,"smash pushes out from disc even on the near side");
            Check(NativeHordeDropRules.Shove(new V3(),new V3(0,0,1),420,false).z>0,"exact centre has deterministic direction");
            var brute=data.adds.Single(a=>a.id=="brute");Check(brute.impulse==420&&brute.hit==.43f&&NativeHordeKinds.SmashRadius==1.6f&&NativeHordeKinds.SmashAhead==1.2f,"smash content and unchanged geometry/timing");
            Console.WriteLine("PASS horde drop rules ("+count+" checks: height timeline, hold/exposure, round robin/fallback, radius/falloff/block, outward smash shove)");
        }
    }
}
#endif

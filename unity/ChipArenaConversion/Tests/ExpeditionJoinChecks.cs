using System;
using System.IO;
using System.Linq;
namespace ChipArena.UnityConversion
{
    internal static class ExpeditionJoinChecks
    {
        private static int count;
        private static void Check(bool value,string message){if(!value)throw new Exception("Join rule: "+message);count++;}
        internal static void Run(string root)
        {
            count=0;var layout=NativeExpeditionLayout.Parse(File.ReadAllText(Path.Combine(root,ExpeditionJoinDraftChecks.DraftPath)));
            var rules=new ExpeditionJoinRules(layout);var a=new ExpeditionJoinMember{Account="a",Area=layout.world,Feet=new V3(79,0,0),Alive=true,Connected=true};
            var b=new ExpeditionJoinMember{Account="b",Area=layout.world,Feet=new V3(90,0,0),Alive=true,Connected=true};
            string[] moved=null;int calls=0;
            bool Transfer(string door,string area,string[] cohort){calls++;moved=cohort;return true;}
            void Step(double t)=>rules.Step(t,new[]{a,b},_=>true,Transfer);
            Step(0);Check(rules.Views.Single().joinStartedAt==0&&rules.AtJoin("a")&&!rules.AtJoin("b"),"one starts; outside stays");
            Step(2.99);Check(calls==0,"full three seconds required");
            a.Feet=new V3(90,0,0);b.Feet=new V3(79,2.84f,0);Step(3);
            Check(calls==1&&moved.SequenceEqual(new[]{"b"}),"expiry captures current airborne member only");
            Check(!rules.AtJoin("b")&&rules.Views.Single().joinStartedAt==-1,"accepted transfer resets ring");
            Step(3);Check(calls==1,"same host tick cannot deploy twice");
            rules.Reset();b.Feet=new V3(90,0,0);a.Feet=new V3(79,0,0);Step(4);a.Feet=new V3(90,0,0);Step(5);
            Check(rules.Views.Single().joinStartedAt==-1,"empty cancels");a.Feet=new V3(79,0,0);Step(6);Step(8.99);Check(calls==1,"re-entry gets full countdown");Step(9);Check(calls==2,"re-entry expires");
            rules.Reset();a.Feet=new V3(81.5f,-.85f,0);Step(10);Check(rules.AtJoin("a"),"radius and capsule-centre base inclusive");
            a.Feet=new V3(81.51f,0,0);Step(11);Check(!rules.AtJoin("a"),"outside radius excluded");
            a.Feet=new V3(79,3.15f,0);Step(12);Check(rules.AtJoin("a"),"top inclusive");
            a.Feet=new V3(79,3.16f,0);Step(13);Check(!rules.AtJoin("a"),"above top excluded");
            a.Feet=new V3(79,4.4f,0);Step(14);Check(!rules.AtJoin("a"),"canopy excluded");
            a.Feet=new V3(79,0,0);a.Loading=true;Step(15);Check(!rules.AtJoin("a"),"loading excluded");a.Loading=false;
            a.Carrying=true;Step(16);Check(!rules.AtJoin("a"),"carrying excluded");a.Carrying=false;
            a.Choosing=true;Step(17);Check(!rules.AtJoin("a"),"choosing excluded");a.Choosing=false;
            a.Connected=false;Step(18);Check(!rules.AtJoin("a"),"disconnected excluded");a.Connected=true;
            a.Alive=false;Step(19);Check(!rules.AtJoin("a"),"dead excluded");a.Alive=true;
            a.Area=layout.instances[0];Step(20);Check(!rules.AtJoin("a"),"other area excluded");a.Area=layout.world;
            rules.Step(21,new[]{a,b},_=>false,Transfer);Check(!rules.AtJoin("a"),"closed destination excluded");
            rules.Reset();int refused=0;bool Refuse(string d,string area,string[] c){refused++;return false;}
            foreach(double t in new[]{0d,3,3.1,6,6.1})rules.Step(t,new[]{a},_=>true,Refuse);
            Check(refused==2,"refusal retries only after fresh countdown");
            var snapshot=rules.Views;snapshot[0].joinInside=42;Check(rules.Views[0].joinInside==1,"view detached");
            bool stale=false;try{rules.Step(5,new[]{a},_=>true,Transfer);}catch(ArgumentException){stale=true;}Check(stale,"backward clock rejected");
            var world=layout.Area(layout.world);var original=world.doors.Single();var anchor=world.anchors.Single(x=>x.id==original.anchor);
            world.doors=world.doors.Concat(new[]{new ExpeditionDoorLayout{anchor="aaa-door",join=original.join}}).ToArray();
            world.anchors=world.anchors.Concat(new[]{new ExpeditionWorldAnchor{id="aaa-door",kind="door",building=anchor.building}}).ToArray();
            rules=new ExpeditionJoinRules(layout);Step(0);Check(rules.DoorFor("a")=="aaa-door"&&rules.Views.Sum(x=>x.joinInside)==1,"overlapping rings choose one ordinal door");
            Step(3);Check(moved.SequenceEqual(new[]{"a"}),"overlapping rings transfer once");
            Console.WriteLine("PASS Expedition join rules: "+count+" assertions");
        }
    }
}

using System;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;
// Hotdog Friends, the DEV kill effect (owner 2026-11-09: "Tiny little hotdogs appear from a tiny door (near whereever the body parts
// land/stop) and take all the body parts through the door. (Have them move accordingly so it fits the body parts seamlessly)"): the
// door's moments, each load's trip and its carriers, the scene inside the body's five seconds, the models, the DEV gate and the wiring.
internal static class HotdogFriendsChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Hotdog Friends: "+why);n++;}
        // The door: under the floor until the body rests, up and open, shut after the last friend, then gone.
        Check(!NativeHotdogFriends.Door(NativeHotdogFriends.Settle-.01f,3,out _,out _),"no door before the body has rested");
        Check(NativeHotdogFriends.Door(NativeHotdogFriends.Settle+NativeHotdogFriends.DoorUp*.5f,3,out float half,out float shut)&&half>0&&half<1&&shut==0,"it rises shut");
        Check(NativeHotdogFriends.Door(NativeHotdogFriends.Opened+.01f,3,out float up,out float open)&&up==1&&open>.99f,"then stands open");
        Check(!NativeHotdogFriends.Door(NativeHotdogFriends.Over(3)+.01f,3,out _,out _),"and is gone once it has shut and sunk");
        // A body of six pieces 0.4 to 2.4 m from the door: every trip in order, each friend out and back in, and the whole inside five seconds.
        var mouth=new Vector3(0,0,0);float last=0;
        for(int i=0;i<6;i++)
        {
            float size=.15f+.12f*i;var rest=new Vector3(.4f*i+.4f,.12f,.3f);var trip=NativeHotdogFriends.Plan(i,Vector3.Distance(rest,mouth),0);int friends=NativeHotdogFriends.Friends(size);
            Check(trip.Leave>=NativeHotdogFriends.Opened&&trip.Arrive>trip.Leave&&trip.Lifted>trip.Arrive&&trip.Back>trip.Lifted&&trip.In>trip.Back,"load "+i+": out, lifted, back, in");
            Check(friends>=2&&friends<=NativeHotdogFriends.MostFriends&&(i==0||friends>=NativeHotdogFriends.Friends(size-.12f)),"load "+i+": more friends for a bigger piece");
            NativeHotdogFriends.Load(trip,rest,mouth,trip.Arrive-.01f,out var still,out float s0,out _);Check(still==rest&&s0==1,"load "+i+" lies where it came to rest until they reach it");
            NativeHotdogFriends.Load(trip,rest,mouth,(trip.Lifted+trip.Back)*.5f,out var carried,out float s1,out _);Check(carried.Y>rest.Y+NativeHotdogFriends.LiftHeight*.9f&&s1==1&&Vector3.Distance(carried,mouth)<Vector3.Distance(rest,mouth),"load "+i+" is held up and carried toward the door");
            NativeHotdogFriends.Load(trip,rest,mouth,trip.In,out _,out float s2,out _);Check(s2<1e-3f,"load "+i+" is through the door");
            for(int j=0;j<friends;j++)
            {
                Check(!NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,size,trip.Leave-.01f,out _,out _,out _,out _,out _),"friend "+j+" is not out before its load's file");
                Check(NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,size,(trip.Lifted+trip.Back)*.5f,out var feet,out var facing,out _,out float hold,out float scale)&&hold==1&&scale==1&&Math.Abs(facing.Length()-1)<1e-3f&&feet.Y==0,"friend "+j+" carries it, arms up, on the floor");
                NativeHotdogFriends.Load(trip,rest,mouth,(trip.Lifted+trip.Back)*.5f,out var at,out _,out _);var flat=new Vector2(feet.X-at.X,feet.Z-at.Z).Length();
                Check(flat<=Math.Max(.1f,size*.4f)+1e-3f,"friend "+j+" stays round its load's edge as it goes ("+flat.ToString("0.00")+" m)");
                Check(!NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,size,NativeHotdogFriends.InAt(trip,j)+.01f,out _,out _,out _,out _,out _),"friend "+j+" goes in after its load");
                last=Math.Max(last,NativeHotdogFriends.InAt(trip,j));
            }
        }
        Check(NativeHotdogFriends.Over(last)<(float)NativeKillEffectRules.DeathSeconds,"the whole scene ends inside the body's "+NativeKillEffectRules.DeathSeconds+" s ("+NativeHotdogFriends.Over(last).ToString("0.00")+" s)");
        // The door's spots ring the pieces, facing them.
        for(int k=0;k<8;k++){var spot=NativeHotdogFriends.DoorSpot(new Vector3(1,0,2),.6f,77,k,out float yaw);var d=new Vector3(1,0,2)-spot;float face=(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);Check(Math.Abs(Vector3.Distance(spot,new Vector3(1,0,2))-(.6f+NativeHotdogFriends.DoorAway))<1e-3f&&Math.Abs(((face-yaw)%360+540)%360-180)<.1f,"door spot "+k+" stands past the pieces, its mouth to them");}
        // The models build, the walk frames differ, and the stride cycles.
        var a=NativeHotdogFriends.Friend(0,0);var b=NativeHotdogFriends.Friend(NativeHotdogFriends.WalkFrames/2,1);var door=NativeHotdogFriends.DoorFrame();var leaf=NativeHotdogFriends.DoorLeaf();
        Check(a.Count>100&&b.Count>100&&door.Count>50&&leaf.Count>20,"the friend, the door and its leaf build");
        float top=0;for(int i=0;i<a.P.Count;i++)top=Math.Max(top,a.P[i].Y);Check(top>.2f&&top<.36f,"a friend stands about "+NativeHotdogFriends.Height+" m ("+top.ToString("0.00")+")");
        Check(NativeHotdogFriends.Frame(0)==0&&NativeHotdogFriends.Frame(2*NativeHotdogFriends.Stride)==0&&NativeHotdogFriends.Frame(NativeHotdogFriends.Stride)==NativeHotdogFriends.WalkFrames/2,"the stride cycles every two steps");
        // The DEV gate, the lists and the wiring.
        Check(NativeCosmeticGate.DevOnly(NativeHotdogFriends.Style)&&NativeCosmeticGate.Restricted(NativeHotdogFriends.Style)&&!NativeCosmeticGate.Allowed(NativeAccess.Guest,"effect",NativeHotdogFriends.Style,0),"DEV only: a guest cannot pick it");
        Check(NativeLocker.KillText(NativeHotdogFriends.Style)==NativeHotdogFriends.Text&&NativeBisect.Applies(true,NativeHotdogFriends.Style,false,"pilot"),"its locker line; a blade still cuts its body as the stock ragdoll");
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        Check(R("NativeProfileUI.cs").Contains("\"dev-bass-van\",NativeHotdogFriends.Style};")&&R("NativeAdmin.cs").Contains("\"effect:\"+NativeHotdogFriends.Style};"),"listed in the locker and grantable");
        var death=R("NativeDeathPresentation.cs");
        Check(death.Contains("death.style==NativeHotdogFriends.Style?\"impact-ragdoll\":death.style;")&&death.Contains("if(friends&&!remains)HotdogFriends(corpse,fx);")&&death.Contains("stock&&!retain&&!friends?"),"its body is the stock ragdoll, carried off instead of sinking");
        Check(R("NativeBloodPresentation.cs").Contains("HotdogFriendsFx(now);"),"the door's puffs are drawn");
        var pres=R("NativeHotdogFriendsPresentation.cs");Check(!pres.Contains("Damage(")&&!pres.Contains("SendInput"),"presentation only");
        Console.WriteLine("PASS hotdog friends: "+n+" checks (the door, six loads' trips and carriers inside "+NativeKillEffectRules.DeathSeconds+" s, the models, the DEV gate, the wiring)");
    }
}

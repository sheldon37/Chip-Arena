using System;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;
// Hotdog Friends, the DEV kill effect (owner 2026-11-09: "Tiny little hotdogs appear from a tiny door (near whereever the body parts
// land/stop) and take all the body parts through the door. (Have them move accordingly so it fits the body parts seamlessly)"; then "some
// hotdogs pickup nothing, fix this ... We want comical hotdog runs and running away into the door. (They also dont bring things in the
// door, they kind of just leave them out)"): the door's moments and its slam, the friends' spots under the pieces, each load's trip and
// each friend's dash, skid, heave, waddle, dash home and dive (the last one's wave), the scene inside the body's five seconds, the models,
// the DEV gate and the wiring (the pieces through the door shrink on the model too).
internal static class HotdogFriendsChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Hotdog Friends: "+why);n++;}
        float Flat(Vector3 a,Vector3 b)=>new Vector2(a.X-b.X,a.Z-b.Z).Length();
        // The door: under the floor until the body rests, up and open, slammed after the last friend, bouncing a crack, then gone.
        Check(!NativeHotdogFriends.Door(NativeHotdogFriends.Settle-.01f,3,out _,out _),"no door before the body has rested");
        Check(NativeHotdogFriends.Door(NativeHotdogFriends.Settle+NativeHotdogFriends.DoorUp*.5f,3,out float half,out float shut)&&half>0&&half<1&&shut==0,"it rises shut");
        Check(NativeHotdogFriends.Door(NativeHotdogFriends.Opened+.01f,3,out float up,out float open)&&up==1&&open>.99f,"then stands open");
        float shutAt=NativeHotdogFriends.ShutAt(3);
        Check(NativeHotdogFriends.Door(shutAt+NativeHotdogFriends.DoorShut*.5f,3,out _,out float slamming)&&slamming>0&&slamming<.9f,"slammed behind the last friend");
        Check(NativeHotdogFriends.Door(shutAt+NativeHotdogFriends.DoorShut+NativeHotdogFriends.DoorBounce*.5f,3,out _,out float bounce)&&bounce>.02f&&bounce<.15f,"it bounces open a crack");
        Check(!NativeHotdogFriends.Door(NativeHotdogFriends.Over(3)+.01f,3,out _,out _),"and is gone once it has shut and sunk");
        // The spots: every friend under one of its load's own pieces (a long body: along its length, to both sides).
        var dir=new Vector3(0,0,1);
        var pieces=new[]{new Vector3(0,0,-.6f),new Vector3(0,0,-.2f),new Vector3(.1f,0,.2f),new Vector3(0,0,.6f),new Vector3(.3f,0,0)};var widths=new[]{.12f,.15f,.1f,.08f,.05f};
        for(int count=2;count<=NativeHotdogFriends.MostFriends;count++)
        {
            var slots=NativeHotdogFriends.Slots(pieces,widths,dir,count);Check(slots.Length==count,count+" spots for "+count+" friends");
            for(int j=0;j<count;j++){float near=float.MaxValue;foreach(var p in pieces)near=Math.Min(near,Flat(slots[j],p));Check(near<=.17f,"friend "+j+" of "+count+" is under a piece ("+near.ToString("0.00")+" m)");}
            for(int a=0;a<count;a++)for(int b=a+1;b<count;b++)Check(Flat(slots[a],slots[b])>.04f,"friends "+a+" and "+b+" of "+count+" stand apart");
        }
        var lone=NativeHotdogFriends.Slots(new[]{new Vector3(.2f,0,.1f)},new[]{.04f},dir,3);foreach(var s in lone)Check(Flat(s,new Vector3(.2f,0,.1f))<=.17f,"a lone hand's three friends all under it");
        Check(Flat(lone[0],lone[1])>.04f&&Flat(lone[1],lone[2])>.04f&&Flat(lone[0],lone[2])>.04f,"round it, not on top of each other");
        // Six loads 0.4 to 2.4 m from the door: every trip in order, each friend's run, skid, heave, waddle, run home and dive.
        var mouth=new Vector3(0,0,0);float last=0;
        for(int i=0;i<6;i++)
        {
            float size=.15f+.12f*i;var rest=new Vector3(.4f*i+.4f,.12f,.3f);int friends=NativeHotdogFriends.Friends(size);var trip=NativeHotdogFriends.Plan(i,6,Vector3.Distance(rest,mouth),friends,0);
            var toDoor=mouth-rest;toDoor.Y=0;var slots=NativeHotdogFriends.Slots(new[]{new Vector3(-size*.3f,0,0),Vector3.Zero,new Vector3(size*.3f,0,0)},new[]{size*.2f,size*.2f,size*.2f},toDoor,friends);
            Check(trip.Leave>=NativeHotdogFriends.Opened&&trip.Arrive>trip.Leave&&trip.Lifted>trip.Arrive&&trip.Back>trip.Lifted&&trip.In>trip.Back,"load "+i+": out, lifted, back, in");
            Check(friends>=2&&friends<=NativeHotdogFriends.MostFriends&&(i==0||friends>=NativeHotdogFriends.Friends(size-.12f)),"load "+i+": more friends for a bigger piece");
            NativeHotdogFriends.Load(trip,rest,mouth,trip.Arrive-.01f,out var still,out float s0,out _);Check(still==rest&&s0==1,"load "+i+" lies where it came to rest until they reach it");
            NativeHotdogFriends.Load(trip,rest,mouth,trip.Arrive+NativeHotdogFriends.Crouch*.5f,out var crouched,out _,out _);Check(crouched==rest,"load "+i+" stays down while they crouch under it");
            NativeHotdogFriends.Load(trip,rest,mouth,trip.Lifted,out var heaved,out _,out _);Check(Math.Abs(heaved.Y-rest.Y-NativeHotdogFriends.LiftHeight)<.01f,"load "+i+" heaved up overhead");
            NativeHotdogFriends.Load(trip,rest,mouth,(trip.Lifted+trip.Back)*.5f,out var carried,out float s1,out _);Check(carried.Y>rest.Y+NativeHotdogFriends.LiftHeight*.9f&&s1==1&&Vector3.Distance(carried,mouth)<Vector3.Distance(rest,mouth),"load "+i+" is held up and carried toward the door");
            NativeHotdogFriends.Load(trip,rest,mouth,(trip.Back+trip.In)*.5f,out _,out float s15,out _);Check(s15>0&&s15<1,"load "+i+" shrinks as it is stuffed through");
            NativeHotdogFriends.Load(trip,rest,mouth,trip.In,out _,out float s2,out _);Check(s2<1e-3f,"load "+i+" is through the door");
            for(int j=0;j<friends;j++)
            {
                const bool waves=false;var slot=slots[j];float inAt=NativeHotdogFriends.InAt(trip,j,slot,waves);
                Check(!NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,slot,waves,trip.Leave-.01f,out _),"friend "+j+" is not out before its load's file");
                // The dash: leaning into it, bouncing, squashing and stretching; then the skid, leaning back.
                float leave=Math.Min(trip.Leave+j*NativeHotdogFriends.File,trip.Arrive-.1f),dash=leave+NativeHotdogFriends.Pop+(trip.Arrive-NativeHotdogFriends.Skid-leave-NativeHotdogFriends.Pop)*.5f;
                Check(NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,slot,waves,dash,out var run)&&run.Lean>.2f&&run.Arms==NativeHotdogFriends.ArmsSwing&&!run.Standing&&run.Squash>.85f&&run.Squash<1.15f&&run.Hop>=0,"friend "+j+" dashes out leaning into it");
                Check(NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,slot,waves,trip.Arrive-.005f,out var skid)&&skid.Lean<0,"friend "+j+" skids to a stop");
                Check(NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,slot,waves,trip.Arrive+NativeHotdogFriends.Crouch*.8f,out var crouch)&&crouch.Squash<.9f&&crouch.Standing,"friend "+j+" crouches under it");
                Check(NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,slot,waves,(trip.Lifted+trip.Back)*.5f,out var carry)&&carry.Hold==1&&carry.Arms==NativeHotdogFriends.ArmsUp&&carry.Scale==1&&Math.Abs(carry.Facing.Length()-1)<1e-3f&&carry.Feet.Y==0,"friend "+j+" carries it, arms up, on the floor");
                NativeHotdogFriends.Load(trip,rest,mouth,(trip.Lifted+trip.Back)*.5f,out var at,out _,out _);
                Check(Flat(carry.Feet,at+slot)<1e-3f,"friend "+j+" stays on its spot under the load as it goes");
                Check(Math.Abs(carry.Roll)<=.17f&&carry.Hop<=.013f,"friend "+j+" waddles");
                Check(NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,slot,waves,inAt-.005f,out var dive)&&dive.Lean>1&&dive.Scale<.2f&&Flat(dive.Feet,mouth)<.15f,"friend "+j+" dives into the door head first");
                Check(!NativeHotdogFriends.Friend(trip,j,friends,rest,mouth,slot,waves,inAt+.01f,out _),"friend "+j+" goes in after its load");
                last=Math.Max(last,inAt);
            }
        }
        // One load a metre off: its last friend has time to turn on the step and wave before it dives in.
        {
            var rest=new Vector3(1,.1f,.2f);var toDoor=mouth-rest;toDoor.Y=0;var trip=NativeHotdogFriends.Plan(0,1,1,3,0);var slots=NativeHotdogFriends.Slots(new[]{Vector3.Zero},new[]{.1f},toDoor,3);
            float plain=0;for(int j=0;j<3;j++)plain=Math.Max(plain,NativeHotdogFriends.InAt(trip,j,slots[j],false));Check(NativeHotdogFriends.WaveFits(plain),"a small scene has time for the wave");
            float inAt=NativeHotdogFriends.InAt(trip,2,slots[2],true),waveAt=inAt-NativeHotdogFriends.Dive-NativeHotdogFriends.Wave*.5f;
            Check(NativeHotdogFriends.Friend(trip,2,3,rest,mouth,slots[2],true,waveAt,out var wave)&&wave.Arms==NativeHotdogFriends.ArmsWave&&Vector3.Dot(wave.Facing,Vector3.Normalize(toDoor))<-.99f&&Flat(wave.Feet,mouth)<.15f,"the last friend waves on the step, facing out");
            Check(Math.Abs(inAt-NativeHotdogFriends.InAt(trip,2,slots[2],false)-NativeHotdogFriends.Wave)<1e-4f&&NativeHotdogFriends.Friend(trip,2,3,rest,mouth,slots[2],true,inAt-.005f,out var after)&&after.Lean>1,"its wave holds it back, then it dives");
            Check(NativeHotdogFriends.Over(inAt)<(float)NativeKillEffectRules.DeathSeconds,"and the scene still ends in time");
        }
        Check(NativeHotdogFriends.Over(last)<(float)NativeKillEffectRules.DeathSeconds,"the whole scene ends inside the body's "+NativeKillEffectRules.DeathSeconds+" s ("+NativeHotdogFriends.Over(last).ToString("0.00")+" s)");
        // The most: twelve loads 3 m away, six friends each, the wave too.
        {
            float worst=0;var far=new Vector3(3,.1f,0);
            for(int i=0;i<NativeHotdogFriends.MaxLoads;i++){var trip=NativeHotdogFriends.Plan(i,NativeHotdogFriends.MaxLoads,3,NativeHotdogFriends.MostFriends,0);for(int j=0;j<NativeHotdogFriends.MostFriends;j++)worst=Math.Max(worst,NativeHotdogFriends.InAt(trip,j,new Vector3(.8f,0,0),false));}
            Check(NativeHotdogFriends.Over(worst)<(float)NativeKillEffectRules.DeathSeconds,"the most loads and friends still end inside "+NativeKillEffectRules.DeathSeconds+" s ("+NativeHotdogFriends.Over(worst).ToString("0.00")+" s)");
            Check(!NativeHotdogFriends.WaveFits(worst),"no wave when it would run past the body's time ("+NativeHotdogFriends.Over(worst+NativeHotdogFriends.Wave).ToString("0.00")+" s)");
        }
        // The door's spots ring the pieces, facing them.
        for(int k=0;k<8;k++){var spot=NativeHotdogFriends.DoorSpot(new Vector3(1,0,2),.6f,77,k,out float yaw);var d=new Vector3(1,0,2)-spot;float face=(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);Check(Math.Abs(Vector3.Distance(spot,new Vector3(1,0,2))-(.6f+NativeHotdogFriends.DoorAway))<1e-3f&&Math.Abs(((face-yaw)%360+540)%360-180)<.1f,"door spot "+k+" stands past the pieces, its mouth to them");}
        // The models build; the stride frames differ, the standing one has both feet down, the wave's hand moves.
        int verts=-1;
        for(int f=0;f<NativeHotdogFriends.Frames;f++)for(int a=0;a<NativeHotdogFriends.ArmKinds;a++){var m=NativeHotdogFriends.Friend(f,a);Check(m.Count>100,"friend frame "+f+" arms "+a+" builds");if(verts<0)verts=m.Count;}
        var a0=NativeHotdogFriends.Friend(0,NativeHotdogFriends.ArmsSwing);var a4=NativeHotdogFriends.Friend(NativeHotdogFriends.WalkFrames/2,NativeHotdogFriends.ArmsSwing);var standing=NativeHotdogFriends.Friend(NativeHotdogFriends.WalkFrames,NativeHotdogFriends.ArmsSwing);
        var w0=NativeHotdogFriends.Friend(0,NativeHotdogFriends.ArmsWave);var w2=NativeHotdogFriends.Friend(2,NativeHotdogFriends.ArmsWave);
        bool Differ(NativeFallenKit x,NativeFallenKit y){if(x.P.Count!=y.P.Count)return true;for(int i=0;i<x.P.Count;i++)if(Vector3.Distance(x.P[i],y.P[i])>1e-4f)return true;return false;}
        Check(Differ(a0,a4)&&Differ(a0,standing)&&Differ(w0,w2),"the stride's frames differ, and the wave's hand moves");
        float top=0,low=1;for(int i=0;i<standing.P.Count;i++){top=Math.Max(top,standing.P[i].Y);low=Math.Min(low,standing.P[i].Y);}Check(top>.2f&&top<.36f&&low>-.01f,"a friend stands about "+NativeHotdogFriends.Height+" m ("+top.ToString("0.00")+"), on the floor");
        var door=NativeHotdogFriends.DoorFrame();var leaf=NativeHotdogFriends.DoorLeaf();Check(door.Count>50&&leaf.Count>20,"the door and its leaf build");
        Check(NativeHotdogFriends.Frame(new NativeHotdogFriends.Pose{Standing=true})==NativeHotdogFriends.WalkFrames&&NativeHotdogFriends.Frame(new NativeHotdogFriends.Pose{Step=.5f})==NativeHotdogFriends.WalkFrames/2&&NativeHotdogFriends.Frame(new NativeHotdogFriends.Pose{Step=1})==0&&NativeHotdogFriends.Frame(new NativeHotdogFriends.Pose{Arms=NativeHotdogFriends.ArmsWave,Standing=true,Step=.25f})==NativeHotdogFriends.WalkFrames/4,"the stride cycles every two steps; standing has its own frame; the wave runs its own");
        // The DEV gate, the lists and the wiring.
        Check(NativeCosmeticGate.Restricted(NativeHotdogFriends.Style)&&!NativeCosmeticGate.Allowed(NativeAccess.Guest,"effect",NativeHotdogFriends.Style,0),"restricted as the other DEV effects: a guest cannot pick it");
        Check(NativeLocker.KillText(NativeHotdogFriends.Style)==NativeHotdogFriends.Text&&NativeBisect.Applies(true,NativeHotdogFriends.Style,false,"pilot"),"its locker line; a blade still cuts its body as the stock ragdoll");
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        Check(R("NativeProfileUI.cs").Contains("\"dev-bass-van\",NativeHotdogFriends.Style};")&&R("NativeAdmin.cs").Contains("\"effect:\"+NativeHotdogFriends.Style};"),"listed in the locker and grantable");
        var death=R("NativeDeathPresentation.cs");
        Check(death.Contains("death.style==NativeHotdogFriends.Style?\"impact-ragdoll\":death.style;")&&death.Contains("if(friends&&!remains)HotdogFriends(corpse,fx);")&&death.Contains("stock&&!retain&&!friends?"),"its body is the stock ragdoll, carried off instead of sinking");
        Check(R("NativeBloodPresentation.cs").Contains("HotdogFriendsFx(now);"),"the scene's puffs are drawn");
        var pres=R("NativeHotdogFriendsPresentation.cs");Check(!pres.Contains("Damage(")&&!pres.Contains("SendInput"),"presentation only");
        Check(pres.Contains("!c.IsCrushed(pair.Key)&&pair.Value.gameObject.activeInHierarchy")&&pres.Contains("c.LooseModel!=null"),"only what is drawn is carried (a crushed part is its fragments)");
        Check(pres.Contains("NativeHotdogFriends.WaveFits(latest)")&&pres.Contains("var rest=bounds.center;")&&pres.Contains("NativeHotdogFriends.Slots(spots,halves,"),"a load's middle is its pieces' own, and its friends stand under them");
        var body=R("NativeVisualBodyPresentation.cs");
        Check(body.Contains("if(corpse.FriendsCarried)VisualCorpseFriendsScale(c,corpse);")&&body.Contains("b.localScale=Vector3.one*Mathf.Max(.001f,own/Mathf.Max(.001f,above));"),"the model's pieces shrink through the door with their parts");
        Console.WriteLine("PASS hotdog friends: "+n+" checks (the door and its slam, the spots under the pieces, six loads' trips and each friend's dash, skid, heave, waddle and dive, the wave, inside "+NativeKillEffectRules.DeathSeconds+" s at the most, the models, the DEV gate, the wiring)");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Cyber City's way out of the Cut (Core NativeCyberCityExits, NativeCyberCityKit's exit dressing, NativeCyberCityFx's floor
    // guidance; Runtime NativeCutExitHud). Against the real map (Resources/UnityMaps.json `cybercity`, with Astra's generated nav):
    // the exits are found from data (the maglev's envelope, the walls, the nav routes), match the stair pits and skip the alcoves;
    // a synthetic map shows a new exit (any id) is picked up and a dead end is not; the nearest-exit and bearing queries; the
    // HUD hint shows only in the Cut; the phase colouring of the chevrons and the hint; the kit dresses exits (EXIT, not SAFE) and
    // chases its climb lights upward; the FX keeps the guidance on every tier, keeps 16 beacons and allocates nothing.
    internal static class CutExitChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Cut exits: "+what);count++;}
        public static void Run(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};
            var map=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o).maps.Single(m=>m.id=="cybercity");
            var x=NativeCyberCityExits.Build(map);
            FromData(map,x);Synthetic();Queries(map,x);Hint(x);Tones(map,x);Kit(map,x);Fx(map,x);Hooks(root);
            Console.WriteLine("PASS cut exits ("+count+" checks: exits from the maglev envelope, walls and nav routes (4 stair pits, no alcoves), a new exit picked up and a dead end not; nearest exit and bearing; hint only in the Cut; chevron and hint tones follow the maglev; kit EXIT signs, frames and chasing climb lights instead of SAFE; guidance on every tier, 16 beacons, no allocations; runtime hooks)");
        }
        private static int Id(ContentMap m,string id)=>Array.FindIndex(m.blocks,b=>b.id==id);
        private static void FromData(ContentMap map,NativeCyberCityExits x)
        {
            var h=map.hazards.First(NativeCyberCityHazards.IsMaglev);
            Check(x.Found&&x.Maglev==Array.IndexOf(map.hazards,h)&&Math.Abs(x.FloorY-(h.center.y-h.size.y/2))<1e-4f&&x.X0==h.center.x-h.size.x/2&&x.X1==h.center.x+h.size.x/2,"the Cut is the maglev's envelope (floor "+x.FloorY+")");
            Check(Math.Abs(x.Inner-3)<1e-4f&&Math.Abs(x.Outer-4.2f)<1e-4f&&Math.Abs(x.StreetY)<1e-4f,"walls' inner/outer faces and the street read from the trench walls ("+x.Inner+", "+x.Outer+", "+x.StreetY+")");
            // Four exits: each door opens onto a stair pit's landing on its own wall; the routes rise to the street.
            var landings=map.blocks.Where(b=>b.id.StartsWith("cybercity-pit-",StringComparison.Ordinal)&&b.id.EndsWith("-landing",StringComparison.Ordinal)).ToArray();
            Check(x.Exits.Length==landings.Length&&x.Exits.Length==4,"one exit per stair pit ("+x.Exits.Length+" exits, "+landings.Length+" landings)");
            foreach(var e in x.Exits)
            {
                var l=landings.Single(b=>Math.Abs(b.pos.x-e.DoorX)<b.size.x/2&&Math.Sign(b.pos.z)==e.Side);
                Check(Math.Abs(e.DoorZ-e.Side*x.Inner)<1e-4f&&e.PathCount>4&&Math.Abs(e.Rise-(x.StreetY-x.FloorY))<1e-3f&&Math.Abs(e.TopY-x.StreetY)<1e-3f&&e.Length>e.Rise,"exit at "+e.DoorX+" rises "+e.Rise+" m over "+e.Length+" m to the street at "+e.TopX+", "+e.TopZ);
                Check(Math.Abs(e.TopZ-l.pos.z)<1&&Math.Sign(e.TopX-l.pos.x)==Math.Sign(e.RunX)&&Math.Abs(e.RunX)>.9f,"the route climbs away from "+l.id+" along the wall");
                var li=map.blocks[e.Lintel];Check(e.Lintel>=0&&li.size.y<3&&li.pos.x-li.size.x/2<=e.DoorX&&li.pos.x+li.size.x/2>=e.DoorX&&li.id.StartsWith("cybercity-trench-wall-",StringComparison.Ordinal),"the lintel over "+e.DoorX+" is "+li.id);
                Check(e.JambLo>=0&&e.JambHi>=0&&map.blocks[e.JambLo].size.y>=3&&map.blocks[e.JambHi].size.y>=3&&Math.Abs(e.OpenX1-e.OpenX0-li.size.x)<1e-3f&&Math.Abs(e.OpenTop-(li.pos.y-li.size.y/2))<1e-4f,"its jambs are the tall walls either side and the opening is the lintel's span");
                // Every step of that pit carries this exit's climb, rising 0 to 1 with the step tops; nothing else does but the landing.
                string pit=l.id.Substring(0,l.id.Length-"-landing".Length);var steps=Enumerable.Range(0,map.blocks.Length).Where(i=>map.blocks[i].id.StartsWith(pit+"-stair-",StringComparison.Ordinal)).ToArray();
                int ei=Array.IndexOf(x.Exits,e);
                Check(steps.Length==16&&steps.All(i=>x.BlockExit[i]==ei)&&x.BlockExit[Id(map,l.id)]==ei,"all 16 steps and the landing of "+pit+" carry its climb");
                var ordered=steps.OrderBy(i=>map.blocks[i].pos.y+map.blocks[i].size.y/2).Select(i=>x.BlockClimb[i]).ToArray();
                Check(ordered.Zip(ordered.Skip(1),(a,b)=>b>a).All(v=>v)&&ordered[0]<.01f&&ordered[15]>.9f,"the climb fraction rises with the steps ("+ordered[0]+".."+ordered[15]+")");
            }
            Check(Enumerable.Range(0,map.blocks.Length).Where(i=>x.BlockExit[i]>=0).All(i=>map.blocks[i].id.StartsWith("cybercity-pit-",StringComparison.Ordinal)),"only the pits' blocks carry climbs (not the floor, walls or street)");
            // The other lintels are alcoves: openings with no way up.
            var lintels=map.blocks.Select((b,i)=>(b,i)).Where(p=>p.b.id.StartsWith("cybercity-trench-wall-",StringComparison.Ordinal)&&p.b.size.y<3).ToArray();
            Check(lintels.Length==16&&lintels.Count(p=>x.BlockOpening[p.i]>=0&&x.BlockOpening[p.i]%4==1)==4,"4 of the 16 lintels are exits, 12 stay alcoves");
            Check(x.Exits.Select(e=>e.Side).OrderBy(s=>s).SequenceEqual(new[]{-1,-1,1,1})&&x.Exits.All(e=>e.Component==x.Exits[0].Component),"two exits a wall, all on the one Cut floor");
            var y=NativeCyberCityExits.Build(map);Check(y.Exits.Length==x.Exits.Length&&y.Exits.Zip(x.Exits,(a,b)=>a.DoorX==b.DoorX&&a.DoorZ==b.DoorZ&&a.TopX==b.TopX&&a.Lintel==b.Lintel&&a.PathCount==b.PathCount).All(v=>v)&&y.BlockClimb.SequenceEqual(x.BlockClimb),"the derivation is deterministic");
            Check(!NativeCyberCityExits.Build(new ContentMap{blocks=map.blocks,nav=map.nav}).Found&&NativeCyberCityExits.Build(new ContentMap{blocks=map.blocks,hazards=map.hazards}).Exits.Length==0&&NativeCyberCityExits.Build(null).Exits.Length==0,"no maglev, no Cut; no nav, no exits; nothing breaks without a map");
        }
        // A small Cut with one new opening whose ids follow no convention: the exit comes from geometry and nav alone.
        private static ContentMap Toy(bool top,bool second)
        {
            ContentBlock B(string id,float x,float y,float z,float sx,float sy,float sz)=>new ContentBlock{id="cybercity-"+id,material="stone",role="static",pos=new V3(x,y,z),size=new V3(sx,sy,sz)};
            var blocks=new List<ContentBlock>{B("floor",0,-4.25f,0,40,.5f,8.4f),B("wall-a",-10.5f,-2,3.6f,19,4,1.2f),B("door-head",0,-.8f,3.6f,2,1.6f,1.2f),B("wall-b",10.5f,-2,3.6f,19,4,1.2f),B("wall-s",0,-2,-3.6f,40,4,1.2f),B("pad",0,-4.25f,5.1f,2.2f,.5f,1.8f)};
            for(int k=1;k<=7;k++){float tall=.5f+.5f*k;blocks.Add(B("ramp-"+k,k,-4.5f+tall/2,5.1f,1,tall,1.8f));}
            var nodes=new List<MapNavNode>();var links=new List<MapNavLink>();
            MapNavNode N(float px,float floor,float pz){var n=new MapNavNode{id="n"+px+","+pz+","+floor,pos=new V3(px,floor+NativeCyberCityExits.NavLift,pz)};nodes.Add(n);return n;}
            void L(MapNavNode a,MapNavNode b)=>links.Add(new MapNavLink{id=a.id+">"+b.id,a=a.id,b=b.id,kind="walk",requires=new string[0],after=new string[0],hazards=new string[0]});
            var floor=new Dictionary<(int,int),MapNavNode>();for(int px=-6;px<=12;px++)for(int pz=-2;pz<=3;pz++)floor[(px,pz)]=N(px,-4,pz);
            foreach(var kv in floor){if(floor.TryGetValue((kv.Key.Item1+1,kv.Key.Item2),out var r))L(kv.Value,r);if(floor.TryGetValue((kv.Key.Item1,kv.Key.Item2+1),out var u))L(kv.Value,u);}
            var mouth=N(0,-4,4);L(floor[(0,3)],mouth);var prev=N(0,-4,5);L(mouth,prev);
            for(int k=1;k<=7;k++){var s=N(k,-4+.5f*k,5);L(prev,s);prev=s;}
            if(top)L(prev,N(8,0,5));
            var pocket=N(-4,-4,4);L(floor[(-4,3)],pocket);// an alcove: a dead end
            if(second){blocks.Add(B("hatch",10,-.8f,3.6f,2,1.6f,1.2f));var m2=N(10,-4,4);L(floor[(10,3)],m2);var c2=N(10,-2,4.8f);L(m2,c2);L(c2,N(10,0,5.6f));}
            var h=new MapHazard{id="cybercity-maglev",kind="maglev",center=new V3(0,-2.45f,0),size=new V3(40,3.1f,5.4f),axis=new V3(1,0,0),first=18,period=32,warn=6,danger=3,speed=44,length=42,lethal=true};
            return new ContentMap{id="cybercity",blocks=blocks.ToArray(),hazards=new[]{h},nav=new MapNav{nodes=nodes.ToArray(),links=links.ToArray()}};
        }
        private static void Synthetic()
        {
            var m=Toy(true,false);var x=NativeCyberCityExits.Build(m);
            Check(x.Found&&x.Exits.Length==1&&x.Exits[0].Side==1&&Math.Abs(x.Exits[0].DoorX)<1e-4f&&m.blocks[x.Exits[0].Lintel].id=="cybercity-door-head"&&m.blocks[x.Exits[0].JambLo].id=="cybercity-wall-a"&&m.blocks[x.Exits[0].JambHi].id=="cybercity-wall-b","a new opening with any ids is an exit, its head the lintel, the walls its jambs");
            Check(Math.Abs(x.Exits[0].TopX-8)<1e-4f&&x.Exits[0].RunX>.9f&&Enumerable.Range(1,7).All(k=>x.BlockExit[Id(m,"cybercity-ramp-"+k)]==0)&&x.BlockExit[Id(m,"cybercity-pad")]==0&&x.BlockExit[Id(m,"cybercity-floor")]<0,"its route, its climb pieces and its landing; the floor slab is not a climb");
            var kit=NativeCyberCityKit.Build(m.blocks,m.hazards,x);int head=Id(m,"cybercity-door-head");
            Check(kit.Parts.Any(p=>p.Parent==head&&p.Drive==NativeCyberCityKit.DriveExit)&&!kit.Parts.Any(p=>p.Parent==head&&p.Drive==NativeCyberCityKit.DriveSafe)&&kit.Parts.Any(p=>p.Parent==Id(m,"cybercity-wall-a")&&p.Drive==NativeCyberCityKit.DriveExit)&&kit.Parts.Any(p=>p.Parent==Id(m,"cybercity-pad")&&p.Drive==NativeCyberCityKit.DriveExitChase),"the kit dresses the new exit: EXIT on its head, a frame on its jamb, chevrons on its landing");
            Check(NativeCyberCityExits.Build(Toy(false,false)).Exits.Length==0,"a climb that never reaches the street is no exit");
            var two=NativeCyberCityExits.Build(Toy(true,true));Check(two.Exits.Length==2&&two.Exits[1].DoorX==10&&two.Exits[1].Lintel==Id(Toy(true,true),"cybercity-hatch"),"a second route added later is picked up ("+two.Exits.Length+")");
        }
        private static void Queries(ContentMap map,NativeCyberCityExits x)
        {
            // Nearest reachable exit: the minimum door distance (brute force agrees) everywhere on the floor.
            bool agree=true;for(float px=-37.5f;px<=37.5f;px+=.5f)for(float pz=-2.8f;pz<=2.8f;pz+=.7f)
            {
                int e=x.Nearest(px,pz,out float d);float best=x.Exits.Min(q=>(float)Math.Sqrt((q.DoorX-px)*(q.DoorX-px)+(q.DoorZ-pz)*(q.DoorZ-pz)));
                if(e<0||Math.Abs(d-best)>1e-3f||x.ComponentAt(px,pz)<0)agree=false;
            }
            Check(agree,"the nearest reachable exit is the closest door from every floor point, all on one component");
            float far=0;for(float px=-38;px<=38;px+=.25f)for(float pz=-2.8f;pz<=2.8f;pz+=.4f){x.Nearest(px,pz,out float d);far=Math.Max(far,d);}
            Check(far<16,"no Cut floor point is more than 16 m from an exit door ("+far.ToString("0.0")+" m)");
            // The chevrons on each wall point to that wall's nearest exit: the watershed is halfway between its two exits.
            foreach(int side in new[]{-1,1})
            {
                var doors=x.Exits.Where(e=>e.Side==side).Select(e=>e.DoorX).OrderBy(v=>v).ToArray();float mid=(doors[0]+doors[1])/2;
                Check(x.NearestOnSide(mid-.5f,side,out float a)>=0&&a<0&&x.NearestOnSide(mid+.5f,side,out float b)>=0&&b>0&&x.NearestOnSide(doors[1]+5,side,out float c)>=0&&c<0&&x.NearestOnSide(doors[0]-1,side,out float d)>=0&&d>0,"wall "+side+": chevrons turn at "+mid+" and point back from beyond the end exits");
            }
            // Bearing: clockwise from the camera's forward, matching atan2 on the camera's right; ahead is 0.
            var e1=x.Exits[0];var h=x.HintFor(e1.DoorX,-4,e1.DoorZ-2,0,1);Check(h.Shown&&h.Exit==0&&Math.Abs(h.Bearing)<1e-3f&&Math.Abs(h.Distance-2)<1e-4f,"facing the door from 2 m: bearing 0, 2 m");
            h=x.HintFor(e1.DoorX-3,-4,e1.DoorZ,0,1);Check(h.Shown&&h.Exit==0&&Math.Abs(h.Bearing-90)<1e-3f,"the door to the right (+x while facing +z) is +90");
            h=x.HintFor(e1.DoorX+3,-4,e1.DoorZ,0,1);Check(Math.Abs(h.Bearing+90)<1e-3f,"to the left is -90");
            h=x.HintFor(e1.DoorX,-4,e1.DoorZ-2,0,-1);Check(Math.Abs(Math.Abs(h.Bearing)-180)<1e-3f,"behind is 180");
            h=x.HintFor(0,-4,0,1,0);var ne=x.Exits[h.Exit];Check(Math.Abs(h.Bearing-(float)(Math.Atan2(-(ne.DoorZ-0),ne.DoorX-0)*180/Math.PI))<1e-3f,"a diagonal door's bearing ("+h.Bearing+")");
        }
        private static void Hint(NativeCyberCityExits x)
        {
            var e=x.Exits[0];
            Check(x.HintFor(-20,-4,0,1,0).Shown&&x.HintFor(-20,-2.4f,1,1,0).Shown,"shown on the Cut floor, also mid-jump");
            Check(x.HintFor(-28,-4,3.6f,1,0).Shown&&x.HintFor(e.DoorX,-4,e.DoorZ+.6f,1,0).Shown,"shown in an alcove and in the door recess");
            Check(!x.HintFor(-20,0,8,1,0).Shown&&!x.HintFor(0,0,0,1,0).Shown&&!x.HintFor(-24,0,0,1,0).Shown,"hidden on the street and on the bridges over the Cut");
            Check(!x.HintFor(e.DoorX,-4,e.DoorZ+2.1f,1,0).Shown&&!x.HintFor(-31,-2.5f,5,1,0).Shown,"hidden on the landing and the stairs beyond the door");
            Check(!x.HintFor(45,-4,0,1,0).Shown&&!x.HintFor(-20,-6,0,1,0).Shown&&!new NativeCyberCityExits().HintFor(-20,-4,0,1,0).Shown,"hidden past the portals, under the floor and on maps without a Cut");
            Check(x.InCut(-20,-.6f,0)&&!x.InCut(-20,-.4f,0),"in the Cut from half a metre under the street");
        }
        private static void Tones(ContentMap map,NativeCyberCityExits x)
        {
            var h=map.hazards[x.Maglev];
            (double t,int tone)[] table={(20,0),(22.5,1),(24.9,1),(25.2,2),(27.9,2),(28.6,2),(30.5,2),(31.5,0),(33,0),(54.5,1),(57.5,2)};
            foreach(var (t,tone) in table)Check(NativeCyberCityExits.Tone(NativeCyberCityHazards.Evaluate(h,1,10,t))==tone,"tone at "+t+" s is "+tone);
            Check(NativeCyberCityExits.Tone(NativeCyberCityHazards.At(h,NativePhase.Countdown,1,10,26.2))==0,"calm outside the Fight and Result (the hazard clock is Idle there)");
            NativeCyberCityFx.GuideTone(0,out double r0,out double g0,out double b0,out double v0);NativeCyberCityFx.GuideTone(1,out double r1,out double g1,out double b1,out double v1);NativeCyberCityFx.GuideTone(2,out double r2,out double g2,out double b2,out double v2);
            Check(Math.Abs(r0-g0)<.15*r0&&Math.Abs(b0-g0)<.15*g0&&r0>.8,"calm chevrons are white ("+r0.ToString("0.00")+", "+g0.ToString("0.00")+", "+b0.ToString("0.00")+")");
            Check(r1>g1*1.8&&g1>b1*10,"warning chevrons are amber");Check(r2>g2*10&&r2>b2*10,"danger chevrons are red");
            Check(v0<v1&&v1<v2,"the pulse scrolls faster as the train nears ("+v0+", "+v1+", "+v2+" m/s)");
            // The pulse travels toward the door: the brightest of a lane's chevrons moves to a smaller distance as time runs.
            double Peak(double t){double best=-1,at=0;for(double d=0;d<9;d+=.05){double k=NativeCyberCityFx.GuidePulse(0,d,t);if(k>best){best=k;at=d;}}return at;}
            double p0=Peak(9),p1=Peak(9.3);Check(p1<p0&&p0-p1>.5,"the pulse scrolls toward the exit ("+p0.ToString("0.00")+" -> "+p1.ToString("0.00")+" m)");
            bool bounded=true;for(int tone=0;tone<3;tone++)for(double d=0;d<40;d+=.37)for(double t=0;t<5;t+=.13){double k=NativeCyberCityFx.GuidePulse(tone,d,t);if(!(k>0&&k<=1.2))bounded=false;}
            Check(bounded,"the pulse stays in (0, 1.2]");
        }
        private static void Kit(ContentMap map,NativeCyberCityExits x)
        {
            var old=NativeCyberCityKit.Build(map.blocks,map.hazards);var kit=NativeCyberCityKit.Build(map.blocks,map.hazards,x);
            Check(old.Parts.Count(p=>p.Drive==NativeCyberCityKit.DriveSafe)==16&&old.Exits==null,"without exits the kit is unchanged (16 SAFE lights)");
            Check(kit.Exits==x&&kit.Parts.Count(p=>p.Drive==NativeCyberCityKit.DriveSafe)==12,"with them the 12 alcoves keep SAFE and the 4 exits lose it");
            foreach(var e in x.Exits)
            {
                Check(kit.Parts.Count(p=>p.Parent==e.Lintel&&p.Drive==NativeCyberCityKit.DriveExit)>=10&&!kit.Parts.Any(p=>p.Parent==e.Lintel&&p.Drive==NativeCyberCityKit.DriveSafe),"the lintel at "+e.DoorX+" carries EXIT, a pictogram and its lights, no SAFE");
                Check(kit.Parts.Any(p=>p.Parent==e.JambLo&&p.Drive==NativeCyberCityKit.DriveExit)&&kit.Parts.Any(p=>p.Parent==e.JambHi&&p.Drive==NativeCyberCityKit.DriveExit),"both jambs are framed");
                // Every lintel part faces the Cut (on the wall's inner side).
                var li=map.blocks[e.Lintel];Check(kit.Parts.Where(p=>p.Parent==e.Lintel&&p.Y>-li.size.y/2).All(p=>Math.Sign(-p.Z)==e.Side||Math.Abs(p.Z)<li.size.z/2),"the sign is on the face toward the Cut");
                // The pictogram rises the way the climb goes.
                var steps=kit.Parts.Where(p=>p.Parent==e.Lintel&&p.Drive==NativeCyberCityKit.DriveExit&&p.Hx>.08f&&p.Hx<.12f&&p.Y<0).OrderBy(p=>p.Hy).ToArray();
                Check(steps.Length==4&&Math.Sign(steps[3].X-steps[0].X)==Math.Sign(e.RunX),"the stair pictogram rises toward the climb ("+steps.Length+")");
            }
            var chase=kit.Parts.Where(p=>p.Drive==NativeCyberCityKit.DriveExitChase).ToArray();
            Check(chase.Length>=4*16*2&&chase.All(p=>p.Param>=0&&p.Param<=1&&p.Hazard==kit.Maglev),"every exit step's nosings chase ("+chase.Length+" parts)");
            Check(kit.Parts.Where(p=>p.Drive==NativeCyberCityKit.DriveSteady&&map.blocks[p.Parent].id.Contains("station-stair-")).Any(),"other stairs keep their steady ice nosings");
            // Drives: EXIT brightens from the warning; the chase's pulse climbs (its peak moves up as time runs).
            var states=new NativeHazardState[map.hazards.Length];void Frame(double t){for(int i=0;i<states.Length;i++)states[i]=NativeCyberCityHazards.Evaluate(map.hazards[i],1,10,t);}
            var sign=kit.Parts.First(p=>p.Drive==NativeCyberCityKit.DriveExit);
            float Level(NativeCyberCityPart p,double t){Frame(t);NativeCyberCityKit.Drive(p,states,t,out _,out _,out _,out float l);return l;}
            Check(Level(sign,20)<1.01f&&Level(sign,23.5)>1.04f&&Level(sign,26.2)>1.04f,"EXIT breathes at rest and brightens in the telegraph");
            Frame(20);NativeCyberCityKit.Drive(sign,states,20,out float sr,out float sg,out float sb,out _);Check(Math.Abs(sr-sg)<.1f*sr&&Math.Abs(sb-sg)<.1f*sg,"EXIT is white, apart from the lime SAFE");
            var safe=kit.Parts.First(p=>p.Drive==NativeCyberCityKit.DriveSafe);NativeCyberCityKit.Drive(safe,states,20,out float lr,out float lg,out float lb,out _);Check(lg>lr*1.3f&&lg>lb*1.3f,"SAFE stays lime");
            double Climb(double t){Frame(t);float best=-1;double at=0;for(float c=0;c<1;c+=.02f){var p=chase[0];p.Param=c;NativeCyberCityKit.Drive(p,states,t,out _,out _,out _,out float l);if(l>best){best=l;at=c;}}return at;}
            double c0=Climb(20.1),c1=Climb(20.4);Check(c1>c0||c0>.9,"the chase climbs ("+c0.ToString("0.00")+" -> "+c1.ToString("0.00")+")");
            bool pure=true;foreach(double t in new[]{20,23.5,26.2,28.6}){Frame(t);foreach(var p in kit.Parts){NativeCyberCityKit.Drive(p,states,t,out float a1,out float b1,out float c1_,out float l1);NativeCyberCityKit.Drive(p,states,t,out float a2,out float b2,out float c2,out float l2);if(a1!=a2||l1!=l2||float.IsNaN(a1)||l1<0)pure=false;}}
            Check(pure,"the exit drives are pure and finite");
            // §2.1 rule 1 holds for the new parts (below 3 m within .25 m of the block, or paint, or thin).
            int bad=0;for(int i=0;i<kit.Parts.Length;i++)
            {
                var p=kit.Parts[i];if(p.Drive!=NativeCyberCityKit.DriveExit&&p.Drive!=NativeCyberCityKit.DriveExitChase)continue;var bl=map.blocks[p.Parent];
                float ox=Math.Max(0,Math.Abs(p.X)+p.Hx-bl.size.x/2),oy=Math.Max(0,Math.Abs(p.Y)+p.Hy-bl.size.y/2),oz=Math.Max(0,Math.Abs(p.Z)+p.Hz-bl.size.z/2);
                if(Math.Max(ox,Math.Max(oy,oz))>.25f+1e-4f)bad++;
            }
            Check(bad==0,"the exit parts stay within .25 m of their blocks ("+bad+")");
            int draws=(kit.Parts.Count(p=>p.Shadow)+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk+(kit.Parts.Count(p=>!p.Shadow)+kit.Train.Length+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;
            Check(draws<=NativeBlockLooks.DrawBudget/6,"the dressing still fits its draws ("+draws+")");
        }
        private static NativeFxLight Light(){var l=new NativeFxLight{Lx=-.4,Ly=.8,Lz=.4,Sr=.3,Sg=.33,Sb=.5};for(int i=0;i<9;i++)l.Ambient[i]=.05;return l;}
        private static void Fx(ContentMap map,NativeCyberCityExits x)
        {
            var back=NativeCyberCityScenery.Build();var kit0=NativeCyberCityKit.Build(map.blocks,map.hazards);var kit=NativeCyberCityKit.Build(map.blocks,map.hazards,x);
            var s0=NativeCyberFxScene.Build(map.blocks,map.hazards,kit0,back);var s1=NativeCyberFxScene.Build(map.blocks,map.hazards,kit,back);
            Check(s0.Cut==null&&s1.Cut==x&&s0.BeaconX.Length==16&&s1.BeaconX.Length==16,"16 pocket beacons either way (the exits keep theirs)");
            var fx=new NativeCyberCityFx();var fx0=new NativeCyberCityFx();var light=Light();var states=new NativeHazardState[map.hazards.Length];var screens=new NativeCyberScreen[1];
            void Frame(NativeCyberCityFx f,NativeCyberFxScene sc,double t,double q,bool low,double ex=-20,double ey=-2.4,double ez=0){for(int i=0;i<states.Length;i++)states[i]=NativeCyberCityHazards.Evaluate(map.hazards[i],1,10,t);f.Build(sc,t,ex,ey,ez,0,0,-1,0,1,0,light,q,low,true,states,screens,0);}
            Frame(fx0,s0,20,1,false);Check(fx0.Guide==0,"no guidance without exits");
            // The guidance's own quads (GuideStart..+Guide); its strokes (mode 0) carry the tone, its glows and pools are white.
            (double r,double g,double b) Mean(double t)
            {
                Frame(fx,s1,t,1,false);int k=fx.GuideStart;
                double r=0,g=0,b=0;int n=0;for(int i=k;i<k+fx.Guide&&i<fx.Quads.Count;i++){if(fx.Quads.Modes[i]!=0)continue;r+=fx.Quads.Colors[i*4];g+=fx.Quads.Colors[i*4+1];b+=fx.Quads.Colors[i*4+2];n++;}
                return n>0?(r/n,g/n,b/n):(0,0,0);
            }
            var calm=Mean(20);var warn=Mean(23.5);var danger=Mean(26.2);
            Check(calm.r>0&&Math.Abs(calm.r-calm.g)<.2*calm.r&&Math.Abs(calm.b-calm.g)<.2*calm.g,"the floor chevrons are white at rest");
            Check(warn.r>warn.g*1.8&&warn.g>warn.b*5,"amber in the warning");Check(danger.r>danger.g*8,"red in the danger");
            Frame(fx,s1,20,1,false);int lanes=fx.Guide;Check(lanes>150,"chevrons along both lanes, the doors' marks and the stair-top glows ("+lanes+" quads)");
            foreach(double t in new[]{20,23.5,26.2,28.6}){Frame(fx,s1,t,1,false);int full=fx.Guide,tel=fx.Telegraph;Frame(fx,s1,t,.3,false);int reduced=fx.Guide;Frame(fx,s1,t,1,true);int low=fx.Guide;Check(full==reduced&&full==low&&full>0,"at "+t+" s the guidance keeps "+full+" quads on every tier");}
            Frame(fx,s1,28.6,1,false,6.6,1.7,-2.3);Check(fx.Quads.Count<NativeCyberCityFx.Capacity*.8,"a busy frame with the guidance fits ("+fx.Quads.Count+" of "+NativeCyberCityFx.Capacity+")");
            bool finite=true;for(int i=0;i<fx.Quads.Count*12;i++)if(float.IsNaN(fx.Quads.Corners[i])||float.IsInfinity(fx.Quads.Corners[i]))finite=false;for(int i=0;i<fx.Quads.Count;i++){float al=fx.Quads.Colors[i*4+3];if(!(al>0&&al<=1)||fx.Quads.Modes[i]>1)finite=false;}
            Check(finite,"the guidance quads are finite, opaque enough and in the known modes");
            Frame(fx,s1,20,1,false,100,5,0);int away=fx.Guide;Check(away<lanes/4,"chevrons far from the eye are culled ("+away+" quads seen from 100 m east)");
            var a=new NativeCyberCityFx();Frame(a,s1,23.5,1,false);Frame(fx,s1,23.5,1,false);Check(a.Quads.Count==fx.Quads.Count&&a.Quads.Colors.Take(a.Quads.Count*4).SequenceEqual(fx.Quads.Colors.Take(fx.Quads.Count*4)),"the guidance is a pure function of the frame");
            // Nothing allocates per frame: the FX, the drives and the hint queries. The measured pass is warmed by the identical
            // pass first, so one-time runtime work on first use (type set-up, tier-up) is not counted as the frame's (CI 2026-10-14
            // counted 3520 bytes once after only one warm frame); a failure names the calls that allocated.
            long fxB=0,driveB=0,hintB=0,nearB=0,sideB=0,toneB=0;
            void Pass(bool split)
            {
                for(int i=0;i<20;i++)
                {
                    long b0=GC.GetAllocatedBytesForCurrentThread();Frame(fx,s1,20+i*.7,i%3==0?.3:1,i%5==0);long b1=GC.GetAllocatedBytesForCurrentThread();
                    foreach(var p in kit.Parts)NativeCyberCityKit.Drive(p,states,20+i,out _,out _,out _,out _);long b2=GC.GetAllocatedBytesForCurrentThread();
                    x.HintFor(-20+i,-4,1,1,0);long b3=GC.GetAllocatedBytesForCurrentThread();x.Nearest(i,-1,out _);long b4=GC.GetAllocatedBytesForCurrentThread();
                    x.NearestOnSide(i,1,out _);long b5=GC.GetAllocatedBytesForCurrentThread();NativeCyberCityExits.Tone(states[x.Maglev]);long b6=GC.GetAllocatedBytesForCurrentThread();
                    if(split){fxB+=b1-b0;driveB+=b2-b1;hintB+=b3-b2;nearB+=b4-b3;sideB+=b5-b4;toneB+=b6-b5;}
                }
            }
            Pass(false);long before=GC.GetAllocatedBytesForCurrentThread();Pass(true);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocations per frame ("+used+" bytes over 20 frames: fx "+fxB+", drives "+driveB+", hint "+hintB+", nearest "+nearB+", side "+sideB+", tone "+toneB+")");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var p=R("NativeCyberCityPresentation.cs");Check(p.Contains("NativeCyberCityExits.Build(map.blocks,map.nav,metroHazards)")&&p.Contains("NativeCyberCityKit.Build(map.blocks,metroHazards,metroExits)"),"the presentation derives the exits from the map and dresses them");
            var h=R("NativeCutExitHud.cs");
            Check(R("NativeFightHud.cs").Contains("CutExitHint(p);")&&h.Contains("if(!NativeCyberCityLooks.Active(arena.MapId)){cutHintAlpha=0;return;}")&&h.Contains("cutExits.HintFor(")&&h.Contains("match.Phase==NativePhase.Fight")&&h.Contains("p.Hp>0"),"the hint runs in the fight HUD, only on Cyber City, only while my pilot lives in the Fight");
            Check(h.Contains("NativeCyberCityExits.Tone(state)")&&h.Contains("NativeCyberCityHazards.At(cutMaglev,match.Phase,NativeStructureFx.RoundIndex(match.Set,match.Round),match.FightAt,arena.Time)"),"its tone reads the maglev's clock from the replicated match frame");
            Check(h.Contains("cutMetres[m]??=")&&!h.Contains("new GameObject")&&!h.Contains("Collider"),"its strings are cached; it builds no objects");
            Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeCyberCityExits.cs")).Contains("UnityEngine"),"NativeCyberCityExits is engine-free Core");
        }
    }
}

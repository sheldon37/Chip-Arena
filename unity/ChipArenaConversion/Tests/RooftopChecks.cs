using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// The home screen's rooftop (Core NativeRooftopCity / NativeRooftopActors / NativeRooftopArt; Runtime
// NativeRooftopPresentation draws it) and the cyber home menu (Runtime NativeHomeMenu): the city builds the same every time
// within its triangle budget and thins its sprinkle on the reduced and low tiers; the camera, the traffic and the actors loop
// seamlessly in 48 s; the near car crosses while the seated pilot's gaze follows it; every holo screen and distant pilot lands
// in the t=0 frame with a clear line of sight; the poses stay finite with the pilot seated on the lip, legs over the edge;
// the duel's tracers all meet the blade's block; the ads and the wordmark rasterise; and the home screen keeps every menu
// function, the updater card's states and the map orbit on the other screens, in the cyber palette (source checks). Pixels,
// shader compilation and frame time in the Unity player are not checked here.
internal static class RooftopChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Rooftop: "+what);count++;}
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    private static bool Near(Vector3 a,Vector3 b,float e)=>Vector3.Distance(a,b)<=e;
    // Möller-Trumbore against a quad's two triangles: the hit distance along the unit ray, or +inf.
    private static float Hit(Vector3 o,Vector3 d,RooftopQuad q){float a=Tri(o,d,q.A,q.B,q.C),b=Tri(o,d,q.A,q.C,q.D);return Math.Min(a,b);}
    private static float Tri(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c)
    {
        var e1=b-a;var e2=c-a;var p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);if(Math.Abs(det)<1e-9f)return float.PositiveInfinity;float inv=1/det;var t=o-a;
        float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)return float.PositiveInfinity;var q=Vector3.Cross(t,e1);float v=Vector3.Dot(d,q)*inv;if(v<0||u+v>1)return float.PositiveInfinity;float dist=Vector3.Dot(e2,q)*inv;return dist>0?dist:float.PositiveInfinity;
    }
    // A world point's screen position (0..1, top-left origin) in the t=0 camera at `aspect`, as At places it.
    private static Vector2 Project(Vector3 p,float aspect)
    {
        NativeRooftopCity.Camera(0,out var eye,out var f);var z=-f;var x=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,z));var y=Vector3.Cross(z,x);var d=p-eye;
        float th=(float)Math.Tan(NativeRooftopCity.FovDegrees*Math.PI/360),depth=Vector3.Dot(d,f);
        return new Vector2(Vector3.Dot(d,x)/depth/(th*aspect)*.5f+.5f,.5f-Vector3.Dot(d,y)/depth/th*.5f);
    }
    // The modelled art's bank (Core NativeRooftopBank): the plan from a synthetic bank (names, tiers, covers), and, where the bank
    // is on this machine (git-ignored: Assets/LocalAssets), the bank itself.
    private static void RooftopBankChecks(string root,NativeRooftopCity.World w)
    {
        NativePackBank.Prop P(string name,string group,bool parts=true)=>new NativePackBank.Prop{Name=name,Group=group,Parts=parts?new[]{new NativePackBank.Part{Mesh=0}}:new NativePackBank.Part[0]};
        var bank=new NativePackBank.Bank();
        foreach(var p in new[]{P("static/perch/0","static"),P("static/perch/2","static"),P("static/city-0/0","static"),P("facade/city/0","facade"),P("facade/city/2","facade"),P("rig/car-cab/0","rig"),P("rig/car-cab/2","rig"),P("rig/car-van/2","rig"),
            P("rig/train/0","rig"),P("static/odd","static"),P("block/x/0","block"),P("cover/perch","cover",false),P("cover/city","cover",false)})bank.Props.Add(p);
        var high=NativeRooftopBank.Read(bank,false);var low=NativeRooftopBank.Read(bank,true);
        Check(high!=null&&high.Static.SequenceEqual(new[]{2,0})&&high.Facade.SequenceEqual(new[]{3})&&low.Static.SequenceEqual(new[]{2,1})&&low.Facade.SequenceEqual(new[]{4}),"the bank's plan: each tier's scenery and facades (a missing tier takes the nearest)");
        Check(high.Cars.SequenceEqual(new[]{"cab","van"})&&high.CarNear.SequenceEqual(new[]{5,7})&&high.CarFar.SequenceEqual(new[]{6,7})&&low.CarNear.SequenceEqual(new[]{6,7}),"the cars by kind, the near lane's fine and the rest low");
        Check(high.Perch&&high.City&&high.Covers(NativeRooftopCity.PerchPart)&&high.Covers(NativeRooftopCity.CityPart)&&!high.Covers(NativeRooftopCity.Far),"the covers name the parts the art replaces; the far city is never covered");
        var bare=new NativePackBank.Bank();bare.Props.Add(P("static/perch/0","static"));var only=NativeRooftopBank.Read(bare,false);
        Check(only!=null&&!only.Perch&&!only.City&&NativeRooftopBank.Read(null,false)==null&&NativeRooftopBank.Read(new NativePackBank.Bank(),false)==null,"a bank without covers replaces nothing; no bank, no plan");
        NativeRooftopBank.Facade(2+.9f*.55f,out float style,out float share);Check(style==2&&Math.Abs(share-.55f)<1e-4f,"a facade vertex unpacks its style and lit share");
        int cars=w.Lanes.Sum(l=>l.Count);var kinds=new int[5];for(int l=0;l<w.Lanes.Count;l++)for(int k=0;k<w.Lanes[l].Count;k++)kinds[NativeRooftopBank.CarKind(l,k,5)]++;
        Check(kinds.All(k=>k>=cars/12)&&NativeRooftopBank.CarKind(2,1,5)==NativeRooftopBank.CarKind(2,1,5)&&NativeRooftopBank.CarKind(0,0,0)==0,"every kind of car flies ("+string.Join("/",kinds)+"), the same car each loop");
        string path=Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources",NativeRooftopBank.Name+".bytes");
        if(!File.Exists(path)){Console.WriteLine("PASS Rooftop art: the plan (the bank is not on this machine: its own checks are skipped)");return;}
        long bytes=new FileInfo(path).Length;var real=NativePackBank.Read(File.ReadAllBytes(path));var hi=NativeRooftopBank.Read(real,false);var lo=NativeRooftopBank.Read(real,true);
        Check(hi!=null&&lo!=null&&hi.Perch&&hi.City&&hi.Static.Count>=2&&hi.Facade.Count>=2&&hi.Cars.Count>=3,"the bank covers the perch and the city, with scenery, facades and cars");
        int Tris(IEnumerable<int> props)=>props.Sum(p=>real.Props[p].Parts.Sum(part=>real.Meshes[part.Mesh].Submeshes.Sum(sm=>sm.Indices.Length)))/3;
        int hiTris=Tris(hi.Static)+Tris(hi.Facade),loTris=Tris(lo.Static)+Tris(lo.Facade);
        Check(bytes<30L*1024*1024&&loTris<hiTris*.6f&&hiTris<400000,"the bank keeps its budget: "+(bytes/1048576f).ToString("0.0")+" MB, "+hiTris+" triangles High, "+loTris+" Low");
        foreach(int p in hi.Facade)foreach(var part in real.Props[p].Parts){var m=real.Meshes[part.Mesh];for(int i=0;i<m.Positions.Length;i++){NativeRooftopBank.Facade(m.Uv1[i].Y,out float st,out float sh);if(st<0||st>3||sh<=0||sh>1||m.Uv1[i].X!=(float)Math.Floor(m.Uv1[i].X)||Math.Abs(m.Normals[i].Y)>1e-3f)Check(false,"a facade vertex carries a whole seed, a style, a lit share and a vertical wall");}}
        // the art stands on the composition: the pilot's lip, the actors' roofs
        var perch=real.Props[hi.Static.First(p=>real.Props[p].Name.StartsWith("static/perch"))];
        Check(Math.Abs(perch.Max.Y-9.2f)<.6f&&perch.Min.X<-1.6f&&perch.Max.X>25.5f&&perch.Min.Z<-1.7f,"the modelled roof spans Core's (the mast to its beacon, the lip to the corner)");
        Console.WriteLine("PASS Rooftop art bank: "+(bytes/1048576f).ToString("0.0")+" MB, "+hiTris+" / "+loTris+" triangles (High / Low), "+hi.Cars.Count+" car kinds, "+real.Props.Count+" props");
    }
    public static void Run(string root)
    {
        var total=Stopwatch.StartNew();
        // 1. The city: deterministic, within budget, the sprinkle thinned on the reduced and low tiers.
        var w=NativeRooftopCity.Build(1);var again=NativeRooftopCity.Build(1);var reduced=NativeRooftopCity.Build(.6f);var low=NativeRooftopCity.Build(.34f);
        Check(w.Quads.Count==again.Quads.Count&&w.Quads.Zip(again.Quads,(a,b)=>a.A==b.A&&a.C==b.C&&a.Attr==b.Attr&&a.Mode==b.Mode).All(x=>x),"the city builds identically every time");
        Check(w.Quads.Count==low.Quads.Count&&w.Screens.Count==low.Screens.Count&&w.LotCount==low.LotCount,"every tier keeps the same city (only the sprinkle thins)");
        Check(w.Quads.All(q=>Finite(q.A)&&Finite(q.B)&&Finite(q.C)&&Finite(q.D)&&Math.Abs(q.Normal.Length()-1)<1e-3f),"every quad is finite with a unit normal");
        // Counter-clockwise about its normal (the Runtime mirrors x and flips the winding; a wrong one would be culled).
        Check(w.Quads.All(q=>Vector3.Dot(Vector3.Cross(q.B-q.A,q.C-q.A),q.Normal)>0),"every quad winds counter-clockwise about its normal");
        int cars=w.Lanes.Sum(l=>l.Count);int sprites(NativeRooftopCity.World x)=>x.Glows.Count+x.Mist.Count+x.Steam.Count+x.Motes.Count+x.Rain.Count;
        int triangles=w.Quads.Count*2+w.Screens.Count*2+cars*24+(sprites(w)+1024)*2;
        Check(triangles<=170000,"within the triangle budget ("+triangles+")");
        Check(sprites(w)>sprites(reduced)&&sprites(reduced)>sprites(low)&&low.Mist.Count==0&&w.Mist.Count>0&&low.Rain.Count<reduced.Rain.Count&&reduced.Rain.Count<w.Rain.Count,"the sprinkle thins: high "+sprites(w)+", reduced "+sprites(reduced)+", low "+sprites(low)+" (no mist)");
        Check(low.Steam.Count<w.Steam.Count&&low.Motes.Count<reduced.Motes.Count&&reduced.Motes.Count<w.Motes.Count,"steam and motes thin with the tier");
        Check(w.Screens.Select(s=>s.Kind).OrderBy(k=>k).SequenceEqual(new[]{"banner","brand","classes","retro","sign","stages"}),"six holo screens: the classes, the stages and the brand rotations, the CHIP banner, the ARENA sign and the old TV set");
        Check(new[]{"runA","runB","blade","gunner","watch"}.All(w.Sites.ContainsKey),"every distant pilot has its place");
        // Without the bank every part draws, and what draws is the city as it was before the art (2026-11-11) to the bit: a hash of
        // every quad's corners, normal, face coordinates, attributes, mode and top, in order, and of every sprite. Two things were
        // added to it that day and are left out of the hash: the old TV set's quads (the easter egg's housing) and its light, which
        // took the stair house lamp's slot. The parts and the notes are not in it.
        ulong hash=14695981039346656037UL;void H(float f){unchecked{hash=(hash^(uint)BitConverter.SingleToInt32Bits(f))*1099511628211UL;}}void H3(Vector3 v){H(v.X);H(v.Y);H(v.Z);}
        for(int qi=0;qi<w.Quads.Count;qi++){if(qi>=w.EggFirst&&qi<w.EggFirst+w.EggCount)continue;var q=w.Quads[qi];H3(q.A);H3(q.B);H3(q.C);H3(q.D);H3(q.Normal);H(q.Uv.X);H(q.Uv.Y);H(q.Uv.Z);H(q.Uv.W);H(q.Attr.X);H(q.Attr.Y);H(q.Attr.Z);H(q.Attr.W);H(q.Mode);H(q.W);}
        var retroScreen=w.Screens.Single(sc=>sc.Kind=="retro");
        foreach(var list in new[]{w.Glows,w.Mist,w.Steam,w.Motes,w.Rain})foreach(var sp in list){if(list==w.Glows&&sp.Size==Math.Max(retroScreen.Width,retroScreen.Height)*2.4f)continue;H3(sp.Position);H(sp.Size);H(sp.Kind);H(sp.Color.X);H(sp.Color.W);}
        Check(hash==0xbfb9e5499c5d3542UL,"the quads and sprites the fallback draws are the city as it was (hash "+hash.ToString("x16")+")");
        Check(w.EggCount==12&&w.Quads.Skip(w.EggFirst).Take(w.EggCount).All(q=>q.Part==NativeRooftopCity.PerchPart&&Vector3.Distance((q.A+q.C)*.5f,NativeRooftopCity.RetroCenter)<1.2f),"the old TV set is two boxes by the pilot, on the perch");
        // The parts the art can stand in for: our roof, and the towers in view with the actors' roofs and the screens' frames.
        int perchQuads=w.Quads.Count(q=>q.Part==NativeRooftopCity.PerchPart),cityQuads=w.Quads.Count(q=>q.Part==NativeRooftopCity.CityPart);
        Check(perchQuads>500&&cityQuads>20000&&w.Quads.Where(q=>q.Mode==NativeRooftopCity.Puddle).All(q=>q.Part==NativeRooftopCity.Far)&&w.Quads.Count(q=>q.Part==NativeRooftopCity.Far)>20000,"the parts: "+perchQuads+" perch and "+cityQuads+" city quads the art can cover; the puddles, the far towers and the street stay quads");
        Check(w.Notes==null&&NativeRooftopCity.Build(1,true).Notes.Count>5000&&NativeRooftopCity.Build(1,true).Quads.Count==w.Quads.Count,"the layout's notes are written only when asked and change nothing");
        Console.WriteLine("PASS Rooftop city: "+w.LotCount+" lots, "+w.Quads.Count+" quads, "+cars+" cars, "+triangles+" triangles; sprites "+sprites(w)+"/"+sprites(reduced)+"/"+sprites(low));

        // 2. A seamless 48 s loop: the camera (and its halved drift), every lane's cars, every actor.
        NativeRooftopCity.Camera(0,out var e0,out var f0);NativeRooftopCity.Camera(NativeRooftopCity.Loop,out var e1,out var f1);
        Check(Near(e0,e1,1e-3f)&&Near(f0,f1,1e-4f),"the camera returns to its start after 48 s");
        NativeRooftopCity.Camera(17,out var eh,out _,.5f);NativeRooftopCity.Camera(17,out var ef,out _);var mean=new Vector3(7.1f,4.45f,4.5f);
        Check(Math.Abs(Vector3.Distance(eh,mean)*2-Vector3.Distance(ef,mean))<1e-3f,"reduced effects halves the camera's breathing");
        for(float t=0;t<NativeRooftopCity.Loop;t+=.5f){NativeRooftopCity.Camera(t,out var e,out var f);if(!Finite(e)||Math.Abs(f.Length()-1)>1e-3f||e.Y<3.5f)Check(false,"the camera stays above the roof looking ahead at "+t);}
        foreach(var lane in w.Lanes)
        {
            Check(lane.Period>0&&Math.Abs(NativeRooftopCity.Loop/lane.Period-Math.Round(NativeRooftopCity.Loop/lane.Period))<1e-4f,"lane period "+lane.Period+" divides the loop");
            for(int k=0;k<lane.Count;k++){float a=NativeRooftopCity.CarU(lane,k,3),b=NativeRooftopCity.CarU(lane,k,3+NativeRooftopCity.Loop);Check(Math.Abs(a-b)<1e-4f||Math.Abs(Math.Abs(a-b)-1)<1e-4f,"cars repeat each loop");}
            var p=NativeRooftopCity.LanePoint(lane,.37f,out var tg);Check(Finite(p)&&Math.Abs(tg.Length()-1)<1e-3f,"lane points are finite with unit tangents");
        }
        // 3. The glance: a near-lane car crosses in front of the pilot while its gaze swings to follow it (28-34 s).
        var nearLane=w.Lanes.Single(l=>l.Near);bool crossing=false;
        for(float t=28.6f;t<=32.6f;t+=.1f)for(int k=0;k<nearLane.Count;k++)if(Math.Abs(NativeRooftopCity.LanePoint(nearLane,NativeRooftopCity.CarU(nearLane,k,t),out _).X)<40)crossing=true;
        Check(crossing,"a near-lane car crosses in front of the pilot during the glance");
        NativeRooftopActors.Gaze(29,out float yawEarly,out _);NativeRooftopActors.Gaze(32.5f,out float yawLate,out _);NativeRooftopActors.Gaze(10,out float yawScan,out float pitchScan);
        Check(yawEarly>.3f&&yawLate<-.2f,"the gaze follows the car from the pilot's left (world -x) to its right");
        for(float t=0;t<NativeRooftopCity.Loop;t+=.25f){NativeRooftopActors.Gaze(t,out float y,out float p);if(Math.Abs(y)>1||p>0||p<-.5f)Check(false,"the gaze stays a natural head turn at "+t);}
        NativeRooftopActors.Gaze(0,out float g0,out float gp0);NativeRooftopActors.Gaze(NativeRooftopCity.Loop,out float g1,out float gp1);Check(Math.Abs(g0-g1)<1e-4f&&Math.Abs(gp0-gp1)<1e-4f,"the gaze loops");

        // 4. Composition: every screen and distant pilot inside the t=0 frame (16:9 and 21:9) with nothing in the way.
        NativeRooftopCity.Camera(0,out var eye,out _);var targets=new List<(string what,Vector3 p)>();
        foreach(var s in w.Screens)if(s.Kind!="sign")targets.Add(("screen "+s.Kind,s.Center));
        foreach(var id in new[]{"blade","gunner","watch"})targets.Add((id,w.Sites[id].Position+new Vector3(0,1.2f,0)));
        foreach(var id in new[]{"runA","runB"}){var site=w.Sites[id];var look=NativeRooftopActors.Crowd.First(c=>c.Id==id);var fwd=new Vector3((float)Math.Sin(site.Yaw),0,(float)Math.Cos(site.Yaw));// runA is seen on the skybridge after its leap; runB on its first roof (its second runs off the frame's right edge).
                float along=id=="runA"?look.L1+look.Gap+look.L2*.5f:look.L1*.5f,drop=id=="runA"?look.Drop:0;targets.Add((id+" running",site.Position+fwd*along+new Vector3(0,1-drop,0)));}
        targets.Add(("the seated pilot",NativeRooftopCity.PilotSeat+new Vector3(0,.9f,0)));
        foreach(var (what,p) in targets)
        {
            foreach(float aspect in new[]{16f/9,3440f/1440}){var sp=Project(p,aspect);Check(sp.X>.04f&&sp.X<.985f&&sp.Y>.04f&&sp.Y<.96f,what+" is in the frame at "+aspect.ToString("0.00")+" ("+sp.X.ToString("0.00")+", "+sp.Y.ToString("0.00")+")");}
            var d=p-eye;float dist=d.Length();d/=dist;float first=float.PositiveInfinity;RooftopQuad by=default;foreach(var q in w.Quads){float h=Hit(eye,d,q);if(h<first){first=h;by=q;}}
            Check(first>=dist-2.5f,what+" is in clear view (first surface at "+first.ToString("0.0")+" of "+dist.ToString("0.0")+" m: mode "+by.Mode+" at "+((by.A+by.C)/2)+" normal "+by.Normal+" target "+p+")");
        }
        Console.WriteLine("PASS Rooftop composition: "+targets.Count+" screens and pilots framed at 16:9 and 21:9 in clear view");

        // 5. The cast: finite poses all loop; the pilot seated on the lip with its legs over the edge; runners vanish into
        //    their huts; every tracer meets the blade's block.
        var seat=new NativeRooftopActors.State();var you=new NativeRooftopActors.Look{Id="you",Spec="rifle",Hat="cap",Body="#e5e7eb",Accent="#22e6ff",Script=NativeRooftopActors.Seated};
        foreach(var spec in new[]{"pistol","rifle","sword","daggers","fire","shield","emperor"})
        {
            you.Spec=spec;NativeRooftopActors.Sample(you,seat,0);float sy=NativeRooftopActors.SeatHeight(seat);var pose=seat.Pose;
            float pelvis=sy+NativePortraitRules.GroupY+pose.WorldPosition[CapturedRig.Pelvis].Y-.10f;Check(Math.Abs(pelvis-NativeRooftopCity.LipTop)<1e-4f,spec+": the pelvis rests on the lip");
            // the pilot faces -z (turned by pi): its local +z ahead is world -z
            foreach(int foot in new[]{CapturedRig.LFoot,CapturedRig.RFoot}){var fp=pose.WorldPosition[foot];float wy=sy+NativePortraitRules.GroupY+fp.Y,wz=NativeRooftopCity.PilotSeat.Z-fp.Z;Check(wy<NativeRooftopCity.LipTop-.15f&&wz<NativeRooftopCity.RoofZ0+.05f,spec+": the feet hang over the edge (y "+wy.ToString("0.00")+", z "+wz.ToString("0.00")+")");}
            Check(!seat.Weapon.Visible&&!seat.Offhand.Visible,spec+": the seated pilot's weapon lies beside it, not in its hands");
        }
        you.Spec="rifle";var looks=new[]{you}.Concat(NativeRooftopActors.Crowd).ToArray();var states=looks.Select(_=>new NativeRooftopActors.State()).ToArray();var hidden=new int[looks.Length];int samples=0;
        for(float t=0;t<NativeRooftopCity.Loop;t+=.2f)for(int i=0;i<looks.Length;i++)
        {
            NativeRooftopActors.Sample(looks[i],states[i],t);samples++;if(states[i].Hidden)hidden[i]++;
            if(!Finite(states[i].Offset)||states[i].Pose.WorldPosition.Take(CapturedRig.Bones.Length).Any(p=>!Finite(p)))Check(false,looks[i].Id+" poses finite at "+t);
        }
        // The scripted pilots (runners, the duel) loop with the scene; the lobby poses (the seated pilot's breathing, the
        // watcher's stance) run on their own idle clocks, so they are checked for smooth motion frame to frame instead.
        for(int i=0;i<looks.Length;i++)
        {
            bool scripted=looks[i].Script==NativeRooftopActors.Runner||looks[i].Script==NativeRooftopActors.Blade||looks[i].Script==NativeRooftopActors.Gunner;
            var a=new NativeRooftopActors.State();var b=new NativeRooftopActors.State();NativeRooftopActors.Sample(looks[i],a,5.3f);NativeRooftopActors.Sample(looks[i],b,5.3f+(scripted?NativeRooftopCity.Loop:1/60f));
            Check(a.Hidden==b.Hidden&&Near(a.Offset,b.Offset,scripted?1e-3f:.2f)&&Enumerable.Range(0,CapturedRig.Bones.Length).All(k=>Near(a.Pose.WorldPosition[k],b.Pose.WorldPosition[k],scripted?2e-3f:.03f)),looks[i].Id+(scripted?" loops in 48 s":" moves smoothly"));
        }
        Check(hidden[0]==0&&hidden.Skip(3).All(h=>h==0),"the seated pilot and the duel never vanish");
        int frames=(int)Math.Ceiling(NativeRooftopCity.Loop/.2f);Check(hidden[1]>0&&hidden[1]<frames/2&&hidden[2]>0&&hidden[2]<frames/2,"the runners vanish into their huts between runs, and run most of the time");
        foreach(var r in NativeRooftopActors.Crowd.Where(c=>c.Script==NativeRooftopActors.Runner))Check(NativeRooftopCity.Loop%r.Cycle==0&&(r.L1+r.Gap+r.L2)/r.Speed<r.Cycle,r.Id+": its run fits its cycle, which divides the loop");
        for(int i=0;i<NativeRooftopActors.Shots.Length;i++)
        {
            float impact=NativeRooftopActors.Shots[i]+NativeRooftopActors.TracerFlight;Check(impact>.7f&&impact+.35f<2.6f+.35f&&impact<2.6f,"shot "+i+" reaches the blade inside its block (.7-2.6 s)");
            Check(NativeRooftopActors.Tracer(NativeRooftopActors.Shots[i]+.06f,i)>0&&NativeRooftopActors.Deflection(impact+.1f,i)>0&&NativeRooftopActors.Tracer(impact+.1f,i)<0,"shot "+i+": the tracer flies, then sparks");
        }
        Console.WriteLine("PASS Rooftop cast: "+looks.Length+" pilots over "+samples+" samples finite and looping; the pilot seated on the lip for 7 specs; "+NativeRooftopActors.Shots.Length+" tracers deflected");

        // 5b. The rework (owner 2026-10-29: "Rework main menu background. Improve the graphics immensely~ utilize lighting ... the
        //     screens better designed on buildings + animated ... the background characters animations better designed ...
        //     building design (It still appears "voxels")"): towers, props, lights, framed animated screens, the crowd's moves.
        var towers=w.Quads.Where(q=>q.Mode>=NativeRooftopCity.Tower&&q.Mode<NativeRooftopCity.Prop).ToList();var props=w.Quads.Where(q=>q.Mode>=NativeRooftopCity.Prop).ToList();
        Check(towers.Count>5000&&props.Count>5000&&w.Quads.Count(q=>q.Mode==NativeRooftopCity.Facade)==0,"the towers draw as facades ("+towers.Count+" quads) with their built detail ("+props.Count+" props); no voxel window boxes remain");
        Check(towers.All(q=>q.Attr.X==Math.Floor(q.Attr.X)&&q.Attr.Y>=0&&q.Attr.Y<=3&&q.Attr.W>0&&q.Attr.W<=1&&q.W>NativeRooftopCity.Street),"every facade carries a whole seed (the shader rounds it back before hashing: no speckle), a style, a lit share and its tower's top");
        Check(towers.Select(q=>(int)q.Attr.Y).Distinct().Count()==4,"all four tower styles stand in the city");
        Check(props.All(q=>q.Attr.W>=0&&q.Attr.W<=6),"every prop names one of the seven materials");
        Check(w.Lights.Count==NativeRooftopCity.LightCount&&w.Lights.Count(l=>l.Screen!=null&&l.Screen!="sign")==5&&w.Lights.Any(l=>l.Screen=="sign")&&w.Lights.Any(l=>l.Screen=="retro"&&l.Reach<2)&&w.Lights.All(l=>Finite(l.Position)&&l.Reach>0),"eight lights: the four big screens', the old TV set's, the corner sign's and our roof's lamps");
        foreach(var l in w.Lights.Where(l=>l.Screen!=null))
        {
            var lo=Enumerable.Range(0,96).Select(k=>NativeRooftopCity.ScreenLight(l,k*.5f).Length()).ToArray();
            Check(lo.Max()>lo.Min()*1.05f&&Near(NativeRooftopCity.ScreenLight(l,7.3f),NativeRooftopCity.ScreenLight(l,7.3f+NativeRooftopCity.Loop),1e-3f),"the "+l.Screen+" screen's light breathes with its panel and loops");
        }
        var rosterContent=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        var rotations=new Dictionary<string,RooftopSlide[]>{["classes"]=NativeRooftopAds.Classes(rosterContent.weapons),["stages"]=NativeRooftopAds.Stages,["brand"]=NativeRooftopAds.Brand};
        foreach(var sc in w.Screens)
        {
            int n=rotations.TryGetValue(sc.Kind,out var set)?set.Length:0;
            for(float t=0;t<NativeRooftopCity.Loop;t+=.25f)
            {
                var a=NativeRooftopCity.ScreenAnim(sc.Kind,t,n);var b=NativeRooftopCity.ScreenAnim(sc.Kind,t+NativeRooftopCity.Loop,n);
                // the breathing, the sweep and the chase loop with the camera; the rotations and the egg on the scene clock's wrap (below)
                if(sc.Kind!="retro"&&Math.Abs(a.Pulse-b.Pulse)>1e-3f||Math.Abs(a.Sweep-b.Sweep)>1e-3f||Math.Abs(a.Chase-b.Chase)>1e-3f||a.Pulse<.6f||a.Pulse>1.25f||a.Cut<0||a.Cut>1||a.Sweep>1||(a.Sweep<0&&a.Sweep!=-1))Check(false,sc.Kind+" screen's animation stays in range and loops at "+t);
            }
            float sec=NativeRooftopCity.SlideSeconds(sc.Kind);
            if(n>0)
            {
                Check(sec>0&&Math.Abs(NativeRooftopAds.Wrap/(sec*n)-Math.Round(NativeRooftopAds.Wrap/(sec*n)))<1e-4f,sc.Kind+": its rotation ("+n+" slides of "+sec+" s) divides the scene clock's wrap");
                var seen=new HashSet<int>();float cutTime=0;
                for(float t=0;t<sec*n;t+=.05f){var a=NativeRooftopCity.ScreenAnim(sc.Kind,t,n);seen.Add(a.Slide);if(a.Cut>.5f)cutTime+=.05f;if(a.Slide<0||a.Slide>=n||a.Age<0||a.Age>sec+1e-3f||Math.Abs(a.Seconds-sec)>1e-4f)Check(false,sc.Kind+": the slide and its age stay in range at "+t);}
                Check(seen.Count==n,sc.Kind+": every slide of the rotation shows ("+seen.Count+" of "+n+")");
                Check(Math.Abs(cutTime-n*NativeRooftopCity.SlideCut)<n*.11f,sc.Kind+": a glitch cut at each slide's ends only ("+cutTime.ToString("0.0")+" s a rotation)");
                var w0=NativeRooftopCity.ScreenAnim(sc.Kind,137.25f,n);var w1=NativeRooftopCity.ScreenAnim(sc.Kind,137.25f+NativeRooftopAds.Wrap,n);
                Check(w0.Slide==w1.Slide&&Math.Abs(w0.Age-w1.Age)<.02f,sc.Kind+": the rotation carries over the scene clock's wrap");
            }
            else Check(sec==0&&NativeRooftopCity.ScreenAnim(sc.Kind,13,0).Seconds==0&&(sc.Kind=="retro"||Enumerable.Range(0,480).All(k=>NativeRooftopCity.ScreenAnim(sc.Kind,k*.1f).Cut==0)),sc.Kind+": a fixed screen has no slides and never cuts");
            if(sc.Kind=="sign"||sc.Kind=="retro")continue;
            int frame=props.Count(q=>Vector3.Distance((q.A+q.C)*.5f,sc.Center)<Math.Max(sc.Width,sc.Height)*.8f+2);
            Check(frame>=40,sc.Kind+" screen hangs in its frame ("+frame+" frame quads: bezel, casing, brackets, lamps)");
        }
        // The easter egg (owner 2026-11-11): the old TV set shows the retired RICOCHET card for EggSeconds of every EggCycle, first
        // within two minutes of the menu; it bounces inside the screen, hits a corner dead on half way through, and takes a new
        // colour at every edge.
        {
            float on=0,first=-1;var tones=new HashSet<int>();float lastHit=0;bool rises=false;
            for(float t=0;t<NativeRooftopCity.EggCycle;t+=.02f)
            {
                var a=NativeRooftopCity.ScreenAnim("retro",t);if(a.EggOn>.5f){on+=.02f;if(first<0)first=t;tones.Add((int)a.EggTone);if(a.EggHit>lastHit+.01f)rises=true;lastHit=a.EggHit;}
                if(a.EggX<-1e-4f||a.EggX>1+1e-4f||a.EggY<-1e-4f||a.EggY>1+1e-4f||a.EggOn<0||a.EggOn>1||a.EggTone<0||a.EggTone>5.001f||a.EggHit<0)Check(false,"the card stays inside the screen at "+t);
            }
            Check(Math.Abs(on-(NativeRooftopCity.EggSeconds-.3f))<.2f&&first>=60&&first<=120,"the card shows "+on.ToString("0.0")+" s of every "+NativeRooftopCity.EggCycle+" s, first "+first.ToString("0")+" s after the menu opens");
            var mid=NativeRooftopCity.ScreenAnim("retro",NativeRooftopCity.EggFrom+NativeRooftopCity.EggSeconds*.5f);
            Check(mid.EggOn>.99f&&mid.EggX<1e-3f&&mid.EggY<1e-3f&&mid.EggHit<1e-3f,"half way through its showing the card hits a corner dead on");
            Check(tones.Count>=5&&rises,"the card takes a new colour at each bounce ("+tones.Count+" colours a showing)");
            Check(Enumerable.Range(0,60).All(k=>NativeRooftopCity.ScreenAnim("retro",k).EggOn==0)&&NativeRooftopCity.ScreenAnim("retro",NativeRooftopCity.EggFrom+.02f).Cut>.9f,"until then the set shows static, and the card arrives through a burst of it");
            Check(NativeRooftopAds.Wrap%NativeRooftopCity.EggCycle==0,"the egg's cycle divides the scene clock's wrap");
        }
        // the duel's footwork: the blade driven back by every deflected tracer, its dash and hop back; the gunner's backpedal
        float zHit0=NativeRooftopActors.DuelOffset("blade",.75f).Z,zHitN=NativeRooftopActors.DuelOffset("blade",2.55f).Z,zDash=NativeRooftopActors.DuelOffset("blade",3.5f).Z;
        Check(zHitN<zHit0-1.5f&&zDash>zHit0+1&&Near(NativeRooftopActors.DuelOffset("blade",0),NativeRooftopActors.DuelOffset("blade",NativeRooftopActors.DuelCycle),1e-3f)&&Near(NativeRooftopActors.DuelOffset("blade",3.99f),NativeRooftopActors.DuelOffset("blade",4.01f),.05f),"the blade is driven back by the burst ("+zHit0.ToString("0.0")+" to "+zHitN.ToString("0.0")+" m), dashes in ("+zDash.ToString("0.0")+") and hops back to its mark, seamlessly");
        Check(NativeRooftopActors.Shots.All(t=>Math.Abs(NativeRooftopActors.DuelOffset("gunner",t).Z)<1e-3f)&&NativeRooftopActors.DuelOffset("gunner",3.3f).Z<-1&&Near(NativeRooftopActors.DuelOffset("gunner",3.99f),NativeRooftopActors.DuelOffset("gunner",4.01f),.05f),"the gunner fires from its mark, backpedals from the dash and walks back before the next burst");
        string roofScene=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeRooftopPresentation.cs"));
        Check(roofScene.Contains("muzzleLocal+NativeRooftopActors.DuelOffset(\"gunner\",time)")&&roofScene.Contains("hitLocal+NativeRooftopActors.DuelOffset(\"blade\",time)"),"the tracers and sparks follow the fighters' footwork");
        // the runners: the level gap's front flip turns the body over; the drop's roll keeps every joint above the bridge
        {
            var runA=NativeRooftopActors.Crowd.First(c=>c.Id=="runA");var runB=NativeRooftopActors.Crowd.First(c=>c.Id=="runB");var st=new NativeRooftopActors.State();bool over=false;float lowest=float.MaxValue;
            for(float x=runB.L1;x<runB.L1+runB.Gap;x+=.2f){NativeRooftopActors.Sample(runB,st,x/runB.Speed-runB.Phase+runB.Cycle);var P=st.Pose.WorldPosition;if(P[CapturedRig.Head].Y<P[CapturedRig.Pelvis].Y)over=true;}
            for(float x=runA.L1+runA.Gap;x<runA.L1+runA.Gap+runA.Speed*.8f;x+=.1f){NativeRooftopActors.Sample(runA,st,x/runA.Speed-runA.Phase);var P=st.Pose.WorldPosition;float joint=P.Take(CapturedRig.Bones.Length).Min(v=>v.Y)+NativePortraitRules.GroupY+st.Offset.Y+runA.Drop;lowest=Math.Min(lowest,joint);}
            Check(over,"runB front-flips over its gap (head below the hips mid-air)");
            Check(lowest>-.02f,"runA rolls out of its drop with every joint on or above the bridge (lowest "+lowest.ToString("0.00")+" m)");
        }
        Console.WriteLine("PASS Rooftop rework: "+towers.Count+" facade and "+props.Count+" prop quads in four styles, "+w.Lights.Count+" lights breathing with their screens, framed animated screens, the duel's footwork, the flip and the roll");

        // 6. The art: the ads and the wordmark rasterise through the engine's own rasteriser.
        var everySlide=rotations.Values.SelectMany(x=>x).ToArray();string used=string.Concat(everySlide.Select(x=>x.Title+x.Sub+x.Kicker+x.Ticker))+"CHIP ARENA RICOCHET RARE";Check(used.All(c=>c==' '||NativeRooftopArt.Has(c))&&"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-".All(NativeRooftopArt.Has),"every letter the art uses has a glyph (the alphabet, the digits, the hyphen)");
        Check(NativeRooftopArt.Transform("M1 1H5V7Z",2,10,20)=="M12 22 H20 V34 Z","glyph paths scale and offset");
        foreach(char c in "CHIPARENA"){var cover=NativeIconRaster.Coverage(NativeIconRaster.Parse(NativeRooftopArt.Letter(c,2.4f),1,NativeIconRaster.Mode.Markup),48,NativeRooftopArt.GlyphHeight);float sum=cover.Sum()/cover.Length;Check(sum>.02f&&sum<.35f,"wordmark letter "+c+" rasterises as a stroke ("+sum.ToString("0.000")+")");}
        var watch=Stopwatch.StartNew();long slowest=0;
        foreach(var kind in new[]{"ricochet","banner","sign"})
        {
            var sw=Stopwatch.StartNew();int px=NativeRooftopArt.AdPixels;var rgba=NativeIconRaster.Raster(NativeIconRaster.Parse(NativeRooftopArt.Ad(kind),1,NativeIconRaster.Mode.Markup),px,100);slowest=Math.Max(slowest,sw.ElapsedMilliseconds);
            var uv=NativeRooftopArt.AdUv(kind);int x0=(int)Math.Ceiling(uv.u0*px)+2,x1=(int)Math.Floor((uv.u0+uv.du)*px)-2,y0=(int)Math.Ceiling(uv.v0*px)+2,y1=(int)Math.Floor((uv.v0+uv.dv)*px)-2;
            double alpha=0,ink=0;int n=0;float br=rgba[(y0*px+x0)*4],bg=rgba[(y0*px+x0)*4+1],bb=rgba[(y0*px+x0)*4+2];
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){int k=(y*px+x)*4;alpha+=rgba[k+3];if(Math.Abs(rgba[k]-br)+Math.Abs(rgba[k+1]-bg)+Math.Abs(rgba[k+2]-bb)>.25f)ink++;n++;}
            Check(alpha/n>.99&&ink/n>.03&&ink/n<.6,kind+" ad: opaque over its screen with its drawing on it ("+(ink/n).ToString("0.000")+" drawn)");
        }
        Check(NativeRooftopArt.Ad("siren").Length==0&&NativeRooftopArt.Ad("chip").Length==0&&NativeRooftopArt.Ad("ricochet").Contains("stroke=\"#70b5ff\""),"the Siren's Boss Test ad is gone and the RICOCHET RARE card is the old TV set's alone");
        // 6b. The rotating ads (owner 2026-11-11: "Changing animations happening on the TV screens as well, since things on there are
        //     out dated"): the public classes, the nine remodelled stages, the Expedition and the logo, and nothing DEV only or
        //     unreleased.
        {
            var publicWeapons=rosterContent.weapons.Where(x=>!x.devOnly).ToArray();var classSlides=rotations["classes"].Where(x=>x.Kind==RooftopSlide.Class).ToArray();
            Check(publicWeapons.Length>=14&&classSlides.Select(x=>x.Id).SequenceEqual(publicWeapons.Select(x=>x.id)),"a slide for each public class, in the content's order ("+classSlides.Length+")");
            Check(classSlides.All(x=>x.Title==NativeRooftopAds.Words(publicWeapons.First(p=>p.id==x.Id).name)&&x.Title.Length>0&&(x.Family=="ranged"||x.Family=="melee"||x.Family=="magic")),"each class slide carries its weapon's name and its family's pose");
            Check(new HashSet<string>(classSlides.Select(x=>x.Family)).Count==3,"all three families' poses appear");
            Check(rotations["classes"].Skip(classSlides.Length).All(x=>x.Kind==RooftopSlide.House&&x.Title==classSlides.Length+" CLASSES")&&rotations["classes"].Length>classSlides.Length,"the roster's house slides fill the rotation, counting the public classes");
            Check(NativeRooftopAds.Stages.Where(x=>x.Kind==RooftopSlide.Stage).Select(x=>x.Id).SequenceEqual(new[]{"breakwater","harbor","skyframe","rig","polar","brickfall","siren","factory","cybercity"})&&NativeRooftopAds.Stages.Count(x=>x.Kind==RooftopSlide.Mode&&x.Id=="expedition")==1,"the nine remodelled stages and the Expedition");
            Check(NativeRooftopAds.Stages.Where(x=>x.Kind==RooftopSlide.Stage).Select(x=>x.Title).SequenceEqual(new[]{"BREAKWATER","PIER 9","SKYFRAME","DEEPWELL","POLARIS STATION","BRICKFALL","WARD ZERO","THE FACTORY","CYBER CITY"}),"the stages by the names players see");
            Check(NativeRooftopAds.Brand.Count(x=>x.Kind==RooftopSlide.Logo)==2&&NativeRooftopAds.Brand.Length==4,"the brand screen shows the logo between two teasers");
            // nothing DEV only or unreleased: no DEV-only weapon's name or id, no boss
            var unreleased=rosterContent.weapons.Where(x=>x.devOnly).SelectMany(x=>new[]{NativeRooftopAds.Words(x.name),x.id.ToUpperInvariant()}).Concat(new[]{"NOCTIEL","LUX","CECIDIT","BOSS","SIREN","THE HUM","DEEP END","CONVERTER","6-47","DEV"}).Where(x=>x.Length>0).ToArray();
            foreach(var x in everySlide){string all=" "+x.Title+" "+x.Sub+" "+x.Kicker+" "+x.Ticker+" ";foreach(var h in unreleased)Check(!all.Contains(" "+h+" ")&&!(h.Length>4&&all.Contains(h)),"the "+x.Id+" slide does not name "+h);}
            Check(rosterContent.weapons.Where(x=>x.devOnly).All(x=>everySlide.All(y=>y.Kind!=RooftopSlide.Class||y.Id!=x.id)),"no class slide is a DEV-only class's");
            // every slide rasterises: a drawn picture in each band, a mask that covers some of the picture and none of the bands
            var sw2=Stopwatch.StartNew();int slideCount=0;long slowSlide=0;
            foreach(var kv in rotations)foreach(var x in kv.Value)
            {
                var uv=NativeRooftopArt.AdUv(kv.Key);float y0=uv.v0*100,y1=(uv.v0+uv.dv)*100;int px=NativeRooftopAds.Pixels;var one=Stopwatch.StartNew();
                var rgba=NativeRooftopAds.Compose(x,x.Kind==RooftopSlide.Class?"<path d=\"M4 4H20V20H4Z\" fill=\"#fff\"/>":null,24,y0,y1);slowSlide=Math.Max(slowSlide,one.ElapsedMilliseconds);slideCount++;
                Check(rgba.Length==px*px*4&&rgba.All(float.IsFinite),x.Id+" slide rasterises");
                // rows from the top of the visible rect: the picture, the title band, the ticker
                double Ink(float f0,float f1,out double mask){double ink=0;mask=0;int n=0;int r0=(int)((1-(y0+(y1-y0)*f1)/100)*px),r1=(int)((1-(y0+(y1-y0)*f0)/100)*px);float br=rgba[(r0*px+2)*4],bg=rgba[(r0*px+2)*4+1],bb=rgba[(r0*px+2)*4+2];
                    for(int y=r0+1;y<r1-1;y++)for(int xx=2;xx<px-2;xx++){int k=(y*px+xx)*4;if(Math.Abs(rgba[k]-br)+Math.Abs(rgba[k+1]-bg)+Math.Abs(rgba[k+2]-bb)>.2f)ink++;mask+=rgba[k+3];n++;}mask/=n;return ink/n;}
                if(x.Kind==RooftopSlide.Logo){Check(Ink(0,1,out double lm)>.03&&lm==0,x.Id+": the logo's slide is all picture, no mask");continue;}
                double pic=Ink(0,NativeRooftopAds.PictureFoot,out double picMask),title=Ink(NativeRooftopAds.PictureFoot+.02f,NativeRooftopAds.TitleFoot,out double titleMask),tick=Ink(NativeRooftopAds.TickerTop,1,out double tickMask);
                Check(title>.04&&title<.6&&tick>.03&&tick<.7,x.Id+": its title and its ticker are drawn in their bands ("+title.ToString("0.00")+", "+tick.ToString("0.00")+")");
                Check(picMask>.03&&picMask<.5&&titleMask<.002&&tickMask<.002,x.Id+": its mask strikes over the picture alone ("+picMask.ToString("0.000")+" of it)");
            }
            Check(NativeRooftopAds.Bands(NativeRooftopAds.Brand[0])==(1f,1f,1f)&&NativeRooftopAds.Bands(NativeRooftopAds.Stages[0]).picture==NativeRooftopAds.PictureFoot,"the shader takes each slide's bands (a logo is all picture)");
            Console.WriteLine("PASS Rooftop ads: "+slideCount+" slides ("+classSlides.Length+" public classes, 9 stages, the Expedition, the logo) rasterise at "+NativeRooftopAds.Pixels+" px in "+sw2.ElapsedMilliseconds+" ms here, slowest "+slowSlide+" ms; none names a DEV-only class or a boss");
        }
        Console.WriteLine("PASS Rooftop art: 9 wordmark letters and 3 fixed ads rasterise ("+NativeRooftopArt.AdPixels+" px ads in "+watch.ElapsedMilliseconds+" ms here, slowest "+slowest+" ms; the engine rasterises them on worker threads)");

        // 7. Source checks: the routing, every home function, the updater card's states and the cyber palette.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string home=R("NativeHomeMenu.cs"),game=R("ConversionGame.cs"),update=R("NativeUpdateUI.cs"),scene=R("NativeRooftopPresentation.cs");
        Check(game.Contains("if(menuScreen==MenuScreen.Home&&!InRoom&&presentation!=null&&presentation.RooftopSupported)")&&game.Contains("RooftopLeave();\n            string map=PreviewMapId;")&&game.Contains("if(arena==null){MenuBackdrop();return;}RooftopLeave();backdropMap=null;"),"the rooftop is the home screen's backdrop only; the other screens and a match get the map look back");
        Check(game.Contains("cameraView.fieldOfView=rooftopFov;cameraView.nearClipPlane=rooftopNear;backdropMap=null;"),"leaving the rooftop restores the camera and re-applies the map look");
        foreach(var go in new[]{"MenuScreen.Play","MenuScreen.Create","MenuScreen.Join","MenuScreen.Boss","MenuScreen.Customize","MenuScreen.Settings","MenuScreen.Account","MenuScreen.Dev","MenuScreen.Catalogue"})Check(home.Contains(go+")")||home.Contains(go+";"),"the home screen still reaches "+go);
        Check(home.Contains("HomeItems={(\"boss\",\"START EXPEDITION\",\"01\",MenuScreen.Boss),(\"play\",\"PLAY VS BOTS\",\"02\",MenuScreen.Play)")&&home.Contains("Go(HomeItems[k].Item4);")&&!home.Contains("\"BOSS TEST\""),"Start Expedition is the top home entry (key 1), the versus rooms follow and the keys keep the order (owner 2026-10-15)");
        Check(home.Contains("{Save();Application.Quit();}")&&home.Contains("OpenConnectionReport();")&&home.Contains("HomeKeys();")&&home.Contains("Go(MenuScreen.Play,true)")&&home.Contains("accountNotice=\"Log in once to activate registered DEV tools.\""),"Quit, the Connection report, keys 1-4, the Sandbox and the DEV login redirect stay");
        Check(home.Contains("if(busy)updateOpen=true;")&&home.Contains("u.CheckForUpdates();")&&home.Contains("UpdatePanel(updateAnchor.x,updateAnchor.y)")&&update.Contains("u.CanInstall")&&update.Contains("u.CanCancel")&&update.Contains("u.InstallAvailableUpdate()")&&update.Contains("u.Cancel()"),"the update card keeps CanInstall, CanCancel and Busy, forced open while busy");
        Check(home.Contains("UpdateBeatAt(ready,out float kick,out float breathe,out float wave);")&&home.Contains("kick=ready&&!calm?Mathf.Exp(-6*beat):0;")&&home.Contains("wave=ready&&!calm?beat:-1;")&&home.Contains("bool calm=prefs.reducedEffects;"),"an update waiting beats on the home circle (halo, sonar rings, kick, pip); reduced effects keep only the breathing halo");
        Check(home.Contains("float ww=Wordmark(30,31,16,1,-1);UpdatePill(30+ww+22,27);")&&home.Contains("if(!ready&&!busy)return;")&&home.Contains("bool can=ready&&!InRoom&&!MenuBlocked();")&&home.Contains("Go(MenuScreen.Home);updateOpen=true;"),"the panel screens show the waiting update as a pill that opens the update card at home (in a room it only informs)");
        Check(!home.Contains("VOXEL COMBAT")&&home.Contains("Wordmark(x0,wy,gh,ce,age)")&&home.Contains("\"PROTOTYPE\""),"the vector wordmark, no tagline, PROTOTYPE in the footer");
        Check(home.Contains("var mark=NativeRooftopScene.Logo();")&&scene.Contains("logo=Resources.Load<Texture2D>(\"ChipArenaLogo\")")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/ChipArenaLogo.png"))&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/ChipArenaLogo.png.meta"))&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/ChipArenaLogo.png.meta")).Contains("isReadable: 1"),"the game's logo (owner 2026-11-11) is the title wherever the wordmark was, and readable for the brand screen's slide; the vector wordmark stays as its fallback");
        Check(home.Contains("Go(MenuScreen.Customize);}")&&home.Contains("NativeIcons.Spec(weapon.id)"),"the nameplate opens Customize and shows the class icon");
        string[] orange={"0xff6b35","0xff865b","0xff784b","0xff8a5b","0xff996f","0xffb15c","0xffc590","0xefb587","0xffac68","0xffc4a3","0x36251f"};
        foreach(var f in new[]{"NativeHomeMenu.cs","NativeUi.cs","NativeMenuSkin.cs","NativeUpdateUI.cs","NativeLobbyUI.cs","NativeRoomOptionsUI.cs"}){var text=R(f);var left=orange.Where(o=>text.Contains(o)).ToArray();Check(left.Length==0,f+" has no orange left ("+string.Join(",",left)+")");Check(text.Contains("NativeCyberTheme"),f+" draws with NativeCyberTheme");}
        Check(scene.Contains("NativeRooftopCity.Build(quality)")&&scene.Contains("tri[t]=v;tri[t+1]=v+2;tri[t+2]=v+1;")&&scene.Contains("new Vector3(-v.X,v.Y,v.Z)"),"the Runtime mirrors x and flips the winding at the boundary");
        foreach(var s in new[]{"NativeRooftop","NativeRooftopSky","NativeRooftopScreen","NativeRooftopSprite","NativeRooftopFacade","NativeRooftopProp"}){var path=Path.Combine(root,"Assets/ChipArena/Resources",s+".shader");Check(File.Exists(path)&&File.Exists(path+".meta")&&File.ReadAllText(path).Contains("Shader \"ChipArena/"+s+"\""),s+".shader is in Resources with its meta");}
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeRooftopCommon.cginc.meta"))&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeRooftopLights.cginc.meta")),"the shared includes have their metas");
        {
            string res(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources",f));string facade=res("NativeRooftopFacade.shader"),prop=res("NativeRooftopProp.shader"),screenShader=res("NativeRooftopScreen.shader");
            Check(facade.Contains("float seed=frac(floor(i.uv1.x+.5)*.6180339)*97.0")&&facade.Contains("float2 fw=max(fwidth(g),float2(1e-4,1e-4))"),"the facade rounds its seed back before hashing and takes its footprint before any flow control");
            Check(facade.Contains("#include \"NativeRooftopLights.cginc\"")&&prop.Contains("#include \"NativeRooftopLights.cginc\"")&&facade.Contains("Tags { \"LightMode\"=\"ShadowCaster\" }")&&prop.Contains("Tags { \"LightMode\"=\"ShadowCaster\" }"),"the facades and props share the city's lights and write the depth the outline reads");
            Check(screenShader.Contains("_Kind(\"Kind\",Float)")&&screenShader.Contains("_Anim(\"Animation\",Vector)")&&screenShader.Contains("_Slide(\"Slide\",Vector)")&&screenShader.Contains("_Egg(\"Egg\",Vector)")&&scene.Contains("b.SetVector(AnimId,new Vector4(a.Pulse,a.Cut,a.Sweep,a.Chase));")&&scene.Contains("b.SetVector(EggId,new Vector4(a.EggX,a.EggY,a.EggOn,a.EggTone));")&&scene.Contains("NativeRooftopCity.ScreenLight(l,time)"),"the screens animate from Core's ScreenAnim each frame and their lights breathe with them");
            Check(scene.Contains("NativeRooftopAds.Classes(Weapons())")&&scene.Contains("Task.Run(()=>NativeRooftopAds.Compose(slide,icon,box,y0,y1))")&&scene.Contains("uploads<2")&&game.Contains("Time.unscaledTimeAsDouble%4800.0")&&NativeRooftopAds.Wrap==4800,"the slides rasterise on worker threads, two uploads a frame at most, on the game's 4800 s scene clock");
            // the modelled art (docs/MENU-ROOFTOP-ART.md): drawn in place of the parts its bank covers; nothing changes without it
            string models=R("NativeRooftopModels.cs"),modelShader=res("NativeRooftopModel.shader");
            Check(scene.Contains("models=NativeRooftopModels.Load(quality<.5f);")&&scene.Contains("q.Mode>=from&&q.Mode<to&&(plan==null||!plan.Covers(q.Part))")&&scene.Contains("models?.Draw(camera,towerMaterial);")&&scene.Contains("if(models!=null&&models.HasCars)models.Cars(camera,World.Lanes,time);"),"the scene draws the art in place of the quads its bank covers, and the box cars without it");
            Check(models.Contains("if(NativePackBanks.Off)return null;")&&models.Contains("var asset=Resources.Load<TextAsset>(NativeRooftopBank.Name);if(asset==null){job=null;return;}")&&models.Contains("Resources.UnloadAsset(asset)")&&models.Contains("if(task==null)return null;"),"without the bank (or with pack banks off) there is no art and every quad draws");
            Check(modelShader.Contains("Shader \"ChipArena/NativeRooftopModel\"")&&modelShader.Contains("#include \"NativeRooftopLights.cginc\"")&&modelShader.Contains("Tags { \"LightMode\"=\"ShadowCaster\" }")&&modelShader.Contains("#pragma multi_compile_instancing")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeRooftopModel.shader.meta")),"the model shader shares the city's lights, writes the outline's depth and is instanced");
            RooftopBankChecks(root,w);
        }
        Console.WriteLine("PASS Rooftop home menu: "+count+" checks (routing, functions, updater states, palette, shaders) in "+total.ElapsedMilliseconds+" ms");
    }
}

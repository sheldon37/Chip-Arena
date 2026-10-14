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
        Check(w.Screens.Select(s=>s.Kind).OrderBy(k=>k).SequenceEqual(new[]{"arena","banner","chip","sign","siren"}),"five holo screens: Chip Arena, the CHIP banner, the chip card, the ARENA sign and the Siren");
        Check(new[]{"runA","runB","blade","gunner","watch"}.All(w.Sites.ContainsKey),"every distant pilot has its place");
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

        // 6. The art: the ads and the wordmark rasterise through the engine's own rasteriser.
        string Glyphs="CHIPARENBOST ";Check("CHIP ARENA THE SIREN BOSS TEST RICOCHET RARE".All(NativeRooftopArt.Has)&&Glyphs.All(NativeRooftopArt.Has),"every letter the art uses has a glyph");
        Check(NativeRooftopArt.Transform("M1 1H5V7Z",2,10,20)=="M12 22 H20 V34 Z","glyph paths scale and offset");
        foreach(char c in "CHIPARENA"){var cover=NativeIconRaster.Coverage(NativeIconRaster.Parse(NativeRooftopArt.Letter(c,2.4f),1,NativeIconRaster.Mode.Markup),48,NativeRooftopArt.GlyphHeight);float sum=cover.Sum()/cover.Length;Check(sum>.02f&&sum<.35f,"wordmark letter "+c+" rasterises as a stroke ("+sum.ToString("0.000")+")");}
        var watch=Stopwatch.StartNew();long slowest=0;
        foreach(var kind in new[]{"siren","arena","chip","banner","sign"})
        {
            var sw=Stopwatch.StartNew();int px=NativeRooftopArt.AdPixels;var rgba=NativeIconRaster.Raster(NativeIconRaster.Parse(NativeRooftopArt.Ad(kind),1,NativeIconRaster.Mode.Markup),px,100);slowest=Math.Max(slowest,sw.ElapsedMilliseconds);
            var uv=NativeRooftopArt.AdUv(kind);int x0=(int)Math.Ceiling(uv.u0*px)+2,x1=(int)Math.Floor((uv.u0+uv.du)*px)-2,y0=(int)Math.Ceiling(uv.v0*px)+2,y1=(int)Math.Floor((uv.v0+uv.dv)*px)-2;
            double alpha=0,ink=0;int n=0;float br=rgba[(y0*px+x0)*4],bg=rgba[(y0*px+x0)*4+1],bb=rgba[(y0*px+x0)*4+2];
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){int k=(y*px+x)*4;alpha+=rgba[k+3];if(Math.Abs(rgba[k]-br)+Math.Abs(rgba[k+1]-bg)+Math.Abs(rgba[k+2]-bb)>.25f)ink++;n++;}
            Check(alpha/n>.99&&ink/n>.03&&ink/n<.6,kind+" ad: opaque over its screen with its drawing on it ("+(ink/n).ToString("0.000")+" drawn)");
        }
        Console.WriteLine("PASS Rooftop art: 9 wordmark letters and 5 ads rasterise ("+NativeRooftopArt.AdPixels+" px ads in "+watch.ElapsedMilliseconds+" ms here, slowest "+slowest+" ms; the engine rasterises them on worker threads)");

        // 7. Source checks: the routing, every home function, the updater card's states and the cyber palette.
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string home=R("NativeHomeMenu.cs"),game=R("ConversionGame.cs"),update=R("NativeUpdateUI.cs"),scene=R("NativeRooftopPresentation.cs");
        Check(game.Contains("if(menuScreen==MenuScreen.Home&&!InRoom&&presentation!=null&&presentation.RooftopSupported)")&&game.Contains("RooftopLeave();\n            string map=PreviewMapId;")&&game.Contains("if(arena==null){MenuBackdrop();return;}RooftopLeave();backdropMap=null;"),"the rooftop is the home screen's backdrop only; the other screens and a match get the map look back");
        Check(game.Contains("cameraView.fieldOfView=rooftopFov;cameraView.nearClipPlane=rooftopNear;backdropMap=null;"),"leaving the rooftop restores the camera and re-applies the map look");
        foreach(var go in new[]{"MenuScreen.Play","MenuScreen.Create","MenuScreen.Join","MenuScreen.Boss","MenuScreen.Customize","MenuScreen.Settings","MenuScreen.Account","MenuScreen.Dev","MenuScreen.Catalogue"})Check(home.Contains(go+")")||home.Contains(go+";"),"the home screen still reaches "+go);
        Check(home.Contains("{Save();Application.Quit();}")&&home.Contains("OpenConnectionReport();")&&home.Contains("HomeKeys();")&&home.Contains("playSandbox=true;")&&home.Contains("accountNotice=\"Log in once to activate registered DEV tools.\""),"Quit, the Connection report, keys 1-4, the Sandbox and the DEV login redirect stay");
        Check(home.Contains("if(busy)updateOpen=true;")&&home.Contains("u.CheckForUpdates();")&&home.Contains("UpdatePanel(updateAnchor.x,updateAnchor.y)")&&update.Contains("u.CanInstall")&&update.Contains("u.CanCancel")&&update.Contains("u.InstallAvailableUpdate()")&&update.Contains("u.Cancel()"),"the update card keeps CanInstall, CanCancel and Busy, forced open while busy");
        Check(home.Contains("UpdateBeatAt(ready,out float kick,out float breathe,out float wave);")&&home.Contains("kick=ready&&!calm?Mathf.Exp(-6*beat):0;")&&home.Contains("wave=ready&&!calm?beat:-1;")&&home.Contains("bool calm=prefs.reducedEffects;"),"an update waiting beats on the home circle (halo, sonar rings, kick, pip); reduced effects keep only the breathing halo");
        Check(home.Contains("float ww=Wordmark(30,31,16,1,-1);UpdatePill(30+ww+22,27);")&&home.Contains("if(!ready&&!busy)return;")&&home.Contains("bool can=ready&&!InRoom&&!MenuBlocked();")&&home.Contains("Go(MenuScreen.Home);updateOpen=true;"),"the panel screens show the waiting update as a pill that opens the update card at home (in a room it only informs)");
        Check(!home.Contains("VOXEL COMBAT")&&home.Contains("Wordmark(x0,wy,gh,ce,age)")&&home.Contains("\"PROTOTYPE\""),"the vector wordmark, no tagline, PROTOTYPE in the footer");
        Check(home.Contains("Go(MenuScreen.Customize);}")&&home.Contains("NativeIcons.Spec(weapon.id)"),"the nameplate opens Customize and shows the class icon");
        string[] orange={"0xff6b35","0xff865b","0xff784b","0xff8a5b","0xff996f","0xffb15c","0xffc590","0xefb587","0xffac68","0xffc4a3","0x36251f"};
        foreach(var f in new[]{"NativeHomeMenu.cs","NativeUi.cs","NativeMenuSkin.cs","NativeUpdateUI.cs","NativeLobbyUI.cs","NativeRoomOptionsUI.cs"}){var text=R(f);var left=orange.Where(o=>text.Contains(o)).ToArray();Check(left.Length==0,f+" has no orange left ("+string.Join(",",left)+")");Check(text.Contains("NativeCyberTheme"),f+" draws with NativeCyberTheme");}
        Check(scene.Contains("NativeRooftopCity.Build(quality)")&&scene.Contains("tri[t]=v;tri[t+1]=v+2;tri[t+2]=v+1;")&&scene.Contains("new Vector3(-v.X,v.Y,v.Z)"),"the Runtime mirrors x and flips the winding at the boundary");
        foreach(var s in new[]{"NativeRooftop","NativeRooftopSky","NativeRooftopScreen","NativeRooftopSprite"}){var path=Path.Combine(root,"Assets/ChipArena/Resources",s+".shader");Check(File.Exists(path)&&File.Exists(path+".meta")&&File.ReadAllText(path).Contains("Shader \"ChipArena/"+s+"\""),s+".shader is in Resources with its meta");}
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeRooftopCommon.cginc.meta")),"the shared include has its meta");
        Console.WriteLine("PASS Rooftop home menu: "+count+" checks (routing, functions, updater states, palette, shaders) in "+total.ElapsedMilliseconds+" ms");
    }
}

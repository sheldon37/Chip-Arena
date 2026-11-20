using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ChipArena.UnityConversion;

// The bleed's checks (docs/MERIDIAN-BLEED.md): Core NativeCityBleed (the bands) and NativeCityBleedDress (the approach
// dressing's plan) on all five meridian-b-candidate-3 layouts, each with five deals that put every instance behind every door:
//  - the bands: the share curve through the plan's band edges, the band thresholds and the theme ids;
//  - no seams: a share moves at most 0.08 per 25 cm anywhere (the plan's bound), measured at random places on every floor;
//  - along the route: each door's street reads the plan's distances (threshold to 30 m, warp to 60, drift to 110, rumour to 160);
//  - deterministic per seed and deal (two builds are the same, piece for piece);
//  - every piece inside the placement rules, measured against the map's boxes by this file's own geometry: paint 6 cm or less
//    and wholly on a floor's top; wall pieces on a face found from the street (no more than 0.25 m short of it), flat below
//    3.1 m over the floor, inside no other box; spans and hung pieces 3.1 m or more over every floor below and inside no box;
//    nothing within 3.3 m of a join circle or inside a door front;
//  - coverage: every instance's street has its rumour, drift, warp and threshold pieces and its lamps;
//  - budgets: pieces per walk and per view, draws, and the lamp rule (about 1 in 5 dead, 1 in 10 flickering).
internal static class BleedChecks
{
    private static readonly string[][] Deals=
    {
        new[]{"Level 0","The Poolrooms","Ward Zero"},new[]{"The Poolrooms","Ward Zero","Central Station"},
        new[]{"Ward Zero","Central Station","City Ironworks"},new[]{"Central Station","City Ironworks","Level 0"},
        new[]{"City Ironworks","Level 0","The Poolrooms"},
    };
    // Budgets: pieces in one walk, within 60 m of one eye, and instanced draws (500 instances a draw, two tiers).
    private const int MaxPieces=6000,MaxView=2600,MaxDraws=180;

    public static void Run(string root)
    {
        Curve();
        int walks=0,pieces=0,maxPieces=0,maxView=0,maxDraws=0,lamps=0,dead=0,flicker=0;float worstSeam=0;
        var perTheme=new Dictionary<string,int[]>();
        for(int layout=0;layout<NativeCityLayout.Plans.Count;layout++)
        {
            var walk=new NativeLayeredCityWalk(NativeCityLayout.Plans[layout]);var map=walk.Map;
            for(int deal=0;deal<Deals.Length;deal++)
            {
                uint seed=NativeCityBleedDress.DealSeedFor((uint)(1000*layout+37*deal),Deals[deal]);
                var slots=NativeCityLayout.DealHalls(seed,Deals[deal]);
                if(slots[0]!="Final court"||slots[1]!=Deals[deal][0]||slots[2]!=Deals[deal][1]||slots[3]!=Deals[deal][2])throw new Exception("The picker's deal seed does not deal "+string.Join("/",Deals[deal]));
                var bleed=NativeCityBleed.ForWalk(walk,slots);
                worstSeam=Math.Max(worstSeam,Seams(map,bleed,seed));
                Along(walk,bleed);
                var plan=NativeCityBleedDress.ForWalk(walk,bleed,seed);var again=NativeCityBleedDress.ForWalk(walk,NativeCityBleed.ForWalk(walk,slots),seed);
                Same(plan,again,map.name+" "+string.Join("/",Deals[deal]));
                Rules(map,bleed,plan);
                Coverage(map,bleed,plan,perTheme);
                int view=View(walk,plan),draws=Draws(plan);
                if(plan.Pieces.Count>MaxPieces)throw new Exception(map.name+": "+plan.Pieces.Count+" bleed pieces, over "+MaxPieces);
                if(view>MaxView)throw new Exception(map.name+": "+view+" bleed pieces within 60 m of one eye, over "+MaxView);
                if(draws>MaxDraws)throw new Exception(map.name+": "+draws+" bleed draws, over "+MaxDraws);
                walks++;pieces+=plan.Pieces.Count;maxPieces=Math.Max(maxPieces,plan.Pieces.Count);maxView=Math.Max(maxView,view);maxDraws=Math.Max(maxDraws,draws);
                foreach(var p in plan.Pieces)
                {
                    if(p.Lamp==NativeCityBleedDress.LampState.None||p.Lamp==NativeCityBleedDress.LampState.Turning)continue;
                    lamps++;if(p.Lamp==NativeCityBleedDress.LampState.Dead)dead++;else if(p.Lamp==NativeCityBleedDress.LampState.Flicker)flicker++;
                }
            }
        }
        float deadShare=(float)dead/lamps,flickerShare=(float)flicker/lamps;
        if(deadShare<.12f||deadShare>.3f||flickerShare<.04f||flickerShare>.18f)throw new Exception("The lamp rule: "+dead+" dead and "+flicker+" flickering of "+lamps);
        // a seed changes the variants and the failing lamps, never where the pieces stand
        var w0=new NativeLayeredCityWalk(NativeCityLayout.Plans[0]);var s0=NativeCityLayout.DealHalls(NativeCityBleedDress.DealSeedFor(5,Deals[0]),Deals[0]);var b0=NativeCityBleed.ForWalk(w0,s0);
        var p1=NativeCityBleedDress.ForWalk(w0,b0,11);var p2=NativeCityBleedDress.ForWalk(w0,b0,12);
        if(p1.Pieces.Select(Key).SequenceEqual(p2.Pieces.Select(Key)))throw new Exception("The run seed changes nothing in the bleed");
        Console.WriteLine("PASS bleed bands: share curve, thresholds and themes; worst seam "+worstSeam.ToString("0.0000",CultureInfo.InvariantCulture)+" per 25 cm (bound 0.08)");
        Console.WriteLine("PASS bleed dressing: "+walks+" walks (5 layouts x 5 deals), "+pieces+" pieces inside the placement rules, deterministic; most "+maxPieces+" a walk, "+maxView+" within 60 m of an eye, "+maxDraws+" draws");
        Console.WriteLine("PASS bleed lamps: "+lamps+" lamps, "+dead+" dead ("+(100*deadShare).ToString("0")+"%), "+flicker+" flickering ("+(100*flickerShare).ToString("0")+"%)");
        Console.WriteLine("PASS bleed coverage: "+string.Join("; ",perTheme.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+" rumour "+kv.Value[1]+" drift "+kv.Value[2]+" warp "+kv.Value[3]+" threshold "+kv.Value[4]+" lamps "+kv.Value[5])));
    }

    private static void Curve()
    {
        void Near(float a,float b,string what){if(Math.Abs(a-b)>1e-4f)throw new Exception("Bleed curve "+what+": "+a+" against "+b);}
        Near(NativeCityBleed.ShareAt(0),1,"at the door");Near(NativeCityBleed.ShareAt(30),.70f,"at 30 m");Near(NativeCityBleed.ShareAt(60),.45f,"at 60 m");
        Near(NativeCityBleed.ShareAt(110),.25f,"at 110 m");Near(NativeCityBleed.ShareAt(160),.10f,"at 160 m");Near(NativeCityBleed.ShareAt(250),0,"far off");
        if(NativeCityBleed.Band(.69f)!=NativeCityBleed.Bands.Warp||NativeCityBleed.Band(.7f)!=NativeCityBleed.Bands.Threshold||NativeCityBleed.Band(.45f)!=NativeCityBleed.Bands.Warp||
           NativeCityBleed.Band(.25f)!=NativeCityBleed.Bands.Drift||NativeCityBleed.Band(.1f)!=NativeCityBleed.Bands.Rumour||NativeCityBleed.Band(.099f)!=NativeCityBleed.Bands.None)throw new Exception("Bleed band thresholds");
        for(float m=0;m<260;m+=.25f)if(NativeCityBleed.ShareAt(m+.25f)>NativeCityBleed.ShareAt(m)+1e-6f)throw new Exception("The share grows away from the door");
        var themes=new[]{"level0","poolrooms","wardzero","station","ironworks"};
        for(int i=0;i<5;i++)if(NativeCityBleed.Theme(NativeCityBleedDress.Instances[i])!=themes[i])throw new Exception("Theme of "+NativeCityBleedDress.Instances[i]);
        foreach(var (area,theme) in new[]{("exp-backrooms","level0"),("exp-poolrooms","poolrooms"),("exp-ward-zero","wardzero"),("exp-transit","station"),("exp-foundry","ironworks"),("Final court","final")})
            if(NativeCityBleed.Theme(area)!=theme)throw new Exception("Theme of "+area);
        if(NativeCityBleed.Theme("expedition-city")!=null)throw new Exception("A theme for no instance");
    }

    // The largest change of any door's share over a 25 cm step, at random places over every floor of the map.
    private static float Seams(ContentMap map,NativeCityBleed bleed,uint seed)
    {
        var rng=new SourceRandom(seed^0x5eedu);float worst=0;var b=map.bounds;var all=Enumerable.Range(0,map.blocks.Length).ToArray();
        for(int n=0;n<1200;n++)
        {
            float x=b.minX+(float)rng.Next()*(b.maxX-b.minX),z=b.minZ+(float)rng.Next()*(b.maxZ-b.minZ);
            float floor=NativeCityBleedDress.FloorBelow(map.blocks,all,new V3(x,80,z));if(floor<-1e8f)continue;
            var p=new V3(x,floor+1.6f,z);double a=rng.Next()*Math.PI*2,e=(rng.Next()-.5)*Math.PI;
            var step=new V3((float)(Math.Cos(e)*Math.Cos(a)),(float)Math.Sin(e),(float)(Math.Cos(e)*Math.Sin(a)))*.25f;
            for(int d=0;d<bleed.Doors.Length;d++)
            {
                float k=Math.Abs(bleed.Share(d,p+step)-bleed.Share(d,p));worst=Math.Max(worst,k);
                if(k>.08f)throw new Exception(map.name+": the bleed jumps "+k+" over 25 cm at "+Show(p));
            }
        }
        return worst;
    }

    // Down each instance street, the share at the route's metres is the curve's (or more, where the street doubles back).
    private static void Along(NativeLayeredCityWalk walk,NativeCityBleed bleed)
    {
        for(int d=1;d<bleed.Doors.Length;d++)
        {
            var pts=bleed.Doors[d].Route;float metres=0;
            for(int i=pts.Length-1;i>0;i--)
            {
                var a=pts[i];var seg=pts[i-1]-a;float len=seg.Length;
                for(float s=0;s<len;s+=5)
                {
                    float m=metres+s;if(m>200)break;var p=a+seg*(s/len);float share=bleed.Share(d,p);
                    if(share<NativeCityBleed.ShareAt(m)-.012f)throw new Exception(walk.Map.name+": "+bleed.Doors[d].Instance+" reads "+share+" at "+m+" m along its route");
                }
                metres+=len;
            }
            var at=bleed.Doors[d].At+new V3(0,1.6f,0);if(bleed.At(at).Door!=d||bleed.At(at).Band!=NativeCityBleed.Bands.Threshold)throw new Exception(walk.Map.name+": the door is not its own threshold");
            if(bleed.At(new V3(0,1.6f,0)).Band!=NativeCityBleed.Bands.None)throw new Exception(walk.Map.name+": the camp is in a band");
        }
    }

    private static string Key(NativeCityBleedDress.Piece p)=>p.Kind+" "+Show(p.At)+" "+p.Yaw.ToString("R",CultureInfo.InvariantCulture)+" "+Show(p.Scale)+" "+p.Lamp+" "+p.Light+" "+p.Seed+" "+p.Band+" "+p.Door;
    private static void Same(NativeCityBleedDress.Plan a,NativeCityBleedDress.Plan b,string what)
    {
        if(!a.Pieces.Select(Key).SequenceEqual(b.Pieces.Select(Key)))throw new Exception("The bleed's plan differs between two builds of "+what);
    }

    // ---- The placement rules, measured by this file's own geometry --------------------------------------------------------
    private static void Rules(ContentMap map,NativeCityBleed bleed,NativeCityBleedDress.Plan plan)
    {
        var blocks=map.blocks;var treads=new HashSet<string>(map.ramps.SelectMany(r=>r.blocks));
        foreach(var p in plan.Pieces)
        {
            var k=NativeCityBleedDress.Kinds[p.Kind];string who=map.name+" "+k.Key+" at "+Show(p.At);
            var corners=new List<V3>();foreach(float x in new[]{k.Min.x,k.Max.x})foreach(float y in new[]{k.Min.y,k.Max.y})foreach(float z in new[]{k.Min.z,k.Max.z})corners.Add(NativeCityBleedDress.Local(p,new V3(x,y,z)));
            V3 lo=new V3(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Min(c=>c.z)),hi=new V3(corners.Max(c=>c.x),corners.Max(c=>c.y),corners.Max(c=>c.z));
            if(lo.x<map.bounds.minX||hi.x>map.bounds.maxX||lo.z<map.bounds.minZ||hi.z>map.bounds.maxZ)throw new Exception(who+": outside the map");
            // the join circles and the door fronts
            foreach(var j in plan.Joins)
            {
                float dx=Math.Max(0,Math.Max(lo.x-j.x,j.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-j.z,j.z-hi.z));
                if(dx*dx+dz*dz<NativeCityBleedDress.JoinKeep*NativeCityBleedDress.JoinKeep)throw new Exception(who+": inside a join circle's 3.3 m");
            }
            foreach(var c in corners.Concat(new[]{(lo+hi)*.5f}))
                foreach(var f in plan.Fronts)
                {
                    var o=new V3(c.x-f.Door.x,0,c.z-f.Door.z);float depth=V3.Dot(o,f.Out),across=V3.Dot(o,new V3(f.Out.z,0,-f.Out.x));
                    if(o.x*o.x+o.z*o.z<NativeCityBleedDress.FrontLine*NativeCityBleedDress.FrontLine||depth<NativeCityBleedDress.FrontLine&&depth>-40&&across<f.Right&&-across<f.Left)throw new Exception(who+": inside a door front");
                }
            switch(k.Mount)
            {
                case NativeCityBleedDress.Mount.Floor:
                {
                    if(Math.Abs(k.Min.y)>1e-5f||(k.Max.y-k.Min.y)*p.Scale.y>NativeCityBleedDress.Flat+1e-5f)throw new Exception(who+": paint thicker than 6 cm");
                    foreach(float u in new[]{0f,.5f,1f})foreach(float v in new[]{0f,.5f,1f})
                    {
                        var q=NativeCityBleedDress.Local(p,new V3(k.Min.x+(k.Max.x-k.Min.x)*(.01f+.98f*u),0,k.Min.z+(k.Max.z-k.Min.z)*(.01f+.98f*v)));
                        if(!blocks.Any(b=>Math.Abs(Top(b)-p.At.y)<.005f&&Inside2(b,q)))throw new Exception(who+": paint off its floor at "+Show(q));
                        if(blocks.Any(b=>Overlaps(b,q+new V3(-.005f,.003f,-.005f),q+new V3(.005f,NativeCityBleedDress.Flat,.005f))))throw new Exception(who+": paint inside a box at "+Show(q));
                    }
                    break;
                }
                case NativeCityBleedDress.Mount.Wall:
                {
                    var n=new V3((float)Math.Sin(p.Yaw),0,(float)Math.Cos(p.Yaw));
                    foreach(float u in new[]{.03f,.5f,.97f})foreach(float v in new[]{.03f,.5f,.97f})
                    {
                        var q=NativeCityBleedDress.Local(p,new V3(k.Min.x+(k.Max.x-k.Min.x)*u,k.Min.y+(k.Max.y-k.Min.y)*v,0));
                        int hit=FirstHit(blocks,q+n*1f,new V3(-n.x,0,-n.z),2f,out float t);
                        if(hit<0||t<1-.01f||t>1+NativeCityBleedDress.Reach)throw new Exception(who+": no face behind it within 0.25 m at "+Show(q)+(hit<0?"":" ("+blocks[hit].id+", "+(t-1).ToString("0.000",CultureInfo.InvariantCulture)+" m)"));
                        if(treads.Contains(blocks[hit].id))throw new Exception(who+": on a stair's tread");
                    }
                    float floor=NativeCityBleedDress.FloorBelow(blocks,Enumerable.Range(0,blocks.Length),NativeCityBleedDress.Local(p,new V3((k.Min.x+k.Max.x)/2,k.Min.y,.4f))+new V3(0,.01f,0));
                    if(lo.y<floor+NativeCityBleedDress.Clear-1e-3f&&k.Max.z*p.Scale.z>NativeCityBleedDress.Flat+1e-5f)throw new Exception(who+": stands "+k.Max.z+" m proud below 3.1 m");
                    for(int i=0;i<blocks.Length;i++)if(Overlaps(blocks[i],lo+new V3(.012f,.012f,.012f),hi-new V3(.012f,.012f,.012f)))throw new Exception(who+": inside "+blocks[i].id);
                    break;
                }
                default:
                {
                    // spans and hung pieces: 3.1 m or more over every floor under them, inside no box (a span's ends may reach 0.25 m
                    // into the walls it hangs between)
                    var ins=new V3(.02f,.02f,.02f);V3 l2=lo,h2=hi;
                    if(k.Mount==NativeCityBleedDress.Mount.Span)
                    {
                        var a=NativeCityBleedDress.Local(p,new V3(k.Min.x,0,0));var e=NativeCityBleedDress.Local(p,new V3(k.Max.x,0,0));
                        if(Math.Abs(a.x-e.x)>Math.Abs(a.z-e.z)){l2.x+=NativeCityBleedDress.Reach;h2.x-=NativeCityBleedDress.Reach;}else{l2.z+=NativeCityBleedDress.Reach;h2.z-=NativeCityBleedDress.Reach;}
                    }
                    for(int i=0;i<blocks.Length;i++)if(Overlaps(blocks[i],l2+ins,h2-ins))throw new Exception(who+": inside "+blocks[i].id);
                    for(float u=0;u<=1.001f;u+=.25f)for(float v=0;v<=1.001f;v+=.5f)
                    {
                        var q=new V3(lo.x+(hi.x-lo.x)*u,lo.y,lo.z+(hi.z-lo.z)*v);float floor=NativeCityBleedDress.FloorBelow(blocks,Enumerable.Range(0,blocks.Length),q);
                        if(floor>-1e8f&&lo.y-floor<NativeCityBleedDress.Clear-1e-3f&&!WithinWall(blocks,q))throw new Exception(who+": only "+(lo.y-floor).ToString("0.00",CultureInfo.InvariantCulture)+" m over the floor");
                    }
                    break;
                }
            }
        }
    }
    // A span's end sample may stand over the wall it is anchored on (inside that wall's footprint, below its top).
    private static bool WithinWall(ContentBlock[] blocks,V3 q)=>blocks.Any(b=>Inside2(b,q)&&Top(b)>q.y);
    private static float Top(ContentBlock b)=>b.pos.y+b.size.y/2;
    private static bool Inside2(ContentBlock b,V3 q)=>Math.Abs(q.x-b.pos.x)<=b.size.x/2+1e-3f&&Math.Abs(q.z-b.pos.z)<=b.size.z/2+1e-3f;
    private static bool Overlaps(ContentBlock b,V3 lo,V3 hi)=>hi.x>b.pos.x-b.size.x/2&&lo.x<b.pos.x+b.size.x/2&&hi.y>b.pos.y-b.size.y/2&&lo.y<b.pos.y+b.size.y/2&&hi.z>b.pos.z-b.size.z/2&&lo.z<b.pos.z+b.size.z/2;
    // The first box a horizontal ray enters (slab test, this file's own).
    private static int FirstHit(ContentBlock[] blocks,V3 o,V3 dir,float reach,out float best)
    {
        best=reach;int hit=-1;
        for(int i=0;i<blocks.Length;i++)
        {
            var b=blocks[i];if(o.y<b.pos.y-b.size.y/2||o.y>b.pos.y+b.size.y/2)continue;float t0=0,t1=best;bool ok=true;
            foreach(int ax in new[]{0,2})
            {
                float oc=ax==0?o.x:o.z,dc=ax==0?dir.x:dir.z,c=ax==0?b.pos.x:b.pos.z,h=(ax==0?b.size.x:b.size.z)/2;
                if(Math.Abs(dc)<1e-6f){if(oc<c-h||oc>c+h)ok=false;continue;}
                float a=(c-h-oc)/dc,e=(c+h-oc)/dc;if(a>e)(a,e)=(e,a);t0=Math.Max(t0,a);t1=Math.Min(t1,e);if(t0>t1)ok=false;
            }
            if(ok&&t0<best&&t0>0){best=t0;hit=i;}
        }
        return hit;
    }

    // Every instance street carries its bands and its lamps.
    private static void Coverage(ContentMap map,NativeCityBleed bleed,NativeCityBleedDress.Plan plan,Dictionary<string,int[]> perTheme)
    {
        for(int d=1;d<bleed.Doors.Length;d++)
        {
            string theme=NativeCityBleed.Theme(bleed.Doors[d].Instance);var mine=plan.Pieces.Where(p=>p.Door==d).ToArray();
            var c=new int[6];foreach(var p in mine){c[(int)p.Band]++;if(p.Lamp!=NativeCityBleedDress.LampState.None)c[5]++;}
            foreach(var p in mine)if(!NativeCityBleedDress.Kinds[p.Kind].Key.StartsWith(theme+".",StringComparison.Ordinal)&&!NativeCityBleedDress.Kinds[p.Kind].Key.StartsWith("common.",StringComparison.Ordinal))throw new Exception(map.name+": "+theme+"'s street carries "+NativeCityBleedDress.Kinds[p.Kind].Key);
            if(c[3]+c[4]<20)throw new Exception(map.name+": "+bleed.Doors[d].Instance+"'s street is thin (rumour "+c[1]+", drift "+c[2]+", warp "+c[3]+", threshold "+c[4]+", lamps "+c[5]+")");
            if(!perTheme.TryGetValue(theme,out var t))perTheme[theme]=t=new int[]{int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue};
            for(int i=1;i<6;i++)t[i]=Math.Min(t[i],c[i]);
        }
    }
    // The most pieces within 60 m of one of six eyes: the camp, the north court, and 20 m out from each instance door.
    private static int View(NativeLayeredCityWalk walk,NativeCityBleedDress.Plan plan)
    {
        var eyes=new List<V3>{new V3(0,1.6f,0),walk.Routes[0].Points.Last()};
        for(int r=1;r<walk.Routes.Count;r++){var pts=walk.Routes[r].Points;var at=pts[pts.Length-1];var leg=(pts[pts.Length-2]-at);eyes.Add(at+leg*(20/leg.Length)+new V3(0,1.6f,0));}
        int most=0;foreach(var e in eyes){int n=0;foreach(var p in plan.Pieces){var o=p.At-e;if(o.x*o.x+o.z*o.z<3600)n++;}most=Math.Max(most,n);}
        return most;
    }
    private static int Draws(NativeCityBleedDress.Plan plan)=>plan.Pieces.GroupBy(p=>p.Kind).Sum(g=>2*((g.Count()+499)/500));
    private static string Show(V3 v)=>v.x.ToString("0.###",CultureInfo.InvariantCulture)+","+v.y.ToString("0.###",CultureInfo.InvariantCulture)+","+v.z.ToString("0.###",CultureInfo.InvariantCulture);

    /// <summary>`--bleed-scene LAYOUT EAST,SOUTH,WEST SEED OUTDIR`: one walk's map (maps.json) and its bleed pieces as rig places
    /// (rest.json), for tools/stages/zfight.py against the bleed's bank, and a plan listing (pieces.txt).</summary>
    public static void Scene(int layout,string deal,uint seed,string outDir)
    {
        var names=deal.Split(',');var walk=new NativeLayeredCityWalk(NativeCityLayout.Plans[layout]);
        var slots=NativeCityLayout.DealHalls(NativeCityBleedDress.DealSeedFor(seed,names),names);var bleed=NativeCityBleed.ForWalk(walk,slots);
        var plan=NativeCityBleedDress.ForWalk(walk,bleed,seed);Directory.CreateDirectory(outDir);var inv=CultureInfo.InvariantCulture;
        string F(float v)=>v.ToString("R",inv);string P(V3 v)=>"{\"x\":"+F(v.x)+",\"y\":"+F(v.y)+",\"z\":"+F(v.z)+"}";
        var maps=new StringBuilder("{\"maps\":[{\"id\":\""+walk.Map.id+"\",\"blocks\":[");
        for(int i=0;i<walk.Map.blocks.Length;i++){var b=walk.Map.blocks[i];if(i>0)maps.Append(',');maps.Append("{\"id\":\""+b.id+"\",\"pos\":"+P(b.pos)+",\"size\":"+P(b.size)+"}");}
        maps.Append("]}]}");File.WriteAllText(Path.Combine(outDir,"maps.json"),maps.ToString());
        var rest=new StringBuilder("{\"rig\":[");var list=new StringBuilder();
        for(int i=0;i<plan.Pieces.Count;i++)
        {
            var p=plan.Pieces[i];var k=NativeCityBleedDress.Kinds[p.Kind];float c=(float)Math.Cos(p.Yaw),s=(float)Math.Sin(p.Yaw);
            var m=new[]{c*p.Scale.x,0,s*p.Scale.z,p.At.x,0,p.Scale.y,0,p.At.y,-s*p.Scale.x,0,c*p.Scale.z,p.At.z};
            if(i>0)rest.Append(',');rest.Append("{\"slot\":\"bleed/"+k.Key+"\",\"place\":["+string.Join(",",m.Select(F))+"]}");
            list.AppendLine(k.Key+" "+Show(p.At)+" yaw "+p.Yaw.ToString("0.###",inv)+" scale "+Show(p.Scale)+" "+p.Band+" door "+p.Door+" "+p.Lamp);
        }
        rest.Append("]}");File.WriteAllText(Path.Combine(outDir,"rest.json"),rest.ToString());File.WriteAllText(Path.Combine(outDir,"pieces.txt"),list.ToString());
        Console.WriteLine(walk.Map.id+" "+walk.Map.name+": "+plan.Pieces.Count+" pieces, deal "+string.Join("/",slots)+", run seed "+NativeCityBleedDress.DealSeedFor(seed,names));
    }
}
internal static class BleedProbe
{
    // `--bleed-probe LAYOUT ROUTE`: what a street's walls are, every 5 m from its door (a diagnostic for the planner).
    public static void Run(int layout,int route)
    {
        var walk=new NativeLayeredCityWalk(NativeCityLayout.Plans[layout]);var b=walk.Map.blocks;var all=Enumerable.Range(0,b.Length).ToList();
        var r=walk.Routes[route];var pts=r.Points;float metres=0;
        for(int i=pts.Length-1;i>0;i--)
        {
            var a=pts[i];var seg=pts[i-1]-a;float len=new V3(seg.x,0,seg.z).Length;var along=new V3(seg.x,0,seg.z)/len;var right=new V3(along.z,0,-along.x);
            string kind=r.Kinds!=null&&i-1<r.Kinds.Length?r.Kinds[i-1]:"?";float width=r.Widths!=null&&i-1<r.Widths.Length?r.Widths[i-1]:0;
            for(float s=0;s<len;s+=5)
            {
                var c=a+seg*(s/len);var line=new System.Text.StringBuilder(((int)(metres+s)).ToString().PadLeft(4)+" m "+kind+" w"+width+" y"+c.y.ToString("0.0"));
                foreach(int side in new[]{1,-1})
                {
                    if(NativeCityBleedDress.Ray(b,all,c+new V3(0,1.6f,0),right*side,40,out int bi,out float t,out var n))
                        line.Append(" | "+(side>0?"R ":"L ")+b[bi].id+" at "+t.ToString("0.0")+" top+"+(b[bi].pos.y+b[bi].size.y/2-c.y).ToString("0.0")+" bottom+"+(b[bi].pos.y-b[bi].size.y/2-c.y).ToString("0.0"));
                    else line.Append(" | "+(side>0?"R":"L")+" none");
                }
                Console.WriteLine(line);
            }
            metres+=seg.Length;if(metres>170)break;
        }
    }
}
internal static class BleedTable
{
    // `--bleed-table`: each layout's three instance streets, the pieces by band (deal: the same instance on all three doors' turn).
    public static void Run()
    {
        for(int layout=0;layout<NativeCityLayout.Plans.Count;layout++)
        {
            var walk=new NativeLayeredCityWalk(NativeCityLayout.Plans[layout]);
            foreach(var deal in new[]{new[]{"Level 0","The Poolrooms","Ward Zero"},new[]{"Central Station","City Ironworks","Level 0"}})
            {
                var slots=NativeCityLayout.DealHalls(NativeCityBleedDress.DealSeedFor(7,deal),deal);var bleed=NativeCityBleed.ForWalk(walk,slots);var plan=NativeCityBleedDress.ForWalk(walk,bleed,7);
                for(int d=1;d<4;d++)
                {
                    var c=new int[6];foreach(var p in plan.Pieces)if(p.Door==d){c[(int)p.Band]++;if(p.Lamp!=NativeCityBleedDress.LampState.None)c[5]++;}
                    Console.WriteLine(walk.Map.name.PadRight(15)+" "+"NESW"[d]+" "+slots[d].PadRight(16)+" rumour "+c[1].ToString().PadLeft(3)+" drift "+c[2].ToString().PadLeft(3)+" warp "+c[3].ToString().PadLeft(3)+" threshold "+c[4].ToString().PadLeft(3)+" lamps "+c[5].ToString().PadLeft(2));
                }
            }
        }
    }
}

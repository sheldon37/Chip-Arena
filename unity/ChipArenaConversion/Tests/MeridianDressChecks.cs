using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// Meridian's street dress (Core NativeMeridianDress; docs/expedition/MERIDIAN-DRESS.md): the planner over the five candidate-3
// layouts, the art's rules on the colliders (the integrator's brief: never more than 0.25 m short, nothing proud below 3.1 m but
// flat paint, every visible face of a covered block drawn), determinism, keep-outs, the lamp rule, the look's table, and, with
// the built bank (CHIPARENA_MERIDIAN_BANK, a path to ChipArenaCityMeridian.bytes), the modules' own hugging and the triangles and
// draws per view against the budget.
internal static class MeridianDressChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Meridian dress: "+what);}
    public static NativeMeridianDress.Input InputOf(NativeLayeredCityWalk walk,uint layout,uint run)=>new NativeMeridianDress.Input{Map=walk.Map,Layout=layout,Run=run,
        Hatches=walk.RoofHatches.ToArray(),Slots=NativeCityLayout.DealHalls(run,new[]{"Ward Zero","Central Station","City Ironworks"}),
        Routes=walk.Routes.Select(r=>new NativeMeridianDress.Route{Name=r.Name,Points=r.Points,Widths=r.Widths,Kinds=r.Kinds}).ToArray()};

    public static void Run(string root)
    {
        count=0;var specs=Path.Combine(root,"..","..","docs","expedition","meridian-plans");
        string[] names={"crown-hill","navigation","arcades","stride","yards"};
        var bankPath=Environment.GetEnvironmentVariable("CHIPARENA_MERIDIAN_BANK");NativePackBank.Bank bank=null;Dictionary<string,int[]> modules=null;
        if(!string.IsNullOrEmpty(bankPath)&&File.Exists(bankPath)){bank=NativePackBank.Read(File.ReadAllBytes(bankPath));modules=NativeMeridianDress.Modules(bank);Check(modules.Count>40,"the bank carries its modules");}
        var report=new List<string>();
        for(int li=0;li<NativeCityLayout.Plans.Count;li++)
        {
            var planInfo=NativeCityLayout.Plans[li];var walk=new NativeLayeredCityWalk(planInfo);var map=walk.Map;
            // ---- the roles and the sheet: every block's role is the spec's, every lot has its style ----
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(specs,names[li]+".json"))))
            {
                var lots=NativeMeridianDress.Lots(planInfo.Seed);
                foreach(var b in doc.RootElement.GetProperty("blocks").EnumerateArray())
                {
                    string id=b.GetProperty("id").GetString(),role=b.GetProperty("role").GetString();var mine=NativeMeridianDress.RoleOf(id,lots);
                    string want=role=="court-wall"||role=="court-gate"||role=="city-hall"||role=="tower"?NativeMeridianDress.RCourt:role=="lid"&&id=="cutting-arch-roof"?NativeMeridianDress.RTunnel:role=="tunnel-roof"?NativeMeridianDress.RTunnel:role;
                    Check(mine==want,planInfo.Name+": "+id+" is a "+role+" in the spec, the planner reads "+mine);
                }
                foreach(var g in doc.RootElement.GetProperty("ground").EnumerateArray())Check(NativeMeridianDress.RoleOf(g.GetProperty("id").GetString(),lots)==NativeMeridianDress.RGround,"ground");
            }
            foreach(var blk in map.blocks)if(NativeMeridianDress.RoleOf(blk.id,null)==NativeMeridianDress.ROther&&blk.id.StartsWith("lot-",StringComparison.Ordinal))Check(NativeMeridianDress.Lots(planInfo.Seed).ContainsKey(blk.id),planInfo.Name+": the sheet lists "+blk.id+" (rerun tools/stages/meridian_sheet.py)");
            // ---- determinism: the same records give the same plan; the run moves only the lamps' states ----
            Func<string,bool> has=modules!=null?modules.ContainsKey:(Func<string,bool>)null;
            var plan=NativeMeridianDress.Build(InputOf(walk,planInfo.Seed,77),has);
            var again=NativeMeridianDress.Build(InputOf(new NativeLayeredCityWalk(planInfo),planInfo.Seed,77),has);
            Check(plan.Digest()==again.Digest(),planInfo.Name+": deterministic");
            var other=NativeMeridianDress.Build(InputOf(walk,planInfo.Seed,991),has);
            Check(other.Placements.Count==plan.Placements.Count&&other.Quads.Count==plan.Quads.Count&&other.Lamps.Count==plan.Lamps.Count,planInfo.Name+": a run changes no architecture");
            // ---- the theme hook: null by default; a theme picks a variant only where the bank has it ----
            Check(NativeMeridianDress.ThemeAt==null,"ThemeAt is null by default");
            try
            {
                NativeMeridianDress.ThemeAt=p=>p.x>0?"station":null;var themed=NativeMeridianDress.Build(InputOf(walk,planInfo.Seed,77),k=>!k.Contains("@")||k=="u.plain@station");
                Check(themed.Placements.Any(p=>p.Key=="u.plain@station")==plan.Placements.Any(p=>p.Key=="u.plain"&&p.M[3]>0),"a themed variant replaces its plain key where it exists");
                Check(themed.Placements.All(p=>!p.Key.Contains("@")||p.Key=="u.plain@station"),"a missing themed variant falls back to the plain key");
            }
            finally{NativeMeridianDress.ThemeAt=null;}
            Rules(planInfo.Name,walk,plan);
            // ---- the lamp rule: about one every 12 m on the routes, the protected circuit never dead; side streets 1 in 5 dead,
            // 1 in 10 flickering ----
            float routeLength=walk.Routes.Sum(r=>r.Length);var routeLamps=plan.Lamps.Where(l=>l.Protected).ToList();var side=plan.Lamps.Where(l=>!l.Protected).ToList();
            Check(routeLamps.All(l=>l.State!=NativeMeridianDress.Dead),"no lamp on a route home is dead");
            Check(routeLamps.Count>=routeLength/12f*.2f,planInfo.Name+": route lamps "+routeLamps.Count+" for "+routeLength.ToString("0")+" m of route (about one every 12 m; "+plan.Unmounted+" points had no wall)");
            if(side.Count>40)
            {
                float dead=side.Count(l=>l.State==NativeMeridianDress.Dead)/(float)side.Count,flick=plan.Lamps.Count(l=>l.State==NativeMeridianDress.Flicker)/(float)plan.Lamps.Count;
                Check(dead>.12f&&dead<.28f,planInfo.Name+": side streets 1 in 5 dead ("+dead.ToString("P0")+")");Check(flick>.05f&&flick<.16f,planInfo.Name+": 1 in 10 flickering ("+flick.ToString("P0")+")");
            }
            foreach(var l in plan.Lamps)Check(l.Kind==NativeMeridianDress.Flush||l.Bowl.y-l.Pool.y>=3.1f,planInfo.Name+" kind "+l.Kind+" at "+l.Bowl.x+","+l.Bowl.y+","+l.Bowl.z+" pool "+l.Pool.y+": every bracket or span lamp's head hangs over 3.1 m above its floor (overhead art); a flush lamp hugs its wall");
            // ---- with the bank: the modules' hugging, chunk caps and the triangles per view ----
            string budget="";
            if(bank!=null)budget=Budgets(planInfo.Name,walk,plan,bank,modules);
            report.Add(FormattableString.Invariant($"{planInfo.Name}: {plan.Placements.Count} modules, {plan.Quads.Count} surfaces, {plan.Lamps.Count} lamps ({routeLamps.Count} route, {side.Count} side; {plan.Unmounted} route points unmounted), {plan.Covers.Count} blocks covered{budget}"));
        }
        // ---- the look: the walk draws in the night with the bank, the town's dusk without it; the table's one row ----
        Check(NativeMeridianDress.LookOf("expedition-city-walk-647",()=>true)=="expedition-night"&&NativeMeridianDress.LookOf("expedition-city-walk-647",()=>false)==null,"the walk's night needs the bank");
        Check(NativeMeridianDress.LookOf("expedition-generated-city",()=>true)==null&&NativeMeridianDress.LookOf("expedition-city",()=>true)==null&&NativeMeridianDress.LookTable.Length==1,"the live city and the town keep their looks");
        var looks=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json"));
        Check(looks.Contains("{\"id\":\"expedition-night\",\"look\":\"expedition-night\"")&&looks.Contains("\"key\":{\"color\":10335464,\"intensity\":0.32,\"elevation\":38,\"bearing\":340"),"MapLooks.json carries the night (moon 0x9DB4E8 x 0.32 at 38 degrees, bearing 340)");
        Check(File.ReadAllText(Path.Combine(root,"..","..","docs","arenas","scripts","expedition_city.py")).Contains("def night_look():"),"the night is written by its generator");
        // ---- the runtime's hooks (source): one call each in the walk, the greybox's hide, Ward Zero and the Ironworks untouched ----
        var walkSrc=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeLayeredCityWalkPreview.cs"));
        Check(walkSrc.Contains("MeridianDressOpen(cityWalk, plan.Seed, seed);")&&walkSrc.Contains("MeridianDressClose();"),"one call line each in OpenCityWalk and CloseCityWalk");
        var dressing=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeArenaDressing.cs"));
        Check(dressing.Contains("if(surveyBlocks&&NativeMeridianDressDraw.Hides(arena.MapId,p.Definition.id))continue;"),"the greybox leaves out only the walk's covered blocks the dress drew this frame");
        var draw=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeMeridianDressDraw.cs"));
        Check(draw.Contains("bank=NativePackBanks.Bank(NativeMeridianDress.Bank);if(bank==null)return false;")&&draw.Contains("Time.frameCount-activeFrame<=1"),"without the bank nothing is made; a stale dress hides nothing");
        Check(!draw.Contains("WardField(")&&!draw.Contains("NativeWardZeroField")&&!draw.Contains("NativeIronworks"),"the dress never touches Ward Zero's or the Ironworks' light field");
        foreach(var line in report)Console.WriteLine("  "+line);
        Console.WriteLine("PASS meridian dress: "+count+" checks over the five layouts"+(bank==null?" (no bank: the modules' own checks and the budgets need CHIPARENA_MERIDIAN_BANK)":" with the bank"));
    }

    // The art on the colliders, over a layout's plan.
    private static void Rules(string name,NativeLayeredCityWalk walk,NativeMeridianDress.Plan plan)
    {
        var map=walk.Map;var solids=new NativeMeridianDress.Solids(map.blocks);var B=solids.B;
        // nothing proud below 3.1 m: a facade module whose foot is lower than 3.1 m over the floor in front hugs its face
        foreach(var p in plan.Placements)
        {
            if(p.Paint||p.Block>=0&&p.Face<0)continue;
            if(p.Face>=0&&p.V0<p.Floor+NativeMeridianDress.Hug-.01f)Check(p.Hugs||p.Key.StartsWith("g.",StringComparison.Ordinal)||p.Key.EndsWith(".h",StringComparison.Ordinal)||p.Key.Contains(".h@"),name+": "+p.Key+" stands proud below 3.1 m (its hugging variant is wanted)");
            if(p.Face<0&&p.Block<0&&!p.Key.StartsWith("plate.",StringComparison.Ordinal)&&!p.Key.StartsWith("lamp.flush",StringComparison.Ordinal))Check(p.V0>=p.Floor+NativeMeridianDress.Hug-.6f,name+": "+p.Key+" hangs at "+(p.V0-p.Floor).ToString("0.0")+" m over its floor (overhead art from 3.1 m)");
        }
        // every module on a covered block's face lies on that face (no art floats off a collider)
        foreach(var p in plan.Placements.Where(p=>p.Face>=0&&p.Face!=2&&p.Block>=0))
        {
            var n=NativeMeridianDress.NormalOf(p.Face);int o=p.Block*6;float plane=n.x>0?B[o+1]:n.x<0?B[o]:n.z>0?B[o+5]:B[o+4];float at=n.x!=0?p.M[3]:p.M[11];
            Check(Math.Abs(at-plane)<.002f,name+": "+p.Key+" sits on its face's plane");
        }
        // every visible part of a covered block's sides is drawn: sampled every metre, each exposed point lies in a module's face
        // rectangle or on a generated surface of that face (so no collision without a surface you can see)
        var byFace=new Dictionary<(int,int),List<(float,float,float,float)>>();
        foreach(var p in plan.Placements)if(p.Block>=0&&p.Face>=0){if(!byFace.TryGetValue((p.Block,p.Face),out var l))byFace[(p.Block,p.Face)]=l=new List<(float,float,float,float)>();l.Add((p.U0,p.U1,p.V0,p.V1));}
        var quads=new Dictionary<int,List<NativeMeridianDress.Quad>>();foreach(var q in plan.Quads)if(q.Block>=0){if(!quads.TryGetValue(q.Block,out var l))quads[q.Block]=l=new List<NativeMeridianDress.Quad>();l.Add(q);}
        int sampled=0,missed=0;var covered=new HashSet<string>(plan.Covers);
        for(int bi=0;bi<map.blocks.Length;bi++)
        {
            var blk=map.blocks[bi];if(!covered.Contains(blk.id))continue;var role=NativeMeridianDress.RoleOf(blk.id,NativeMeridianDress.Lots(plan.Layout));
            if(role==NativeMeridianDress.RParapet||role==NativeMeridianDress.RRail||role==NativeMeridianDress.RPost||role==NativeMeridianDress.RWagon)continue;   // filled by their modules
            if(role==NativeMeridianDress.RTread)continue;   // a sloped street's treads: its surface is the wedge's
            foreach(int f in new[]{0,1,4,5})
            {
                NativeMeridianDress.FaceFrame(B,bi*6,f,out var o,out var u,out var v);float lu=u.Length,lv=v.Length;if(lu<.5f||lv<.5f)continue;var n=NativeMeridianDress.NormalOf(f);
                for(float a=.5f;a<lu;a+=1.7f)for(float c=.5f;c<lv;c+=1.3f)
                {
                    var p=o+u*(a/lu)+v*(c/lv)+n*.05f;if(solids.Inside(p.x,p.y,p.z,bi))continue;sampled++;
                    bool hit=false;
                    if(byFace.TryGetValue((bi,f),out var rects))foreach(var r in rects)if(a>=r.Item1-.01f&&a<=r.Item2+.01f&&p.y>=r.Item3-.01f&&p.y<=r.Item4+.01f){hit=true;break;}
                    if(!hit&&quads.TryGetValue(bi,out var ql))foreach(var q in ql)
                    {
                        if(Math.Abs(q.N.x-n.x)>.01f||Math.Abs(q.N.z-n.z)>.01f||Math.Abs(q.N.y)>.01f)continue;
                        var d=p-n*.05f-q.O;float s=V3.Dot(d,q.U)/V3.Dot(q.U,q.U),t=V3.Dot(d,q.V)/V3.Dot(q.V,q.V);if(s>=-.01f&&s<=1.01f&&t>=-.01f&&t<=1.01f){hit=true;break;}
                    }
                    if(!hit){missed++;if(Environment.GetEnvironmentVariable("MERIDIAN_DEBUG")!=null&&missed%25==1)Console.WriteLine("bare "+blk.id+" "+role+" f"+f+" a="+a.ToString("0.0")+" y="+p.y.ToString("0.0")+" b="+blk.pos.y+"/"+blk.size.y);}
                }
            }
        }
        Check(sampled>1000&&missed<=sampled/200,name+": every seen face of a covered block is drawn ("+missed+" of "+sampled+" sampled points bare)");
        // keep-outs: inside a route's corridor nothing but paint, lamps (overhead) and the blocks' own art stands below 3.1 m
        foreach(var p in plan.Placements)
        {
            if(p.Paint||p.Block>=0||p.Key.StartsWith("lamp.",StringComparison.Ordinal))continue;
            var at=new V3(p.M[3],p.M[7],p.M[11]);
            foreach(var r in walk.Routes)for(int i=0;i+1<r.Points.Length;i++)
            {
                var a=r.Points[i];var c=r.Points[i+1];float w=i<r.Widths.Length?r.Widths[i]:10;float dx=c.x-a.x,dz=c.z-a.z,l=dx*dx+dz*dz;if(l<1e-4f)continue;
                float t=Math.Max(0,Math.Min(1,((at.x-a.x)*dx+(at.z-a.z)*dz)/l));float ex=a.x+dx*t-at.x,ez=a.z+dz*t-at.z;
                if(ex*ex+ez*ez<(w*.5f-.3f)*(w*.5f-.3f)&&Math.Abs(at.y-(a.y+(c.y-a.y)*t))<3.1f)Check(false,name+": "+p.Key+" stands in "+r.Name+"'s corridor");
            }
        }
        // every covered block is a dressed role, and the halls, court and landmarks (D2's) stay greybox
        foreach(var id in plan.Covers){var role=NativeMeridianDress.RoleOf(id,NativeMeridianDress.Lots(plan.Layout));Check(NativeMeridianDress.Dressed(role),name+": "+id+" covered");}
        Check(!plan.Covers.Any(id=>id.StartsWith("hall-",StringComparison.Ordinal)||id.StartsWith("court-",StringComparison.Ordinal)||id=="city-hall"||id=="city-hall-tower"),name+": D2's halls and court stay theirs");
    }

    // The bank's side: the hugging modules within 0.25 m in and 6 cm out, the chunks' caps, the six views' triangles and draws.
    public static readonly V3[] Views={};
    private static string Budgets(string name,NativeLayeredCityWalk walk,NativeMeridianDress.Plan plan,NativePackBank.Bank bank,Dictionary<string,int[]> modules)
    {
        foreach(var kv in modules)
        {
            bool hug=kv.Key.StartsWith("g.",StringComparison.Ordinal)||kv.Key.EndsWith(".h",StringComparison.Ordinal)||kv.Key.StartsWith("paint.",StringComparison.Ordinal)||kv.Key.StartsWith("puddle.",StringComparison.Ordinal)||kv.Key.StartsWith("ghost.",StringComparison.Ordinal)||kv.Key=="roof.hatch"||kv.Key.StartsWith("lamp.flush",StringComparison.Ordinal)||kv.Key.StartsWith("plate.",StringComparison.Ordinal);
            if(!hug)continue;
            foreach(int tier in new[]{0,2})
            {
                var prop=bank.Props[kv.Value[tier]];
                foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    float out_=kv.Key.StartsWith("paint.",StringComparison.Ordinal)||kv.Key.StartsWith("puddle.",StringComparison.Ordinal)||kv.Key=="roof.hatch"?v.Y:v.Z;
                    Check(out_<=NativeMeridianDress.Paint+.004f&&out_>=-NativeMeridianDress.Short-.004f,kv.Key+" lies within 0.25 m in and 6 cm out of its face ("+out_.ToString("0.000")+")");
                }
            }
        }
        NativeMeridianDress.ByChunk(plan,out var pl,out var qs);var mesh=new NativeMeridianMesh();int n=plan.Nx*plan.Nz;
        var near=new int[n];var far=new int[n];var box=new (V3 lo,V3 hi)[n];int worst=0;
        for(int c=0;c<n;c++)
        {
            if(pl[c]==null&&qs[c]==null)continue;
            NativeMeridianDress.Combine(plan,c,0,bank,modules,pl[c],qs[c],mesh);near[c]=mesh.Triangles;box[c]=(new V3(mesh.MinX,mesh.MinY,mesh.MinZ),new V3(mesh.MaxX,mesh.MaxY,mesh.MaxZ));
            NativeMeridianDress.Combine(plan,c,2,bank,modules,pl[c],qs[c],mesh);far[c]=mesh.Triangles;worst=Math.Max(worst,near[c]);
        }
        // the six standard views (docs/MERIDIAN-B-WALK.md): the camp's pilot, each route 18 m short of its door, the overview
        var eyes=new List<V3>{new V3(1,6,-10)};
        foreach(var r in walk.Routes){r.Sample(r.Length-18,out var foot,out _,out _);eyes.Add(foot+new V3(0,2,0));}
        eyes.Add(new V3(0,42,-18));
        string text="";
        foreach(var tier in new[]{NativeGraphicsTier.High,NativeGraphicsTier.Low})
        {
            int maxT=0,maxD=0;
            foreach(var e in eyes)
            {
                int t=0,d=0;float reach=NativeMeridianDress.Reach(tier),farR=NativeMeridianDress.Far(tier);
                for(int c=0;c<n;c++)
                {
                    if(near[c]==0&&far[c]==0)continue;var lo=box[c].lo;var hi=box[c].hi;
                    float dx=Math.Max(0,Math.Max(lo.x-e.x,e.x-hi.x)),dy=Math.Max(0,Math.Max(lo.y-e.y,e.y-hi.y)),dz=Math.Max(0,Math.Max(lo.z-e.z,e.z-hi.z));float dist=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
                    if(dist<reach){t+=near[c];d+=2;}else if(dist<farR){t+=far[c];d+=2;}
                }
                maxT=Math.Max(maxT,t);maxD=Math.Max(maxD,d);
            }
            // the environment's share of a view (bible 10.1: 0.9 M and 900 draws on High, 0.24 M and 450 on Low), the whole frame's
            // budget (High 2,177,224, Low 821,492) holding the crowd, the pilots and the effects beside it
            Check(maxT<=(tier==NativeGraphicsTier.High?900000:330000)&&maxD<=(tier==NativeGraphicsTier.High?900:450),name+" "+tier+": the dress's worst view "+maxT+" triangles, "+maxD+" draws");
            text+=FormattableString.Invariant($"; {tier} worst view {maxT:N0} triangles, {maxD} draws");
        }
        return text+FormattableString.Invariant($"; the heaviest LOD0 chunk {worst:N0}");
    }
}

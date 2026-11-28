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
        var hook=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeMeridianDressWalk.cs"));
        Check(hook.Contains("var night=NativeMeridianDress.LookOf(walk.Map.id,null);")&&hook.Contains("walk.Map.look=night;NativeLighting.Apply(walk.Map,cameraView);")&&hook.IndexOf("return;}",StringComparison.Ordinal)<hook.IndexOf("walk.Map.look=night",StringComparison.Ordinal),"the walk draws in the night only once its dress (and its lamps) is made");
        var walkSrc=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeLayeredCityWalkPreview.cs"));
        Check(walkSrc.Contains("MeridianDressOpen(cityWalk, plan.Seed, seed);")&&walkSrc.Contains("MeridianDressClose();"),"one call line each in OpenCityWalk and CloseCityWalk");
        var dressing=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeArenaDressing.cs"));
        Check(dressing.Contains("if(meridianBlocks&&NativeMeridianDressDraw.Hides(arena.MapId,p.Definition.id))continue;")&&dressing.Contains("meridianBlocks=NativeMeridianDress.HidesGreybox(mapId);"),
            "the greybox leaves out only the covered blocks the dress drew this frame, on the walk's layouts and the live map (HubZFightChecks measures the live hub)");
        var draw=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeMeridianDressDraw.cs"));
        Check(draw.Contains("bank=NativePackBanks.Bank(NativeMeridianDress.Bank);if(bank==null)return false;")&&draw.Contains("Time.frameCount-activeFrame<=1"),"without the bank nothing is made; a stale dress hides nothing");
        Check(!draw.Contains("WardField(")&&!draw.Contains("NativeWardZeroField")&&!draw.Contains("NativeIronworks"),"the dress never touches Ward Zero's or the Ironworks' light field");
        foreach(var line in report)Console.WriteLine("  "+line);
        Console.WriteLine("PASS meridian dress: "+count+" checks over the five layouts"+(bank==null?" (no bank: the modules' own checks and the budgets need CHIPARENA_MERIDIAN_BANK)":" with the bank"));
    }

    /// <summary>A look without Unity (--meridian-export LAYOUT X Z RADIUS OUT.ply, CHIPARENA_MERIDIAN_BANK set): the LOD0 chunks
    /// within RADIUS of (X, Z) and the greybox boxes the dress leaves, as one PLY with vertex colours, for a Blender check render.</summary>
    public static void Export(string root,int layout,float x,float z,float radius,string path)
    {
        var bank=NativePackBank.Read(File.ReadAllBytes(Environment.GetEnvironmentVariable("CHIPARENA_MERIDIAN_BANK")));var modules=NativeMeridianDress.Modules(bank);
        var info=NativeCityLayout.Plans[layout];var walk=new NativeLayeredCityWalk(info);var plan=NativeMeridianDress.Build(InputOf(walk,info.Seed,77),modules.ContainsKey);
        NativeMeridianDress.ByChunk(plan,out var pl,out var qs);var mesh=new NativeMeridianMesh();
        var P=new List<float>();var C=new List<byte>();var I=new List<int>();
        for(int c=0;c<plan.Nx*plan.Nz;c++)
        {
            float cx=plan.MinX+(c%plan.Nx+.5f)*NativeMeridianDress.ChunkSize,cz=plan.MinZ+(c/plan.Nx+.5f)*NativeMeridianDress.ChunkSize;
            if((cx-x)*(cx-x)+(cz-z)*(cz-z)>radius*radius)continue;NativeMeridianDress.Combine(plan,c,0,bank,modules,pl[c],qs[c],mesh);int b=P.Count/3;
            for(int i=0;i<mesh.Vertices;i++){P.Add(mesh.P[i*3]);P.Add(mesh.P[i*3+1]);P.Add(mesh.P[i*3+2]);bool glow=false;C.Add(mesh.C[i*4]);C.Add(mesh.C[i*4+1]);C.Add(mesh.C[i*4+2]);C.Add(glow?(byte)255:mesh.C[i*4+3]);}
            foreach(int k in mesh.Solid)I.Add(b+k);foreach(int k in mesh.Glow)I.Add(b+k);
        }
        // the boxes left greybox (halls, the court, landmarks), mid grey
        foreach(var blk in walk.Map.blocks)
        {
            if(plan.Covers.Contains(blk.id))continue;if((blk.pos.x-x)*(blk.pos.x-x)+(blk.pos.z-z)*(blk.pos.z-z)>radius*radius*1.5f)continue;
            int b=P.Count/3;float hx=blk.size.x/2,hy=blk.size.y/2,hz=blk.size.z/2;
            for(int k=0;k<8;k++){P.Add(blk.pos.x+((k&1)==1?hx:-hx));P.Add(blk.pos.y+((k&2)==2?hy:-hy));P.Add(blk.pos.z+((k&4)==4?hz:-hz));C.Add(120);C.Add(120);C.Add(128);C.Add(255);}
            int[] f={0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5};foreach(int k in f)I.Add(b+k);
        }
        using(var w=new BinaryWriter(File.Create(path)))
        {
            var head=string.Join("\n",new[]{"ply","format binary_little_endian 1.0","element vertex "+P.Count/3,"property float x","property float y","property float z","property uchar red","property uchar green","property uchar blue","property uchar alpha","element face "+I.Count/3,"property list uchar int vertex_indices","end_header",""});
            w.Write(System.Text.Encoding.ASCII.GetBytes(head));
            for(int i=0;i<P.Count/3;i++){w.Write(P[i*3]);w.Write(P[i*3+1]);w.Write(P[i*3+2]);w.Write(C[i*4]);w.Write(C[i*4+1]);w.Write(C[i*4+2]);w.Write(C[i*4+3]);}
            for(int i=0;i<I.Count;i+=3){w.Write((byte)3);w.Write(I[i]);w.Write(I[i+1]);w.Write(I[i+2]);}
        }
        Console.WriteLine("wrote "+path+": "+P.Count/3+" vertices, "+I.Count/3+" triangles; lamps "+plan.Lamps.Count);
        foreach(var l in plan.Lamps)if((l.Bowl.x-x)*(l.Bowl.x-x)+(l.Bowl.z-z)*(l.Bowl.z-z)<radius*radius)Console.WriteLine(FormattableString.Invariant($"LAMP {l.Bowl.x:F2} {l.Bowl.y:F2} {l.Bowl.z:F2} {l.State} {l.Kind} {l.Pool.y:F2} {(l.Protected?1:0)} {l.Mount.x:F2} {l.Mount.z:F2}"));
    }

    /// <summary>zfight.py's input for a layout (--meridian-zfight LAYOUT DIR, CHIPARENA_MERIDIAN_BANK set): the combined chunks at
    /// LOD0 and LOD1 as a stage bank (static/&lt;chunk&gt;/0 and /2, cover/&lt;block&gt; for every covered block) and the walk's map.</summary>
    public static void ZFightExport(int layout,string dir,bool parts=false)
    {
        var bank=NativePackBank.Read(File.ReadAllBytes(Environment.GetEnvironmentVariable("CHIPARENA_MERIDIAN_BANK")));var modules=NativeMeridianDress.Modules(bank);
        var info=NativeCityLayout.Plans[layout];var walk=new NativeLayeredCityWalk(info);var plan=NativeMeridianDress.Build(InputOf(walk,info.Seed,77),modules.ContainsKey);
        NativeMeridianDress.ByChunk(plan,out var pl,out var qs);var mesh=new NativeMeridianMesh();Directory.CreateDirectory(dir);
        var meshes=new List<(string name,float[] p,float[] n,float[] uv0,float[] uv1,byte[] c,int[] solid,int[] glow,float[] lo,float[] hi)>();
        // parts: every placement and every surface its own mesh ("p<index>.<key>", "q<index>.<finish>.<block>"), so a report names
        // the pieces that meet (a diagnosis; zfight's figures are over the combined chunks)
        var units=new List<(string name,List<int> pl,List<int> qs)>();
        if(parts)
        {
            for(int i=0;i<plan.Placements.Count;i++)units.Add(("p"+i+"."+plan.Placements[i].Key+".b"+plan.Placements[i].Block,new List<int>{i},null));
            for(int i=0;i<plan.Quads.Count;i++)units.Add(("q"+i+"."+plan.Quads[i].Fin+".b"+plan.Quads[i].Block,null,new List<int>{i}));
        }
        else{var drawn=NativeMeridianDress.DrawChunks(plan,bank,modules);for(int c=0;c<drawn.Count;c++)units.Add((c.ToString(),drawn[c].Placements,drawn[c].Quads));}
        foreach(var u in units)foreach(int tier in new[]{0,2})
        {
            NativeMeridianDress.Combine(plan,0,tier,bank,modules,u.pl,u.qs,mesh);if(mesh.Vertices==0)continue;int n=mesh.Vertices;
            meshes.Add(("static/"+u.name+"/"+tier,mesh.P.Take(n*3).ToArray(),mesh.N.Take(n*3).ToArray(),mesh.Uv0.Take(n*2).ToArray(),mesh.Uv1.Take(n*2).ToArray(),mesh.C.Take(n*4).ToArray(),mesh.Solid.ToArray(),mesh.Glow.ToArray(),
                new[]{mesh.MinX,mesh.MinY,mesh.MinZ},new[]{mesh.MaxX,mesh.MaxY,mesh.MaxZ}));
        }
        using(var w=new BinaryWriter(File.Create(Path.Combine(dir,"meridian-"+layout+".bytes"))))
        {
            void S(string t){var b=System.Text.Encoding.UTF8.GetBytes(t);w.Write(b.Length);w.Write(b);}
            w.Write(System.Text.Encoding.ASCII.GetBytes("CAPB"));w.Write(1);w.Write(0);
            w.Write(2);foreach(var m in new[]{"stage","stage-glow"}){S(m);w.Write(-1);w.Write(-1);for(int k=0;k<4;k++)w.Write(1f);for(int k=0;k<3;k++)w.Write(0f);w.Write((byte)0);}
            w.Write(meshes.Count);
            foreach(var m in meshes)
            {
                S(m.name);foreach(var f in m.lo)w.Write(f);foreach(var f in m.hi)w.Write(f);int n=m.p.Length/3;w.Write(n);w.Write((byte)(NativePackBank.AttrNormal|NativePackBank.AttrUv0|NativePackBank.AttrUv1|NativePackBank.AttrColor));
                foreach(var f in m.p)w.Write(f);foreach(var f in m.n)w.Write(f);foreach(var f in m.uv0)w.Write(f);foreach(var f in m.uv1)w.Write(f);w.Write(m.c);
                var subs=m.glow.Length>0?new[]{m.solid,m.glow}:new[]{m.solid};w.Write(subs.Length);
                for(int s2=0;s2<subs.Length;s2++){w.Write(s2);w.Write(subs[s2].Length);foreach(int ix in subs[s2])if(n>65535)w.Write(ix);else w.Write((ushort)ix);}
            }
            var covers=plan.Covers.OrderBy(x=>x,StringComparer.Ordinal).ToList();w.Write(meshes.Count+covers.Count);
            for(int i=0;i<meshes.Count;i++){var m=meshes[i];S(m.name);S("static");foreach(var f in m.lo)w.Write(f);foreach(var f in m.hi)w.Write(f);w.Write(1);w.Write(i);foreach(var f in new float[]{1,0,0,0,0,1,0,0,0,0,1,0})w.Write(f);}
            foreach(var id in covers){S("cover/"+id);S("cover");for(int k=0;k<6;k++)w.Write(0f);w.Write(0);}
        }
        var blocks=walk.Map.blocks.Select(b=>new Dictionary<string,object>{{"id",b.id},{"pos",new Dictionary<string,float>{{"x",b.pos.x},{"y",b.pos.y},{"z",b.pos.z}}},{"size",new Dictionary<string,float>{{"x",b.size.x},{"y",b.size.y},{"z",b.size.z}}},{"rotation",new Dictionary<string,float>{{"x",0},{"y",0},{"z",0},{"w",1}}}}).ToList();
        File.WriteAllText(Path.Combine(dir,"meridian-"+layout+"-maps.json"),JsonSerializer.Serialize(new Dictionary<string,object>{{"maps",new[]{new Dictionary<string,object>{{"id",walk.Map.id},{"blocks",blocks}}}}}));
        Console.WriteLine("zfight input "+walk.Map.id+": "+meshes.Count+" chunk meshes, "+plan.Covers.Count+" covers");
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
        // the generated surfaces by the plane they lie on (a face two overlapping blocks share is drawn once, by the first)
        var quads=new Dictionary<(int,int),List<NativeMeridianDress.Quad>>();
        foreach(var q in plan.Quads)
        {
            int ax=Math.Abs(q.N.x)>.5f?0:Math.Abs(q.N.y)>.5f?1:2;float plane=ax==0?q.O.x:ax==1?q.O.y:q.O.z;int key=ax*2+((ax==0?q.N.x:ax==1?q.N.y:q.N.z)>0?0:1);
            var k=(key,(int)Math.Round(plane*100));if(!quads.TryGetValue(k,out var l))quads[k]=l=new List<NativeMeridianDress.Quad>();l.Add(q);
        }
        int sampled=0,missed=0;var covered=new HashSet<string>(plan.Covers);var lotsOf=NativeMeridianDress.Lots(plan.Layout);
        for(int bi=0;bi<map.blocks.Length;bi++)
        {
            var blk=map.blocks[bi];if(!covered.Contains(blk.id))continue;var role=NativeMeridianDress.RoleOf(blk.id,NativeMeridianDress.Lots(plan.Layout));
            if(role==NativeMeridianDress.RParapet||role==NativeMeridianDress.RRail||role==NativeMeridianDress.RPost||role==NativeMeridianDress.RWagon)continue;   // filled by their modules
            if(role==NativeMeridianDress.RTread)continue;   // a sloped street's treads: its surface is the wedge's
            foreach(int f in new[]{0,1,4,5})
            {
                // (a face on the map's bounds turned out of it is never seen: beyond the Cordon)
                var mb=map.bounds;int ob=bi*6;if(f==0&&B[ob+1]>=mb.maxX-.05f||f==1&&B[ob]<=mb.minX+.05f||f==4&&B[ob+5]>=mb.maxZ-.05f||f==5&&B[ob+4]<=mb.minZ+.05f)continue;
                NativeMeridianDress.FaceFrame(B,bi*6,f,out var o,out var u,out var v);float lu=u.Length,lv=v.Length;if(lu<.5f||lv<.5f)continue;var n=NativeMeridianDress.NormalOf(f);
                for(float a=.5f;a<lu;a+=1.7f)for(float c=.5f;c<lv;c+=1.3f)
                {
                    var p=o+u*(a/lu)+v*(c/lv)+n*.05f;if(solids.Inside(p.x,p.y,p.z,bi))continue;sampled++;
                    bool hit=false;
                    if(byFace.TryGetValue((bi,f),out var rects))foreach(var r in rects)if(a>=r.Item1-.01f&&a<=r.Item2+.01f&&p.y>=r.Item3-.01f&&p.y<=r.Item4+.01f){hit=true;break;}
                    float pl=f==0||f==1?p.x-n.x*.05f:p.z-n.z*.05f;
                    if(!hit&&quads.TryGetValue((f,(int)Math.Round(pl*100)),out var ql))foreach(var q in ql)
                    {
                        var d=p-n*.05f-q.O;float s=V3.Dot(d,q.U)/V3.Dot(q.U,q.U),t=V3.Dot(d,q.V)/V3.Dot(q.V,q.V);if(s>=-.01f&&s<=1.01f&&t>=-.01f&&t<=1.01f){hit=true;break;}
                    }
                    // (a part of the face in the plane of a face the greybox or the landmarks draw is theirs)
                    // (and a part in the plane of a lot's side is the lot's facade's)
                    if(!hit&&solids.Shared(p.x-n.x*.05f,p.y,p.z-n.z*.05f,f,bi,_=>false,i=>!covered.Contains(map.blocks[i].id)||lotsOf.ContainsKey(map.blocks[i].id)))hit=true;
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
        // the landmarks' fixed covers (the exits' host lots, the court, the halls) are theirs: not covered, no module or surface on
        // them, no lamp, plate or paint hung on or laid over them
        var fixedCovers=NativeMeridianLandmarks.FixedCovers(plan.Layout,map);Check(fixedCovers.Count>0,name+": the landmarks name their blocks");
        Check(fixedCovers.SetEquals(plan.Left),name+": the plan leaves exactly the landmarks' blocks");
        foreach(var id in fixedCovers)Check(!plan.Covers.Contains(id),name+": "+id+" is the landmarks' (not covered by the dress)");
        var leftIx=new HashSet<int>();for(int i=0;i<map.blocks.Length;i++)if(fixedCovers.Contains(map.blocks[i].id))leftIx.Add(i);
        foreach(var p in plan.Placements)Check(p.Block<0||!leftIx.Contains(p.Block),name+": "+p.Key+" lies on the landmarks' "+(p.Block>=0?map.blocks[p.Block].id:""));
        foreach(var q in plan.Quads)Check(q.Block<0||!leftIx.Contains(q.Block),name+": a surface lies on the landmarks' "+(q.Block>=0?map.blocks[q.Block].id:""));
        foreach(var p in plan.Placements.Where(p=>p.Block<0))
        {
            var at=new V3(p.M[3],p.M[7],p.M[11]);
            foreach(int i in leftIx){int o=i*6;if(at.x>B[o]-.02f&&at.x<B[o+1]+.02f&&at.y>B[o+2]-.02f&&at.y<B[o+3]+.02f&&at.z>B[o+4]-.02f&&at.z<B[o+5]+.02f)Check(false,name+": "+p.Key+" stands on the landmarks' "+map.blocks[i].id+FormattableString.Invariant($" at ({at.x:F2},{at.y:F2},{at.z:F2}) n ({p.M[2]:F1},{p.M[10]:F1}), box x {B[o]:F2}..{B[o+1]:F2} y {B[o+2]:F2}..{B[o+3]:F2} z {B[o+4]:F2}..{B[o+5]:F2}"));}
        }
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
        // the chunks as the drawer draws them (a cell too heavy for the caps cut into quarters): each at the caps or under, its
        // counted triangles the combined mesh's
        var units=NativeMeridianDress.DrawChunks(plan,bank,modules);var mesh=new NativeMeridianMesh();int n=units.Count;
        var near=new int[n];var far=new int[n];var box=new (V3 lo,V3 hi)[n];int worst=0,worstFar=0;var cells=new Dictionary<int,int>();
        for(int c=0;c<n;c++)
        {
            var u=units[c];
            NativeMeridianDress.Combine(plan,u.Cell,0,bank,modules,u.Placements,u.Quads,mesh);near[c]=mesh.Triangles;box[c]=(new V3(mesh.MinX,mesh.MinY,mesh.MinZ),new V3(mesh.MaxX,mesh.MaxY,mesh.MaxZ));
            Check(near[c]==u.Fine,name+": a chunk's counted LOD0 triangles are its mesh's");
            NativeMeridianDress.Combine(plan,u.Cell,2,bank,modules,u.Placements,u.Quads,mesh);far[c]=mesh.Triangles;Check(far[c]==u.Far,name+": a chunk's counted LOD1 triangles are its mesh's");
            Check(near[c]<=NativeMeridianDress.FineCap,name+FormattableString.Invariant($": the chunk at ({u.X0:F0},{u.Z0:F0}) {u.Size:F0} m has {near[c]:N0} LOD0 triangles (cap {NativeMeridianDress.FineCap:N0})"));
            Check(far[c]<=NativeMeridianDress.FarCap,name+FormattableString.Invariant($": the chunk at ({u.X0:F0},{u.Z0:F0}) {u.Size:F0} m has {far[c]:N0} LOD1 triangles (cap {NativeMeridianDress.FarCap:N0})"));
            worst=Math.Max(worst,near[c]);worstFar=Math.Max(worstFar,far[c]);cells.TryGetValue(u.Cell,out int k);cells[u.Cell]=k+1;
        }
        int split=cells.Count(kv=>kv.Value>1);
        if(Environment.GetEnvironmentVariable("MERIDIAN_DEBUG")!=null)
        {
            int wc=Array.IndexOf(near,worst);var byKey=new Dictionary<string,(int n,int t)>();var wu=units[wc];
            foreach(int pi in wu.Placements??new List<int>()){NativeMeridianDress.Combine(plan,wu.Cell,0,bank,modules,new List<int>{pi},null,mesh);var k=plan.Placements[pi].Key;byKey.TryGetValue(k,out var e);byKey[k]=(e.n+1,e.t+mesh.Triangles);}
            NativeMeridianDress.Combine(plan,wu.Cell,0,bank,modules,null,wu.Quads,mesh);
            Console.WriteLine(name+": "+n+" chunks in "+cells.Count+" cells ("+split+" cut); LOD1 worst "+worstFar);
            Console.WriteLine(name+FormattableString.Invariant($": heaviest chunk ({wu.X0:F0},{wu.Z0:F0}) {wu.Size:F0} m {worst}: surfaces {mesh.Triangles} ({wu.Quads?.Count??0}); ")+string.Join(", ",byKey.OrderByDescending(kv=>kv.Value.t).Take(14).Select(kv=>kv.Key+" "+kv.Value.n+"x="+kv.Value.t)));
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
        return text+FormattableString.Invariant($"; {n} chunks ({split} of {cells.Count} cells cut for the caps), the heaviest {worst:N0} triangles at LOD0, {worstFar:N0} at LOD1");
    }
}

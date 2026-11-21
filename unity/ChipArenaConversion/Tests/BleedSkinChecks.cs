using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// The bleed's facade skins on the live map (owner, 2026-11-20: "Weird building bug found near ward zero"). Where the street dress
// gives a facade bay to the bleed (Core NativeCityBleedDress.Covers: its skins stand over the whole bay), it left the bay empty, and
// Ward Zero's warp skin, its containment growth, has no wall of its own (tools/stages/meridian_bleed.py wall_growth: blooms only):
// the bay was a hole through the lot to the next street, its blooms floating in it. The quarantine sheeting hangs off its wall,
// the Ironworks' plates leave seams. Every street plan's first live deals, dressed with the bleed as the live run dresses them:
// every bay the street dress gives to the bleed is drawn by the lot's own wall under the skins, nowhere open.
internal static class BleedSkinChecks
{
    private static int count;
    private static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Bleed skins: "+what);}
    public const int DealsPerPlan=4;
    /// <summary>The Navigation's deal like the owner's (2026-11-20): Ward Zero behind the south door, four of its growth bays in a row
    /// under the quarantine sheeting given to the bleed.</summary>
    public const uint OwnersDeal=1126921887u;
    public static void Run(string root)
    {
        count=0;int given=0,growth=0;var report=new List<string>();
        for(int plan=0;plan<=NativeCityLayout.Plans.Count;plan++)
        {
            int planGiven=0;
            for(int k=0;k<(plan<NativeCityLayout.Plans.Count?DealsPerPlan:1);k++)
            {
                var live=plan<NativeCityLayout.Plans.Count?Deal(root,plan,null,k):DealOf(root,OwnersDeal);var world=live.World;var bleedPlan=NativeCityBleedDress.ForWalk(world.City,world.Bleed,world.Blueprint.worldSeed);
                var plain=HubZFightChecks.Dress(live,null,false);var dressed=HubZFightChecks.Dress(live,null,true);var B=new NativeMeridianDress.Solids(world.City.Map.blocks).B;
                string Key(NativeMeridianDress.Placement p)=>p.Block+":"+p.Face+":"+Math.Round(p.U0,2)+":"+Math.Round(p.V0,2);
                var kept=new HashSet<string>(dressed.Placements.Where(p=>p.Block>=0&&p.Face>=0).Select(Key));
                var wallsOf=new Dictionary<(int,int),List<NativeMeridianDress.Quad>>();
                foreach(var q in dressed.Quads){if(q.Block<0||Math.Abs(q.N.y)>.5f)continue;int f=Math.Abs(q.N.x)>.5f?(q.N.x>0?0:1):(q.N.z>0?4:5);
                    if(!wallsOf.TryGetValue((q.Block,f),out var l))wallsOf[(q.Block,f)]=l=new List<NativeMeridianDress.Quad>();l.Add(q);}
                foreach(var p in plain.Placements)
                {
                    if(p.Block<0||p.Face<0||!(p.Key.StartsWith("g.",StringComparison.Ordinal)||p.Key.StartsWith("u.",StringComparison.Ordinal))||kept.Contains(Key(p)))continue;
                    given++;planGiven++;var n=NativeMeridianDress.NormalOf(p.Face);
                    NativeMeridianDress.FaceFrame(B,p.Block*6,p.Face,out var fo,out var fu,out _);var uh=fu/fu.Length;
                    var skins=SkinsAt(bleedPlan,fo+uh*((p.U0+p.U1)*.5f)+new V3(0,(p.V0+p.V1)*.5f-fo.y,0),n);if(skins.Contains("wardzero.growth"))growth++;
                    // the bay's rectangle, sampled: each point on a surface of the lot's own face (its wall under the skins)
                    wallsOf.TryGetValue((p.Block,p.Face),out var ws);
                    foreach(float a in new[]{.1f,.5f,.9f})foreach(float c in new[]{.08f,.5f,.92f})
                    {
                        var at=fo+uh*(p.U0+(p.U1-p.U0)*a);at.y=p.V0+(p.V1-p.V0)*c;
                        bool walled=ws!=null&&ws.Any(q=>{var d=at-q.O;if(Math.Abs(V3.Dot(d,q.N))>.01f)return false;float lu=V3.Dot(q.U,q.U),lv=V3.Dot(q.V,q.V);if(lu<1e-6f||lv<1e-6f)return false;
                            float s1=V3.Dot(d,q.U)/lu,t=V3.Dot(d,q.V)/lv;return s1>=-.002f&&s1<=1.002f&&t>=-.002f&&t<=1.002f;});
                        Check(walled,FormattableString.Invariant($"{NativeCityLayout.Plans[live.Plan].Name} (deal {live.Seed}): the bay {p.Key} the street dress gives to the bleed's {string.Join("+",skins)} on {world.City.Map.blocks[p.Block].id} is open at ({at.x:F2},{at.y:F2},{at.z:F2}): no wall under the skins"));
                    }
                }
            }
            report.Add((plan<NativeCityLayout.Plans.Count?NativeCityLayout.Plans[plan].Name:"the owner's deal "+OwnersDeal)+" "+planGiven);
        }
        Check(given>0&&growth>=4,"the deals give the bleed some bays, four under Ward Zero's growth (the check has something to check)");
        Console.WriteLine("PASS bleed skins: "+count+" checks, "+given+" bays given to the bleed's skins over "+DealsPerPlan+" live deals a plan and the owner's ("+string.Join(", ",report)+"; "+growth+" under Ward Zero's growth), each walled under its skins");
    }
    /// <summary>The live deal of a run seed (the station's own path: a room, its roll, the generated world).</summary>
    public static HubZFightChecks.Live DealOf(string root,uint seed)
    {
        string Read(string name)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name));
        string mapsJson=Read("UnityMaps.json"),layoutJson=Read("ExpeditionLayout.json");
        var maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,new JsonSerializerOptions{IncludeFields=true});
        var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};
        var layout=NativeExpeditionLayout.Parse(layoutJson);layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
        var room=new ExpeditionDeploymentRoom("bleed-skins","pilot",seed,"black-siren",layout,content);
        Check(room.Execute("pilot",0,ExpeditionDeploymentIntent.RollInstances),"the roll of "+seed);
        return new HubZFightChecks.Live{World=NativeExpeditionGeneratedWorld.Build(room),Plan=room.Ticket.cityPlan,Seed=seed};
    }
    /// <summary>A live deal of a street plan with an instance behind one of its doors (the station's own path, seeds tried in turn).</summary>
    public static HubZFightChecks.Live Deal(string root,int planIndex,string instance,int skip=0)
    {
        string Read(string name)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name));
        string mapsJson=Read("UnityMaps.json"),layoutJson=Read("ExpeditionLayout.json");
        var maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,new JsonSerializerOptions{IncludeFields=true});
        var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};
        var layout=NativeExpeditionLayout.Parse(layoutJson);layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
        for(uint i=0;i<4096;i++)
        {
            uint seed=unchecked(i*2654435761u+12345u);if(NativeCityLayout.Choose(seed,-1)!=planIndex)continue;
            var room=new ExpeditionDeploymentRoom("bleed-skins","pilot",seed,"black-siren",layout,content);
            if(!room.Execute("pilot",0,ExpeditionDeploymentIntent.RollInstances))continue;
            var world=NativeExpeditionGeneratedWorld.Build(room);
            if(instance!=null&&!world.HallSlots.Contains(instance))continue;
            if(skip-->0)continue;
            return new HubZFightChecks.Live{World=world,Plan=room.Ticket.cityPlan,Seed=seed};
        }
        throw new Exception("no deal");
    }

    /// <summary>The skins (keys) the bleed's plan has over a point of a face turned to `normal`.</summary>
    public static List<string> SkinsAt(NativeCityBleedDress.Plan plan,V3 at,V3 normal)
    {
        var o=new List<string>();
        foreach(var p in plan.Pieces)
        {
            var k=NativeCityBleedDress.Kinds[p.Kind];if(k.Mount!=NativeCityBleedDress.Mount.Wall||!NativeCityBleedDress.Skin(k.Key))continue;
            float c=(float)Math.Cos(p.Yaw),s=(float)Math.Sin(p.Yaw);var n=new V3(s,0,c);if(V3.Dot(n,normal)<.99f)continue;
            var d=at-p.At;if(Math.Abs(V3.Dot(d,n))>.02f)continue;float x=d.x*c-d.z*s,y=d.y;
            if(x>=k.Min.x*p.Scale.x-1e-3f&&x<=k.Max.x*p.Scale.x+1e-3f&&y>=k.Min.y*p.Scale.y-1e-3f&&y<=k.Max.y*p.Scale.y+1e-3f)o.Add(k.Key);
        }
        return o;
    }
    // `--bleed-skins-probe PLAN INSTANCE DEALS`: the street dress's facade bays each deal gives to the bleed's skins, and the skins over
    // them (low, middle and high in the bay).
    public static void Probe(string root,int planIndex,string instance,int deals)
    {
        for(int k=0;k<deals;k++)
        {
            var live=Deal(root,planIndex,instance,k);var world=live.World;var bleedPlan=NativeCityBleedDress.ForWalk(world.City,world.Bleed,world.Blueprint.worldSeed);
            var plain=HubZFightChecks.Dress(live,null,false);var dressed=HubZFightChecks.Dress(live,null,true);
            string Key(NativeMeridianDress.Placement p)=>p.Block+":"+p.Face+":"+Math.Round(p.U0,2)+":"+Math.Round(p.V0,2);
            var kept=new HashSet<string>(dressed.Placements.Where(p=>p.Block>=0&&p.Face>=0).Select(Key));int given=0;var by=new Dictionary<string,int>();
            foreach(var p in plain.Placements)
            {
                if(p.Block<0||p.Face<0||!(p.Key.StartsWith("g.",StringComparison.Ordinal)||p.Key.StartsWith("u.",StringComparison.Ordinal))||kept.Contains(Key(p)))continue;given++;
                var along=new V3(p.M[0],p.M[4],p.M[8]);var up=new V3(p.M[1],p.M[5],p.M[9]);var n=new V3(p.M[2],p.M[6],p.M[10]);var at=new V3(p.M[3],p.M[7],p.M[11]);
                float h=p.V1-p.V0;var keys=new List<string>();foreach(float v in new[]{.12f,.5f,.88f})keys.AddRange(SkinsAt(bleedPlan,at+new V3(0,v*h,0),n));
                string sig=p.Key.Substring(0,1)+" "+string.Join("+",keys.Distinct().OrderBy(x=>x));by.TryGetValue(sig,out int c);by[sig]=c+1;
                if(given<=6)Console.WriteLine(FormattableString.Invariant($"   {world.City.Map.blocks[p.Block].id} {p.Key} at ({at.x:F1},{at.y:F1},{at.z:F1}) n ({n.x:F0},{n.z:F0}): {sig}"));
            }
            Console.WriteLine("deal "+live.Seed+" ("+string.Join("/",world.HallSlots)+"): "+given+" bays given; "+string.Join("; ",by.OrderByDescending(kv=>kv.Value).Select(kv=>kv.Key+" x"+kv.Value)));
        }
    }

    // `--bleed-skins-export PLAN INSTANCE OUT.ply [X Z RADIUS]` (CHIPARENA_MERIDIAN_BANK and CHIPARENA_BLEED_BANK set): the live deal's
    // street dress, drawn as the live run draws it with the bleed (its facade skins standing in for the dress's bays), and the bleed's
    // pieces, within RADIUS of (X, Z) (default: the instance's door), at LOD0, as one PLY for a Blender check render.
    public static void Export(string root,int planIndex,string instance,string path,float x,float z,float radius)
    {
        var bank=NativePackBank.Read(File.ReadAllBytes(Environment.GetEnvironmentVariable("CHIPARENA_MERIDIAN_BANK")));var modules=NativeMeridianDress.Modules(bank);
        var bleedBank=NativePackBank.Read(File.ReadAllBytes(Environment.GetEnvironmentVariable("CHIPARENA_BLEED_BANK")));
        var live=Deal(root,planIndex,instance,int.TryParse(Environment.GetEnvironmentVariable("BLEED_SKIP"),out int sk)?sk:0);var world=live.World;
        int door=Array.IndexOf(world.HallSlots,instance);var bleed=world.Bleed;
        var bleedPlan=NativeCityBleedDress.ForWalk(world.City,bleed,world.Blueprint.worldSeed);
        var dd=bleed.Doors.FirstOrDefault(q=>q.Instance==instance);if(float.IsNaN(x)&&dd!=null){x=dd.At.x;z=dd.At.z;}
        Console.WriteLine(FormattableString.Invariant($"deal seed {live.Seed}: slots {string.Join("/",world.HallSlots)}; {instance} behind door {door} at ({x:F1}, {z:F1})"));
        var skins=new Dictionary<string,int>();foreach(var p in bleedPlan.Pieces){var kk=NativeCityBleedDress.Kinds[p.Kind];if(kk.Mount==NativeCityBleedDress.Mount.Wall){skins.TryGetValue(kk.Key,out int q);skins[kk.Key]=q+1;}}
        Console.WriteLine("  wall pieces in the whole plan: "+string.Join(", ",skins.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+" "+kv.Value)));
        foreach(var p in bleedPlan.Pieces){var kk=NativeCityBleedDress.Kinds[p.Kind];if(NativeCityBleedDress.Skin(kk.Key))Console.WriteLine(FormattableString.Invariant($"  skin {kk.Key} at ({p.At.x:F2},{p.At.y:F2},{p.At.z:F2}) yaw {p.Yaw*180/Math.PI:F0} scale ({p.Scale.x:F2},{p.Scale.y:F2})"));}
        var plan=HubZFightChecks.Dress(live,modules.ContainsKey,true);
        var mesh=new NativeMeridianMesh();var P=new List<float>();var C=new List<byte>();var I=new List<int>();
        bool In(float px,float pz)=>(px-x)*(px-x)+(pz-z)*(pz-z)<=radius*radius;
        void Take(){int at=P.Count/3;for(int i=0;i<mesh.Vertices;i++){P.Add(mesh.P[i*3]);P.Add(mesh.P[i*3+1]);P.Add(mesh.P[i*3+2]);C.Add(mesh.C[i*4]);C.Add(mesh.C[i*4+1]);C.Add(mesh.C[i*4+2]);C.Add(255);}
            foreach(int k in mesh.Solid)I.Add(at+k);foreach(int k in mesh.Glow)I.Add(at+k);}
        NativeMeridianDress.ByChunk(plan,out var pl,out var qs);
        for(int c=0;c<plan.Nx*plan.Nz;c++)
        {
            float cx=plan.MinX+(c%plan.Nx+.5f)*NativeMeridianDress.ChunkSize,cz=plan.MinZ+(c/plan.Nx+.5f)*NativeMeridianDress.ChunkSize;
            if(!In(cx,cz)&&(cx-x)*(cx-x)+(cz-z)*(cz-z)>(radius+24)*(radius+24))continue;NativeMeridianDress.Combine(plan,c,0,bank,modules,pl[c],qs[c],mesh);Take();
        }
        int pieces=0;var used=new Dictionary<string,int>();
        foreach(var p in bleedPlan.Pieces)
        {
            if(!In(p.At.x,p.At.z))continue;var k=NativeCityBleedDress.Kinds[p.Kind];var prop=bleedBank.Find("rig/bleed/"+k.Key+"/0");if(prop==null)continue;
            float c=(float)Math.Cos(p.Yaw),s=(float)Math.Sin(p.Yaw);var M=new[]{c*p.Scale.x,0,s*p.Scale.z,p.At.x,0,p.Scale.y,0,p.At.y,-s*p.Scale.x,0,c*p.Scale.z,p.At.z};
            mesh.Clear();NativeMeridianDress.AddModule(bleedBank,bleedBank.Props.IndexOf(prop),M,mesh);Take();pieces++;used.TryGetValue(k.Key,out int u);used[k.Key]=u+1;
        }
        // the greybox the dress leaves (halls, landmarks)
        foreach(var blk in world.City.Map.blocks)
        {
            if(blk?.id==null||plan.Covers.Contains(blk.id)||!In(blk.pos.x,blk.pos.z)||blk.id.StartsWith("camp-",StringComparison.Ordinal))continue;
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
        Console.WriteLine("wrote "+path+": "+P.Count/3+" vertices, "+I.Count/3+" triangles; "+pieces+" bleed pieces: "+string.Join(", ",used.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+" "+kv.Value)));
    }
}

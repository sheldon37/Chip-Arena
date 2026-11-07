#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Stage kits (docs/arenas/STAGE-KITS.md; Core NativePackDressing and NativePackScenery, Runtime NativePackStage). The
    // licensed bank never reaches CI, so a synthetic bank stands in: every prop the table and the yards name, as a box of its
    // real bounds (the bank's, 2026-10-28), in its selection group. Against the real maps and the committed table: the table is
    // valid and names only selected solid props; each ruled block draws its kit inside its collider (nothing proud, no side or
    // top left more than 0.25 m short), the same way every time and whatever the block order; what keeps its box (no bank, no
    // entry, foundations, segments, open props, damage past the kept stage, fragments); which kit parts stop with a dressed
    // block; the yards stay outside the arena, the same every time, and the town's skyline makes room for its docks only while
    // they draw; the runtime wiring and the shader variant. With CA_PACK_BANK naming a real ChipArenaPackKit.bytes the plan and
    // the yards are checked against it too, with their triangle budgets.
    internal static class StageKitChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Stage kits: "+what);count++;}
        // The props' real bounds (min, max) in the kit bank.
        private static readonly (string name,float x0,float y0,float z0,float x1,float y1,float z1)[] Sizes={
            ("harbor-cage-barrels-01a",-1.0f,0.0f,-1.0f,1.0f,2.0f,1.0f),
            ("harbor-cage-barrels-01b",-1.0f,0.0f,-1.0f,1.0f,2.0f,1.0f),
            ("harbor-shipping-container-02a",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-02b",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-02c",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-02d",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-02e",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-02f",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-03a",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-03b",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-03c",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-03d",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-03e",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-03f",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-05a",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-05b",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-05c",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-05d",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-05e",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-05f",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-05g",-1.5f,0.0f,-3.0f,1.5f,3.0f,3.05f),
            ("harbor-shipping-container-06a",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-06b",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-06c",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-06d",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-06e",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-06f",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-container-06g",-1.5f,0.0f,-6.0f,1.5f,3.0f,6.05f),
            ("harbor-shipping-metal-crate-01a",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.014f),
            ("harbor-shipping-metal-crate-01b",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.014f),
            ("harbor-shipping-metal-crate-01c",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.014f),
            ("harbor-shipping-metal-crate-02a",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.014f),
            ("harbor-shipping-metal-crate-02b",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.014f),
            ("harbor-shipping-metal-crate-02c",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.014f),
            ("harbor-shipping-wooden-crate-01a",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-01b",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-01c",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-02a",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-02b",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-02c",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-03a",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-03b",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-shipping-wooden-crate-03c",-1.0f,0.006f,-1.002f,1.0f,2.0f,1.006f),
            ("harbor-wooden-pallet-01a",-1.006f,0.0f,-0.994f,1.002f,0.3f,0.982f),
            ("harbor-wooden-pallet-01b",-1.006f,0.0f,-0.994f,1.002f,0.3f,0.982f),
            ("harbor-wooden-pallet-02a",-0.99f,0.0f,-0.978f,0.994f,0.3f,0.982f),
            ("harbor-wooden-pallet-02b",-0.99f,0.0f,-0.978f,0.994f,0.3f,0.982f),
            ("harbor-barrel-01a",-0.402f,-0.0f,-0.395f,0.419f,1.196f,0.395f),
            ("harbor-barrel-05a",-0.44f,0.0f,-0.44f,0.44f,1.261f,0.44f),
            ("harbor-gas-barrel-01a",-0.403f,-0.0f,-0.409f,0.403f,1.005f,0.409f),
            ("harbor-metal-fence-01a",-1.958f,-0.2f,-0.058f,1.958f,2.6f,0.058f),
            ("harbor-plank-pile-01a",-1.914f,-0.007f,-1.571f,1.791f,0.248f,1.467f),
            ("harbor-streetlight-01a",-0.473f,-0.0f,-0.473f,3.686f,9.436f,0.473f),
            ("harbor-streetlight-01b",-0.473f,-0.0f,-0.473f,3.686f,9.436f,0.473f),
            ("harbor-truck-cabin-01a",-1.957f,-0.0f,-1.217f,1.894f,2.975f,6.029f),
            ("harbor-truck-cabin-01c",-1.957f,-0.0f,-1.217f,1.894f,2.975f,6.029f),
            ("harbor-truck-cabin-02b",-1.757f,-0.0f,-1.146f,1.757f,3.954f,6.04f),
            ("harbor-truck-trailer-01b",-1.725f,-0.0f,-10.924f,1.725f,1.875f,1.725f),
            ("harbor-truck-trailer-02a",-1.469f,-0.0f,-7.451f,1.469f,3.947f,1.8f),
            ("harbor-truck-trailer-02c",-1.469f,-0.0f,-7.451f,1.469f,3.947f,1.8f),
            ("harbor-gantry-crane-01a",-9.6f,-0.0f,-33.0f,9.405f,85.4f,79.0f),
            ("harbor-gantry-crane-01c",-9.6f,-0.0f,-33.0f,9.405f,85.4f,79.0f),
            ("harbor-harbor-crane-01a",-9.6f,0.0f,-23.412f,9.405f,35.0f,23.412f),
            ("harbor-harbor-crane-01e",-9.6f,0.0f,-23.412f,9.405f,35.0f,23.412f),
            ("harbor-freighter-02a",-12.504f,-7.616f,-82.11f,12.504f,27.382f,103.625f),
            ("harbor-freighter-03a-loaded",-12.712f,-7.184f,-79.22f,12.712f,32.619f,98.909f),
            ("harbor-tugboat-01a",-5.63f,-3.211f,-19.55f,5.718f,22.5f,16.629f),
            ("harbor-tugboat-03a",-5.63f,-3.211f,-19.55f,5.718f,12.6f,16.629f),
            ("harbor-metal-structure-kit-building-05a",-11.0f,0.0f,-22.3f,9.686f,18.726f,22.3f),
            ("harbor-liquid-tank-03a",-1.66f,0.0f,-1.72f,1.71f,4.0f,2.0f),("harbor-liquid-tank-03b",-1.66f,0.0f,-1.72f,1.71f,4.0f,2.0f),("harbor-liquid-tank-03c",-1.66f,0.0f,-1.72f,1.71f,4.0f,2.0f),
            ("harbor-brick-pile-01a",-0.5f,0.0f,-0.51f,0.49f,1.0f,0.51f),("harbor-brick-pile-01b",-0.5f,0.0f,-0.51f,0.49f,1.0f,0.51f),("harbor-brick-pile-02a",-0.49f,0.0f,-0.5f,0.49f,1.0f,0.5f),("harbor-brick-pile-02b",-0.49f,0.0f,-0.5f,0.49f,1.0f,0.5f),
            ("harbor-concrete-tetrapod-01a",-1.53f,-1.28f,-2.26f,2.48f,2.5f,2.27f),("harbor-concrete-tetrapod-01b",-1.53f,-1.28f,-2.26f,2.48f,2.5f,2.27f)};
        internal static NativePackBank.Bank Synthetic(Dictionary<string,string> groups)
        {
            var bank=new NativePackBank.Bank();bank.Textures.Add(new NativePackBank.Texture{Name="syn-atlas"});
            bank.Materials.Add(new NativePackBank.Material{Name="syn/Atlas",Albedo=0,Emission=-1,Color=Vector4.One});bank.Materials.Add(new NativePackBank.Material{Name="syn/Lights",Albedo=0,Emission=0,Color=Vector4.One,EmissionColor=new Vector3(1.5f)});
            foreach(var s in Sizes)
            {
                var lo=new Vector3(s.x0,s.y0,s.z0);var hi=new Vector3(s.x1,s.y1,s.z1);var p=new Vector3[8];for(int c=0;c<8;c++)p[c]=new Vector3((c&1)==0?lo.X:hi.X,(c&2)==0?lo.Y:hi.Y,(c&4)==0?lo.Z:hi.Z);
                int[] tri={0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};
                bank.Meshes.Add(new NativePackBank.Mesh{Name=s.name,Min=lo,Max=hi,Positions=p,Normals=p.Select(v=>Vector3.Normalize(v-(lo+hi)*.5f+new Vector3(1e-3f))).ToArray(),Uv0=new Vector2[8],
                    Submeshes=new[]{new NativePackBank.Submesh{Material=bank.Meshes.Count%5==0?1:0,Indices=tri}}});
                bank.PropIndex[s.name]=bank.Props.Count;bank.Props.Add(new NativePackBank.Prop{Name=s.name,Group=groups.TryGetValue(s.name,out var g)?g:"?",Min=lo,Max=hi,Parts=new[]{new NativePackBank.Part{Mesh=bank.Meshes.Count-1,Place=Matrix4x4.Identity}}});
            }
            return bank;
        }
        private static bool Same(PackPlan a,PackPlan b)
        {
            if(a.Dressed!=b.Dressed)return false;
            foreach(var pair in a.ById){var d=b.For(pair.Key);if(d==null||d.Pieces.Length!=pair.Value.Pieces.Length||d.Kit!=pair.Value.Kit||d.Keep!=pair.Value.Keep)return false;for(int i=0;i<d.Pieces.Length;i++)if(d.Pieces[i].Prop!=pair.Value.Pieces[i].Prop||d.Pieces[i].Place!=pair.Value.Pieces[i].Place)return false;}
            return true;
        }
        // Every dressed block: nothing proud of the box, the box's sides and top reached by the pieces within the gap rule, the base on the floor.
        internal static void Fits(PackPlan plan,ContentMap map,NativePackBank.Bank bank,string label)
        {
            foreach(var d in plan.ById.Values)
            {
                var b=map.blocks[d.Block];var h=new Vector3(b.size.x*.5f,b.size.y*.5f,b.size.z*.5f);Check(NativePackDressing.Inside(bank,d,b.size,out _)&&NativePackDressing.ProudOf(bank,d,b.size)<=1e-3f,label+" "+b.id+": inside its collider");
                var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);foreach(var p in d.Pieces){NativePackDressing.Bounds(bank.Props[p.Prop],p.Place,out var a,out var c);lo=Vector3.Min(lo,a);hi=Vector3.Max(hi,c);}
                const float g=NativePackDressing.MaxGap+.002f;
                Check(lo.X<=-h.X+g&&hi.X>=h.X-g&&lo.Z<=-h.Z+g&&hi.Z>=h.Z-g&&hi.Y>=h.Y-g&&MathF.Abs(lo.Y+h.Y)<.002f,label+" "+b.id+": the props reach every side and the top within "+NativePackDressing.MaxGap+" m and stand on its floor");
            }
        }
        public static void Run(string root)
        {
            var opts=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var set=JsonSerializer.Deserialize<PackDressingSet>(R("Assets/Conversion/Resources/PackDressing.json"),opts);NativePackDressing.Validate(set);
            var maps=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),opts).maps;
            using var sel=JsonDocument.Parse(R("tools/pack_bank/selections/kit-harbor.json"));var groups=new Dictionary<string,string>();
            foreach(var p in sel.RootElement.GetProperty("props").EnumerateArray())groups[p.GetProperty("id").GetString()]=p.GetProperty("group").GetString();
            Check(set.bank=="ChipArenaPackKit"&&sel.RootElement.GetProperty("bank").GetString()==set.bank&&R("Assets/ChipArena/Runtime/NativePackBanks.cs").Contains("Kit=\"ChipArenaPackKit\""),"the table names the kit bank its selection writes");

            // 1. The table: Unity-era maps only, selected solid props, every rule matching blocks on its map.
            foreach(var e in set.maps)Check(maps.Any(m=>m.id==e.map),e.map+" is a Unity-era map (UnityMaps.json)");
            foreach(var k in set.kits)foreach(var n in k.props.Concat(k.pallet??Array.Empty<string>()))Check(groups.TryGetValue(n,out var g)&&NativePackDressing.Solid.Contains(g),"kit "+k.id+": "+n+" is selected and solid");
            foreach(var e in set.maps)foreach(var r in e.rules??Array.Empty<PackRule>())Check(maps.Single(m=>m.id==e.map).blocks.Any(b=>NativePackDressing.Rule(e,b.id)==r),e.map+": the rule "+r.block+" dresses blocks");
            foreach(var k in set.kits)Check(set.maps.Any(m=>(m.rules??Array.Empty<PackRule>()).Any(r=>r.kit==k.id)),"the kit "+k.id+" is used");
            foreach(var n in NativePackScenery.Containers40.Concat(NativePackScenery.Containers20).Concat(NativePackScenery.Sts).Concat(NativePackScenery.Cranes).Concat(NativePackScenery.Ships).Concat(NativePackScenery.Tugs).Concat(NativePackScenery.Lamps)
                .Concat(NativePackScenery.ShipsLow).Concat(NativePackScenery.Fences).Concat(NativePackScenery.Scatter).Concat(NativePackScenery.Cabins).Concat(NativePackScenery.Trailers).Concat(NativePackScenery.Buildings).Concat(set.maps.SelectMany(m=>m.yards??Array.Empty<PackYard>()).SelectMany(y=>y.props??Array.Empty<string>())))
                Check(groups.ContainsKey(n),"the yards' "+n+" is selected");
            bool Throws(Action a){try{a();return false;}catch(InvalidOperationException){return true;}}
            Check(Throws(()=>NativePackDressing.Validate(new PackDressingSet{schema=2}))&&Throws(()=>NativePackDressing.Validate(new PackDressingSet{schema=1,kits=new[]{new PackKit{id="a",layout="pile",props=new[]{"x"}}}}))
                &&Throws(()=>NativePackDressing.Validate(new PackDressingSet{schema=1,kits=Array.Empty<PackKit>(),maps=new[]{new PackMapDressing{map="m",rules=new[]{new PackRule{block="b",kit="none"}}}}})),"a malformed table is refused");

            // 2. The plans against the synthetic bank: the expected blocks, inside their colliders, deterministic and varied.
            var bank=Synthetic(groups);var expected=new Dictionary<string,int>{["factory"]=16,["expedition-foundry"]=5,["expedition-city"]=20,["expedition-transit"]=0,["breakwater"]=56,["harbor"]=35,["skyframe"]=34,["rig"]=8,["polar"]=24};
            foreach(var e in set.maps)
            {
                var map=maps.Single(m=>m.id==e.map);var plan=NativePackDressing.Plan(set,map,bank);Check(plan!=null&&plan.Map==map.id&&plan.Glow==e.glow,e.map+": a plan");
                Check(plan.Dressed==expected[e.map]&&plan.Kept.Count==0,e.map+": "+plan.Dressed+" blocks dressed (expected "+expected[e.map]+"), kept: "+string.Join("; ",plan.Kept));
                Fits(plan,map,bank,e.map);Check(Same(plan,NativePackDressing.Plan(set,map,bank)),e.map+": the same plan every time");
                var flipped=new ContentMap{id=map.id,blocks=map.blocks.Reverse().ToArray(),segments=map.segments};Check(Same(plan,NativePackDressing.Plan(set,flipped,bank)),e.map+": the same props whatever the block order");
                foreach(var d in plan.ById.Values)Check(d.Keep==NativePackDressing.KeepDefault&&ReferenceEquals(plan.ByBlock[d.Block],d)&&d.Id==map.blocks[d.Block].id,e.map+" "+d.Id+": indexed, kept to S1");
            }
            var factory=maps.Single(m=>m.id=="factory");var fp=NativePackDressing.Plan(set,factory,bank);
            Check(fp.ById.Values.Where(d=>d.Kit=="crates-on-pallet-2x2").SelectMany(d=>d.Pieces).Select(p=>p.Prop).Distinct().Count()>=5,"the cover crates vary (the id's hash picks each crate)");
            Check(fp.For("factory-dock-a-container").Pieces.Length==1&&bank.Props[fp.For("factory-dock-a-container").Pieces[0].Prop].Group=="container"&&fp.For("factory-crate-n-0")?.Pieces.Length==5,"a dock container is one container, a cover crate four crates on a pallet");
            Check(fp.For("factory-wall-n-base")==null&&fp.For("factory-floor")==null&&fp.For("factory-column-n-0")==null,"walls, floors and columns keep their boxes");

            // 3. What keeps its box.
            Check(NativePackDressing.Plan(set,factory,null)==null&&NativePackDressing.Plan(null,factory,bank)==null&&NativePackDressing.Plan(set,maps.Single(m=>m.id=="brickfall"),bank)==null,"no bank, no table or no entry: no plan (today's boxes)");
            ContentBlock B(string id,string role,string material,float sx,float sy,float sz,string segment=null)=>new ContentBlock{id=id,role=role,material=material,pos=new V3(0,sy/2,0),size=new V3(sx,sy,sz),segment=segment};
            var probe=new ContentMap{id="probe",segments=new[]{new StructureDefinition{id="seg",blocks=new[]{"member"},supports=new[]{new StructureSupport{member="pier"}}}}};
            Check(NativePackDressing.Eligible(B("crate","cover","wood",2,1.2f,2),probe,out _)&&NativePackDressing.Eligible(B("bin","static","metal",2,1.2f,2),probe,out _),"static and cover props may dress");
            Check(!NativePackDressing.Eligible(B("slab","static","foundation",2,1,2),probe,out _)&&!NativePackDressing.Eligible(B("wall","structural","brick",2,3,.4f),probe,out _)&&!NativePackDressing.Eligible(B("rubble","rubble","stone",1,1,1),probe,out _)
                &&!NativePackDressing.Eligible(B("member","static","metal",1,1,1),probe,out _)&&!NativePackDressing.Eligible(B("pier","static","metal",1,1,1),probe,out _)&&!NativePackDressing.Eligible(B("x","static","metal",1,1,1,"seg"),probe,out _)
                &&!NativePackDressing.Eligible(B("hall","static","metal",14,3,2),probe,out _)&&!NativePackDressing.Eligible(B("tower","static","metal",2,7,2),probe,out _),"foundations, structural and rubble roles, collapse members and supports, and big blocks keep their boxes");
            var open=new PackKit{id="open",layout="fill",props=new[]{"harbor-shipping-container-02a"}};var openBank=Synthetic(groups);openBank.Props[openBank.PropIndex["harbor-shipping-container-02a"]].Group="scaffolding";
            Check(NativePackDressing.Dress(open,B("c","static","metal",2.4f,2.6f,6),openBank,out string why)==null&&why.StartsWith("open prop",StringComparison.Ordinal),"an open prop (a frame, fence or vehicle) never stands for a collider");
            var squat=new PackKit{id="squat",layout="fill",props=new[]{"harbor-cage-barrels-01a"}};Check(NativePackDressing.Dress(squat,B("tall","static","metal",1.2f,2.4f,1.2f),bank,out why)==null&&why.StartsWith("gap",StringComparison.Ordinal),"a prop that would leave the collider's top bare keeps the box");
            Check(NativePackDressing.Dress(new PackKit{id="m",layout="fill",props=new[]{"harbor-nothing"}},B("c","static","metal",2,2,2),bank,out why)==null&&why=="prop missing","a prop the bank lacks keeps the box");
            var crate=fp.For("factory-crate-n-0");Check(NativePackDressing.Draws(crate,0,false)&&NativePackDressing.Draws(crate,1,false)&&!NativePackDressing.Draws(crate,2,false)&&!NativePackDressing.Draws(crate,3,false)&&!NativePackDressing.Draws(crate,0,true)&&!NativePackDressing.Draws(null,0,false),"props at S0 and S1; the box's damage meshes from S2; fragments are boxes");

            // 4. Kit parts that stop with a dressed block (inside it, from below its top) and those that stay.
            var box=B("box","static","metal",2,1.2f,2);box.pos=new V3(10,.6f,4);
            Check(NativePackDressing.Covers(box,10,.6,4.98,.9,.5,.03,0,0,0,1)&&NativePackDressing.Covers(box,10.9,.3,4,.12,.3,.9,0,0,0,1),"a part on the box's face or edge stops with it");
            Check(!NativePackDressing.Covers(box,10,1.3,4,.8,.1,.8,0,0,0,1)&&!NativePackDressing.Covers(box,10,0,4,3,.01,3,0,0,0,1)&&!NativePackDressing.Covers(box,13,.6,4,.2,.2,.2,0,0,0,1),"a part resting on its top, a floor plate wider than it and a neighbour's part stay");
            var turned=B("t","static","metal",4,1,1);turned.pos=new V3(0,.5f,0);float s45=MathF.Sin(MathF.PI/4),c45=MathF.Cos(MathF.PI/4);turned.rotation=new ContentRotation{x=0,y=s45,z=0,w=c45};
            // Turned a quarter (Unity yaw 90): its 4 m side runs along the world z.
            Check(NativePackDressing.Covers(turned,0,.5,-1.8,.1,.1,.1,0,0,0,1)&&!NativePackDressing.Covers(turned,1.8,.5,0,.1,.1,.1,0,0,0,1)&&NativePackDressing.CoversLocal(turned,1.9,0,0,.1,.1,.1,0,0,0,1),"a turned block's parts are judged in its own frame");

            // 5. The yards: outside the arena, deterministic, and a yard inside the arena refused.
            foreach(var e in set.maps.Where(m=>m.yards!=null&&m.yards.Length>0))
            {
                var map=maps.Single(m=>m.id==e.map);var scene=NativePackScenery.Build(e,map,bank);var again=NativePackScenery.Build(e,map,bank);
                Check(scene.Props.Length>20&&scene.Props.Length==again.Props.Length&&scene.Props.Select((p,i)=>p.Prop==again.Props[i].Prop&&p.World==again.Props[i].World).All(x=>x),e.map+": "+scene.Props.Length+" yard props, the same every time");
                foreach(var p in scene.Props){NativePackDressing.Bounds(bank.Props[p.Prop],p.World,out var lo,out var hi);Check(!NativePackScenery.Enters(scene,lo.X,hi.X,lo.Z,hi.Z),e.map+": "+bank.Props[p.Prop].Name+" stays outside the arena");}
                foreach(var g in scene.Ground)for(int i=0;i+2<g.Positions.Length;i+=3)Check(g.Positions[i+1]<=.3f||g.Positions[i]<=scene.MinX+.02||g.Positions[i]>=scene.MaxX-.02||g.Positions[i+2]<=scene.MinZ+.02||g.Positions[i+2]>=scene.MaxZ-.02,e.map+": the yard's ground stays outside the arena");
                foreach(var b in map.blocks)Check(b.pos.x>=scene.MinX&&b.pos.x<=scene.MaxX&&b.pos.z>=scene.MinZ&&b.pos.z<=scene.MaxZ,e.map+": the keep-out holds every block");
                Check(scene.Props.Any(p=>bank.Props[p.Prop].Group=="crane-sts")||e.map=="expedition-transit"||e.map=="polar",e.map+": ship-to-shore cranes");
                var baked=NativePackScenery.Bake(bank,scene.Props);int verts=scene.Props.Sum(p=>bank.Props[p.Prop].Parts.Sum(q=>bank.Meshes[q.Mesh].Positions.Length));
                Check(baked.Sum(m=>m.Vertices)==verts&&baked.All(m=>m.Indices.All(i=>i>=0&&i<m.Vertices)&&m.Indices.Length%3==0),e.map+": the baked yard meshes hold every part");
            }
            var foundry=maps.Single(m=>m.id=="expedition-foundry");var inside=new PackMapDressing{map=foundry.id,yards=new[]{new PackYard{kind="stacks",x=0,z=0,length=25,depth=6.6f,height=2}}};
            Check(Throws(()=>NativePackScenery.Build(inside,foundry,bank))&&NativePackScenery.Build(inside,foundry,null).Props.Length==0,"a yard inside the arena is refused; no bank, no yards");

            // 6. The town's skyline: today's without the docks; with them, no lot under a yard and the west plate ending at the quay.
            var city=set.maps.Single(m=>m.map=="expedition-city");double quay=NativePackScenery.WestQuay(city);var today=NativeExpeditionCityScenery.Build(out var f0);var none=NativeExpeditionCityScenery.Build(out var f1,true,null,double.NaN);
            Check(!double.IsNaN(quay)&&today.Length==none.Length&&today.Select((b,i)=>b.X==none[i].X&&b.Y==none[i].Y&&b.Z==none[i].Z&&b.Hx==none[i].Hx).All(x=>x)&&f0.Length==f1.Length,"without the docks the skyline is today's");
            var docks=NativeExpeditionCityScenery.Build(out _,true,(x0,x1,z0,z1)=>NativePackScenery.Reserved(city,x0,x1,z0,z1),quay);
            Check(docks.Length<today.Length&&docks.Where(b=>b.Y-b.Hy>-.01f&&b.Y+b.Hy>.05f).All(b=>!NativePackScenery.Reserved(city,b.X-b.Hx,b.X+b.Hx,b.Z-b.Hz,b.Z+b.Hz)||b.Hy<.01f),"with the docks no skyline lot stands under a yard ("+(today.Length-docks.Length)+" boxes fewer)");
            Check(docks.Any(b=>Math.Abs(b.X-b.Hx-quay)<.01&&b.Y<0)&&!docks.Any(b=>b.Y<0&&b.X-b.Hx<quay-.01&&b.Z+b.Hz>NativeExpeditionCityScenery.EdgeMinZ&&b.Z-b.Hz<NativeExpeditionCityScenery.EdgeMaxZ),"the west plate ends at the quay (x "+quay+"): the basin's water shows");

            // 7. The runtime wiring and the shader variant (source scans).
            string rt(string f)=>R("Assets/ChipArena/Runtime/"+f);var dressing=rt("NativeArenaDressing.cs");var stage=rt("NativePackStage.cs");var shader=R("Assets/ChipArena/Resources/NativeBlockLit.shader");
            Check(dressing.Contains("if(e.Pack!=null&&stage<=e.Pack.Keep)")&&dressing.Contains("var dress=!prop.Fragment&&b.role!=\"rubble\"?packPlan?.For(b.id):null;")&&dressing.Contains("pack.Flush(instancing);")&&dressing.Contains("packPlan=dressed?NativePackStage.Plan(map):null;"),"the dressing draws a planned block's props up to its kept stage, never for fragments or rubble");
            Check(dressing.IndexOf("if(e.Pack!=null&&stage<=e.Pack.Keep)",StringComparison.Ordinal)>dressing.IndexOf("Posed.TryGetValue(p.Definition.id,out var pose)",StringComparison.Ordinal)&&dressing.IndexOf("if(e.Pack!=null",StringComparison.Ordinal)>dressing.IndexOf("Hidden.Contains(p.Definition.id))continue;",StringComparison.Ordinal),"props follow the block's pose and nudge and hide with it");
            Check(stage.Contains("public const string Keyword=\"BLOCK_PACK\";")&&stage.Contains("m.EnableKeyword(Keyword);")&&stage.Contains("JsonUtility.FromJson<PackDressingSet>(asset.text);NativePackDressing.Validate(s);")&&stage.Contains("var e=NativePackDressing.Entry(Set,mapId);return e!=null&&Bank!=null?e:null;"),"the runtime reads the validated table and gives nothing without the bank");
            Check(Count(stage,"Graphics.DrawMesh(p.mesh,Matrix4x4.identity,m,layer,null,0,block,ShadowCastingMode.Off,true)")==1&&stage.Contains("if(p.detail&&low||p.lowOnly&&!low)continue;")&&stage.Contains("const int layer=NativeSceneryBuilder.BackdropLayer;"),"the yards draw on the backdrop layer without shadows, the detail tier off on the low tier");
            Check(rt("NativeSourcePresentation.cs").Contains("PackYards(arena);")&&rt("NativeBackdropPresentation.cs").Contains("PackBackdrop(mapId);")&&rt("NativeFactoryPresentation.cs").Contains("NativePackStage.Strip(kitMap,parts);")
                &&rt("NativeIronworksPresentation.cs").Contains("||iwPack!=null&&iwPack[i])continue;")&&rt("NativeExpeditionCityPresentation.cs").Contains("||townPack!=null&&townPack[i])continue;")&&rt("NativeStagePrewarm.cs").Contains("NativePackStage.MaterialFor(0) is Material pm")&&rt("NativeStagePrewarm.cs").Contains("(packYards??=new NativePackYards()).Prepare(map,id);"),"the kits drop a dressed block's parts; the yards draw in play and behind the menu and build behind the loading screen; the variant is warmed");
            Check(Count(shader,"#pragma multi_compile_local __ BLOCK_PACK")==2&&shader.Contains("BlockSurf PackSurface(")&&shader.IndexOf("BlockSurf PackSurface(",StringComparison.Ordinal)<shader.IndexOf("// SHARED END",StringComparison.Ordinal)&&shader.Contains("float3 PackNormal(float3 n)")&&shader.Contains("clip(tex2D(_PackTex,i.uv).a-_PackCutoff);"),"NativeBlockLit's pack variant in both passes, its surface in the shared (harness) code");

            // 8. The real bank, when one is named (the owner's PC or the local session): the same plans and the yards' budgets.
            string real=Environment.GetEnvironmentVariable("CA_PACK_BANK");
            if(!string.IsNullOrEmpty(real)&&File.Exists(real))
            {
                var kit=NativePackBank.Read(File.ReadAllBytes(real));
                foreach(var e in set.maps)
                {
                    var map=maps.Single(m=>m.id==e.map);var plan=NativePackDressing.Plan(set,map,kit);Check(plan.Dressed==expected[e.map]&&plan.Kept.Count==0,e.map+" (real bank): "+plan.Dressed+" dressed, kept: "+string.Join("; ",plan.Kept));Fits(plan,map,kit,e.map+" (real bank)");
                    if(e.yards==null||e.yards.Length==0)continue;var scene=NativePackScenery.Build(e,map,kit);var baked=NativePackScenery.Bake(kit,scene.Props);
                    int all=baked.Where(m=>!m.LowOnly).Sum(m=>m.Indices.Length/3),low=baked.Where(m=>!m.Detail).Sum(m=>m.Indices.Length/3);
                    Check(all<=800000&&low<=300000&&low<all,e.map+" (real bank): the yards' "+all+" triangles ("+low+" on the low tier) within budget");
                    Console.WriteLine("  "+e.map+": "+plan.Dressed+" blocks as props ("+plan.ById.Values.Sum(d=>d.Pieces.Length)+" pieces), "+scene.Props.Length+" yard props, "+all+" yard triangles ("+low+" low), "+baked.Length+" yard meshes");
                }
            }
            Console.WriteLine("PASS stage kits: "+count+" checks"+(string.IsNullOrEmpty(real)?"":" (with the real bank)"));
        }
        private static int Count(string s,string what){int n=0;for(int i=s.IndexOf(what,StringComparison.Ordinal);i>=0;i=s.IndexOf(what,i+what.Length,StringComparison.Ordinal))n++;return n;}
    }
}
#endif
